using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.CredentialHelper;

internal sealed record Wp9ProductionEntryEvidence(
    string Surface,
    bool Masked,
    bool PastePermitted,
    bool HelperOwned,
    bool RendererReceivedSecret,
    bool InitiallyBlank,
    bool Ready,
    string TerminalState,
    bool WindowDestroyed,
    bool BufferCleared,
    bool ThreadJoined);

internal sealed class Wp9ProductionSecretSource : IHelperSecretSource, IDisposable
{
    private byte[] canarySecret = [];
    internal Wp9ProductionEntryEvidence? EntryEvidence { get; private set; }

    public byte[] Capture(HelperAssignmentV2 assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (assignment.AssignmentKind != HelperAssignmentKindV2.Enroll
            || !assignment.AssignmentId.StartsWith("wp9-production-profile/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The WP9 production entry surface accepts only one exact enrollment assignment.");
        }
        using Wp9ProductionEntryCapture capture = Wp9ProductionMaskedEntryDialog.Capture(
            TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));
        EntryEvidence = capture.Evidence;
        if (capture.TerminalState == "cancelled")
        {
            throw new OperationCanceledException("The owner cancelled production credential enrollment.");
        }
        byte[] secret = capture.DetachSecret();
        canarySecret = secret.ToArray();
        return secret;
    }

    internal NativeCanaryEvidence ScanAndClear(
        IReadOnlyList<NativeCanarySurface> surfaces,
        IReadOnlyList<NativeRawTargetCanary> targets)
    {
        try { return NativeCanaryScanner.Scan(canarySecret, targets, surfaces); }
        finally
        {
            CryptographicOperations.ZeroMemory(canarySecret);
            canarySecret = [];
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(canarySecret);
        canarySecret = [];
    }
}

internal sealed class Wp9ProductionEntryCapture(
    byte[] secret,
    string terminalState,
    Wp9ProductionEntryEvidence evidence) : IDisposable
{
    private byte[] secret = secret;
    internal string TerminalState { get; } = terminalState;
    internal Wp9ProductionEntryEvidence Evidence { get; } = evidence;
    internal byte[] DetachSecret()
    {
        byte[] value = secret;
        secret = [];
        return value;
    }
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(secret);
        secret = [];
    }
}

/// <summary>
/// M1-only production enrollment surface. This is intentionally distinct from
/// the consumed WP4 qualification dialog and from the future M2 WPF-parented
/// Settings flow. Clipboard paste is admitted, while copy and cut are blocked.
/// </summary>
internal static class Wp9ProductionMaskedEntryDialog
{
    private const uint WsCaption = 0x00C00000;
    private const uint WsSysMenu = 0x00080000;
    private const uint WsVisible = 0x10000000;
    private const uint WsChild = 0x40000000;
    private const uint WsBorder = 0x00800000;
    private const uint EsPassword = 0x0020;
    private const int GwlStyle = -16;
    private const int GwlWndProc = -4;
    private const uint WmClose = 0x0010;
    private const uint WmCommand = 0x0111;
    private const uint WmKeyDown = 0x0100;
    private const uint WmCut = 0x0300;
    private const uint WmCopy = 0x0301;
    private const uint PmRemove = 0x0001;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int SubmitButtonId = 9101;
    private const int CancelButtonId = 9102;

