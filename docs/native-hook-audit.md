# Native hook registration audit — 2026-08-27

## Custom hair follow-up — 2026-09-06

### Retest correction

The first build loaded its hook but failed visual acceptance. The 10:22–10:29 log
shows completed static hair with `nativeHidden=True` and repeated ten-second timeouts
for dreads/slim. The native `EnumEquip.MoveNext` (`0x7F15C0`) sets the busy flag at
`0x7F178A`, but its return through `0x7F26D8` does not clear that flag. Only the other
exit path clears it at `0x7F2D2C`. Busy=false was therefore an invalid universal
completion test. Hat/bust equip registers suppression at `0x7F26B9` or updates the
source dictionary and recalculates at `0x7F2A10`, after other setup work.

Revision 2 replaces the renderer-reactivation postfix with a prefix that masks the
local custom Hair slot's `_renderDisabled` BEFORE native deactivation. Source flags
remain untouched in CharacterFeatureController. Requests are registered before Equip
starts; pending custom hair is eligible once the newly instantiated item replaces the
previous one. Actual `MoveNext(false)` completion supplies the slot and original source
prefab, so stale/different-source callbacks do not finish newer selections. The native
busy flag is not modified. Native hair replacement and outfit reset remain excluded.

The completion hook and visibility prefix install together or not at all. Current
registration is 12 targets / 7 groups. Validation: 28 stubbed registration/hook checks,
15 compiled prefix/postfix signatures, uniqueness of all 17 audited addresses, and a
Release build. A one-shot diagnostic at 0.5s records hierarchy/renderer/shadow state;
there is no per-frame visibility enforcement. The user confirmed revision 2 working
in game before requesting release 3.3.7.

### First build (superseded)

Added `EquipSlot.ApplyRenderPolicy()` at RVA `0x7DE910` to the guarded late registry
(11 targets, 7 groups). The native method reads `_renderDisabled` and deactivates
each equipped renderer's GameObject, including inactive children. `CharacterFeatureController`
combines clothing suppression flags: Eyes=1, Hair=2, Beard=4. A hat or bust can therefore
hide a valid, successfully equipped hair mesh. The old active-only material lookup then
mistook the hidden mesh for a missing renderer.

The new postfix restores renderer visibility only for the actual custom hair instance
RoweMod finished equipping on the local gameplay/menu Hair slot. It does not modify
clothing flags, other slots, native replacement hair, shadow policy, or remote players.
There is no new toggle. The existing Hair visibility control and Restore Game Outfit
still take precedence. Re-equipping hats/busts re-runs the policy automatically.

Material requests now wait for native equip completion, use the equipped instance and
include inactive children. New model/material requests cancel older material work;
outfit reset cancels pending work. Hair applies to every hair renderer, while other
slots retain their first-renderer material behavior.

Validation: Release build, 26 stubbed registration/renderer-policy checks, compiled
signatures against installed wrappers, and the native uniqueness audit. These do not
prove in-game playback, equip sequencing, or visuals. In-game acceptance still needed:

1. Equip Beard and dreads on a custom character with a hair-hiding hat and bust.
2. Change each material and rapidly switch between the two hair models/materials.
3. Change the hat and bust afterward; custom hair should stay visible.
4. Hide/show Hair using the existing control; it should remain controllable.
5. Restore Game Outfit; the game's normal hiding rules must return.
6. Respawn and check that remote players' hair/outfits were not changed.

The original audit below records the pre-hair baseline.

## Changes

Removed `HarmonyInstance.PatchAll()` from early initialization and added the assembly's `HarmonyDontPatchAll` attribute. The installed MelonLoader 0.7 binary checks that attribute before its own automatic `PatchAll`, so a second registration path cannot bypass the allowlist. No attributed Harmony patch classes remain.

The tweak and nose-manual guards keep their existing explicit late installation. Ten other targets now install through `LateNativeHooks` in six feature groups, after startup config loading:

