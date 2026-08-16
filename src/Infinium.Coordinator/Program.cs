using System.IO.Pipes;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Infinium.Application.Runtime;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1416 // The executable rejects non-Windows hosts before setup.

if (args is ["--wp9-production-profile-enrollment", "--manifest", string wp9Manifest,
    "--manifest-sha256", string wp9ManifestSha256,
    "--output-root", string wp9OutputRoot,
    "--product-root", string wp9ProductRoot])
{
    try
    {
        return await Wp9ProductionProfileEnrollmentRunner.RunAsync(
            wp9Manifest, wp9ManifestSha256, wp9OutputRoot, wp9ProductRoot).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException
        or InvalidOperationException or OperationCanceledException or PlatformNotSupportedException)
    {
        Console.Error.WriteLine($"WP9 production profile enrollment stopped with typed non-secret error: {exception.GetType().Name}");
        return 73;
    }
}

if (args is ["--wp9-production-profile-enrollment", "--manifest", string campaignWp9Manifest,
    "--manifest-sha256", string campaignWp9ManifestSha256,
    "--output-root", string campaignWp9OutputRoot,
    "--product-root", string campaignWp9ProductRoot,
    "--campaign-manifest", string campaignManifest,
    "--campaign-manifest-sha256", string campaignManifestSha256,
    "--campaign-reviewed-candidate", string campaignReviewedCandidate,
    "--campaign-ledger", string campaignLedger])
{
    try
    {
        return await Wp9ProductionProfileEnrollmentRunner.RunAsync(campaignWp9Manifest,
            campaignWp9ManifestSha256, campaignWp9OutputRoot, campaignWp9ProductRoot,
            new(campaignManifest, campaignManifestSha256, campaignReviewedCandidate, campaignLedger))
            .ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException
        or InvalidOperationException or OperationCanceledException or PlatformNotSupportedException)
    {
        Console.Error.WriteLine($"WP9 campaign-derived production enrollment stopped with typed non-secret error: {exception.GetType().Name}");
        return 73;
    }
}

