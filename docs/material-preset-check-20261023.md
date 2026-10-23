# Custom outfit material diagnosis — October 23, 2026

## Failures found

The reported session's Body material applied successfully. The failures came
from two independent paths that could make the result look like a body texture
problem:

1. Gloves, Socks, Hair, and Eyes had been hidden by deactivating their equip-slot
   objects. Loading the preset again submitted native equip to inactive owners.
   Unity's Player.log reported `Coroutine couldn't be started` for those slots;
   RoweMod subsequently timed out without native equip starting.
2. Clothing templates contained `SM_Body` / `SM_Bust` reference geometry. Deferred
   destruction allowed Streets' synchronous native equip clone to retain that
   extra geometry. It appeared as white/blue patches through the shirt and arms.
   Hiding the selected Body did not remove the patches; hiding the shirt exposed
   the extra mesh. The selected dark shirt texture itself was present.

The installed Invisible Eyes material bundle also contains only an empty prefab,
with no renderer or material. Its material lookup correctly fails. This asset
was preserved; the hidden Eyes slot does not need a visible material.

## Changes

Hidden slots are temporarily active while their tracked native equip request
runs, then return to the latest saved visibility. A superseded request cannot
restore visibility over a newer selection.

Reference body geometry is removed immediately from the loaded bundle template
before native equip can clone it. Files on disk are untouched. Body's intentional
automatic-material-selection skip is now an informational message; explicit
material choices and preset material application still run.

## Verification

Tested the public 3.3.15 candidate on Streets build 26150520 / 2.0.0.3070.0:

- Changed the hat before loading the saved custom outfit, then loaded that same
  preset twice and entered Jammas Skatehouse.
- All eleven equip slots completed. Hidden Hair, Eyes, Gloves, and Socks returned
  to inactive visibility with completion true and busy false.
- No inactive-owner coroutine errors or equip timeouts occurred.
- All active renderers were inspected after both preset loads and gameplay
  spawn. No `SM_Body` / `SM_Bust` reference meshes remained. Selected Body and
  shirt materials retained their albedo textures.
- The gameplay shirt rendered dark with clean arms instead of the extra white
  template patches. Temporary audit code was removed after testing; the saved
  config was restored and the preset file remained unchanged.

Candidate DLL SHA-256:
`425c91d3e1183accc9650c0116a2d60abd244c8ebd21d6478a05a1d3052f9eae`

This validates the local installed outfit and native equip lifecycle. It does
not establish other users' bundle contents or multiplayer appearance.
