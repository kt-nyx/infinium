using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Coordinator;

internal static class WindowsPipePeerValidator
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    public static bool IsCurrentUserPeer(
        NamedPipeServerStream pipe,
        out string rejectionReason)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint processId))
        {
            rejectionReason = "The named-pipe client process identity is unavailable.";
            return false;
        }

        return IsCurrentUserProcess(processId, out rejectionReason);
    }

    internal static bool IsCurrentUserProcess(
        uint processId,
        out string rejectionReason)
    {
        if (!OperatingSystem.IsWindows())
        {
            rejectionReason = "Windows process-token validation is unavailable.";
            return false;
        }

        using SafeProcessHandle process = OpenProcess(
            ProcessQueryLimitedInformation,
            inheritHandle: false,
            processId);
        if (process.IsInvalid
            || !OpenProcessToken(process, TokenQuery, out SafeAccessTokenHandle clientToken))
        {
            rejectionReason = "The named-pipe client token cannot be inspected.";
            return false;
        }

        using (clientToken)
        using (WindowsIdentity client = new(clientToken.DangerousGetHandle()))
        using (WindowsIdentity current = WindowsIdentity.GetCurrent(TokenAccessLevels.Query))
        {
            if (client.User is null
                || current.User is null
                || !client.User.Equals(current.User))
            {
                rejectionReason = "The named-pipe client belongs to another Windows user.";
                return false;
            }

            if (!ProcessIdToSessionId(processId, out uint clientSession)
                || !ProcessIdToSessionId(checked((uint)Environment.ProcessId), out uint currentSession)
                || clientSession != currentSession)
            {
                rejectionReason = "The named-pipe client belongs to another logon session.";
                return false;
            }

            if (GetElevation(clientToken) != GetElevation(current.AccessToken))
            {
                rejectionReason = "The named-pipe client elevation differs from the coordinator.";
                return false;
            }

            if (GetIntegrityRid(clientToken) != GetIntegrityRid(current.AccessToken))
            {
                rejectionReason = "The named-pipe client integrity level differs from the coordinator.";
                return false;
            }
        }

        rejectionReason = string.Empty;
        return true;
    }

    private static bool GetElevation(SafeAccessTokenHandle token)
    {
        IntPtr buffer = GetTokenInformationBuffer(token, TokenInformationClass.TokenElevation);
        try
        {
            return Marshal.ReadInt32(buffer) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static uint GetIntegrityRid(SafeAccessTokenHandle token)
    {
        IntPtr buffer = GetTokenInformationBuffer(token, TokenInformationClass.TokenIntegrityLevel);
        try
        {
            IntPtr sid = Marshal.ReadIntPtr(buffer);
            byte count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
            if (count == 0)
            {
                throw new InvalidOperationException("The process integrity SID has no subauthorities.");
            }

            return checked((uint)Marshal.ReadInt32(GetSidSubAuthority(sid, checked((uint)(count - 1)))));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IntPtr GetTokenInformationBuffer(
        SafeAccessTokenHandle token,
        TokenInformationClass informationClass)
    {
        _ = GetTokenInformation(token, informationClass, IntPtr.Zero, 0, out uint length);
        if (length == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)length));
        if (!GetTokenInformation(token, informationClass, buffer, length, out _))
        {
            Marshal.FreeHGlobal(buffer);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return buffer;
    }

    private enum TokenInformationClass
    {
        TokenElevation = 20,
        TokenIntegrityLevel = 25,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        SafeProcessHandle processHandle,
        uint desiredAccess,
        out SafeAccessTokenHandle tokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        SafeAccessTokenHandle tokenHandle,
        TokenInformationClass tokenInformationClass,
        IntPtr tokenInformation,
        uint tokenInformationLength,
        out uint returnLength);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthority);
}
