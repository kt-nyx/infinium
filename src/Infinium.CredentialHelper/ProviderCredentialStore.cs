using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Infinium.CredentialHelper;

public sealed record ProviderCredentialReference(
    string ProfileId,
    string GenerationId,
    string AccountIdentityId,
    string ProjectIdentityId)
{
    public string TargetName => $"Infinium:{ProfileId}:{GenerationId}";

    public void Validate()
    {
        static bool ValidIdentity(string value) => value.Length is > 0 and <= 120
            && value.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
        if (!ValidIdentity(ProfileId)
            || !ValidIdentity(GenerationId)
            || !ValidIdentity(AccountIdentityId)
            || !ValidIdentity(ProjectIdentityId))
        {
            throw new InvalidDataException(
                "Credential profile, generation, account, and project identities must be finite non-secret identifiers.");
        }
    }

    internal string CanonicalMetadata()
    {
        Validate();
        return JsonSerializer.Serialize(new
        {
            provider = "openai",
            account_identity_id = AccountIdentityId,
            project_identity_id = ProjectIdentityId,
        });
    }

    public string TargetFingerprintSha256()
    {
        Validate();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(TargetName)));
    }
}

public sealed class ProviderCredentialLease : IDisposable
{
    private byte[]? secret;

    internal ProviderCredentialLease(byte[] value)
    {
        secret = value;
    }

    public ReadOnlyMemory<byte> Secret =>
        secret ?? throw new ObjectDisposedException(nameof(ProviderCredentialLease));

    public void Dispose()
    {
        if (secret is null)
        {
            return;
        }
        CryptographicOperations.ZeroMemory(secret);
        secret = null;
    }
}

/// <summary>
/// Exact-target Windows Credential Manager access. This type never enumerates
/// credentials and never accepts fallback profile or generation identities.
/// </summary>
public static class ProviderCredentialStore
{
    public const int MaximumSecretBytes = 2_560;
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public static bool ExistsExact(ProviderCredentialReference reference)
    {
        reference.Validate();
        if (!CredReadW(reference.TargetName, CredentialTypeGeneric, 0, out SafeCredentialBuffer? buffer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return false;
            }
            throw new System.ComponentModel.Win32Exception(
                error,
                "Windows Credential Manager could not read the exact provider target.");
        }
        using (buffer)
        {
            ValidateMetadata(reference, buffer);
            return true;
        }
    }

    public static ProviderCredentialLease ReadExact(ProviderCredentialReference reference)
    {
        reference.Validate();
        if (!CredReadW(reference.TargetName, CredentialTypeGeneric, 0, out SafeCredentialBuffer? buffer))
        {
            int error = Marshal.GetLastWin32Error();
            throw error == ErrorNotFound
                ? new KeyNotFoundException("The exact provider credential generation is not enrolled.")
                : new System.ComponentModel.Win32Exception(
                    error,
                    "Windows Credential Manager could not read the exact provider target.");
        }
        using (buffer)
        {
            NativeCredential credential = buffer.Read();
            ValidateMetadata(reference, credential);
            if (credential.CredentialBlobSize is 0 or > MaximumSecretBytes
                || credential.CredentialBlob == 0)
            {
                throw new InvalidDataException(
                    "The exact provider credential contains an invalid secret length.");
            }
            byte[] secret = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, secret, 0, secret.Length);
            return new ProviderCredentialLease(secret);
        }
    }

    public static void WriteNew(ProviderCredentialReference reference, ReadOnlySpan<byte> secret)
    {
        reference.Validate();
        if (secret.Length is 0 or > MaximumSecretBytes)
        {
            throw new InvalidDataException(
                "The provider credential secret is empty or exceeds the Windows Credential Manager bound.");
        }
        if (ExistsExact(reference))
        {
            throw new InvalidOperationException(
                "The exact provider credential generation already exists; overwriting is prohibited.");
        }

        byte[] copy = secret.ToArray();
        try
        {
            unsafe
            {
                fixed (byte* pointer = copy)
                {
                    NativeCredential credential = new()
                    {
                        Type = CredentialTypeGeneric,
                        TargetName = reference.TargetName,
                        CredentialBlobSize = checked((uint)copy.Length),
                        CredentialBlob = (nint)pointer,
                        Persist = CredentialPersistLocalMachine,
                        UserName = reference.CanonicalMetadata(),
                    };
                    if (!CredWriteW(ref credential, 0))
                    {
                        throw new System.ComponentModel.Win32Exception(
                            Marshal.GetLastWin32Error(),
                            "Windows Credential Manager could not write the exact provider generation.");
                    }
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(copy);
        }
    }

    public static bool DeleteExact(ProviderCredentialReference reference)
    {
        reference.Validate();
        if (CredDeleteW(reference.TargetName, CredentialTypeGeneric, 0))
        {
            return true;
        }
        int error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
        {
            return false;
        }
        throw new System.ComponentModel.Win32Exception(
            error,
            "Windows Credential Manager could not delete the exact provider generation.");
    }

    private static void ValidateMetadata(
        ProviderCredentialReference reference,
        SafeCredentialBuffer buffer) =>
        ValidateMetadata(reference, buffer.Read());

    private static void ValidateMetadata(
        ProviderCredentialReference reference,
        NativeCredential credential)
    {
        if (!string.Equals(credential.TargetName, reference.TargetName, StringComparison.Ordinal)
            || !string.Equals(
                credential.UserName,
                reference.CanonicalMetadata(),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The credential target exists, but its non-secret account/project metadata does not match the selected profile.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        internal uint Flags;
        internal uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] internal string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint CredentialBlobSize;
        internal nint CredentialBlob;
        internal uint Persist;
        internal uint AttributeCount;
        internal nint Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] internal string UserName;
    }

    private sealed class SafeCredentialBuffer : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeCredentialBuffer() : base(ownsHandle: true)
        {
        }

        internal NativeCredential Read() =>
            Marshal.PtrToStructure<NativeCredential>(handle);

        protected override bool ReleaseHandle()
        {
            CredFree(handle);
            return true;
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(
        string target,
        uint type,
        uint flags,
        out SafeCredentialBuffer credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern void CredFree(nint buffer);
}
