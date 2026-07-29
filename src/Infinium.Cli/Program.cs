using System.Runtime.InteropServices;
using System.Text.Json;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;

string? root = Option(args, "--root");
string? command = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)
    && !string.Equals(argument, root, StringComparison.Ordinal));
if (string.IsNullOrWhiteSpace(root)
    || !Path.IsPathFullyQualified(root)
    || command is not ("start" or "status" or "wait" or "cancel" or "inspect"))
{
    Usage();
    return 2;
}

bool json = HasFlag(args, "--json");
try
{
    await EnsureCoordinatorAsync(root).ConfigureAwait(false);
    using CancellationTokenSource connectTimeout = new(TimeSpan.FromSeconds(15));
    await using CoordinatorConnection connection = await CoordinatorConnection.ConnectAsync(
        root,
        connectTimeout.Token).ConfigureAwait(false);
    return command switch
    {
        "start" => await StartAsync(connection, args, json).ConfigureAwait(false),
        "status" => await StatusAsync(connection, args, json, inspect: false).ConfigureAwait(false),
        "inspect" => await StatusAsync(connection, args, json, inspect: true).ConfigureAwait(false),
        "wait" => await WaitAsync(connection, args, json).ConfigureAwait(false),
        "cancel" => await CancelAsync(connection, args, json).ConfigureAwait(false),
        _ => 2,
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine(Bounded(exception.Message));
    return 1;
}

static async Task<int> StartAsync(
    CoordinatorConnection connection,
    string[] arguments,
    bool json)
{
    string snapshot = RequiredOption(arguments, "--snapshot");
    string context = RequiredOption(arguments, "--context");
    string configuration = RequiredOption(arguments, "--configuration");
    string manifest = RequiredOption(arguments, "--manifest");
    SubmitRunCommandRequest request = new()
    {
        IdempotencyKey = new DurableCommandId { Value = Guid.NewGuid().ToString("N") },
        Start = new ManualStartCommand
        {
            InstallationSnapshotId = new InstallationSnapshotId { Value = snapshot },
            AnalysisContextId = new AnalysisContextId { Value = context },
            EffectiveScanConfigurationId = new ScanConfigurationId { Value = configuration },
            ResolvedInputManifestId = new ResolvedInputManifestId { Value = manifest },
            InitiationKind = ManualInitiationKind.CliUserAction,
            DispatchDeadline = Instant(DateTimeOffset.UtcNow.AddMinutes(5)),
        },
    };
    SubmitRunCommandResponse response = await connection.Client.SubmitRunCommandAsync(
        request,
        deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
    if (response.Disposition is not (CommandDisposition.Accepted or CommandDisposition.AlreadyAccepted))
    {
        throw new InvalidOperationException($"Start rejected: {response.Failure?.Detail}");
    }

    Write(json, new
    {
        schemaVersion = 1,
        command = "start",
        disposition = response.Disposition.ToString(),
        runId = response.RunId.Value,
        immutableBindings = new
        {
            installationSnapshotId = snapshot,
            analysisContextId = context,
            effectiveScanConfigurationId = configuration,
            resolvedInputManifestId = manifest,
        },
    });
    return 0;
}

static async Task<int> StatusAsync(
    CoordinatorConnection connection,
    string[] arguments,
    bool json,
    bool inspect)
{
    string runId = PositionalAfter(arguments, inspect ? "inspect" : "status");
    GetRunResponse response = await connection.Client.GetRunAsync(
        new GetRunRequest { RunId = new RunId { Value = runId } },
        deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
    if (response.ResultCase != GetRunResponse.ResultOneofCase.Run)
    {
        throw new InvalidOperationException(response.Failure?.Detail ?? "Run lookup failed.");
    }

    RunDetail run = response.Run;
    object value = inspect
        ? new
        {
            schemaVersion = 1,
            command = "inspect",
            runId,
            lifecycle = new
            {
                state = run.Summary.LifecycleState.ToString(),
                generation = run.Summary.LifecycleGeneration,
            },
            immutableBindings = new
            {
                installationSnapshotId = run.InstallationSnapshotId.Value,
                analysisContextId = run.AnalysisContextId.Value,
                effectiveScanConfigurationId = run.EffectiveScanConfigurationId.Value,
                resolvedInputManifestId = run.ResolvedInputManifestId.Value,
            },
            replayability = run.ReplayabilityState.ToString(),
            auditability = run.AuditabilityState.ToString(),
            findingOccurrenceCount = run.FindingOccurrenceCount,
            caseOccurrenceCount = run.CaseOccurrenceCount,
            projectionVersion = run.ProjectionVersion.Value,
        }
        : new
        {
            schemaVersion = 1,
            command = "status",
            runId,
            state = run.Summary.LifecycleState.ToString(),
            generation = run.Summary.LifecycleGeneration,
            updatedAt = FromInstant(run.Summary.UpdatedAt),
        };
    Write(json, value);
    return 0;
}

static async Task<int> WaitAsync(
    CoordinatorConnection connection,
    string[] arguments,
    bool json)
{
    string runId = PositionalAfter(arguments, "wait");
    TimeSpan timeout = TimeSpan.FromSeconds(
        int.TryParse(Option(arguments, "--timeout-seconds"), out int seconds)
            ? Math.Clamp(seconds, 1, 3600)
            : 300);
    using CancellationTokenSource cancellation = new(timeout);
    while (true)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        GetRunResponse response = await connection.Client.GetRunAsync(
            new GetRunRequest { RunId = new RunId { Value = runId } },
            deadline: DateTime.UtcNow.AddSeconds(15),
            cancellationToken: cancellation.Token).ResponseAsync.ConfigureAwait(false);
        if (response.ResultCase != GetRunResponse.ResultOneofCase.Run)
        {
            throw new InvalidOperationException(response.Failure?.Detail ?? "Run lookup failed.");
        }

        if (IsTerminal(response.Run.Summary.LifecycleState))
        {
            Write(json, new
            {
                schemaVersion = 1,
                command = "wait",
                runId,
                state = response.Run.Summary.LifecycleState.ToString(),
                generation = response.Run.Summary.LifecycleGeneration,
            });
            return response.Run.Summary.LifecycleState is LifecycleState.Completed
                or LifecycleState.CompletedWithGaps ? 0 : 1;
        }

        await Task.Delay(100, cancellation.Token).ConfigureAwait(false);
    }
}

static async Task<int> CancelAsync(
    CoordinatorConnection connection,
    string[] arguments,
    bool json)
{
    string runId = PositionalAfter(arguments, "cancel");
    GetRunResponse current = await connection.Client.GetRunAsync(
        new GetRunRequest { RunId = new RunId { Value = runId } },
        deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
    if (current.ResultCase != GetRunResponse.ResultOneofCase.Run)
    {
        throw new InvalidOperationException(current.Failure?.Detail ?? "Run lookup failed.");
    }

    SubmitRunCommandResponse response = await connection.Client.SubmitRunCommandAsync(
        new SubmitRunCommandRequest
        {
            IdempotencyKey = new DurableCommandId { Value = Guid.NewGuid().ToString("N") },
            Cancel = new CancelCommand
            {
                RunId = new RunId { Value = runId },
                ExpectedLifecycleGeneration = current.Run.Summary.LifecycleGeneration,
            },
        },
        deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
    if (response.Disposition != CommandDisposition.Accepted)
    {
        throw new InvalidOperationException($"Cancel rejected: {response.Failure?.Detail}");
    }

    Write(json, new
    {
        schemaVersion = 1,
        command = "cancel",
        runId,
        disposition = response.Disposition.ToString(),
    });
    return 0;
}

static async Task EnsureCoordinatorAsync(string productRoot)
{
    try
    {
        using CancellationTokenSource quick = new(TimeSpan.FromMilliseconds(500));
        await using CoordinatorConnection ignored = await CoordinatorConnection.ConnectAsync(
            productRoot,
            quick.Token).ConfigureAwait(false);
        return;
    }
    catch (Exception)
    {
        // A missing or stale descriptor is recovered only by a new single-authority startup.
    }

    string coordinator = Path.Combine(
        AppContext.BaseDirectory,
        "coordinator",
        "Infinium.Coordinator.dll");
    if (!File.Exists(coordinator))
    {
        throw new InvalidOperationException("The coordinator assembly is not installed with the CLI.");
    }

    string dotnet = Path.GetFullPath(Path.Combine(
        RuntimeEnvironment.GetRuntimeDirectory(),
        "..",
        "..",
        "..",
        "dotnet.exe"));
    _ = DetachedProcessLauncher.Start(
        dotnet,
        [coordinator, "--root", productRoot, "--quiet"],
        Path.GetDirectoryName(coordinator)!,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            ["DOTNET_ROOT"] = Path.GetDirectoryName(dotnet)!,
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        });

    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(15);
    Exception? last = null;
    while (DateTimeOffset.UtcNow < deadline)
    {
        try
        {
            using CancellationTokenSource quick = new(TimeSpan.FromSeconds(1));
            await using CoordinatorConnection ignored = await CoordinatorConnection.ConnectAsync(
                productRoot,
                quick.Token).ConfigureAwait(false);
            return;
        }
        catch (Exception exception)
        {
            last = exception;
            await Task.Delay(100).ConfigureAwait(false);
        }
    }

    throw new InvalidOperationException($"Coordinator startup timed out: {last?.Message}");
}

static void Write(bool json, object value)
{
    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
    else
    {
        JsonElement element = JsonSerializer.SerializeToElement(value);
        Console.WriteLine(string.Join(
            " ",
            element.EnumerateObject().Select(property =>
                $"{property.Name}={property.Value.ToString().Replace(' ', '_')}")));
    }
}

static bool IsTerminal(LifecycleState state) =>
    state is LifecycleState.Cancelled
        or LifecycleState.Completed
        or LifecycleState.CompletedWithGaps
        or LifecycleState.Failed
        or LifecycleState.LimitReached
        or LifecycleState.InvalidatedByChangedInput;

static Instant Instant(DateTimeOffset value) =>
    new()
    {
        UnixSeconds = value.ToUnixTimeSeconds(),
        Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
    };

static DateTimeOffset FromInstant(Instant value) =>
    DateTimeOffset.FromUnixTimeSeconds(value.UnixSeconds).AddTicks(value.Nanoseconds / 100);

static string PositionalAfter(string[] arguments, string command)
{
    int index = Array.IndexOf(arguments, command);
    if (index < 0 || index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        throw new ArgumentException($"Command '{command}' requires a run ID.");
    }

    return arguments[index + 1];
}

static string RequiredOption(string[] arguments, string name) =>
    Option(arguments, name) is { Length: > 0 } value && value.Length <= 128
        ? value
        : throw new ArgumentException($"Option '{name}' requires a bounded value.");

static string? Option(string[] arguments, string name)
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

static bool HasFlag(string[] arguments, string name) =>
    arguments.Contains(name, StringComparer.Ordinal);

static string Bounded(string value) => value.Length <= 512 ? value : value[..512];

static void Usage() =>
    Console.Error.WriteLine(
        """
        Usage:
          Infinium.Cli --root <absolute-product-root> start --snapshot <id> --context <id> --configuration <id> --manifest <id> [--json]
          Infinium.Cli --root <absolute-product-root> status <run-id> [--json]
          Infinium.Cli --root <absolute-product-root> wait <run-id> [--timeout-seconds <1..3600>] [--json]
          Infinium.Cli --root <absolute-product-root> cancel <run-id> [--json]
          Infinium.Cli --root <absolute-product-root> inspect <run-id> [--json]
        """);
