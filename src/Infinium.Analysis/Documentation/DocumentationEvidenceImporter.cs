using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Documentation;

public static class DocumentationEvidenceImporter
{
    private const int MaximumSourceBytes = 8 * 1024 * 1024;
    private const long MaximumContradictionClosureWork = 1_000_000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static DocumentationEvidenceContract Import(DocumentationImportRequestContract request)
    {
        ArgumentNullException.ThrowIfNull(request);
        DocumentationClaimImportContractInvariants.Validate(request.Manifest);
        ValidateApplicationTargets(request);
        if (request.Mode == DocumentationImportMode.Unspecified)
        {
            throw new InvalidOperationException("Documentation import mode must be explicit.");
        }
        if (request.Mode == DocumentationImportMode.RetainedReuse)
        {
            return Reuse(request);
        }
        if (request.RetainedEvidence is not null)
        {
            throw new InvalidOperationException("Clean import cannot consume retained extraction output.");
        }

        if (request.Manifest.Availability == DocumentationSourceAvailability.Deleted)
        {
            throw new InvalidOperationException(
                "Deletion receipts require a retained-reuse operation over previously admitted evidence.");
        }

        DocumentationClaimImportManifestContract manifest = request.Manifest;
        ReadOnlyMemory<byte>? sourceBytes = request.SourceBytes;
        bool present = manifest.Availability == DocumentationSourceAvailability.Present;
        if (present != sourceBytes.HasValue)
        {
            throw new InvalidDataException("Present documentation requires retained bytes; deleted or unavailable documentation must not supply bytes.");
        }

        if (!present && (manifest.Claims.Count != 0 || manifest.Applications.Count != 0))
        {
            throw new InvalidDataException("Unavailable source bytes cannot authorize new passages, claims, applications, or purpose assignments.");
        }

        if (sourceBytes is { } retained)
        {
            if (retained.Length > MaximumSourceBytes)
            {
                throw new InvalidDataException("Documentation source exceeds the bounded 8 MiB import limit.");
            }

            try
            {
                _ = StrictUtf8.GetString(retained.Span);
            }
            catch (DecoderFallbackException exception)
            {
                throw new DocumentationImportFailureException(
                    "invalid-utf8",
                    "Documentation source bytes are not valid UTF-8.",
                    exception);
            }
            string actualFingerprint = Hash(retained.Span);
            if (retained.Length != manifest.ByteLength
                || !StringComparer.Ordinal.Equals(actualFingerprint, manifest.ByteFingerprint.Value))
            {
                throw new DocumentationImportFailureException(
                    "source-identity-mismatch",
                    "Documentation bytes do not match the declared length and SHA-256 identity.");
            }
        }

        OpaqueId revisionId = StableId(
            "docrev",
            manifest.SourceId.Value,
            SourceKindToken(manifest.SourceKind),
            manifest.SourceRevision,
            manifest.ByteFingerprint.Value,
            manifest.ByteLength.ToString(CultureInfo.InvariantCulture));
        AnalysisResultState retentionState = manifest.Availability switch
        {
            DocumentationSourceAvailability.Present => AnalysisResultState.Present,
            DocumentationSourceAvailability.Deleted => AnalysisResultState.Unavailable,
            DocumentationSourceAvailability.Unavailable => AnalysisResultState.Unavailable,
            _ => throw new InvalidOperationException("Source availability must be closed."),
        };
        ReplayState replayState = manifest.Availability switch
        {
            DocumentationSourceAvailability.Present => ReplayState.CompleteClean,
            DocumentationSourceAvailability.Deleted => ReplayState.AuditOnly,
            DocumentationSourceAvailability.Unavailable => ReplayState.Unavailable,
            _ => throw new InvalidOperationException("Source availability must be closed."),
        };
        DocumentationRevisionContract revision = new(
            revisionId,
            manifest.SourceId,
            manifest.SourceKind,
            manifest.SourceRevision,
            manifest.ByteFingerprint,
            manifest.ByteLength,
            manifest.SupplyingSnapshotId,
            retentionState,
            replayState);

        OpaqueId importId = StableId(
            "docimport",
            request.ImportRunId.Value,
            revisionId.Value,
            ImportModeToken(request.Mode),
            request.DependencyClosureId.Value,
            request.ExtractorId.Value);
        DocumentationImportContract import = new(
            importId,
            request.ImportRunId,
            revisionId,
            request.Mode,
            null,
            request.DependencyClosureId,
            request.ExtractorId,
            LlmInvolvementState.None,
            LlmOperation.None,
            NotUsedBoundaries(),
            request.ImportedAt);

        Dictionary<OpaqueId, DocumentationClaimInputContract> inputsByKey = manifest.Claims
            .ToDictionary(item => item.ClaimKey);
        List<DocumentationPassageContract> passages = [];
        Dictionary<(long Start, long End), (string Text, DocumentationPassageContract Passage)> passagesByRange = [];
        Dictionary<OpaqueId, DocumentationPassageContract> passagesByClaimKey = [];
        Dictionary<OpaqueId, OpaqueId> claimBaseIds = [];
        List<DocumentationClaimContract> claims = [];
        Dictionary<OpaqueId, DocumentationClaimContract> claimsByKey = [];
        foreach (DocumentationClaimInputContract input in manifest.Claims.OrderBy(item => item.ClaimKey.Value, StringComparer.Ordinal))
        {
            (long Start, long End) rangeKey = (input.Utf8StartOffset, input.Utf8EndOffset);
            if (!passagesByRange.TryGetValue(
                    rangeKey,
                    out (string Text, DocumentationPassageContract Passage) cachedPassage))
            {
                ReadOnlySpan<byte> passageBytes = sourceBytes!.Value.Span[
                    checked((int)input.Utf8StartOffset)..checked((int)input.Utf8EndOffset)];
                string decodedText;
                try
                {
                    decodedText = StrictUtf8.GetString(passageBytes);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new DocumentationImportFailureException(
                        "invalid-passage-utf8",
                        "A documentation passage boundary splits or contains invalid UTF-8.",
                        exception);
                }

                Sha256Fingerprint passageFingerprint = new(Hash(passageBytes));
                OpaqueId passageId = StableId(
                    "docpass",
                    revisionId.Value,
                    input.Utf8StartOffset.ToString(CultureInfo.InvariantCulture),
                    input.Utf8EndOffset.ToString(CultureInfo.InvariantCulture),
                    passageFingerprint.Value);
                DocumentationPassageContract newPassage = new(
                    passageId,
                    revisionId,
                    input.Utf8StartOffset,
                    input.Utf8EndOffset,
                    passageFingerprint,
                    AnalysisResultState.Present);
                cachedPassage = (decodedText, newPassage);
                passagesByRange.Add(rangeKey, cachedPassage);
                passages.Add(newPassage);
            }

            if (!StringComparer.Ordinal.Equals(cachedPassage.Text, input.ExactText))
            {
                throw new DocumentationImportFailureException(
                    "passage-text-mismatch",
                    "A declared claim does not exactly match its retained UTF-8 passage bytes.");
            }

            DocumentationPassageContract passage = cachedPassage.Passage;
            passagesByClaimKey.Add(input.ClaimKey, passage);
            claimBaseIds.Add(input.ClaimKey, StableId(
                "docclaimbase",
                importId.Value,
                passage.PassageId.Value,
                ClaimKindToken(input.Kind),
                input.ExactText,
                Canonical(input.Conditions),
                AuthorityToken(input.Authority),
                ApplicabilityToken(input.Applicability),
                RoleToken(input.ClassificationRole)));
        }

        long contradictionClosureWork = 0;
        Dictionary<OpaqueId, OpaqueId> claimIds = manifest.Claims.ToDictionary(
            input => input.ClaimKey,
            input => StableId(
                "docclaim",
                claimBaseIds[input.ClaimKey].Value,
                Canonical(input.ContradictingClaimKeys.Select(key => claimBaseIds[key].Value)),
                ContradictionClosureDescriptor(
                    input,
                    inputsByKey,
                    claimBaseIds,
                    ref contradictionClosureWork)));
        foreach (DocumentationClaimInputContract input in manifest.Claims.OrderBy(item => item.ClaimKey.Value, StringComparer.Ordinal))
        {
            DocumentationPassageContract passage = passagesByClaimKey[input.ClaimKey];
            OpaqueId claimId = claimIds[input.ClaimKey];
            DocumentationClaimContract claim = new(
                claimId,
                importId,
                passage.PassageId,
                input.Kind,
                input.ExactText,
                input.Conditions.Order(StringComparer.Ordinal).ToArray(),
                input.Authority,
                input.Applicability,
                input.ClassificationRole,
                input.ContradictingClaimKeys
                    .Select(key => claimIds[key])
                    .OrderBy(item => item.Value, StringComparer.Ordinal)
                    .ToArray());
            claims.Add(claim);
            claimsByKey.Add(input.ClaimKey, claim);
        }

        List<ClaimApplicationContract> applications = [];
        List<DocumentationPurposeAssignmentContract> purposeAssignments = [];
        foreach (DocumentationApplicationInputContract input in manifest.Applications
                     .OrderBy(item => item.ClaimKey.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.ConsumingRunId.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.SubjectId.Value, StringComparer.Ordinal))
        {
            DocumentationClaimContract claim = claimsByKey[input.ClaimKey];
            OpaqueId[] evidenceIds = input.SupportingClaimKeys
                .Select(key => claimsByKey[key].ClaimId)
                .Append(claim.ClaimId)
                .Distinct()
                .OrderBy(item => item.Value, StringComparer.Ordinal)
                .ToArray();
            OpaqueId applicationId = StableId(
                "docapply",
                claim.ClaimId.Value,
                input.ConsumingRunId.Value,
                input.AnalysisContextId.Value,
                input.SubjectId.Value,
                input.SubjectType,
                input.DependencyClosureId.Value,
                ApplicabilityToken(input.Applicability),
                Canonical(evidenceIds.Select(item => item.Value)));
            ClaimApplicationContract application = new(
                applicationId,
                claim.ClaimId,
                input.ConsumingRunId,
                input.AnalysisContextId,
                input.SubjectId,
                input.SubjectType,
                input.DependencyClosureId,
                input.Applicability,
                evidenceIds);
            applications.Add(application);

            if (input.DeclaredPurpose is null)
            {
                continue;
            }

            if (claim.Kind != ClaimKind.DeclaredPurpose
                || claim.Authority != EvidenceAuthority.AuthoritativeExternal
                || claim.ClassificationRole != ClassificationRole.Declared
                || claim.Applicability != ClaimApplicabilityState.Applicable
                || input.Applicability != ClaimApplicabilityState.Applicable)
            {
                throw new InvalidDataException("A declared-purpose assignment requires an applicable authoritative declared-purpose claim and application.");
            }

            DocumentationPurposeInputContract purpose = input.DeclaredPurpose;
            OpaqueId assignmentId = StableId(
                "purpose",
                applicationId.Value,
                purpose.Code,
                Canonical(purpose.ApplicabilityConditionIds.Select(key => claimsByKey[key].ClaimId.Value)),
                purpose.AnalyzerOrAdjudicatorId.Value,
                request.ImportedAt.ToString(),
                purpose.Reason);
            purposeAssignments.Add(new DocumentationPurposeAssignmentContract(
                assignmentId,
                ContractConstants.TaxonomyId,
                new ContractVersion(0, 1, 0),
                "declared-purpose-and-intended-feature-area",
                "purpose-kind",
                purpose.Code,
                TaxonomyApplicability.Assigned,
                input.SubjectId,
                input.SubjectType,
                ClassificationRole.Declared,
                claim.ClaimId,
                applicationId,
                purpose.ApplicabilityConditionIds
                    .Select(key => claimsByKey[key].ClaimId)
                    .OrderBy(item => item.Value, StringComparer.Ordinal)
                    .ToArray(),
                purpose.AnalyzerOrAdjudicatorId,
                request.ImportedAt,
                purpose.Reason));
        }

        List<DocumentationGapContract> gaps = BuildGaps(
            manifest,
            revisionId,
            claims,
            applications,
            request.OriginatingRunId,
            request.ImportedAt,
            retainedReuse: false);
        passages = passages.OrderBy(item => item.PassageId.Value, StringComparer.Ordinal).ToList();
        claims = claims.OrderBy(item => item.ClaimId.Value, StringComparer.Ordinal).ToList();
        applications = applications.OrderBy(item => item.ApplicationId.Value, StringComparer.Ordinal).ToList();
        purposeAssignments = purposeAssignments
            .OrderBy(item => item.AssignmentId.Value, StringComparer.Ordinal)
            .ToList();
        List<DocumentationDeletionReceiptContract> deletionReceipts = BuildDeletionReceipts(
            manifest,
            revisionId,
            passages,
            request.OriginatingRunId,
            request.ImportedAt);
        DocumentationEvidenceContract result = new(
            ContractConstants.DocumentationEvidenceSchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId("docevidence-pending"),
            request.OriginatingRunId,
            [revision],
            [import],
            passages,
            claims,
            applications,
            purposeAssignments,
            deletionReceipts,
            gaps,
            []);
        result = result with { PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(result) };
        DocumentationEvidenceContractInvariants.Validate(result);
        return result;
    }

