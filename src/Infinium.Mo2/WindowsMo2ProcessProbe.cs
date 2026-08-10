using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

#pragma warning disable CA1859 // Contract-shaped collection abstractions keep capture seams narrow.

namespace Infinium.Mo2;


internal sealed class WindowsMo2ProcessProbe : IMo2ProcessProbe
{
    public bool IsRunning(string exactExecutablePath)
    {
        string expected = Path.GetFullPath(exactExecutablePath);
        WindowsObjectIdentity expectedIdentity =
            WindowsReadOnlyObjectIdentity.Open(expected, directory: false);
        string processName = Path.GetFileNameWithoutExtension(expected);
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    string? path = process.MainModule?.FileName;
                    if (path is not null
                        && WindowsReadOnlyObjectIdentity.Open(path, directory: false)
                            .CanonicalValue == expectedIdentity.CanonicalValue)
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (
                    exception is Win32Exception
                        or InvalidOperationException
                        or NotSupportedException)
                {
                    if (string.Equals(
                            process.ProcessName,
                            processName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
