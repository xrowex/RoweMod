# RoweMod Animation Studio

## Make a held trick

Open **Tools > RoweMod > Animation Studio** in Unity 2022.3.62f2.

## First: choose what the trick does

Every new trick asks you to pick one style before you can create it:

| Style | What happens | What Studio exports |
| --- | --- | --- |
| **Hold a pose** | Stay in one pose until release. | Enter + Exit; no Loop or Tweak. |
| **Hold + Tweak** | Hold normally, then move into an exaggerated pose when the game triggers Tweak. | Enter + Tweak + Exit; no Loop. |
| **Repeat a movement** | Repeat a moving animation until release. | Enter + Loop + Exit; no Tweak. |

Choose **Hold + Tweak** for a normal held trick with an exaggerated version. Choose **Repeat a movement** only when the animation itself should keep moving. These are simplified authoring styles, not a claim that the native data cannot contain both slots: V2 processes Tweak during Launch/Hold, not while in Loop.

The steps adapt: Hold hides Tweak entirely; Repeat shows **Movement** instead. Loop/Tweak slots are configured automatically, including replaced mirror/bike tracks. **Change style** preserves saved poses and supplied clips. Existing recipes remain **Advanced / existing setup** until you explicitly choose a style; opening one never rewrites its behavior.

For **Repeat a movement**, open Movement and choose **Start a new movement from Held**, copy your starting animation, or assign an editable Humanoid loop. **Pose rider + bike on one timeline** keeps all editing in this window. Match the first and last frames, and use **Match Held pose to the first loop frame** to align generated Enter/Exit. Export enables looping on the output copy, never the source. A missing Loop blocks export with a specific message. Preview repeats that actual movement and exits when you press Release.

**Game setup** normally shows a short summary. Bike clips, mirrors, timing and game rules are tucked under **Optional**. Manual phase setup and legacy clip-only export remain available under Change style > Advanced. Using supplied rider phases overrides the generated poses; a warning makes that explicit.

The game still owns triggering, blends and whether the chosen base trick supports native hold/loop. Test the exported trick in Streets; the authoring preview is not the game's input or physics simulation.

1. Choose a style, then **Rider + bike** or **Rider only**. Each click reveals the next choice. Choose a starting Humanoid animation and trick name. For a two-pose clip, choose its first frame as Riding and last frame as Held. **Create my trick** makes a separate recipe and pose clips; the original remains untouched. Use **Next** and **Back** to follow the numbered steps.
2. **Riding pose** is the starting/return position. **Held pose** is what the rider maintains. Click **Start posing**, pick a body part and drag its Scene-view control. Feet/hands/hips support position and rotation; elbow/knee controls steer the bend. Save is automatic after releasing a handle, or use **Save pose**. Ctrl+Z undoes a keyed pose.
3. **Tweak comes from Core Hold**, including the rider and authored bike. Until you edit a Tweak, its initial pose follows changes to the core Hold; simply choosing Hold + Tweak no longer locks in an early copy. Drag the same controls to exaggerate the pose. After editing a Tweak, its work is preserved when you revisit it, change Hold, or rebuild. **Restart Tweak from core Hold** copies the latest Hold into both Tweak tracks (with confirmation and Ctrl+Z); it never changes Core Hold. Older recipes are preserved until you use that button.
4. **Game setup** summarizes the selected style; it automatically includes the correct phase assignments in the bundle. Leave optional bike/timing/rules unchanged to inherit the base trick, or open the Optional section. Advanced creators can supply rider/bike phases and separate mirrors; irrelevant phase slots stay hidden and are excluded from export. Legacy manual recipes retain the original Include Game Setup, no-Loop and no-Tweak switches.
5. In **Export**, set authored Enter/Tweak/Exit durations and preview your poses. **Preview Tweak** and **Release > exit** are authoring aids, not a simulation of Streets' native transition logic. **Stop** restores the scene. Use Game setup's paired phase sampler to inspect supplied custom clips.
6. **Build animation bundle** exports selected clips plus settings and INSTALL.txt. Nothing is installed automatically. In Streets choose the base trick, select any package clip in **Custom Animation Clips**, then **Apply Studio Package + Save**. This requires the updated RoweMod build with package support. Settings and explicit empty phases persist in the selected trick's config.
7. Existing recipes can keep **Include Game Setup** off for legacy four-clip export. Use **Apply Complete Held Trick** or assign phases individually on a compatible older build. Legacy clip assignment does not apply the new settings or linked mirror route.

