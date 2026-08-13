[CmdletBinding()]
param([string]$ManifestPath='docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.v1.json')
if($PSVersionTable.PSEdition-ne'Core'){& (Get-Command pwsh.exe).Source -NoProfile -File $PSCommandPath -ManifestPath $ManifestPath;exit $LASTEXITCODE}
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$path=[IO.Path]::GetFullPath((Join-Path $root $ManifestPath))
$schema=Join-Path $root 'contracts/repository/wp4-credential-native-recovery.v1.schema.json'
if(-not(Test-Json -LiteralPath $path -SchemaFile $schema)){throw 'Recovery manifest schema failed.'}
$bytes=[IO.File]::ReadAllBytes($path);$m=[Text.Encoding]::UTF8.GetString($bytes)|ConvertFrom-Json -Depth 100 -DateKind String
if($m.native_boundary.allowed_calls -join '|' -ne 'CredReadW|CredDeleteW|CredFree' -or $m.native_boundary.forbidden -notcontains 'CredWriteW'){throw 'Recovery native boundary differs.'}
if(@($m.disposable_namespace.targets).Count-ne 12){throw 'Recovery requires 12 targets.'}
foreach($t in $m.disposable_namespace.targets){$raw="Infinium:$($t.access_profile_id):$($t.generation_id)";$sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant();if($sha-ne$t.target_fingerprint_sha256){throw "Target fingerprint mismatch $($t.alias)"}}
if($m.binding.failure_record_commit-ne'fd6bd645f041502333d92b5e95c69bf8c69f2c83' -or $m.binding.consumed_lock_sha256-ne'05bf7fc259bf90d367c20f9ba23af3d1525aa2514ee6e1888304cbaf44b364c4'){throw 'Failure binding differs.'}
[pscustomobject]@{status=if($m.status-eq'draft-binding-pending'){'draft'}else{'ready'};manifest_id=$m.manifest_id;manifest_sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant();execution_authorized=$false;native_operations=0;network_operations=0;provider_operations=0}|ConvertTo-Json
