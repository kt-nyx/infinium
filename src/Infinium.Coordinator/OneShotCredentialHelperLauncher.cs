using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.Coordinator;

public sealed record HelperProcessReceipt(
    int ProcessId,
    int ExitCode,
    string BinarySha256,
    HelperReceiptV2 Receipt,
    int InheritedPrivateHandleCount,
    int StandardProtocolHandleCount,
    int ListenerCount,
    int NetworkOperationCount,
    int NativeCredentialOperationCount,
    bool ProcessTreeTerminated,
    bool RetryAttempted);

public sealed class OneShotCredentialHelperLauncher
{
    private readonly string helperBinary;

    public OneShotCredentialHelperLauncher(string helperBinary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperBinary);
        this.helperBinary = Path.GetFullPath(helperBinary);
        if (!Path.IsPathFullyQualified(this.helperBinary) || !File.Exists(this.helperBinary)
            || !string.Equals(Path.GetFileName(this.helperBinary), "Infinium.CredentialHelper.exe", StringComparison.Ordinal))
        {
            throw new ArgumentException("The launcher requires the exact repository-built helper executable.", nameof(helperBinary));
        }
    }

    public async Task<HelperProcessReceipt> ExecuteAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        ValidateOutboundSequence(bootstrap, assignment, finalRevalidation);

        using AnonymousPipeServerStream request = new(
            PipeDirection.Out, HandleInheritability.Inheritable, 64 * 1024);
        using AnonymousPipeServerStream response = new(
            PipeDirection.In, HandleInheritability.Inheritable, 64 * 1024);
        ProcessStartInfo start = new()
        {
            FileName = helperBinary,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = Path.GetDirectoryName(helperBinary)!,
        };
        start.ArgumentList.Add("--request-handle");
        start.ArgumentList.Add(request.GetClientHandleAsString());
        start.ArgumentList.Add("--response-handle");
        start.ArgumentList.Add(response.GetClientHandleAsString());
        start.Environment.Clear();
        start.Environment.Add("DOTNET_EnableDiagnostics", "0");

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The exact one-shot helper could not be launched.");
        request.DisposeLocalCopyOfClientHandle();
        response.DisposeLocalCopyOfClientHandle();
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        try
        {
            await HelperPrivateProtocolV2.WriteAsync(request, bootstrap, bounded.Token).ConfigureAwait(false);
            await HelperPrivateProtocolV2.WriteAsync(request, assignment, bounded.Token).ConfigureAwait(false);
            if (finalRevalidation is not null)
            {
                await HelperPrivateProtocolV2.WriteAsync(request, finalRevalidation, bounded.Token).ConfigureAwait(false);
            }
            HelperPrivateFrameV2 terminal = await HelperPrivateProtocolV2.ReadAsync(
                response, finalRevalidation is null ? 3UL : 4UL, bounded.Token).ConfigureAwait(false);
            request.Close();
            await process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
            if (process.ExitCode != 0 || terminal.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Receipt)
            {
                throw new InvalidOperationException("The one-shot helper failed without an admissible terminal receipt.");
            }

            return new HelperProcessReceipt(
                process.Id,
                process.ExitCode,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(helperBinary))).ToLowerInvariant(),
                terminal.Receipt,
                2,
                0,
                0,
                0,
                0,
                process.HasExited,
                false);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
    }

    private static void ValidateOutboundSequence(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(assignment);
        if (bootstrap.Sequence != 1 || bootstrap.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Bootstrap
            || assignment.Sequence != 2 || assignment.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Assignment)
        {
            throw new InvalidDataException("The helper launch requires one exact bootstrap and immutable assignment.");
        }
        bool dispatch = assignment.Assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        if (dispatch != (finalRevalidation is not null)
            || finalRevalidation is not null && (finalRevalidation.Sequence != 3
                || finalRevalidation.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.DispatchRevalidation))
        {
            throw new InvalidDataException("Provider dispatch requires exactly one final revalidation; credential operations forbid it.");
        }
    }
}