    private static DocumentationEvidenceContract Reuse(DocumentationImportRequestContract request)
    {
        DocumentationEvidenceContract retained = request.RetainedEvidence
            ?? throw new InvalidOperationException("Retained reuse requires a retained documentation evidence payload.");
        DocumentationEvidenceContractInvariants.Validate(retained);
        if (request.SourceBytes is not null
            || request.Manifest.Claims.Count != 0
            || request.Manifest.Applications.Count != 0)
        {
            throw new InvalidOperationException("Retained reuse consumes retained extraction output and must not re-import bytes or claim declarations.");
        }
        if (retained.Revisions.Count != 1)
        {
            throw new InvalidOperationException("Retained reuse requires exactly one retained source revision.");
        }

        DocumentationRevisionContract revision = retained.Revisions[0];
        DocumentationClaimImportManifestContract manifest = request.Manifest;
        if (revision.SourceId != manifest.SourceId
            || revision.SourceKind != manifest.SourceKind
            || !StringComparer.Ordinal.Equals(revision.SourceRevision, manifest.SourceRevision)
            || revision.ByteFingerprint != manifest.ByteFingerprint
            || revision.ByteLength != manifest.ByteLength
            || revision.SupplyingSnapshotId != manifest.SupplyingSnapshotId)
        {
            throw new InvalidDataException("Retained reuse source identity does not match the retained revision.");
        }

        OpaqueId importId = StableId(
            "docimport",
            request.ImportRunId.Value,
            revision.RevisionId.Value,
            ImportModeToken(request.Mode),
            request.DependencyClosureId.Value,
            request.ExtractorId.Value);
        DocumentationImportContract import = new(
            importId,
            request.ImportRunId,
            revision.RevisionId,
            request.Mode,
            retained.Imports.Single().ImportId,
            request.DependencyClosureId,
            request.ExtractorId,
            LlmInvolvementState.None,
            LlmOperation.None,
            NotUsedBoundaries(),
            request.ImportedAt);
        List<DocumentationGapContract> gaps = [];
        if (manifest.Availability is DocumentationSourceAvailability.Deleted
            or DocumentationSourceAvailability.Unavailable)
        {
            DocumentationClaimImportManifestContract lossManifest = manifest with
            {
                Claims = [],
                Applications = [],
            };
            gaps.AddRange(BuildGaps(
                lossManifest,
                revision.RevisionId,
                retained.Claims,
                retained.Applications,
                request.OriginatingRunId,
                request.ImportedAt,
                retainedReuse: true));
            gaps = gaps.DistinctBy(item => item.GapId).OrderBy(item => item.GapId.Value, StringComparer.Ordinal).ToList();
        }

        List<DocumentationDeletionReceiptContract> deletionReceipts = BuildDeletionReceipts(
            manifest,
            revision.RevisionId,
            retained.Passages,
            request.OriginatingRunId,
            request.ImportedAt);

        DocumentationEvidenceContract result = retained with
        {
            PayloadId = new OpaqueId("docevidence-pending"),
            OriginatingRunId = request.OriginatingRunId,
            Imports = [import],
            DeletionReceipts = deletionReceipts,
            Gaps = gaps,
        };
        result = result with { PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(result) };
        DocumentationEvidenceContractInvariants.Validate(result);
        return result;
    }

