[CmdletBinding()]
param([string] $OutputPath = 'artifacts/m1-slice6/wp3/accepted-wp2-upgrade.json')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$acceptedCommit = 'ed27ed04897103d93a60e6200971ca12d04f2e11'
$acceptedFingerprint = '240a06fe2a9fa3d79db63985fbda329c8e83822534b93cbfb539062a109cad9e'
$rejectedWp3Commit = '7130ddc1d5b163adc05d9b0d06d5066341cfcfa9'
$rejectedWp3Fingerprint = '554129523ac64ce52ee4d24e90644dbaa167c0d98602f1c2d0f25ad271ec0581'
$currentFingerprint = '85c0ed0d1ee466c9a62d33c2a5ce6da8f28b2fc788603deffaa364683d5966fd'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('Infinium-Wp3-Upgrade-' + [guid]::NewGuid().ToString('N'))
$resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
if (-not $resolvedTemp.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The WP3 upgrade verifier temporary root escaped the system temporary directory.'
}

function New-PrivateDirectory([string] $Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetAccessRuleProtection($true, $false)
    $security.SetOwner($identity)
    $inheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
    $rule = [Security.AccessControl.FileSystemAccessRule]::new(
        $identity,
        [Security.AccessControl.FileSystemRights]::FullControl,
        $inheritance,
        [Security.AccessControl.PropagationFlags]::None,
        [Security.AccessControl.AccessControlType]::Allow)
    $security.AddAccessRule($rule)
    $system = [Security.Principal.SecurityIdentifier]::new(
        [Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
    $security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        $system,
        [Security.AccessControl.FileSystemRights]::FullControl,
        $inheritance,
        [Security.AccessControl.PropagationFlags]::None,
        [Security.AccessControl.AccessControlType]::Allow))
    [IO.Directory]::SetAccessControl($Path, $security)
}

