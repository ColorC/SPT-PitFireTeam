param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [string]$ServerRoot,
    [string]$BigBrainAssemblyPath,
    [string]$DotnetPath = 'dotnet',
    [switch]$Verify
)
$ErrorActionPreference = 'Stop'
$GameRoot = [IO.Path]::GetFullPath($GameRoot)
if (!$ServerRoot) { $ServerRoot = Join-Path $GameRoot 'SPT_Runtime' }
if (!$BigBrainAssemblyPath) {
    $matches = @(Get-ChildItem -LiteralPath (Join-Path $GameRoot 'BepInEx/plugins') -Filter 'DrakiaXYZ-BigBrain.dll' -Recurse -File)
    if ($matches.Count -ne 1) { throw 'Expected exactly one compatible BigBrain DLL; pass -BigBrainAssemblyPath explicitly.' }
    $BigBrainAssemblyPath = $matches[0].FullName
}
Push-Location $PSScriptRoot
try {
    & $DotnetPath build client/pitFireTeam.csproj -c Release --nologo --disable-build-servers -m:1 "-p:ClientRuntimeRoot=$GameRoot" "-p:BigBrainAssemblyPath=$BigBrainAssemblyPath" -p:BuildSAINAddon=false -p:SkipSpt5Contracts=true
    if ($LASTEXITCODE) { throw 'Client build failed' }
    & $DotnetPath build server/pitFireTeam.Server.csproj -c Release --nologo --disable-build-servers -m:1 "-p:SptReferenceRoot=$ServerRoot"
    if ($LASTEXITCODE) { throw 'Server build failed' }
    if ($Verify) {
        & ./tests/Check-Spt5Build.ps1 -ModDll (Join-Path $PSScriptRoot 'client/bin/Release/net6.0/pitFireTeam.dll') -ClientRuntimeRoot $GameRoot -DotnetPath $DotnetPath
    }
    $stage = Join-Path $PSScriptRoot 'artifacts/pitFireTeam-spt5'
    if (Test-Path -LiteralPath $stage) { throw "Staging directory exists; move it before rebuilding: $stage" }
    $client = Join-Path $stage 'BepInEx/plugins/pitFireTeam'
    $server = Join-Path $stage 'SPT_Runtime/user/mods/pitFireTeam-ServerMod'
    New-Item -ItemType Directory -Path $client,$server -Force | Out-Null
    Copy-Item -LiteralPath client/bin/Release/net6.0/pitFireTeam.dll -Destination $client
    Copy-Item -LiteralPath resources -Destination $client -Recurse
    Copy-Item -LiteralPath server/bin/Release/net10.0/pitFireTeam.Server.dll,server/bin/Release/net10.0/pitFireTeam.Server.deps.json -Destination $server
    Copy-Item -LiteralPath server/Resources -Destination $server -Recurse
    Copy-Item -LiteralPath LICENSE,NOTICE.md -Destination $stage
    Compress-Archive -Path "$stage/*" -DestinationPath (Join-Path $PSScriptRoot 'artifacts/pitFireTeam-spt5.zip')
} finally { Pop-Location }