    private static List<DocumentationGapContract> BuildGaps(
        DocumentationClaimImportManifestContract manifest,
        OpaqueId revisionId,
        IReadOnlyList<DocumentationClaimContract> claims,
        IReadOnlyList<ClaimApplicationContract> applications,
        OpaqueId originatingRunId,
        UtcTimestamp createdAt,
        bool retainedReuse)
    {
        List<DocumentationGapContract> gaps = [];
        foreach (DocumentationClaimContract claim in claims.Where(item =>
                     item.Applicability == ClaimApplicabilityState.Contradicted
                     || item.ContradictingEvidenceIds.Count != 0))
        {
            gaps.Add(Gap(
                DocumentationGapKind.Contradiction,
                revisionId,
                claim.ClaimId,
                null,
                ReplayState.CompleteClean,
                "The retained external claim has explicit contradicting evidence or contradicted applicability.",
                originatingRunId,
                createdAt));
        }

        foreach (ClaimApplicationContract application in applications.Where(item =>
                     item.Applicability == ClaimApplicabilityState.Contradicted))
        {
            gaps.Add(Gap(
                DocumentationGapKind.Contradiction,
                revisionId,
                application.ClaimId,
                application.ApplicationId,
                ReplayState.CompleteClean,
                "The consuming analysis application is explicitly contradicted.",
                originatingRunId,
                createdAt));
        }

        if (manifest.Availability == DocumentationSourceAvailability.Deleted)
        {
            gaps.Add(Gap(
                DocumentationGapKind.Deletion,
                revisionId,
                null,
                null,
                ReplayState.AuditOnly,
                "The source body was explicitly deleted; its fingerprint remains but exact passage replay is unavailable.",
                originatingRunId,
                createdAt));
            gaps.Add(Gap(
                DocumentationGapKind.Replay,
                revisionId,
                null,
                null,
                ReplayState.AuditOnly,
                "Deletion leaves an inspectable audit identity but prevents clean source replay.",
                originatingRunId,
                createdAt));
        }
        else if (manifest.Availability == DocumentationSourceAvailability.Unavailable)
        {
            ReplayState replayEffect = retainedReuse ? ReplayState.Partial : ReplayState.Unavailable;
            string unavailableReason = retainedReuse
                ? "The live source revision is unavailable; the retained revision remains available for bounded reuse or clean re-extraction."
                : "The declared source revision is unavailable and cannot authorize passage or claim derivation.";
            string replayReason = retainedReuse
                ? "Live source reacquisition is unavailable, but retained source bytes preserve bounded replay."
                : "The source revision cannot be replayed because no retained bytes are available.";
            gaps.Add(Gap(
                DocumentationGapKind.UnavailableSource,
                revisionId,
                null,
                null,
                replayEffect,
                unavailableReason,
                originatingRunId,
                createdAt));
            gaps.Add(Gap(
                DocumentationGapKind.Replay,
                revisionId,
                null,
                null,
                replayEffect,
                replayReason,
                originatingRunId,
                createdAt));
        }

        return gaps.OrderBy(item => item.GapId.Value, StringComparer.Ordinal).ToList();
    }

