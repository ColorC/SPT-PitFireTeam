# Validation scope — 2026-10-18 preview

## Observed

- Private runtime: squad members joined raids and fought; manual F2 status with health was visually confirmed by the tester.
- A recorded Wedge/Interchange scenario was playable. The accompanying video is a demonstration, not a complete automated acceptance run.
- Earlier native UI batch completed 29 operations with no new logged errors; appearance/head-selection and repeated settings flows also received visual checks.
- The preview.1 client and preview.2 maintenance build passed its required 189/189 contract scope. This is a required-scope result, not complete coverage of every dynamic native target.
- Latest server compiled successfully; independent HTTP checks covered persistence, catalog/profile filtering and rejected invalid equipment policies without profile/item/currency mutations.
- ESP alignment and performance were visually accepted by the tester and its released DLL is frozen.

## preview.2 maintenance update

- Nullable mover-target and core readiness changes are documented in [release notes](RELEASE-NOTES-preview.2.md). The ordinary-runtime cold raid completed extraction/settlement/main-menu return; independent readiness checks passed 38/38.
- The publication-time verifier reports `Required scope: PASS (189/189)`. Its broad `Overall` result remains false because three optional SAIN target contracts are unresolved (`FollowerSainProficiency`, `FollowerSainAimTargetPatch`, `SAINPatch`); these are outside the core required scope and are not claimed validated.
- Opt-in QA observation and bounded GC experiment helpers do not change normal automatic GC policy.
- The [strong-pointer runtime experiment](../experimental/strong-pointer-cache/README.md) is source-only, excluded from the mod package, and has incomplete native lifecycle/UI acceptance. Its later process exit reason is unknown. Short performance improvements are not a stability acceptance.

## Still open / do not infer a pass

- The complete native gear-to-stash, save/reopen, stash-to-gear round trip and purchase/mail/claim/reuse round trip have not completed the final combined acceptance run.
- A repeatable multi-raid extraction/re-entry matrix and every teammate's native Elite/mastery screen have not all been captured in one final run.
- Normal personal installation: a first startup exited with a coreclr access violation. A second startup reached the Wedge/PvE UI, but final main-menu confirmation was interrupted by the user. Cause of the first crash remains unresolved; no claim of startup reliability or full personal-install acceptance is made.
- Existing normal-client plugins produced KmyTarkovConfiguration generic-injection and legacy EFTVersion dependency errors. Their relationship to the startup failure is not established.
- Native preview code can emit an `Invalid Layer Index 11` warning; no corresponding visual defect was observed in the reviewed screenshots. It is not marked fixed.
- Optional SAIN addon, Fika, other game builds and arbitrary mod combinations are unvalidated.

Please report the exact EFT/SPT/BepInEx/dependency versions, reproduction steps and a redacted relevant log excerpt. Do not upload whole saves, credentials or session tokens.

## 2026-10-19 scoped follow-up

See [preview.3](RELEASE-NOTES-preview.3.md) and [Woods measurements](PERFORMANCE-20261019.md). Required contracts passed 189/189. Native consecutive raid/menu and preview smoke checks now cover the runtime candidate; full client-stack Woods and hideout checks were added. Earlier UI/inventory matrix gaps and optional SAIN failures are not silently closed. Dependency experiments remain exact-input, source-only and excluded from the mod ZIP.
