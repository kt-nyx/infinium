using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.EvaluatorV2.LegacyV1;

public enum AssertionStatus
{
    Unspecified,
    Passed,
    Failed,
    NotApplicable,
    Blocked,
}

public enum AssertionType
{
    Unspecified,
    TypedEquality,
    Presence,
    Absence,
    CollectionEmpty,
    Coverage,
    Taxonomy,
    Provenance,
    Replayability,
    AnswerIsolation,
    NonMutation,
    Security,
}

public sealed record EvaluationAssertionResult(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId AssertionResultId,
    string AssertionId,
    string EvaluationId,
    string SpecificationRevision,
    OpaqueId FixtureId,
    ContractVersion FixtureVersion,
    FixturePartition Partition,
    string ImplementationCommit,
    bool DirtyWorktree,
    OpaqueId RunId,
    Sha256Fingerprint RunOutputFingerprint,
    Sha256Fingerprint OracleFingerprint,
    AssertionType AssertionType,
    AssertionStatus Status,
    IReadOnlyList<string> ActualReferences,
    IReadOnlyList<string> OracleEntryReferences,
    IReadOnlyList<string> Messages,
    UtcTimestamp EvaluatedAt);

public static class AssertionResultReader
{
    private const long MaximumAssertionResultBytes = 4 * 1024 * 1024;
    private static readonly HashSet<string> AssertionResultProperties = new(StringComparer.Ordinal)
    {
        "schema_id",
        "schema_version",
        "assertion_result_id",
        "assertion_id",
        "evaluation_id",
        "specification_revision",
        "fixture_id",
        "fixture_version",
        "partition",
        "implementation_commit",
        "dirty_worktree",
        "run_id",
        "run_output_fingerprint",
        "oracle_fingerprint",
        "assertion_type",
        "status",
        "actual_references",
        "oracle_entry_references",
        "messages",
        "evaluated_at",
    };