    private static DocumentationGapContract Gap(
        DocumentationGapKind kind,
        OpaqueId revisionId,
        OpaqueId? claimId,
        OpaqueId? applicationId,
        ReplayState replayEffect,
        string reason,
        OpaqueId originatingRunId,
        UtcTimestamp createdAt) =>
        new(
            StableId(
                "docgap",
                originatingRunId.Value,
                GapKindToken(kind),
                revisionId.Value,
                claimId?.Value ?? "none",
                applicationId?.Value ?? "none",
                ReplayToken(replayEffect),
                createdAt.ToString(),
                reason),
            originatingRunId,
            kind,
            revisionId,
            claimId,
            applicationId,
            replayEffect,
            reason,
            createdAt);

    private static List<DocumentationDeletionReceiptContract> BuildDeletionReceipts(
        DocumentationClaimImportManifestContract manifest,
        OpaqueId revisionId,
        IReadOnlyList<DocumentationPassageContract> passages,
        OpaqueId originatingRunId,
        UtcTimestamp deletedAt)
    {
        if (manifest.Availability != DocumentationSourceAvailability.Deleted)
        {
            return [];
        }
        const string reason = "The retained source body and exact passage payloads were selected for deletion after dependent evidence publication.";
        OpaqueId[] passageIds = passages.Select(item => item.PassageId)
            .OrderBy(item => item.Value, StringComparer.Ordinal)
            .ToArray();
        return
        [
            new DocumentationDeletionReceiptContract(
                StableId(
                    "docdelete",
                    originatingRunId.Value,
                    revisionId.Value,
                    manifest.ByteFingerprint.Value,
                    Canonical(passageIds.Select(item => item.Value)),
                    Canonical([]),
                    deletedAt.ToString(),
                    reason),
                originatingRunId,
                revisionId,
                manifest.ByteFingerprint,
                passageIds,
                [],
                ReplayState.AuditOnly,
                deletedAt,
                reason),
        ];
    }

