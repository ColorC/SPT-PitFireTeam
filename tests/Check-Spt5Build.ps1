param(
    [Parameter(Mandatory=$true)][string]$ModDll,
    [Parameter(Mandatory=$true)][string]$ClientRuntimeRoot,
    [string]$ModKind = 'pitFireTeam',
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Spt5InteropContracts/Spt5InteropContracts.csproj'
& $DotnetPath build $project -c Release --nologo --disable-build-servers -m:1 "-p:ClientRuntimeRoot=$ClientRuntimeRoot"
if ($LASTEXITCODE) { throw 'Verifier build failed' }
$tool = Join-Path $PSScriptRoot 'Spt5InteropContracts/bin/Release/net10.0/Spt5InteropContracts.dll'
$fixtures = Join-Path $PSScriptRoot 'Spt5InteropContracts/fixtures'
$reportRoot = Join-Path $PSScriptRoot '../artifacts'
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
& $DotnetPath $tool audit --mod $ModDll --game-root (Join-Path $ClientRuntimeRoot 'BepInEx/interop') --runtime-root (Join-Path $ClientRuntimeRoot 'BepInEx') --native-exe (Join-Path $ClientRuntimeRoot 'EscapeFromTarkov.exe') --build-id '1.1.5.47242' --expected-native-version '1.1.5.47242' --expected-framework net6.0 --framework-root (Join-Path $ClientRuntimeRoot 'dotnet') --contract (Join-Path $fixtures 'patch-contracts-valid.json') --required-patches (Join-Path $fixtures 'required-patches.json') --fail-scope-only --report (Join-Path $reportRoot 'contracts.json')
if ($LASTEXITCODE) { throw 'Required native contract scope failed; inspect artifacts/contracts.json' }