if (args is ["--wp9-campaign-credential-admission-probe", "--manifest", string probeWp9Manifest,
    "--manifest-sha256", string probeWp9ManifestSha256,
    "--campaign-manifest", string probeCampaignManifest,
    "--campaign-manifest-sha256", string probeCampaignManifestSha256,
    "--campaign-reviewed-candidate", string probeReviewedCandidate])
{
    try
    {
        Wp9ProductionProfileEnrollmentRunner.ValidateCampaignAdmissionOnly(probeWp9Manifest,
            probeWp9ManifestSha256, probeCampaignManifest, probeCampaignManifestSha256,
            probeReviewedCandidate);
        Console.WriteLine("WP9 campaign credential admission validated with zero effect.");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
    {
        Console.Error.WriteLine($"WP9 campaign credential admission probe stopped: {exception.GetType().Name}");
        return 74;
    }
}

if (args is ["--wp9-campaign-credential-handoff-admission", "--manifest", string handoffWp9Manifest,
    "--manifest-sha256", string handoffWp9ManifestSha256,
    "--campaign-manifest", string handoffCampaignManifest,
    "--campaign-manifest-sha256", string handoffCampaignManifestSha256,
    "--campaign-reviewed-candidate", string handoffReviewedCandidate,
    "--campaign-ledger", string handoffCampaignLedger])
{
    try
    {
        Wp9ProductionProfileEnrollmentRunner.AdmitCampaignCredentialExecutionHandoff(
            handoffWp9Manifest, handoffWp9ManifestSha256, handoffCampaignManifest,
            handoffCampaignManifestSha256, handoffReviewedCandidate, handoffCampaignLedger,
            DateTimeOffset.UtcNow);
        Console.WriteLine("WP9 campaign credential execution handoff admitted with zero effect.");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
    {
        Console.Error.WriteLine($"WP9 campaign credential handoff admission stopped: {exception.GetType().Name}");
        return 76;
    }
}

if (args is ["--wp9-campaign-credential-evidence-acceptance", "--manifest", string acceptedWp9Manifest,
    "--manifest-sha256", string acceptedWp9ManifestSha256,
    "--campaign-manifest", string acceptedCampaignManifest,
    "--campaign-manifest-sha256", string acceptedCampaignManifestSha256,
    "--campaign-reviewed-candidate", string acceptedReviewedCandidate,
    "--campaign-ledger", string acceptedCampaignLedger,
    "--evidence", string acceptedCredentialEvidence,
    "--record", string acceptedCredentialRecord])
{
    try
    {
        Wp9ProductionProfileEnrollmentRunner.AcceptCampaignCredentialEvidence(acceptedWp9Manifest,
            acceptedWp9ManifestSha256, acceptedCampaignManifest, acceptedCampaignManifestSha256,
            acceptedReviewedCandidate, acceptedCampaignLedger, acceptedCredentialEvidence,
            acceptedCredentialRecord, DateTimeOffset.UtcNow);
        Console.WriteLine("WP9 campaign credential evidence accepted with zero provider effect.");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
    {
        Console.Error.WriteLine($"WP9 campaign credential evidence acceptance stopped: {exception.GetType().Name}");
        return 75;
    }
}

if (args is ["--m1-slice6-campaign-stage", "--stage-manifest", string stageManifest,
    "--stage-manifest-sha256", string stageManifestSha256,
    "--campaign-manifest", string stageCampaignManifest,
    "--campaign-manifest-sha256", string stageCampaignManifestSha256,
    "--campaign-reviewed-candidate", string stageCampaignReviewedCandidate,
    "--credential-manifest", string stageCredentialManifest,
    "--credential-manifest-sha256", string stageCredentialManifestSha256,
    "--campaign-ledger", string stageCampaignLedger,
    "--safety-state-root", string stageSafetyStateRoot,
    "--helper-binary", string stageHelperBinary,
    "--helper-sha256", string stageHelperSha256,
    "--evidence", string stageEvidence])
{
    try
    {
        return await M1Slice6CampaignStageRunner.RunAsync(stageManifest, stageManifestSha256,
            stageCampaignManifest, stageCampaignManifestSha256, stageCampaignReviewedCandidate,
            stageCredentialManifest, stageCredentialManifestSha256, stageCampaignLedger,
            stageSafetyStateRoot, stageHelperBinary, stageHelperSha256, stageEvidence)
            .ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException
        or InvalidOperationException or OperationCanceledException or TimeoutException
        or PlatformNotSupportedException)
    {
        Console.Error.WriteLine($"M1 Slice 6 campaign stage stopped with typed non-secret error: {exception.GetType().Name}");
        return 79;
    }
}

if (args is ["--m1-slice6-campaign-stage-evidence-acceptance", "--stage", string acceptedStageText,
    "--campaign-manifest", string acceptedStageCampaignManifest,
    "--campaign-manifest-sha256", string acceptedStageCampaignManifestSha256,
    "--campaign-reviewed-candidate", string acceptedStageReviewedCandidate,
    "--credential-manifest", string acceptedStageCredentialManifest,
    "--credential-manifest-sha256", string acceptedStageCredentialManifestSha256,
    "--campaign-ledger", string acceptedStageLedger,
    "--stage-manifest", string acceptedStageManifest,
    "--evidence", string acceptedStageEvidence,
    "--record", string acceptedStageRecord]
    && Enum.TryParse(acceptedStageText, ignoreCase: false, out M1Slice6CampaignStage acceptedStage)
    && acceptedStage is M1Slice6CampaignStage.Qualification or M1Slice6CampaignStage.SourceClaimExtraction
        or M1Slice6CampaignStage.CandidateInvestigation)
{
    try
    {
        M1Slice6CampaignStageRunner.AcceptEvidence(acceptedStageCampaignManifest,
            acceptedStageCampaignManifestSha256, acceptedStageReviewedCandidate,
            acceptedStageCredentialManifest, acceptedStageCredentialManifestSha256,
            acceptedStageLedger, acceptedStageManifest, acceptedStageEvidence, acceptedStage, acceptedStageRecord,
            DateTimeOffset.UtcNow);
        Console.WriteLine("M1 Slice 6 campaign stage evidence independently accepted.");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
    {
        Console.Error.WriteLine($"Campaign stage evidence acceptance stopped: {exception.GetType().Name}");
        return 80;
    }
}

if (args is ["--m1-slice6-campaign-composed-evidence-acceptance",
    "--campaign-manifest", string composedCampaignManifest,
    "--campaign-manifest-sha256", string composedCampaignManifestSha256,
    "--campaign-reviewed-candidate", string composedReviewedCandidate,
    "--credential-manifest", string composedCredentialManifest,
    "--credential-manifest-sha256", string composedCredentialManifestSha256,
    "--campaign-ledger", string composedLedger,
    "--evidence", string composedEvidence,
    "--record", string composedRecord])
{
    try
    {
        M1Slice6CampaignStageRunner.CompleteComposedEvidence(composedCampaignManifest,
            composedCampaignManifestSha256, composedReviewedCandidate, composedCredentialManifest,
            composedCredentialManifestSha256, composedLedger, composedEvidence, composedRecord,
            DateTimeOffset.UtcNow);
        Console.WriteLine("M1 Slice 6 composed campaign evidence independently accepted.");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
    {
        Console.Error.WriteLine($"Campaign composed evidence acceptance stopped: {exception.GetType().Name}");
        return 81;
    }
}

if (args is ["--credential-native-qualification-v2", "--manifest", string nativeManifest,
    "--manifest-sha256", string nativeManifestSha256,
    "--output-root", string nativeOutputRoot])
{
    try
    {
        return await CredentialNativeQualificationRunner.RunAsync(
            nativeManifest, nativeManifestSha256, nativeOutputRoot)
            .ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException
        or InvalidOperationException or OperationCanceledException)
    {
        Console.Error.WriteLine($"WP4 v2 coordinator supervisor failed with typed non-secret error: {exception.GetType().Name}");
        return 68;
    }
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Infinium Coordinator currently requires Windows.");
    return 2;
}

string? root = GetOption(args, "--root");
if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
{
    Console.Error.WriteLine("Usage: Infinium.Coordinator --root <absolute-product-root>");
    return 2;
}

if (IsElevated())
{
    Console.Error.WriteLine("Infinium Coordinator refuses elevated execution.");
    return 3;
}

StoragePaths paths;
try
{
    paths = new StoragePaths(root);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

string mutexName = "Local\\Infinium.Coordinator."
    + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(paths.AuthorityIdentity)))[..32];
using Mutex authorityMutex = new(initiallyOwned: false, mutexName);
bool ownsMutex;
try
{
    ownsMutex = authorityMutex.WaitOne(TimeSpan.Zero);
}
catch (AbandonedMutexException)
{
    ownsMutex = true;
}

if (!ownsMutex)
{
    Console.Error.WriteLine("An Infinium coordinator already owns this product root.");
    return 4;
}

try
{
    using AuthoritativeStore store = new(paths);
    string instanceId = Guid.NewGuid().ToString("N");
    CoordinatorAuthority authority =
        store.AcquireCoordinatorAuthorityAfterProcessExclusion(
        instanceId,
        DateTimeOffset.UtcNow,
        TimeSpan.FromMinutes(5));
    RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
        instanceId,
        authority.FencingEpoch,
        Environment.ProcessId,
        elevated: false,
        DateTimeOffset.UtcNow);
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();
    if (!args.Contains("--quiet", StringComparer.Ordinal))
    {
        builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
    }
    builder.Services.AddSingleton(store);
    builder.Services.AddSingleton(authority);
    builder.Services.AddSingleton(descriptor);
    builder.Services.AddSingleton<CoordinatorRuntime>();
    builder.Services.AddSingleton<WorkerBootstrapRegistry>();
    builder.Services.AddSingleton<ManagedRunExecutor>();
    builder.Services.AddSingleton<SnapshotCaptureExecutor>();
    builder.Services.AddHostedService<CoordinatorLeaseRenewalService>();
    builder.Services.AddGrpc(options =>
    {
        options.MaxReceiveMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
        options.MaxSendMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
        options.EnableDetailedErrors = false;
    });
    builder.WebHost.UseNamedPipes(options =>
    {
        // The explicit protected DACL below cannot be combined with the
        // CurrentUserOnly flag. A NETWORK deny ACE supplies the remote-client
        // rejection while the sole allow ACE binds access to this logon user.
        options.CurrentUserOnly = false;
        options.PipeSecurity = BuildPipeSecurity();
        options.MaxReadBufferSize = ProtocolConstants.MaximumMessageBytes;
        options.MaxWriteBufferSize = ProtocolConstants.MaximumMessageBytes;
    });
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxConcurrentConnections = 32;
        options.Limits.Http2.MaxStreamsPerConnection = 32;
        options.ListenNamedPipe(
            descriptor.ApplicationPipe,
            listen =>
            {
                listen.Protocols = HttpProtocols.Http2;
                BindRole(listen, "application", descriptor.ApplicationPipe);
            });
        options.ListenNamedPipe(
            descriptor.WorkerPipe,
            listen =>
            {
                listen.Protocols = HttpProtocols.Http2;
                BindRole(listen, "worker", descriptor.WorkerPipe);
            });
    });

    WebApplication app = builder.Build();
    app.MapGrpcService<ApplicationGrpcService>();
    app.MapGrpcService<WorkerGrpcService>();
    await app.StartAsync().ConfigureAwait(false);
    try
    {
        paths.WriteCoordinatorRuntimeDescriptor(descriptor.Serialize());
        store.RecordAuditEvent(
            "runtime-descriptor-written",
            "runtime-descriptor",
            RuntimeDescriptor.FileName,
            DateTimeOffset.UtcNow);
        app.Services.GetRequiredService<ManagedRunExecutor>().RecoverAtStartup();
        app.Services.GetRequiredService<SnapshotCaptureExecutor>().RecoverAtStartup();
        await app.WaitForShutdownAsync().ConfigureAwait(false);
    }
    finally
    {
        await app.StopAsync().ConfigureAwait(false);
    }

    return 0;
}
finally
{
    authorityMutex.ReleaseMutex();
}

static string? GetOption(string[] arguments, string name)
{
    for (int index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], name, StringComparison.Ordinal))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

