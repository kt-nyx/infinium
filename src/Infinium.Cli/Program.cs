using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Application.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using CliSummaryDocumentContract = Infinium.Domain.Contracts.CliSummaryDocumentContract;

string? root = FirstOption(args, "--root");
string? command = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)
    && !string.Equals(argument, root, StringComparison.Ordinal));
bool localScopeResults = command is "scope-results" or "scope-reports";
if ((!localScopeResults && (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)))
    || command is not ("start" or "status" or "wait" or "cancel" or "inspect" or "results" or "scope-results" or "scope-reports"))
{
    Usage();
    return 2;
}

bool json = HasFlag(args, "--json");
try
{
    ValidateCommandArguments(args, command);
    if (localScopeResults)
    {
        return command == "scope-reports"
            ? await ScopeReportsAsync(args, json).ConfigureAwait(false)
            : await ScopeResultsAsync(args, json).ConfigureAwait(false);
    }
    await EnsureCoordinatorAsync(root!).ConfigureAwait(false);
    using CancellationTokenSource connectTimeout = new(TimeSpan.FromSeconds(15));
    await using CoordinatorConnection connection = await CoordinatorConnection.ConnectAsync(
        root!,
        connectTimeout.Token).ConfigureAwait(false);
    return command switch
    {
        "start" => await StartAsync(connection, args, json).ConfigureAwait(false),
        "status" => await StatusAsync(connection, args, json, inspect: false).ConfigureAwait(false),
        "inspect" => await StatusAsync(connection, args, json, inspect: true).ConfigureAwait(false),
        "results" => await ResultsAsync(connection, args, json).ConfigureAwait(false),
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

static async Task<int> ScopeResultsAsync(string[] arguments, bool json)
{
    string path = Path.GetFullPath(PositionalAfter(arguments, "scope-results"));
    using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    if (input.Length is < 1 or > 64L * 1024 * 1024)
    {
        throw new InvalidOperationException("The scope-reversion result exceeds its CLI input bound.");
    }
    byte[] bytes = new byte[checked((int)input.Length)];
    input.ReadExactly(bytes);
    using JsonDocument header = JsonDocument.Parse(bytes);
    string schemaId = header.RootElement.GetProperty("schema_id").GetString()
        ?? throw new InvalidDataException("The scope-reversion result omits its schema identity.");
    if (schemaId == Infinium.Domain.Contracts.ScopeReversionV2Contract.SchemaId)
    {
        Infinium.Domain.Contracts.ScopeReversionV2AnalysisContract v2 =
            ScopeReversionV2JsonCodec.Deserialize(bytes);
        if (json)
        {
            await Console.OpenStandardOutput().WriteAsync(ScopeReversionV2JsonCodec.Serialize(v2)).ConfigureAwait(false);
            Console.WriteLine();
        }
        else
        {
            Console.Write(ScopeReversionV2OutputRenderer.RenderHuman(v2));
        }
        return 0;
    }
    Infinium.Domain.Contracts.ScopeReversionAnalysisContract analysis =
        ScopeReversionJsonCodec.Deserialize(bytes);
    if (json)
    {
        await Console.OpenStandardOutput().WriteAsync(ScopeReversionJsonCodec.Serialize(analysis)).ConfigureAwait(false);
        Console.WriteLine();
    }
    else
    {
        Console.Write(ScopeReversionOutputRenderer.RenderHuman(analysis));
    }
    return 0;
}

static async Task<int> ScopeReportsAsync(string[] arguments, bool json)
{
    string path = Path.GetFullPath(PositionalAfter(arguments, "scope-reports"));
    using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    if (input.Length is < 1 or > 64L * 1024 * 1024)
    {
        throw new InvalidOperationException("The scope-reversion result exceeds its report input bound.");
    }
    byte[] bytes = new byte[checked((int)input.Length)];
    input.ReadExactly(bytes);
    using JsonDocument header = JsonDocument.Parse(bytes);
    string schemaId = header.RootElement.GetProperty("schema_id").GetString()
        ?? throw new InvalidDataException("The scope-reversion result omits its schema identity.");
    IReadOnlyList<Infinium.Domain.Contracts.FindingReportDocument> reports =
        schemaId == Infinium.Domain.Contracts.ScopeReversionV2Contract.SchemaId
            ? FindingReportProjection.Project(ScopeReversionV2JsonCodec.Deserialize(bytes))
            : FindingReportProjection.Project(ScopeReversionJsonCodec.Deserialize(bytes));
    if (json)
    {
        await Console.OpenStandardOutput().WriteAsync(JsonSerializer.SerializeToUtf8Bytes(
            reports,
            Infinium.Domain.Contracts.ContractJsonSerializer.Options)).ConfigureAwait(false);
        Console.WriteLine();
        return 0;
    }
    foreach (Infinium.Domain.Contracts.FindingReportDocument report in reports)
    {
        Console.WriteLine($"{report.State}: {report.Title}");
        Console.WriteLine($"  {report.Conclusion}");
        Console.WriteLine($"  Action: {report.RecommendedAction}");
        Console.WriteLine();
    }
    return 0;
}

static async Task<int> ResultsAsync(
    CoordinatorConnection connection,
    string[] arguments,
    bool json)
{
    string runId = PositionalAfter(arguments, "results");
    GetAnalysisOutputResponse response = await connection.Client.GetAnalysisOutputAsync(
        new GetAnalysisOutputRequest
        {
            RunId = new RunId { Value = runId },
            ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
        },
        deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
    if (response.ResultCase != GetAnalysisOutputResponse.ResultOneofCase.Output)
    {
        throw new InvalidOperationException(response.Failure?.Detail ?? "Analysis output query failed.");
    }
    CliSummaryDocumentContract summary = CliSummaryJsonCodec.Deserialize(response.Output.CliSummaryJson.Span);
    if (json)
    {
        await Console.OpenStandardOutput().WriteAsync(response.Output.RunOutputJson.Memory).ConfigureAwait(false);
        Console.WriteLine();
    }
    else
    {
        Console.Write(response.Output.HumanOutput);
    }
    return summary.ExitCode;
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
    string commandId = BoundedCommandId(arguments);
    string? analysisRequestPath = Option(arguments, "--analysis-request");
    byte[]? analysisRequestBytes = null;
    string? requestedRunId = null;
    if (analysisRequestPath is not null)
    {
        string fullPath = Path.GetFullPath(analysisRequestPath);
        using FileStream input = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is < 1 or > ManagedAnalysisOrchestrationRequest.MaximumRequestBytes)
        {
            throw new InvalidOperationException("The managed analysis request exceeds its CLI input bound.");
        }
        analysisRequestBytes = new byte[checked((int)input.Length)];
        input.ReadExactly(analysisRequestBytes);
        ManagedAnalysisOrchestrationRequest managed = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
            analysisRequestBytes, Infinium.Domain.Contracts.ContractJsonSerializer.Options)
            ?? throw new InvalidOperationException("The managed analysis request is malformed.");
        analysisRequestBytes = JsonSerializer.SerializeToUtf8Bytes(
            managed, Infinium.Domain.Contracts.ContractJsonSerializer.Options);
        if (analysisRequestBytes.LongLength > ManagedAnalysisOrchestrationRequest.MaximumRequestBytes)
        {
            throw new InvalidOperationException("The canonical managed analysis request exceeds its CLI input bound.");
        }
        requestedRunId = managed.ExecutionInput.RunId.Value;
        if (managed.ExecutionInput.InstallationSnapshot.ArtifactId.Value != snapshot
            || managed.AnalysisContext.ContextId.Value != context
            || managed.ExecutionInput.EffectiveConfiguration.ArtifactId.Value != configuration
            || managed.ExecutionInput.ResolvedInputManifest.ArtifactId.Value != manifest)
        {
            throw new InvalidOperationException("The managed analysis request differs from the CLI immutable bindings.");
        }
    }
    SubmitRunCommandRequest request = new()
    {
        IdempotencyKey = new DurableCommandId { Value = commandId },
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
    if (analysisRequestBytes is not null)
    {
        request.Start.AnalysisOrchestrationRequestJson = ByteString.CopyFrom(analysisRequestBytes);
        request.Start.RequestedRunId = new RunId { Value = requestedRunId };
    }
    SubmitRunCommandResponse response =
        await SubmitDurableAsync(connection, request).ConfigureAwait(false);
    if (response.Disposition is not (CommandDisposition.Accepted or CommandDisposition.AlreadyAccepted))
    {
        throw new InvalidOperationException($"Start rejected: {response.Failure?.Detail}");
    }

    Write(json, new
    {
        schemaVersion = 1,
        command = "start",
        disposition = response.Disposition.ToString(),
        durableCommandId = commandId,
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
    string commandId = BoundedCommandId(arguments);
    if (Option(arguments, "--command-id") is not null)
    {
        GetDurableCommandResponse prior = await connection.Client.GetDurableCommandAsync(
            new GetDurableCommandRequest
            {
                DurableCommandId = new DurableCommandId { Value = commandId },
            },
            deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
        if (prior.Status is not null)
        {
            if (!string.Equals(prior.Status.RunId?.Value, runId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Durable command '{commandId}' is already bound to another run.");
            }

            if (prior.Status.Disposition is not (
                    CommandDisposition.Accepted or CommandDisposition.AlreadyAccepted)
                || prior.Status.AcceptedInput?.CommandKind != DurableCommandKind.Cancel)
            {
                throw new InvalidOperationException(
                    $"Durable command '{commandId}' is already bound to different command inputs.");
            }

            Write(json, new
            {
                schemaVersion = 1,
                command = "cancel",
                runId,
                durableCommandId = commandId,
                disposition = CommandDisposition.AlreadyAccepted.ToString(),
            });
            return 0;
        }
    }

    GetRunResponse current = await connection.Client.GetRunAsync(
        new GetRunRequest { RunId = new RunId { Value = runId } },
        deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
    if (current.ResultCase != GetRunResponse.ResultOneofCase.Run)
    {
        throw new InvalidOperationException(current.Failure?.Detail ?? "Run lookup failed.");
    }

    SubmitRunCommandResponse response = await SubmitDurableAsync(
        connection,
        new SubmitRunCommandRequest
        {
            IdempotencyKey = new DurableCommandId { Value = commandId },
            Cancel = new CancelCommand
            {
                RunId = new RunId { Value = runId },
                ExpectedLifecycleGeneration = current.Run.Summary.LifecycleGeneration,
            },
        }).ConfigureAwait(false);
    if (response.Disposition is not (CommandDisposition.Accepted or CommandDisposition.AlreadyAccepted))
    {
        throw new InvalidOperationException($"Cancel rejected: {response.Failure?.Detail}");
    }

    Write(json, new
    {
        schemaVersion = 1,
        command = "cancel",
        runId,
        durableCommandId = commandId,
        disposition = response.Disposition.ToString(),
    });
    return 0;
}

static async Task<SubmitRunCommandResponse> SubmitDurableAsync(
    CoordinatorConnection connection,
    SubmitRunCommandRequest request)
{
    string commandId = request.IdempotencyKey.Value;
    try
    {
        return await connection.Client.SubmitRunCommandAsync(
            request,
            deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
    }
    catch (Grpc.Core.RpcException exception) when (
        exception.StatusCode is Grpc.Core.StatusCode.DeadlineExceeded
            or Grpc.Core.StatusCode.Unavailable
            or Grpc.Core.StatusCode.Cancelled)
    {
        try
        {
            GetDurableCommandResponse durable =
                await connection.Client.GetDurableCommandAsync(
                    new GetDurableCommandRequest
                    {
                        DurableCommandId = new DurableCommandId { Value = commandId },
                    },
                    deadline: DateTime.UtcNow.AddSeconds(15)).ResponseAsync.ConfigureAwait(false);
            if (durable.Status is not null
                && durable.Status.Disposition is (
                    CommandDisposition.Accepted or CommandDisposition.AlreadyAccepted)
                && DurableStatusMatchesRequest(durable.Status, request))
            {
                return new SubmitRunCommandResponse
                {
                    Disposition = CommandDisposition.AlreadyAccepted,
                    DurableCommandId = durable.Status.DurableCommandId,
                    DurableTransitionId = durable.Status.DurableTransitionId,
                    RunId = durable.Status.RunId,
                };
            }
        }
        catch (Grpc.Core.RpcException)
        {
            // Preserve the durable identity in the final indeterminate diagnostic.
        }

        throw new InvalidOperationException(
            $"Command '{commandId}' has an indeterminate transport result; retry the identical command with --command-id {commandId}.",
            exception);
    }
}

static bool DurableStatusMatchesRequest(
    DurableCommandStatus status,
    SubmitRunCommandRequest request)
{
    DurableCommandInputIdentity? accepted = status.AcceptedInput;
    if (accepted is null)
    {
        return false;
    }

    return request.CommandCase switch
    {
        SubmitRunCommandRequest.CommandOneofCase.Start =>
            accepted.CommandKind == DurableCommandKind.Start
            && accepted.ExpectedLifecycleGeneration == 0
            && accepted.InstallationSnapshotId?.Value
                == request.Start.InstallationSnapshotId?.Value
            && accepted.AnalysisContextId?.Value
                == request.Start.AnalysisContextId?.Value
            && accepted.EffectiveScanConfigurationId?.Value
                == request.Start.EffectiveScanConfigurationId?.Value
            && accepted.ResolvedInputManifestId?.Value
                == request.Start.ResolvedInputManifestId?.Value
            && accepted.ManualInitiationKind == request.Start.InitiationKind
            && ManagedStartIdentityMatches(accepted, request.Start),
        SubmitRunCommandRequest.CommandOneofCase.Pause =>
            MatchesTransition(
                status,
                accepted,
                DurableCommandKind.Pause,
                request.Pause.RunId?.Value,
                request.Pause.ExpectedLifecycleGeneration),
        SubmitRunCommandRequest.CommandOneofCase.Resume =>
            MatchesTransition(
                status,
                accepted,
                DurableCommandKind.Resume,
                request.Resume.RunId?.Value,
                request.Resume.ExpectedLifecycleGeneration),
        SubmitRunCommandRequest.CommandOneofCase.Cancel =>
            MatchesTransition(
                status,
                accepted,
                DurableCommandKind.Cancel,
                request.Cancel.RunId?.Value,
                request.Cancel.ExpectedLifecycleGeneration),
        _ => false,
    };
}

static bool ManagedStartIdentityMatches(
    DurableCommandInputIdentity accepted,
    ManualStartCommand requested)
{
    if (requested.AnalysisOrchestrationRequestJson.Length == 0)
    {
        return string.IsNullOrEmpty(accepted.RequestedRunId?.Value)
            && accepted.AnalysisOrchestrationRequest is null;
    }
    byte[] bytes = requested.AnalysisOrchestrationRequestJson.ToByteArray();
    return accepted.RequestedRunId?.Value == requested.RequestedRunId?.Value
        && accepted.AnalysisOrchestrationRequest?.Algorithm == DigestAlgorithm.Sha256
        && accepted.AnalysisOrchestrationRequest.Value.Span.SequenceEqual(SHA256.HashData(bytes))
        && accepted.AnalysisOrchestrationRequest.SizeBytes == checked((ulong)bytes.LongLength);
}

static bool MatchesTransition(
    DurableCommandStatus status,
    DurableCommandInputIdentity accepted,
    DurableCommandKind expectedKind,
    string? expectedRunId,
    ulong expectedGeneration) =>
    accepted.CommandKind == expectedKind
    && accepted.ExpectedLifecycleGeneration == expectedGeneration
    && string.Equals(status.RunId?.Value, expectedRunId, StringComparison.Ordinal);

static string BoundedCommandId(string[] arguments)
{
    string value = Option(arguments, "--command-id") ?? Guid.NewGuid().ToString("N");
    if (string.IsNullOrWhiteSpace(value)
        || value.Length > 128
        || value.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
    {
        throw new ArgumentException(
            "--command-id must contain 1-128 letters, digits, hyphens, or underscores.");
    }

    return value;
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
    string? found = null;
    for (int index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], name, StringComparison.Ordinal))
        {
            if (found is not null)
            {
                throw new ArgumentException($"Option '{name}' may be supplied only once.");
            }

            found = arguments[index + 1];
        }
    }

    return found;
}

static string? FirstOption(string[] arguments, string name)
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

static void ValidateCommandArguments(string[] arguments, string command)
{
    HashSet<string> valueOptions = new(StringComparer.Ordinal)
    {
        "--root",
    };
    int expectedPositionals;
    switch (command)
    {
        case "start":
            valueOptions.UnionWith(
                ["--snapshot", "--context", "--configuration", "--manifest", "--command-id", "--analysis-request"]);
            expectedPositionals = 0;
            break;
        case "wait":
            valueOptions.Add("--timeout-seconds");
            expectedPositionals = 1;
            break;
        case "cancel":
            valueOptions.Add("--command-id");
            expectedPositionals = 1;
            break;
        case "scope-results":
        case "scope-reports":
            valueOptions.Remove("--root");
            expectedPositionals = 1;
            break;
        default:
            expectedPositionals = 1;
            break;
    }

    HashSet<string> seen = new(StringComparer.Ordinal);
    int commandCount = 0;
    int positionalCount = 0;
    for (int index = 0; index < arguments.Length; index++)
    {
        string argument = arguments[index];
        if (string.Equals(argument, command, StringComparison.Ordinal))
        {
            commandCount++;
            continue;
        }

        if (string.Equals(argument, "--json", StringComparison.Ordinal))
        {
            if (!seen.Add(argument))
            {
                throw new ArgumentException("Flag '--json' may be supplied only once.");
            }

            continue;
        }

        if (argument.StartsWith("--", StringComparison.Ordinal))
        {
            if (!valueOptions.Contains(argument))
            {
                throw new ArgumentException($"Unknown option '{argument}'.");
            }

            if (!seen.Add(argument))
            {
                throw new ArgumentException($"Option '{argument}' may be supplied only once.");
            }

            if (++index >= arguments.Length
                || arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{argument}' requires a value.");
            }

            continue;
        }

        positionalCount++;
    }

    if (commandCount != 1 || positionalCount != expectedPositionals)
    {
        throw new ArgumentException($"Command '{command}' received an invalid positional argument set.");
    }
}

static void Usage() =>
    Console.Error.WriteLine(
        """
        Usage:
          Infinium.Cli --root <absolute-product-root> start --snapshot <id> --context <id> --configuration <id> --manifest <id> [--analysis-request <json-path>] [--command-id <id>] [--json]
          Infinium.Cli --root <absolute-product-root> status <run-id> [--json]
          Infinium.Cli --root <absolute-product-root> wait <run-id> [--timeout-seconds <1..3600>] [--json]
          Infinium.Cli --root <absolute-product-root> cancel <run-id> [--command-id <id>] [--json]
          Infinium.Cli --root <absolute-product-root> inspect <run-id> [--json]
          Infinium.Cli --root <absolute-product-root> results <run-id> [--json]
          Infinium.Cli scope-results <scope-reversion-analysis-json-path> [--json]
          Infinium.Cli scope-reports <scope-reversion-analysis-json-path> [--json]
        """);