| Feature | Native target | Audited RVA |
| --- | --- | --- |
| Host capacity UI | FusionBootstrap.DrawServerBrowserHostMapControls(float) | 0xA61F70 |
| Host capacity | NetworkRunner.StartGame(StartGameArgs) | 0xD53AA0 |
| Player labels | NetworkPlayer.Spawned() | 0x9F47C0 |
| Manual IK | HumanIK.OnAnimatorIK(int) | 0x2983750 |
| Manual IK | UnityIKLimb.UpdateIK(bool) | 0x298C9E0 |
| Manual IK | VehicleFootPedalAnimationRig.LateUpdate() | 0x89A960 |
| Grind remapping | BikeGrindPoser.SetInputData(int, HookGrind) | 0x8077B0 |
| Landing assistance | QuaternionPDDrive.Tick(Rigidbody, Quaternion) | 0x837760 |
| Drone placement input | DroneController.LocalSpawnBullet(Vector3, Quaternion, Vector3) | 0x90DA80 |
| Drone placement input | DroneController.RPC_FireBullet(Vector3, Quaternion, Vector3) | 0x90E100 |

Every address is unique in the audited game metadata. `NativeHookSafety` hashes the game binary once (caching success or failure), checks the expected native address and exact IL2CPP patch backend before installation. Unsupported builds refuse these hooks. The existing two guards retain their independent startup checks. Hash: `3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1`.

Groups validate all targets before patching. Partial failure removes only the group's exact prefix/postfix methods and allows unrelated groups to install. Manual IK cannot activate without its complete hook group. No new per-frame enforcement or scene searches were added.

Grind remapping now checks the cached local poser before changing input or lerp speed. This prevents enabling its native hook from applying the user's settings to remote bikes. Patches with ordinary named arguments now use `__0`, `__1`, etc. Disabled landing assistance no longer formats a diagnostic every physics call; active diagnostic formatting is throttled before allocation.

## Already-late paths

Challenge hooks already install after startup/player events, so their timing stays unchanged. They now also validate unique targets before patching: TrickDetection.RecordTrickToHistory at `0x8544D0`, NetworkPlayer.Spawned at `0x9F47C0`, and NetworkPlayer.Despawned at `0x9F3220`.

ReplayInputPatch currently uses binding-only D-pad reservation. Its old native installer helpers are not called by the active path; they have not been re-enabled. No shared getter, global Animator, or generic completion-check hook was added.

## Checks and limits

- `Scripts/Test-TrickTweakNativeTargets.ps1`: uniqueness for all 15 active native addresses across 193,019 methods / 164 assemblies, plus the shared-getter regression case.
- `Scripts/Test-LateNativeHookRegistration.ps1`: compiled assembly has no blanket/automatic/early patches; all 10 explicit registrations covered; actual prefix/postfix argument signatures agree with installed game wrappers.
- `Tests/LateNativeHooks`: 20 checks of the real registrar's all-or-nothing groups, failure isolation, exact rollback, no unpatching of rejected targets, and one-time registration using fake patching/validation.
- `Tests/TrickTweakGuard`: existing 53 tweak/nose-manual guard checks.

These are build, metadata, and logic checks, not in-game acceptance. After restarting, expect `[NativeHooks] ... 6/6 feature groups ready`. Test map spawn/respawn, custom foot/hip poses including fakie, grind mapping, landing assistance, Object Dropper selection vs drone fire, and host player capacity. Check the next log for native patch failures before considering a release.

Reference: [MelonLoader lifecycle and automatic patching source](https://github.com/LavaGang/MelonLoader/blob/master/MelonLoader/Melons/MelonBase.cs), [HarmonyX backend registration](https://github.com/BepInEx/HarmonyX/wiki/Custom-MethodPatcher), [Il2CppInterop native detour implementation](https://github.com/BepInEx/Il2CppInterop/blob/master/Il2CppInterop.HarmonySupport/Il2CppDetourMethodPatcher.cs). Installed binaries and metadata were inspected in addition to these references.
