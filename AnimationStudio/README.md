# RoweMod Animation Studio

Editable sources for the unified Unity authoring window. The working copy is also installed in the local animation workbench. No recovered game assets or paid IK source code is included here.

## Use

1. Use Unity 2022.3.62f2 and a compatible Humanoid rider rig. A Generic bike animation rig is optional.
2. Install your own licensed Final IK and Baker editor dependencies. Do not import duplicate vendor subsets.
3. Copy `Editor/` and the shared `RoweAnimationPackage.cs` into your project's `Assets/Editor/` folder. Keep the shared schema identical to the mod's copy.
4. Open **Tools > RoweMod > Animation Studio**. First choose **Hold a pose**, **Hold + Tweak**, or **Repeat a movement**; the steps adapt to your choice. Assign the rider/bike under Rig setup when not using the local workbench.
5. Follow [the creator guide](Editor/ROWE_ANIMATION_STUDIO.md). Studio packages need a RoweMod build containing `TrickAnimationPackages`; older releases only accept the legacy clip workflow.

The recovered-workbench construction menus require local reference assets; this folder is not a standalone rig project. Custom bike export currently validates against the local **Bike Skeleton** hierarchy. The shared editor export excludes paid source, rigs and recovered animations.

## Audit and verification

[Native system audit](../docs/trick-animation-system-audit.md) documents the installed binary, phase routing, mirroring, timing quirks and what is deliberately left character-wide.

`dotnet run --project Tests/AnimationStudio/AnimationStudio.Tests.csproj` checks the shared schema and runtime apply helpers against fake Unity objects. `Tests/AnimationStudio/Unity/RoweGameSetupValidation.cs` is the isolated-workbench Unity integration test; it requires the local paired scene and reference clip noted in that source. Never run asset-generating validation in a user's authoring project.

Build/export checks do not establish in-game acceptance. Test application, both directions, Tweak/release/landing, reset and map reload before publishing. No DLL or bundle is installed automatically by this authoring workflow.
