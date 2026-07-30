using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Mo2;

public sealed record SnapshotControlObservation(
    string Role,
    string SourcePath,
    bool Exists,
    long ByteLength,
    Sha256Fingerprint Fingerprint,
    string Base64Bytes,
    string? PhysicalObjectIdentity);

public sealed record SnapshotRootObservation(
    string Role,
    string SourcePath,
    string PhysicalObjectIdentity);

public sealed record SnapshotStructuralObservation(
    string RootRole,
    string RelativePath,
    bool IsDirectory,
    long ByteLength,
    long LastWriteUtcTicks,
    FileAttributes Attributes,
    string? PhysicalObjectIdentity);

public sealed record SnapshotMappingDependency(
    string MappingId,
    string SourceRoot,
    string VirtualPrefix,
    Sha256Fingerprint MapperFingerprint,
    bool Admitted);

public sealed record Mo2SnapshotDependencyManifest(
    ContractVersion SchemaVersion,
    Sha256Fingerprint CanonicalFingerprint,
    string AdapterId,
    string ManagerId,
    string ExplicitSelectedProfileName,
    RuntimeTargetContext DeclaredRuntimeTarget,
    ExecutableIdentity Mo2ExecutableIdentity,
    ExecutableIdentity SkyrimGamePluginIdentity,
    ExecutableIdentity RuntimeExecutableIdentity,
    IReadOnlyList<string> EnabledMapperSha256s,
    IReadOnlyList<string> QualifiedMapperSha256s,
    IReadOnlyList<SnapshotControlObservation> ControlObservations,
    IReadOnlyList<SnapshotRootObservation> RootObservations,
    IReadOnlyList<SnapshotStructuralObservation> StructuralObservations,
    IReadOnlyList<SnapshotMappingDependency> MappingDependencies);

public static class Mo2SnapshotCanonicalization
{
    public static OpaqueId ComputeSnapshotId(
        Sha256Fingerprint canonicalFingerprint,
        UtcTimestamp capturedAt)
    {
        ArgumentNullException.ThrowIfNull(canonicalFingerprint);
        ArgumentNullException.ThrowIfNull(capturedAt);
        string occurrence = HashUtf8(
            $"{canonicalFingerprint.Value}|{capturedAt.Value:O}");
        return new OpaqueId($"snapshot-{occurrence[..24].ToLowerInvariant()}");
    }

    public static Sha256Fingerprint Compute(
        Mo2SnapshotDependencyManifest dependencies,
        OpaqueId instanceId,
        OpaqueId profileId)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(profileId);

        string roots = string.Join(
            '\n',
            dependencies.RootObservations
                .OrderBy(root => root.Role, StringComparer.Ordinal)
                .Select(root => $"{root.Role}|{root.PhysicalObjectIdentity}"));
        string entries = string.Join(
            '\n',
            dependencies.StructuralObservations
                .OrderBy(entry => entry.RootRole, StringComparer.Ordinal)
                .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .Select(entry => FormattableString.Invariant(
                    $"{entry.RootRole}|{entry.RelativePath}|{entry.IsDirectory}|{entry.ByteLength}|{entry.LastWriteUtcTicks}|{entry.Attributes}|{entry.PhysicalObjectIdentity}")));
        string structuralFingerprint = HashUtf8($"{roots}\n{entries}");

        StringBuilder canonical = new();
        canonical.AppendLine(dependencies.AdapterId)
            .AppendLine(structuralFingerprint)
            .AppendLine(instanceId.Value)
            .AppendLine(profileId.Value)
            .AppendLine(dependencies.ManagerId)
            .AppendLine(dependencies.ExplicitSelectedProfileName)
            .Append(dependencies.DeclaredRuntimeTarget.Platform)
            .Append('|')
            .Append(dependencies.DeclaredRuntimeTarget.DistributionChannel)
            .Append('|')
            .AppendLine(dependencies.DeclaredRuntimeTarget.ApplicationId)
            .AppendLine(dependencies.Mo2ExecutableIdentity.Sha256)
            .AppendLine(dependencies.SkyrimGamePluginIdentity.Sha256)
            .AppendLine(dependencies.RuntimeExecutableIdentity.Sha256);
        foreach (string mapper in dependencies.EnabledMapperSha256s
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            canonical.Append("enabled-mapper|").AppendLine(mapper.ToLowerInvariant());
        }

        foreach (string mapper in dependencies.QualifiedMapperSha256s
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            canonical.Append("qualified-mapper|").AppendLine(mapper.ToLowerInvariant());
        }

        foreach (SnapshotControlObservation control in
                 dependencies.ControlObservations.OrderBy(
                     control => control.Role,
                     StringComparer.Ordinal))
        {
            canonical.Append(control.Role)
                .Append('|')
                .Append(control.Exists)
                .Append('|')
                .Append(control.Fingerprint.Value)
                .Append('|')
                .AppendLine(control.PhysicalObjectIdentity ?? string.Empty);
        }

        foreach (SnapshotMappingDependency mapping in
                 dependencies.MappingDependencies.OrderBy(
                     mapping => mapping.MappingId,
                     StringComparer.Ordinal))
        {
            canonical.Append(mapping.MappingId)
                .Append('|')
                .Append(Path.GetFullPath(mapping.SourceRoot))
                .Append('|')
                .Append(mapping.VirtualPrefix.Replace('\\', '/').Trim('/'))
                .Append('|')
                .Append(mapping.MapperFingerprint.Value)
                .Append('|')
                .Append(mapping.Admitted)
                .AppendLine();
        }

        return new Sha256Fingerprint(HashUtf8(canonical.ToString()));
    }

    private static string HashUtf8(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
