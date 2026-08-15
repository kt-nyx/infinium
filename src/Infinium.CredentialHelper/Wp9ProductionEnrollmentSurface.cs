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
    bool HelperProcessOwned,
    bool SameSession,
    bool InputDesktopAvailable,
    bool NotCloaked,
    bool OnMonitor,
    bool Enabled,
    bool Focused,
    bool Foreground,
    bool Active,
    int ReadinessChecks,
    int PreReadinessIgnoredActions,
    int MessagePumpIterations,
    string TerminalState,
    bool WindowDestroyed,
    bool BufferCleared,
    bool NativeEditEmptyVerified,
    bool ThreadJoined);

internal sealed class Wp9ProductionEntryFailureException(
    Wp9ProductionEntryEvidence evidence,
    Exception innerException)
    : Exception("WP9 production entry stopped with retained helper-local evidence.", innerException)
{
    internal Wp9ProductionEntryEvidence Evidence { get; } = evidence;
}

internal sealed record Wp9ProductionReadinessSnapshot(
    bool WindowVisible,
    bool EditVisible,
    bool InitiallyBlank,
    bool HelperProcessOwned,
    bool SameSession,
    bool InputDesktopAvailable,
    bool NotCloaked,
    bool OnMonitor,
    bool Enabled,
    bool Focused,
    bool Foreground,
    bool Active);

internal static class Wp9ProductionEntryReadinessOracle
{
    internal static bool IsReady(Wp9ProductionReadinessSnapshot value) =>
        value.WindowVisible && value.EditVisible && value.InitiallyBlank
        && value.HelperProcessOwned && value.SameSession && value.InputDesktopAvailable
        && value.NotCloaked && value.OnMonitor && value.Enabled && value.Focused
        && value.Foreground && value.Active;

    internal static bool AdmitAction(bool ready, string action) =>
        ready && action is "submit" or "cancel";

    internal static bool IsAdmissibleCharacterLength(int length, int maximum) =>
        length is > 0 && length <= maximum;

    internal static bool ShouldClearPreReadinessContent(bool ready, int currentLength) =>
        !ready && currentLength > 0;

    internal static bool BufferCleanupComplete(bool managedCleared, bool nativeEditEmptyVerified) =>
        managedCleared && nativeEditEmptyVerified;
}

internal sealed record Wp9ProductionHiddenPumpProbe(
    bool PreReadySubmitRejected,
    bool ReadySubmitAdmitted,
    bool ReadyCancelAdmitted,
    bool PreReadyContentCleared,
    bool NativeBufferEmpty,
    bool HelperProcessOwned,
    bool InputDesktopMatched,
    bool WindowDestroyed,
    bool ThreadJoined);

internal sealed record Wp9ProductionLaunchOptions(nint ExcludedHandle, bool SpawnContainmentProbe);

internal sealed record Wp9ProductionFailureClassification(string Stage, string Reason);

internal static class Wp9ProductionFailureClassifier
{
    internal static Wp9ProductionFailureClassification ContainmentLaunch() =>
        new("launch-boundary", "containment-launch-failure");
}

