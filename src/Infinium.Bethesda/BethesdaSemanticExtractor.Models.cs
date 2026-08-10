using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Headers;
using Mutagen.Bethesda.Plugins.Binary.Streams;
using Mutagen.Bethesda.Plugins.Binary.Translations;
using Mutagen.Bethesda.Plugins.Meta;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace Infinium.Bethesda;

#pragma warning disable CA1859 // Contract-facing collection abstractions are intentional.


public sealed partial class BethesdaSemanticExtractor
{
    private sealed record AuthorizedPlugin(PluginState Plugin, LooseProvider Winner, string Path);

    private sealed record ValidatedInput(Mo2InstallationSnapshot Snapshot, IReadOnlyList<AuthorizedPlugin> OrderedPlugins);

    private sealed record SealedPlugin(
        BethesdaPluginReceipt Receipt,
        ModKey ModKey,
        byte[] Bytes,
        BethesdaMasterStyle MasterStyle);

    private sealed record BethesdaUnsupportedRecord(
        string Signature,
        string FormKey,
        string SourcePlugin);

    private sealed record BethesdaUnsupportedField(
        string SourcePlugin,
        string FormKey,
        string RecordSignature,
        string FieldSignature);

    private sealed record BethesdaUnsupportedShape(
        string SourcePlugin,
        string FormKey,
        string RecordSignature,
        string FieldSignature);

    private sealed record PluginStructuralObservation(
        IReadOnlyDictionary<string, Dictionary<string, int>> Fields,
        IReadOnlyList<BethesdaUnsupportedField> UnsupportedFields,
        IReadOnlyList<BethesdaUnsupportedShape> UnsupportedShapes)
    {
        public bool HasField(string formKey, string field) =>
            Fields.TryGetValue(formKey, out Dictionary<string, int>? fields)
            && fields.ContainsKey(field);

        public bool HasSupportedField(string formKey, string field) =>
            HasField(formKey, field)
            && !UnsupportedShapes.Any(shape =>
                string.Equals(shape.FormKey, formKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(shape.FieldSignature, field, StringComparison.Ordinal));
    }

    private sealed class BethesdaInputException(
        BethesdaExtractionState state,
        string code,
        string input,
        string message) : Exception(message)
    {
        public BethesdaExtractionState State { get; } = state;
        public string Code { get; } = code;
        public string Input { get; } = input;
    }
}
