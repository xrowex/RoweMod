# Changelog

## 3.3.20

- Joint clicks in Animation Studio now select the joint. Drag the red X, green Y or blue Z rotation rings to edit its pose; right-drag still orbits the preview.
- Corrected the preview steering assembly to share the frame's steering axis and connection point before applying authored edits.
- Fixed sparse Loop keys fading back toward the stock pose on every cycle. Loop tracks now wrap between authored keys, and a single key holds throughout the loop.
- Tweak tracks now hold their nearest authored pose outside the keyed range, including full tweak. Existing saved variations use the corrected interpolation automatically.
- Preserved inherited game contact locks, saved variations, presets and settings.

**Testing note:** Release build, Studio data/gizmo math checks and compiled compatibility audits passed. The new steering correction, rotation rings and playback changes still require in-game visual and riding confirmation.

## 3.3.19

- Added Animation Studio to the Streets main menu, using the equipped rider and bike. Edit rider/bike pose variations over native trick phases, save multiple versions, and explicitly apply a saved variation in game.
- Added frame stepping, previous/next edited key, phase boundaries, selected-joint/all-key views, contact keys, undo/redo and saved assignments. Arrow keys step frames, Ctrl+Arrows jump keys, and Home/End move to phase boundaries.
- Stock BMX sources are isolated from legacy animation remaps across cache rebuilds. The stock catalog contains 54 BMX entries and excludes scooter clips. Applying a stock-based variation restores its paired base clips; existing saved presets are retained.
- Contacts inherit the game's animated weights. Explicit locks capture the authored limb position relative to the bike, avoiding a snap to generic grip/pedal targets. Bar/frame edits rotate about the equipped frame's steering line.

**Testing note:** Release build, 63 Studio data checks, compiled wrapper/signature audits and native preview probes passed. Probes checked all stock clip tracks, remap/cache isolation, zero pose change when locking, held foot anchors and the steering reference against the equipped frame. This editor is a preview: full controller-driven riding, mirrored playback, multiplayer and replay acceptance remain unverified.

## 3.3.18

- Added audited support for Streets build 26257400 / 2.0.0.3073.0, restoring all seven guarded native hook groups.
- Fixed outfit requests remaining stuck after empty Hair/Eyes slots were cleared so subsequent material selections can apply. Presets and clothing files are preserved.

## 3.3.17

- Added audited support for Streets build 26175718 / 2.0.0.3072.0. The game update moved native functions, causing 3.3.16 to skip all seven hook groups and time out waiting for custom outfit completion.
- Restores the native equip-completion and custom-hair hooks used by clothing materials, along with the other guarded native feature groups. Preserves all four older build profiles and unknown-build/shared-function rejection.

**Testing note:** Release build, 466 automated checks, 17 unique native targets, compiled wrapper/signature audits, and local repeated-preset/map-load checks passed. The reporting user's log confirms successful material-bundle loading followed by unsupported-build hook failures; their exact clothing bundles and multiplayer appearance remain unverified.

## 3.3.16

- Smoke Session props and smoke now appear in the replay editor for newly recorded emotes in the current session. Their poses and smoke age follow native playback time, including pause and scrubbing.
- Replay visuals preserve the accepted camera grip and bong orientations. Closing replay clears them and stops the live emote loop.

**Testing note:** Release build, 451 automated checks, compiled wrapper/signature audits, and native-target audits passed. All five prop models appeared in the Streets replay editor; pause, backward seeks, normal playback, and the stop boundary were checked. Prop history belongs to the current session and is not embedded in saved replay files. Physical controller speed/reverse controls and native replay-exit cleanup remain unverified.

## 3.3.15

- Added nine Smoke Session gestures to the shared Emotes browser, with temporary joint, bong, beer, and camcorder props. Playing one dismounts the rider; Stop leaves the rider on foot.
- Added an emote search and category filters, including Smoke Session. Animation Studio validation clips and custom trick phases no longer appear as imported emotes.
- Corrected the camcorder grip and direction, both bong orientations, and visible smoke with a short fade after Stop. The pack is embedded in the DLL and excluded from the object dropper.
- Hidden clothing slots now stay active until native equip finishes, then return to their saved visibility. Loading the same outfit preset again no longer prevents their equip coroutines from starting.
- Removed bundled reference body meshes before native clothing cloning, fixing extra white body patches showing through custom clothing.
- Updated the native-target audit script to recognize the already-supported October 22 Streets binary.

