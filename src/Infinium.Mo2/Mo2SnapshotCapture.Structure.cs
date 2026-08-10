using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

#pragma warning disable CA1859 // Contract-shaped collection abstractions keep capture seams narrow.

namespace Infinium.Mo2;


public sealed partial class Mo2SnapshotCapture
{
    private StructuralCapture CaptureStructure(
        ValidatedPaths paths,
        IReadOnlyList<AdmittedMapping> admittedMappings,
        ICollection<SnapshotGap> gaps,
        CancellationToken cancellationToken)
    {
        List<StructuralEntry> entries = [];
        WindowsObjectIdentity instanceIdentity =
            WindowsReadOnlyObjectIdentity.Open(paths.InstanceRoot, directory: true);
        ValidateOpenedPath(instanceIdentity, paths.InstanceRoot);
        Dictionary<string, string> rootIdentities = new(StringComparer.Ordinal)
        {
            ["instance"] = instanceIdentity.CanonicalValue,
        };
        CaptureRoot("profile", paths.ProfileRoot, entries);
        CaptureRoot("mods", paths.ModsRoot, entries);
        CaptureRoot("overwrite", paths.OverwriteRoot, entries);
        CaptureRoot("game-data", paths.GameDataRoot, entries);
        foreach (AdmittedMapping mapping in admittedMappings)
        {
            CaptureRoot($"mapping:{mapping.Mapping.MappingId}", mapping.SourceRoot, entries);
        }

        string canonical = string.Join(
            '\n',
            entries
                .OrderBy(entry => entry.Root, StringComparer.Ordinal)
                .ThenBy(entry => entry.RelativePath, PathComparer)
                .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .Select(entry => FormattableString.Invariant(
                    $"{entry.Root}|{entry.RelativePath}|{entry.IsDirectory}|{entry.Length}|{entry.LastWriteUtcTicks}|{entry.Attributes}|{entry.ObjectIdentity}")));
        string roots = string.Join(
            '\n',
            rootIdentities
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}|{pair.Value}"));
        return new StructuralCapture(HashUtf8($"{roots}\n{canonical}"), entries, rootIdentities);

        void CaptureRoot(
            string rootName,
            string root,
            List<StructuralEntry> destination)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowsReadOnlyObjectHandle rootHandle =
                WindowsReadOnlyObjectIdentity.OpenHandle(root, directory: true);
            ValidateOpenedPath(rootHandle.Identity, root);
            if ((rootHandle.Identity.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                rootHandle.Dispose();
                throw new InvalidDataException(
                    $"Reparse points are not qualified capture roots: {root}");
            }

            rootIdentities[rootName] = rootHandle.Identity.CanonicalValue;
            uint rootVolume = rootHandle.Identity.VolumeSerialNumber;
            Stack<(WindowsReadOnlyObjectHandle Handle, string RelativePath)> directories = new();
            directories.Push((rootHandle, string.Empty));
            try
            {
                while (directories.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    (WindowsReadOnlyObjectHandle directory, string parentRelative) =
                        directories.Pop();
                    using (directory)
                    {
                        string expectedDirectory = string.IsNullOrEmpty(parentRelative)
                            ? root
                            : Path.Combine(
                                root,
                                parentRelative.Replace('/', Path.DirectorySeparatorChar));
                        WindowsReadOnlyObjectIdentity.ValidateContainedObject(
                            directory.Identity,
                            expectedDirectory,
                            rootVolume);
                        foreach (WindowsReadOnlyDirectoryEntry listed
                                 in WindowsReadOnlyObjectIdentity.EnumerateChildren(directory))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string relative = string.IsNullOrEmpty(parentRelative)
                                ? NormalizeRelativePath(listed.Name)
                                : $"{parentRelative}/{NormalizeRelativePath(listed.Name)}";
                            beforeHandleRelativeEntryOpen?.Invoke(rootName, relative);
                            WindowsReadOnlyObjectHandle child =
                                WindowsReadOnlyObjectIdentity.OpenRelative(
                                    directory,
                                    listed.Name,
                                    listed.IsDirectory);
                            WindowsObjectIdentity identity = child.Identity;
                            FileAttributes attributes = identity.Attributes;
                            bool isDirectory =
                                (attributes & FileAttributes.Directory) != 0;
                            string expectedChild = Path.Combine(
                                directory.Identity.FinalPath,
                                listed.Name);
                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                if (identity.VolumeSerialNumber
                                        != directory.Identity.VolumeSerialNumber
                                    || !PathComparer.Equals(
                                        identity.FinalPath,
                                        Path.GetFullPath(expectedChild)))
                                {
                                    child.Dispose();
                                    throw new InvalidDataException(
                                        "A reparse object escaped its opened snapshot parent.");
                                }

                                child.Dispose();
                                destination.Add(new StructuralEntry(
                                    rootName,
                                    relative,
                                    isDirectory,
                                    0,
                                    identity.LastWriteUtcTicks,
                                    attributes,
                                    identity.CanonicalValue));
                                gaps.Add(new SnapshotGap(
                                    "reparse-point-unsupported",
                                    "filesystem",
                                    $"Reparse point is outside the qualified capture surface: {expectedChild}"));
                                continue;
                            }

                            try
                            {
                                WindowsReadOnlyObjectIdentity.ValidateContainedObject(
                                    identity,
                                    expectedChild,
                                    directory.Identity.VolumeSerialNumber);
                            }
                            catch
                            {
                                child.Dispose();
                                throw;
                            }

                            destination.Add(new StructuralEntry(
                                rootName,
                                relative,
                                isDirectory,
                                identity.ByteLength,
                                identity.LastWriteUtcTicks,
                                attributes,
                                identity.CanonicalValue));
                            if (destination.Count > MaximumEntries)
                            {
                                child.Dispose();
                                throw new InvalidDataException(
                                    $"Snapshot contains more than {MaximumEntries} structural entries.");
                            }

                            if (isDirectory)
                            {
                                directories.Push((child, relative));
                            }
                            else
                            {
                                child.Dispose();
                            }
                        }
                    }
                }
            }
            finally
            {
                while (directories.Count > 0)
                {
                    directories.Pop().Handle.Dispose();
                }
            }
        }
    }

    private static void ValidateOpenedPath(WindowsObjectIdentity identity, string expectedPath)
    {
        if ((identity.Attributes & FileAttributes.ReparsePoint) != 0
            || !PathComparer.Equals(identity.FinalPath, Path.GetFullPath(expectedPath)))
        {
            throw new InvalidDataException(
                "A capture object changed identity, resolved through a reparse point, or escaped its declared path.");
        }
    }

    private static Dictionary<string, ControlFile> ReadControls(ValidatedPaths paths)
    {
        string gameRoot = Path.GetDirectoryName(paths.GameDataRoot)
            ?? throw new InvalidDataException(
                "The admitted game Data root has no parent game directory.");
        Dictionary<string, ControlFile> controls = new(StringComparer.Ordinal)
        {
            ["instance-ini"] = ReadControl(
                paths.InstanceRoot,
                Path.GetRelativePath(paths.InstanceRoot, paths.InstanceIni)),
            ["modlist"] = ReadOptionalControl(paths.ProfileRoot, "modlist.txt"),
            ["plugins"] = ReadOptionalControl(paths.ProfileRoot, "plugins.txt"),
            ["loadorder"] = ReadOptionalControl(paths.ProfileRoot, "loadorder.txt"),
            ["archives"] = ReadOptionalControl(paths.ProfileRoot, "archives.txt"),
            ["skyrim-ccc"] = ReadOptionalControl(gameRoot, "Skyrim.ccc"),
        };
        return controls;
    }

    private static void AddMetaControls(
        ValidatedPaths paths,
        StructuralCapture structure,
        IDictionary<string, ControlFile> controls)
    {
        foreach ((string name, string root) in DiscoverModDirectories(paths, structure))
        {
            string relative = $"{NormalizeRelativePath(name)}/meta.ini";
            if (structure.Entries.Any(entry =>
                    entry.Root == "mods"
                    && !entry.IsDirectory
                    && string.Equals(
                        entry.RelativePath,
                        relative,
                        StringComparison.OrdinalIgnoreCase)))
            {
                controls.Add(
                    $"mod-meta:{name}",
                    ReadControl(paths.ModsRoot, relative));
            }
        }
    }

}
