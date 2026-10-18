# Install and build the SPT 5 preview

## Compiled mod ZIP

Close game and server, and back up your saves and existing Pit FireTeam data. Extract the compiled ZIP into the game directory. It contains:

```
BepInEx/plugins/pitFireTeam/pitFireTeam.dll
BepInEx/plugins/pitFireTeam/resources/*
SPT_Runtime/user/mods/pitFireTeam-ServerMod/pitFireTeam.Server.dll
SPT_Runtime/user/mods/pitFireTeam-ServerMod/pitFireTeam.Server.deps.json
SPT_Runtime/user/mods/pitFireTeam-ServerMod/Resources/lang/*
SPT_Runtime/user/mods/pitFireTeam-ServerMod/Resources/avatars/*
```

If your backend is in another directory, move only the server mod folder to its `user/mods` directory. Preserve existing `Resources/teammates`, settings and saves. Do not install the optional SAIN addon DLL from a 4.x release. Keep exactly one BigBrain plugin assembly to avoid duplicate plugin GUIDs.

The game creates `BepInEx/config/xyz.pit.fireteam.cfg`. Open **My Squad** from the main menu. The accepted personal configuration uses F2 for manual status, health display enabled, and automatic enemy markers disabled. Existing user config is intentionally not overwritten by the public ZIP; set these options in My Squad/Settings or ConfigurationManager.

## Required compatibility stack

This exact tested target uses BigBrain 1.5 port source and three runtime compatibility files (BepInEx.Unity.IL2CPP, funchook and a field-boxing-corrected Il2CppInterop.Runtime). These are separate dependencies, not game DLLs or Pit FireTeam output. Their exact tested versions and SHA256 values are recorded in `compat/TESTED-RUNTIME.json`.

The mod-only ZIP does not contain those runtime binaries. `compat/README.md` provides source provenance and rebuild instructions. Apply these only to the matching runtime after backing it up; do not assume a 4.x BigBrain DLL or unmodified be.788 is equivalent. Follow the upstream Waypoints requirements for your SPT 5 distribution; no 4.x Waypoints binary is redistributed here.

## Source build

Pass `-GameRoot`, optional `-ServerRoot` (default `GameRoot/SPT_Runtime`), `-BigBrainAssemblyPath` and `-DotnetPath` to `Build-Spt5.ps1`. Required references come from your legally installed game, generated BepInEx interop and compatible SPT dependencies. Game assemblies are neither fetched nor shipped by this repository.

The release binaries are the exact locally played builds, not an automatic rebuild of documentation-only packaging changes. `RELEASE-MANIFEST.json` records their SHA256. Source C# was copied unchanged from the working tree used for those builds; project defaults were made portable. Different compiler versions and source paths can change binary hashes.
