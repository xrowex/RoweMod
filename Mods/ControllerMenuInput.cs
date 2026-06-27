using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using rowemod.Utils;

namespace rowemod.Mods
{
    public static class ControllerMenuInput
    {
        private const float ScrollSpeed = 720f;
        private const float StickDeadzone = 0.35f;
        private const float TabRepeatDelay = 0.22f;
        private static readonly string[] GameplayInputMaps =
        {
            "BMX",
            "Tricking",
            "Freerunning",
            "Ragdoll",
            "Drone"
        };

        private static readonly Dictionary<InputActionMap, bool> BlockedInputMaps = new Dictionary<InputActionMap, bool>();
        private static float nextTabInputTime;
        private static bool playerInputBehaviourBlocked;

        public static void Update()
        {
            if (!Menu.isOpen || PieMenu.IsOpen)
                return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return;

            if (gamepad.buttonEast.wasPressedThisFrame)
            {
                Main.CloseRoweModMenu();
                return;
            }

            if (gamepad.leftShoulder.isPressed && Time.unscaledTime >= nextTabInputTime)
            {
                nextTabInputTime = Time.unscaledTime + TabRepeatDelay;
                Menu.SelectPreviousVisibleTab();
            }

            if (gamepad.rightShoulder.isPressed && Time.unscaledTime >= nextTabInputTime)
            {
                nextTabInputTime = Time.unscaledTime + TabRepeatDelay;
                Menu.SelectNextVisibleTab();
            }

            HandleScroll(gamepad);
        }

        private static void HandleScroll(Gamepad gamepad)
        {
            float stickY = gamepad.leftStick.ReadValue().y;
            float scrollInput = Mathf.Abs(stickY) >= StickDeadzone
                ? stickY
                : gamepad.dpad.ReadValue().y;

            if (scrollInput != 0f)
                Menu.AdjustControllerScroll(-scrollInput * ScrollSpeed * Time.unscaledDeltaTime);
        }

        public static void SetGameplayInputBlocked(bool blocked)
        {
            SetPlayerInputBehaviourBlocked(blocked);

            foreach (InputActionAsset asset in UnityEngine.Object.FindObjectsOfType<InputActionAsset>())
            {
                if (asset == null)
                    continue;

                foreach (string mapName in GameplayInputMaps)
                {
                    InputActionMap map = asset.FindActionMap(mapName, false);
                    if (map == null)
                        continue;

                    if (blocked)
                    {
                        if (!BlockedInputMaps.ContainsKey(map))
                            BlockedInputMaps[map] = map.enabled;

                        if (map.enabled)
                            map.Disable();
                    }
                    else if (BlockedInputMaps.TryGetValue(map, out bool wasEnabled) && wasEnabled)
                    {
                        map.Enable();
                    }
                }
            }

            if (!blocked)
                BlockedInputMaps.Clear();
        }

        private static void SetPlayerInputBehaviourBlocked(bool blocked)
        {
            try
            {
                if (Memory.playerInputBehaviour == null)
                    return;

                if (blocked)
                {
                    if (playerInputBehaviourBlocked)
                        return;

                    Memory.playerInputBehaviour.UnRegisterInputs();
                    playerInputBehaviourBlocked = true;
                    Log.Msg("[ControllerMenuInput] PlayerInputBehaviour inputs unregistered.");
                    return;
                }

                if (!playerInputBehaviourBlocked)
                    return;

                Memory.playerInputBehaviour.InitializeInputs();
                playerInputBehaviourBlocked = false;
                Log.Msg("[ControllerMenuInput] PlayerInputBehaviour inputs initialized.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ControllerMenuInput] Failed to {(blocked ? "block" : "restore")} PlayerInputBehaviour inputs: {ex.Message}");
            }
        }
    }
}
