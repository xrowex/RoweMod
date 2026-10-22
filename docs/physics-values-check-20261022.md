# Gravity, small hop, and grind friction

Implemented in RoweMod 3.3.12, following the native-hook compatibility release 3.3.11.

The report identified two separate problems:

- Grind Friction was removed in commit `ca4ac93`, including its config field,
  menu control, peg lookup, and material writes.
- Gravity and Small Hop still displayed raw values from the older physics path.
  RoweMod set world gravity directly and replaced two base hop velocities with
  `smallHopForce` (default 4.2), while the current Streets quick-hop control is a
  separate multiplier whose default is 1.

The updated Handling page uses the local player's native Gravity and Small Hop
bindings. Slider ranges and Reset values come from the actual native definitions.
New configs use this mode, preserve the player's current Streets preferences,
and leave world gravity and the bike's base hop velocities untouched. An explicit
slider edit saves a native override; later changes made through Streets are
observed and saved instead of repeatedly overwritten. Bindings must be local,
bound, and not network proxies.

Existing configs and older Physics Presets remain in a clearly labeled Legacy
raw tune mode. **Use Streets Physics Values** restores captured world gravity
and distinct original hop velocities, then uses the player's current native
settings. This switch requires a mounted local bike with native bindings. A new
Physics Preset captures the current native values; loading a preset keeps the
active vehicle selection. Reset Handling uses the game's native defaults.

**Ride → Handling → Grinding** restores Custom Grind Friction with the previous
0–5 range and 0.03 reset value. It edits private copies assigned to local peg
colliders, preserving shared materials, wheels, triggers, and other players.
Disabling it restores the native materials. Part replacement and detached pegs
retire the old copies. It defaults off, leaving the game's materials active.
Older configs/presets with the removed friction field keep their explicit tune,
including zero friction.

## Validation

Target: Steam build 26117410, GameAssembly SHA256
`3EBD5AB08931C3BB35E1264739975F0EB6FD2AD56EDFD99E5FA48DA7EABD6E85`.

Native resource reads confirmed:

| Setting | Native default | Range |
| --- | ---: | ---: |
| Gravity | 0 | 0–5 |
| Quick Hop Mult | 1 | 0.25–2 |

The native Gravity setting is kept separate from world gravity in m/s². Native
disassembly confirms quick-hop height reads its player setting and multiplies
the existing base hop velocity; it is not the old raw velocity editor.

- Isolated Release build: zero errors, 51 existing warnings.
- 34 new physics, preset, and material-isolation checks; 235 existing checks.
- Eight real Unity/IL2CPP fixture checks passed inside Streets, including native
  material cloning, friction writes (including 4.0), shared-material isolation,
  restoration, preserving base hop velocities in native mode, and restoring
  distinct original velocities after legacy tuning.
- Compiled fresh-config and Reset Handling defaults both use native scale
  version 1 and a 1x small-hop default.
- All 1,654 compiled wrapper member references resolve against both preceding
  and current interop; all 16 patch signatures still match both wrapper sets.
- Fresh startup retains 7/7 native hook groups and both trick guards.

Runtime fixtures used temporary inactive objects. Riding, a mounted local
player's slider interaction, and multiplayer have not been accepted in gameplay.
The initial local check used a private 3.3.11 development build. The subsequent
3.3.12 release packages this fix; see `releases/3.3.12.md` for install instructions
and the validation limits.

Backups, scoped build source, native metadata/disassembly, diagnostics, startup
logs, and the validation receipt are under `outputs/physics-alignment-20261022/`.
The temporary diagnostic mod is removed after testing, and the user's original
configuration is restored byte-for-byte.
