using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Infinium.Application.Runtime;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(paths.ProductRoot)))[..32];
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
    CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
        instanceId,
        DateTimeOffset.UtcNow,
        TimeSpan.FromHours(12));
    RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
        instanceId,
        authority.FencingEpoch,
        Environment.ProcessId,
        elevated: false,
        DateTimeOffset.UtcNow);
    descriptor.WriteRestricted(paths.ProductRoot);

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
    builder.Services.AddGrpc(options =>
    {
        options.MaxReceiveMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
        options.MaxSendMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
        options.EnableDetailedErrors = false;
    });
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
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
    app.Services.GetRequiredService<ManagedRunExecutor>().RecoverAtStartup();
    await app.RunAsync().ConfigureAwait(false);
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

static void BindRole(ListenOptions listen, string role, string pipeName)
{
    listen.Use(next => connection =>
    {
        connection.Features.Set(new InfiniumPipeRoleFeature(role, pipeName));
        return next(connection);
    });
}
