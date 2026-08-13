using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;

namespace Infinium.PublicFixtures;

public sealed record SourceClaimFixturePackage(
    string PackageId,
    string Partition,
    SourceClaimExecutionInput ExecutionInput,
    IReadOnlyList<SourceClaimRetainedTranscript> Transcripts,
    JsonElement Oracle,
    JsonElement OracleProvenance);

public static class SourceClaimFixtureReader
{
    private const long MaximumDocumentBytes = 128 * 1024;
    private static readonly string[] ExactFiles =
    [
        "context-manifest.v1.json", "execution-input.v1.json", "oracle-provenance.v1.json",
        "oracle.v1.json", "public-manifest.json", "retained-transcripts.v1.json",
    ];

    public static SourceClaimFixturePackage Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string[] files = Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!files.SequenceEqual(ExactFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Source-claim fixture package closure is not exact.");
        }
        using BoundedJsonDocumentSnapshot manifest = ReadBounded(Path.Combine(directory, "public-manifest.json"));
        using BoundedJsonDocumentSnapshot context = ReadBounded(Path.Combine(directory, "context-manifest.v1.json"));
        using BoundedJsonDocumentSnapshot inputDocument = ReadBounded(Path.Combine(directory, "execution-input.v1.json"));
        using BoundedJsonDocumentSnapshot transcriptsDocument = ReadBounded(Path.Combine(directory, "retained-transcripts.v1.json"));
        using BoundedJsonDocumentSnapshot oracle = ReadBounded(Path.Combine(directory, "oracle.v1.json"));
        using BoundedJsonDocumentSnapshot provenance = ReadBounded(Path.Combine(directory, "oracle-provenance.v1.json"));
        JsonElement manifestRoot = manifest.Document.RootElement;
        ActiveJsonSchemaValidator.Validate(inputDocument.Document.RootElement, "source-claim-execution-input.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(transcriptsDocument.Document.RootElement, "source-claim-retained-transcripts.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(context.Document.RootElement, "source-claim-context.v1.schema.json");
        if (manifestRoot.GetProperty("status").GetString() != "oracle-frozen-pre-comparison"
            || !manifestRoot.GetProperty("answer_free").GetBoolean()
            || manifestRoot.GetProperty("network_required").GetBoolean())
        {
            throw new InvalidDataException("Source-claim fixture manifest is not a frozen offline answer-isolated package.");
        }
        SourceClaimExecutionInput input = JsonSerializer.Deserialize<SourceClaimExecutionInput>(
            inputDocument.Document.RootElement, SourceClaimContextMinimizer.JsonOptions)!;
        SourceClaimRetainedTranscript[] transcripts = JsonSerializer.Deserialize<SourceClaimRetainedTranscript[]>(
            transcriptsDocument.Document.RootElement.GetProperty("transcripts"), SourceClaimContextMinimizer.JsonOptions)!;
        SourceClaimContextMinimizer.ValidateInput(input);
        return new(input.PackageId, manifestRoot.GetProperty("partition").GetString()!, input, transcripts,
            oracle.Document.RootElement.Clone(), provenance.Document.RootElement.Clone());
    }

    private static BoundedJsonDocumentSnapshot ReadBounded(string path) =>
        BoundedJsonDocumentReader.Read(path, MaximumDocumentBytes, maximumDepth: 32);
}