## Rider + bike in one window

### Use your own preview character

Open **Rig setup / sharing**. **Use workbench rider + bike** assigns `Human Temp` as the Humanoid rider and `Bike Skeleton` as the bike. Accidentally reversed assignments are also repaired when you start posing. Assigning an imported model's Generic Animator as Rider will not work.

Select your character's root in the Hierarchy (or a prefab in Project), press **Use selected model**, then **Connect character**. Use a model skinned to the workbench's existing skeleton, such as RoweBodyV2. The tool validates its skin/bone mapping and connects its meshes/materials to the existing rider; a Humanoid import alone does not make an arbitrary skeleton compatible. Inspect the whole body in Riding, Core Hold and Tweak before authoring.

The original Avatar, bones, IK controls and saved clips stay unchanged. The mannequin and its placeholder head are hidden, not removed. A selected scene source is also hidden so you don't see two characters; its position is not copied to the rider. **Show mannequin** restores the original visibility. Ctrl+Z undoes a swap. Save the scene to retain the connected character. This adds no extra Animator, IK solver or per-frame follower. The preview model and its editor-only bookkeeping are excluded from animation bundles; tools-only export includes the hookup code, not your model or textures.

Pick **Rider + bike** while creating a trick, or **Add bike posing to this trick** in an existing recipe. The same Riding, Held, Tweak and Movement steps now save both tracks. You do not need to assign generated bike phases manually. Disabling bike posing retains its saved work.

- **Body controls:** hands, feet and hips move or rotate; elbows and knees steer the bend. Blue spheres are left, orange spheres are right, yellow is hips.
- **Wrist comfort:** use **Relax both wrists**, or select a hand and relax only that side. Each click first snaps selected hands **within the Release distance** to their normal Riding palm contacts on the **current** bars, then relaxes around those contacts. It deliberately removes small grip offsets when clicked; ordinary bike movement still preserves your offsets. Hands outside the range are never snapped, even with auto-release off, and turning **Hands follow bars** off prevents snapping. The range is checked independently for both hands before either moves. **Relax amount** defaults to 50%; 100% aims for the normal forearm-relative Riding alignment, subject to Humanoid constraints. At 0%, nearby hands still snap without requesting an angle change. Hand auto-release measures the same palm point, so a relaxed grip stays attached. The command reduces an unsafe correction or keeps the original pose if the grip is unreachable. This is a one-time pose edit, not a continuous override; manual hand controls still work. Auto-key saves snap and relaxation together, or use Save pose; Ctrl+Z undoes the combined edit. Rider-only tricks do not snap to a bike.
- **Bike controls:** cyan boxes select Whole bike, Frame, Bars/forks, Cranks, each Pedal and each Wheel. Choose Move or Rotate, use the Scene gizmo, or type local values. These are animation joints, not the physical vehicle.
- **Hands follow bars/forks:** allows each nearby hand to follow its grip as you edit the bars. Turn it off to keep both hands independent, including during Whole bike edits.
- **Feet follow pedals:** allows each nearby foot to follow its pedal, including frame/crank movement. Turn it off to keep both feet independent.
- **Auto-release hands / feet when moved away (default ON):** a hand or foot farther than **Release distance** from its normal Riding contact stops following, independently of the other limbs. The default is **15 cm**, adjustable from 2–100 cm. Move it back within range to follow again. The four **Following / Released / Follow off** labels show what will happen on the next bike edit. Released position and rotation stay where you posed them; small grip offsets are preserved without snapping. Turning auto-release off restores unrestricted following for enabled contact groups.
- **Keep hips in place (default ON):** holds the pelvis at your posed position and rotation while eligible hands/feet follow the bike. You can still select Hips and pose it directly. Turn this off if you want Whole bike to carry the hips too; released or disabled hands/feet remain independent.
- **Frame** and **Bars / forks** share the real headset steering line, read from the equipped frame's MashBike `Forks_Anchor` and `Headset_Anchor`. A tailwhip leaves bars/forks/front wheel fixed while frame/cranks/rear wheel orbit that line; a barspin does the reverse. **Whole bike** includes both branches. Wheels rotate around their wheel joints without rotating frame/fork-mounted pegs.

