using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;
using Infinium.Application.Analysis;
using Infinium.Application.Evaluation;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.AspNetCore.Connections.Features;
using DomainLifecycleState = Infinium.Domain.Contracts.LifecycleState;

namespace Infinium.Coordinator;

internal sealed record AnalysisArtifactCursor(
    string RunId,
    string PublicationIdentity,
    string FilterHash,
    AnalysisArtifactSortOrder SortOrder,
    uint PageSize,
    AnalysisArtifactCursorKey LastKey,
    DateTimeOffset ExpiresAt);

public sealed class ApplicationGrpcService(
    CoordinatorRuntime runtime,
    ManagedRunExecutor executor,
    SnapshotCaptureExecutor snapshotExecutor)
    : ApplicationService.ApplicationServiceBase
{
    public override Task<HandshakeResponse> Negotiate(
        ApplicationHandshakeRequest request,
        ServerCallContext context)
    {
        HandshakeResponse response = BuildHandshake(request, context);
        if (response.Disposition == HandshakeDisposition.Accepted)
        {
            string connectionId = context.GetHttpContext().Connection.Id;
            if (!runtime.TryAdmitApplicationConnection(connectionId))
            {
                response.Disposition = HandshakeDisposition.LimitsRejected;
                response.Failure = Failure(
                    FailureCode.LimitExceeded,
                    "The application connection admission bound is full.");
                return Task.FromResult(response);
            }

            IConnectionLifetimeFeature? lifetime =
                context.GetHttpContext().Features.Get<IConnectionLifetimeFeature>();
            lifetime?.ConnectionClosed.Register(
                () => runtime.ReleaseApplicationConnection(connectionId));
        }

        return Task.FromResult(response);
    }

    public override Task<HealthResponse> Health(HealthRequest request, ServerCallContext context)
    {
        RequireNegotiated(context);
        return Task.FromResult(new HealthResponse
        {
            State = HealthState.Healthy,
            CoordinatorInstanceId = new CoordinatorInstanceId
            {
                Value = runtime.Authority.InstanceId,
            },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
        });
    }

    public override Task<ListRunsResponse> ListRuns(
        ListRunsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.RequestedPageSize == 0
            || request.RequestedPageSize > ProtocolConstants.MaximumPageItems
            || request.Sort.Count > ProtocolConstants.MaximumSortTerms
            || (request.Filter?.LifecycleStates.Count ?? 0)
                + (request.Filter?.CoverageStates.Count ?? 0)
                > ProtocolConstants.MaximumFilterTerms)
        {
            return Task.FromResult(new ListRunsResponse
            {
                Failure = Failure(FailureCode.LimitExceeded, "The query exceeds a finite protocol bound."),
            });
        }

        if ((request.Filter?.LifecycleStates.Count ?? 0) > 0
            || (request.Filter?.CoverageStates.Count ?? 0) > 0
            || request.Filter?.CreatedAtOrAfter is not null
            || request.Filter?.CreatedBefore is not null
            || request.Sort.Count > 0)
        {
            return Task.FromResult(new ListRunsResponse
            {
                Failure = Failure(
                    FailureCode.Unsupported,
                    "Slice 2 supports the deterministic created-at ascending run query only."),
            });
        }

        if (request.ExpectedProjectionVersion is not null
            && request.ExpectedProjectionVersion.Value is not ("" or "1"))
        {
            return Task.FromResult(CursorRejected(
                CursorDisposition.ProjectionInvalidated,
                "The requested projection is no longer current."));
        }

        RunPageCursor? cursor = null;
        if (request.After is not null && request.After.OpaqueValue.Length > 0)
        {
            try
            {
                cursor = DecodeRunCursor(request.After.OpaqueValue);
            }
            catch (InvalidOperationException exception)
            {
                return Task.FromResult(CursorRejected(
                    CursorDisposition.Malformed,
                    Bounded(exception.Message)));
            }
        }

        int requested = checked((int)request.RequestedPageSize);
        IReadOnlyList<RunRecord> runs = runtime.Store.ListRuns(
            requested + 1,
            cursor?.CreatedAt,
            cursor?.RunId);
        bool hasMore = runs.Count > requested;
        IReadOnlyList<RunRecord> items = hasMore ? runs.Take(requested).ToArray() : runs;
        RunPage page = new()
        {
            HasMore = hasMore,
            ProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        page.Items.Add(items.Select(ProtoMapping.ToSummary));
        if (hasMore)
        {
            RunRecord last = items[^1];
            page.Next = new PageCursor
            {
                OpaqueValue = EncodeRunCursor(new RunPageCursor(
                    last.CreatedAt,
                    last.RunId,
                    DateTimeOffset.UtcNow.AddMinutes(5))),
            };
        }

        return Task.FromResult(new ListRunsResponse { Page = page });
    }

    public override Task<GetRunResponse> GetRun(GetRunRequest request, ServerCallContext context)
    {
        RequireNegotiated(context);
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetRunResponse
            {
                ProjectionInvalidated = ProjectionInvalidated(),
            });
        }

        try
        {
            RunRecord run = runtime.Store.GetRun(Required(request.RunId?.Value, "run ID"));
            return Task.FromResult(new GetRunResponse { Run = ProtoMapping.ToDetail(run) });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetRunResponse
            {
                Failure = Failure(FailureCode.NotFound, "The requested run does not exist."),
            });
        }
    }

    public override Task<GetProgressResponse> GetProgress(
        GetProgressRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetProgressResponse
            {
                ProjectionInvalidated = ProjectionInvalidated(),
            });
        }

        try
        {
            RunRecord run = runtime.Store.GetRun(Required(request.RunId?.Value, "run ID"));
            return Task.FromResult(new GetProgressResponse
            {
                Progress = new ProgressSnapshot
                {
                    RunId = new RunId { Value = run.RunId },
                    LifecycleState = ProtoMapping.ToProto(run.State),
                    Progress = ProtoMapping.EmptyProgress(run.State),
                    ProjectionVersion = new ProjectionVersion { Value = "1" },
                    DurableEventSequence = checked((ulong)run.DurableSequence),
                    ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
                },
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetProgressResponse
            {
                Failure = Failure(FailureCode.NotFound, "The requested run does not exist."),
            });
        }
    }

    public override Task<ListFindingsResponse> ListFindings(
        ListFindingsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.RequestedPageSize == 0
            || request.RequestedPageSize > ProtocolConstants.MaximumPageItems)
        {
            return Task.FromResult(new ListFindingsResponse
            {
                Failure = Failure(FailureCode.LimitExceeded, "The page size exceeds its finite bound."),
            });
        }

        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new ListFindingsResponse
            {
                Failure = Failure(
                    FailureCode.ResyncRequired,
                    "The requested projection is no longer current."),
            });
        }

        if (request.After?.OpaqueValue.Length > 0
            || request.SupportStates.Count > 0
            || request.Sort is not null)
        {
            return Task.FromResult(new ListFindingsResponse
            {
                Failure = Failure(
                    FailureCode.Unsupported,
                    "Slice 2 has no finding producer or supported finding query shape."),
            });
        }

        return Task.FromResult(new ListFindingsResponse
        {
            Page = new FindingPage
            {
                HasMore = false,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
            },
        });
    }

    public override Task<GetAnalysisSummaryResponse> GetAnalysisSummary(
        GetAnalysisSummaryRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetAnalysisSummaryResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            AnalysisSummaryPersistenceRecord value = runtime.Store.GetAnalysisSummary(
                Required(request.RunId?.Value, "run ID"));
            return Task.FromResult(new GetAnalysisSummaryResponse
            {
                Summary = new AnalysisSummary
                {
                    RunId = new RunId { Value = value.RunId },
                    FindingCount = checked((ulong)value.FindingCount),
                    SupportedCaseCount = checked((ulong)value.SupportedCaseCount),
                    LeadOnlyCaseCount = checked((ulong)value.LeadOnlyCaseCount),
                    CandidateDecisionCount = checked((ulong)value.CandidateDecisionCount),
                    CoveragePopulationCount = checked((ulong)value.CoveragePopulationCount),
                    GapCount = checked((ulong)value.GapCount),
                    UnsupportedCount = checked((ulong)value.UnsupportedCount),
                    Replay = ToProto(value),
                    ProjectionVersion = new ProjectionVersion { Value = value.ProjectionVersion },
                },
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetAnalysisSummaryResponse
            {
                Failure = Failure(FailureCode.NotFound, "The run has no published analysis result."),
            });
        }
        catch (ArgumentException exception)
        {
            return Task.FromResult(new GetAnalysisSummaryResponse
            {
                Failure = Failure(FailureCode.InvalidArgument, Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetAnalysisReplayResponse> GetAnalysisReplay(
        GetAnalysisReplayRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetAnalysisReplayResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            AnalysisReplayPersistenceRecord value = runtime.Store.GetAnalysisReplay(
                Required(request.RunId?.Value, "run ID"));
            return Task.FromResult(new GetAnalysisReplayResponse { Replay = ToProto(value) });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetAnalysisReplayResponse
            {
                Failure = Failure(FailureCode.NotFound, "The run has no published replay manifest."),
            });
        }
    }

    public override Task<ListAnalysisArtifactsResponse> ListAnalysisArtifacts(
        ListAnalysisArtifactsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new ListAnalysisArtifactsResponse
            {
                CursorRejection = new CursorRejection
                {
                    Disposition = CursorDisposition.ProjectionInvalidated,
                    CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
                    Failure = Failure(FailureCode.ResyncRequired, "The analysis projection was rebuilt."),
                },
            });
        }
        if (request.RequestedPageSize is < 1 or > 100
            || request.Kinds.Count > 7
            || request.States.Count > 20
            || request.Kinds.Any(item => item is AnalysisArtifactKind.Unspecified or AnalysisArtifactKind.Unknown or AnalysisArtifactKind.Unsupported)
            || request.States.Any(item => item is AnalysisArtifactState.Unspecified or AnalysisArtifactState.Unknown)
            || request.Sort is AnalysisArtifactSort.Unknown or AnalysisArtifactSort.Unsupported)
        {
            return Task.FromResult(new ListAnalysisArtifactsResponse
            {
                Failure = Failure(FailureCode.InvalidArgument, "The analysis artifact query exceeds its closed bounds."),
            });
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            string[] kinds = request.Kinds.Select(KindToken).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string[] states = request.States.Select(StateToken).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string publicationIdentity = runtime.Store.GetAnalysisSemanticFingerprint(runId)
                ?? throw new KeyNotFoundException("The run has no published analysis output.");
            AnalysisArtifactSortOrder sortOrder = ArtifactSort(request.Sort);
            string filterHash = QueryHash(kinds, states);
            AnalysisArtifactCursorKey? after = null;
            if (request.After is not null && request.After.OpaqueValue.Length != 0)
            {
                AnalysisArtifactCursor cursor;
                try
                {
                    cursor = JsonSerializer.Deserialize<AnalysisArtifactCursor>(DecodeAuthenticated(request.After.OpaqueValue))
                        ?? throw new InvalidOperationException("The analysis cursor is malformed.");
                }
                catch (Exception exception) when (exception is InvalidOperationException or JsonException)
                {
                    throw new AnalysisCursorException(
                        CursorDisposition.Malformed,
                        "The analysis cursor is malformed: " + exception.Message);
                }
                AnalysisArtifactCursorBindingDisposition binding = AnalysisArtifactCursorBindingPolicy.Validate(
                    new AnalysisArtifactCursorBinding(
                        cursor.RunId, cursor.PublicationIdentity, cursor.FilterHash,
                        cursor.SortOrder, checked((int)cursor.PageSize), cursor.ExpiresAt),
                    new AnalysisArtifactCursorBinding(
                        runId, publicationIdentity, filterHash, sortOrder,
                        checked((int)request.RequestedPageSize), DateTimeOffset.MaxValue),
                    DateTimeOffset.UtcNow);
                if (binding != AnalysisArtifactCursorBindingDisposition.Accepted)
                {
                    throw binding switch
                    {
                        AnalysisArtifactCursorBindingDisposition.Expired => new AnalysisCursorException(
                            CursorDisposition.Expired, "The analysis cursor expired."),
                        AnalysisArtifactCursorBindingDisposition.ScopeMismatch => new AnalysisCursorException(
                            CursorDisposition.ScopeMismatch, "The analysis cursor belongs to another run."),
                        AnalysisArtifactCursorBindingDisposition.PublicationMismatch => new AnalysisCursorException(
                            CursorDisposition.ProjectionInvalidated, "The analysis publication changed."),
                        AnalysisArtifactCursorBindingDisposition.QueryMismatch => new AnalysisCursorException(
                            CursorDisposition.QueryMismatch, "The analysis cursor belongs to another filter or page size."),
                        _ => new AnalysisCursorException(
                            CursorDisposition.SortMismatch, "The analysis cursor belongs to another sort order."),
                    };
                }
                after = cursor.LastKey;
            }
            AnalysisArtifactPagePersistenceRecord result = runtime.Store.ListAnalysisArtifacts(
                runId, kinds.ToHashSet(StringComparer.Ordinal), states.ToHashSet(StringComparer.Ordinal),
                checked((int)request.RequestedPageSize), sortOrder, after);
            AnalysisArtifactPage page = new()
            {
                HasMore = result.HasMore,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
            };
            page.Items.Add(result.Items.Select(ToProto));
            if (result.NextKey is not null)
            {
                page.Next = new PageCursor
                {
                    OpaqueValue = EncodeAuthenticated(JsonSerializer.SerializeToUtf8Bytes(
                        new AnalysisArtifactCursor(
                            runId, publicationIdentity, filterHash, sortOrder, request.RequestedPageSize,
                            result.NextKey, DateTimeOffset.UtcNow.AddMinutes(5)))),
                };
            }
            return Task.FromResult(new ListAnalysisArtifactsResponse { Page = page });
        }
        catch (AnalysisCursorException exception)
        {
            return Task.FromResult(new ListAnalysisArtifactsResponse
            {
                CursorRejection = new CursorRejection
                {
                    Disposition = exception.Disposition,
                    CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
                    Failure = Failure(FailureCode.ResyncRequired, Bounded(exception.Message)),
                },
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new ListAnalysisArtifactsResponse
            {
                Failure = Failure(FailureCode.NotFound, "The run has no published analysis artifact index."),
            });
        }
    }

    public override Task<GetAnalysisProvenanceResponse> GetAnalysisProvenance(
        GetAnalysisProvenanceRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetAnalysisProvenanceResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        if (request.RequestedMaximumEdges is < 1 or > 256)
        {
            return Task.FromResult(new GetAnalysisProvenanceResponse
            {
                Failure = Failure(FailureCode.InvalidArgument, "The provenance edge bound must be between 1 and 256."),
            });
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            string artifactId = Required(request.ArtifactId?.Value, "analysis artifact ID");
            AnalysisArtifactPersistenceRecord artifact = runtime.Store.GetAnalysisArtifact(runId, artifactId);
            AnalysisReplayContract replay = AnalysisReplayJsonCodec.Deserialize(runtime.Store.ReadAnalysisReplay(runId));
            Dictionary<string, ReplayDependencyNodeContract> nodes = replay.Dependencies
                .ToDictionary(item => item.DependencyId.Value, StringComparer.Ordinal);
            string[] dependencyIds = runtime.Store.ListAnalysisDependencyIds(
                runId, artifactId, checked((int)request.RequestedMaximumEdges + 1)).ToArray();
            ReplayDependencyNodeContract[] dependencies = dependencyIds
                .Select(id => nodes.TryGetValue(id, out ReplayDependencyNodeContract? node)
                    ? node : throw new InvalidDataException("The retained replay edge names an absent dependency node."))
                .ToArray();
            AnalysisProvenance provenance = new()
            {
                Artifact = ToProto(artifact),
                Truncated = dependencies.Length > request.RequestedMaximumEdges,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
            };
            provenance.Dependencies.Add(dependencies.Take(checked((int)request.RequestedMaximumEdges)).Select(dependency =>
                new AnalysisArtifactReference
                {
                    ArtifactId = new AnalysisArtifactId { Value = dependency.DependencyId.Value },
                    Kind = AnalysisArtifactKind.Unknown,
                    SchemaId = dependency.Kind,
                    SchemaVersion = new SemanticVersion { Value = dependency.Version.ToString() },
                    Revision = 1,
                    State = ProtoState(JsonNamingPolicy.KebabCaseLower.ConvertName(dependency.State.ToString())),
                    ContentDigest = new ContentDigest
                    {
                        Algorithm = DigestAlgorithm.Sha256,
                        Value = ByteString.CopyFrom(Convert.FromHexString(dependency.Fingerprint.Value)),
                    },
                    ProvenanceId = new AnalysisArtifactId { Value = dependency.DependencyId.Value },
                    DependencyClosureId = new AnalysisArtifactId { Value = artifact.DependencyClosureId },
                }));
            return Task.FromResult(new GetAnalysisProvenanceResponse { Provenance = provenance });
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            return Task.FromResult(new GetAnalysisProvenanceResponse
            {
                Failure = Failure(FailureCode.NotFound, "The requested published analysis artifact does not exist."),
            });
        }
        catch (ArgumentException)
        {
            return Task.FromResult(new GetAnalysisProvenanceResponse
            {
                Failure = Failure(FailureCode.InvalidArgument,
                    "The provenance query identities are malformed."),
            });
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException)
        {
            return Task.FromResult(new GetAnalysisProvenanceResponse
            {
                Failure = Failure(FailureCode.Internal,
                    "The retained provenance graph could not be projected safely."),
            });
        }
    }

    public override Task<GetAnalysisOutputResponse> GetAnalysisOutput(
        GetAnalysisOutputRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetAnalysisOutputResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            byte[] outputBytes = runtime.Store.ReadAnalysisRunOutput(runId);
            byte[] summaryBytes = runtime.Store.ReadAnalysisCliSummary(runId);
            if (outputBytes.LongLength > AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes
                || summaryBytes.LongLength > 128 * 1024)
            {
                throw new InvalidDataException("The retained result exceeds its application query bound.");
            }
            string human = AnalysisOutputRenderer.Render(
                RunOutputJsonCodec.Deserialize(outputBytes), CliSummaryJsonCodec.Deserialize(summaryBytes));
            GetAnalysisOutputResponse response = new()
            {
                Output = new AnalysisOutputPayload
                {
                    RunOutputJson = ByteString.CopyFrom(outputBytes),
                    CliSummaryJson = ByteString.CopyFrom(summaryBytes),
                    HumanOutput = human,
                    ProjectionVersion = new ProjectionVersion { Value = "1" },
                },
            };
            if (response.CalculateSize() > ProtocolConstants.MaximumMessageBytes)
            {
                throw new InvalidDataException("The retained result exceeds the application protocol message bound.");
            }
            return Task.FromResult(response);
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetAnalysisOutputResponse
            {
                Failure = Failure(FailureCode.NotFound, "The run has no published analysis output."),
            });
        }
        catch (InvalidDataException exception)
        {
            return Task.FromResult(new GetAnalysisOutputResponse
            {
                Failure = Failure(FailureCode.LimitExceeded, Bounded(exception.Message)),
            });
        }
    }

    public override Task<SubmitRunCommandResponse> SubmitRunCommand(
        SubmitRunCommandRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            string commandId = Required(request.IdempotencyKey?.Value, "durable command ID");
            bool replay = DurableCommandExists(commandId);
            if (!replay && !runtime.TryAdmitNewDurableCommand(DateTimeOffset.UtcNow))
            {
                return Task.FromResult(new SubmitRunCommandResponse
                {
                    Disposition = CommandDisposition.Rejected,
                    Failure = Failure(
                        FailureCode.LimitExceeded,
                        "The new durable-command rate bound is full."),
                });
            }

            RunRecord result = request.CommandCase switch
            {
                SubmitRunCommandRequest.CommandOneofCase.Start => Start(commandId, request.Start),
                SubmitRunCommandRequest.CommandOneofCase.Pause =>
                    Pause(commandId, request.Pause),
                SubmitRunCommandRequest.CommandOneofCase.Resume =>
                    Resume(commandId, request.Resume),
                SubmitRunCommandRequest.CommandOneofCase.Cancel =>
                    Cancel(commandId, request.Cancel),
                _ => throw new InvalidOperationException("A supported command is required."),
            };
            DurableCommandRecord durable = runtime.Store.GetDurableCommand(commandId);
            SubmitRunCommandResponse response = new()
            {
                Disposition = replay
                    ? CommandDisposition.AlreadyAccepted
                    : CommandDisposition.Accepted,
                DurableCommandId = new DurableCommandId { Value = commandId },
                RunId = new RunId { Value = result.RunId },
            };
            if (durable.TransitionId is not null)
            {
                response.DurableTransitionId =
                    new DurableTransitionId { Value = durable.TransitionId };
            }

            return Task.FromResult(response);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or InvalidDataException
                or JsonException
                or KeyNotFoundException)
        {
            return Task.FromResult(new SubmitRunCommandResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(exception is InvalidDataException or JsonException
                    ? FailureCode.InvalidArgument : FailureCode.Conflict, Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetDurableCommandResponse> GetDurableCommand(
        GetDurableCommandRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            DurableCommandRecord command = runtime.Store.GetDurableCommand(
                Required(request.DurableCommandId?.Value, "durable command ID"));
            DurableCommandStatus status = new()
            {
                DurableCommandId = new DurableCommandId { Value = command.CommandId },
                Disposition = command.Disposition == "accepted"
                    ? CommandDisposition.Accepted
                    : CommandDisposition.Rejected,
                RunId = new RunId { Value = command.RunId },
                ResultingLifecycleState = ProtoMapping.ToProto(
                    Enum.Parse<DomainLifecycleState>(command.ResultingState)),
                ObservedAt = ProtoMapping.ToProto(command.CreatedAt),
                AcceptedInput = new DurableCommandInputIdentity
                {
                    CommandKind = command.CommandKind switch
                    {
                        "start" => DurableCommandKind.Start,
                        "pausing" => DurableCommandKind.Pause,
                        "queued" => DurableCommandKind.Resume,
                        "cancelling" => DurableCommandKind.Cancel,
                        _ => DurableCommandKind.Unknown,
                    },
                    ExpectedLifecycleGeneration =
                        checked((ulong)command.ExpectedGeneration),
                    InstallationSnapshotId = new InstallationSnapshotId
                    {
                        Value = command.RunBinding.InstallationSnapshotId,
                    },
                    AnalysisContextId = new AnalysisContextId
                    {
                        Value = command.RunBinding.AnalysisContextId,
                    },
                    EffectiveScanConfigurationId = new ScanConfigurationId
                    {
                        Value = command.RunBinding.EffectiveScanConfigurationId,
                    },
                    ResolvedInputManifestId = new ResolvedInputManifestId
                    {
                        Value = command.RunBinding.ResolvedInputManifestId,
                    },
                },
            };
            if (command.TransitionId is not null)
            {
                status.DurableTransitionId =
                    new DurableTransitionId { Value = command.TransitionId };
            }
            if (command.StartInitiationKind is not null
                && command.StartDispatchDeadline is not null)
            {
                status.AcceptedInput.ManualInitiationKind =
                    Enum.Parse<ManualInitiationKind>(command.StartInitiationKind);
                status.AcceptedInput.DispatchDeadline =
                    ProtoMapping.ToProto(command.StartDispatchDeadline.Value);
                RunOperationRecord? operation = runtime.Store.GetRunOperation(command.RunId);
                if (operation?.OperationKind == "managed-analysis-v1")
                {
                    status.AcceptedInput.RequestedRunId = new RunId { Value = command.RunId };
                    status.AcceptedInput.AnalysisOrchestrationRequest = new ContentDigest
                    {
                        Algorithm = DigestAlgorithm.Sha256,
                        Value = ByteString.CopyFrom(Convert.FromHexString(operation.RequestSha256)),
                        SizeBytes = checked((ulong)Encoding.UTF8.GetByteCount(operation.RequestJson)),
                    };
                }
            }

            return Task.FromResult(new GetDurableCommandResponse { Status = status });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetDurableCommandResponse
            {
                Failure = Failure(FailureCode.NotFound, "The durable command does not exist."),
            });
        }
    }

    public override Task<SubmitSnapshotCaptureResponse> SubmitSnapshotCapture(
        SubmitSnapshotCaptureRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            string commandId = Required(
                request.IdempotencyKey?.Value,
                "durable command ID");
            SnapshotCaptureOperationRecord? replay =
                runtime.Store.FindSnapshotCaptureByCommand(commandId);
            if (replay is null
                && !runtime.TryAdmitNewDurableCommand(DateTimeOffset.UtcNow))
            {
                return Task.FromResult(new SubmitSnapshotCaptureResponse
                {
                    Disposition = CommandDisposition.Rejected,
                    Failure = Failure(
                        FailureCode.LimitExceeded,
                        "The new durable-command rate bound is full."),
                });
            }

            ManagedMo2SnapshotCaptureAssignment selection =
                ValidateSnapshotSelection(request.Selection);
            string requestJson = JsonSerializer.Serialize(selection);
            string requestSha256 = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestJson)))
                .ToLowerInvariant();
            if (request.InitiationKind is not (
                    ManualInitiationKind.CliUserAction
                    or ManualInitiationKind.DesktopUserGesture
                    or ManualInitiationKind.EvaluationHarness))
            {
                throw new InvalidOperationException(
                    "A supported explicit initiation kind is required.");
            }

            string operationId = Guid.NewGuid().ToString("N");
            SnapshotCaptureOperationRecord operation =
                runtime.Store.CreateSnapshotCaptureOperation(
                    commandId,
                    operationId,
                    requestJson,
                    requestSha256,
                    request.InitiationKind.ToString(),
                    FromProto(request.DispatchDeadline),
                    runtime.Authority.FencingEpoch,
                    DateTimeOffset.UtcNow);
            snapshotExecutor.Schedule(operation.OperationId);
            return Task.FromResult(new SubmitSnapshotCaptureResponse
            {
                Disposition = replay is null
                    ? CommandDisposition.Accepted
                    : CommandDisposition.AlreadyAccepted,
                DurableCommandId = new DurableCommandId { Value = commandId },
                OperationId = new SnapshotCaptureOperationId
                {
                    Value = operation.OperationId,
                },
            });
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            return Task.FromResult(new SubmitSnapshotCaptureResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(FailureCode.Conflict, Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetSnapshotCaptureResponse> GetSnapshotCapture(
        GetSnapshotCaptureRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            SnapshotCaptureOperationRecord operation =
                request.IdentityCase switch
                {
                    GetSnapshotCaptureRequest.IdentityOneofCase.OperationId =>
                        runtime.Store.GetSnapshotCaptureOperation(
                            Required(
                                request.OperationId?.Value,
                                "snapshot capture operation ID")),
                    GetSnapshotCaptureRequest.IdentityOneofCase.DurableCommandId =>
                        runtime.Store.FindSnapshotCaptureByCommand(
                            Required(
                                request.DurableCommandId?.Value,
                                "durable command ID"))
                        ?? throw new KeyNotFoundException(),
                    _ => throw new ArgumentException(
                        "A snapshot capture operation or durable command identity is required."),
                };
            SnapshotCaptureStatus status = new()
            {
                OperationId = new SnapshotCaptureOperationId
                {
                    Value = operation.OperationId,
                },
                DurableCommandId = new DurableCommandId
                {
                    Value = operation.DurableCommandId,
                },
                LifecycleState = operation.State switch
                {
                    "Queued" => SnapshotCaptureLifecycleState.Queued,
                    "Running" => SnapshotCaptureLifecycleState.Running,
                    "Completed" => SnapshotCaptureLifecycleState.Completed,
                    "Failed" => SnapshotCaptureLifecycleState.Failed,
                    _ => SnapshotCaptureLifecycleState.Unknown,
                },
                LifecycleGeneration = checked((ulong)operation.Generation),
                RequestSha256 = operation.RequestSha256,
                CreatedAt = ProtoMapping.ToProto(operation.CreatedAt),
                UpdatedAt = ProtoMapping.ToProto(operation.UpdatedAt),
            };
            if (operation.InstallationSnapshotId is not null)
            {
                status.InstallationSnapshotId = new InstallationSnapshotId
                {
                    Value = operation.InstallationSnapshotId,
                };
            }
            if (operation.PayloadId is not null)
            {
                status.PayloadId = new PayloadId { Value = operation.PayloadId };
            }

            return Task.FromResult(new GetSnapshotCaptureResponse { Status = status });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetSnapshotCaptureResponse
            {
                Failure = Failure(
                    FailureCode.NotFound,
                    "The snapshot capture operation does not exist."),
            });
        }
        catch (ArgumentException exception)
        {
            return Task.FromResult(new GetSnapshotCaptureResponse
            {
                Failure = Failure(
                    FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override async Task SubscribeEvents(
        SubscribeEventsRequest request,
        IServerStreamWriter<ApplicationEvent> responseStream,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.RequestedQueueItems == 0
            || request.RequestedQueueItems > ProtocolConstants.MaximumStreamQueueItems
            || request.RunScope.Count != 1)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid bounded subscription."));
        }

        if (!runtime.TryAdmitEventSubscription())
        {
            throw new RpcException(
                new Status(StatusCode.ResourceExhausted, "The event subscription admission bound is full."));
        }

        try
        {
            string runId = Required(request.RunScope[0].Value, "run ID");
            RunRecord run = runtime.Store.GetRun(runId);
            if (!IsCurrentProjection(request.ExpectedProjectionVersion))
            {
                await WriteResyncAsync(
                    responseStream,
                    request,
                    run,
                    ResyncReason.ProjectionRebuilt).ConfigureAwait(false);
                return;
            }

            EventStreamCursor? cursor = null;
            if (request.After is not null && request.After.OpaqueValue.Length > 0)
            {
                try
                {
                    cursor = DecodeEventCursor(request.After.OpaqueValue);
                }
                catch (InvalidOperationException)
                {
                    await WriteResyncAsync(
                        responseStream,
                        request,
                        run,
                        IsCursorFromAnotherCoordinator(request.After.OpaqueValue)
                            ? ResyncReason.CoordinatorRestart
                            : ResyncReason.CursorInvalid).ConfigureAwait(false);
                    return;
                }

                ResyncReason? reason =
                    cursor.RunId != runId ? ResyncReason.CursorInvalid
                    : cursor.CoordinatorInstanceId != runtime.Authority.InstanceId
                        || cursor.CoordinatorFencingEpoch != runtime.Authority.FencingEpoch
                        ? ResyncReason.CoordinatorRestart
                    : cursor.ExpiresAt < DateTimeOffset.UtcNow
                        ? ResyncReason.ReplayWindowExpired
                    : cursor.ProjectionVersion != "1"
                        ? ResyncReason.ProjectionRebuilt
                    : cursor.DurableSequence > run.DurableSequence
                        ? ResyncReason.SequenceGap
                    : run.DurableSequence - cursor.DurableSequence > 1
                        ? ResyncReason.SequenceGap
                    : null;
                if (reason is not null)
                {
                    await WriteResyncAsync(responseStream, request, run, reason.Value)
                        .ConfigureAwait(false);
                    return;
                }
            }

            Channel<ApplicationEvent> queue = Channel.CreateBounded<ApplicationEvent>(
                new BoundedChannelOptions(checked((int)request.RequestedQueueItems))
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                });
            bool overflow = false;
            Task producer = Task.Run(async () =>
            {
                long lastSequence = cursor?.DurableSequence ?? 0;
                try
                {
                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        RunRecord current = runtime.Store.GetRun(runId);
                        if (current.DurableSequence > lastSequence)
                        {
                            ApplicationEvent item = ProgressEvent(request, current);
                            if (!queue.Writer.TryWrite(item))
                            {
                                overflow = true;
                                break;
                            }

                            lastSequence = current.DurableSequence;
                        }

                        await Task.Delay(50, context.CancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                }
                finally
                {
                    queue.Writer.TryComplete();
                }
            }, CancellationToken.None);
            try
            {
                await foreach (ApplicationEvent item in queue.Reader.ReadAllAsync(
                    context.CancellationToken).ConfigureAwait(false))
                {
                    await responseStream.WriteAsync(item).ConfigureAwait(false);
                }

                if (overflow && !context.CancellationToken.IsCancellationRequested)
                {
                    await WriteResyncAsync(
                        responseStream,
                        request,
                        runtime.Store.GetRun(runId),
                        ResyncReason.QueueOverflow).ConfigureAwait(false);
                }
            }
            finally
            {
                await producer.ConfigureAwait(false);
            }
        }
        finally
        {
            runtime.ReleaseEventSubscription();
        }
    }

    private RunRecord Start(string commandId, ManualStartCommand command)
    {
        if (command is null
            || command.InitiationKind is not (
                ManualInitiationKind.CliUserAction
                or ManualInitiationKind.DesktopUserGesture
                or ManualInitiationKind.EvaluationHarness))
        {
            throw new InvalidOperationException(
                "A supported initiation kind and future dispatch deadline are required.");
        }

        RunBinding binding = new(
            Required(command.InstallationSnapshotId?.Value, "installation snapshot ID"),
            Required(command.AnalysisContextId?.Value, "analysis context ID"),
            Required(command.EffectiveScanConfigurationId?.Value, "scan configuration ID"),
            Required(command.ResolvedInputManifestId?.Value, "resolved input manifest ID"));
        ManagedAnalysisOrchestrationRequest? managedRequest = null;
        string runId;
        if (command.AnalysisOrchestrationRequestJson.Length == 0)
        {
            runId = Guid.NewGuid().ToString("N");
        }
        else
        {
            if (command.AnalysisOrchestrationRequestJson.Length > ManagedAnalysisOrchestrationRequest.MaximumRequestBytes)
            {
                throw new InvalidOperationException("The managed analysis request exceeds its IPC admission bound.");
            }
            runId = Required(command.RequestedRunId?.Value, "requested managed analysis run ID");
            managedRequest = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
                command.AnalysisOrchestrationRequestJson.Span, ContractJsonSerializer.Options)
                ?? throw new InvalidOperationException("The managed analysis request is malformed.");
            ManagedAnalysisOrchestrator.Validate(managedRequest, runId, binding);
        }
        RunRecord run = managedRequest is null
            ? runtime.Store.CreateRun(
                commandId, runId, binding, runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow,
                command.InitiationKind.ToString(), FromProto(command.DispatchDeadline))
            : executor.CreateManagedAnalysisRun(
                commandId, runId, binding, managedRequest, command.InitiationKind.ToString(),
                FromProto(command.DispatchDeadline));
        executor.Schedule(run.RunId);
        return run;
    }

    private static ManagedMo2SnapshotCaptureAssignment ValidateSnapshotSelection(
        Mo2SnapshotCaptureSelection? selection)
    {
        if (selection is null
            || selection.QualifiedMappings.Count > 32
            || selection.EnabledMapperSha256.Count > 32)
        {
            throw new InvalidOperationException(
                "A bounded MO2 snapshot selection is required.");
        }

        string profileName = Required(selection.SelectedProfileName, "selected profile name");
        if (profileName is "." or ".."
            || profileName.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':']) >= 0)
        {
            throw new InvalidOperationException(
                "The selected profile name is not a single safe component.");
        }

        ManagedQualifiedMappingAssignment[] mappings =
            selection.QualifiedMappings.Select(mapping =>
                new ManagedQualifiedMappingAssignment(
                    Required(mapping.MappingId, "mapping ID"),
                    RequiredAbsolutePath(mapping.SourceRoot, "mapping source root"),
                    Required(mapping.VirtualPrefix, "mapping virtual prefix"),
                    RequiredSha256(mapping.MapperSha256, "mapper SHA-256")))
            .ToArray();
        string[] enabledMappers = selection.EnabledMapperSha256
            .Select(value => RequiredSha256(value, "enabled mapper SHA-256"))
            .ToArray();
        if (mappings.Select(mapping => mapping.MappingId)
                .Distinct(StringComparer.Ordinal).Count() != mappings.Length
            || enabledMappers.Distinct(StringComparer.Ordinal).Count()
                != enabledMappers.Length)
        {
            throw new InvalidOperationException(
                "Snapshot mapping identities must be unique.");
        }

        return new ManagedMo2SnapshotCaptureAssignment(
            RequiredAbsolutePath(selection.Mo2ExecutablePath, "MO2 executable path"),
            RequiredAbsolutePath(selection.InstanceRoot, "MO2 instance root"),
            RequiredAbsolutePath(selection.InstanceIniPath, "MO2 instance INI path"),
            RequiredAbsolutePath(selection.ProfilesRoot, "MO2 profiles root"),
            RequiredAbsolutePath(selection.ModsRoot, "MO2 mods root"),
            RequiredAbsolutePath(selection.OverwriteRoot, "MO2 overwrite root"),
            RequiredAbsolutePath(selection.GameDataRoot, "game Data root"),
            RequiredAbsolutePath(selection.SkyrimExecutablePath, "Skyrim executable path"),
            profileName,
            Required(selection.Platform, "runtime platform"),
            Required(selection.DistributionChannel, "runtime distribution channel"),
            Required(selection.ApplicationId, "runtime application ID"),
            mappings,
            enabledMappers);
    }

    private static string RequiredAbsolutePath(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 32_767
            || !Path.IsPathFullyQualified(value)
            || value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException($"A bounded absolute {field} is required.");
        }

        return Path.GetFullPath(value);
    }

    private static string RequiredSha256(string? value, string field)
    {
        if (value is null
            || value.Length != 64
            || value.Any(ch => ch is not (
                >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F')))
        {
            throw new ArgumentException($"A canonical {field} is required.");
        }

        return value.ToLowerInvariant();
    }

    private bool DurableCommandExists(string commandId)
    {
        try
        {
            _ = runtime.Store.GetDurableCommand(commandId);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private RunRecord Pause(string commandId, PauseCommand command)
    {
        string runId = Required(command.RunId?.Value, "run ID");
        RunRecord pausing = runtime.Store.Transition(
            commandId,
            runId,
            checked((long)command.ExpectedLifecycleGeneration),
            DomainLifecycleState.Pausing,
            runtime.Authority.FencingEpoch,
            "pause requested",
            DateTimeOffset.UtcNow,
            Infinium.Domain.Contracts.LifecycleTransitionRecordKind.Requested);
        return ObserveSafeBoundaryWhenIdle(
            commandId,
            pausing,
            DomainLifecycleState.Paused,
            "paused-at-safe-boundary",
            "pause observed with no dispatched work");
    }

    private RunRecord Resume(string commandId, ResumeCommand command)
    {
        string runId = Required(command.RunId?.Value, "run ID");
        RunRecord run = runtime.Store.Transition(
            commandId,
            runId,
            checked((long)command.ExpectedLifecycleGeneration),
            DomainLifecycleState.Queued,
            runtime.Authority.FencingEpoch,
            "resume requested",
            DateTimeOffset.UtcNow);
        executor.Schedule(runId);
        return run;
    }

    private RunRecord Cancel(string commandId, CancelCommand command)
    {
        string runId = Required(command.RunId?.Value, "run ID");
        RunRecord cancelling = runtime.Store.Transition(
            commandId,
            runId,
            checked((long)command.ExpectedLifecycleGeneration),
            DomainLifecycleState.Cancelling,
            runtime.Authority.FencingEpoch,
            "cancellation requested",
            DateTimeOffset.UtcNow,
            Infinium.Domain.Contracts.LifecycleTransitionRecordKind.Requested);
        return ObserveSafeBoundaryWhenIdle(
            commandId,
            cancelling,
            DomainLifecycleState.Cancelled,
            "cancelled-at-safe-boundary",
            "cancellation observed with no dispatched work");
    }

    private RunRecord ObserveSafeBoundaryWhenIdle(
        string commandId,
        RunRecord requested,
        DomainLifecycleState observedState,
        string attemptOutcome,
        string reason)
    {
        DomainLifecycleState requestedState = observedState == DomainLifecycleState.Paused
            ? DomainLifecycleState.Pausing
            : DomainLifecycleState.Cancelling;
        if (requested.State != requestedState
            || runtime.Store.HasLiveAttempts(requested.RunId))
        {
            return requested;
        }

        runtime.Store.SettleLiveAttempts(
            requested.RunId,
            attemptOutcome,
            runtime.Authority.FencingEpoch);
        return runtime.Store.Transition(
            commandId + "-observed",
            requested.RunId,
            requested.Generation,
            observedState,
            runtime.Authority.FencingEpoch,
            reason,
            DateTimeOffset.UtcNow);
    }

    private HandshakeResponse BuildHandshake(
        ApplicationHandshakeRequest request,
        ServerCallContext context)
    {
        HandshakeResponse response = new()
        {
            NegotiatedProtocol = ProtocolConstants.Version,
            Compatibility = ProtocolConstants.Compatibility,
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = runtime.Authority.InstanceId },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            BoundEndpointRole = EndpointRole.ApplicationClient,
            Limits = ProtocolConstants.Limits,
        };
        if (!IsExpectedEndpoint(context))
        {
            response.Disposition = HandshakeDisposition.WrongEndpoint;
            response.Failure = Failure(
                FailureCode.Unauthorized,
                "The service is bound to another pipe role.");
            return response;
        }

        if (request.SupportedProtocol?.Major != ProtocolConstants.Major)
        {
            response.Disposition = HandshakeDisposition.IncompatibleMajor;
            response.Failure = Failure(FailureCode.IncompatibleVersion, "Protocol major is incompatible.");
            return response;
        }

        if (request.ClientKind is not (
            ApplicationClientKind.Cli
            or ApplicationClientKind.DesktopHost
            or ApplicationClientKind.TestHarness))
        {
            response.Disposition = HandshakeDisposition.UnsupportedCapability;
            response.Failure = Failure(
                FailureCode.Unsupported,
                "The application client kind is unsupported.");
            return response;
        }

        if (request.SupportedProtocol.MinimumMinor > ProtocolConstants.Minor
            || request.SupportedProtocol.MaximumMinor < ProtocolConstants.Minor)
        {
            response.Disposition = HandshakeDisposition.IncompatibleMinor;
            response.Failure = Failure(FailureCode.IncompatibleVersion, "Protocol minor is incompatible.");
            return response;
        }

        if (request.Compatibility?.ApplicationContract?.Value != ProtocolConstants.ContractVersion
            || request.Compatibility.DomainContract?.Value != ProtocolConstants.ContractVersion
            || request.Compatibility.StorageContract?.Value != ProtocolConstants.ContractVersion)
        {
            response.Disposition = HandshakeDisposition.IncompatibleApplicationContract;
            response.Failure = Failure(FailureCode.IncompatibleVersion, "Contract compatibility is invalid.");
            return response;
        }

        if (!request.CoordinatorInstanceNonce.Span.SequenceEqual(runtime.Descriptor.GetNonce()))
        {
            response.Disposition = HandshakeDisposition.InvalidNonce;
            response.Failure = Failure(FailureCode.Unauthenticated, "The coordinator nonce is invalid.");
            return response;
        }

        if (request.RequestedCapabilities.Count > ProtocolConstants.MaximumCapabilityFlags)
        {
            response.Disposition = HandshakeDisposition.LimitsRejected;
            response.Failure = Failure(FailureCode.LimitExceeded, "Too many capability flags were requested.");
            return response;
        }

        Capability[] granted =
        [
            Capability.ApplicationQuery,
            Capability.DurableCommand,
            Capability.EventStream,
            Capability.KeysetCursor,
        ];
        if (request.RequestedCapabilities.Any(capability => !granted.Contains(capability)))
        {
            response.Disposition = HandshakeDisposition.UnsupportedCapability;
            response.Failure = Failure(FailureCode.Unsupported, "A requested capability is unsupported.");
            return response;
        }

        response.Disposition = HandshakeDisposition.Accepted;
        response.GrantedCapabilities.Add(granted);
        return response;
    }

    private void RequireNegotiated(ServerCallContext context)
    {
        if (!IsExpectedEndpoint(context)
            || !runtime.IsApplicationConnectionAdmitted(context.GetHttpContext().Connection.Id))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Negotiate first."));
        }
    }

    private bool IsExpectedEndpoint(ServerCallContext? request)
    {
        if (request is null)
        {
            return true;
        }

        InfiniumPipeRoleFeature? feature =
            request.GetHttpContext().Features.Get<InfiniumPipeRoleFeature>();
        return feature is not null
            && string.Equals(feature.Role, "application", StringComparison.Ordinal)
            && string.Equals(
                feature.PipeName,
                runtime.Descriptor.ApplicationPipe,
                StringComparison.Ordinal);
    }

    private static Failure Failure(FailureCode code, string detail) =>
        new() { Code = code, Detail = Bounded(detail) };

    private static ListRunsResponse CursorRejected(
        CursorDisposition disposition,
        string detail) =>
        new()
        {
            CursorRejection = new CursorRejection
            {
                Disposition = disposition,
                CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
                Failure = Failure(FailureCode.ResyncRequired, detail),
            },
        };

    private ByteString EncodeRunCursor(RunPageCursor cursor)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(cursor);
        byte[] mac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        byte[] value = new byte[mac.Length + payload.Length];
        mac.CopyTo(value, 0);
        payload.CopyTo(value, mac.Length);
        return ByteString.CopyFrom(value);
    }

    private ApplicationEvent ProgressEvent(
        SubscribeEventsRequest request,
        RunRecord run)
    {
        EventStreamCursor cursor = new(
            runtime.Authority.InstanceId,
            runtime.Authority.FencingEpoch,
            run.RunId,
            run.DurableSequence,
            "1",
            DateTimeOffset.UtcNow.AddMinutes(5));
        return new ApplicationEvent
        {
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = runtime.Authority.InstanceId },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            SubscriptionId = request.SubscriptionId,
            DurableEventSequence = checked((ulong)run.DurableSequence),
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            Kind = EventKind.Progress,
            RunScope = new RunId { Value = run.RunId },
            ResumeCursor = new EventCursor { OpaqueValue = EncodeEventCursor(cursor) },
            Progress = new ProgressSnapshot
            {
                RunId = new RunId { Value = run.RunId },
                LifecycleState = ProtoMapping.ToProto(run.State),
                Progress = ProtoMapping.EmptyProgress(run.State),
                ProjectionVersion = new ProjectionVersion { Value = "1" },
                DurableEventSequence = checked((ulong)run.DurableSequence),
                ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
            },
        };
    }

    private async Task WriteResyncAsync(
        IServerStreamWriter<ApplicationEvent> responseStream,
        SubscribeEventsRequest request,
        RunRecord run,
        ResyncReason reason)
    {
        await responseStream.WriteAsync(new ApplicationEvent
        {
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = runtime.Authority.InstanceId },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            SubscriptionId = request.SubscriptionId,
            DurableEventSequence = checked((ulong)run.DurableSequence),
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            Kind = EventKind.ResyncRequired,
            RunScope = new RunId { Value = run.RunId },
            ResyncRequired = new ResyncRequired
            {
                Reason = reason,
                CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
            },
        }).ConfigureAwait(false);
    }

    private ByteString EncodeEventCursor(EventStreamCursor cursor) =>
        EncodeAuthenticated(JsonSerializer.SerializeToUtf8Bytes(cursor));

    private EventStreamCursor DecodeEventCursor(ByteString opaque)
    {
        byte[] payload = DecodeAuthenticated(opaque);
        EventStreamCursor cursor = JsonSerializer.Deserialize<EventStreamCursor>(payload)
            ?? throw new InvalidOperationException("The event cursor is malformed.");
        return cursor;
    }

    private ByteString EncodeAuthenticated(byte[] payload)
    {
        byte[] mac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        byte[] value = new byte[mac.Length + payload.Length];
        mac.CopyTo(value, 0);
        payload.CopyTo(value, mac.Length);
        return ByteString.CopyFrom(value);
    }

    private byte[] DecodeAuthenticated(ByteString opaque)
    {
        if (opaque.Length is <= 32 or > 1024)
        {
            throw new InvalidOperationException("The authenticated cursor has an invalid size.");
        }

        ReadOnlySpan<byte> bytes = opaque.Span;
        ReadOnlySpan<byte> suppliedMac = bytes[..32];
        byte[] payload = bytes[32..].ToArray();
        byte[] expectedMac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        if (!CryptographicOperations.FixedTimeEquals(suppliedMac, expectedMac))
        {
            throw new InvalidOperationException("The cursor authentication failed.");
        }

        return payload;
    }

    private bool IsCursorFromAnotherCoordinator(ByteString opaque)
    {
        if (opaque.Length is <= 32 or > 1024)
        {
            return false;
        }

        try
        {
            EventStreamCursor? untrusted =
                JsonSerializer.Deserialize<EventStreamCursor>(opaque.Span[32..]);
            return untrusted is not null
                && !string.IsNullOrWhiteSpace(untrusted.CoordinatorInstanceId)
                && !string.Equals(
                    untrusted.CoordinatorInstanceId,
                    runtime.Authority.InstanceId,
                    StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsCurrentProjection(ProjectionVersion? expected) =>
        expected is null || expected.Value is "" or "1";

    private static ProjectionInvalidated ProjectionInvalidated() => new()
    {
        CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
        Reason = ResyncReason.ProjectionRebuilt,
    };

    private RunPageCursor DecodeRunCursor(ByteString opaque)
    {
        if (opaque.Length is <= 32 or > 512)
        {
            throw new InvalidOperationException("The run cursor has an invalid size.");
        }

        ReadOnlySpan<byte> bytes = opaque.Span;
        ReadOnlySpan<byte> suppliedMac = bytes[..32];
        ReadOnlySpan<byte> payload = bytes[32..];
        byte[] expectedMac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        if (!CryptographicOperations.FixedTimeEquals(suppliedMac, expectedMac))
        {
            throw new InvalidOperationException("The run cursor authentication failed.");
        }

        RunPageCursor cursor = JsonSerializer.Deserialize<RunPageCursor>(payload)
            ?? throw new InvalidOperationException("The run cursor payload is malformed.");
        if (string.IsNullOrWhiteSpace(cursor.RunId)
            || cursor.RunId.Length > 128
            || cursor.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("The run cursor is expired or malformed.");
        }

        return cursor;
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128
            ? throw new ArgumentException($"A bounded {field} is required.")
            : value;

    private static string Bounded(string value) =>
        value.Length <= 512 ? value : value[..512];

    private static DateTimeOffset FromProto(Instant? value)
    {
        if (value is null
            || value.Nanoseconds is < 0 or > 999_999_999)
        {
            throw new InvalidOperationException("The dispatch deadline is malformed.");
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value.UnixSeconds)
                .AddTicks(value.Nanoseconds / 100);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException(
                "The dispatch deadline is outside the supported range.",
                exception);
        }
    }

    private static ReplaySummary ToProto(AnalysisSummaryPersistenceRecord value) => new()
    {
        ReplayManifestId = new ReplayManifestId { Value = value.ReplayManifestId },
        ReplayState = ReplayState(value.ReplayState),
        AuditabilityState = AuditabilityState(value.AuditabilityState),
        SemanticallyEquivalent = value.SemanticallyEquivalent,
        DependencyCount = checked((ulong)value.DependencyCount),
        MissingDependencyCount = checked((ulong)value.MissingDependencyCount),
        CoverageGapCount = checked((ulong)value.CoverageGapCount),
    };

    private static ReplaySummary ToProto(AnalysisReplayPersistenceRecord value) => new()
    {
        ReplayManifestId = new ReplayManifestId { Value = value.ReplayManifestId },
        ReplayState = ReplayState(value.ReplayState),
        AuditabilityState = AuditabilityState(value.AuditabilityState),
        SemanticallyEquivalent = value.SemanticallyEquivalent,
        DependencyCount = checked((ulong)value.DependencyCount),
        MissingDependencyCount = checked((ulong)value.MissingDependencyCount),
        CoverageGapCount = checked((ulong)value.CoverageGapCount),
    };

    private static AnalysisArtifactReference ToProto(AnalysisArtifactPersistenceRecord value) => new()
    {
        ArtifactId = new AnalysisArtifactId { Value = value.ArtifactId },
        Kind = value.Kind switch
        {
            "documentation-evidence" => AnalysisArtifactKind.DocumentationEvidence,
            "candidate-analysis" => AnalysisArtifactKind.CandidateAnalysis,
            "finding-case" => AnalysisArtifactKind.FindingCase,
            "analysis-replay" => AnalysisArtifactKind.AnalysisReplay,
            "analysis-execution-input" => AnalysisArtifactKind.AnalysisExecutionInput,
            _ => AnalysisArtifactKind.Unknown,
        },
        SchemaId = value.SchemaId,
        SchemaVersion = new SemanticVersion { Value = value.SchemaVersion },
        Revision = checked((ulong)value.Revision),
        State = ProtoState(value.State),
        ContentDigest = new ContentDigest
        {
            Algorithm = DigestAlgorithm.Sha256,
            Value = ByteString.CopyFrom(Convert.FromHexString(value.ContentSha256)),
            SizeBytes = checked((ulong)value.ByteLength),
        },
        ByteLength = checked((ulong)value.ByteLength),
        ProvenanceId = new AnalysisArtifactId { Value = value.ProvenanceId },
        DependencyClosureId = new AnalysisArtifactId { Value = value.DependencyClosureId },
    };

    private static string KindToken(AnalysisArtifactKind value) => value switch
    {
        AnalysisArtifactKind.DocumentationEvidence => "documentation-evidence",
        AnalysisArtifactKind.CandidateAnalysis => "candidate-analysis",
        AnalysisArtifactKind.FindingCase => "finding-case",
        AnalysisArtifactKind.AnalysisReplay => "analysis-replay",
        AnalysisArtifactKind.AnalysisExecutionInput => "analysis-execution-input",
        _ => throw new ArgumentException("Analysis artifact kind is not queryable."),
    };

    private static string StateToken(AnalysisArtifactState value) =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());

    private static AnalysisArtifactState ProtoState(string value) => value switch
    {
        "present" => AnalysisArtifactState.Present,
        "resolved-negative" => AnalysisArtifactState.ResolvedNegative,
        "missing" => AnalysisArtifactState.Missing,
        "invalid-input" => AnalysisArtifactState.InvalidInput,
        "unsupported" => AnalysisArtifactState.Unsupported,
        "ambiguous" => AnalysisArtifactState.Ambiguous,
        "partial" => AnalysisArtifactState.Partial,
        "abstained" => AnalysisArtifactState.Abstained,
        "not-applicable" => AnalysisArtifactState.NotApplicable,
        "not-used" => AnalysisArtifactState.NotUsed,
        "failed" => AnalysisArtifactState.Failed,
        "cancelled" => AnalysisArtifactState.Cancelled,
        "limit-reached" => AnalysisArtifactState.LimitReached,
        "unavailable" => AnalysisArtifactState.Unavailable,
        _ => AnalysisArtifactState.Unknown,
    };

    private static Infinium.Contracts.Protobuf.Domain.V1.ReplayState ReplayState(string value) => value switch
    {
        "complete-clean" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.CompleteClean,
        "partial" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.Partial,
        "audit-only" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.AuditOnly,
        "unavailable" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.Unavailable,
        "failed-identity-drift" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.FailedIdentityDrift,
        _ => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.Unknown,
    };

    private static Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState AuditabilityState(string value) => value switch
    {
        "complete" => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.Complete,
        "partial" => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.CompleteWithGaps,
        "unavailable" => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.Unavailable,
        _ => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.Unknown,
    };

    private static AnalysisArtifactSortOrder ArtifactSort(AnalysisArtifactSort value) => value switch
    {
        AnalysisArtifactSort.Unspecified or AnalysisArtifactSort.IdentityAscending =>
            AnalysisArtifactSortOrder.IdentityAscending,
        AnalysisArtifactSort.RankDescendingIdentityAscending =>
            AnalysisArtifactSortOrder.RankDescendingIdentityAscending,
        AnalysisArtifactSort.UpdatedTickDescendingIdentityDescending =>
            AnalysisArtifactSortOrder.UpdatedTickDescendingIdentityDescending,
        _ => throw new ArgumentException("The analysis artifact sort is not supported."),
    };

    private static string QueryHash(
        IReadOnlyList<string> kinds,
        IReadOnlyList<string> states) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join('\n', string.Join(',', kinds), string.Join(',', states)))));

    private sealed record RunPageCursor(
        DateTimeOffset CreatedAt,
        string RunId,
        DateTimeOffset ExpiresAt);

    private sealed record EventStreamCursor(
        string CoordinatorInstanceId,
        long CoordinatorFencingEpoch,
        string RunId,
        long DurableSequence,
        string ProjectionVersion,
        DateTimeOffset ExpiresAt);

    private sealed class AnalysisCursorException(CursorDisposition disposition, string message)
        : InvalidOperationException(message)
    {
        public CursorDisposition Disposition { get; } = disposition;
    }
}
