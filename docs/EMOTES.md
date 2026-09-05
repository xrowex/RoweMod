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

## Remove

Close the game and remove `Mods/RoweMod/Bundles/rowemod_custom_emotes`.
The game's stock emotes remain available.

## Validation

The packaged bundle is a byte-for-byte copy of the locally installed bundle.
The archive's folder layout, Unity bundle signature, and SHA-256 are checked
before publication. This is not a new in-game animation acceptance test.