static bool IsElevated()
{
    using WindowsIdentity identity = WindowsIdentity.GetCurrent();
    WindowsPrincipal principal = new(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

static PipeSecurity BuildPipeSecurity()
{
    SecurityIdentifier user = WindowsIdentity.GetCurrent().User
        ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
    PipeSecurity security = new();
    security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
    security.SetOwner(user);
    security.AddAccessRule(new PipeAccessRule(
        new SecurityIdentifier(WellKnownSidType.NetworkSid, domainSid: null),
        PipeAccessRights.FullControl,
        AccessControlType.Deny));
    security.AddAccessRule(new PipeAccessRule(
        user,
        PipeAccessRights.FullControl,
        AccessControlType.Allow));
    return security;
}

static void BindRole(ListenOptions listen, string role, string pipeName)
{
    listen.Use(next => connection =>
    {
        IConnectionNamedPipeFeature? pipeFeature =
            connection.Features.Get<IConnectionNamedPipeFeature>();
        string rejectionReason = "The connection does not expose its named-pipe transport.";
        if (pipeFeature is null
            || !WindowsPipePeerValidator.IsCurrentUserPeer(
                pipeFeature.NamedPipe,
                out rejectionReason))
        {
            connection.Abort(new ConnectionAbortedException(rejectionReason));
            return Task.CompletedTask;
        }

        connection.Features.Set(new InfiniumPipeRoleFeature(role, pipeName));
        return next(connection);
    });
}

#pragma warning restore CA1416
