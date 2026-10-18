# Owned strong-pointer cache — experimental source only

This is an opt-in research tool, separate from the normal mod release and compatibility stack. Native lifecycle, teardown and long-session acceptance are incomplete. The later test-process exit is unexplained. See [measured results and limits](../../docs/PERFORMANCE-20261018.md).

The patch accepts only the exact field-boxing-corrected Il2CppInterop.Runtime input SHA256 `082799b08208703637a38bc17b0dd551e5ce3c79e6e9d33e3d9db199402f7462`. Its eligibility depends on the reviewed Unity 2022.3 non-moving IL2CPP collector and owned-handle layout. No other build is supported. It audits handle writers, strong creation, release sites, fallback instructions and public/protected ABI. A failed native-class classification retains the original getter. `WasCollected`, weak APIs and the object pool are unchanged. The finalizer cache clear precedes the original free.

Only patch/fixture source is included, under the repository MIT license. The external Il2CppInterop dependency has its own upstream licensing and provenance described in [compat](../../compat/README.md); no dependency, game or patched runtime DLL is redistributed. The tested patch-tool source is byte-identical; only project reference paths were made portable. Its generated report contains the caller's local paths and should be redacted before sharing.

## Build and generate a separate candidate

Use .NET SDK 10, .NET 6 targeting support and references from the matching installed runtime. From this experiment directory, set `$gameRoot` to your installed reference game:

```powershell
$gameRoot = 'D:\Games\SPT5'
$experimentRoot = (Get-Location).Path
dotnet build ./tool/StrongPointerPatch.csproj -c Release "-p:ClientRuntimeRoot=$gameRoot"
$patchTool = Join-Path $experimentRoot 'tool/bin/Release/net10.0/StrongPointerPatch.dll'
dotnet $patchTool patch "$gameRoot/BepInEx/core/Il2CppInterop.Runtime.dll" "$experimentRoot/candidate.dll" "$experimentRoot/patch-report.json"
```

The input must already have the exact field-boxing repair; an unmodified or different build is rejected. Outputs must be new files inside this experiment directory. The tool does not replace the installed input. The locally generated native-API-preserving candidate hash was `6694b45f4518de1edf0597ad3bbfe7ff0a9f01cb3903f282ff2517186f7e6bd0`.

## Offline fixtures — never deploy their DLL

```powershell
dotnet build ./bridge/NativeFixtureBridge.csproj -c Release
dotnet $patchTool fixture "$experimentRoot/candidate.dll" "$experimentRoot/bridge/bin/Release/net6.0/NativeFixtureBridge.dll" "$experimentRoot/fixture/Il2CppInterop.Runtime.dll" "$experimentRoot/fixture/report.json"
dotnet build ./checks/StrongPointerChecks.csproj -c Release "-p:ClientRuntimeRoot=$gameRoot"
dotnet ./checks/bin/Release/net6.0/StrongPointerChecks.dll
```

The fixture command replaces native boundaries in a **separate test-only assembly**. Never install or distribute anything from `fixture/` or `checks/bin/`. The checks require .NET 6; the recorded run used 6.0.7. See [VERIFICATION.json](VERIFICATION.json) for 22 passing case names and four negative patch-tool gate results. No native collector lifetime or arbitrary concurrent manual-finalization safety is inferred from these fixtures. No runtime binary is included in the mod ZIP or this source snapshot.
