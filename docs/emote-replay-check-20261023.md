# Emote prop replay checks — October 23, 2026

Smoke Session's temporary props previously disappeared when opening replay.
They now record poses on Streets' native recording clock. Replay creates a
separate visual for the recorded gesture, preserving its material, scale,
camcorder grip, bong orientation, and the table's fixed placement. Separate plays
have separate segments so seeking cannot interpolate across emote changes.

Smoke records each puff's birth time, origin, velocity, size, spin, opacity, and
lifetime. Replay reconstructs its position and fade from playback time using a
bounded pool of billboards. Pause and backward seeking do not age the smoke.
Native replay duration bounds the in-memory history; scene/mod cleanup clears it.

## Verification

- Isolated build from the released 3.3.15 source plus the replay changes: zero
  errors, 51 existing warnings. Unrelated Animation Studio edits were excluded.
- 50 timeline checks cover interpolation, same-time visibility changes, gaps,
  separate gesture segments, pause, arbitrary/reverse seek order, recording
  rewind, native buffer expiry, bounded memory, and cleanup.
- 16 emote bundle/cache checks passed. All 1,687 compiled wrapper references
  resolve against both tested interop versions. All 16 native patch signatures
  match installed wrappers; no new native hooks were added.
- Streets build 26150520 / 2.0.0.3070.0 recorded camera, beer, standing bong,
  table bong, and seated smoking gestures. Native editor checks found every
  corresponding replay prop active at its recorded time.
- Pausing retained exactly identical prop and smoke positions and scales.
  Seeking from the table to seated smoking and back reconstructed identical
  state. The post-emote gap had its prop hidden and zero visible smoke puffs.
  Normal forward playback advanced recorded poses and smoke.
- Native editor screenshots confirm the camcorder and smoke with the recorded
  rider pose. A temporary diagnostic camera framed these checks; it is outside
  production source. The helper was moved out of Mods after testing. The game
  was left open for the user's replay inspection; restarting unloads its idle
  diagnostic instance. Existing user settings were preserved.

Tested and locally installed DLL SHA-256:
`022618e87ef2bdad99e74227d509e1bd8b52f479a8db04fcc05ebdff7c549add`

## Scope

The runtime checks above used the local development build. Release 3.3.16
packages the same replay implementation with updated version metadata and a
fresh release build and audits. Perform a new emote to capture props.
History belongs to the current native replay session and is
not embedded in saved replay files. Multiplayer synchronization is unchanged.
Physical controller speed/reverse controls and native replay-exit cleanup were
not independently verified. Backward seeking, pause, normal forward playback,
and the stop boundary were verified in the native editor.
