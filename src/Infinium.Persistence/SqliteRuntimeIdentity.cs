using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

#pragma warning disable IDE0008 // Provider objects have unambiguous inferred types.

namespace Infinium.Persistence;

public sealed record SqliteBindingIdentity(
    string Version,
    string SourceId,
    string NativeSha256,
    IReadOnlyList<string> CompileOptions);

public static class SqliteRuntimeIdentity
{
    public const string RequiredVersion = "3.53.4";
    public const string RequiredSourceId =
        "2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc";
    public const string RequiredWinX64NativeSha256 =
        "6ad8e149f8ce3ed3716402b4b3a2268ebbdc7b64391b5fafed747e03bb1b9418";

    private static int initialized;

    public static SqliteBindingIdentity VerifyExactPatchedBinding(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        InitializeNativeProvider();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        string version;
        string sourceId;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT sqlite_version(), sqlite_source_id();";
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("SQLite did not report its runtime identity.");
            }

            version = reader.GetString(0);
            sourceId = reader.GetString(1);
        }

        List<string> compileOptions = [];
        using (var optionsCommand = connection.CreateCommand())
        {
            optionsCommand.CommandText =
                "SELECT compile_options FROM pragma_compile_options ORDER BY compile_options;";
            using var optionsReader = optionsCommand.ExecuteReader();
            while (optionsReader.Read())
            {
                compileOptions.Add(optionsReader.GetString(0));
            }
        }

        string nativeSha256 = HashLoadedNativeLibrary();
        SqliteBindingIdentity identity = new(version, sourceId, nativeSha256, compileOptions);
        if (!string.Equals(identity.Version, RequiredVersion, StringComparison.Ordinal)
            || !string.Equals(identity.SourceId, RequiredSourceId, StringComparison.Ordinal)
            || !string.Equals(
                identity.NativeSha256,
                RequiredWinX64NativeSha256,
                StringComparison.Ordinal)
            || !identity.CompileOptions.Contains("THREADSAFE=1", StringComparer.Ordinal)
            || !identity.CompileOptions.Contains("DEFAULT_WAL_SYNCHRONOUS=2", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported SQLite runtime '{identity.Version}' ({identity.SourceId}, "
                + $"{identity.NativeSha256}). Infinium requires the exact patched "
                + $"{RequiredVersion} win-x64 binding and compile contract.");
        }

        return identity;
    }

    public static void InitializeNativeProvider()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 0)
        {
            SQLitePCL.Batteries_V2.Init();
        }
    }

    private static string HashLoadedNativeLibrary()
    {
        using Process process = Process.GetCurrentProcess();
        string? path = process.Modules
            .Cast<ProcessModule>()
            .FirstOrDefault(module =>
                string.Equals(module.ModuleName, "e_sqlite3.dll", StringComparison.OrdinalIgnoreCase))
            ?.FileName;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("The loaded e_sqlite3 native module could not be identified.");
        }

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

#pragma warning restore IDE0008
