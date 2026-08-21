using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed partial class AuthoritativeStore
{
    public static CredentialProfileProjection ReadCredentialProfileProjectionReadOnly(
        string productRoot,
        string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productRoot);
        ValidateCredentialIdentity(profileId, nameof(profileId));
        if (!Path.IsPathFullyQualified(productRoot))
        {
            throw new ArgumentException("The product root must be absolute.", nameof(productRoot));
        }

        string databasePath = Path.Combine(Path.GetFullPath(productRoot), "data", "infinium.sqlite3");
        if (!File.Exists(databasePath))
        {
            throw new InvalidDataException("The authoritative product database is absent.");
        }

        using StoragePaths paths = new(Path.GetFullPath(productRoot));
        paths.BindExistingWriteClass(ProductWriteClass.Data);
        using WindowsGuardedSqliteVfs sqliteVfs = new(
            paths,
            ProductWriteClass.Data,
            "infinium.sqlite3");
        string immutableDatabaseUri = "file:"
            + new Uri(databasePath).GetComponents(UriComponents.Path, UriFormat.UriEscaped)
            + "?immutable=1";
        using SqliteConnection source = new(new SqliteConnectionStringBuilder
        {
            DataSource = immutableDatabaseUri,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            Vfs = sqliteVfs.Name,
        }.ToString());
        source.Open();
        using (SqliteCommand queryOnly = source.CreateCommand())
        {
            queryOnly.CommandText = "PRAGMA query_only=ON;";
            queryOnly.ExecuteNonQuery();
        }

        using SqliteConnection readOnly = new(new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            Mode = SqliteOpenMode.Memory,
            Pooling = false,
        }.ToString());
        readOnly.Open();
        source.BackupDatabase(readOnly);
        using (SqliteCommand queryOnly = readOnly.CreateCommand())
        {
            queryOnly.CommandText = "PRAGMA query_only=ON;";
            queryOnly.ExecuteNonQuery();
        }
        using SqliteCommand command = readOnly.CreateCommand();
        command.CommandText =
            """
            SELECT p.profile_id,p.generation_id,g.generation_ordinal,p.revocation_epoch,
                   p.lifecycle_state,p.verification_state,p.capability_snapshot_id,
                   p.account_identity_id,p.billing_scope_identity_id,p.intent_id,
                   p.recovery_disposition,p.cleanup_disposition,p.projection_version,p.updated_at
            FROM provider_profile_projection p
            JOIN provider_generations g ON g.profile_id=p.profile_id AND g.generation_id=p.generation_id
            WHERE p.profile_id=$profile;
            """;
        command.Parameters.AddWithValue("$profile", profileId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("The authoritative credential profile projection is absent.");
        }
        CredentialProfileProjection projection = new(
            reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4),
            reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10), reader.GetString(11),
            reader.GetInt64(12), DateTimeOffset.Parse(reader.GetString(13),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind));
        if (reader.Read())
        {
            throw new InvalidDataException("The authoritative credential profile projection is ambiguous.");
        }
        sqliteVfs.VerifyAllGuards();
        return projection;
    }

    public (string? AccountIdentityId, string? BillingScopeIdentityId) ReadCredentialIdentityBinding(string profileId)
    {
        ValidateCredentialIdentity(profileId, nameof(profileId));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT account_identity_id,billing_scope_identity_id FROM provider_access_profiles WHERE profile_id=$profile;";
            command.Parameters.AddWithValue("$profile", profileId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("The authoritative credential profile is absent.");
            }
            string? account = reader.IsDBNull(0) ? null : reader.GetString(0);
            string? billing = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (reader.Read())
            {
                throw new InvalidOperationException("The authoritative credential profile is ambiguous.");
            }
            return (account, billing);
        }
    }

    public CredentialProfileProjection BeginCredentialEnrollment(
        string profileId,
        string generationId,
        string displayLabel,
        DateTimeOffset now,
        string? accountIdentityId = null,
        string? billingScopeIdentityId = null)
    {
        ValidateCredentialIdentity(profileId, nameof(profileId));
        ValidateCredentialIdentity(generationId, nameof(generationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);
        if (displayLabel.Length > 120)
        {
            throw new ArgumentException("The credential display label exceeds its closed bound.", nameof(displayLabel));
        }

        string root = $"enroll-{profileId}-{generationId}";
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            Execute("INSERT INTO provider_access_profiles VALUES($profile,'openai','responses',$label,$account,$billing,$now);",
                transaction, ("$profile", profileId), ("$label", displayLabel),
                ("$account", accountIdentityId), ("$billing", billingScopeIdentityId), ("$now", ToText(now)));
            Execute("INSERT INTO provider_generations VALUES($generation,$profile,1,0,$now);",
                transaction, ("$generation", generationId), ("$profile", profileId), ("$now", ToText(now)));
            InsertCredentialIntent(transaction, root + ":pending", profileId, generationId, "enroll", "pending",
                "none", "pending-enrollment", "none", null, null, null, now);
            Execute("INSERT INTO provider_credential_intent_events VALUES($event,$root,$intent,1,NULL,$now);",
                transaction, ("$event", root + ":event:1"), ("$root", root),
                ("$intent", root + ":pending"), ("$now", ToText(now)));
            Execute(
                "INSERT INTO provider_profile_projection VALUES($profile,$generation,0,'pending-enrollment','not-applicable',NULL,NULL,NULL,$intent,'not-required','not-requested',1,$now);",
                transaction, ("$profile", profileId), ("$generation", generationId),
                ("$intent", root + ":pending"), ("$now", ToText(now)));
            transaction.Commit();
        }
        return GetCredentialProfile(profileId);
    }

    public void AddCredentialGeneration(
        string profileId,
        string generationId,
        long generationOrdinal,
        long revocationEpoch,
        DateTimeOffset now)
    {
        ValidateCredentialIdentity(profileId, nameof(profileId));
        ValidateCredentialIdentity(generationId, nameof(generationId));
        RequirePositive(generationOrdinal, nameof(generationOrdinal));
        ArgumentOutOfRangeException.ThrowIfNegative(revocationEpoch);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            Execute("INSERT INTO provider_generations VALUES($generation,$profile,$ordinal,$epoch,$now);", transaction,
                ("$generation", generationId), ("$profile", profileId), ("$ordinal", generationOrdinal),
                ("$epoch", revocationEpoch), ("$now", ToText(now)));
            transaction.Commit();
        }
    }

    public bool CredentialGenerationExists(string profileId, string generationId)
    {
        ValidateCredentialIdentity(profileId, nameof(profileId));
        ValidateCredentialIdentity(generationId, nameof(generationId));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM provider_generations WHERE profile_id=$profile AND generation_id=$generation;";
            command.Parameters.AddWithValue("$profile", profileId);
            command.Parameters.AddWithValue("$generation", generationId);
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
        }
    }

    public CredentialProfileProjection BeginCredentialReplacement(
        string rootId,
        string profileId,
        string predecessorGenerationId,
        string successorGenerationId,
        long successorGenerationOrdinal,
        DateTimeOffset now)
    {
        ValidateCredentialIdentity(rootId, nameof(rootId));
        ValidateCredentialIdentity(profileId, nameof(profileId));
        ValidateCredentialIdentity(predecessorGenerationId, nameof(predecessorGenerationId));
        ValidateCredentialIdentity(successorGenerationId, nameof(successorGenerationId));
        RequirePositive(successorGenerationOrdinal, nameof(successorGenerationOrdinal));
        if (predecessorGenerationId == successorGenerationId)
        {
            throw new ArgumentException("Credential replacement requires a fresh generation identity.",
                nameof(successorGenerationId));
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            CredentialProfileProjection current = GetCredentialProfileCore(profileId, transaction);
            if (current.GenerationId != predecessorGenerationId
                || current.GenerationOrdinal + 1 != successorGenerationOrdinal
                || current.LifecycleState is not ("active-unverified" or "active-verified"))
            {
                throw new InvalidOperationException("The atomic replacement predecessor is stale or ineligible.");
            }
            Execute("INSERT INTO provider_generations VALUES($generation,$profile,$ordinal,$epoch,$now);", transaction,
                ("$generation", successorGenerationId), ("$profile", profileId),
                ("$ordinal", successorGenerationOrdinal), ("$epoch", current.RevocationEpoch), ("$now", ToText(now)));
            InsertCredentialIntent(transaction, rootId + ":pending", profileId, predecessorGenerationId,
                "replace", "pending", current.LifecycleState, "replacing", current.LifecycleState,
                current.CapabilitySnapshotId, current.AccountIdentityId, current.BillingScopeIdentityId, now,
                "unavailable", "not-required", "not-requested");
            Execute("INSERT INTO provider_credential_intent_events VALUES($event,$root,$intent,1,NULL,$now);", transaction,
                ("$event", rootId + ":event:1"), ("$root", rootId),
                ("$intent", rootId + ":pending"), ("$now", ToText(now)));
            InsertCredentialIntent(transaction, rootId + ":terminal", profileId, predecessorGenerationId,
                "replace", "completed", current.LifecycleState, "replacing", "replacing",
                current.CapabilitySnapshotId, current.AccountIdentityId, current.BillingScopeIdentityId, now.AddTicks(1),
                "unavailable", "not-required", "not-requested");
            Execute("INSERT INTO provider_credential_intent_events VALUES($event,$root,$intent,2,$prior,$now);", transaction,
                ("$event", rootId + ":event:2"), ("$root", rootId),
                ("$intent", rootId + ":terminal"), ("$prior", rootId + ":event:1"),
                ("$now", ToText(now.AddTicks(1))));
            Execute(
                """
                UPDATE provider_profile_projection SET lifecycle_state='replacing',verification_state='unavailable',
                  intent_id=$intent,recovery_disposition='not-required',
                  cleanup_disposition='not-requested',projection_version=projection_version+1,updated_at=$now
                WHERE profile_id=$profile;
                """,
                transaction, ("$intent", rootId + ":terminal"), ("$now", ToText(now.AddTicks(1))),
                ("$profile", profileId));
            transaction.Commit();
        }
        return GetCredentialProfile(profileId);
    }

    public CredentialProfileProjection ApplyCredentialTransition(CredentialTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCredentialIdentity(request.RootId, nameof(request.RootId));
        CredentialStateFields fields = CredentialStateFields.For(
            request.TerminalState,
            request.CapabilitySnapshotId,
            request.AccountIdentityId,
            request.BillingScopeIdentityId,
            request.SecureStoreUnavailable || request.Failed && request.IntentKind == "delete");
        string terminalKind = request.Cancelled ? "cancelled"
            : request.SecureStoreUnavailable ? "unavailable"
            : request.Failed ? "failed"
            : "completed";
        string terminalOutcome = request.Cancelled ? request.FromState
            : request.Failed && request.IntentKind == "delete" ? "delete-pending"
            : request.TerminalState;
        bool keepPending = request.IntentKind == "delete"
            && request.FromState != "delete-pending"
            && request.ToState == "delete-pending"
            && !request.Cancelled
            && !request.Failed
            && !request.SecureStoreUnavailable;

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            CredentialProfileProjection current = GetCredentialProfileCore(request.ProfileId, transaction);
            if (current.LifecycleState != request.FromState)
            {
                throw new InvalidOperationException("The credential transition predecessor is stale.");
            }
            InsertCredentialIntent(transaction, request.RootId + ":pending", request.ProfileId, request.GenerationId,
                request.IntentKind, "pending", request.FromState, request.ToState, request.FromState,
                fields.CapabilitySnapshotId, fields.AccountIdentityId, fields.BillingScopeIdentityId, request.PendingAt,
                fields.VerificationState, fields.RecoveryDisposition, fields.CleanupDisposition);
            Execute("INSERT INTO provider_credential_intent_events VALUES($event,$root,$intent,1,NULL,$now);", transaction,
                ("$event", request.RootId + ":event:1"), ("$root", request.RootId),
                ("$intent", request.RootId + ":pending"), ("$now", ToText(request.PendingAt)));

            if (!keepPending)
            {
                InsertCredentialIntent(transaction, request.RootId + ":terminal", request.ProfileId, request.GenerationId,
                    request.IntentKind, terminalKind, request.FromState, request.ToState, terminalOutcome,
                    fields.CapabilitySnapshotId, fields.AccountIdentityId, fields.BillingScopeIdentityId, request.TerminalAt,
                    fields.VerificationState, fields.RecoveryDisposition, fields.CleanupDisposition);
                Execute("INSERT INTO provider_credential_intent_events VALUES($event,$root,$intent,2,$prior,$now);", transaction,
                    ("$event", request.RootId + ":event:2"), ("$root", request.RootId),
                    ("$intent", request.RootId + ":terminal"), ("$prior", request.RootId + ":event:1"),
                    ("$now", ToText(request.TerminalAt)));
            }

            long revocationEpoch = request.IncrementRevocationEpoch
                ? checked(current.RevocationEpoch + 1)
                : current.RevocationEpoch;
            Execute(
                """
                UPDATE provider_profile_projection SET
                  generation_id=$generation,revocation_epoch=$epoch,lifecycle_state=$state,
                  verification_state=$verification,capability_snapshot_id=$capability,
                  account_identity_id=$account,billing_scope_identity_id=$billing,intent_id=$intent,
                  recovery_disposition=$recovery,cleanup_disposition=$cleanup,
                  projection_version=projection_version+1,updated_at=$now
                WHERE profile_id=$profile;
                """,
                transaction,
                ("$generation", request.GenerationId), ("$epoch", revocationEpoch), ("$state", terminalOutcome),
                ("$verification", fields.VerificationState), ("$capability", fields.CapabilitySnapshotId),
                ("$account", fields.AccountIdentityId), ("$billing", fields.BillingScopeIdentityId),
                ("$intent", terminalOutcome == "deleted" ? null
                    : request.RootId + (keepPending ? ":pending" : ":terminal")),
                ("$recovery", fields.RecoveryDisposition),
                ("$cleanup", fields.CleanupDisposition), ("$now", ToText(request.TerminalAt)),
                ("$profile", request.ProfileId));
            transaction.Commit();
        }
        return GetCredentialProfile(request.ProfileId);
    }

    public CredentialProfileProjection GetCredentialProfile(string profileId)
    {
        ValidateCredentialIdentity(profileId, nameof(profileId));
        lock (gate)
        {
            return GetCredentialProfileCore(profileId, null);
        }
    }

    internal bool IsCredentialReplacementCleanupRecovery(
        string profileId,
        string predecessorGenerationId,
        string successorGenerationId)
    {
        ValidateCredentialIdentity(profileId, nameof(profileId));
        ValidateCredentialIdentity(predecessorGenerationId, nameof(predecessorGenerationId));
        ValidateCredentialIdentity(successorGenerationId, nameof(successorGenerationId));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM provider_profile_projection projection
                JOIN provider_generations predecessor
                  ON predecessor.profile_id=projection.profile_id
                 AND predecessor.generation_id=projection.generation_id
                JOIN provider_generations successor
                  ON successor.profile_id=projection.profile_id
                 AND successor.generation_id=$successor
                 AND successor.generation_ordinal=predecessor.generation_ordinal+1
                WHERE projection.profile_id=$profile
                  AND projection.generation_id=$predecessor
                  AND projection.lifecycle_state='delete-pending'
                  AND projection.cleanup_disposition IN ('pending','failed')
                  AND EXISTS(
                    SELECT 1 FROM provider_credential_intents replacement
                    JOIN provider_credential_intent_events replacement_event
                      ON replacement_event.intent_id=replacement.intent_id
                    WHERE replacement.profile_id=projection.profile_id
                      AND replacement.generation_id=projection.generation_id
                      AND replacement.intent_kind='replace'
                      AND replacement.intent_state='completed'
                      AND replacement.to_lifecycle_state='replacing'
                      AND replacement.outcome_lifecycle_state='replacing');
                """;
            command.Parameters.AddWithValue("$profile", profileId);
            command.Parameters.AddWithValue("$predecessor", predecessorGenerationId);
            command.Parameters.AddWithValue("$successor", successorGenerationId);
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
        }
    }

    public IReadOnlyList<CredentialProfileProjection> RebuildCredentialProfileProjections(DateTimeOffset now)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            List<string> profiles = ReadStrings(
                "SELECT profile_id FROM provider_access_profiles ORDER BY profile_id;", transaction);
            foreach (string profile in profiles)
            {
                // Every mutable row is checked against the latest immutable event root
                // by schema triggers; a no-op read/rewrite is intentionally avoided.
                _ = GetCredentialProfileCore(profile, transaction);
            }
            transaction.Commit();
            return profiles.Select(GetCredentialProfile).ToArray();
        }
    }

    public IReadOnlyList<CredentialProfileProjection> MarkRestoredCredentialsRecoveryRequired(DateTimeOffset now)
    {
        List<CredentialProfileProjection> current;
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT profile_id FROM provider_profile_projection WHERE lifecycle_state NOT IN ('deleted','delete-pending') ORDER BY profile_id;";
            List<string> profileIds = [];
            {
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    profileIds.Add(reader.GetString(0));
                }
            }
            current = profileIds.Select(GetCredentialProfile).ToList();
        }
        List<CredentialProfileProjection> recovered = [];
        foreach (CredentialProfileProjection profile in current)
        {
            DateTimeOffset authorityFloor = GetCredentialAuthorityTimeFloor(profile.ProfileId);
            DateTimeOffset transitionAt = (now > authorityFloor ? now : authorityFloor).AddTicks(1);
            recovered.Add(ApplyCredentialTransition(new(
                $"restore-recovery-{profile.ProfileId}-{profile.GenerationId}-{transitionAt.UtcTicks}",
                profile.ProfileId,
                profile.GenerationId,
                "recover",
                profile.LifecycleState,
                "recovery-required",
                "recovery-required",
                profile.CapabilitySnapshotId,
                profile.AccountIdentityId,
                profile.BillingScopeIdentityId,
                transitionAt,
                transitionAt.AddTicks(1),
                IncrementRevocationEpoch: true)));
        }
        return recovered;
    }

    private DateTimeOffset GetCredentialAuthorityTimeFloor(string profileId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT max(authority_time) FROM (
                  SELECT i.created_at AS authority_time
                  FROM provider_credential_intents i WHERE i.profile_id=$profile
                  UNION ALL
                  SELECT e.created_at
                  FROM provider_credential_intent_events e
                  JOIN provider_credential_intents i ON i.intent_id=e.intent_id
                  WHERE i.profile_id=$profile
                  UNION ALL
                  SELECT p.updated_at
                  FROM provider_profile_projection p WHERE p.profile_id=$profile);
                """;
            command.Parameters.AddWithValue("$profile", profileId);
            object? value = command.ExecuteScalar();
            if (value is not string authorityTime)
            {
                throw new InvalidDataException("Credential authority time is absent for a restored profile.");
            }
            return DateTimeOffset.Parse(
                authorityTime,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }
    }

    private CredentialProfileProjection GetCredentialProfileCore(string profileId, SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT p.profile_id,p.generation_id,g.generation_ordinal,p.revocation_epoch,
                   p.lifecycle_state,p.verification_state,p.capability_snapshot_id,
                   p.account_identity_id,p.billing_scope_identity_id,p.intent_id,
                   p.recovery_disposition,p.cleanup_disposition,p.projection_version,p.updated_at
            FROM provider_profile_projection p
            JOIN provider_generations g ON g.profile_id=p.profile_id AND g.generation_id=p.generation_id
            WHERE p.profile_id=$profile;
            """;
        command.Parameters.AddWithValue("$profile", profileId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException("The credential profile projection is absent.");
        }
        return new(
            reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4),
            reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10), reader.GetString(11),
            reader.GetInt64(12), DateTimeOffset.Parse(reader.GetString(13), System.Globalization.CultureInfo.InvariantCulture));
    }

    private void InsertCredentialIntent(
        SqliteTransaction transaction, string intentId, string profileId, string generationId,
        string kind, string state, string from, string to, string outcome,
        string? capability, string? account, string? billing, DateTimeOffset now,
        string? verification = null, string? recovery = null, string? cleanup = null)
    {
        CredentialStateFields fields = verification is null
            ? CredentialStateFields.For(to, capability, account, billing, false)
            : new(verification, recovery!, cleanup!, capability, account, billing);
        Execute(
            """
            INSERT INTO provider_credential_intents VALUES(
              $intent,$profile,$generation,$kind,$state,$from,$to,$outcome,$verification,
              $account,$billing,$capability,$recovery,$cleanup,$now);
            """,
            transaction, ("$intent", intentId), ("$profile", profileId), ("$generation", generationId),
            ("$kind", kind), ("$state", state), ("$from", from), ("$to", to), ("$outcome", outcome),
            ("$verification", fields.VerificationState), ("$account", fields.AccountIdentityId),
            ("$billing", fields.BillingScopeIdentityId), ("$capability", fields.CapabilitySnapshotId),
            ("$recovery", fields.RecoveryDisposition), ("$cleanup", fields.CleanupDisposition),
            ("$now", ToText(now)));
    }

    private List<string> ReadStrings(string sql, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> values = [];
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }
        return values;
    }

    private static void ValidateCredentialIdentity(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        if (value.Length > 120 || value.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_'))
        {
            throw new ArgumentException("Credential identities use 1-120 closed ASCII token characters.", name);
        }
    }

    private sealed record CredentialStateFields(
        string VerificationState, string RecoveryDisposition, string CleanupDisposition,
        string? CapabilitySnapshotId, string? AccountIdentityId, string? BillingScopeIdentityId)
    {
        public static CredentialStateFields For(
            string state, string? capability, string? account, string? billing, bool unavailable) => state switch
            {
                "pending-enrollment" => new("not-applicable", "not-required", "not-requested", null, null, null),
                "active-verified" => new("available", "not-required", "not-requested", capability, account, billing),
                "active-unverified" or "replacing" or "disabled" =>
                    new("unavailable", "not-required", "not-requested", capability, account, billing),
                "delete-pending" => new("unavailable", "not-required", unavailable ? "failed" : "pending", capability, account, billing),
                "deleted" => new("unavailable", "not-required", "confirmed", null, null, null),
                "secure-store-unavailable" => new("unavailable", "unavailable", "not-requested", capability, account, billing),
                "recovery-required" => new("unavailable", "required", "not-requested", capability, account, billing),
                _ => throw new ArgumentException("The credential lifecycle state is outside the accepted closed set.", nameof(state)),
            };
    }
}