    private static IReadOnlyList<ExecutionBoundaryContract> NotUsedBoundaries() =>
    [
        new("provider", BoundaryUseState.NotUsed, "deterministic local documentation import"),
        new("hosted-search", BoundaryUseState.NotUsed, "deterministic local documentation import"),
        new("nexus", BoundaryUseState.NotUsed, "deterministic local documentation import"),
        new("loot", BoundaryUseState.NotUsed, "deterministic local documentation import"),
    ];

    private static void ValidateApplicationTargets(DocumentationImportRequestContract request)
    {
        if (request.Mode == DocumentationImportMode.RetainedReuse)
        {
            if (request.AcceptedApplicationTargets.Count != 0)
            {
                throw new InvalidOperationException(
                    "Retained reuse consumes prior application admissions and cannot admit new targets.");
            }
            return;
        }

        if (request.AcceptedApplicationTargets.Count > 10_000
            || request.AcceptedApplicationTargets.Distinct().Count()
                != request.AcceptedApplicationTargets.Count)
        {
            throw new InvalidOperationException(
                "Accepted documentation application targets must be finite and unique.");
        }

        Dictionary<ApplicationTargetKey, DocumentationApplicationTargetContract> targetsByApplication = [];
        foreach (DocumentationApplicationTargetContract target in request.AcceptedApplicationTargets)
        {
            if (!StringComparer.Ordinal.Equals(target.SubjectType, "installed-entity"))
            {
                throw new InvalidOperationException(
                    "Accepted documentation application targets require the installed-entity subject type.");
            }
            if (!targetsByApplication.TryAdd(
                    new ApplicationTargetKey(
                        target.ConsumingRunId,
                        target.AnalysisContextId,
                        target.SubjectId,
                        target.SubjectType,
                        target.DependencyClosureId),
                    target))
            {
                throw new InvalidDataException(
                    "An application target cannot resolve to multiple accepted snapshot mappings.");
            }
        }

        HashSet<ApplicationTargetKey> consumedTargets = [];
        foreach (DocumentationApplicationInputContract application in request.Manifest.Applications)
        {
            ApplicationTargetKey applicationKey = new(
                application.ConsumingRunId,
                application.AnalysisContextId,
                application.SubjectId,
                application.SubjectType,
                application.DependencyClosureId);
            if (!targetsByApplication.ContainsKey(applicationKey))
            {
                throw new InvalidDataException(
                    "Every documentation application must resolve to exactly one separately accepted installed-entity target mapping.");
            }
            consumedTargets.Add(applicationKey);
        }

        if (request.AcceptedApplicationTargets.Count != consumedTargets.Count)
        {
            throw new InvalidDataException(
                "Accepted documentation application target mappings must be consumed exactly by this import.");
        }
    }

