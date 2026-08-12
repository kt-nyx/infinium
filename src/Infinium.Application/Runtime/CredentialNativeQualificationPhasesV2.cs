using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.Application.Runtime;

public enum CredentialNativeQualificationSecretModeV2
{
    None,
    Manual,
    Generated48,
    GeneratedMaximum,
    GeneratedOversize,
}

public sealed record CredentialNativeQualificationPhaseV2(
    string ScenarioId,
    string PhaseId,
    HelperAssignmentKindV2 AssignmentKind,
    CredentialNativeQualificationSecretModeV2 SecretMode,
    bool UnavailableBeforeNativeCall,
    bool FailExactPredecessorDeleteBeforeNativeCall,
    bool ManualEntryMustCancel = false)
{
    public string AssignmentId => $"wp4-v2/{ScenarioId}/{PhaseId}";
}

public static class CredentialNativeQualificationPhasesV2
{
    public static IReadOnlyList<CredentialNativeQualificationPhaseV2> Definitions { get; } =
    [
        Phase("interactive-entry-submit", "preflight", HelperAssignmentKindV2.Verify),
        Phase("interactive-entry-submit", "submit", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Manual),
        Phase("interactive-entry-submit", "cleanup", HelperAssignmentKindV2.Delete),
        Phase("interactive-entry-cancel", "preflight", HelperAssignmentKindV2.Verify),
        Phase("interactive-entry-cancel", "cancel", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Manual, manualCancel: true),
        Phase("interactive-entry-cancel", "cleanup", HelperAssignmentKindV2.Delete),
        Phase("credential-size-boundaries", "preflight-maximum", HelperAssignmentKindV2.Verify),
        Phase("credential-size-boundaries", "preflight-oversize", HelperAssignmentKindV2.Verify),
        Phase("credential-size-boundaries", "maximum", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.GeneratedMaximum),
        Phase("credential-size-boundaries", "oversize", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.GeneratedOversize),
        Phase("credential-size-boundaries", "cleanup-maximum", HelperAssignmentKindV2.Delete),
        Phase("credential-size-boundaries", "cleanup-oversize", HelperAssignmentKindV2.Delete),
        Phase("secure-store-unavailable", "preflight", HelperAssignmentKindV2.Verify),
        Phase("secure-store-unavailable", "unavailable", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Generated48, unavailable: true),
        Phase("secure-store-unavailable", "cleanup", HelperAssignmentKindV2.Delete),
        Phase("replacement", "preflight-predecessor", HelperAssignmentKindV2.Verify),
        Phase("replacement", "preflight-successor", HelperAssignmentKindV2.Verify),
        Phase("replacement", "predecessor-active", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Generated48),
        Phase("replacement", "replacement-interrupted", HelperAssignmentKindV2.Replace, CredentialNativeQualificationSecretModeV2.Generated48, failDelete: true),
        Phase("replacement", "replacement-recovered", HelperAssignmentKindV2.Recover),
        Phase("replacement", "cleanup-predecessor", HelperAssignmentKindV2.Delete),
        Phase("replacement", "cleanup-successor", HelperAssignmentKindV2.Delete),
        Phase("revoke-delete", "preflight", HelperAssignmentKindV2.Verify),
        Phase("revoke-delete", "active", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Generated48),
        Phase("revoke-delete", "verify", HelperAssignmentKindV2.Verify),
        Phase("revoke-delete", "deleted-after-revocation", HelperAssignmentKindV2.Delete),
        Phase("helper-and-coordinator-crash-restart", "preflight", HelperAssignmentKindV2.Verify),
        Phase("helper-and-coordinator-crash-restart", "half-commit", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Generated48),
        Phase("helper-and-coordinator-crash-restart", "restart-recovery", HelperAssignmentKindV2.Recover),
        Phase("helper-and-coordinator-crash-restart", "cleanup", HelperAssignmentKindV2.Delete),
        Phase("backup-restore-reauthentication", "preflight-old", HelperAssignmentKindV2.Verify),
        Phase("backup-restore-reauthentication", "preflight-new", HelperAssignmentKindV2.Verify),
        Phase("backup-restore-reauthentication", "backup-active", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Generated48),
        Phase("backup-restore-reauthentication", "restored-new-generation", HelperAssignmentKindV2.Recover, CredentialNativeQualificationSecretModeV2.Manual),
        Phase("backup-restore-reauthentication", "cleanup-restored-predecessor", HelperAssignmentKindV2.Delete),
        Phase("backup-restore-reauthentication", "cleanup-successor", HelperAssignmentKindV2.Delete),
        Phase("fake-provider-dispatch", "preflight", HelperAssignmentKindV2.Verify),
        Phase("fake-provider-dispatch", "enroll", HelperAssignmentKindV2.Enroll, CredentialNativeQualificationSecretModeV2.Generated48),
        Phase("fake-provider-dispatch", "verify", HelperAssignmentKindV2.Verify),
        Phase("fake-provider-dispatch", "final-gate-dispatch-stage-admit-settle", HelperAssignmentKindV2.ProviderDispatch),
        Phase("fake-provider-dispatch", "cleanup", HelperAssignmentKindV2.Delete),
    ];

    private static readonly Dictionary<string, CredentialNativeQualificationPhaseV2> ByAssignmentId =
        Definitions.ToDictionary(item => item.AssignmentId, StringComparer.Ordinal);

    public static CredentialNativeQualificationPhaseV2 Parse(
        string assignmentId,
        HelperAssignmentKindV2 assignmentKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);
        if (!ByAssignmentId.TryGetValue(assignmentId, out CredentialNativeQualificationPhaseV2? phase)
            || phase.AssignmentKind != assignmentKind)
        {
            throw new InvalidDataException("The native qualification assignment is absent from the closed phase contract.");
        }
        return phase;
    }

    private static CredentialNativeQualificationPhaseV2 Phase(
        string scenario,
        string phase,
        HelperAssignmentKindV2 kind,
        CredentialNativeQualificationSecretModeV2 secret = CredentialNativeQualificationSecretModeV2.None,
        bool unavailable = false,
        bool failDelete = false,
        bool manualCancel = false) =>
        new(scenario, phase, kind, secret, unavailable, failDelete, manualCancel);
}
