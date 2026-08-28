using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Infinium.DesktopHost;

internal static class ProcessElevationPolicy
{
    private const uint TokenQuery = 0x0008;
    private const int TokenElevation = 20;

    public static bool IsElevated()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out SafeAccessTokenHandle token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        using (token)
        {
            int size = Marshal.SizeOf<TokenElevationValue>();
            nint buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, TokenElevation, buffer, size, out _))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return Marshal.PtrToStructure<TokenElevationValue>(buffer).TokenIsElevated != 0;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevationValue { public int TokenIsElevated; }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle tokenHandle, int tokenInformationClass, nint tokenInformation, int tokenInformationLength, out int returnLength);
}
