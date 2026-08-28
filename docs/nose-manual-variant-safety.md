# Nose-manual variants / Hang 5

The control is under **Ride > Handling > Pump, Spin & Manuals > Nose Manual Variants (Hang 5)**.
It retains `physics.hangFiveMode`, including existing config/preset values:

- Game (0): original incoming variant.
- Disabled (1): route the variant to 0, the normal nose manual.
- Swap Sides (2): swap variants 1 and 2; leave 0 and unknown values alone.

## Native evidence and boundary

For GameAssembly SHA256 `3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1`:

1. `BicycleAnimationInput.FixedUpdate` chooses `_noseyVariant` (1/2 from shoulder inputs), then sends `Nosey Variant` via `IAnimationInputHandler.SetInteger`.
2. `VehicleAnimationInputHandler.SetInteger(string,int)` forwards the value to both HybridAnimancer components (rider and vehicle).
3. Its native RVA is `0x85BE70`, length `0xBC`. The audit across 193,019 methods / 164 assemblies found exactly one owner.

The old attributed prefix was registered in early `PatchAll`, before native patcher registration. It is removed, not left alongside the new hook. `Main.OnLateInitializeMelon` now installs the explicit prefix after verifying the binary hash, exact overload, native address, and Il2CppInterop native patcher. Unknown builds disable this feature with a warning instead of falling back to an unsafe target.

HarmonyX requires the patcher resolver to be registered before applying patches ([documentation](https://github.com/BepInEx/HarmonyX/wiki/Custom-MethodPatcher)). The prefix uses `__0` / `ref __1` to avoid reliance on generated IL2CPP parameter names ([argument injection documentation](https://harmony.pardeike.net/articles/patching-injections.html)).

## Scope and cost

- Bind only the existing local-player spawn root's vehicle handler, once on spawn (or explicit setting change if binding was missing).
- Clear the identity on scene changes, replacement spawns, and cleanup.
- Compare the native handler pointer and exact parameter name before routing.
- Preserve unrelated animation parameters, other players, input actions, and custom animation/IK settings.
- No new Update/FixedUpdate/LateUpdate, coroutine, repeated scene search, or post-animation corrective write. This intercepts the game's existing animation-input call, which itself runs during physics updates.
- Do not change the source `_noseyVariant`; swapping cannot feed back and alternate each frame. Changing mode affects the next normal animation-input write, including while already nose-manualing.
- Diagnostics record binding and a capped six variant changes per spawn/setting change. Check the cap before formatting.

## Verification

Run the native-address audit with `Scripts/Test-TrickTweakNativeTargets.ps1` (now covers both guards). Run the focused test project under `Tests/TrickTweakGuard`; it links both actual guard source files and supplies test doubles only for game/interop objects.

The tests cover Game/Disabled/Swap, local vs remote identity, unknown builds/modes, unrelated parameters, respawn/rebinding, scene exit, cleanup, bounded logs, no added component searches in the prefix, and no extra animator writes. These are logic tests, not a real native trampoline or animation test.

In-game acceptance still requires: select Disabled, perform a nose manual with each bumper and both together, and confirm the rider stays in the normal nose-manual pose. Check Swap Sides, return to Game, then repeat after respawning/map change. Ordinary bumper actions outside the nose manual should remain unchanged. Logs should show `[HangFive] ... verified native hook`, a local handler binding, and `Local Nosey Variant 1 -> 0` / `2 -> 0` in Disabled mode.
