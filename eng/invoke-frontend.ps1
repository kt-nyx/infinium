param(
    [ValidateSet('Restore', 'RestoreOffline', 'Generate', 'CheckGenerated', 'BuildDesktop', 'CheckDesktop', 'TypeCheck', 'Lint', 'Test', 'All')]
    [string]$Task = 'All'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$toolProject = Join-Path $repositoryRoot 'eng/tooling/Infinium.FrontendToolchain/Infinium.FrontendToolchain.csproj'
$node = Join-Path $repositoryRoot '.packages/node.js.redist.win/24.14.1/tools/x64/node.exe'
$tsc = Join-Path $repositoryRoot '.packages/microsoft.typescript.msbuild/5.9.3/tools/tsc/tsc.js'
$tsconfig = Join-Path $repositoryRoot 'src/Infinium.Frontend/tsconfig.json'
$desktopTsconfig = Join-Path $repositoryRoot 'src/Infinium.Frontend/tsconfig.desktop.json'

function Restore-FrontendToolchain {
    & dotnet restore $toolProject --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The locked frontend toolchain restore failed.' }
}

function Restore-FrontendToolchainOffline {
    & dotnet restore $toolProject --locked-mode --force-evaluate --nologo --configfile (Join-Path $repositoryRoot 'eng/NuGet.frontend-offline.config')
    if ($LASTEXITCODE -ne 0) { throw 'The cached locked frontend toolchain restore failed with network sources disabled.' }
}

function Assert-FrontendToolchain {
    if (-not (Test-Path -LiteralPath $node) -or -not (Test-Path -LiteralPath $tsc)) {
        throw 'The repository-owned frontend toolchain is absent. Run this script with -Task Restore first.'
    }
    $nodeVersion = (& $node --version).Trim()
    if ($nodeVersion -ne 'v24.14.1') { throw "Unexpected Node version: $nodeVersion" }
    $typescriptVersion = (& $node $tsc --version).Trim()
    if ($typescriptVersion -ne 'Version 5.9.3') { throw "Unexpected TypeScript version: $typescriptVersion" }
}

function Generate-FrontendContracts {
    Assert-FrontendToolchain
    & $node (Join-Path $repositoryRoot 'eng/generate-renderer-contracts.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'Renderer contract generation failed.' }
}

function Check-GeneratedContracts {
    Assert-FrontendToolchain
    & $node (Join-Path $repositoryRoot 'eng/generate-renderer-contracts.mjs') --check
    if ($LASTEXITCODE -ne 0) { throw 'Generated renderer contracts were stale.' }
}

function Invoke-TypeCheck {
    Assert-FrontendToolchain
    & $node $tsc --project $tsconfig --noEmit
    if ($LASTEXITCODE -ne 0) { throw 'Strict TypeScript compilation failed.' }
    & $node $tsc --project $desktopTsconfig --noEmit
    if ($LASTEXITCODE -ne 0) { throw 'Strict desktop renderer TypeScript compilation failed.' }
}

function Build-DesktopAssets {
    Assert-FrontendToolchain
    $output = Join-Path $repositoryRoot 'artifacts/desktop-assets-compiled'
    & $node $tsc --project $desktopTsconfig --outDir $output
    if ($LASTEXITCODE -ne 0) { throw 'Desktop renderer compilation failed.' }
    & $node (Join-Path $repositoryRoot 'eng/generate-desktop-assets.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'Desktop asset packaging failed.' }
}

function Check-DesktopAssets {
    Assert-FrontendToolchain
    $output = Join-Path $repositoryRoot 'artifacts/desktop-assets-compiled'
    & $node $tsc --project $desktopTsconfig --outDir $output
    if ($LASTEXITCODE -ne 0) { throw 'Desktop renderer compilation failed.' }
    & $node (Join-Path $repositoryRoot 'eng/generate-desktop-assets.mjs') --check
    if ($LASTEXITCODE -ne 0) { throw 'Packaged desktop assets are stale.' }
}

function Invoke-Lint {
    Assert-FrontendToolchain
    & $node (Join-Path $repositoryRoot 'eng/lint-frontend.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'Frontend source policy lint failed.' }
}

function Invoke-Tests {
    Assert-FrontendToolchain
    $output = Join-Path $repositoryRoot 'artifacts/frontend-tests'
    & $node $tsc --project $tsconfig --outDir $output
    if ($LASTEXITCODE -ne 0) { throw 'Frontend test compilation failed.' }
    & $node (Join-Path $output 'tests.js')
    if ($LASTEXITCODE -ne 0) { throw 'Frontend unit tests failed.' }
}

Push-Location $repositoryRoot
try {
    switch ($Task) {
        'Restore' { Restore-FrontendToolchain }
        'RestoreOffline' { Restore-FrontendToolchainOffline }
        'Generate' { Generate-FrontendContracts }
        'CheckGenerated' { Check-GeneratedContracts }
        'BuildDesktop' { Build-DesktopAssets }
        'CheckDesktop' { Check-DesktopAssets }
        'TypeCheck' { Invoke-TypeCheck }
        'Lint' { Invoke-Lint }
        'Test' { Invoke-Tests }
        'All' {
            Restore-FrontendToolchain
            Check-GeneratedContracts
            Check-DesktopAssets
            Invoke-TypeCheck
            Invoke-Lint
            Invoke-Tests
        }
    }
}
finally { Pop-Location }