    public static EvaluationAssertionResult Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using BoundedJsonDocumentSnapshot snapshot = BoundedJsonDocumentReader.Read(
            path,
            MaximumAssertionResultBytes,
            maximumDepth: 32);
        JsonElement root = snapshot.Document.RootElement;
        EmbeddedJsonSchemaValidator.Validate(root, "evaluation-assertion-result.v1.schema.json");
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Assertion result must be a JSON object.");
        }
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!AssertionResultProperties.Contains(property.Name))
            {
                throw new InvalidDataException($"Assertion result contains unsupported property '{property.Name}'.");
            }
        }

        string schemaId = RequiredString(root, "schema_id");
        if (!StringComparer.Ordinal.Equals(schemaId, ContractConstants.EvaluationAssertionSchemaId))
        {
            throw new InvalidDataException($"Unsupported assertion-result schema '{schemaId}'.");
        }

        string schemaVersionText = RequiredString(root, "schema_version");
        if (!StringComparer.Ordinal.Equals(schemaVersionText, "1"))
        {
            throw new InvalidDataException("Only canonical assertion-result schema version '1' is supported.");
        }
        ContractVersion schemaVersion = new(1, 0, 0);

        string implementationCommit = RequiredString(root, "implementation_commit");
        if (implementationCommit.Length != 40 || implementationCommit.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Implementation commit must be a complete 40-character Git object ID.");
        }

        string evaluationId = RequiredString(root, "evaluation_id");
        if (evaluationId.Length != 9
            || !evaluationId.StartsWith("EVAL-", StringComparison.Ordinal)
            || !evaluationId.AsSpan(5).ToString().All(char.IsAsciiDigit))
        {
            throw new InvalidDataException("Evaluation ID must use the EVAL-0000 form.");
        }

        bool dirtyWorktree = RequiredBoolean(root, "dirty_worktree");
        AssertionStatus status = ParseStatus(RequiredString(root, "status"));
        AssertionType assertionType = ParseAssertionType(RequiredString(root, "assertion_type"));
        if (dirtyWorktree && status == AssertionStatus.Passed)
        {
            throw new InvalidDataException(
                "A dirty implementation cannot emit retained passing evaluation evidence.");
        }

        string[] actualReferences = ReadStringArray(root, "actual_references");
        string[] oracleEntryReferences = ReadStringArray(root, "oracle_entry_references");
        ValidatePassingEvidence(status, assertionType, actualReferences, oracleEntryReferences);

        return new EvaluationAssertionResult(
            schemaId,
            schemaVersion,
            new OpaqueId(RequiredString(root, "assertion_result_id")),
            RequiredString(root, "assertion_id"),
            evaluationId,
            RequiredString(root, "specification_revision"),
            new OpaqueId(RequiredString(root, "fixture_id")),
            ContractVersion.Parse(RequiredString(root, "fixture_version")),
            ParsePartition(RequiredString(root, "partition")),
            implementationCommit.ToLowerInvariant(),
            dirtyWorktree,
            new OpaqueId(RequiredString(root, "run_id")),
            ParseFingerprint(root, "run_output_fingerprint"),
            ParseFingerprint(root, "oracle_fingerprint"),
            assertionType,
            status,
            actualReferences,
            oracleEntryReferences,
            ReadStringArray(root, "messages"),
            UtcTimestamp.Parse(RequiredString(root, "evaluated_at")));
    }

    private static void ValidatePassingEvidence(
        AssertionStatus status,
        AssertionType assertionType,
        string[] actualReferences,
        string[] oracleEntryReferences)
    {
        if (status != AssertionStatus.Passed)
        {
            return;
        }

        if (oracleEntryReferences.Length == 0)
        {
            throw new InvalidDataException(
                "A passing assertion must reference at least one sealed oracle entry.");
        }

        bool actualMayBeEmpty = assertionType is
            AssertionType.CollectionEmpty
            or AssertionType.Absence
            or AssertionType.NonMutation
            or AssertionType.AnswerIsolation
            or AssertionType.Security;
        if (!actualMayBeEmpty && actualReferences.Length == 0)
        {
            throw new InvalidDataException(
                "This passing assertion type must reference at least one actual result.");
        }
    }

    private static AssertionType ParseAssertionType(string value)
    {
        return value switch
        {
            "typed-equality" => AssertionType.TypedEquality,
            "presence" => AssertionType.Presence,
            "absence" => AssertionType.Absence,
            "collection-empty" => AssertionType.CollectionEmpty,
            "coverage" => AssertionType.Coverage,
            "taxonomy" => AssertionType.Taxonomy,
            "provenance" => AssertionType.Provenance,
            "replayability" => AssertionType.Replayability,
            "answer-isolation" => AssertionType.AnswerIsolation,
            "non-mutation" => AssertionType.NonMutation,
            "security" => AssertionType.Security,
            _ => throw new InvalidDataException($"Unknown assertion type '{value}'."),
        };
    }

    private static AssertionStatus ParseStatus(string value)
    {
        return value switch
        {
            "passed" => AssertionStatus.Passed,
            "failed" => AssertionStatus.Failed,
            "not-applicable" => AssertionStatus.NotApplicable,
            "blocked" => AssertionStatus.Blocked,
            _ => throw new InvalidDataException($"Unknown assertion status '{value}'."),
        };
    }

    private static FixturePartition ParsePartition(string value)
    {
        return value switch
        {
            "development" => FixturePartition.Development,
            "validation" => FixturePartition.Validation,
            "held-out" => FixturePartition.HeldOut,
            _ => throw new InvalidDataException($"Unknown fixture partition '{value}'."),
        };
    }

    private static string[] ReadStringArray(JsonElement parent, string propertyName)
    {
        return RequiredArray(parent, propertyName).EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!
                : throw new InvalidDataException($"'{propertyName}' must contain only strings."))
            .ToArray();
    }

    private static Sha256Fingerprint ParseFingerprint(JsonElement parent, string propertyName)
    {
        string value = RequiredString(parent, propertyName);
        Sha256Fingerprint fingerprint = new(value);
        if (!StringComparer.Ordinal.Equals(value, fingerprint.Value))
        {
            throw new InvalidDataException($"'{propertyName}' must be lowercase.");
        }

        return fingerprint;
    }

    private static JsonElement RequiredArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Required array property '{propertyName}' is missing.");
        }

        return value;
    }

    private static string RequiredString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Required string property '{propertyName}' is missing.");
        }

        return value.GetString()!;
    }

    private static bool RequiredBoolean(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"Required Boolean property '{propertyName}' is missing.");
        }

        return value.GetBoolean();
    }
}
