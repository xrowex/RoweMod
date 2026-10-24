# Materials after the Streets 3072 update

## Reported log

The October 24 report runs the public RoweMod **3.3.16** DLL. Its recorded
SHA-256 matches the published asset, despite the local filename having a numeric
suffix. The log shows only one RoweMod assembly; the filename is not evidence of
a duplicate installation.

The game version is **2.0.0.3072.0**, dated October 23. RoweMod reports that this
binary has not been audited and finishes late registration with **0/7 feature
groups ready**. Both tweak and nose-manual guards are also unavailable.

The clothing material bundles load successfully. Later Body, Top, Gloves,
Socks, Bust, Hat, and Hair requests time out with `nativeStarted=False` and
`busyFlag=False`, or report native outfit loading has not completed. This is
consistent with missing native outfit-completion handling on the new build.
The screenshot's white clothing does not establish a corrupt texture bundle.

The separate posture mod also reports unsupported game-build warnings. This
patch updates RoweMod's compatibility profile only.

## Audit and change

The local Steam installation updated to build **26175718**. Its GameAssembly
SHA-256 is:
`6d42aeb57fa79c2c29014bd35d3693f4983f954a4108218662c6827bc340b460`.

MelonLoader regenerated Cpp2IL metadata and managed wrappers from this binary.
All seventeen hook targets were matched by full signature, then checked for
unique address ownership across 201,337 methods in 162 assemblies. The shared
getter at `0x6BC680` has 500 aliases and remains rejected. The production profile
is pinned in `docs/native-hook-profile-26175718.json`; the audit script checks
that exact table. No hash or address guard was bypassed.

## Validation

- Isolated 3.3.17 Release build: zero errors, 51 existing warnings. Unrelated
  Animation Studio and live-foot work stayed outside the build.
- 466 automated checks passed, including the new build routing and rejection
  cases across all three native-registration fixture variants.
- All 1,687 compiled wrapper references resolve against three interop versions;
  all sixteen native patch signatures match installed wrappers.
- Fresh startup reports 7/7 native hook groups and both guards ready.
- The local outfit test changes a hat, loads a custom preset twice, and loads
  Jammas Skatehouse. Native equips complete and selected materials are applied.

Runtime evidence and reversible settings/install backups are retained under
`outputs/streets-update-20261024/`. A pending updater initially restored the old
public DLL during a test restart; loaded-DLL hash checks caught it. The actual
compatibility run uses candidate SHA-256:
`71a9ff5b586a7f2dbb12b10356b002cfb978ac92b87dbfb6ef57b9d7f6d7dcd9`.

These checks use local installed clothing, not the reporting player's exact
bundles. Their outfit and multiplayer appearance still need user acceptance.
