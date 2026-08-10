using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using rowemod.Utils;
using GameInputManager = Il2CppMashBox.Core.Runtime.Input.InputManager;
using PlayerInputBehaviour = Il2CppMashBox.BMX_Physics_Development.NewShit.PlayerInputBehaviour;

namespace rowemod.Mods
{
    public static class ControllerMenuInput
    {
        private const float ScrollSpeed = 760f;
        private const float StickDeadzone = 0.55f;
        private const float InitialRepeatDelay = 0.34f;
        private const float RepeatInterval = 0.10f;
        private const float CaptureRefreshInterval = 0.50f;
        private const float AuxiliaryAssetRefreshInterval = 2f;
        private const float NavigationButtonPressThreshold = 0.65f;
        private const float NavigationButtonReleaseThreshold = 0.25f;

        private static readonly string[] GameplayInputMaps =
        {
            "BMX",
            "Tricking",
            "Freerunning",
            "Ragdoll",
            "Drone"
        };

        private static readonly Dictionary<InputActionMap, bool> BlockedInputMaps =
            new Dictionary<InputActionMap, bool>();

        private static int lastVerticalDirection;
        private static int lastHorizontalDirection;
        private static float nextVerticalRepeatTime;
        private static float nextHorizontalRepeatTime;
        private static bool captureRequested;
        private static float nextCaptureRefreshTime;
        private static float nextAuxiliaryAssetRefreshTime;
        private static PlayerInputBehaviour blockedPlayerInputBehaviour;
        private static GameInputManager cachedInputManager;
        private static bool waitForDpadRightRelease;
        private static bool leftShoulderHeld;
        private static bool rightShoulderHeld;
        private static bool leftTriggerHeld;
        private static bool rightTriggerHeld;
        private static float nextVehicleNavigationDiagnosticTime;
        // Vehicle Tuning is a separate IMGUI overlay.  Do not share its repeat state with
        // the main menu/pie handoff: a held input during that handoff could mark the first
        // direction as already handled and leave the overlay focused on nothing.
        private static int lastVehicleVerticalDirection;
        private static int lastVehicleHorizontalDirection;
        private static float nextVehicleVerticalRepeatTime;
        private static float nextVehicleHorizontalRepeatTime;

