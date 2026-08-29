# Streets animation audit and Animation Studio — 2026-08-28

## Evidence and boundaries

Audited the installed GameAssembly.dll (SHA256 `3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1`) using Cpp2IL metadata **and x64 native disassembly**, not the stubbed C# bodies. Reproducible read-only tools: `Scripts/Export-TrickAnimationAudit.ps1` and `Scripts/Disassemble-TrickAnimationAudit.py` (Capstone/pefile). Metadata contains shared native functions: an address can have many aliases and is not automatically safe to hook. This implementation adds **no hooks**.

This is static evidence for that binary, not an in-game acceptance test or a claim about every game version. GameObject/prefab serialized defaults may override constructor values. The new exporter and package loader do not change global controller defaults.

## Ownership

`TrickSystemBrainV2` reads selection/gameplay state and calls the controller. `TrickControllerV2` owns phase and release decisions. `TrickAnimator` plays corresponding rider and bike clips through separate Animancer layers. `SyncTrickAnimationData` supplies the selected trick's two tracks, timing, rules and mirror partner. Each `TrickAnimationData` has Enter / Loop / Tweak / Exit plus legacy mirror slots and a track-speed field.

The rider is Humanoid animation; the bike is Generic Transform curves that need the exact animation-joint hierarchy. Neither clip is the bike's physical rotation/force controller. Final IK is an editor posing tool here; exports are baked clips and plain JSON, not IK code, rigs or runtime solvers.

## Actual phase flow

| Native method / RVA | Observed behavior |
| --- | --- |
| `Fire` / `0x8AFFB0` | Rejects non-idle, missing data or missing normal rider Enter; initializes trick state, raises the layer, plays Enter and installs an end callback. |
| `RequestTweak` / `0x8B0A50`, `Update` / `0x8B1330` | Request flag or autoTweak allows a once-only Tweak while Launch/Hold. Checks normal rider Tweak exists and clamped Enter normalized progress against the data's TweakBeginBlendNormalizedTime (with a small tolerance). Does not read autoTweakAt. |
| `Tweak` / `0x8B0E30` | Rejects Exit/already-tweaked/missing normal rider Tweak. Plays the Tweak pair with a 0.15-second fade override and end callback. |
| `OnPhaseEnded` / `0x8B06F0` | With useLoopPhase, a normal rider Loop and valid continuing selection, starts Loop. Otherwise holdIfNoLoop **and no rider Loop** selects Hold. Other cases exit. A supplied Loop is not synonymous with a static held pose. |
| `ShouldLoopNow` / `0x8B0AA0` | Requires current slot not -1, current set matching fired set, and no zeroed input since fire. It does not require the same slot ID. |
| `HoldCurrentPose` / `0x8B0610`, `Update` / `0x8B1330` | Chooses tweaked state when available, otherwise Enter. Hold uses/clamps normalized time near 0.9995. This is the game's own update behavior; RoweMod adds no hold enforcement loop. |
| `UpdateSelection` / `0x8B11B0` | Selection/zero/release decisions differ between Launch, Tweak, Loop and Hold. For example Hold exits on a set change; early Launch release has additional checks. The editor's single Release button is not an exact input simulator. |
| `GoToExit` / `0x8B03C0` | Plays Exit directly with context-dependent fade/start bias; there is no reverse-Tweak phase. Exit completion handles cleanup. Require an Exit in packages to avoid invalid state/end-callback paths. |

The old guided editor reverses Tweak to Held before Exit for pose continuity. That remains **clearly labelled as a preview aid**, not exported game behavior. Use paired phase sampling to inspect supplied clips; test release/landing in Streets.

## Timing: an important native quirk

`TrickAnimator.ApplySpeed` (`0x8AE2E0`) and the inlined speed logic in `PlayPhase` (`0x8AED80`) assign equal speeds to rider and bike:

- Enter/Loop: character master speed × `_overallSpeedMult` × phase field.
- Tweak/Exit: character master speed × `_overallSpeedMult` **squared** × phase field.

Reason: `get_TweakSpeedMult` (`0x2923080`) and `get_ExitSpeedMult` (`0x2922F20`) already multiply by overall speed, and the caller multiplies again. Studio preserves native behavior and explains it instead of silently compensating. Keep overall at 1 and use individual phase speeds for predictable timing. `_loopMult` and `LoopSpeedMult` alias the same field; they are not separate knobs. Per-track `_overallSpeedMult` is not consumed by the inspected V2 speed path.

Equal speed is not equal duration. Match the authored rider and bike phase lengths. Tweak playback optionally aligns their starting normalized time; it does not stretch unequal clips into equal duration.

## Mirroring: the old slots are not the V2 route

`GetPlayerEnterClip(bool)` (`0x2922350`) and equivalent phase/vehicle getters route a mirrored request to `_mirrorSyncData.Get*Clip(false)` when a partner exists. Otherwise they return the normal clip. They do **not** use `_enterAnimationClipMirror` etc. for this path.