The normal contact points are captured once from the recipe's saved **Riding pose**, in each bar/pedal joint's local space. They are saved with the recipe and do not move to a lifted Core Hold or Tweak. Release is calculated from the current pose, so reload, scrubbing and Undo do not silently reattach a distant limb. Eligibility is checked **before** a bike edit so a large bike rotation does not itself break a valid grip. Following is applied only while posing or baking in the editor; there is no idle solver or extra runtime work in Streets. Reach and Humanoid joint limits still apply. Ordinary sparse keys may interpolate away from contact between keys; preview the entire movement and add keys as needed.

### A full frame turn or barspin

Choose **Repeat a movement** → pose Riding and Held → **Movement** → **Start a new movement from Held** (choose the duration). Select **Frame** or **Bars / forks**, choose its local spin axis and optional reverse direction, then **Add 360°**. For a tailwhip, lift the feet beyond the release distance or turn off Feet follow pedals. For a barspin, Hands follow bars can stay on; move one hand away to release it while the other still follows. Inspect arm reach and any contact-release transitions throughout the movement.

The full-turn button adds rotation over the entire movement, preserving its underlying motion. It bakes intermediate bike keys and any enabled rider contacts at 60 samples/second (up to 10 seconds), so the turn does not collapse to identical start/end keys. **Ctrl+Z** undoes the paired bake. It is an offline editor operation: no extra solver or constant override runs in Streets. Repeated clicks add more turns.

Use the shared time slider to pose additional rider and bike keys. **Save rider + bike key** saves them at the same time. **Stop / restore** returns both rigs to their original scene transforms. Full-turn animation changes the visible rig; it does not force a physical spin or landing.

Generated rider/bike Enter, Tweak and Exit durations match automatically. If you supply an existing movement clip, keep its duration aligned with the bike movement. **Same bike both directions** reuses the same clip content; it does not geometrically mirror it. Explicit mirror assignments under Game setup take precedence.

### Game setup: practical choices

- **Generated poses** is the easy route. **Use my own phase clips** lets you assign editable `.anim` clips under `Assets/CustomClips` with as many animation keys as you want. Rebuild does not overwrite supplied source clips.
- Enter and Exit are required. No Loop selects the game's native hold-if-no-loop path; a Loop is a repeating phase and depends on `useLoopPhase` and continuing input. V2's automatic/requested Tweak update runs in Launch/Hold, not Loop.
- Rider clips must be Humanoid. Bike clips must use the exact Generic bike joint paths. Match corresponding clip durations: the game gives both tracks equal speed, not equal duration.
- Both-direction playback uses a linked partner trick in V2. The package creates a private partner when replacing mirrors, preserving stock assets. **Same clips both directions** copies animation content; it does not mirror the pose geometrically.
- Keep **All phases speed** at 1 and tune phase speeds. Native Enter/Loop = character master × overall × phase. Native Tweak/Exit = master × overall² × phase in the audited game build. This double multiplication is explained, not secretly corrected.
- **Tweak start** is percentage of Enter, not seconds. Native Tweak uses a 0.15-second blend. The `autoTweakAt` controller field is not used by the inspected update; it reads the per-trick Tweak start.
- Landing-hold is consumed by the V2 brain. Air-only is saved as native data, but no read was found in the inspected V2 firing path; it is not guaranteed to restrict input.
- Global controller hold/loop/auto-tweak, blend fades, masks, additive flags and cache settings are reference-only. Changing one animation must not silently reconfigure every trick on the character. No new runtime hooks or constant overrides are added.

