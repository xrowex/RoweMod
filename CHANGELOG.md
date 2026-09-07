# Changelog

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