    private static string ContradictionClosureDescriptor(
        DocumentationClaimInputContract root,
        Dictionary<OpaqueId, DocumentationClaimInputContract> inputsByKey,
        Dictionary<OpaqueId, OpaqueId> claimBaseIds,
        ref long work)
    {
        HashSet<OpaqueId> visited = [root.ClaimKey];
        Stack<OpaqueId> pending = new(root.ContradictingClaimKeys);
        List<string> descriptors = [];
        while (pending.Count != 0)
        {
            OpaqueId key = pending.Pop();
            work++;
            if (work > MaximumContradictionClosureWork)
            {
                throw new InvalidOperationException(
                    "Documentation contradiction closure exceeds the bounded semantic-identity work limit.");
            }
            if (!visited.Add(key))
            {
                continue;
            }

            DocumentationClaimInputContract input = inputsByKey[key];
            work += input.ContradictingClaimKeys.Count;
            if (work > MaximumContradictionClosureWork)
            {
                throw new InvalidOperationException(
                    "Documentation contradiction closure exceeds the bounded semantic-identity work limit.");
            }
            descriptors.Add(CanonicalInOrder(
                claimBaseIds[key].Value,
                Canonical(input.ContradictingClaimKeys.Select(target => claimBaseIds[target].Value))));
            foreach (OpaqueId target in input.ContradictingClaimKeys)
            {
                pending.Push(target);
            }
        }
        return Canonical(descriptors);
    }

