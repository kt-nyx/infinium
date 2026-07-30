[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BeforePath,
    [Parameter(Mandatory)][string]$AfterPath,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$before = Get-Content -LiteralPath $BeforePath -Raw | ConvertFrom-Json -Depth 100
$after = Get-Content -LiteralPath $AfterPath -Raw | ConvertFrom-Json -Depth 100

$comparisons = foreach ($beforeRoot in $before.roots) {
    $afterRoot = @($after.roots | Where-Object { $_.root_token -eq $beforeRoot.root_token })
    if ($afterRoot.Count -ne 1) {
        [ordered]@{
            root_token = $beforeRoot.root_token
            unchanged = $false
            reason = 'missing-or-ambiguous-after-root'
        }
        continue
    }

    $bContent = @($beforeRoot.scoped_content | ForEach-Object {
        "$($_.relative_path)|$($_.byte_length)|$($_.sha256)"
    })
    $aContent = @($afterRoot[0].scoped_content | ForEach-Object {
        "$($_.relative_path)|$($_.byte_length)|$($_.sha256)"
    })

    $unchanged = (
        $beforeRoot.file_count -eq $afterRoot[0].file_count -and
        $beforeRoot.total_bytes -eq $afterRoot[0].total_bytes -and
        $beforeRoot.structural_sha256 -eq $afterRoot[0].structural_sha256 -and
        (($bContent -join "`n") -ceq ($aContent -join "`n"))
    )

    [ordered]@{
        root_token = $beforeRoot.root_token
        unchanged = $unchanged
        before_file_count = $beforeRoot.file_count
        after_file_count = $afterRoot[0].file_count
        before_total_bytes = $beforeRoot.total_bytes
        after_total_bytes = $afterRoot[0].total_bytes
        before_structural_sha256 = $beforeRoot.structural_sha256
        after_structural_sha256 = $afterRoot[0].structural_sha256
        scoped_content_unchanged = (($bContent -join "`n") -ceq ($aContent -join "`n"))
    }
}

$result = [ordered]@{
    schema_id = 'infinium.eval.protected-root-comparison'
    schema_version = 1
    compared_at = [DateTimeOffset]::UtcNow.ToString('O')
    all_unchanged = (@($comparisons | Where-Object { -not $_.unchanged }).Count -eq 0)
    comparisons = @($comparisons)
}

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
[IO.File]::WriteAllText(
    $OutputPath,
    (($result | ConvertTo-Json -Depth 20) + "`n"),
    [Text.UTF8Encoding]::new($false)
)

if (-not $result.all_unchanged) {
    Write-Error "One or more protected roots changed. See '$OutputPath'."
}

Write-Output $OutputPath
