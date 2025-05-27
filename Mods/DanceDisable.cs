using Il2CppMashBox.Core.Runtime.Input;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using InputManager = Il2CppMashBox.Core.Runtime.Input.InputManager;
using Object = UnityEngine.Object;

namespace rowemod.Mods
{
    public class DanceDisableMod : MelonMod
    {
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            MelonCoroutines.Start(DisableEmoteInput());
        }

        private System.Collections.IEnumerator DisableEmoteInput()
        {
            yield return new WaitForSeconds(1f); // Wait for scene + input system to initialize

            var inputManager = Object.FindObjectOfType<InputManager>();
            if (inputManager == null)
            {
                MelonLogger.Warning("InputManager not found.");
                yield break;
            }

            var inputAsset = inputManager._gameplayInputActionAsset;
            if (inputAsset == null)
            {
                MelonLogger.Warning("InputActionAsset is null.");
                yield break;
            }

            // Try find "General/Emote" action
            InputAction emoteAction = inputAsset.FindAction("Emote", true);
            if (emoteAction != null)
            {
                emoteAction.Disable(); // disable it at runtime
                MelonLogger.Msg("Emote action disabled.");
            }
            else
            {
                MelonLogger.Warning("Couldn't find 'General/Emote' action.");
            }
        }
    }
}