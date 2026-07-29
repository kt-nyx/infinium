using System.Collections.Concurrent;
using Infinium.Application.Runtime;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record InfiniumPipeRoleFeature(string Role, string PipeName);

public sealed class CoordinatorRuntime(
    AuthoritativeStore store,
    CoordinatorAuthority authority,
    RuntimeDescriptor descriptor)
{
    internal const int MaximumApplicationConnections = 16;
    internal const int MaximumWorkerConnections = 4;
    internal const int MaximumEventSubscriptions = 16;
    internal const int MaximumNewDurableCommandsPerMinute = 120;

    private readonly ConcurrentDictionary<string, byte> applicationConnections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> workerConnections = new(StringComparer.Ordinal);
    private readonly Lock admissionGate = new();
    private readonly Queue<DateTimeOffset> durableCommandAdmissions = new();
    private int eventSubscriptions;

    public AuthoritativeStore Store { get; } = store;
    public CoordinatorAuthority Authority { get; } = authority;
    public RuntimeDescriptor Descriptor { get; } = descriptor;

    public bool TryAdmitApplicationConnection(string connectionId) =>
        TryAdmit(applicationConnections, connectionId, MaximumApplicationConnections);

    public bool TryAdmitWorkerConnection(string connectionId) =>
        TryAdmit(workerConnections, connectionId, MaximumWorkerConnections);

    public bool TryAdmitEventSubscription()
    {
        int next = Interlocked.Increment(ref eventSubscriptions);
        if (next <= MaximumEventSubscriptions)
        {
            return true;
        }

        Interlocked.Decrement(ref eventSubscriptions);
        return false;
    }

    public void ReleaseEventSubscription() => Interlocked.Decrement(ref eventSubscriptions);

    public bool TryAdmitNewDurableCommand(DateTimeOffset now)
    {
        lock (admissionGate)
        {
            DateTimeOffset windowStart = now.Subtract(TimeSpan.FromMinutes(1));
            while (durableCommandAdmissions.TryPeek(out DateTimeOffset admitted)
                   && admitted <= windowStart)
            {
                durableCommandAdmissions.Dequeue();
            }

            if (durableCommandAdmissions.Count >= MaximumNewDurableCommandsPerMinute)
            {
                return false;
            }

            durableCommandAdmissions.Enqueue(now);
            return true;
        }
    }

    public void ReleaseApplicationConnection(string connectionId)
    {
        lock (admissionGate)
        {
            applicationConnections.TryRemove(connectionId, out _);
        }
    }

    public void ReleaseWorkerConnection(string connectionId)
    {
        lock (admissionGate)
        {
            workerConnections.TryRemove(connectionId, out _);
        }
    }

    public bool IsApplicationConnectionAdmitted(string connectionId) =>
        applicationConnections.ContainsKey(connectionId);

    public bool IsWorkerConnectionAdmitted(string connectionId) =>
        workerConnections.ContainsKey(connectionId);

    private bool TryAdmit(
        ConcurrentDictionary<string, byte> connections,
        string connectionId,
        int maximum)
    {
        lock (admissionGate)
        {
            if (connections.ContainsKey(connectionId))
            {
                return true;
            }

            if (connections.Count >= maximum)
            {
                return false;
            }

            return connections.TryAdd(connectionId, 0);
        }
    }
}
