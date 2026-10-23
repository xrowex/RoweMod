# RoweMod Emotes Pack

Custom emote animations for RoweMod 3.3.5 and later. This pack contains the
`rowemod_custom_emotes` Unity AssetBundle used by RoweMod's imported-emote menu.
It does not contain the mod DLL, custom trick packages, or downloaded music.

## Automatic installation (3.3.6 and later)

Update RoweMod, restart BMX Streets, load a map, and open Emotes. The official
pack downloads, verifies, and loads automatically. No ZIP is needed. A verified
installed pack is reused offline; use Refresh Emotes to retry a failed download.

## Manual installation (optional, or for 3.3.5)

1. Install RoweMod 3.3.5 and close BMX Streets.
2. Download `RoweMod-Emotes-3.3.5.zip` from the
   [3.3.5 release](https://github.com/xrowex/RoweMod/releases/tag/v3.3.5).
3. Extract the ZIP into the BMX Streets game folder, merging its `Mods` folder.
   The bundle must end up at:

   `BMX Streets/Mods/RoweMod/Bundles/rowemod_custom_emotes`

4. Start the game and load a map. Hold D-pad Right, select Emotes with the right
   stick down, then release D-pad Right. Choose an imported emote and use Loop
   if you want it to repeat.

Do not leave the ZIP inside the Bundles folder or add an extra folder above
`Mods`. If replacing an older copy, keep only one `rowemod_custom_emotes` bundle
under Bundles and its subfolders. Restart the game after installing or replacing it.

In 3.3.5, install the pack manually. In 3.3.6 and later, opening Emotes installs it automatically.
Imported animations are local; this pack does not add multiplayer emote sync.
Radio/TV prop downloads are handled separately by RoweMod.

## Browse emotes

Open **Customize → Emotes**, or choose Emotes from the pie menu. The shared
browser lists emotes, dances, added poses, and imported rider clips together,
above the Radio controls. Use **All**, **Dances**, **Poses**, **Game**, or
**Imported**, or **Smoke Session** to filter, and search by display name or clip name.

Each entry has **Play**. Added poses and imported clips also have **Loop**;
**Stop Emote** remains above the browser. Radio playback stays independent.
Only loaded, playable catalog entries appear in the browser.
Imported emotes use the `RoweMod_Custom_Emote_` name prefix. Trick phases and
Animation Studio validation clips stay in the trick editor and do not appear here.

## Smoke Session (3.3.15 and later)

Smoke Session's nine NOBLE gestures share the same browser under the
**Smoke Session** filter. Joint, bong, beer can, and camcorder props are temporary
and follow the local rider during their matching gesture. Emotes play off the
bike: choosing Play or Loop while riding first uses Streets' native dismount.
Stop Emote, movement cancellation, replay, and scene changes remove the props.
Joint and bong smoke includes mouth puffs during exhalation. Existing smoke
fades for up to 3.2 seconds after Stop; replay and scene changes clear it immediately.
The camcorder faces outward and attaches at its top handle. Both bongs face
the rider, with the table bong rotating independently of the furniture.
The pack is local-only in Streets; multiplayer and replay recording are not added.

The DLL embeds the verified pack, so replacing `Mods/rowemod.dll` and restarting Streets is enough. No separate download is needed. The assets are excluded from the object-dropper prefab catalog. The bike stays visible after dismounting; Stop leaves the rider on foot. Pack audio is not included.

Animations and prop models: **N O B L E**, Smoke Session 2.2.1.
Source: https://thunderstore.io/c/reskate/p/NOBLE1337/Smoke_Session/
Camcorder: James.Hasegawa, CC BY 4.0, modified by NOBLE and converted for Streets:
https://sketchfab.com/3d-models/camcorder-video-camera-002d346e9faa41b6b8075feced9e0ba7

## Remove

Close the game and remove `Mods/RoweMod/Bundles/rowemod_custom_emotes`.
The game's stock emotes remain available.

## Validation

The packaged bundle is a byte-for-byte copy of the locally installed bundle.
The archive's folder layout, Unity bundle signature, and SHA-256 are checked
before publication. This is not a new in-game animation acceptance test.
