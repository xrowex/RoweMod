# Trigger presets and manual activation

Local development changes after 3.3.12; not in the published DLL.

The report says LT/RT mappings stopped working in saved trick presets and asks
for manual activation sensitivity. The exact affected preset and physical-input
failure remain unconfirmed; these changes address verified restoration defects.

## Trick preset restoration

- Prefer the local vehicle's `VehicleTrickDataSets`, which native
  `ResolveVehicleTrick` reads, over the first resource whose name contains BMX.
- Read all eight native trick actions from the local input owner. The older six
  action cache was not the actual gameplay routing, so its omission alone does
  not establish the cause of the reported trigger failure.
- Refresh only the enabled, active local V2 brain through `RefreshTrickSet`.
  Removed the global component toggles and broadcast initialization methods.
- Retain unloaded sets and extra slots when saving a partial catalog. A six-set
  scan no longer discards saved LT/RT rows.
- Resolve known set-name aliases and cloned names without confusing LT, RT,
  LT+RT, or shoulder/trigger combinations. Exact names win; ambiguous aliases
  are not guessed.
- Preserve unavailable or empty-name saved mappings and log them. An unresolved
  enabled entry no longer blanks a usable live slot or gets overwritten with an
  empty mapping on the next save. Explicit replacements clear pending entries.

This does not change native brake/hop trigger gates, trigger bindings, or stick
direction rules. A controller retest with the affected preset is still required.

## Manual activation

Ride → Handling → Manual Activation offers **Customize Manual Activation**,
separate manual/nose-manual delays in milliseconds, and separate minimum stick
thresholds. **Use Game Activation** restores each hold point's original values.
Customization defaults off and starts from the current local values.

The current native assets use a 200 ms manual delay, 100 ms nose-manual delay,
and 0.2 minimum stick input for both. Native maximum hold input is 0.8, maximum
cancel input 1.1, and maximum input velocity 1. Those upper/velocity gates remain
unchanged. Lowering sensitivity also adjusts the lower cancellation gate so a
newly activated manual does not immediately cancel on its next tick.

Only authoritative local input hold points are changed. Rider/point replacement,
disable, and Handling reset restore captured values. Settings travel with Physics
Presets. Zero delay means activation on the next eligible native physics tick.

## Verification

- Release build: zero errors; 51 existing warnings on a full build.
- 307 automated checks, including 38 new key-alias, eight-action, local-refresh,
  and manual-activation checks, and all 269 preceding checks.
- Sixteen real IL2CPP fixtures inside Streets: eight-set discovery, LT/RT
  mapping restore and save, RT alias migration, six-set save preservation,
  missing/null-name preservation, unloaded-set retention, disabled-component
  isolation, native hold delay, immediate low-threshold activation, consistent
  cancellation, upper/velocity gate preservation, and native value restoration.
- All 1,656 compiled wrapper member references resolve against both preceding
  and current interop. All 16 native patch signatures still match both.
- Compiled runtime isolation audit rejects component toggling, broadcast
  initialization, and action/map enabled-state changes in the trick paths.
- Fresh startup retains 7/7 native groups, both trick guards, and successful
  outfit material application.

Fixtures used temporary inactive objects and temporary config entries restored
afterwards. No physical controller, affected-user preset, mounted UI interaction,
or multiplayer acceptance is claimed. The changes are included in 3.3.13; the
release build is validated separately under `outputs/release-3.3.13/`.

Backups, scoped source, native metadata/disassembly, logs, and validation receipt
are under `outputs/triggers-manual-20261022/`. The diagnostic mod is removed and
the original configuration restored byte-for-byte after testing.