### What the generated pose clips mean

- `_Enter`: riding pose to held pose, once.
- `_Loop`: the old pose builder's constant held pose. Guided Hold styles omit it. Repeat a movement uses your separate animated movement clip instead.
- `_Tweak`: moves from Held into your custom Tweak pose over **Time to Tweak**. When custom Tweak is off, it stays at Held instead. The game still decides when Tweak/auto-tweak activates; this tool does not change those controls.
- `_Exit`: held pose back to riding pose, once.

The generated phase clips are outputs. Edit Riding, Core Hold and optional Tweak in Studio; rebuilding replaces generated phase curves while retaining their GUIDs. Tweak begins as an exact copy of the latest Core Hold. Each untouched rider/bike Tweak continues to follow Core Hold until its own curves are edited; authored Tweak work and older recipes without tracking are never silently overwritten. This is an editor-only seed refresh, not a runtime additive layer or an automatic rebase of edited Tweaks. The exported Tweak transition always starts at the latest Hold. Existing two-pose recipes keep working without conversion or new files until you explicitly add Tweak. Use **Advanced Clip Editor** for unrestricted multi-key animation instead. The simple builder makes smooth transitions between poses, not custom motion paths or changing hold loops.

The preview reverses Tweak back to Held before Exit, including if you release halfway into Tweak. That return is an editor-only preview aid, not a fifth exported clip. Streets chooses its own phase transitions and blends into the regular `_Exit`, which starts at Held. In-game release from Tweak still needs testing with the chosen base trick; the preview does not establish that game's blend behavior.

Elbow and knee handles start at the actual joints, not offset outside the body. Unedited handles follow the joint after other body edits. Once dragged, a bend target remains where you placed it until you reload/scrub the saved pose; the endpoint and limb lengths still constrain where the joint can reach.

Streets' input and phase logic remain in control. Packages can change explicitly selected per-trick clips, timing and data flags; other values stay inherited. The pose preview is not an exact simulation of early release, runtime IK, collision, mirrored animation or game blending. A rider-only package still needs an appropriate stock bike-animation pairing. Test both directions, Tweak, release, landing, reset and map reload before sharing.

## Share animations versus share the editor

**Players installing a finished animation bundle do not need Final IK.** Studio packages contain ordinary AnimationClips and a JSON settings asset; legacy bundles contain clips only. Review the rights to any source animation before publishing a derived clip. No rigs, vendor sources or recipe references are bundled.

The **tools-only** export includes RoweMod editor code and this guide. It excludes Final IK/Baker sources, game rigs/meshes, all recovered clips, scenes, personal recipes and absolute machine paths. It is not a standalone ready-to-run project.

Creators using that package need:

- Unity 2022.3.62f2 (matching this workflow).
- Their own licensed RootMotion Final IK and Baker installation (including Baker helpers).
- A compatible Humanoid rig and their own/imported reference clips. Assign the rider Animator under **Rig setup / sharing**; the local private workbench is detected automatically if present. A bike Animator is optional.

Keep the local `RoweIKVendor` subset private. If importing a full Final IK/Baker installation into the private workbench, remove the duplicate subset first to avoid duplicate type errors. Do not include paid source files in a public download without appropriate redistribution permission. Purchasing an asset does not by itself establish that permission; consult the current licence or ask its publisher.

The recovered-workbench construction menus require local reference assets and are not a game-asset downloader. This tool does not extract assets from someone else's game installation.

## Safety

Stop the built-in Animation Preview/Record or Paired Preview before Studio takes control. It will not hijack another preview. Clip data saves separately; scene transforms restore on Stop, closing the editor, script reload, saving/closing the scene or entering Play Mode. IK runs only when posing. A held preview does not continuously solve the skeleton.
