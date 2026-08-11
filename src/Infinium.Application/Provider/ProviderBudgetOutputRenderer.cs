using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Provider;

public sealed record ProviderBudgetOutputDocument(
    string SchemaIdentity,
    string ScopeKind,
    string ScopeId,
    ProviderBudgetVectorContract Reserved,
    ProviderBudgetVectorContract Settled,
    ProviderBudgetVectorContract Unresolved,
    long ProjectionVersion,
    string ExecutionMode,
    bool NetworkUsed,
    bool CredentialAccessed,
    IReadOnlyList<string> Gaps);

public static class ProviderBudgetOutputRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static ProviderBudgetOutputDocument CreateDocument(
        ProviderBudgetProjectionContract projection,
        IReadOnlyList<string> gaps) =>
        new("infinium.provider.budget-output/1", projection.ScopeKind, projection.ScopeId.Value,
            projection.Reserved, projection.Settled, projection.Unresolved, projection.ProjectionVersion,
            "simulated-nonnetwork", false, false, gaps);

    public static string RenderJson(ProviderBudgetOutputDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
    }

    public static string RenderHuman(ProviderBudgetOutputDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return string.Join(Environment.NewLine,
            $"Provider budget scope: {document.ScopeKind}/{document.ScopeId}",
            $"Execution mode: {document.ExecutionMode}",
            $"Reserved: {document.Reserved.NanoUsd} nano-USD; settled: {document.Settled.NanoUsd}; unresolved: {document.Unresolved.NanoUsd}",
            $"Dispatch/input/output: {document.Settled.DispatchCount}/{document.Settled.InputTokens}/{document.Settled.OutputTokens}",
            $"Network used: {document.NetworkUsed}; credential accessed: {document.CredentialAccessed}",
            $"Gaps: {(document.Gaps.Count == 0 ? "none" : string.Join(", ", document.Gaps))}") + Environment.NewLine;
    }
}