try {
    New-Item -ItemType Directory -Path $resolvedTemp | Out-Null
    $archive = Join-Path $resolvedTemp 'accepted-wp2.zip'
    $source = Join-Path $resolvedTemp 'accepted-wp2'
    & git -C $repoRoot archive --format=zip --output=$archive $acceptedCommit
    if ($LASTEXITCODE -ne 0) { throw 'The exact accepted WP2 source archive could not be materialized.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $source
    $offlineConfig = Join-Path $resolvedTemp 'NuGet.Offline.Config'
    [IO.File]::WriteAllText($offlineConfig,
        '<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear /></packageSources></configuration>',
        [Text.UTF8Encoding]::new($false))
    $packages = Join-Path $repoRoot '.packages'

    $acceptedRunner = Join-Path $source 'wp3-upgrade-source-runner'
    New-Item -ItemType Directory -Path $acceptedRunner | Out-Null
    [IO.File]::WriteAllText((Join-Path $acceptedRunner 'Runner.csproj'), @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><RestorePackagesPath>$packages</RestorePackagesPath></PropertyGroup>
  <ItemGroup><ProjectReference Include="..\src\Infinium.Persistence\Infinium.Persistence.csproj" /></ItemGroup>
</Project>
"@, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $acceptedRunner 'Program.cs'), @'
using Infinium.Persistence;
Directory.CreateDirectory(args[0]);
using AuthoritativeStore store = new(new StoragePaths(args[0]));
'@, [Text.UTF8Encoding]::new($false))
    & dotnet restore (Join-Path $acceptedRunner 'Runner.csproj') --configfile $offlineConfig --packages $packages --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The exact accepted WP2 database runner did not restore from local packages.' }
    $sourceStore = Join-Path $resolvedTemp 'source-store'
    New-PrivateDirectory $sourceStore
    & dotnet run --project (Join-Path $acceptedRunner 'Runner.csproj') -c Release --no-restore -- $sourceStore
    if ($LASTEXITCODE -ne 0) { throw 'The exact accepted WP2 binary did not create its schema-6 store.' }

    $rejectedArchive = Join-Path $resolvedTemp 'rejected-wp3.zip'
    $rejectedSource = Join-Path $resolvedTemp 'rejected-wp3'
    & git -C $repoRoot archive --format=zip --output=$rejectedArchive $rejectedWp3Commit
    if ($LASTEXITCODE -ne 0) { throw 'The exact rejected WP3 source archive could not be materialized.' }
    Expand-Archive -LiteralPath $rejectedArchive -DestinationPath $rejectedSource
    $rejectedRunner = Join-Path $rejectedSource 'wp3-correction-source-runner'
    New-Item -ItemType Directory -Path $rejectedRunner | Out-Null
    [IO.File]::WriteAllText((Join-Path $rejectedRunner 'Runner.csproj'), @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><RestorePackagesPath>$packages</RestorePackagesPath></PropertyGroup>
  <ItemGroup><ProjectReference Include="..\src\Infinium.Persistence\Infinium.Persistence.csproj" /></ItemGroup>
</Project>
"@, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $rejectedRunner 'Program.cs'), @'
using Infinium.Persistence;
Directory.CreateDirectory(args[0]);
using AuthoritativeStore store = new(new StoragePaths(args[0]));
'@, [Text.UTF8Encoding]::new($false))
    & dotnet restore (Join-Path $rejectedRunner 'Runner.csproj') --configfile $offlineConfig --packages $packages --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The exact rejected WP3 database runner did not restore from local packages.' }
    $rejectedSourceStore = Join-Path $resolvedTemp 'rejected-source-store'
    New-PrivateDirectory $rejectedSourceStore
    & dotnet run --project (Join-Path $rejectedRunner 'Runner.csproj') -c Release --no-restore -- $rejectedSourceStore
    if ($LASTEXITCODE -ne 0) { throw 'The exact rejected WP3 binary did not create its schema-6 store.' }

    $currentRunner = Join-Path $resolvedTemp 'current-runner'
    New-Item -ItemType Directory -Path $currentRunner | Out-Null
    $persistenceProject = Join-Path $repoRoot 'src/Infinium.Persistence/Infinium.Persistence.csproj'
    [IO.File]::WriteAllText((Join-Path $currentRunner 'Runner.csproj'), @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><RestorePackagesPath>$packages</RestorePackagesPath></PropertyGroup>
  <ItemGroup><ProjectReference Include="$persistenceProject" /></ItemGroup>
</Project>
"@, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $currentRunner 'Program.cs'), @'
using System;
using System.IO;
using System.Text.Json;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

static string Metadata(string root, string key)
{
    using SqliteConnection connection = new($"Data Source={Database(root)};Mode=ReadOnly;Pooling=False");
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT value FROM store_metadata WHERE key=$key;";
    command.Parameters.AddWithValue("$key", key);
    return (string)command.ExecuteScalar()!;
}
static void CopyStore(string source, string target)
{
    StoragePaths targetPaths = new(target);
    targetPaths.Create();
    File.Copy(Database(source), targetPaths.Database);
}
static string Database(string root) => Path.Combine(root, "data", "infinium.sqlite3");

string source = args[0]; string upgradedRoot = args[1]; string freshRoot = args[2];
string restoredRoot = args[3]; string unknownRoot = args[4]; string rejectedSource = args[5];
string correctedRoot = args[6]; string output = args[7];
string sourceFingerprint = Metadata(source, "schema_fingerprint");
CopyStore(source, upgradedRoot);
BackupArtifact backup;
using (AuthoritativeStore upgraded = new(new StoragePaths(upgradedRoot)))
    backup = upgraded.CreateBackup("Wp3AcceptedWp2Upgrade", new DateTimeOffset(2026, 8, 11, 18, 0, 0, TimeSpan.Zero));
string finalFingerprint = Metadata(upgradedRoot, "schema_fingerprint");
string extension = Metadata(upgradedRoot, "wp3_schema_extension_id");
string correction = Metadata(upgradedRoot, "wp3_schema_correction_id");
using (AuthoritativeStore fresh = new(new StoragePaths(freshRoot))) { }
string freshFingerprint = Metadata(freshRoot, "schema_fingerprint");
AuthoritativeStore.RestoreBackup(backup, new StoragePaths(restoredRoot));
string restoredFingerprint = Metadata(restoredRoot, "schema_fingerprint");
CopyStore(source, unknownRoot);
using (SqliteConnection unknown = new($"Data Source={Database(unknownRoot)};Pooling=False"))
{
    unknown.Open(); using SqliteCommand mutation = unknown.CreateCommand();
    mutation.CommandText = "CREATE TABLE unauthorized_wp3_shape(value TEXT) STRICT;"; mutation.ExecuteNonQuery();
}
bool unknownRefused;
try { using AuthoritativeStore rejected = new(new StoragePaths(unknownRoot)); unknownRefused = false; }
catch (InvalidOperationException) { unknownRefused = true; }
string correctionSourceFingerprint = Metadata(rejectedSource, "schema_fingerprint");
CopyStore(rejectedSource, correctedRoot);
using (AuthoritativeStore corrected = new(new StoragePaths(correctedRoot))) { }
string correctedFingerprint = Metadata(correctedRoot, "schema_fingerprint");
string correctedExtension = Metadata(correctedRoot, "wp3_schema_extension_id");
string correctedMigration = Metadata(correctedRoot, "wp3_schema_correction_id");
if (sourceFingerprint != "240a06fe2a9fa3d79db63985fbda329c8e83822534b93cbfb539062a109cad9e"
    || finalFingerprint != ProviderPersistenceDeclarations.SchemaFingerprint
    || freshFingerprint != finalFingerprint || restoredFingerprint != finalFingerprint
    || extension != ProviderPersistenceDeclarations.Wp3ExtensionMigrationId
    || correction != ProviderPersistenceDeclarations.Wp3CorrectionMigrationId
    || correctionSourceFingerprint != ProviderPersistenceDeclarations.Wp3CorrectionSourceSchemaFingerprint
    || correctedFingerprint != finalFingerprint
    || correctedExtension != ProviderPersistenceDeclarations.Wp3ExtensionMigrationId
    || correctedMigration != ProviderPersistenceDeclarations.Wp3CorrectionMigrationId || !unknownRefused)
    throw new InvalidOperationException("Accepted-WP2 -> WP3 schema convergence or refusal failed.");
byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
{
    schema = "infinium.wp3.accepted-wp2-upgrade-evidence/v1",
    accepted_wp2_commit = "ed27ed04897103d93a60e6200971ca12d04f2e11",
    source_schema_fingerprint = sourceFingerprint,
    extension_id = extension,
    correction_id = correction,
    correction_source_commit = "7130ddc1d5b163adc05d9b0d06d5066341cfcfa9",
    correction_source_schema_fingerprint = correctionSourceFingerprint,
    correction_upgrade_converged = correctedFingerprint == finalFingerprint,
    final_schema_fingerprint = finalFingerprint,
    fresh_upgrade_converged = freshFingerprint == finalFingerprint,
    backup_restore_converged = restoredFingerprint == finalFingerprint,
    unknown_same_version_refused = unknownRefused,
    network_operations = 0,
    native_credential_operations = 0,
});
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllBytes(output, [.. json, (byte)10]);
'@, [Text.UTF8Encoding]::new($false))
    & dotnet restore (Join-Path $currentRunner 'Runner.csproj') --configfile $offlineConfig --packages $packages --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The current WP3 upgrade runner did not restore from local packages.' }
    $output = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
    foreach ($privateRoot in @('upgraded','fresh','unknown','corrected')) {
        New-PrivateDirectory (Join-Path $resolvedTemp $privateRoot)
    }
    & dotnet run --project (Join-Path $currentRunner 'Runner.csproj') -c Release --no-restore -- $sourceStore `
        (Join-Path $resolvedTemp 'upgraded') (Join-Path $resolvedTemp 'fresh') `
        (Join-Path $resolvedTemp 'restored') (Join-Path $resolvedTemp 'unknown') `
        $rejectedSourceStore (Join-Path $resolvedTemp 'corrected') $output
    if ($LASTEXITCODE -ne 0) { throw 'The current WP3 accepted-WP2 upgrade regression failed.' }
    $evidence = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    $invalidEvidence = $evidence.accepted_wp2_commit -ne $acceptedCommit
    $invalidEvidence = $invalidEvidence -or $evidence.source_schema_fingerprint -ne $acceptedFingerprint
    $invalidEvidence = $invalidEvidence -or $evidence.final_schema_fingerprint -ne $currentFingerprint
    $invalidEvidence = $invalidEvidence -or $evidence.correction_source_commit -ne $rejectedWp3Commit
    $invalidEvidence = $invalidEvidence -or $evidence.correction_source_schema_fingerprint -ne $rejectedWp3Fingerprint
    $invalidEvidence = $invalidEvidence -or -not $evidence.correction_upgrade_converged
    $invalidEvidence = $invalidEvidence -or -not $evidence.fresh_upgrade_converged
    $invalidEvidence = $invalidEvidence -or -not $evidence.backup_restore_converged
    $invalidEvidence = $invalidEvidence -or -not $evidence.unknown_same_version_refused
    $invalidEvidence = $invalidEvidence -or [int]$evidence.network_operations -ne 0
    $invalidEvidence = $invalidEvidence -or [int]$evidence.native_credential_operations -ne 0
    if ($invalidEvidence) {
        throw 'The exact accepted-WP2 upgrade evidence is incomplete or inconsistent.'
    }
}
finally {
    if (Test-Path -LiteralPath $resolvedTemp) { Remove-Item -LiteralPath $resolvedTemp -Recurse -Force }
}
