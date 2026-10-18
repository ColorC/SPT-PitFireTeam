# Runtime compatibility source

The public mod ZIPs intentionally contain only the two mods' own binaries/resources. The exact tested runtime hashes are in [TESTED-RUNTIME.json](TESTED-RUNTIME.json). This folder publishes the dependency changes needed to reproduce that stack; matching the version number alone is insufficient.

## BigBrain 1.5 SPT 5 port (MIT)

`BigBrain/` contains the complete tracked source snapshot of the locally tested [DrakiaXYZ/SPT-BigBrain](https://github.com/DrakiaXYZ/SPT-BigBrain) port. Base upstream commit: `4d26e8b`; local port commit: `eab4c4a354a0757b891cbe1f51073800809f0b66`. The original MIT license is preserved. Project-path defaults were made portable.

```powershell
dotnet build ./compat/BigBrain/DrakiaXYZ-BigBrain.csproj -c Release -p:ClientRuntimeRoot='D:\Games\SPT5' -p:SkipSpt5Contracts=true
```

Install only its resulting `DrakiaXYZ-BigBrain.dll` in one plugin folder. Do not copy game/reference DLLs or leave a second copy elsewhere.

## BepInEx 6 be.788 Funchook ownership/relocation patch (LGPL-2.1)

Upstream: [BepInEx/BepInEx](https://github.com/BepInEx/BepInEx), base commit `5b766a3`. Apply `BepInEx-be788-funchook.patch` to that source before building `Runtimes/Unity/BepInEx.Unity.IL2CPP/BepInEx.Unity.IL2CPP.csproj`. It contains the complete two-file change from local commits `1aa97d9` and `67726c1`; the first fixes borrowed error-message ownership and the second adds relocation diagnostics. The LGPL-2.1 license is included. Follow the upstream project's reference-library/build setup. The tested local output identifies commit `67726c1c05bcdf88d16d4103ed53d09d6d4e789e`.

```powershell
git clone https://github.com/BepInEx/BepInEx.git
git -C BepInEx checkout 5b766a3
git -C BepInEx apply ../SPT-PitFireTeam/compat/BepInEx-be788-funchook.patch
# After satisfying upstream reference requirements:
dotnet build ./BepInEx/Runtimes/Unity/BepInEx.Unity.IL2CPP/BepInEx.Unity.IL2CPP.csproj -c Release -p:GeneratePackageOnBuild=false -p:BuildDir='./artifacts'
```

## Native funchook

The tested DLL was built without source modifications from [kubo/funchook v1.1.3](https://github.com/kubo/funchook/tree/v1.1.3), commit `88388db3c69e16c1560fee65c6857d75f5ce6fd5`. Clone with submodules and build with an x64 Visual C++ / CMake environment:

```powershell
git clone --recursive --branch v1.1.3 https://github.com/kubo/funchook.git
cmake -S funchook -B funchook/build -G Ninja -DCMAKE_BUILD_TYPE=Release -DFUNCHOOK_BUILD_TESTS=OFF -DFUNCHOOK_BUILD_STATIC=OFF -DFUNCHOOK_INSTALL=ON
cmake --build funchook/build --config Release
```

Use upstream's license for that dependency; no native funchook binary is distributed here.

## Il2CppInterop field boxing correction

The installed SPT distribution supplied Il2CppInterop.Runtime version `1.6.0+4efa0905c3b6ba19e21b0d97ce4f1e866ee3a028`. That commit could not be resolved in the public BepInEx/Il2CppInterop repository during publication, so this repository does not claim an upstream source build for that binary or redistribute it. The tested original input SHA256 was `f5ccffc08bdb580df99492c44b005dd7e129cfe688e7f57823353e6c52a485dc`.

`RuntimeFieldBoxingRepair.cs` is the complete local correction source, also built into the checked-in contract tool. It validates the IL shape and inserts the already-boxed flag after the first field boxing, preserving other control flow. Input/output must be different files. Build the verifier first, then patch a separate copy of the matching installed runtime:

```powershell
dotnet build ./tests/Spt5InteropContracts/Spt5InteropContracts.csproj -c Release -p:ClientRuntimeRoot='D:\Games\SPT5'
dotnet ./tests/Spt5InteropContracts/bin/Release/net10.0/Spt5InteropContracts.dll repair-runtime-field-boxing 'D:\Games\SPT5\BepInEx\core\Il2CppInterop.Runtime.dll' './Il2CppInterop.Runtime.repaired.dll'
```

Back up before replacing runtime files. A different input/runtime build needs separate review, not an assumed match. Runtime rebuilds can differ by compiler metadata; the SHA list identifies the binaries actually played, not a promise of bit-for-bit reproduction across toolchains.

## Separate experiment, not a compatibility requirement

The [owned strong-pointer cache source](../experimental/strong-pointer-cache/README.md) is an optional investigation with incomplete native lifecycle acceptance. It is not included in the normal mod ZIP or TESTED-RUNTIME compatibility baseline.

## 2026-10-19 local experiments

Updated [Woods evidence](../docs/PERFORMANCE-20261019.md) and source-only AI Limit non-raid / LiveMap BTR lookup tools are under `experimental/`. Local native acceptance is broader than the earlier short trial; this does not redefine the dependency baseline or bundle patched third-party/runtime DLLs.
