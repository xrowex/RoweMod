# Smoke Session build inputs

The bundled pack contains nine local Humanoid gesture clips and temporary emote
props. RoweMod embeds `Bundles/rowemod_smoke_session` in its DLL and verifies its
SHA-256 before Unity loads it. `Mods/SmokeSessionController.cs` pins the hash.

The authoring project uses Unity 2022.3.62f2. Place recovered per-frame joint JSON
in `Assets/DirectPoses` and recovered OBJ/TGA assets in `Assets/SourceProps`, then
copy these two editor scripts into `Assets/Editor`. Run:

    unity run <project> --editor-version 2022.3.62f2 -- -nographics -executeMethod SmokeSessionPackBuild.Build

`SmokeSessionDirectBuild` converts the source right-handed skeleton directly to
Unity coordinates, enforces a T-pose for the source Humanoid avatar, and samples
muscles/body motion at 30 Hz. It bypasses the FBX bone-axis intermediary. The
clips keep vertical motion for crouching and sitting; the native emote controller
owns playback, cancellation, and repeating the clips.

`SmokeSessionPropsBuild` creates the temporary prop prefabs and Windows bundle.
Position-only native shadow sections are omitted from the visible OBJ exports.
The hand bong is isolated from the original table's ROOR geometry to avoid
relying on a Frostbite-specific effect deformation shader. The table appears
only during Bong Table and is never registered with the object dropper.
The authoring build separates its 9,272 bong triangles from 1,164 furniture
triangles and creates `SmokeSessionTableBong` at the bong base. Runtime turns
that pivot 180 degrees without moving the furniture. The hand bong uses the
same turn around its centre; the smoke source follows each bowl.

After rebuilding, copy the bundle into `Bundles/rowemod_smoke_session`, update
its pinned hash, and build with `-p:SkipRoweModDeploy=true`. Validate all nine
clips in Streets, local hand props, loop restart, Stop, movement cancellation,
scene/replay cleanup, and native dismount before playback. The bike stays visible,
and stopping an emote leaves the rider on foot.

Source: NOBLE1337 Smoke Session 2.2.1 on Thunderstore.
Animations and models by N O B L E. Camcorder by James.Hasegawa, CC BY 4.0,
modified by NOBLE and converted for Streets:
https://sketchfab.com/3d-models/camcorder-video-camera-002d346e9faa41b6b8075feced9e0ba7

Local preview evidence and recovered authoring inputs are kept under the task's
`outputs/smoke-session-review-20261023` and `outputs/smoke-session-streets-20261023`
folders. These folders are ignored by Git and are not public release artifacts.
