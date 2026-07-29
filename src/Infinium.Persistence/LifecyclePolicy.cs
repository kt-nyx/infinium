using Infinium.Domain.Contracts;

#pragma warning disable IDE0008 // Pattern-local inference keeps the transition guard readable.
#pragma warning disable CA1859 // Expose the table through its immutable dictionary contract.

namespace Infinium.Persistence;

public static class LifecyclePolicy
{
    public const string Version = "1.0.0";

    private static readonly IReadOnlyDictionary<LifecycleState, LifecycleState[]> Allowed =
        new Dictionary<LifecycleState, LifecycleState[]>
        {
            [LifecycleState.Queued] =
                [LifecycleState.Running, LifecycleState.Pausing, LifecycleState.Cancelling, LifecycleState.Failed],
            [LifecycleState.Running] =
                [LifecycleState.Waiting, LifecycleState.Retrying, LifecycleState.Pausing,
                 LifecycleState.Cancelling, LifecycleState.Completed, LifecycleState.CompletedWithGaps,
                 LifecycleState.Failed, LifecycleState.LimitReached, LifecycleState.InvalidatedByChangedInput],
            [LifecycleState.Waiting] =
                [LifecycleState.Running, LifecycleState.Retrying, LifecycleState.Pausing,
                 LifecycleState.Cancelling, LifecycleState.Completed,
                 LifecycleState.CompletedWithGaps, LifecycleState.Failed, LifecycleState.LimitReached],
            [LifecycleState.Retrying] =
                [LifecycleState.Running, LifecycleState.Waiting, LifecycleState.Pausing,
                 LifecycleState.Cancelling, LifecycleState.Failed, LifecycleState.LimitReached],
            [LifecycleState.Pausing] =
                [LifecycleState.Paused, LifecycleState.Cancelling, LifecycleState.Failed],
            [LifecycleState.Paused] =
                [LifecycleState.Queued, LifecycleState.Cancelling],
            [LifecycleState.Cancelling] =
                [LifecycleState.Cancelled, LifecycleState.Failed],
        };

    public static bool IsTerminal(LifecycleState state) =>
        state is LifecycleState.Cancelled
            or LifecycleState.Completed
            or LifecycleState.CompletedWithGaps
            or LifecycleState.Failed
            or LifecycleState.LimitReached
            or LifecycleState.InvalidatedByChangedInput;

    public static void EnsureAllowed(LifecycleState from, LifecycleState to)
    {
        if (from == LifecycleState.Unspecified || to == LifecycleState.Unspecified)
        {
            throw new InvalidOperationException("Lifecycle states must be explicit.");
        }

        if (IsTerminal(from) || !Allowed.TryGetValue(from, out var targets) || !targets.Contains(to))
        {
            throw new InvalidOperationException($"Lifecycle transition {from} -> {to} is not allowed.");
        }
    }
}

#pragma warning restore CA1859
#pragma warning restore IDE0008
