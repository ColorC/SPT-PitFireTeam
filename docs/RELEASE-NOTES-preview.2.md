# SPT 5 preview.2 — 2026-10-18

This is a core-client maintenance prerelease for EFT 1.1.5.47242 IL2CPP / SPT 5.0.0 BEM ec15a4. Server binaries, resources and the accepted ESP binary are unchanged from preview.1.

- Correct empty native `BotMover.TargetPoint` nullable returns in movement, combat and battle-recorder paths, preserving each existing fallback.
- Skip unnecessary core follower readiness scans when no command or temporary aggression override is active. Command expiry/cleanup and the SAIN branch retain their ordering. Reuse native Memory/ProfileId reads within the same decision; no cross-frame enemy cache is introduced.
- Add a strictly opt-in private frame sampler with a bounded ring buffer and background file writer. Periodic legacy JSON memory capture now needs an additional opt-in.
- Keep the bounded incremental-GC investigation behind private QA guards. It was not adopted as normal gameplay policy; automatic GC remains the default.

The separate strong-pointer-cache runtime experiment is **source-only and opt-in**. It is not included in this ZIP or required by preview.2. Short measurements improved, but the later client exit was not explained and lifecycle/UI acceptance was not completed. See [performance investigation](PERFORMANCE-20261018.md) and [experimental source](../experimental/strong-pointer-cache/README.md).

The distributed client is the exact locally exercised build, SHA256 `94d2767c6be982191752283f898812a0d08899b5d367f10464510b441d411fc7`. The required native contract scope passed 189/189; an independent source-based readiness check passed 38/38. A cold raid using the ordinary compatibility runtime completed squad combat, instant extraction, settlement and main-menu return. This does not close the earlier combined UI/stash/multi-raid acceptance gaps.

The existing [single annotated gameplay demo](https://github.com/ColorC/SPT-PitFireTeam/releases/download/v0.10.3-spt5-preview.1/spt5-pitfireteam-wedge-demo-annotated-720p30.mp4) remains hosted in preview.1. It demonstrates the earlier Wedge/Interchange scenario, not a preview.2 benchmark.
