# Changelog

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