**Testing note:** Release build, 401 automated checks, 17 unique native targets, and compiled wrapper/signature audits passed. The release DLL passed repeated-preset and gameplay-spawn checks using the local custom outfit. Gesture and prop orientations were visually accepted in Streets. Imported gestures and props remain local; multiplayer synchronization, replay recording, and pack audio are not included. An empty Invisible Eyes material bundle remains an asset issue; that slot stays hidden.

## 3.3.14

- Added an audited native-hook profile for Streets build 26150520 / 2.0.0.3070.0, restoring equip-completion handling and retaining all older profiles and safety checks.
- Custom clothing materials now cover all renderers and material slots in the equipped item, fixing visible meshes left on white/default materials.
- Gameplay restores current model/material selections without requiring a named preset, resolves spawn children to the equip-slot owner, and cancels stale or superseded requests.
- Corrected Eyewear material-path persistence.

**Testing note:** Release build, 401 automated checks, 17 unique native targets, wrapper/signature audits, and fresh runtime checks passed. Selected textures were verified on the menu and gameplay rider after a real local map load with no named preset. The reporting player's exact bundles and multiplayer appearance remain unverified.

## 3.3.13

- Preserves LT/RT and unloaded trick sets, extra slots, and unresolved trick names when saving presets. Supports native set-name aliases without merging ambiguous sets.
- Missing or unnamed entries preserve usable live slots, while explicitly disabled entries stay disabled.
- Refreshes only the local rider's trick runtime without enabling disabled components or broadcasting initialization.
- Added opt-in Manual Activation controls under Ride → Handling: separate manual/nose-manual delays and minimum stick thresholds, Physics Preset persistence, and restoration to captured game values.

**Testing note:** Release build, 307 automated checks, 16 real IL2CPP fixtures, wrapper/signature audits, and fresh startup checks passed. The reporting player's physical LT/RT failure and exact preset remain unverified; mounted UI, riding, and multiplayer require gameplay acceptance.

## 3.3.12

- Restored Custom Grind Friction under Ride → Handling, with the previous 0–5 range and 0.03 reset value. Uses local peg material copies and restores native materials when disabled.
- New configs use Streets' native Gravity and Small Hop values, ranges, and resets. Small Hop's native default is 1x; this mode leaves the bike's base hop velocities and world gravity untouched.
- Existing raw physics tunes and old presets remain in Legacy mode. Mount the local bike and choose Use Streets Physics Values to restore original base values and switch to current Streets settings.
- Native Physics Presets capture and restore the game values while retaining the active vehicle. Later edits through Streets are saved instead of repeatedly overwritten.
- Preserves old friction presets, including intentional zero friction, and rejects unbound or remote-proxy native settings.

**Testing note:** Release build and 269 automated checks passed. Eight native Unity/IL2CPP fixture checks cover local material isolation/restoration and hop preservation/restoration. Fresh startup retains all native groups and both trick guards. Mounted slider interaction, riding, and multiplayer still require gameplay acceptance.

## 3.3.11

- Added an audited native-hook profile for BMX Streets build 26117410 (October 21 update), preserving both previously supported builds.
- Restored the hooks used by asynchronous material application, custom hair, Disable Trick Tweaking, nose-manual/hang-five controls, manual IK, grind remapping, landing assistance, drone placement, host limits, player labels, and challenge lifecycle/trick capture.
- Exposed the existing saved per-trick speed setting as a prominent Trick Speed slider (0.05x to 10x) with Reset to 1x in the trick editor.
- Continues to reject unknown game binaries, unexpected addresses, and shared native functions.

**Testing note:** Release build, 235 automated checks, and an audit of 17 unique targets across 201,418 methods passed. Fresh runtime logs confirmed 7/7 native groups, both tweak/nose-manual guards, and bust/hat/hair equip completion and material application without timeouts. Riding/controller behavior, per-trick speed behavior, and multiplayer still need gameplay acceptance.

## 3.3.10

- Added an audited native-hook profile for BMX Streets build 26071101 while preserving the previously supported build.
- Updated equip-completion handling for the renamed coroutine, restoring the hooks used by custom-hair visibility and asynchronous material application.
- Tracks the native coroutine before it replaces the source with a clone, preserving stale-selection protection, and waits for the game's pending outfit load before submitting a replacement.
- Updated drone local-fire and RPC signatures.
- Updated the native targets for host limits, player labels, manual IK, grind remapping, landing assistance, tweak and nose-manual controls, and challenge lifecycle/trick hooks.
- Continues to reject unknown game binaries, unexpected native addresses, and shared native getters.

