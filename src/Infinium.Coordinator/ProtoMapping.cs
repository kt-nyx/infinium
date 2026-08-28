using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Persistence;
using Common = Infinium.Contracts.Protobuf.Common.V1;
using DomainContracts = Infinium.Domain.Contracts;
using DomainProto = Infinium.Contracts.Protobuf.Domain.V1;

namespace Infinium.Coordinator;

internal static class ProtoMapping
{
    public static Common.Instant ToProto(DateTimeOffset value) =>
        new()
        {
            UnixSeconds = value.ToUnixTimeSeconds(),
            Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
        };

    public static DomainProto.LifecycleState ToProto(DomainContracts.LifecycleState state) =>
        state switch
        {
            DomainContracts.LifecycleState.Queued => DomainProto.LifecycleState.Queued,
            DomainContracts.LifecycleState.Running => DomainProto.LifecycleState.Running,
            DomainContracts.LifecycleState.Waiting => DomainProto.LifecycleState.Waiting,
            DomainContracts.LifecycleState.Retrying => DomainProto.LifecycleState.Retrying,
            DomainContracts.LifecycleState.Pausing => DomainProto.LifecycleState.Pausing,
            DomainContracts.LifecycleState.Paused => DomainProto.LifecycleState.Paused,
            DomainContracts.LifecycleState.Cancelling => DomainProto.LifecycleState.Cancelling,
            DomainContracts.LifecycleState.Cancelled => DomainProto.LifecycleState.Cancelled,
            DomainContracts.LifecycleState.Completed => DomainProto.LifecycleState.Completed,
            DomainContracts.LifecycleState.CompletedWithGaps => DomainProto.LifecycleState.CompletedWithGaps,
            DomainContracts.LifecycleState.Failed => DomainProto.LifecycleState.Failed,
            DomainContracts.LifecycleState.LimitReached => DomainProto.LifecycleState.LimitReached,
            DomainContracts.LifecycleState.InvalidatedByChangedInput =>
                DomainProto.LifecycleState.InvalidatedByChangedInput,
            _ => DomainProto.LifecycleState.Unspecified,
        };

    public static RunSummary ToSummary(RunRecord run) =>
        new()
        {
            RunId = new DomainProto.RunId { Value = run.RunId },
            LifecycleState = ToProto(run.State),
            LifecycleGeneration = checked((ulong)run.Generation),
            CreatedAt = ToProto(run.CreatedAt),
            UpdatedAt = ToProto(run.UpdatedAt),
            CoverageState = DomainProto.CoverageState.Unknown,
            ReadinessScope = DomainProto.ReadinessScope.None,
            Progress = EmptyProgress(run.State),
            Cost = EmptyCost(),
        };

    public static RunDetail ToDetail(RunRecord run) =>
        new()
        {
            Summary = ToSummary(run),
            InstallationSnapshotId =
                new DomainProto.InstallationSnapshotId { Value = run.Binding.InstallationSnapshotId },
            AnalysisContextId =
                new DomainProto.AnalysisContextId { Value = run.Binding.AnalysisContextId },
            EffectiveScanConfigurationId =
                new DomainProto.ScanConfigurationId { Value = run.Binding.EffectiveScanConfigurationId },
            ResolvedInputManifestId =
                new DomainProto.ResolvedInputManifestId { Value = run.Binding.ResolvedInputManifestId },
            // The coordinator retains lifecycle and publication authority, but not the complete
            // input/dependency set required to promise a replay.
            ReplayabilityState = DomainProto.ReplayabilityState.Unavailable,
            AuditabilityState = DomainProto.AuditabilityState.CompleteWithGaps,
            ProjectionVersion = new DomainProto.ProjectionVersion { Value = "1" },
        };

    public static ProgressSummary EmptyProgress(DomainContracts.LifecycleState state) =>
        new()
        {
            DenominatorState = ProgressDenominatorState.Known,
            PopulationRevision = 1,
            TotalUnits = new Common.OptionalUInt64
            {
                Availability = Common.AvailabilityState.Available,
                Value = 1,
            },
            CompletedUnits = state is DomainContracts.LifecycleState.Completed ? 1UL : 0UL,
            QueuedUnits = state is DomainContracts.LifecycleState.Queued
                or DomainContracts.LifecycleState.Waiting
                or DomainContracts.LifecycleState.Retrying
                or DomainContracts.LifecycleState.Paused ? 1UL : 0UL,
            RunningUnits = state is DomainContracts.LifecycleState.Running
                or DomainContracts.LifecycleState.Pausing
                or DomainContracts.LifecycleState.Cancelling ? 1UL : 0UL,
            FailedUnits = state is DomainContracts.LifecycleState.Failed ? 1UL : 0UL,
            SkippedUnits = state is DomainContracts.LifecycleState.Cancelled ? 1UL : 0UL,
            UnsupportedUnits = state is DomainContracts.LifecycleState.Unspecified ? 1UL : 0UL,
            LimitedUnits = state is DomainContracts.LifecycleState.LimitReached ? 1UL : 0UL,
            InvalidatedUnits = state is DomainContracts.LifecycleState.InvalidatedByChangedInput ? 1UL : 0UL,
            GapUnits = state is DomainContracts.LifecycleState.CompletedWithGaps ? 1UL : 0UL,
        };

    public static CostSummary EmptyCost() =>
        new()
        {
            ReservedNanoUsd = UnavailableInt64(),
            CalculatedActualNanoUsd = UnavailableInt64(),
            ProviderInputTokens = UnavailableUInt64(),
            ProviderOutputTokens = UnavailableUInt64(),
            ProviderReasoningTokens = UnavailableUInt64(),
            ProviderDispatchCount = UnavailableUInt64(),
            ProviderToolCallCount = UnavailableUInt64(),
        };

    private static Common.OptionalUInt64 UnavailableUInt64() =>
        new() { Availability = Common.AvailabilityState.Unavailable };

    private static Common.OptionalInt64 UnavailableInt64() =>
        new() { Availability = Common.AvailabilityState.Unavailable };
}
