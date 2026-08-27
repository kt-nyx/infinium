using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Tests;

[TestClass]
public sealed class SolutionIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestProperty("Category", "Integration")]
    public void SolutionContainsEveryDeclaredProject()
    {
        string solution = TestRepository.Read("Infinium.sln");
        string[] projectFiles = TestRepository
            .EnumerateProjectFiles()
            .Select(path => Path.GetRelativePath(TestRepository.Root, path).Replace('/', '\\'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(17, projectFiles);
        foreach (string projectFile in projectFiles)
        {
            StringAssert.Contains(solution, $"\"{projectFile}\"");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestProperty("Category", "Integration")]
    public void EveryProjectHasARestoreLock()
    {
        string[] projectDirectories = TestRepository
            .EnumerateProjectFiles()
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .ToArray();

        foreach (string projectDirectory in projectDirectories)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(projectDirectory, "packages.lock.json")),
                $"Project '{projectDirectory}' does not have a restore lock.");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Evaluation")]
    public void RunOutputClassifiesEveryLifecycleUnitWithoutUnsupportedAuditClaims()
    {
        foreach (Infinium.Domain.Contracts.LifecycleState state
                 in Enum.GetValues<Infinium.Domain.Contracts.LifecycleState>())
        {
            RunRecord run = new(
                "run-output",
                new RunBinding("snapshot", "context", "configuration", "manifest"),
                state,
                1,
                1,
                1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            RunDetail detail = ProtoMapping.ToDetail(run);
            Assert.AreEqual(ReplayabilityState.Unavailable, detail.ReplayabilityState);
            Assert.AreEqual(AuditabilityState.CompleteWithGaps, detail.AuditabilityState);
            ProgressSummary progress = detail.Summary.Progress;
            ulong classified = progress.CompletedUnits
                + progress.ReusedUnits
                + progress.QueuedUnits
                + progress.RunningUnits
                + progress.FailedUnits
                + progress.SkippedUnits
                + progress.UnsupportedUnits
                + progress.LimitedUnits
                + progress.InvalidatedUnits
                + progress.GapUnits;
            Assert.AreEqual(1UL, classified, state.ToString());
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void RuntimeEntryPointsFailClosedOutsideTheirTypedAuthority()
    {
        ProcessResult cli = Run("Infinium.Cli", []);
        Assert.AreEqual(2, cli.ExitCode);
        StringAssert.Contains(cli.Error, "Usage:");
        string parserRoot = Path.Combine(Path.GetTempPath(), $"infinium-cli-parser-{Guid.NewGuid():N}");
        ProcessResult unknownOption = Run(
            "Infinium.Cli",
            ["--root", parserRoot, "status", "run-a", "--unknown"]);
        Assert.AreEqual(1, unknownOption.ExitCode);
        StringAssert.Contains(unknownOption.Error, "Unknown option");
        ProcessResult duplicateOption = Run(
            "Infinium.Cli",
            ["--root", parserRoot, "status", "run-a", "--root", parserRoot]);
        Assert.AreEqual(1, duplicateOption.ExitCode);
        StringAssert.Contains(duplicateOption.Error, "only once");

        ProcessResult coordinator = Run("Infinium.Coordinator", []);
        Assert.AreEqual(2, coordinator.ExitCode);
        StringAssert.Contains(coordinator.Error, "--root");

        ProcessResult worker = Run("Infinium.Worker", []);
        Assert.AreEqual(2, worker.ExitCode);
        StringAssert.Contains(worker.Error, "coordinator-launched only");

        ProcessResult helper = Run("Infinium.CredentialHelper", []);
        Assert.AreEqual(64, helper.ExitCode);
        StringAssert.Contains(helper.Error, "two private pipes, one secure-store capability, and authoritative time");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    [TestProperty("Category", "Evaluation")]
    public async Task TypedSetupAndPreparedManualRunSurviveReconnectAndRestartOffline()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The named-pipe application workflow requires Windows.");
        }

        string root = Path.Combine(Path.GetTempPath(), $"infinium-setup-workflow-{Guid.NewGuid():N}");
        const string credentialCanary = "credential-canary-offline-7c51d2";
        int coordinatorProcessId = 0;
        try
        {
            using (AuthoritativeStore initialize = new(new StoragePaths(root)))
            {
                Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, initialize.GetSchemaVersion());
            }
            string mo2Root = Path.Combine(root, "fixture-mo2");
            Directory.CreateDirectory(Path.Combine(mo2Root, "profiles", "Profile A"));
            File.Copy(
                typeof(SolutionIntegrationTests).Assembly.Location,
                Path.Combine(mo2Root, "ModOrganizer.exe"));
            File.WriteAllText(
                Path.Combine(mo2Root, "ModOrganizer.ini"),
                "[General]\nselected_profile=@ByteArray(Profile A)\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            PreparedAnalysisFixtureIdentity retainedFixture = await PreparedAnalysisTestFixture.SeedAsync(
                root,
                mo2Root,
                "Profile A");
            string installationIdentity = "mo2-installation-" + Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(Path.GetFullPath(mo2Root).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar).ToUpperInvariant())))[..32];
            string profileId = "profile-" + Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(installationIdentity + "\nPROFILE A")))[..24];
            Assert.AreNotEqual(
                profileId,
                ApplicationGrpcService.ComputeProfileCandidateId(
                    Path.Combine(root, "different-mo2-installation"),
                    "Profile A"));
            using (AuthoritativeStore legacyConfiguration = new(new StoragePaths(root)))
            {
                _ = legacyConfiguration.ApplySetupMutation(new(
                    "seed-legacy-configuration",
                    "create-configuration",
                    "saved-scan-configuration",
                    "configuration-legacy",
                    0,
                    "active",
                    "{\"Name\":\"Legacy unsupported\",\"Values\":{\"AnalyzerIds\":[\"unmapped-legacy-analyzer\"],\"LocalOnly\":true,\"MaximumConcurrency\":1,\"MaximumProviderDispatches\":0,\"MaximumCalculatedNanoUsd\":0,\"MaximumElapsedMilliseconds\":60000}}",
                    DateTimeOffset.UtcNow));
            }
            DowngradePreparedAnalysisAdmissionForMigrationEvidence(root);

            ProcessResult bootstrap = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "bootstrap-snapshot",
                    "--context", "bootstrap-context",
                    "--configuration", "bootstrap-configuration",
                    "--manifest", "bootstrap-manifest",
                    "--json",
                ],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, bootstrap.ExitCode, bootstrap.Error);
            RuntimeDescriptor descriptor = RuntimeDescriptor.Read(root);
            coordinatorProcessId = descriptor.ProcessId;

            string preparationId;
            string preparedRunId;
            using (GrpcChannel channel = NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe))
            {
                ApplicationService.ApplicationServiceClient application = new(channel);
                HandshakeResponse accepted = await application.NegotiateAsync(
                    ApplicationHandshake(descriptor)).ResponseAsync;
                Assert.AreEqual(HandshakeDisposition.Accepted, accepted.Disposition);

                GetSetupStateResponse initial = await application.GetSetupStateAsync(
                    new GetSetupStateRequest
                    {
                        MaximumSavedConfigurations = 10,
                        ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                    }).ResponseAsync;
                Assert.IsNotNull(initial.Setup, initial.Error?.InertDetail);
                Assert.AreEqual(
                    ToolValidationState.NotYetValidated,
                    initial.Setup.Tools.Single(item => item.Tool == ExternalToolKind.ModOrganizer2).State);
                Assert.IsFalse(initial.Setup.ProfileSelection.ExplicitlyConfirmed);
                Assert.AreEqual(string.Empty, initial.Setup.ProfileSelection.SuggestedCandidateId);
                Assert.AreEqual(string.Empty, initial.Setup.ProfileSelection.ConfirmedCandidateId);
                SavedScanConfiguration migratedConfiguration = initial.Setup.SavedConfigurations.Single(
                    item => item.ConfigurationId.Value == "configuration-legacy");
                Assert.AreEqual("r2", migratedConfiguration.Revision.OpaqueValue);
                Assert.AreEqual(
                    AnalysisCapabilityKind.Unsupported,
                    migratedConfiguration.Values.AnalysisCapabilities.Single());
                SubmitSetupCommandResponse validatedMo2 = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-validate-mo2",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r0" },
                        ValidateTool = new ValidateToolConfiguration
                        {
                            Tool = ExternalToolKind.ModOrganizer2,
                            ModOrganizerInstallationRoot = mo2Root,
                        },
                    }).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Accepted, validatedMo2.Receipt.Disposition);
                ToolConfiguration validatedMo2State = validatedMo2.Setup.Tools.Single(
                    item => item.Tool == ExternalToolKind.ModOrganizer2);
                Assert.AreEqual(ToolValidationState.Available, validatedMo2State.State);
                StringAssert.StartsWith(validatedMo2State.ObservedVersion, "2.5.2");
                Assert.AreEqual(profileId, validatedMo2.Setup.ProfileSelection.SuggestedCandidateId);
                Assert.IsFalse(validatedMo2.Setup.ProfileSelection.ExplicitlyConfirmed);
                SubmitSetupCommandResponse confirmed = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-confirm-profile",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r0" },
                        ConfirmProfile = new ConfirmProfileSelection { CandidateId = profileId },
                    }).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Accepted, confirmed.Receipt.Disposition);
                Assert.IsTrue(confirmed.Setup.ProfileSelection.ExplicitlyConfirmed);
                Assert.AreEqual(profileId, confirmed.Setup.ProfileSelection.ConfirmedCandidateId);
                Assert.AreEqual(
                    installationIdentity,
                    confirmed.Setup.ProfileSelection.ConfirmedInstallationIdentity);
                GetSetupStateRequest unknownFieldSetup = GetSetupStateRequest.Parser.ParseFrom(
                    new GetSetupStateRequest
                    {
                        MaximumSavedConfigurations = 1,
                    }.ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray());
                GetSetupStateResponse unknownFieldRejected = await application.GetSetupStateAsync(
                    unknownFieldSetup).ResponseAsync;
                Assert.AreEqual(ApplicationErrorCode.InvalidArgument, unknownFieldRejected.Error.Code);
                Assert.AreEqual(
                    ToolValidationState.Available,
                    ApplicationGrpcService.ClassifySupportedToolVersion(
                        ExternalToolKind.ModOrganizer2,
                        "2.5.2"));
                Assert.AreEqual(
                    ToolValidationState.Unsupported,
                    ApplicationGrpcService.ClassifySupportedToolVersion(
                        ExternalToolKind.ModOrganizer2,
                        "2.5.1"));
                Assert.AreEqual(
                    ToolValidationState.NotYetValidated,
                    ApplicationGrpcService.ClassifySupportedToolVersion(
                        ExternalToolKind.Loot,
                        "0.24.1"));

                SubmitSetupCommandResponse malformedLoot = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-loot-relative",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r0" },
                        ValidateTool = new ValidateToolConfiguration
                        {
                            Tool = ExternalToolKind.Loot,
                            LootInstallationRoot = "relative-path-is-not-authority",
                        },
                    }).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Accepted, malformedLoot.Receipt.Disposition);
                Assert.AreEqual(
                    ToolValidationState.Misconfigured,
                    malformedLoot.Setup.Tools.Single(item => item.Tool == ExternalToolKind.Loot).State);
                SubmitSetupCommandResponse missingLoot = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-loot-missing",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r1" },
                        ValidateTool = new ValidateToolConfiguration
                        {
                            Tool = ExternalToolKind.Loot,
                            LootInstallationRoot = Path.Combine(root, "missing-loot"),
                        },
                    }).ResponseAsync;
                Assert.AreEqual(
                    ToolValidationState.Missing,
                    missingLoot.Setup.Tools.Single(item => item.Tool == ExternalToolKind.Loot).State);
                SubmitSetupCommandResponse uncLoot = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-loot-unc",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r2" },
                        ValidateTool = new ValidateToolConfiguration
                        {
                            Tool = ExternalToolKind.Loot,
                            LootInstallationRoot = @"\\server\share\loot",
                        },
                    }).ResponseAsync;
                Assert.AreEqual(
                    ToolValidationState.Misconfigured,
                    uncLoot.Setup.Tools.Single(item => item.Tool == ExternalToolKind.Loot).State);
                SubmitSetupCommandResponse deviceLoot = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-loot-device",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r3" },
                        ValidateTool = new ValidateToolConfiguration
                        {
                            Tool = ExternalToolKind.Loot,
                            LootInstallationRoot = @"\\?\C:\loot",
                        },
                    }).ResponseAsync;
                Assert.AreEqual(
                    ToolValidationState.Misconfigured,
                    deviceLoot.Setup.Tools.Single(item => item.Tool == ExternalToolKind.Loot).State);

                SubmitSetupCommandRequest create = new()
                {
                    RequestId = "request-create-local",
                    ExpectedRevision = new RevisionToken { OpaqueValue = "r0" },
                    CreateConfiguration = new CreateSavedScanConfiguration
                    {
                        ConfigurationId = new ScanConfigurationId { Value = "configuration-local" },
                        Name = "Local offline",
                        Values = new ScanConfigurationValues
                        {
                            LocalOnly = true,
                            MaximumConcurrency = 1,
                            MaximumElapsedMilliseconds = 60_000,
                        },
                    },
                };
                create.CreateConfiguration.Values.AnalysisCapabilities.Add(
                    AnalysisCapabilityKind.DeliveredIndexLocal);
                SubmitSetupCommandResponse created = await application.SubmitSetupCommandAsync(create).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Accepted, created.Receipt.Disposition);
                Assert.AreEqual("r1", created.Receipt.AcceptedRevision.OpaqueValue);

                SubmitSetupCommandResponse replayed = await application.SubmitSetupCommandAsync(create).ResponseAsync;
                Assert.AreEqual(OperationDisposition.AlreadyAccepted, replayed.Receipt.Disposition);
                SubmitSetupCommandRequest stale = create.Clone();
                stale.RequestId = "request-stale-local";
                stale.CreateConfiguration.Name = "Stale update";
                SubmitSetupCommandResponse conflicted = await application.SubmitSetupCommandAsync(stale).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Conflict, conflicted.Receipt.Disposition);

                SubmitSetupCommandResponse cloned = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-clone-local",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r0" },
                        CloneConfiguration = new CloneSavedScanConfiguration
                        {
                            SourceConfigurationId = new ScanConfigurationId { Value = "configuration-local" },
                            ConfigurationId = new ScanConfigurationId { Value = "configuration-clone" },
                            Name = "Local clone",
                        },
                    }).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Accepted, cloned.Receipt.Disposition);
                GetSavedScanConfigurationResponse cloneDetail =
                    await application.GetSavedScanConfigurationAsync(
                        new GetSavedScanConfigurationRequest
                        {
                            ConfigurationId = new ScanConfigurationId { Value = "configuration-clone" },
                        }).ResponseAsync;
                Assert.AreEqual("Local clone", cloneDetail.Configuration.Name);
                SubmitSetupCommandResponse updated = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-update-clone",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r1" },
                        UpdateConfiguration = new UpdateSavedScanConfiguration
                        {
                            ConfigurationId = new ScanConfigurationId { Value = "configuration-clone" },
                            Name = "Updated clone",
                            Values = create.CreateConfiguration.Values.Clone(),
                        },
                    }).ResponseAsync;
                Assert.AreEqual("r2", updated.Receipt.AcceptedRevision.OpaqueValue);
                GetSetupStateResponse bounded = await application.GetSetupStateAsync(
                    new GetSetupStateRequest
                    {
                        MaximumSavedConfigurations = 1,
                        ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                    }).ResponseAsync;
                Assert.HasCount(1, bounded.Setup.SavedConfigurations);
                SubmitSetupCommandResponse deleted = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-delete-clone",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r2" },
                        DeleteConfiguration = new DeleteSavedScanConfiguration
                        {
                            ConfigurationId = new ScanConfigurationId { Value = "configuration-clone" },
                        },
                    }).ResponseAsync;
                Assert.AreEqual("r3", deleted.Receipt.AcceptedRevision.OpaqueValue);
                GetSavedScanConfigurationResponse deletedDetail =
                    await application.GetSavedScanConfigurationAsync(
                        new GetSavedScanConfigurationRequest
                        {
                            ConfigurationId = new ScanConfigurationId { Value = "configuration-clone" },
                        }).ResponseAsync;
                Assert.IsTrue(deletedDetail.Configuration.Deleted);

                SubmitSetupCommandRequest unavailableCapability = create.Clone();
                unavailableCapability.RequestId = "request-unsupported-capability";
                unavailableCapability.CreateConfiguration.ConfigurationId =
                    new ScanConfigurationId { Value = "configuration-unsupported" };
                unavailableCapability.CreateConfiguration.Values.AnalysisCapabilities.Clear();
                unavailableCapability.CreateConfiguration.Values.AnalysisCapabilities.Add(
                    AnalysisCapabilityKind.Unsupported);
                SubmitSetupCommandResponse unavailable = await application.SubmitSetupCommandAsync(
                    unavailableCapability).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Rejected, unavailable.Receipt.Disposition);

                SubmitSetupCommandRequest unavailableProviderExecution = create.Clone();
                unavailableProviderExecution.RequestId = "request-provider-execution-unavailable";
                unavailableProviderExecution.CreateConfiguration.ConfigurationId =
                    new ScanConfigurationId { Value = "configuration-provider-unavailable" };
                unavailableProviderExecution.CreateConfiguration.Values.LocalOnly = false;
                unavailableProviderExecution.CreateConfiguration.Values.MaximumProviderDispatches = 1;
                SubmitSetupCommandResponse unavailableProvider = await application.SubmitSetupCommandAsync(
                    unavailableProviderExecution).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Rejected, unavailableProvider.Receipt.Disposition);

                SubmitSetupCommandResponse enrollment = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-provider-intent",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r0" },
                        RequestProviderEnrollment = new RequestProviderEnrollment
                        {
                            ProfileId = new ProviderAccessProfileId { Value = "provider-profile-a" },
                            DisplayLabel = "Provider profile A",
                        },
                    }).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Accepted, enrollment.Receipt.Disposition);
                Assert.IsTrue(enrollment.Setup.Provider.EnrollmentPending);
                Assert.IsFalse(enrollment.Setup.Provider.Configured);
                Assert.IsFalse(enrollment.Setup.Provider.Verified);

                byte[] canaryBytes = Encoding.UTF8.GetBytes(credentialCanary);
                SubmitSetupCommandRequest canaryCarrier = SubmitSetupCommandRequest.Parser.ParseFrom(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-provider-canary",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r1" },
                        RequestProviderEnrollment = new RequestProviderEnrollment
                        {
                            ProfileId = new ProviderAccessProfileId { Value = "provider-profile-b" },
                            DisplayLabel = "Provider profile B",
                        },
                    }.ToByteArray()
                    .Concat(new byte[] { 0xa2, 0x06, checked((byte)canaryBytes.Length) })
                    .Concat(canaryBytes)
                    .ToArray());
                SubmitSetupCommandResponse canaryRejected = await application.SubmitSetupCommandAsync(
                    canaryCarrier).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Rejected, canaryRejected.Receipt.Disposition);
                Assert.IsFalse(canaryRejected.ToString().Contains(credentialCanary, StringComparison.Ordinal));

                PrepareManualRunResponse nonexistentInput = await application.PrepareManualRunAsync(
                    new PrepareManualRunRequest
                    {
                        RequestId = "request-prepare-nonexistent",
                        SavedConfigurationId = new ScanConfigurationId { Value = "configuration-local" },
                        ExpectedConfigurationRevision = new RevisionToken { OpaqueValue = "r1" },
                        ExpectedProfileRevision = new RevisionToken { OpaqueValue = "r1" },
                        InstallationSnapshotId = new InstallationSnapshotId { Value = "snapshot-missing" },
                        AnalysisContextId = new AnalysisContextId { Value = retainedFixture.AnalysisContextId },
                        ResolvedInputManifestId = new ResolvedInputManifestId { Value = retainedFixture.ResolvedInputManifestId },
                    }).ResponseAsync;
                Assert.AreEqual(PrepareManualRunResponse.ResultOneofCase.Error, nonexistentInput.ResultCase);
                PrepareManualRunResponse substitutedContext = await application.PrepareManualRunAsync(
                    new PrepareManualRunRequest
                    {
                        RequestId = "request-prepare-substituted-context",
                        SavedConfigurationId = new ScanConfigurationId { Value = "configuration-local" },
                        ExpectedConfigurationRevision = new RevisionToken { OpaqueValue = "r1" },
                        ExpectedProfileRevision = new RevisionToken { OpaqueValue = "r1" },
                        InstallationSnapshotId = new InstallationSnapshotId { Value = retainedFixture.InstallationSnapshotId },
                        AnalysisContextId = new AnalysisContextId { Value = "analysis-context-substituted" },
                        ResolvedInputManifestId = new ResolvedInputManifestId { Value = retainedFixture.ResolvedInputManifestId },
                    }).ResponseAsync;
                Assert.AreEqual(PrepareManualRunResponse.ResultOneofCase.Error, substitutedContext.ResultCase);
                PrepareManualRunResponse substitutedManifest = await application.PrepareManualRunAsync(
                    new PrepareManualRunRequest
                    {
                        RequestId = "request-prepare-substituted-manifest",
                        SavedConfigurationId = new ScanConfigurationId { Value = "configuration-local" },
                        ExpectedConfigurationRevision = new RevisionToken { OpaqueValue = "r1" },
                        ExpectedProfileRevision = new RevisionToken { OpaqueValue = "r1" },
                        InstallationSnapshotId = new InstallationSnapshotId { Value = retainedFixture.InstallationSnapshotId },
                        AnalysisContextId = new AnalysisContextId { Value = retainedFixture.AnalysisContextId },
                        ResolvedInputManifestId = new ResolvedInputManifestId { Value = "resolved-manifest-substituted" },
                    }).ResponseAsync;
                Assert.AreEqual(PrepareManualRunResponse.ResultOneofCase.Error, substitutedManifest.ResultCase);

                PrepareManualRunResponse prepared = await application.PrepareManualRunAsync(
                    new PrepareManualRunRequest
                    {
                        RequestId = "request-prepare-local",
                        SavedConfigurationId = new ScanConfigurationId { Value = "configuration-local" },
                        ExpectedConfigurationRevision = new RevisionToken { OpaqueValue = "r1" },
                        ExpectedProfileRevision = new RevisionToken { OpaqueValue = "r1" },
                        InstallationSnapshotId = new InstallationSnapshotId { Value = retainedFixture.InstallationSnapshotId },
                        AnalysisContextId = new AnalysisContextId { Value = retainedFixture.AnalysisContextId },
                        ResolvedInputManifestId = new ResolvedInputManifestId { Value = retainedFixture.ResolvedInputManifestId },
                    }).ResponseAsync;
                Assert.AreEqual(PrepareManualRunResponse.ResultOneofCase.Preparation, prepared.ResultCase);
                preparationId = prepared.Preparation.PreparationId;
                Assert.AreEqual(AvailabilityState.Available, prepared.Preparation.Estimate.EstimatedCalculatedNanoUsd.Availability);
                Assert.AreEqual(0L, prepared.Preparation.Estimate.EstimatedCalculatedNanoUsd.Value);
                Assert.AreEqual(AvailabilityState.Unavailable, prepared.Preparation.Estimate.EstimatedElapsedMilliseconds.Availability);
                Assert.AreEqual(AvailabilityState.Unavailable, prepared.Preparation.Estimate.EstimatedCoverageUnits.Availability);

                SubmitPreparedRunRequest submitRequest = new()
                {
                    IdempotencyKey = new DurableCommandId { Value = "command-prepared-local" },
                    PreparationId = prepared.Preparation.PreparationId,
                    ExpectedPreparationRevision = new RevisionToken { OpaqueValue = "r1" },
                    RequestedRunId = new RunId { Value = "run-prepared-local" },
                    InitiationKind = ManualInitiationKind.EvaluationHarness,
                    UserGestureId = "gesture-1234567890abcdef",
                    DispatchDeadline = ProtoMapping.ToProto(DateTimeOffset.UtcNow.AddMinutes(1)),
                };
                SubmitRunCommandResponse submitted = await application.SubmitPreparedRunAsync(
                    submitRequest).ResponseAsync;
                Assert.AreEqual(CommandDisposition.Accepted, submitted.Disposition);
                preparedRunId = submitted.RunId.Value;

                SubmitRunCommandResponse duplicate = await application.SubmitPreparedRunAsync(
                    submitRequest).ResponseAsync;
                Assert.AreEqual(CommandDisposition.AlreadyAccepted, duplicate.Disposition);
                Assert.AreEqual(preparedRunId, duplicate.RunId.Value);
                SubmitPreparedRunRequest rebound = submitRequest.Clone();
                rebound.RequestedRunId = new RunId { Value = "run-prepared-substituted" };
                SubmitRunCommandResponse rejectedRebind = await application.SubmitPreparedRunAsync(
                    rebound).ResponseAsync;
                Assert.AreEqual(CommandDisposition.Rejected, rejectedRebind.Disposition);
                SubmitPreparedRunRequest changedDeadline = submitRequest.Clone();
                changedDeadline.DispatchDeadline = ProtoMapping.ToProto(DateTimeOffset.UtcNow.AddMinutes(2));
                SubmitRunCommandResponse rejectedDeadline = await application.SubmitPreparedRunAsync(
                    changedDeadline).ResponseAsync;
                Assert.AreEqual(CommandDisposition.Rejected, rejectedDeadline.Disposition);
                SubmitPreparedRunRequest reusedGesture = submitRequest.Clone();
                reusedGesture.IdempotencyKey = new DurableCommandId { Value = "command-reused-gesture" };
                reusedGesture.RequestedRunId = new RunId { Value = "run-reused-gesture" };
                SubmitRunCommandResponse rejectedGesture = await application.SubmitPreparedRunAsync(
                    reusedGesture).ResponseAsync;
                Assert.AreEqual(CommandDisposition.Rejected, rejectedGesture.Disposition);
                SubmitPreparedRunRequest stalePreparation = submitRequest.Clone();
                stalePreparation.IdempotencyKey = new DurableCommandId { Value = "command-stale-preparation" };
                stalePreparation.RequestedRunId = new RunId { Value = "run-stale-preparation" };
                stalePreparation.UserGestureId = "gesture-stale-preparation-1234";
                stalePreparation.ExpectedPreparationRevision = new RevisionToken { OpaqueValue = "r0" };
                SubmitRunCommandResponse rejectedPreparation = await application.SubmitPreparedRunAsync(
                    stalePreparation).ResponseAsync;
                Assert.AreEqual(CommandDisposition.Rejected, rejectedPreparation.Disposition);

                using CancellationTokenSource progressTimeout = new(TimeSpan.FromSeconds(30));
                ProgressSnapshot? terminalProgress = null;
                while (terminalProgress?.LifecycleState !=
                       Infinium.Contracts.Protobuf.Domain.V1.LifecycleState.Completed)
                {
                    GetProgressResponse progress = await application.GetProgressAsync(
                        new GetProgressRequest
                        {
                            RunId = new RunId { Value = preparedRunId },
                            ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                        },
                        cancellationToken: progressTimeout.Token).ResponseAsync;
                    Assert.AreEqual(GetProgressResponse.ResultOneofCase.Progress, progress.ResultCase);
                    terminalProgress = progress.Progress;
                    if (terminalProgress.LifecycleState !=
                        Infinium.Contracts.Protobuf.Domain.V1.LifecycleState.Completed)
                    {
                        await Task.Delay(25, progressTimeout.Token);
                    }
                }
                Assert.AreEqual(1UL, terminalProgress.Progress.CompletedUnits);

                string replacementMo2Root = Path.Combine(root, "replacement-mo2");
                Directory.CreateDirectory(Path.Combine(replacementMo2Root, "profiles", "Profile A"));
                File.Copy(
                    typeof(SolutionIntegrationTests).Assembly.Location,
                    Path.Combine(replacementMo2Root, "ModOrganizer.exe"));
                File.WriteAllText(
                    Path.Combine(replacementMo2Root, "ModOrganizer.ini"),
                    "[General]\nselected_profile=@ByteArray(Profile A)\n",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                SubmitSetupCommandResponse changedInstallation = await application.SubmitSetupCommandAsync(
                    new SubmitSetupCommandRequest
                    {
                        RequestId = "request-change-mo2-installation",
                        ExpectedRevision = new RevisionToken { OpaqueValue = "r1" },
                        ValidateTool = new ValidateToolConfiguration
                        {
                            Tool = ExternalToolKind.ModOrganizer2,
                            ModOrganizerInstallationRoot = replacementMo2Root,
                        },
                    }).ResponseAsync;
                Assert.AreEqual(OperationDisposition.Accepted, changedInstallation.Receipt.Disposition);
                Assert.IsFalse(changedInstallation.Setup.ProfileSelection.ExplicitlyConfirmed);
                Assert.AreNotEqual(
                    profileId,
                    changedInstallation.Setup.ProfileSelection.SuggestedCandidateId);
            }

            await WaitForRunStateAsync(root, preparedRunId, LifecycleState.Completed, TimeSpan.FromSeconds(30));
            StopCoordinator(root, coordinatorProcessId);
            coordinatorProcessId = 0;

            ProcessResult afterRestart = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", preparedRunId, "--json"],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, afterRestart.ExitCode, afterRestart.Error);
            RuntimeDescriptor restarted = RuntimeDescriptor.Read(root);
            coordinatorProcessId = restarted.ProcessId;
            using JsonDocument restartJson = JsonDocument.Parse(afterRestart.Output);
            Assert.AreEqual("Completed", restartJson.RootElement.GetProperty("lifecycle").GetProperty("state").GetString());

            StopCoordinator(root, coordinatorProcessId);
            coordinatorProcessId = 0;
            using AuthoritativeStore readback = new(new StoragePaths(root));
            Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, readback.GetSchemaVersion());
            DurableCommandRecord receipt = readback.GetDurableCommand("command-prepared-local");
            Assert.AreEqual(preparationId, receipt.StartPreparationId);
            Assert.AreEqual("gesture-1234567890abcdef", receipt.StartUserGestureId);
            Assert.AreEqual("effective-", receipt.RunBinding.EffectiveScanConfigurationId[..10]);
            RunOperationRecord operation = readback.GetRunOperation(preparedRunId)!;
            Assert.AreEqual("managed-analysis-v1", operation.OperationKind);
            Assert.AreEqual(operation.RequestSha256, Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(operation.RequestJson))));
            ManagedAnalysisOrchestrationRequest operationRequest = JsonSerializer.Deserialize<
                ManagedAnalysisOrchestrationRequest>(
                    operation.RequestJson,
                    Infinium.Domain.Contracts.ContractJsonSerializer.Options)!;
            Assert.AreEqual(60_000, operationRequest.ExecutionInput.Limits.MaximumWallTimeMilliseconds);
            Assert.AreEqual(
                receipt.RunBinding.EffectiveScanConfigurationId,
                operationRequest.ExecutionInput.EffectiveConfiguration.ArtifactId.Value);
            PreparedRunSubmissionRecord submission = readback.GetPreparedRunSubmission(
                "command-prepared-local");
            Assert.AreEqual(64, submission.SubmissionFingerprint.Length);
            Assert.AreNotEqual(new string('0', 64), submission.SubmissionFingerprint);
            Infinium.Domain.Contracts.RunOutputContract output =
                Infinium.Application.Serialization.RunOutputJsonCodec.Deserialize(
                    readback.ReadAnalysisRunOutput(preparedRunId));
            Assert.AreEqual(
                "candidate-source-delivered-indexes-v1",
                output.AnalyzerDeclarations.Single().ArtifactId);
            Assert.AreEqual(retainedFixture.InstallationSnapshotId, output.InstallationSnapshot.ArtifactId);
            Assert.AreEqual("completed", output.RunState);
            Assert.HasCount(0, output.CoverageGaps);
            Assert.AreEqual("completed", output.AnalyzerCoverage.Single().Status);
            Assert.HasCount(3, readback.ListSetupObjects("saved-scan-configuration"));
            BackupArtifact backup = readback.CreateBackup("PreparedAnalysisCanary", DateTimeOffset.UtcNow);
            Assert.IsFalse(File.ReadAllText(backup.ManifestPath).Contains(
                credentialCanary,
                StringComparison.Ordinal));
            Assert.IsFalse(File.ReadAllBytes(backup.DatabasePath).AsSpan().IndexOf(
                Encoding.UTF8.GetBytes(credentialCanary)) >= 0);
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (file.StartsWith(Path.Combine(root, "data") + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                byte[] bytes = File.ReadAllBytes(file);
                Assert.IsFalse(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(credentialCanary)) >= 0, file);
            }
        }
        finally
        {
            StopCoordinator(root, coordinatorProcessId);
            if (Directory.Exists(root))
            {
                DeleteDirectoryAfterWorkerRelease(root);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Evaluation")]
    public async Task CliCoordinatorWorkerNamedPipeFlowCompletesAndInspectsImmutableBindings()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-ipc-fixture-{Guid.NewGuid():N}");
        int coordinatorProcessId = 0;
        try
        {
            ProcessResult start = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-a",
                    "--context", "context-a",
                    "--configuration", "configuration-a",
                    "--manifest", "manifest-a",
                    "--json",
                ],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, start.ExitCode, start.Error);
            using JsonDocument startJson = JsonDocument.Parse(start.Output);
            string runId = startJson.RootElement.GetProperty("runId").GetString()!;

            ProcessResult wait = Run(
                "Infinium.Cli",
                ["--root", root, "wait", runId, "--timeout-seconds", "30", "--json"],
                timeoutMilliseconds: 40_000);
            Assert.AreEqual(0, wait.ExitCode, wait.Error);
            using JsonDocument waitJson = JsonDocument.Parse(wait.Output);
            Assert.AreEqual("Completed", waitJson.RootElement.GetProperty("state").GetString());

            ProcessResult inspect = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", runId, "--json"]);
            Assert.AreEqual(0, inspect.ExitCode, inspect.Error);
            using JsonDocument inspectJson = JsonDocument.Parse(inspect.Output);
            JsonElement bindings = inspectJson.RootElement.GetProperty("immutableBindings");
            Assert.AreEqual("snapshot-a", bindings.GetProperty("installationSnapshotId").GetString());
            Assert.AreEqual("context-a", bindings.GetProperty("analysisContextId").GetString());
            Assert.AreEqual(
                "configuration-a",
                bindings.GetProperty("effectiveScanConfigurationId").GetString());
            Assert.AreEqual(
                "manifest-a",
                bindings.GetProperty("resolvedInputManifestId").GetString());

            string descriptorPath = Path.Combine(root, "runtime", "coordinator.v1.json");
            using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            coordinatorProcessId = descriptor.RootElement.GetProperty("ProcessId").GetInt32();
            Assert.AreEqual(
                "standard-user",
                descriptor.RootElement.GetProperty("Elevation").GetString());
            StringAssert.EndsWith(
                descriptor.RootElement.GetProperty("ApplicationPipe").GetString()!,
                "-application");
            StringAssert.EndsWith(
                descriptor.RootElement.GetProperty("WorkerPipe").GetString()!,
                "-worker");
            Assert.AreNotEqual(
                descriptor.RootElement.GetProperty("ApplicationPipe").GetString(),
                descriptor.RootElement.GetProperty("WorkerPipe").GetString());

            ProcessResult competingCoordinator = Run(
                "Infinium.Coordinator",
                ["--root", root]);
            Assert.AreEqual(4, competingCoordinator.ExitCode);
            StringAssert.Contains(competingCoordinator.Error, "already owns");

            HashSet<int> existingCoordinatorChildren =
                CaptureDirectChildProcessIds(coordinatorProcessId);
            Task<SuspendedWorkerBarrier> workerBarrier = SuspendNewWorkerAsync(
                coordinatorProcessId,
                existingCoordinatorChildren,
                TimeSpan.FromSeconds(10));
            ProcessResult cancellable = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-cancel",
                    "--context", "context-cancel",
                    "--configuration", "configuration-cancel",
                    "--manifest", "manifest-cancel",
                    "--command-id", "start-cancellable-command",
                    "--json",
                ]);
            Assert.AreEqual(0, cancellable.ExitCode, cancellable.Error);
            using JsonDocument cancellableJson = JsonDocument.Parse(cancellable.Output);
            string cancellableRunId =
                cancellableJson.RootElement.GetProperty("runId").GetString()!;
            using (await workerBarrier.ConfigureAwait(false))
            {
                await WaitForRunStateAsync(
                    root,
                    cancellableRunId,
                    LifecycleState.Running,
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                ProcessResult crossKindReplay = Run(
                    "Infinium.Cli",
                    [
                        "--root", root,
                        "cancel", cancellableRunId,
                        "--command-id", "start-cancellable-command",
                        "--json",
                    ]);
                Assert.AreNotEqual(0, crossKindReplay.ExitCode);
                StringAssert.Contains(
                    crossKindReplay.Error,
                    "already bound to different command inputs");

                ProcessResult cancel = Run(
                    "Infinium.Cli",
                    [
                        "--root", root,
                        "cancel", cancellableRunId,
                        "--command-id", "cancel-cancellable-command",
                        "--json",
                    ]);
                Assert.AreEqual(0, cancel.ExitCode, cancel.Error);
                ProcessResult cancellingInspect = Run(
                    "Infinium.Cli",
                    ["--root", root, "inspect", cancellableRunId, "--json"]);
                using JsonDocument cancellingJson = JsonDocument.Parse(cancellingInspect.Output);
                Assert.AreEqual(
                    "Cancelling",
                    cancellingJson.RootElement
                        .GetProperty("lifecycle")
                        .GetProperty("state")
                        .GetString());

                ProcessResult replayedCancel = Run(
                    "Infinium.Cli",
                    [
                        "--root", root,
                        "cancel", cancellableRunId,
                        "--command-id", "cancel-cancellable-command",
                        "--json",
                    ]);
                Assert.AreEqual(0, replayedCancel.ExitCode, replayedCancel.Error);
                using JsonDocument replayedCancelJson = JsonDocument.Parse(replayedCancel.Output);
                Assert.AreEqual(
                    "AlreadyAccepted",
                    replayedCancelJson.RootElement.GetProperty("disposition").GetString());
            }

            await WaitForRunStateAsync(
                root,
                cancellableRunId,
                LifecycleState.Cancelled,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            ProcessResult cancelledInspect = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", cancellableRunId, "--json"]);
            using JsonDocument cancelledJson = JsonDocument.Parse(cancelledInspect.Output);
            Assert.AreEqual(
                "Cancelled",
                cancelledJson.RootElement
                    .GetProperty("lifecycle")
                    .GetProperty("state")
                    .GetString());
            await AssertIpcRoleVersionNonceAndBoundariesAsync(root).ConfigureAwait(false);
        }
        finally
        {
            StopCoordinator(root, coordinatorProcessId);
            if (Directory.Exists(root))
            {
                DeleteDirectoryAfterWorkerRelease(root);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public async Task CoordinatorRestartFencesInterruptedWorkerAndRecoversDurableRun()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-recovery-fixture-{Guid.NewGuid():N}");
        int firstCoordinatorProcessId = 0;
        int recoveredCoordinatorProcessId = 0;
        try
        {
            ProcessResult start = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-recovery",
                    "--context", "context-recovery",
                    "--configuration", "configuration-recovery",
                    "--manifest", "manifest-recovery",
                    "--json",
                ],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, start.ExitCode, start.Error);
            using JsonDocument startJson = JsonDocument.Parse(start.Output);
            string runId = startJson.RootElement.GetProperty("runId").GetString()!;

            await WaitForRunStateAsync(
                root,
                runId,
                LifecycleState.Running,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            RuntimeDescriptor first = RuntimeDescriptor.Read(root);
            firstCoordinatorProcessId = first.ProcessId;
            StopProcess(firstCoordinatorProcessId);
            firstCoordinatorProcessId = 0;

            ProcessResult wait = Run(
                "Infinium.Cli",
                ["--root", root, "wait", runId, "--timeout-seconds", "30", "--json"],
                timeoutMilliseconds: 40_000);
            Assert.AreEqual(0, wait.ExitCode, wait.Error);
            using JsonDocument waitJson = JsonDocument.Parse(wait.Output);
            Assert.AreEqual("Completed", waitJson.RootElement.GetProperty("state").GetString());
            Assert.IsTrue(
                waitJson.RootElement.GetProperty("generation").GetUInt64() >= 4);

            RuntimeDescriptor recovered = RuntimeDescriptor.Read(root);
            recoveredCoordinatorProcessId = recovered.ProcessId;
            Assert.AreNotEqual(first.ProcessId, recovered.ProcessId);
            Assert.IsTrue(recovered.FencingEpoch > first.FencingEpoch);
        }
        finally
        {
            StopProcess(firstCoordinatorProcessId);
            StopCoordinator(root, recoveredCoordinatorProcessId);
            if (Directory.Exists(root))
            {
                DeleteDirectoryAfterWorkerRelease(root);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public async Task CoordinatorRestartObservesPendingCancellationAndSettlesAttempt()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-cancel-recovery-{Guid.NewGuid():N}");
        int coordinatorProcessId = 0;
        try
        {
            ProcessResult start = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-cancel-recovery",
                    "--context", "context-cancel-recovery",
                    "--configuration", "configuration-cancel-recovery",
                    "--manifest", "manifest-cancel-recovery",
                    "--json",
                ],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, start.ExitCode, start.Error);
            using JsonDocument startJson = JsonDocument.Parse(start.Output);
            string runId = startJson.RootElement.GetProperty("runId").GetString()!;
            await WaitForRunStateAsync(
                root,
                runId,
                LifecycleState.Running,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            ProcessResult cancel = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "cancel", runId,
                    "--command-id", "cancel-before-restart",
                    "--json",
                ]);
            Assert.AreEqual(0, cancel.ExitCode, cancel.Error);
            RuntimeDescriptor beforeRestart = RuntimeDescriptor.Read(root);
            coordinatorProcessId = beforeRestart.ProcessId;
            StopProcess(coordinatorProcessId);
            coordinatorProcessId = 0;

            ProcessResult inspect = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", runId, "--json"],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, inspect.ExitCode, inspect.Error);
            using JsonDocument inspectJson = JsonDocument.Parse(inspect.Output);
            Assert.AreEqual(
                "Cancelled",
                inspectJson.RootElement
                    .GetProperty("lifecycle")
                    .GetProperty("state")
                    .GetString());
            RuntimeDescriptor afterRestart = RuntimeDescriptor.Read(root);
            coordinatorProcessId = afterRestart.ProcessId;
            Assert.IsGreaterThan(beforeRestart.FencingEpoch, afterRestart.FencingEpoch);

            StopCoordinator(root, coordinatorProcessId);
            coordinatorProcessId = 0;
            using AuthoritativeStore store = new(new StoragePaths(root));
            Assert.IsFalse(store.HasLiveAttempts(runId));
        }
        finally
        {
            StopCoordinator(root, coordinatorProcessId);
            if (Directory.Exists(root))
            {
                DeleteDirectoryAfterWorkerRelease(root);
            }
        }
    }

