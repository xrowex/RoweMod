using System.Collections;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    // Spawn, gameplay-enter, and replay-exit can arrive for the same rider.
    // Restore the current selection once those requests settle, never a stale preset.
    internal static class GameplayCharacterOutfit
    {
        private static int _generation;

        internal static IEnumerator RestoreWhenReady()
        {
            int generation = ++_generation;
            GameObject character = Memory.gamePlayer;
            long revision = Custom.SelectionRevision;
            yield return new WaitForSecondsRealtime(4f);

            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (generation != _generation || character == null ||
                    character != Memory.gamePlayer || !Main.playableSceneLoaded ||
                    Main.IsGameMainMenuActive || !RemoteKillSwitched.isModEnabled ||
                    revision != Custom.SelectionRevision || Custom.NativeRestoreRunning)
                    yield break;

                GameObject root = Custom.FindCharacterRoot(character);
                if (root != null)
                {
                    Custom.RestoreCurrentSelection(root);
                    Log.Msg($"[CharacterOutfit] Restored current selection on local gameplay rider '{root.name}'.");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Log.Warning("[CharacterOutfit] Local gameplay equip slots were not ready; outfit restoration skipped.");
        }
    }
}