    internal static Wp9ProductionEntryCapture Capture(TimeSpan readinessDeadline, TimeSpan responseDeadline)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WP9 production credential entry requires Windows.");
        }
        if (readinessDeadline <= TimeSpan.Zero || readinessDeadline > TimeSpan.FromSeconds(10)
            || responseDeadline <= TimeSpan.Zero || responseDeadline > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(responseDeadline));
        }

        byte[] result = [];
        Exception? failure = null;
        string terminal = "failed";
        bool ready = false;
        bool initiallyBlank = false;
        bool destroyed = false;
        bool cleared = false;
        Thread thread = new(() =>
        {
            char[] captured = new char[WindowsCredentialManagerStore.MaximumBlobBytes + 1];
            nint window = 0;
            nint edit = 0;
            nint submit = 0;
            nint cancel = 0;
            string className = $"InfiniumWp9ProductionEntry-{Environment.ProcessId}-{Environment.CurrentManagedThreadId}";
            ushort atom = 0;
            NativeWindowProcedure? windowProcedure = null;
            NativeWindowProcedure? editProcedure = null;
            nint originalEditProcedure = 0;
            bool submitRequested = false;
            bool cancelRequested = false;
            try
            {
                nint module = GetModuleHandleW(null);
                windowProcedure = (handle, message, wParam, lParam) =>
                {
                    if (message == WmClose) { cancelRequested = true; return 0; }
                    if (message == WmCommand && handle == window)
                    {
                        int command = unchecked((int)(wParam & 0xffff));
                        if (command == SubmitButtonId && lParam == submit) { submitRequested = true; return 0; }
                        if (command == CancelButtonId && lParam == cancel) { cancelRequested = true; return 0; }
                    }
                    return DefWindowProcW(handle, message, wParam, lParam);
                };
                WindowClassEx definition = new()
                {
                    Size = checked((uint)Marshal.SizeOf<WindowClassEx>()),
                    Instance = module,
                    WindowProcedure = Marshal.GetFunctionPointerForDelegate(windowProcedure),
                    ClassName = className,
                };
                atom = RegisterClassExW(ref definition);
                if (atom == 0) { throw new Win32Exception(Marshal.GetLastWin32Error(), "WP9 entry class registration failed."); }
                window = CreateWindowExW(0, className, "Infinium OpenAI API key enrollment",
                    WsCaption | WsSysMenu | WsVisible, 100, 100, 600, 220, 0, 0, module, 0);
                nint instruction = CreateWindowExW(0, "STATIC",
                    "Paste the OpenAI API key for the exact authorized profile. The value remains inside this helper.",
                    WsChild | WsVisible, 20, 15, 550, 35, window, 0, 0, 0);
                edit = CreateWindowExW(0, "EDIT", null, WsChild | WsVisible | WsBorder | EsPassword,
                    20, 58, 550, 28, window, 0, 0, 0);
                submit = CreateWindowExW(0, "BUTTON", "Submit", WsChild | WsVisible,
                    365, 105, 95, 30, window, SubmitButtonId, 0, 0);
                cancel = CreateWindowExW(0, "BUTTON", "Cancel", WsChild | WsVisible,
                    475, 105, 95, 30, window, CancelButtonId, 0, 0);
                if (window == 0 || instruction == 0 || edit == 0 || submit == 0 || cancel == 0
                    || (GetWindowLongPtrW(edit, GwlStyle).ToInt64() & EsPassword) == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WP9 masked entry controls failed to initialize.");
                }
                editProcedure = (handle, message, wParam, lParam) => message is WmCut or WmCopy
                    ? 0
                    : CallWindowProcW(originalEditProcedure, handle, message, wParam, lParam);
                originalEditProcedure = SetWindowLongPtrW(
                    edit, GwlWndProc, Marshal.GetFunctionPointerForDelegate(editProcedure));
                if (originalEditProcedure == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WP9 entry clipboard boundary failed.");
                }
                initiallyBlank = GetWindowTextLengthW(edit) == 0;
                _ = SetForegroundWindow(window);
                _ = SetFocus(edit);
                Stopwatch readiness = Stopwatch.StartNew();
                while (readiness.Elapsed < readinessDeadline)
                {
                    PumpMessages(edit, ref submitRequested, ref cancelRequested);
                    if (cancelRequested) { terminal = "cancelled"; return; }
                    if (IsWindowVisible(window) && IsWindowVisible(edit) && GetFocus() == edit && initiallyBlank)
                    {
                        ready = true;
                        break;
                    }
                    Thread.Sleep(25);
                }
                if (!ready) { throw new TimeoutException("WP9 entry did not become ready within ten seconds."); }
                Stopwatch response = Stopwatch.StartNew();
                while (response.Elapsed < responseDeadline)
                {
                    PumpMessages(edit, ref submitRequested, ref cancelRequested);
                    if (cancelRequested) { terminal = "cancelled"; return; }
                    if (submitRequested)
                    {
                        int length = GetWindowTextW(edit, captured, captured.Length);
                        if (length <= 0) { submitRequested = false; continue; }
                        result = Encoding.UTF8.GetBytes(captured, 0, length);
                        if (result.Length == 0 || result.Length > WindowsCredentialManagerStore.MaximumBlobBytes)
                        {
                            CryptographicOperations.ZeroMemory(result);
                            result = [];
                            submitRequested = false;
                            continue;
                        }
                        terminal = "submitted";
                        return;
                    }
                    Thread.Sleep(10);
                }
                terminal = "timed-out";
                throw new TimeoutException("WP9 production credential entry timed out.");
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                Array.Clear(captured);
                cleared = true;
                if (edit != 0 && originalEditProcedure != 0) { _ = SetWindowLongPtrW(edit, GwlWndProc, originalEditProcedure); }
                if (window != 0) { _ = DestroyWindow(window); destroyed = true; }
                if (atom != 0) { _ = UnregisterClassW(className, GetModuleHandleW(null)); }
                GC.KeepAlive(editProcedure);
                GC.KeepAlive(windowProcedure);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        bool joined = thread.Join(readinessDeadline + responseDeadline + TimeSpan.FromSeconds(5));
        if (!joined) { throw new TimeoutException("WP9 production entry thread exceeded its finite deadline."); }
        Wp9ProductionEntryEvidence evidence = new(
            "wp9-distinct-helper-owned-native-masked-paste-surface", true, true, true, false,
            initiallyBlank, ready, terminal, destroyed, cleared, joined);
        if (failure is not null)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new InvalidOperationException("WP9 production entry failed with a typed helper-local error.", failure);
        }
        if (terminal == "cancelled") { return new([], terminal, evidence); }
        if (terminal != "submitted" || result.Length == 0)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new InvalidOperationException("WP9 production entry did not produce a valid terminal submission.");
        }
        return new(result, terminal, evidence);
    }

    private static void PumpMessages(nint edit, ref bool submit, ref bool cancel)
    {
        while (PeekMessageW(out NativeMessage message, 0, 0, 0, PmRemove))
        {
            if (message.Message == WmKeyDown && message.Window == edit)
            {
                if (message.WParam == VkReturn) { submit = true; continue; }
                if (message.WParam == VkEscape) { cancel = true; continue; }
            }
            _ = TranslateMessage(in message);
            _ = DispatchMessageW(in message);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        internal uint Size; internal uint Style; internal nint WindowProcedure; internal int ClassExtra;
        internal int WindowExtra; internal nint Instance; internal nint Icon; internal nint Cursor;
        internal nint Background; internal string? MenuName; internal string ClassName; internal nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        internal nint Window; internal uint Message; internal nuint WParam; internal nint LParam;
        internal uint Time; internal int X; internal int Y; internal uint Private;
    }
    private delegate nint NativeWindowProcedure(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassExW(ref WindowClassEx windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool UnregisterClassW(string className, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowExW(uint exStyle, string className, string? windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] private static extern nint GetFocus();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLengthW(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(nint window, [Out] char[] text, int maximum);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtrW(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtrW(nint window, int index, nint value);
    [DllImport("user32.dll")] private static extern nint CallWindowProcW(nint procedure, nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out NativeMessage message, nint window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(in NativeMessage message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(in NativeMessage message);
}