#pragma warning disable CA1416 // This integration helper is explicitly Windows-gated above.
    private static async Task AssertIpcRoleVersionNonceAndBoundariesAsync(string root)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The named-pipe integration contract requires Windows.");
        }

        RuntimeDescriptor descriptor = RuntimeDescriptor.Read(root);
        using (NamedPipeClientStream securityProbe = new(
            ".",
            descriptor.ApplicationPipe,
            PipeDirection.InOut,
            PipeOptions.Asynchronous))
        {
            await securityProbe.ConnectAsync(5_000).ConfigureAwait(false);
            PipeSecurity security = securityProbe.GetAccessControl();
            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: false,
                typeof(SecurityIdentifier));
            SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User!;
            Assert.IsTrue(rules.OfType<PipeAccessRule>().Any(rule =>
                rule.AccessControlType == AccessControlType.Allow
                && currentUser.Equals(rule.IdentityReference)
                && (rule.PipeAccessRights & PipeAccessRights.ReadWrite) != 0));
            SecurityIdentifier network =
                new(WellKnownSidType.NetworkSid, domainSid: null);
            Assert.IsTrue(rules.OfType<PipeAccessRule>().Any(rule =>
                rule.AccessControlType == AccessControlType.Deny
                && network.Equals(rule.IdentityReference)));
        }

        using (GrpcChannel unauthenticatedChannel =
            NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe))
        {
            ApplicationService.ApplicationServiceClient unauthenticated = new(unauthenticatedChannel);
            RpcException exception = await Assert.ThrowsExactlyAsync<RpcException>(
                async () =>
                {
                    _ = await unauthenticated.HealthAsync(new HealthRequest()).ResponseAsync;
                });
            Assert.AreEqual(StatusCode.Unauthenticated, exception.StatusCode);
        }

        using (GrpcChannel applicationChannel =
            NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe))
        {
            ApplicationService.ApplicationServiceClient application = new(applicationChannel);
            ApplicationHandshakeRequest badNonce = ApplicationHandshake(descriptor);
            badNonce.CoordinatorInstanceNonce = ByteString.CopyFrom(new byte[32]);
            HandshakeResponse nonceResponse =
                await application.NegotiateAsync(badNonce).ResponseAsync;
            Assert.AreEqual(HandshakeDisposition.InvalidNonce, nonceResponse.Disposition);

            ApplicationHandshakeRequest badMajor = ApplicationHandshake(descriptor);
            badMajor.SupportedProtocol.Major++;
            HandshakeResponse majorResponse =
                await application.NegotiateAsync(badMajor).ResponseAsync;
            Assert.AreEqual(HandshakeDisposition.IncompatibleMajor, majorResponse.Disposition);

            ApplicationHandshakeRequest unknownClient = ApplicationHandshake(descriptor);
            unknownClient.ClientKind = ApplicationClientKind.Unknown;
            HandshakeResponse unknownClientResponse =
                await application.NegotiateAsync(unknownClient).ResponseAsync;
            Assert.AreEqual(
                HandshakeDisposition.UnsupportedCapability,
                unknownClientResponse.Disposition);

            HandshakeResponse accepted =
                await application.NegotiateAsync(ApplicationHandshake(descriptor)).ResponseAsync;
            Assert.AreEqual(HandshakeDisposition.Accepted, accepted.Disposition);
            GetApplicationBootstrapResponse bootstrap = await application.GetApplicationBootstrapAsync(
                new GetApplicationBootstrapRequest
                {
                    RendererContractVersion = new SemanticVersion
                    {
                        Value = ProtocolConstants.RendererContractVersion,
                    },
                    MaximumRecentRuns = ProtocolConstants.MaximumBootstrapRecentRuns,
                    ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                }).ResponseAsync;
            Assert.AreEqual(
                GetApplicationBootstrapResponse.ResultOneofCase.Bootstrap,
                bootstrap.ResultCase);
            Assert.AreEqual(ProtocolConstants.ContractVersion,
                bootstrap.Bootstrap.Compatibility.ApplicationContract.Value);
            Assert.AreEqual(ProtocolConstants.DomainContractVersion,
                bootstrap.Bootstrap.Compatibility.DomainContract.Value);
            Assert.AreEqual(ProtocolConstants.StorageContractVersion,
                bootstrap.Bootstrap.Compatibility.StorageContract.Value);
            Assert.AreEqual(ProtocolConstants.RendererContractVersion,
                bootstrap.Bootstrap.RendererContractVersion.Value);
            Assert.IsTrue(bootstrap.Bootstrap.RecentRuns.Count <= ProtocolConstants.MaximumBootstrapRecentRuns);
            Assert.IsTrue(bootstrap.Bootstrap.Capabilities.All(item =>
                item.Capability != ApplicationCapability.Unspecified
                && item.Availability != Availability.Unspecified));

            GetApplicationBootstrapResponse incompatibleBootstrap =
                await application.GetApplicationBootstrapAsync(new GetApplicationBootstrapRequest
                {
                    RendererContractVersion = new SemanticVersion { Value = "2.0.0" },
                    MaximumRecentRuns = 1,
                }).ResponseAsync;
            Assert.AreEqual(
                ApplicationErrorCode.IncompatibleVersion,
                incompatibleBootstrap.Error.Code);
            GetApplicationBootstrapRequest validBootstrapRequest = new()
            {
                RendererContractVersion = new SemanticVersion
                {
                    Value = ProtocolConstants.RendererContractVersion,
                },
                MaximumRecentRuns = 1,
            };
            GetApplicationBootstrapRequest unknownFieldBootstrap =
                GetApplicationBootstrapRequest.Parser.ParseFrom(
                    validBootstrapRequest.ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray());
            GetApplicationBootstrapResponse unknownFieldResponse =
                await application.GetApplicationBootstrapAsync(unknownFieldBootstrap).ResponseAsync;
            Assert.AreEqual(ApplicationErrorCode.InvalidArgument, unknownFieldResponse.Error.Code);
            ListRunsResponse bounded = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = ProtocolConstants.MaximumPageItems + 1,
            }).ResponseAsync;
            Assert.AreEqual(FailureCode.LimitExceeded, bounded.Failure.Code);
            ListRunsResponse firstPage = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = 1,
            }).ResponseAsync;
            Assert.IsTrue(firstPage.Page.HasMore);
            Assert.HasCount(1, firstPage.Page.Items);
            Assert.IsTrue(firstPage.Page.Next.OpaqueValue.Length > 32);
            ListRunsResponse secondPage = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = 1,
                After = firstPage.Page.Next,
                ExpectedProjectionVersion = firstPage.Page.ProjectionVersion,
            }).ResponseAsync;
            Assert.HasCount(1, secondPage.Page.Items);
            Assert.AreNotEqual(
                firstPage.Page.Items[0].RunId.Value,
                secondPage.Page.Items[0].RunId.Value);
            PageCursor tampered = firstPage.Page.Next.Clone();
            tampered.OpaqueValue = ByteString.CopyFrom(
                tampered.OpaqueValue.ToByteArray().Select((value, index) =>
                    index == 0 ? (byte)(value ^ 0xff) : value).ToArray());
            ListRunsResponse rejectedCursor = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = 1,
                After = tampered,
            }).ResponseAsync;
            Assert.AreEqual(
                CursorDisposition.Malformed,
                rejectedCursor.CursorRejection.Disposition);

            string streamRunId = firstPage.Page.Items[0].RunId.Value;
            GetRunResponse staleProjection = await application.GetRunAsync(new GetRunRequest
            {
                RunId = new RunId { Value = streamRunId },
                ExpectedProjectionVersion = new ProjectionVersion { Value = "obsolete" },
            }).ResponseAsync;
            Assert.AreEqual(
                GetRunResponse.ResultOneofCase.ProjectionInvalidated,
                staleProjection.ResultCase);

            EventCursor resume;
            LifecycleState stateBeforeTransportCancel;
            using (CancellationTokenSource streamCancellation = new(TimeSpan.FromSeconds(5)))
            using (AsyncServerStreamingCall<ApplicationEvent> stream =
                application.SubscribeEvents(
                    new SubscribeEventsRequest
                    {
                        SubscriptionId = new SubscriptionId { Value = Guid.NewGuid().ToString("N") },
                        RequestedQueueItems = 2,
                        RunScope = { new RunId { Value = streamRunId } },
                        ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                    },
                    cancellationToken: streamCancellation.Token))
            {
                Assert.IsTrue(await stream.ResponseStream.MoveNext(streamCancellation.Token));
                ApplicationEvent firstEvent = stream.ResponseStream.Current;
                Assert.AreEqual(EventKind.Progress, firstEvent.Kind);
                Assert.IsTrue(firstEvent.ResumeCursor.OpaqueValue.Length > 32);
                resume = firstEvent.ResumeCursor.Clone();
                stateBeforeTransportCancel = firstEvent.Progress.LifecycleState;
                streamCancellation.Cancel();
            }

            EventCursor invalidResume = resume.Clone();
            byte[] invalidResumeBytes = invalidResume.OpaqueValue.ToByteArray();
            invalidResumeBytes[0] ^= 0xff;
            invalidResume.OpaqueValue = ByteString.CopyFrom(invalidResumeBytes);
            using (CancellationTokenSource resyncCancellation = new(TimeSpan.FromSeconds(5)))
            using (AsyncServerStreamingCall<ApplicationEvent> resync =
                application.SubscribeEvents(
                    new SubscribeEventsRequest
                    {
                        SubscriptionId = new SubscriptionId { Value = Guid.NewGuid().ToString("N") },
                        RequestedQueueItems = 2,
                        RunScope = { new RunId { Value = streamRunId } },
                        After = invalidResume,
                        ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                    },
                    cancellationToken: resyncCancellation.Token))
            {
                Assert.IsTrue(await resync.ResponseStream.MoveNext(resyncCancellation.Token));
                Assert.AreEqual(EventKind.ResyncRequired, resync.ResponseStream.Current.Kind);
                Assert.AreEqual(
                    ResyncReason.CursorInvalid,
                    resync.ResponseStream.Current.ResyncRequired.Reason);
            }

            GetRunResponse afterTransportCancel = await application.GetRunAsync(new GetRunRequest
            {
                RunId = new RunId { Value = streamRunId },
            }).ResponseAsync;
            Assert.AreEqual(
                stateBeforeTransportCancel,
                afterTransportCancel.Run.Summary.LifecycleState);

            WorkerService.WorkerServiceClient wrongRoleWorker = new(applicationChannel);
            HandshakeResponse wrongWorkerEndpoint = await wrongRoleWorker.NegotiateAsync(
                new WorkerHandshakeRequest
                {
                    SupportedProtocol = new ProtocolVersionRange
                    {
                        Major = ProtocolConstants.Major,
                        MinimumMinor = ProtocolConstants.Minor,
                        MaximumMinor = ProtocolConstants.Minor,
                    },
                }).ResponseAsync;
            Assert.AreEqual(
                HandshakeDisposition.WrongEndpoint,
                wrongWorkerEndpoint.Disposition);
        }

        using GrpcChannel workerChannel =
            NamedPipeGrpcChannel.Create(descriptor.WorkerPipe);
        ApplicationService.ApplicationServiceClient wrongRoleApplication = new(workerChannel);
        HandshakeResponse wrongApplicationEndpoint = await wrongRoleApplication.NegotiateAsync(
            ApplicationHandshake(descriptor)).ResponseAsync;
        Assert.AreEqual(
            HandshakeDisposition.WrongEndpoint,
            wrongApplicationEndpoint.Disposition);
    }
