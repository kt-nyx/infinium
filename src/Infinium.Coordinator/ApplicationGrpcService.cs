using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
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

public sealed partial class ApplicationGrpcService(
    CoordinatorRuntime runtime,
    ManagedRunExecutor executor,
    SnapshotCaptureExecutor snapshotExecutor,
    TargetedVerificationExecutor targetedVerificationExecutor)
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

    public override Task<GetApplicationBootstrapResponse> GetApplicationBootstrap(
        GetApplicationBootstrapRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.RendererContractVersion?.Value != ProtocolConstants.RendererContractVersion)
        {
            return Task.FromResult(BootstrapError(
                ApplicationErrorCode.IncompatibleVersion,
                "The renderer contract version is incompatible."));
        }

        if (request.MaximumRecentRuns == 0
            || request.MaximumRecentRuns > ProtocolConstants.MaximumBootstrapRecentRuns)
        {
            return Task.FromResult(BootstrapError(
                ApplicationErrorCode.LimitExceeded,
                "The bootstrap recent-run count exceeds its finite bound."));
        }

        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            GetApplicationBootstrapResponse response = BootstrapError(
                ApplicationErrorCode.ResyncRequired,
                "The bootstrap projection is no longer current.");
            response.Error.CurrentProjectionVersion = new ProjectionVersion { Value = "1" };
            return Task.FromResult(response);
        }

        try
        {
            ApplicationContractValidator.Validate(request);
        }
        catch (InvalidDataException)
        {
            return Task.FromResult(BootstrapError(
                ApplicationErrorCode.InvalidArgument,
                "The bootstrap request contains unknown or invalid contract data."));
        }

        IReadOnlyList<RunRecord> runs = runtime.Store.ListRecentRuns(
            checked((int)request.MaximumRecentRuns));
        ApplicationBootstrap bootstrap = new()
        {
            Compatibility = ProtocolConstants.Compatibility,
            Limits = ProtocolConstants.Limits,
            RendererContractVersion = new SemanticVersion
            {
                Value = ProtocolConstants.RendererContractVersion,
            },
            CoordinatorHealth = HealthState.Healthy,
            Configuration = new ConfigurationAvailability
            {
                Availability = Availability.Available,
                InertReason = "Versioned setup configuration and prepared local run input are available.",
            },
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            CoordinatorInstanceId = new CoordinatorInstanceId
            {
                Value = runtime.Authority.InstanceId,
            },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
        };
        bootstrap.RecentRuns.Add(runs.Select(ProtoMapping.ToSummary));
        bootstrap.Capabilities.Add(
        [
            BuildApplicationCapability(ApplicationCapability.Bootstrap, Availability.Available, "Bootstrap projection is active."),
            BuildApplicationCapability(ApplicationCapability.RunQuery, Availability.Available, "Typed prepared-run initiation and bounded run queries are active."),
            BuildApplicationCapability(ApplicationCapability.EventResync, Availability.Available, "Bounded event resync is active."),
            BuildApplicationCapability(ApplicationCapability.Configuration, Availability.Available, "Versioned setup configuration and prepared-run review are active."),
            BuildApplicationCapability(ApplicationCapability.ProviderEnrollment, Availability.Partial, "Non-secret enrollment intent and status are active; native secret entry remains unavailable in this phase."),
            BuildApplicationCapability(ApplicationCapability.ResultExploration, Availability.Partial, "FindingReport query/readback and adversarial request validation have focused correction evidence; Checkpoint C and desktop consumption remain unaccepted."),
            BuildApplicationCapability(ApplicationCapability.DurableUserReview, Availability.Partial, "Review, export deletion/recovery, and the native targeted-verification workflow are implemented; corrected Checkpoint C review and desktop delivery remain pending."),
        ]);
        return Task.FromResult(new GetApplicationBootstrapResponse { Bootstrap = bootstrap });
    }

    private static ApplicationCapabilityState BuildApplicationCapability(
        ApplicationCapability capability,
        Availability availability,
        string reason) => new()
        {
            Capability = capability,
            Availability = availability,
            InertReason = reason,
        };

    private static GetApplicationBootstrapResponse BootstrapError(
        ApplicationErrorCode code,
        string detail) => new()
        {
            Error = new ApplicationContractError
            {
                Code = code,
                InertDetail = detail,
                RetryMayBeSafe = false,
            },
        };

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
                    "The run query supports deterministic created-at ascending order only."),
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
                    "The current query service has no finding producer or supported finding query shape."),
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

}
