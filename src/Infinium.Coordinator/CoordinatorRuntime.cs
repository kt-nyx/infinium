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
    private readonly ConcurrentDictionary<string, byte> applicationConnections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> workerConnections = new(StringComparer.Ordinal);

    public AuthoritativeStore Store { get; } = store;
    public CoordinatorAuthority Authority { get; } = authority;
    public RuntimeDescriptor Descriptor { get; } = descriptor;

    public void AdmitApplicationConnection(string connectionId) =>
        applicationConnections.TryAdd(connectionId, 0);

    public void AdmitWorkerConnection(string connectionId) =>
        workerConnections.TryAdd(connectionId, 0);

    public bool IsApplicationConnectionAdmitted(string connectionId) =>
        applicationConnections.ContainsKey(connectionId);

    public bool IsWorkerConnectionAdmitted(string connectionId) =>
        workerConnections.ContainsKey(connectionId);
}