Studio therefore isolates the selected trick's track assets and, if mirrored tracks are replaced, supplies a private linked SyncTrickAnimationData partner. Stock partner tricks and other tricks sharing the original track assets stay untouched. Config stores this routing and the effective mirror phase names. Reset restores the original route; scene cleanup restores original references and destroys owned copies. “Same clips both directions” reuses animation content; it does not calculate a mirrored pose.

## What the unified UI exposes

The friendly style picker now offers **Hold a pose**, **Hold + Tweak**, or **Repeat a movement**. This is an authoring simplification: native data can contain both Loop and Tweak, but V2's Tweak update does not process requests during Loop. New UI recipes require an explicit choice. Old recipes keep manual behavior until a style is selected. Styles filter exported phase copies without deleting the underlying pose or clip assignments. Repeat requires an actual movement clip, previews repetition/release, and enables loopTime on exported Loop copies. Optional technical controls are collapsed by default.

- Riding / Held / Tweak posing plus the existing multi-key editor.
- **Game setup**: generated rider clips or four supplied rider phases; no Loop for native steady hold; no Tweak for an empty native Tweak slot.
- Optional four bike phases and separate rider/bike mirror phases. Enter/Exit required; blank Loop/Tweak explicitly clears them. Keeping an entire track leaves the selected game's values intact.
- Opt-in overall/phase speeds, Tweak start as a percentage of Enter, and native air/landing data flags. Off means inherit the selected game's values, not assume 1×.
- Air-only is exposed as a **native data flag, not a proven V2 input restriction**. No read of this flag was found in the inspected V2 Fire/Brain FixedUpdate paths. Landing-hold is read through CurrentTrickAllowLandingHolding in Brain FixedUpdate. Other input rules still apply.
- A paired phase sampler, plain-language help, compatibility checks and explicit export instructions in the same window.

Controller-wide useLoopPhase, holdIfNoLoop, autoTweak, layer fades, exit bias; animator masks, additive layers, fade modes, base animation and cache settings are explained in a **reference-only** section. autoTweakAt and per-track speed are not presented as working per-trick sliders. They are not silently applied across the character by an animation package.

## Export / compatibility / performance

Existing recipes keep clip-only export unless Game setup is enabled. New recipes enable Studio packages with native steady hold. Advanced phase sources must be editable `.anim` files under CustomClips; exporter copies them into owned output assets without modifying sources. Generic bike curves are checked against the workbench. Events, object-reference curves, wrong rig types, missing Enter/Exit, invalid timing and unexpected dependencies are rejected.

Bundle: explicitly selected clips + `rowemod.animation.json`, with no recipe/vendor/rig dependencies. Runtime: non-generic IL2CPP asset loads, bounded JSON, strict version/clip identity/type/range validation, rejection of duplicate package IDs. Catalog loading is tied to the existing refresh path, not an added per-frame search. **Apply Studio Package + Save** is explicit, applies once and uses existing saved overrides. Old clip-only bundles still work. This requires the newly built mod; it is not available in older installed releases until deployed.

## Checks

- Mod compiles without deployment (51 existing warnings, no errors at audit time).
- `Tests/AnimationStudio`: 21 checks passed for the real package validator and runtime apply/isolation helpers against fake Unity objects. These checks do not prove IL2CPP allocation, full config-reload integration or in-game animation behavior.
- Isolated Unity workbench tests passed JSON/recipe round trips, rider and Generic bike bundle contents, mirrored tracks, native no-Loop/no-Tweak, timing/rules, source preservation, safe failure cases and legacy export. See `studio-game-setup-validation.json` and `Logs/StudioGameSetupValidationFinal.log` in the local animation workspace.
- Existing Tweak regression passed: saved IK pose replay, phase continuity, hold/release preview, disabled-edit preservation, scene/source restoration and clean legacy export. See `tweak-pose-validation.json` and `Logs/StudioTweakRegression.log` in the workspace.
- Friendly-style validation passed all three exported phase combinations, style persistence, preservation when switching, missing-loop/mirror warnings, repeating preview/release, and blocking Tweak in Hold-only preview. See `studio-style-validation.json` and `Logs/StudioStyleValidation.log`.
- Unity UI inspection identified truncated toggle labels; Game setup uses full-row checkboxes and wider timing labels. The friendly style picker was subsequently inspected in the actual docked Unity window: all three full-width choice cards and descriptions fit, and the editor Console showed no errors after recompilation. Per-style exports and preview behavior were verified separately in the isolated project.
- No game process was controlled or used as acceptance proof. Restart/load/apply, both directions, repeated tricks, Tweak, early/late release, landing, reset and map reload remain in-game acceptance steps.

General framework references: [Animancer playback](https://kybernetik.com.au/animancer/docs/manual/playing/) and [transition/end-time semantics](https://kybernetik.com.au/animancer/docs/manual/transitions/). Game-specific behavior above comes from the installed binary, not assumptions from the framework documentation.