        public static void Update()
        {
            bool vehicleTuningOpen = RuntimeVehicleTuneResetSupport.IsOpen;
            if (!Menu.isOpen && !vehicleTuningOpen)
                return;

            EnsureGameplayInputBlocked();
            if (PieMenu.IsOpen)
                return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return;

            if (ObjectDropper.IsPlacementMode)
            {
                ObjectDropper.UpdatePlacementController(gamepad);
                return;
            }

            if (waitForDpadRightRelease)
            {
                if (gamepad.dpad.right.isPressed)
                    return;

                waitForDpadRightRelease = false;
                ResetNavigationRepeatState();
            }

            if (vehicleTuningOpen)
            {
                if (gamepad.buttonEast.wasPressedThisFrame)
                {
                    if (RuntimeVehicleTuneResetSupport.TryCancelNumericEditor())
                        return;

                    RuntimeVehicleTuneResetSupport.CloseInspector();
                    return;
                }

                if (gamepad.buttonSouth.wasPressedThisFrame)
                    Menu.RequestControllerActivation();

                ProcessVehicleTuningDirections(gamepad);
                return;
            }

            if (gamepad.buttonEast.wasPressedThisFrame)
            {
                if (TrickAnimationEditor.TryCloseControllerPicker() || TrickMods.TryCloseControllerPicker())
                {
                    Menu.ResetControllerNavigation();
                    return;
                }

                Main.CloseRoweModMenu();
                return;
            }

            bool pickerOpen = TrickAnimationEditor.IsControllerPickerOpen || TrickMods.IsControllerPickerOpen;
            bool leftShoulderPressed = ReadReleaseGatedPress(gamepad.leftShoulder.ReadValue(), ref leftShoulderHeld);
            bool rightShoulderPressed = ReadReleaseGatedPress(gamepad.rightShoulder.ReadValue(), ref rightShoulderHeld);
            bool leftTriggerPressed = ReadReleaseGatedPress(gamepad.leftTrigger.ReadValue(), ref leftTriggerHeld);
            bool rightTriggerPressed = ReadReleaseGatedPress(gamepad.rightTrigger.ReadValue(), ref rightTriggerHeld);
            if (!pickerOpen)
            {
                if (leftShoulderPressed)
                    Menu.SelectPreviousVisibleTab();
                if (rightShoulderPressed)
                    Menu.SelectNextVisibleTab();
                if (leftTriggerPressed)
                    Menu.SelectPreviousPage();
                if (rightTriggerPressed)
                    Menu.SelectNextPage();
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
                Menu.RequestControllerActivation();

            ProcessMenuDirections(gamepad, false);
        }

        private static void ProcessMenuDirections(Gamepad gamepad, bool vehicleTuningOpen)
        {
            Vector2 dpad = gamepad.dpad.ReadValue();
            Vector2 stick = gamepad.leftStick.ReadValue();
            int verticalDirection = ReadAxisDirection(dpad.y, stick.y);
            int horizontalDirection = ReadAxisDirection(dpad.x, stick.x);

            if (vehicleTuningOpen && (verticalDirection != 0 || horizontalDirection != 0) &&
                Time.unscaledTime >= nextVehicleNavigationDiagnosticTime)
            {
                nextVehicleNavigationDiagnosticTime = Time.unscaledTime + 0.25f;
                Log.Msg(
                    $"[VehicleTuningController] input dpad=({dpad.x:F2},{dpad.y:F2}) " +
                    $"leftStick=({stick.x:F2},{stick.y:F2}) directions=({horizontalDirection},{verticalDirection}) " +
                    $"targets={Menu.ControllerTargetCount} focus={Menu.ControllerFocusedTargetId}.");
            }

            ProcessRepeatingDirection(
                verticalDirection,
                ref lastVerticalDirection,
                ref nextVerticalRepeatTime,
                direction =>
                {
                    int focusDirection = -direction;
                    if (vehicleTuningOpen)
                        Menu.MoveControllerFocus(focusDirection);
                    else
                        Menu.MoveControllerFocus(focusDirection);
                });
            ProcessRepeatingDirection(
                horizontalDirection,
                ref lastHorizontalDirection,
                ref nextHorizontalRepeatTime,
                Menu.AdjustControllerFocusedControl);

            float scrollInput = gamepad.rightStick.ReadValue().y;
            if (Mathf.Abs(scrollInput) >= StickDeadzone)
                Menu.AdjustControllerScroll(-scrollInput * ScrollSpeed * Time.unscaledDeltaTime);
        }

        private static void ProcessVehicleTuningDirections(Gamepad gamepad)
        {
            Vector2 dpad = gamepad.dpad.ReadValue();
            Vector2 stick = gamepad.leftStick.ReadValue();
            int verticalDirection = ReadAxisDirection(dpad.y, stick.y);
            int horizontalDirection = ReadAxisDirection(dpad.x, stick.x);

            if ((verticalDirection != 0 || horizontalDirection != 0) &&
                Time.unscaledTime >= nextVehicleNavigationDiagnosticTime)
            {
                nextVehicleNavigationDiagnosticTime = Time.unscaledTime + 0.25f;
                Log.Msg(
                    $"[VehicleTuningController] input dpad=({dpad.x:F2},{dpad.y:F2}) " +
                    $"leftStick=({stick.x:F2},{stick.y:F2}) directions=({horizontalDirection},{verticalDirection}) " +
                    $"targets={Menu.ControllerTargetCount} focus={Menu.ControllerFocusedTargetId}.");
            }

            ProcessRepeatingDirection(
                verticalDirection,
                ref lastVehicleVerticalDirection,
                ref nextVehicleVerticalRepeatTime,
                direction =>
                {
                    // Down moves forward through the registered rows. Keep the focused
                    // control fully visible as selection advances; this is deliberately
                    // non-wrapping, so reaching the final row cannot snap back to the top.
                    Menu.MoveControllerOverlayFocus(-direction, float.PositiveInfinity);
                    Log.Msg($"[VehicleTuningController] move vertical={direction}; focus={Menu.ControllerFocusedTargetId}.");
                });

            ProcessRepeatingDirection(
                horizontalDirection,
                ref lastVehicleHorizontalDirection,
                ref nextVehicleHorizontalRepeatTime,
                direction =>
                {
                    Menu.AdjustControllerFocusedControl(direction);
                    Log.Msg($"[VehicleTuningController] adjust horizontal={direction}; focus={Menu.ControllerFocusedTargetId}.");
                });

            float scrollInput = gamepad.rightStick.ReadValue().y;
            if (Mathf.Abs(scrollInput) >= StickDeadzone)
                Menu.AdjustControllerFocusedScroll(-scrollInput * ScrollSpeed * Time.unscaledDeltaTime);
        }

        private static bool ReadReleaseGatedPress(float value, ref bool held)
        {
            // Require a real release before accepting another navigation step. The separate
            // thresholds prevent noisy analog triggers (and bouncing shoulder switches) from
            // producing two page changes during one physical press.
            if (held)
            {
                if (value <= NavigationButtonReleaseThreshold)
                    held = false;

                return false;
            }

            if (value < NavigationButtonPressThreshold)
                return false;

            held = true;
            return true;
        }

        public static void SuppressNavigationUntilDpadRightRelease()
        {
            waitForDpadRightRelease = true;
            ResetNavigationRepeatState();
        }

        public static void ResumeNavigationForOverlay()
        {
            // The pie uses this release gate to reserve D-pad Right while it opens. Once an
            // owned overlay (Vehicle Tuning) has taken over, keeping that gate can swallow the
            // menu's first D-pad/left-stick navigation entirely. Gameplay maps remain blocked.
            waitForDpadRightRelease = false;
            ResetNavigationRepeatState();
            ResetVehicleTuningNavigationState();
        }

        private static int ReadAxisDirection(float digitalValue, float analogValue)
        {
            if (Mathf.Abs(digitalValue) > 0.5f)
                return digitalValue > 0f ? 1 : -1;
            if (Mathf.Abs(analogValue) >= StickDeadzone)
                return analogValue > 0f ? 1 : -1;
            return 0;
        }

        private static void ProcessRepeatingDirection(
            int direction,
            ref int lastDirection,
            ref float nextRepeatTime,
            Action<int> action)
        {
            if (direction == 0)
            {
                lastDirection = 0;
                nextRepeatTime = 0f;
                return;
            }

            float now = Time.unscaledTime;
            if (direction != lastDirection)
            {
                lastDirection = direction;
                nextRepeatTime = now + InitialRepeatDelay;
                action(direction);
                return;
            }

            if (now < nextRepeatTime)
                return;

            nextRepeatTime = now + RepeatInterval;
            action(direction);
        }

        public static void SetGameplayInputBlocked(bool blocked)
        {
            // RoweMod has three controller-owned surfaces: the main window, the pie, and
            // Vehicle Tuning. They can hand off to one another in the same frame. Do not let
            // the surface that just closed restore BMX input while another is still visible.
            // This was most visible when Vehicle Tuning opened from RoweMod: the main menu's
            // close path immediately re-enabled all gameplay maps behind the tuning window.
            if (!blocked && (Menu.isOpen || RuntimeVehicleTuneResetSupport.IsOpen || PieMenu.IsOpen))
            {
                captureRequested = true;
                nextCaptureRefreshTime = 0f;
                nextAuxiliaryAssetRefreshTime = 0f;
                EnsureGameplayInputBlocked();
                return;
            }

            captureRequested = blocked;
            if (blocked)
            {
                nextCaptureRefreshTime = 0f;
                nextAuxiliaryAssetRefreshTime = 0f;
                EnsureGameplayInputBlocked();
                return;
            }

            RestoreGameplayInput();
        }

        public static void OnSceneInitialized()
        {
            bool shouldRecapture = Menu.isOpen || RuntimeVehicleTuneResetSupport.IsOpen;
            captureRequested = false;
            RestoreGameplayInput();
            cachedInputManager = null;
            waitForDpadRightRelease = false;
            ResetNavigationRepeatState();
            ResetVehicleTuningNavigationState();

            if (shouldRecapture)
            {
                captureRequested = true;
                nextCaptureRefreshTime = 0f;
            }
        }

        public static void Shutdown()
        {
            captureRequested = false;
            RestoreGameplayInput();
            waitForDpadRightRelease = false;
            ResetNavigationRepeatState();
            ResetVehicleTuningNavigationState();
        }

        private static void EnsureGameplayInputBlocked()
        {
            if (!captureRequested)
                return;

            float now = Time.unscaledTime;
            bool refreshDue = now >= nextCaptureRefreshTime;
            if (!refreshDue)
            {
                CapturePlayerInputBehaviour(false);
                return;
            }

            nextCaptureRefreshTime = now + CaptureRefreshInterval;
            int newlyCapturedMaps = 0;

            try
            {
                // This is the authoritative asset used by BMX Streets. Capture every map in
                // it so controls such as pause/replay do not leak through while RoweMod owns
                // the controller. Raw Gamepad reads used by this menu keep working.
                if (cachedInputManager == null)
                    cachedInputManager = UnityEngine.Object.FindObjectOfType<GameInputManager>();

                InputActionAsset gameplayAsset = cachedInputManager?._gameplayInputActionAsset;
                if (gameplayAsset != null)
                {
                    foreach (InputActionMap map in gameplayAsset.actionMaps)
                        newlyCapturedMaps += CaptureAndDisableMap(map);
                }

                // Some vehicle or mode-specific maps can live on separate assets. Keep the
                // narrow name scan as a low-frequency fallback and de-duplicate every map.
                if (now >= nextAuxiliaryAssetRefreshTime)
                {
                    nextAuxiliaryAssetRefreshTime = now + AuxiliaryAssetRefreshInterval;
                    foreach (InputActionAsset asset in UnityEngine.Object.FindObjectsOfType<InputActionAsset>())
                    {
                        if (asset == null)
                            continue;

                        foreach (string mapName in GameplayInputMaps)
                        {
                            InputActionMap map = asset.FindActionMap(mapName, false);
                            if (map == null)
                                continue;

                            newlyCapturedMaps += CaptureAndDisableMap(map);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ControllerMenuInput] Failed to capture gameplay action maps: {ex.Message}");
            }

            // Capture the action-map enabled states before UnRegisterInputs changes them. This
            // ordering is what lets RestoreGameplayInput put the controller back exactly as it
            // was when B closes RoweMod.
            CapturePlayerInputBehaviour(true);

            if (newlyCapturedMaps > 0)
                Log.Msg($"[ControllerMenuInput] Captured {newlyCapturedMaps} gameplay action map(s).");
        }

        private static int CaptureAndDisableMap(InputActionMap map)
        {
            if (map == null)
                return 0;

            bool newlyCaptured = !BlockedInputMaps.ContainsKey(map);
            if (newlyCaptured)
                BlockedInputMaps[map] = map.enabled;

            if (map.enabled)
                map.Disable();

            return newlyCaptured ? 1 : 0;
        }

        private static void CapturePlayerInputBehaviour(bool refreshExisting)
        {
            PlayerInputBehaviour currentPlayerInput = Memory.playerInputBehaviour;
            if (blockedPlayerInputBehaviour != null && blockedPlayerInputBehaviour != currentPlayerInput)
            {
                TryRestorePlayerInputBehaviour(blockedPlayerInputBehaviour);
                blockedPlayerInputBehaviour = null;
            }

            if (currentPlayerInput == null)
                return;

            try
            {
                if (blockedPlayerInputBehaviour == currentPlayerInput)
                {
                    // The game can rebuild its bindings after a state transition. Reassert the
                    // block on the low-frequency refresh, never as a per-frame polling task.
                    if (refreshExisting)
                        currentPlayerInput.UnRegisterInputs();
                    return;
                }

                currentPlayerInput.UnRegisterInputs();
                blockedPlayerInputBehaviour = currentPlayerInput;
                Log.Msg("[ControllerMenuInput] PlayerInputBehaviour inputs unregistered while RoweMod is open.");
            }
            catch (Exception ex)
            {
                Log.Error($"[ControllerMenuInput] Failed to block PlayerInputBehaviour: {ex.Message}");
            }
        }

        private static void RestoreGameplayInput()
        {
            if (blockedPlayerInputBehaviour != null)
            {
                TryRestorePlayerInputBehaviour(blockedPlayerInputBehaviour);
                blockedPlayerInputBehaviour = null;
            }

            foreach (KeyValuePair<InputActionMap, bool> pair in BlockedInputMaps)
            {
                try
                {
                    InputActionMap map = pair.Key;
                    if (map == null)
                        continue;

                    if (pair.Value)
                    {
                        if (!map.enabled)
                            map.Enable();
                    }
                    else if (map.enabled)
                    {
                        map.Disable();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ControllerMenuInput] Failed to restore an input map: {ex.Message}");
                }
            }

            if (BlockedInputMaps.Count > 0)
                Log.Msg($"[ControllerMenuInput] Restored {BlockedInputMaps.Count} gameplay action map(s).");

            BlockedInputMaps.Clear();
            nextCaptureRefreshTime = 0f;
            nextAuxiliaryAssetRefreshTime = 0f;
        }

        private static void TryRestorePlayerInputBehaviour(PlayerInputBehaviour playerInput)
        {
            try
            {
                if (playerInput == null)
                    return;

                // Use the same clean unregister/register sequence the game uses during its own
                // state transitions. Calling InitializeInputs alone can leave stale callback
                // registrations after the menu-owned capture.
                playerInput.UnRegisterInputs();
                playerInput.InitializeInputs();
                ReplayInputPatch.EnsureDpadReservation("gameplay input restored after RoweMod menu close");
                Log.Msg("[ControllerMenuInput] PlayerInputBehaviour inputs restored.");
            }
            catch (Exception ex)
            {
                Log.Error($"[ControllerMenuInput] Failed to restore PlayerInputBehaviour: {ex.Message}");
            }
        }

        private static void ResetNavigationRepeatState()
        {
            lastVerticalDirection = 0;
            lastHorizontalDirection = 0;
            nextVerticalRepeatTime = 0f;
            nextHorizontalRepeatTime = 0f;
            leftShoulderHeld = false;
            rightShoulderHeld = false;
            leftTriggerHeld = false;
            rightTriggerHeld = false;
        }

        private static void ResetVehicleTuningNavigationState()
        {
            lastVehicleVerticalDirection = 0;
            lastVehicleHorizontalDirection = 0;
            nextVehicleVerticalRepeatTime = 0f;
            nextVehicleHorizontalRepeatTime = 0f;
        }
    }
}