    private static OpaqueId StableId(string prefix, params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string value in values)
        {
            byte[] bytes = StrictUtf8.GetBytes(value);
            hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
            hash.AppendData(bytes);
        }

        return new OpaqueId(prefix + "-" + Convert.ToHexStringLower(hash.GetHashAndReset())[..32]);
    }

    private static string Canonical(IEnumerable<string> values) =>
        string.Concat(values.Order(StringComparer.Ordinal).Select(value =>
            FormattableString.Invariant($"{Encoding.UTF8.GetByteCount(value)}:{value}")));

    private static string CanonicalInOrder(params string[] values) =>
        string.Concat(values.Select(value =>
            FormattableString.Invariant($"{Encoding.UTF8.GetByteCount(value)}:{value}")));

    private sealed record ApplicationTargetKey(
        OpaqueId RunId,
        OpaqueId ContextId,
        OpaqueId SubjectId,
        string SubjectType,
        OpaqueId ClosureId);

    private static string SourceKindToken(DocumentationSourceKind value) => value switch
    {
        DocumentationSourceKind.ProjectAuthoredLocal => "project-authored-local",
        DocumentationSourceKind.Fixture => "fixture",
        _ => throw new InvalidOperationException("Documentation source kind is not closed."),
    };

    private static string ImportModeToken(DocumentationImportMode value) => value switch
    {
        DocumentationImportMode.CleanImport => "clean-import",
        DocumentationImportMode.RetainedReuse => "retained-reuse",
        _ => throw new InvalidOperationException("Documentation import mode is not closed."),
    };

    private static string ClaimKindToken(ClaimKind value) => value switch
    {
        ClaimKind.DeclaredPurpose => "declared-purpose",
        ClaimKind.Requirement => "requirement",
        ClaimKind.Incompatibility => "incompatibility",
        ClaimKind.InstallationInstruction => "installation-instruction",
        ClaimKind.PriorityInstruction => "priority-instruction",
        ClaimKind.LifecycleInstruction => "lifecycle-instruction",
        ClaimKind.ConfigurationInstruction => "configuration-instruction",
        ClaimKind.PatchInstruction => "patch-instruction",
        ClaimKind.KnownIssue => "known-issue",
        _ => throw new InvalidOperationException("Documentation claim kind is not closed."),
    };

    private static string AuthorityToken(EvidenceAuthority value) => value switch
    {
        EvidenceAuthority.AuthoritativeExternal => "authoritative-external",
        _ => throw new InvalidOperationException("Documentation claims require authoritative-external authority."),
    };

    private static string ApplicabilityToken(ClaimApplicabilityState value) => value switch
    {
        ClaimApplicabilityState.Applicable => "applicable",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "contradicted",
        _ => throw new InvalidOperationException("Documentation applicability is not closed."),
    };

    private static string RoleToken(ClassificationRole value) => value switch
    {
        ClassificationRole.Declared => "declared",
        ClassificationRole.Observed => "observed",
        ClassificationRole.Predicted => "predicted",
        ClassificationRole.Established => "established",
        _ => throw new InvalidOperationException("Documentation classification role is not closed."),
    };

    private static string GapKindToken(DocumentationGapKind value) => value switch
    {
        DocumentationGapKind.Contradiction => "contradiction",
        DocumentationGapKind.Deletion => "deletion",
        DocumentationGapKind.UnavailableSource => "unavailable-source",
        DocumentationGapKind.Replay => "replay",
        _ => throw new InvalidOperationException("Documentation gap kind is not closed."),
    };

    private static string ReplayToken(ReplayState value) => value switch
    {
        ReplayState.CompleteClean => "complete-clean",
        ReplayState.Partial => "partial",
        ReplayState.AuditOnly => "audit-only",
        ReplayState.Unavailable => "unavailable",
        ReplayState.FailedIdentityDrift => "failed-identity-drift",
        _ => throw new InvalidOperationException("Documentation replay state is not closed."),
    };

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
