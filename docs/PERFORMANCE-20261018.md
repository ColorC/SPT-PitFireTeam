# Early-raid frame-time investigation — 2026-10-18

The original recording already contains visible pauses; continuous video timestamps rule out a missing-frame timestamp gap as their explanation. No synthetic smoothing was applied to the published demo. ESP's visually accepted DLL was kept unchanged during this investigation.

## Core changes shipped in preview.2

Empty native mover-target nullable returns are handled without constructing a wrapper around null. A separate core readiness fast path avoids redundant work when no command/override exists and reads native Memory/ProfileId only once per decision. It preserves expiry cleanup, temporary-override maintenance and the SAIN callback path. Independent checks compiled the actual RequestLayer source and exact extracted follower methods with external-runtime stubs: 38/38 passed. The native required contract scope passed 189/189. These checks do not substitute for native gameplay acceptance.

The shipped client was exercised with the ordinary field-boxing-corrected runtime: four followers joined and fought, and extraction, settlement and main-menu return were observed. No repeat of the full combined UI/stash matrix is claimed. The fixes alone are not claimed to eliminate every early-raid hitch.

## Experimental runtime work — excluded from the mod ZIP

Lightweight CPU sampling showed main-thread native handle lookups and finalizer handle frees sharing a native lock. A heavier trace substantially disturbed the workload, so it was excluded as a quantitative explanation of the original pauses. A Manual/incremental-GC trial did not reliably help and was not selected as normal policy.

A separate exact-input runtime patch caches only owned strong pointers for positively identified non-injected native classes. Unknown/injected/weak/uncached paths retain the original getter, and the finalizer clears the cache before freeing the handle. It changes neither normal automatic GC policy nor native API implementations. [Source and fixture instructions](../experimental/strong-pointer-cache/README.md) are available for review; no runtime DLL is distributed.

For one conservative comparison, the first clean 25-second PresentMon window from each separate cold start is shown below. Both used the same preview.2 client, normal automatic GC and four teammates. Neither window overlapped the CPU trace. These are short independent starts, not randomized identical workloads.

| Runtime | Presentations | Median | p99 | Maximum | Frames >80 ms | Frames >150 ms |
|---|---:|---:|---:|---:|---:|---:|
| Ordinary compatibility runtime | 1,340 | 17.32 ms | 48.79 ms | 185.34 ms | 10 | 4 |
| Experimental pointer cache | 1,483 | 16.42 ms | 26.24 ms | 72.23 ms | 0 | 0 |

Managed Gen0 collections continued in the candidate window. Additional short candidate windows also showed no frames above 80 ms, but these observations are not a long-session or universal performance guarantee.

Offline verification executed the patched methods on .NET 6.0.7: 22/22 fixtures passed, plus four negative patch-tool gates. Synthetic native boundaries test managed behavior, not native lifetime safety. After the measured candidate windows the process was found to have exited before planned status-report, extraction and UI checks; the exit reason was not verified. **Candidate stability, teardown, UI and extraction acceptance remain open.** Testing stopped at the user's request. The ordinary personal installation runtime was not replaced by this experiment.

Input runtime SHA256: `082799b08208703637a38bc17b0dd551e5ce3c79e6e9d33e3d9db199402f7462`. Experimental native-API-preserving candidate SHA256: `6694b45f4518de1edf0597ad3bbfe7ff0a9f01cb3903f282ff2517186f7e6bd0`. These identify local evidence, not a recommendation to replace other runtime builds.
