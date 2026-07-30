using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Infinium.Mo2;

public interface IExecutableAdmissionService
{
    public ExecutableAdmission AdmitMo2(string path);

    public ExecutableAdmission AdmitSkyrimGamePlugin(string path);

    public ExecutableAdmission AdmitSkyrim(string path, RuntimeTargetContext context);
}

public sealed class SupportedExecutableManifests : IExecutableAdmissionService
{
    public const string Mo2ManifestId = "infinium.mo2-2.5.2-local-research/v1";
    public const string SkyrimGamePluginManifestId =
        "infinium.mo2-game-skyrimse-2.5.2-local-research/v1";
    public const string SkyrimManifestId = "infinium.skyrimse-1.6.1170-steam/v1";
    public const string AdapterId = "infinium.mo2-static-reconstruction/v3";
    public static IReadOnlySet<string> QualifiedMapperSha256s { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public const string SupportedMo2Sha256 =
        "442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622";
    public const string SupportedSkyrimGamePluginSha256 =
        "5EAACE8EC5E3F1E6DC6E85FFE22ABDD30C99DFA414807E2D7E2EF242CC90A429";
    public const string SupportedSkyrimSha256 =
        "C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9";

    private const long SupportedSkyrimGamePluginLength = 440_320;
    private const long SupportedSkyrimLength = 37_157_144;

    public ExecutableAdmission AdmitMo2(string path)
    {
        return Admit(
            path,
            "ModOrganizer.exe",
            SupportedMo2Sha256,
            expectedLength: null,
            maximumLength: 512 * 1024 * 1024,
            expectedVersion: "2.5.2",
            requirePeShape: false,
            Mo2ManifestId);
    }

    public ExecutableAdmission AdmitSkyrimGamePlugin(string path)
    {
        return Admit(
            path,
            "game_skyrimse.dll",
            SupportedSkyrimGamePluginSha256,
            SupportedSkyrimGamePluginLength,
            SupportedSkyrimGamePluginLength,
            expectedVersion: null,
            requirePeShape: false,
            SkyrimGamePluginManifestId);
    }

    public ExecutableAdmission AdmitSkyrim(
        string path,
        RuntimeTargetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!OperatingSystem.IsWindows()
            || !string.Equals(context.Platform, "windows-x64", StringComparison.Ordinal)
            || !string.Equals(context.DistributionChannel, "steam", StringComparison.Ordinal)
            || !string.Equals(context.ApplicationId, "489830", StringComparison.Ordinal))
        {
            return new ExecutableAdmission(
                AdmissionState.Unsupported,
                SkyrimManifestId,
                null,
                ["runtime platform, distribution channel, or application ID is unsupported"]);
        }

        return Admit(
            path,
            "SkyrimSE.exe",
            SupportedSkyrimSha256,
            SupportedSkyrimLength,
            SupportedSkyrimLength,
            "1.6.1170.0",
            requirePeShape: true,
            SkyrimManifestId);
    }

    private static ExecutableAdmission Admit(
        string path,
        string expectedFileName,
        string expectedSha256,
        long? expectedLength,
        long maximumLength,
        string? expectedVersion,
        bool requirePeShape,
        string manifestId)
    {
        List<string> reasons = [];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return new ExecutableAdmission(
                AdmissionState.Indeterminate,
                manifestId,
                null,
                ["executable path is not an absolute path"]);
        }

