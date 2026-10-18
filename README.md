# Pit FireTeam — experimental SPT 5.0.0 community port

这是 [pitAlex/Pit FireTeam](https://github.com/pitAlex/SPT-PitFireTeam) 的社区移植分支。当前版本已能实际带队进入战局游玩，但仍有未完成的验收与兼容性细节，按 **Pre-release** 发布。原作者及上游仍为 pitAlex。

This fork provides a playable **experimental SPT 5.0.0 port** of Pit FireTeam 0.10.3. It is not an official upstream release or a claim of complete feature parity.

- [Download compiled prereleases](https://github.com/ColorC/SPT-PitFireTeam/releases)
- [Gameplay demo](https://github.com/ColorC/SPT-PitFireTeam/releases/download/v0.10.3-spt5-preview.1/spt5-pitfireteam-wedge-demo-annotated-720p30.mp4)
- [Build and install](docs/SPT5-PREVIEW.md)
- [Validation and known limitations](docs/VALIDATION.md)

## Target

Tested target: EFT **1.1.5.47242 IL2CPP**, SPTushonka **5.0.0 BEM ec15a4**, BepInEx 6 be.788 with the compatibility requirements documented below, and the SPT 5 BigBrain port. Other game/server builds require separate verification. The optional SAIN addon is excluded from the release and is not validated for SPT 5. Fika is not supported.

## Included work

- IL2CPP and native ABI/task/nullable compatibility work for the core follower path.
- Squad UI, profile statistics, skills, clothing/appearance and weapon preview repairs.
- Loadout save and real stash transfer policy repairs; see the outstanding native round-trip checks before relying on this with valuable saves.
- Manual F2 status reports including teammate health, plus follower follow/combat integration.
- Scoped optional scenario hooks; personal Wedge profiles and loadout databases are not distributed.

Back up saves and mod-owned teammate data before installing. See the target-specific runtime requirements in [SPT5-PREVIEW.md](docs/SPT5-PREVIEW.md); this DLL alone does not upgrade a stock 4.x dependency stack to 5.0.

## Build

Install .NET SDK 10 (with .NET 6 targeting support), start the compatible game once to generate BepInEx interop assemblies, then run from this repository:

```powershell
./Build-Spt5.ps1 -GameRoot 'D:\Games\SPT5' -DotnetPath dotnet
```

The script accepts `-ServerRoot` and `-BigBrainAssemblyPath` overrides. It builds the core client and server, excludes the SAIN addon, and packages only mod-owned artifacts. Dependency/game DLLs are never copied into its output ZIP. Use `-Verify` to additionally run the checked-in native contract verifier.

## License

MIT, preserving upstream attribution. See [NOTICE.md](NOTICE.md) and [LICENSE](LICENSE).
