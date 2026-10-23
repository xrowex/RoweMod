# Custom clothing textures — October 23 check

The reported failure was custom clothing textures breaking after entering a map.
Three defects were found in the local investigation:

1. Streets updated to Steam build **26150520**, version **2.0.0.3070.0**. The
   public 3.3.13 native profiles do not include its GameAssembly hash. A local
   launch reproduced 0/7 native groups and custom material equip timeouts.
2. Gameplay restoration required a named clothing preset, whereas the menu
   restores the current saved model/material selection. With preset `None`,
   gameplay logged `Cannot load preset` and skipped current selections.
3. Material application stopped at the first renderer for non-hair slots. A real
   Baggy Tee had a textured first renderer and a second visible renderer still on
   `MG_Clothing` with `DefaultTexture2D-White`. The shirt visibly stayed white.

## Changes

- Added the exact October 22 binary profile, preserving all three older profiles
  and unknown-build/shared-function rejection. All 17 native addresses were
  matched by full signature and checked for uniqueness across **201,454 methods
  in 162 assemblies**. See `native-hook-profile-26150520.json`.
- Spawn, gameplay-enter, and replay-exit restoration use the current selection,
  targeting only the captured local gameplay character. They coalesce overlapping
  requests, wait for equip-slot readiness, cancel on character replacement or a
  newer selection/reset, and use realtime waits while the game is paused.
- Model, material, and visibility operations resolve the equip-slot owner from
  the supplied spawn child. Automatic restoration does not auto-select a model's
  first material or overwrite the current selection from an older named preset.
- The chosen slot material applies to every renderer and material index within
  its equipped item, including inactive children. Renderer enabled states and
  objects outside that item remain untouched.
- Corrected Eyewear material-path persistence.

## Validation

- Isolated Release build: zero errors, 51 existing warnings; unrelated Animation
  Studio and live-foot changes excluded.
- **401 automated checks**, including 26 gameplay restoration checks, eight
  multi-renderer/submesh material checks, and 60 additional build-profile checks
  across the three native-registration test variants.
- **1,656** compiled game-wrapper references resolve against both tested interop
  versions; all **16** native patch signatures match both wrapper versions.
- Fresh startup on build 26150520 restores **7/7** native groups and both guards.
- Before/after live renderer audit and visible menu check: Baggy Tee changes from
  white to the selected red material on both renderers; Body and ARShorts retain
  their selected texture references.
- Real local map integration: the diagnostic invoked the game's native map
  service to load Jammas Skatehouse. The normal local-player spawn callback
  restored current selections with named preset `None`. Native equip completion
  and materials succeeded for Body, Top (both renderers), and Bottoms. A settled
  renderer audit confirmed the same texture references on the gameplay rider,
  and the selected red shirt was visibly confirmed in the gameplay viewport.

The test uses installed local clothing packages, not the reporting player's
exact bundles. Multiplayer and other players' appearance remain unverified.
The fixes are included in 3.3.14. Its separate release validation is recorded in
`outputs/release-3.3.14/`.
Evidence and configuration/install backups are in
`outputs/clothing-gameplay-20261023/`.
