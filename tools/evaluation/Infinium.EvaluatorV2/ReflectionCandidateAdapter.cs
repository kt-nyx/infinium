using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinium.EvaluatorV2;

internal static class ReflectionCandidateAdapter
{
    private static readonly JsonSerializerOptions ForeignJsonOptions = CreateForeignJsonOptions();
    private static readonly HashSet<string> FrameworkAssemblies = CreateFrameworkAssemblySet();

    internal static CandidateSemanticOutput Execute(ExecutionManifest manifest)
    {
        string assemblyPath = Path.GetFullPath(manifest.Candidate.AssemblyPath);
        CandidateLoadContext context = new(manifest.Candidate);
        try
        {
            Assembly bethesda = context.LoadFromAssemblyPath(assemblyPath);
            Assembly domain = context.LoadFromAssemblyName(new AssemblyName("Infinium.Domain"));
            Assembly mo2 = context.LoadFromAssemblyName(new AssemblyName("Infinium.Mo2"));
            object request = BuildRequest(manifest, bethesda, domain, mo2);
            Type extractorType = RequiredType(bethesda, "Infinium.Bethesda.BethesdaSemanticExtractor");
            object extractor = Activator.CreateInstance(extractorType)
                ?? throw new InvalidDataException("The candidate extractor could not be constructed.");
            MethodInfo extract = extractorType.GetMethod("Extract", [request.GetType(), typeof(CancellationToken)])
                ?? throw new InvalidDataException("The candidate extractor does not expose the required public adapter boundary.");
            object result;
            try
            {
                result = extract.Invoke(extractor, [request, CancellationToken.None])
                    ?? throw new CandidateOutputException("The candidate returned a null semantic result.");
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw new InvalidDataException("Candidate execution threw an exception.", exception.InnerException);
            }

            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(result, result.GetType(), ForeignJsonOptions));
            JsonElement root = document.RootElement;
            string state = root.GetProperty("state").GetString()
                ?? throw new CandidateOutputException("Candidate state is absent.");
            List<SemanticFact> facts = SemanticCanonicalizer.Flatten(root)
                .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
                .ToList();
            CandidateSemanticOutput output = new(
                EvaluatorProtocol.CandidateSchema,
                EvaluatorProtocol.ProtocolId,
                manifest.Candidate.Commit,
                manifest.Candidate.Artifact,
                state,
                facts);

            string serialized = EvaluatorProtocol.Serialize(output);
            using JsonDocument strict = JsonDocument.Parse(serialized);
            Infinium.Application.Evaluation.EmbeddedJsonSchemaValidator.Validate(
                strict.RootElement,
                "candidate-semantic-output.v1.schema.json");
            return output;
        }
        catch (CandidateOutputException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CandidateOutputException("The candidate result was not valid semantic JSON.", exception);
        }
        finally
        {
            context.Unload();
        }
    }

    private static object BuildRequest(
        ExecutionManifest manifest,
        Assembly bethesda,
        Assembly domain,
        Assembly mo2)
    {
        Type opaqueIdType = RequiredType(domain, "Infinium.Domain.Contracts.OpaqueId");
        Type versionType = RequiredType(domain, "Infinium.Domain.Contracts.ContractVersion");
        Type fingerprintType = RequiredType(domain, "Infinium.Domain.Contracts.Sha256Fingerprint");
        Type timestampType = RequiredType(domain, "Infinium.Domain.Contracts.UtcTimestamp");
        Type assuranceType = RequiredType(domain, "Infinium.Domain.Contracts.SnapshotPopulationAssurance");
        Type contractType = RequiredType(domain, "Infinium.Domain.Contracts.InstallationSnapshotContract");
        Type executableType = RequiredType(mo2, "Infinium.Mo2.ExecutableIdentity");
        Type admissionType = RequiredType(mo2, "Infinium.Mo2.ExecutableAdmission");
        Type targetType = RequiredType(mo2, "Infinium.Mo2.RuntimeTargetContext");
        Type dependencyType = RequiredType(mo2, "Infinium.Mo2.Mo2SnapshotDependencyManifest");
        Type pluginStateType = RequiredType(mo2, "Infinium.Mo2.PluginState");
        Type entityType = RequiredType(mo2, "Infinium.Mo2.LocalInstalledEntity");
        Type sourceHintType = RequiredType(mo2, "Infinium.Mo2.LocalSourceHint");
        Type providerType = RequiredType(mo2, "Infinium.Mo2.LooseProvider");
        Type chainType = RequiredType(mo2, "Infinium.Mo2.LooseProviderChain");
        Type modStateType = RequiredType(mo2, "Infinium.Mo2.ModState");
        Type inventoryType = RequiredType(mo2, "Infinium.Mo2.PhysicalInventoryEntry");
        Type gapType = RequiredType(mo2, "Infinium.Mo2.SnapshotGap");
        Type snapshotType = RequiredType(mo2, "Infinium.Mo2.Mo2InstallationSnapshot");
        Type captureType = RequiredType(mo2, "Infinium.Mo2.Mo2SnapshotCaptureResult");

        string material = string.Join('|', manifest.Execution.Plugins
            .OrderBy(plugin => plugin.LoadOrder)
            .Select(plugin => $"{plugin.PluginName}|{plugin.LoadOrder}|{plugin.LocalInstalledEntityId}|{plugin.Sha256}"));
        string structuralHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        object structural = New(fingerprintType, structuralHash);
        object version = New(versionType, 3u, 0u, 0u);
        object timestamp = New(timestampType, new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero));
        object snapshotId = New(opaqueIdType, $"evaluation-{structuralHash[..24]}");
        object instanceId = New(opaqueIdType, "evaluation-instance");
        object profileId = New(opaqueIdType, "evaluation-profile");
        object[] entityIds = manifest.Execution.Plugins
            .OrderBy(plugin => plugin.LoadOrder)
            .Select(plugin => New(opaqueIdType, plugin.LocalInstalledEntityId))
            .ToArray();
        object contract = New(
            contractType,
            snapshotId,
            version,
            instanceId,
            profileId,
            structural,
            ArrayOf(assuranceType, []),
            ArrayOf(opaqueIdType, entityIds),
            timestamp);

        const string zeroHash = "0000000000000000000000000000000000000000000000000000000000000000";
        object executable = New(executableType, "evaluator.exe", 1L, zeroHash, null, null, null, null, "evaluator-object");
        object admission = New(
            admissionType,
            EnumValue(mo2, "Infinium.Mo2.AdmissionState", "Accepted"),
            "evaluator-admission",
            executable,
            Array.Empty<string>());
        object target = New(targetType, "windows-x64", "steam", "489830");
        object dependencies = New(
            dependencyType,
            version,
            structural,
            "infinium.mo2-static-reconstruction/v3",
            "mod-organizer-2",
            "evaluation-profile",
            target,
            executable,
            executable,
            executable,
            Array.Empty<string>(),
            Array.Empty<string>(),
            EmptyArray(mo2, "Infinium.Mo2.SnapshotControlObservation"),
            EmptyArray(mo2, "Infinium.Mo2.SnapshotRootObservation"),
            EmptyArray(mo2, "Infinium.Mo2.SnapshotStructuralObservation"),
            EmptyArray(mo2, "Infinium.Mo2.SnapshotMappingDependency"));

        List<object> pluginStates = [];
        List<object> entities = [];
        List<object> chains = [];
        foreach (PluginExecutionInput plugin in manifest.Execution.Plugins.OrderBy(plugin => plugin.LoadOrder))
        {
            object entityId = New(opaqueIdType, plugin.LocalInstalledEntityId);
            pluginStates.Add(New(
                pluginStateType,
                plugin.PluginName,
                EnumValue(mo2, "Infinium.Mo2.PluginEnablementState", "EnabledByProfile"),
                EnumValue(mo2, "Infinium.Mo2.PluginClassification", "Regular"),
                (int?)plugin.LoadOrder,
                entityId,
                "correlated"));
            entities.Add(New(
                entityType,
                entityId,
                Path.GetDirectoryName(Path.GetFullPath(plugin.Path))!,
                EnumValue(mo2, "Infinium.Mo2.LooseProviderKind", "RegularMod"),
                structural,
                ArrayOf(sourceHintType, [])));
            object provider = New(
                providerType,
                entityId,
                EnumValue(mo2, "Infinium.Mo2.LooseProviderKind", "RegularMod"),
                Path.GetFullPath(plugin.Path),
                plugin.LoadOrder);
            chains.Add(New(chainType, plugin.PluginName, ArrayOf(providerType, [provider]), provider));
        }

        string firstRoot = Path.GetDirectoryName(Path.GetFullPath(manifest.Execution.Plugins[0].Path))!;
        object snapshot = New(
            snapshotType,
            contract,
            "infinium.mo2-static-reconstruction/v3",
            firstRoot,
            firstRoot,
            "evaluation-profile",
            admission,
            admission,
            admission,
            dependencies,
            ArrayOf(modStateType, []),
            ArrayOf(pluginStateType, pluginStates),
            ArrayOf(entityType, entities),
            ArrayOf(chainType, chains),
            ArrayOf(inventoryType, []),
            Array.Empty<string>(),
            ArrayOf(gapType, []),
            false,
            false);
        object capture = New(
            captureType,
            EnumValue(mo2, "Infinium.Mo2.SnapshotCaptureState", "Completed"),
            snapshot,
            ArrayOf(gapType, []));

        Type unsupportedType = RequiredType(bethesda, "Infinium.Bethesda.BethesdaUnsupportedCapability");
        object[] unsupported = manifest.Execution.UnsupportedCapabilities.Select(value => Enum.Parse(
            unsupportedType,
            value switch
            {
                "archive_member_read" => "ArchiveMemberRead",
                "localized_string_resolution" => "LocalizedStringResolution",
                "automatic_environment_discovery" => "AutomaticEnvironmentDiscovery",
                _ => throw new InvalidDataException($"Unknown unsupported capability '{value}'."),
            })).ToArray();
        Type requestType = RequiredType(bethesda, "Infinium.Bethesda.BethesdaSemanticRequest");
        return New(requestType, capture, ArrayOf(unsupportedType, unsupported));
    }

    private static JsonSerializerOptions CreateForeignJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }

    private static HashSet<string> CreateFrameworkAssemblySet()
    {
        string trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        string runtimeRoot = Path.GetFullPath(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        return trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => Path.GetFullPath(path).StartsWith(runtimeRoot, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static object New(Type type, params object?[] arguments) =>
        Activator.CreateInstance(type, arguments)
        ?? throw new InvalidDataException($"Could not construct '{type.FullName}'.");

    private static Array ArrayOf(Type elementType, IEnumerable<object> values)
    {
        object[] source = values.ToArray();
        Array result = Array.CreateInstance(elementType, source.Length);
        for (int index = 0; index < source.Length; index++)
        {
            result.SetValue(source[index], index);
        }

        return result;
    }

    private static Array EmptyArray(Assembly assembly, string typeName) =>
        Array.CreateInstance(RequiredType(assembly, typeName), 0);

    private static Type RequiredType(Assembly assembly, string name) =>
        assembly.GetType(name, throwOnError: true, ignoreCase: false)!;

    private static object EnumValue(Assembly assembly, string typeName, string value) =>
        Enum.Parse(RequiredType(assembly, typeName), value);

    private sealed class CandidateLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, string> declaredAssemblies;

        internal CandidateLoadContext(CandidateIdentity candidate)
            : base(isCollectible: true)
        {
            string root = Path.GetFullPath(candidate.Root);
            declaredAssemblies = candidate.Files
                .Where(file => string.Equals(Path.GetExtension(file.RelativePath), ".dll", StringComparison.OrdinalIgnoreCase)
                               && string.IsNullOrEmpty(Path.GetDirectoryName(file.RelativePath)))
                .ToDictionary(
                    file => Path.GetFileNameWithoutExtension(file.RelativePath),
                    file => Path.GetFullPath(Path.Combine(root, file.RelativePath)),
                    StringComparer.OrdinalIgnoreCase);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null
                && declaredAssemblies.TryGetValue(assemblyName.Name, out string? path))
            {
                return LoadFromAssemblyPath(path);
            }

            if (assemblyName.Name is not null && FrameworkAssemblies.Contains(assemblyName.Name))
            {
                return null;
            }

            throw new FileNotFoundException(
                $"Candidate dependency '{assemblyName.Name}' is absent from the retained inventory.");
        }
    }
}
