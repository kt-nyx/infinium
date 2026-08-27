using System.Globalization;
using System.IO.Pipes;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Infinium.Application.Evaluation;
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
    builder.Services.AddSingleton<TargetedVerificationExecutor>();
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
        app.Services.GetRequiredService<TargetedVerificationExecutor>().RecoverAtStartup();
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