#pragma warning restore CA1416

    private static async Task WaitForRunStateAsync(
        string root,
        string runId,
        LifecycleState expected,
        TimeSpan timeout)
    {
        RuntimeDescriptor descriptor = RuntimeDescriptor.Read(root);
        using GrpcChannel channel = NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe);
        ApplicationService.ApplicationServiceClient application = new(channel);
        HandshakeResponse accepted =
            await application.NegotiateAsync(ApplicationHandshake(descriptor)).ResponseAsync;
        Assert.AreEqual(HandshakeDisposition.Accepted, accepted.Disposition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            GetRunResponse response = await application.GetRunAsync(new GetRunRequest
            {
                RunId = new RunId { Value = runId },
            }).ResponseAsync;
            if (response.Run?.Summary?.LifecycleState == expected)
            {
                return;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.Fail($"Run '{runId}' did not reach {expected} within {timeout}.");
    }

    private static async Task<SuspendedWorkerBarrier> SuspendNewWorkerAsync(
        int coordinatorProcessId,
        HashSet<int> excludedProcessIds,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            foreach (int processId in CaptureDirectChildProcessIds(coordinatorProcessId))
            {
                if (excludedProcessIds.Contains(processId))
                {
                    continue;
                }

                Process? process = null;
                try
                {
                    process = Process.GetProcessById(processId);
                    if (process.HasExited)
                    {
                        process.Dispose();
                        continue;
                    }

                    int status = NtSuspendProcess(process.Handle);
                    if (status != 0)
                    {
                        process.Dispose();
                        throw new InvalidOperationException(
                            $"The synthetic worker barrier could not suspend process {processId}; NTSTATUS=0x{status:X8}.");
                    }

                    return new SuspendedWorkerBarrier(process);
                }
                catch (ArgumentException)
                {
                    process?.Dispose();
                    // The short-lived child exited between snapshot and handle acquisition.
                }
                catch (InvalidOperationException) when (process is null || process.HasExited)
                {
                    process?.Dispose();
                    // The short-lived child exited between snapshot and suspension.
                }
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"No new worker child of coordinator {coordinatorProcessId} reached the synthetic suspension barrier within {timeout}.");
    }

    private static HashSet<int> CaptureDirectChildProcessIds(int parentProcessId)
    {
        using SafeFileHandle snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot.IsInvalid)
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "The synthetic worker process snapshot could not be created.");
        }

        HashSet<int> processIds = [];
        ProcessEntry32 entry = new()
        {
            Size = checked((uint)Marshal.SizeOf<ProcessEntry32>()),
        };
        if (!Process32FirstW(snapshot, ref entry))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorNoMoreFiles)
            {
                throw new System.ComponentModel.Win32Exception(
                    error,
                    "The synthetic worker process snapshot could not be enumerated.");
            }

            return processIds;
        }

        do
        {
            if (entry.ParentProcessId == checked((uint)parentProcessId))
            {
                processIds.Add(checked((int)entry.ProcessId));
            }

            entry.Size = checked((uint)Marshal.SizeOf<ProcessEntry32>());
        }
        while (Process32NextW(snapshot, ref entry));

        int finalError = Marshal.GetLastWin32Error();
        if (finalError != ErrorNoMoreFiles)
        {
            throw new System.ComponentModel.Win32Exception(
                finalError,
                "The synthetic worker process snapshot ended unexpectedly.");
        }

        return processIds;
    }

    private sealed class SuspendedWorkerBarrier(Process process) : IDisposable
    {
        private Process? process = process;

        public void Dispose()
        {
            Process? retained = Interlocked.Exchange(ref process, null);
            if (retained is null)
            {
                return;
            }

            try
            {
                if (!retained.HasExited)
                {
                    int status = NtResumeProcess(retained.Handle);
                    if (status != 0)
                    {
                        throw new InvalidOperationException(
                            $"The synthetic worker barrier could not resume process {retained.Id}; NTSTATUS=0x{status:X8}.");
                    }
                }
            }
            finally
            {
                retained.Dispose();
            }
        }
    }

    private const uint Th32csSnapProcess = 0x00000002;
    private const int ErrorNoMoreFiles = 18;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(
        uint flags,
        uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(
        SafeFileHandle snapshot,
        ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(
        SafeFileHandle snapshot,
        ref ProcessEntry32 entry);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(nint processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(nint processHandle);

    private static ApplicationHandshakeRequest ApplicationHandshake(RuntimeDescriptor descriptor)
    {
        ApplicationHandshakeRequest request = new()
        {
            SupportedProtocol = new ProtocolVersionRange
            {
                Major = ProtocolConstants.Major,
                MinimumMinor = ProtocolConstants.Minor,
                MaximumMinor = ProtocolConstants.Minor,
            },
            Compatibility = ProtocolConstants.Compatibility,
            ClientKind = ApplicationClientKind.TestHarness,
            CoordinatorInstanceNonce = ByteString.CopyFrom(descriptor.GetNonce()),
        };
        request.RequestedCapabilities.Add(Capability.ApplicationQuery);
        return request;
    }

    private static ProcessResult Run(
        string project,
        IReadOnlyList<string> arguments,
        int timeoutMilliseconds = 15_000) => TestProcessRunner.RunDotnetProject(
            $"src/{project}",
            arguments,
            timeoutMilliseconds,
            $"{project} did not terminate within its bound.");

    private static void StopProcess(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            process.Kill();
            Assert.IsTrue(
                process.WaitForExit(5_000),
                $"Coordinator process {processId} did not terminate.");
        }
        catch (ArgumentException)
        {
            // The coordinator already exited.
        }
    }

    private static void StopCoordinator(string root, int knownProcessId)
    {
        int processId = knownProcessId;
        if (processId <= 0)
        {
            try
            {
                processId = RuntimeDescriptor.Read(root).ProcessId;
            }
            catch (IOException)
            {
                // Startup failed before a readable descriptor was committed.
            }
        }

        StopProcess(processId);
    }

    private static void DeleteDirectoryAfterWorkerRelease(string root)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        Exception? lastFailure = null;
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException exception)
            {
                lastFailure = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastFailure = exception;
            }

            Thread.Sleep(100);
        }

        throw new IOException(
            $"The temporary integration root remained in use after {timeout.Elapsed}.",
            lastFailure);
    }

    private static void DowngradePreparedAnalysisAdmissionForMigrationEvidence(string root)
    {
        using StoragePaths paths = new(root);
        using SqliteConnection connection = new($"Data Source={paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = TargetedVerificationMigrationTestSupport.DropSchema17Sql +
            """
            DROP TABLE structured_export_projection;
            DROP TABLE structured_export_events;
            DROP TABLE finding_report_publications;
            DROP TABLE structured_exports;
            DROP TABLE targeted_verifications;
            DROP TABLE assumption_projection;
            DROP TABLE assumption_events;
            DROP TABLE review_projection;
            DROP TABLE review_events;
            DROP TABLE result_projection_items;
            DELETE FROM migration_history WHERE migration_id='results-review-workflow-0014';
            DELETE FROM migration_history WHERE migration_id='result-publication-and-export-deletion-0015';
            DROP TRIGGER prepared_run_submissions_append_only_update;
            DROP TRIGGER prepared_run_submissions_append_only_delete;
            DROP INDEX prepared_run_submissions_one_shot_gesture;
            ALTER TABLE prepared_run_submissions DROP COLUMN submission_fingerprint;
            CREATE TRIGGER prepared_run_submissions_append_only_update
            BEFORE UPDATE ON prepared_run_submissions
            BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
            CREATE TRIGGER prepared_run_submissions_append_only_delete
            BEFORE DELETE ON prepared_run_submissions
            BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
            DELETE FROM migration_history WHERE migration_id='prepared-analysis-admission-0013';
            UPDATE store_metadata SET value='12' WHERE key='schema_version';
            UPDATE store_metadata SET value='1.11.0' WHERE key='storage_contract_version';
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            PRAGMA user_version=12;
            """;
        command.Parameters.AddWithValue(
            "$fingerprint",
            ApplicationSetupPersistenceDeclarations.Schema12Fingerprint);
        command.ExecuteNonQuery();
    }

}