**Testing note:** the Release build, 220 automated checks, native address audit, and both game versions' compiled wrapper/signature audits passed. Fresh runtime logs confirmed 7/7 native feature groups, both tweak/nose-manual guards, bust/hat/hair completion and material application, and active hair rendering without equip timeouts. Controller-driven feature behavior and multiplayer remain to be checked in gameplay.

## 3.3.9

- Fixed a startup `TypeLoadException` when the game's generated equip coroutine wrapper is missing.
- Resolves the optional wrapper inside the hook group's error handling, allowing the remaining hooks and RoweMod startup to continue when that group is unavailable.
- Validates the completion wrapper's properties before installing either custom-hair hook and preserves native game-build and hook-address checks.

**Testing note:** the Release build, 156 automated checks (including an assembly with the equip coroutine omitted), and compiled hook/signature audit passed. A fresh local launch regenerated interop assemblies with `_EnumEquip_d__111` instead of `_EnumEquip_d__80` and reached the main menu with RoweMod running and no `TypeLoadException`. Native-hook safety rejected that newer game build (0/7 late hook groups available), and custom outfit materials timed out waiting for equip completion. This release fixes startup; it does not establish full compatibility with the newer game build. The reporting user's game remains to be retested.

## 3.3.8

- Fixed main-menu character discovery when no named RoweMod preset is saved.
- Restores current custom models and materials to the menu preview, including the custom-hair visibility fix from 3.3.7.
- Limits automatic menu restoration to the menu character, preserving gameplay characters and manual selections made during loading.
- Uses a pause-safe delay, cancels outdated restoration when the character or scene changes, and avoids duplicate restores.

**Testing note:** main-menu runtime logs confirmed dreads, hat, and bust restoration, with hair active and visible to rendering afterward. The Release build, automated checks, and native-hook audits passed.

## 3.3.7

- Fixed hats and busts hiding custom hair, including when clothing changes after hair is equipped. No new toggle is required.
- Fixed physics-hair equip timeouts by following native coroutine completion instead of the game's unreliable busy flag.
- Fixed custom hair materials skipping hidden meshes or applying before equipping finishes; newer selections cancel stale material requests.
- Preserved the existing Hair visibility control, Restore Game Outfit, and other players' outfit settings.

**Testing note:** the custom-hair fix was confirmed working in game by the user. Release build, automated checks, and native-hook audits passed. This release does not change experimental TV or multiplayer-media support.

## 3.3.6

- Custom emotes automatically download, verify, cache, and load when the Emotes page opens.
- Added download status, retry through Refresh Emotes, and temporary-file cleanup.
- Published the official emote bundle and an optional manual-install pack.
- Preserved the four-button pie menu with Replay on the right.

## 3.3.5

- Reduced the pie menu to four equal sections: RoweMod up, Replay right, Emotes down, and Vehicle Tuning left.
- Added Restore Game Outfit to clear custom overrides, request the native local outfit, and clean up unused assets while retaining saved presets.
- Consolidated vehicle tuning in RoweMod with presets and added coping-finder, transition, and prediction controls.
- Added emote looping, an independent native boombox with directional audio and volume/distance controls, replay prop/media integration, and five completed songs with cache cleanup.
- Included experimental TV playback and opt-in multiplayer media sharing.
- Added an extended replay-capacity option for higher physics rates and raised the replay camera-light limit to 50,000.
- Included peg-spark visibility fixes and replay lifecycle diagnostics.

**Testing note:** compilation and automated checks are separate from in-game acceptance. Fresh checks remain for the pie layout, outfit restoration, replay capacity, and media playback. TV can still produce no visible frame; multiplayer media needs two-client and late-join testing. See `docs/releases/3.3.5.md`.

## 3.3.4

### Peg sparks

- Fixed the Visual Effect Graph bounds that caused live particles to be culled after the spark rig moved from bundle space to a bike peg.
- Corrected the graph's exposed color properties so sparks use the intended hot-orange lifetime tint instead of retaining the prefab's blue base tint.
- Explicitly starts the graph's authored `OnPlay` event when an AssetBundle VFX component is enabled in the IL2CPP game.

