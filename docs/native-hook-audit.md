# Native hook registration audit — 2026-08-27

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
