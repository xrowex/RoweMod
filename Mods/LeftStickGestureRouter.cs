using System;
using System.Collections;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace rowemod.Mods
{
    /// <summary>
    /// Owns the left-stick click so a short camera tap and a stance hold cannot
    /// both fire from the same press.
    /// </summary>
    internal static class LeftStickGestureRouter
    {
        private const float StanceHoldSeconds = 0.5f;

        private static Il2CppSystem.Action _afterInputUpdateHandler;
        private static bool _initialized;
        // onAfterUpdate runs after the Input System has processed its event
        // batch. Track the persistent control value ourselves rather than
        // relying on per-frame flags at that point.
        private static bool _buttonWasPressed;
        private static bool _pressed;
        private static bool _consumed;
        private static float _pressedAt;
        private static int _gestureGeneration;

        public static string Status
        {
            get
            {
                if (!_initialized)
                    return "Left-stick gesture input is unavailable.";
                if (_pressed && !_consumed)
                {
                    float remaining = Mathf.Max(
                        0f,
                        StanceHoldSeconds - (Time.unscaledTime - _pressedAt));
                    return $"Hold for stance: {remaining:0.0}s";
                }

                return "Tap LS for camera; hold LS for 0.5s to switch bike stance.";
            }
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                _afterInputUpdateHandler ??=
                    Il2CppInterop.Runtime.DelegateSupport
                        .ConvertDelegate<Il2CppSystem.Action>(ProcessInputUpdate);
                InputSystem.add_onAfterUpdate(_afterInputUpdateHandler);
                _initialized = true;
                Log.Msg("[LeftStickGesture] Tap camera / hold stance input initialized.");
            }
            catch (Exception ex)
            {
                Cleanup();
                Log.Error($"[LeftStickGesture] Input initialization failed: {ex.Message}");
            }
        }

        public static void OnSceneInitialized()
        {
            Cancel();
        }

        public static void Cancel()
        {
            _gestureGeneration++;
            _pressed = false;
            _consumed = false;
            _pressedAt = 0f;
        }

        public static void Cleanup()
        {
            Cancel();

            if (_afterInputUpdateHandler != null)
            {
                try
                {
                    InputSystem.remove_onAfterUpdate(_afterInputUpdateHandler);
                }
                catch
                {
                    // The input system may already be shutting down.
                }
            }

            _initialized = false;
            _buttonWasPressed = false;
        }

        private static void ProcessInputUpdate()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad?.leftStickButton == null)
            {
                if (_pressed)
                    Cancel();
                _buttonWasPressed = false;
                return;
            }

            bool buttonIsPressed = gamepad.leftStickButton.isPressed;
            if (buttonIsPressed == _buttonWasPressed)
                return;

            _buttonWasPressed = buttonIsPressed;
            if (buttonIsPressed)
                OnStarted();
            else
                OnCanceled();
        }

        private static void OnStarted()
        {
            if (_pressed)
                return;

            if (!CanAcceptGameplayInput())
            {
                Cancel();
                return;
            }

            _gestureGeneration++;
            int generation = _gestureGeneration;
            _pressed = true;
            _consumed = false;
            _pressedAt = Time.unscaledTime;
            MelonCoroutines.Start(WaitForHoldThreshold(generation));
        }

        private static void OnCanceled()
        {
            if (!_pressed)
                return;

            float heldSeconds = Time.unscaledTime - _pressedAt;
            bool wasConsumed = _consumed;

            if (!wasConsumed && heldSeconds >= StanceHoldSeconds)
                wasConsumed = TrySwitchBikeStance();

            _gestureGeneration++;
            _pressed = false;
            _consumed = false;
            _pressedAt = 0f;

            if (wasConsumed || !CanAcceptGameplayInput())
                return;

            if (Config.cameraSettings?.leftStickOffsetSwitch == true)
                Camera.FlipCameraOffset();
        }

        private static IEnumerator WaitForHoldThreshold(int generation)
        {
            yield return new WaitForSecondsRealtime(StanceHoldSeconds);

            if (!_pressed || _consumed || generation != _gestureGeneration)
                yield break;
            if (!CanAcceptGameplayInput())
            {
                Cancel();
                yield break;
            }

            _consumed = TrySwitchBikeStance();
        }

        private static bool TrySwitchBikeStance()
        {
            BikeOnlyStanceSettings settings = Config.bikeOnlyStanceSettings;
            if (settings == null ||
                !settings.enabled ||
                !settings.holdLeftStickToSwitchStance ||
                !BikeOnlyStance.CanSwitchFromInput())
            {
                return false;
            }

            settings.goofy = !settings.goofy;
            BikeOnlyStance.NotifySettingsChanged();
            Config.RequestSave();
            Log.Msg(
                $"[LeftStickGesture] Bike stance switched to " +
                $"{(settings.goofy ? "Goofy" : "Regular")}; camera tap consumed.");
            return true;
        }

        private static bool CanAcceptGameplayInput()
        {
            return RemoteKillSwitched.isModEnabled &&
                   !SteamUserManager.LastAccessDeniedByBan &&
                   Main.playableSceneLoaded &&
                   Main.IsGameplayInputActive &&
                   Memory.rMbCharacter != null &&
                   Gamepad.current != null &&
                   !Menu.isOpen &&
                   !PieMenu.IsOpen;
        }
    }
}