**Testing note:** the Release build and project checks passed. In game, the preview spawned live particles and the fixed build rendered visible sparks; native peg-contact grinding and the final MelonLoader log were also checked.

## 3.3.3

### Tricks and animation workflow

- Cleaned up the Trick Mapping layout so the toolbar, input map, set groups, and rows use one consistent visual flow.
- Trick previews now follow the game's active trick state and wait for the complete animation before repeating. Stopping preview or leaving the menu cancels the preview and restores the rider.
- Added one-click Studio animation packages that apply coordinated rider and bike clips, phase timing, and mirror routing without modifying unrelated tricks.
- Included the Animation Studio authoring tools, package format, validation utilities, and focused tests used by the runtime workflow.

### Riding and effects fixes

- Manual IK now uses Mash's tracked manual and nose-manual activity state. Ordinary hop landings no longer claim manual-pose ownership, while real and fakie manuals retain the short release latch needed to avoid pedal-target flicker.
- Restored peg sparks to native peg contacts, added optional entry-impact bursts, recorded sparks for Replay, and simplified the player controls.
- Reorganized Grind Poses into Bike, Rider, Balance, and Transform sections and cached preset discovery to reduce tab lag.

### Replay and session recovery

- Added an opt-in Replay recovery path: if normal Back fails, holding B or Escape for two seconds walks the game's native close APIs before a final Gameplay transition.
- Fixed the host player-capacity nullable conversion that could throw an `InvalidCastException` while starting a host session.

**Testing note:** the Release build and native target/signature audits passed, along with 25 focused animation checks. An in-game pass covered startup, native hooks, trick previews, manual IK, grind tools, peg sparks and Replay recording/playback with no exceptions or errors in the final MelonLoader log.

## 3.3.2

### More control over your rider

- Reworked **Disable Trick Tweaking** to block manual and automatic bike tweaks.
- Added **Nose Manual Variants (Hang 5)**: keep the game's behavior, disable the variants, or swap the bumper-selected sides.
- Added optional manual and nose-manual pose editing. Move and rotate the left foot, right foot, and hips separately, with clearer controls, larger in-game handles, and wider adjustment ranges.
- Load custom rider animation clips from Unity bundles and assign them to trick phases in the animation editor. Custom clips must be supplied separately.
- Added a **30-250 Hz physics update rate** option. RoweMod uses the game's own rate unless you enable the override; higher rates cost more CPU and are not an FPS boost.

### Replay and menu improvements

- Added VX1000 film grain, adjustable lens dirt, and fine scratches for replays.
- Improved landing-assist controls and One Point Oh vehicle-tuning toggles.
- Added scrolling to the grind-pose panel and removed unused old menu code.

### Safer loading

- Reworked startup patching to avoid the earlier tweak-related map-spawn failure. Controls that cannot be verified for a game build are left inactive instead of using unsafe fallbacks.
- Kept grind remapping local to your own bike and reduced repeated landing-assist diagnostic work.
- Removed the custom-frame rear-peg workaround; rear-peg placement is a base-game issue.

**Testing note:** build and automated safety checks passed. The newest nose-manual, pose, and startup changes still need broader in-game testing; this is not a claim that every feature has been play-tested. Restart BMX Streets after updating, and keep a copy of your current DLL and settings if you want to roll back.

## 3.3.1

- Vehicle presets, physics presets, grind input remapping, host player limits, rear-peg frame support, and the expanded camera-light cap are now included in the released build.
- Vehicle presets remain simple for players: choose, save, update, or delete a preset without exposing technical setting lists.

## 3.3.0

- New controller-first RoweMod menu with a stable custom cursor and clearer navigation.
- New Graphics controls: HDRI skies, per-map exposure, and Balanced, Low, and Potato performance presets.
- Object Dropper now supports controller placement, its own placement camera, stacked objects, remembered selection, and automatic list scrolling.
- Vehicle Tuning is available from the pie menu, with controller navigation, presets, clearer controls, and expanded flip, spin, tire-ride, grinding, and rotation tuning.
- Vehicle Tuning now keeps the selected setting visible while you scroll with the controller.
- Improved replay framing and camera-light cleanup.
- Improved grind-pose navigation and preset controls.
- Removed the experimental rider IK, variant, foot-offset, and head-tracking controls because they were not reliable enough to ship.
