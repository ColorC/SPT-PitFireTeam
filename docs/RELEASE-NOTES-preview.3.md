# SPT 5 preview.3 — 2026-10-19

Restore optional AI Limit follower compatibility on the actual IL2CPP loader: use its installed lowercase GUID `com.dvize.ailimit` and declare a soft dependency so the limiter loads first. The deliberately empty legacy Mono compatibility dictionary is unchanged; optional SAIN/Orbit paths are not enabled by this change. Server, resources and the accepted ESP DLL are unchanged.

Woods performance follow-up isolated two issues: native handle lookup contention under managed collections, and LiveMap's repeated global scene search for an absent BTR. Exact-input source patch tools and measurements are provided separately. **Neither a runtime nor AI Limit nor LiveMap DLL is included in this mod ZIP. Installing this ZIP alone is not claimed to remove those dependency-level stalls.** Normal automatic Unity GC remains enabled.

The exact packaged client SHA256 is `e08390260d7659f8399405bd3f05cdf312fedb4c062746a37c648a3319801e45`. Required native contracts passed 189/189; the broad scan still has three unresolved optional SAIN targets. The local combination completed Woods, extraction, settlement, main-menu return and a separate 100-second hideout lifecycle check. The runtime candidate also completed two consecutive raids and model/weapon/statistics smoke checks. See [measurements, scope and residual limits](PERFORMANCE-20261019.md). This does not claim universal stability or completion of the older full inventory/UI matrix.

The [single existing annotated demo](https://github.com/ColorC/SPT-PitFireTeam/releases/download/v0.10.3-spt5-preview.1/spt5-pitfireteam-wedge-demo-annotated-720p30.mp4) stays in preview.1. It shows the earlier Wedge/Interchange scenario, not this performance test.
