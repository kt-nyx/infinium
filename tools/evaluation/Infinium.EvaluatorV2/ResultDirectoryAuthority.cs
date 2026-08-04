using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Infinium.EvaluatorV2;

internal sealed class ResultDirectoryAuthority : IDisposable
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private readonly List<SafeFileHandle> directoryLeases = [];

    private ResultDirectoryAuthority(string root)
    {
        Root = root;
    }

    internal string Root { get; }

    internal static ResultDirectoryAuthority Create(string resultDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultDirectory);
        string root = Path.GetFullPath(resultDirectory);
        if (Directory.Exists(root) || File.Exists(root))
        {
            throw new IOException("The result directory must not already exist.");
        }

        string parent = Path.GetDirectoryName(root)
            ?? throw new InvalidDataException("The result directory has no parent.");
        if (!Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException("The result directory parent does not exist.");
        }

        ValidateDirectoryChain(parent);
        Directory.CreateDirectory(root);
        ResultDirectoryAuthority authority = new(root);
        try
        {
            ValidateDirectoryChain(root);
            if (OperatingSystem.IsWindows())
            {
                foreach (string directory in EnumerateDirectoryChain(root))
                {
                    SafeFileHandle handle = CreateFile(
                        directory,
                        0,
                        FileShareRead | FileShareWrite,
                        IntPtr.Zero,
                        OpenExisting,
                        FileFlagBackupSemantics,
                        IntPtr.Zero);
                    if (handle.IsInvalid)
                    {
                        handle.Dispose();
                        throw new IOException(
                            $"Unable to pin result-path authority for '{directory}'.",
                            new Win32Exception(Marshal.GetLastPInvokeError()));
                    }

                    authority.directoryLeases.Add(handle);
                }

                ValidateDirectoryChain(root);
            }

            return authority;
        }
        catch
        {
            authority.Dispose();
            TryDelete(root);
            throw;
        }
    }

    internal FileStream OpenNew(string fileName)
    {
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || fileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new InvalidDataException("Evaluator output names must be single file names.");
        }

        ValidateDirectoryChain(Root);
        return new FileStream(
            Path.Combine(Root, fileName),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.WriteThrough);
    }

    public void Dispose()
    {
        foreach (SafeFileHandle handle in directoryLeases)
        {
            handle.Dispose();
        }

        directoryLeases.Clear();
    }

    internal static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The caller still reports result_write; cleanup is best effort after a failed publication.
        }
    }

    private static void ValidateDirectoryChain(string path)
    {
        foreach (string directory in EnumerateDirectoryChain(path))
        {
            DirectoryInfo info = new(directory);
            if (!info.Exists
                || (info.Attributes & FileAttributes.ReparsePoint) != 0
                || info.LinkTarget is not null)
            {
                throw new InvalidDataException("The result path cannot traverse a symbolic link or reparse point.");
            }
        }
    }

    private static Stack<string> EnumerateDirectoryChain(string path)
    {
        Stack<string> directories = new();
        DirectoryInfo? current = new(Path.GetFullPath(path));
        while (current is not null)
        {
            directories.Push(current.FullName);
            current = current.Parent;
        }

        return directories;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