internal static class Wp9ProductionLaunchContract
{
    internal static bool TryParse(string[] options, out Wp9ProductionLaunchOptions? parsed)
    {
        parsed = null;
        if (options is not ["--excluded-handle-probe", string handle,
            "--spawn-containment-probe", "1"]
            || !nint.TryParse(handle, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out nint excluded))
        {
            return false;
        }
        parsed = new(excluded, true);
        return true;
    }
}

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
        Wp9ProductionEntryCapture capture;
        try
        {
            capture = Wp9ProductionMaskedEntryDialog.Capture(
                TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));
        }
        catch (Wp9ProductionEntryFailureException failure)
        {
            EntryEvidence = failure.Evidence;
            throw;
        }
        using (capture)
        {
        EntryEvidence = capture.Evidence;
        if (capture.TerminalState == "cancelled")
        {
            throw new OperationCanceledException("The owner cancelled production credential enrollment.");
        }
        byte[] secret = capture.DetachSecret();
        canarySecret = secret.ToArray();
        return secret;
        }
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
    private const uint GwOwner = 4;
    private const uint MonitorDefaultToNull = 0;
    private const int UoiName = 2;
    private const uint DwmwaCloaked = 14;
    private const uint DesktopReadObjects = 0x0001;
    private const uint DesktopSwitchDesktop = 0x0100;

    internal static Wp9ProductionHiddenPumpProbe RunNonLiveHiddenPumpProbe()
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        bool preReadyRejected = false;
        bool submitAdmitted = false;
        bool cancelAdmitted = false;
        bool contentCleared = false;
        bool nativeEmpty = false;
        bool processOwned = false;
        bool desktopMatched = false;
        bool destroyed = false;
        Exception? failure = null;
        Thread thread = new(() =>
        {
            nint edit = 0;
            try
            {
                edit = CreateWindowExW(0, "EDIT", null, 0, 0, 0, 0, 0,
                    new nint(-3), 0, GetModuleHandleW(null), 0);
                if (edit == 0) { throw new Win32Exception(Marshal.GetLastWin32Error()); }
                bool submit = false;
                bool cancel = false;
                int ignored = 0;
                _ = PostMessageW(edit, WmKeyDown, VkReturn, 0);
                PumpMessages(edit, false, ref submit, ref cancel, ref ignored);
                preReadyRejected = !submit && ignored == 1;
                _ = PostMessageW(edit, WmKeyDown, VkReturn, 0);
                PumpMessages(edit, true, ref submit, ref cancel, ref ignored);
                submitAdmitted = submit;
                submit = false;
                _ = PostMessageW(edit, WmKeyDown, VkEscape, 0);
                PumpMessages(edit, true, ref submit, ref cancel, ref ignored);
                cancelAdmitted = cancel;
                _ = SetWindowTextW(edit, "disposable-dummy-text");
                if (Wp9ProductionEntryReadinessOracle.ShouldClearPreReadinessContent(
                    ready: false, GetWindowTextLengthW(edit)))
                {
                    _ = SetWindowTextW(edit, string.Empty);
                    contentCleared = true;
                }
                nativeEmpty = GetWindowTextLengthW(edit) == 0;
                _ = GetWindowThreadProcessId(edit, out uint ownerProcess);
                processOwned = ownerProcess == (uint)Environment.ProcessId;
                nint input = OpenInputDesktop(0, false, DesktopReadObjects | DesktopSwitchDesktop);
                nint current = GetThreadDesktop(GetCurrentThreadId());
                desktopMatched = input != 0 && current != 0
                    && string.Equals(GetDesktopName(input), GetDesktopName(current), StringComparison.Ordinal);
                if (input != 0) { _ = CloseDesktop(input); }
            }
            catch (Exception exception) { failure = exception; }
            finally { if (edit != 0) { destroyed = DestroyWindow(edit) || !IsWindow(edit); } }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool joined = thread.Join(TimeSpan.FromSeconds(5));
        if (failure is not null) { throw new InvalidOperationException("Hidden WP9 pump probe failed.", failure); }
        return new(preReadyRejected, submitAdmitted, cancelAdmitted, contentCleared,
            nativeEmpty, processOwned, desktopMatched, destroyed, joined);
    }

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
        bool nativeEditEmptyVerified = false;
        bool helperProcessOwned = false;
        bool sameSession = false;
        bool inputDesktopAvailable = false;
        bool notCloaked = false;
        bool onMonitor = false;
        bool enabled = false;
        bool focused = false;
        bool foreground = false;
        bool active = false;
        int readinessChecks = 0;
        int preReadinessIgnoredActions = 0;
        int messagePumpIterations = 0;
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
            bool readyForActions = false;
            try
            {
                nint module = GetModuleHandleW(null);
                windowProcedure = (handle, message, wParam, lParam) =>
                {
                    if (message == WmClose)
                    {
                        if (Wp9ProductionEntryReadinessOracle.AdmitAction(readyForActions, "cancel")) { cancelRequested = true; }
                        else { preReadinessIgnoredActions++; }
                        return 0;
                    }
                    if (message == WmCommand && handle == window)
                    {
                        int command = unchecked((int)(wParam & 0xffff));
                        if (command == SubmitButtonId && lParam == submit)
                        {
                            if (Wp9ProductionEntryReadinessOracle.AdmitAction(readyForActions, "submit")) { submitRequested = true; }
                            else { preReadinessIgnoredActions++; }
                            return 0;
                        }
                        if (command == CancelButtonId && lParam == cancel)
                        {
                            if (Wp9ProductionEntryReadinessOracle.AdmitAction(readyForActions, "cancel")) { cancelRequested = true; }
                            else { preReadinessIgnoredActions++; }
                            return 0;
                        }
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
                uint currentSession = (uint)Process.GetCurrentProcess().SessionId;
                _ = SetForegroundWindow(window);
                _ = SetFocus(edit);
                Stopwatch readiness = Stopwatch.StartNew();
                while (readiness.Elapsed < readinessDeadline)
                {
                    messagePumpIterations++;
                    PumpMessages(edit, readyForActions, ref submitRequested, ref cancelRequested,
                        ref preReadinessIgnoredActions);
                    readinessChecks++;
                    int preReadinessLength = GetWindowTextLengthW(edit);
                    if (Wp9ProductionEntryReadinessOracle.ShouldClearPreReadinessContent(
                        readyForActions, preReadinessLength))
                    {
                        preReadinessIgnoredActions++;
                        _ = SetWindowTextW(edit, string.Empty);
                    }
                    bool liveBlank = GetWindowTextLengthW(edit) == 0;
                    _ = GetWindowThreadProcessId(window, out uint ownerProcessId);
                    helperProcessOwned = ownerProcessId == (uint)Environment.ProcessId && GetWindow(window, GwOwner) == 0;
                    sameSession = ProcessIdToSessionId(ownerProcessId, out uint ownerSession)
                        && ownerSession == currentSession;
                    nint inputDesktop = OpenInputDesktop(0, false, DesktopReadObjects | DesktopSwitchDesktop);
                    nint threadDesktop = GetThreadDesktop(GetCurrentThreadId());
                    inputDesktopAvailable = inputDesktop != 0 && threadDesktop != 0
                        && string.Equals(GetDesktopName(inputDesktop), GetDesktopName(threadDesktop),
                            StringComparison.Ordinal);
                    if (inputDesktop != 0) { _ = CloseDesktop(inputDesktop); }
                    int cloaked = 1;
                    notCloaked = DwmGetWindowAttribute(window, DwmwaCloaked, out cloaked, sizeof(int)) == 0
                        && cloaked == 0;
                    onMonitor = IsActuallyOnMonitor(window);
                    enabled = IsWindowEnabled(window) && IsWindowEnabled(edit)
                        && IsWindowEnabled(submit) && IsWindowEnabled(cancel);
                    focused = GetFocus() == edit;
                    foreground = GetForegroundWindow() == window;
                    active = GetActiveWindow() == window;
                    Wp9ProductionReadinessSnapshot snapshot = new(
                        IsWindowVisible(window), IsWindowVisible(edit), initiallyBlank && liveBlank,
                        helperProcessOwned, sameSession, inputDesktopAvailable, notCloaked,
                        onMonitor, enabled, focused, foreground, active);
                    if (Wp9ProductionEntryReadinessOracle.IsReady(snapshot))
                    {
                        PumpMessages(edit, ready: false, ref submitRequested, ref cancelRequested,
                            ref preReadinessIgnoredActions);
                        if (GetWindowTextLengthW(edit) != 0)
                        {
                            preReadinessIgnoredActions++;
                            _ = SetWindowTextW(edit, string.Empty);
                            continue;
                        }
                        ready = true;
                        readyForActions = true;
                        submitRequested = false;
                        cancelRequested = false;
                        break;
                    }
                    Thread.Sleep(25);
                }
                if (!ready) { throw new TimeoutException("WP9 entry did not become ready within ten seconds."); }
                Stopwatch response = Stopwatch.StartNew();
                while (response.Elapsed < responseDeadline)
                {
                    messagePumpIterations++;
                    PumpMessages(edit, readyForActions, ref submitRequested, ref cancelRequested,
                        ref preReadinessIgnoredActions);
                    if (cancelRequested) { terminal = "cancelled"; return; }
                    if (submitRequested)
                    {
                        int liveLength = GetWindowTextLengthW(edit);
                        if (!Wp9ProductionEntryReadinessOracle.IsAdmissibleCharacterLength(
                            liveLength, WindowsCredentialManagerStore.MaximumBlobBytes))
                        {
                            submitRequested = false;
                            continue;
                        }
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
                nativeEditEmptyVerified = edit == 0;
                if (edit != 0)
                {
                    _ = SetWindowTextW(edit, string.Empty);
                    nativeEditEmptyVerified = GetWindowTextLengthW(edit) == 0;
                }
                Array.Clear(captured);
                cleared = Wp9ProductionEntryReadinessOracle.BufferCleanupComplete(
                    managedCleared: true, nativeEditEmptyVerified);
                if (edit != 0 && originalEditProcedure != 0) { _ = SetWindowLongPtrW(edit, GwlWndProc, originalEditProcedure); }
                destroyed = window == 0 || DestroyWindow(window) || !IsWindow(window);
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
            initiallyBlank, ready, helperProcessOwned, sameSession, inputDesktopAvailable,
            notCloaked, onMonitor, enabled, focused, foreground, active, readinessChecks,
            preReadinessIgnoredActions, messagePumpIterations, terminal, destroyed, cleared,
            nativeEditEmptyVerified, joined);
        if (failure is not null)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new Wp9ProductionEntryFailureException(evidence, failure);
        }
        if (terminal == "cancelled") { return new([], terminal, evidence); }
        if (terminal != "submitted" || result.Length == 0)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new InvalidOperationException("WP9 production entry did not produce a valid terminal submission.");
        }
        return new(result, terminal, evidence);
    }

    private static void PumpMessages(
        nint edit,
        bool ready,
        ref bool submit,
        ref bool cancel,
        ref int preReadinessIgnoredActions)
    {
        while (PeekMessageW(out NativeMessage message, 0, 0, 0, PmRemove))
        {
            if (message.Message == WmKeyDown && message.Window == edit)
            {
                if (message.WParam == VkReturn)
                {
                    if (Wp9ProductionEntryReadinessOracle.AdmitAction(ready, "submit")) { submit = true; }
                    else { preReadinessIgnoredActions++; }
                    continue;
                }
                if (message.WParam == VkEscape)
                {
                    if (Wp9ProductionEntryReadinessOracle.AdmitAction(ready, "cancel")) { cancel = true; }
                    else { preReadinessIgnoredActions++; }
                    continue;
                }
            }
            _ = TranslateMessage(in message);
            _ = DispatchMessageW(in message);
        }
    }

    private static string? GetDesktopName(nint desktop)
    {
        _ = GetUserObjectInformationW(desktop, UoiName, null, 0, out uint required);
        if (required is 0 or > 4096) { return null; }
        char[] name = new char[checked((int)(required / sizeof(char)) + 1)];
        return GetUserObjectInformationW(desktop, UoiName, name, checked((uint)(name.Length * sizeof(char))), out _)
            ? new string(name, 0, Array.IndexOf(name, '\0') is int end && end >= 0 ? end : name.Length)
            : null;
    }

    private static bool IsActuallyOnMonitor(nint window)
    {
        nint monitor = MonitorFromWindow(window, MonitorDefaultToNull);
        MonitorInfo info = new() { Size = checked((uint)Marshal.SizeOf<MonitorInfo>()) };
        return monitor != 0 && GetWindowRect(window, out NativeRect windowRect)
            && GetMonitorInfoW(monitor, ref info)
            && windowRect.Right > info.Monitor.Left && windowRect.Left < info.Monitor.Right
            && windowRect.Bottom > info.Monitor.Top && windowRect.Top < info.Monitor.Bottom;
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
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { internal int Left; internal int Top; internal int Right; internal int Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        internal uint Size; internal NativeRect Monitor; internal NativeRect Work; internal uint Flags;
    }
    private delegate nint NativeWindowProcedure(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassExW(ref WindowClassEx windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool UnregisterClassW(string className, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowExW(uint exStyle, string className, string? windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("kernel32.dll")] private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint threadId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformationW(nint handle, int index, [Out] char[]? information, uint length, out uint required);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] private static extern nint GetFocus();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetActiveWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLengthW(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(nint window, [Out] char[] text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowTextW(nint window, string text);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtrW(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtrW(nint window, int index, nint value);
    [DllImport("user32.dll")] private static extern nint CallWindowProcW(nint procedure, nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out NativeMessage message, nint window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(in NativeMessage message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(in NativeMessage message);
    [DllImport("user32.dll")] private static extern bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);
}