        try
        {
            FileInfo info = new(path);
            if (!info.Exists)
            {
                return new ExecutableAdmission(
                    AdmissionState.Indeterminate,
                    manifestId,
                    null,
                    ["executable is missing"]);
            }

            ExecutableIdentity observed;
            using (FileStream stream = OpenStableRead(path))
            {
                if (stream.Length > maximumLength)
                {
                    return new ExecutableAdmission(
                        AdmissionState.Inconsistent,
                        manifestId,
                        null,
                        ["executable exceeds the admitted byte-length bound"]);
                }

                string sha256 = Convert.ToHexString(SHA256.HashData(stream));
                (ushort? machine, ushort? magic, ushort? subsystem) = ReadPeShape(stream);
                string? version = FileVersionInfo.GetVersionInfo(path).FileVersion;
                WindowsObjectIdentity objectIdentity =
                    WindowsReadOnlyObjectIdentity.Read(stream.SafeFileHandle);
                observed = new ExecutableIdentity(
                    info.Name,
                    stream.Length,
                    sha256,
                    version,
                    machine,
                    magic,
                    subsystem,
                    objectIdentity.CanonicalValue);
            }

            if (requirePeShape
                && (observed.PeMachine is null
                    || observed.PeOptionalHeaderMagic is null
                    || observed.PeSubsystem is null))
            {
                return new ExecutableAdmission(
                    AdmissionState.Indeterminate,
                    manifestId,
                    observed,
                    ["executable PE headers are malformed or truncated"]);
            }

            if (!string.Equals(observed.FileName, expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("unexpected executable file name");
            }

            if (expectedLength is not null && observed.ByteLength != expectedLength.Value)
            {
                reasons.Add("unexpected executable byte length");
            }

            if (expectedVersion is not null
                && !string.Equals(
                    observed.ProductVersion,
                    expectedVersion,
                    StringComparison.Ordinal))
            {
                reasons.Add("unexpected executable file version");
            }

            if (requirePeShape
                && (observed.PeMachine != 0x8664
                    || observed.PeOptionalHeaderMagic != 0x020b
                    || observed.PeSubsystem != 2))
            {
                reasons.Add("unexpected PE32+ AMD64 GUI shape");
            }

            if (reasons.Count > 0)
            {
                return new ExecutableAdmission(
                    AdmissionState.Inconsistent,
                    manifestId,
                    observed,
                    reasons);
            }

            if (!string.Equals(observed.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new ExecutableAdmission(
                    AdmissionState.Unrecognized,
                    manifestId,
                    observed,
                    ["identity fields match, but the exact executable hash is not admitted"]);
            }

            return new ExecutableAdmission(AdmissionState.Accepted, manifestId, observed, []);
        }
        catch (IOException exception)
        {
            return new ExecutableAdmission(
                AdmissionState.Indeterminate,
                manifestId,
                null,
                [$"executable could not be read: {exception.GetType().Name}"]);
        }
        catch (UnauthorizedAccessException)
        {
            return new ExecutableAdmission(
                AdmissionState.Indeterminate,
                manifestId,
                null,
                ["executable access was denied"]);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return new ExecutableAdmission(
                AdmissionState.Indeterminate,
                manifestId,
                null,
                [$"executable identity could not be read: {exception.NativeErrorCode}"]);
        }
    }

    internal static FileStream OpenStableRead(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
    }

    private static (ushort? Machine, ushort? Magic, ushort? Subsystem) ReadPeShape(
        FileStream stream)
    {
        if (stream.Length < 0x40)
        {
            return (null, null, null);
        }

        Span<byte> header = stackalloc byte[0x40];
        stream.Position = 0;
        stream.ReadExactly(header);
        if (header[0] != (byte)'M' || header[1] != (byte)'Z')
        {
            return (null, null, null);
        }

        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(header[0x3c..]);
        if (peOffset < 0 || peOffset > stream.Length - 96)
        {
            return (null, null, null);
        }

        Span<byte> pe = stackalloc byte[96];
        stream.Position = peOffset;
        stream.ReadExactly(pe);
        if (!pe[..4].SequenceEqual("PE\0\0"u8))
        {
            return (null, null, null);
        }

        ushort machine = BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]);
        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[24..]);
        ushort subsystem = BinaryPrimitives.ReadUInt16LittleEndian(pe[92..]);
        return (machine, magic, subsystem);
    }
}
