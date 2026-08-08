using System.Globalization;

namespace Infinium.Domain.Contracts;

public sealed record OpaqueId
{
    public OpaqueId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128
            || !char.IsAsciiLetterOrDigit(value[0])
            || value.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not ('.' or '_' or ':' or '/' or '-')))
        {
            throw new ArgumentException(
                "An opaque ID must be an ASCII token of at most 128 characters.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ContractVersion
{
    public ContractVersion(uint major, uint minor, uint patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public uint Major { get; }

    public uint Minor { get; }

    public uint Patch { get; }

    public static ContractVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string[] parts = value.Split('.');
        if (parts.Length != 3
            || !uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint major)
            || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint minor)
            || !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint patch))
        {
            throw new FormatException($"'{value}' is not a three-part unsigned contract version.");
        }

        return new ContractVersion(major, minor, patch);
    }

    public override string ToString() => FormattableString.Invariant($"{Major}.{Minor}.{Patch}");
}

public sealed record Sha256Fingerprint
{
    public Sha256Fingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 fingerprint must contain exactly 64 hexadecimal characters.", nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record UtcTimestamp
{
    public UtcTimestamp(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Contract timestamps must use the UTC offset.", nameof(value));
        }

        Value = value;
    }

    public DateTimeOffset Value { get; }

    public static UtcTimestamp Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            throw new FormatException($"'{value}' is not an ISO-8601 UTC timestamp.");
        }

        return new UtcTimestamp(parsed);
    }

    public override string ToString() => Value.ToString("O", CultureInfo.InvariantCulture);
}

public static class ContractConstants
{
    public const string TaxonomyId = "infinium.skyrim-se.mod-impact-taxonomy";
    public const string TaxonomyVersion = "0.1.0";
    public const string FixturePublicManifestSchemaId = "infinium.evaluation.fixture-public-manifest/v1";
    public const string AnalyzerDeclarationSchemaId = "infinium.analyzer.declaration/v1";
    public const string EffectiveScanConfigurationSchemaId = "infinium.scan.effective-configuration/v1";
    public const string RunOutputSchemaId = "infinium.run-output/v1";
    public const string CliSummarySchemaId = "infinium.cli-summary/v1";
    public const string DiagnosticTraceSchemaId = "infinium.diagnostic.trace/v1";
    public const string EvaluationAssertionSchemaId = "infinium.evaluation.assertion-result/v1";
    public const string DocumentationEvidenceSchemaId = "infinium.documentation.evidence/v1";
    public const string CandidateAnalysisSchemaId = "infinium.analysis.candidate/v1";
    public const string FindingCaseSchemaId = "infinium.analysis.finding-case/v1";
    public const string AnalysisReplaySchemaId = "infinium.analysis.replay/v1";
    public const string AnalysisExecutionInputSchemaId = "infinium.analysis.execution-input/v1";
}
