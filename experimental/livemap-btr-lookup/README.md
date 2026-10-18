# livemap-btr-lookup — exact-input local compatibility experiment

Replace only the original `EntityService.CollectBtr` global `FindObjectsOfType<BTRVehicle>` query with a private helper reading the registered native `BtrController.Instance.BtrVehicle`. Preserve Unity destroyed-object and activeInHierarchy filtering, the existing retry cadence, map marker/position handling and every other existing method. A missing/late vehicle is checked again without a global scene scan. No settings or map resolution are reduced.

The helper's actual 28 IL instructions were extracted and exercised with substituted native boundaries: 8/8 checks passed, including later appearance/activation and inactive-object exclusion. This is separate from native gameplay evidence. The draft missing the inactive filter is not the final candidate. See the dated performance report for actual measurements and remaining limits.

This directory contains our patch-tool/fixture source under this repository's license, not the third-party mod, game references or patched DLL. Obtain dependencies under their own terms. The tool refuses other input hashes, requires fresh output/report paths and preserves the input. LiveMap also pins the exact matching EFT interop hash. Review `verification.json` and [native measurements](../../docs/PERFORMANCE-20261019.md) before choosing to use it.

Build with .NET 10: `dotnet build tool/Tool.csproj -c Release -p:ClientRuntimeRoot=<your-matching-client-root>`. Inspect `Program.cs` for its positional CLI; use separate fresh output paths. These are optional experiments outside the normal mod ZIP and cannot be applied to arbitrary dependency versions.
