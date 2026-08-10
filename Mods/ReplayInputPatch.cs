using System;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using GameEvent = Il2CppMashBox.Core.Runtime.Events.GameEvent;
using NativeGameLoopManager = Il2CppMashBox.Addons.GameLoop.GameLoopManager;
using NativeReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;
using NativeGameState = Il2CppMashBoxBridge.Common.Sys.GameState;
using NativeMenuService = Il2CppMashBoxBridge.Common.Sys.MenuService;

namespace rowemod.Mods
{
    /// <summary>
    /// Owns RoweMod's D-pad Right action and reserves the native replay trigger.
    ///
    /// GameLoopManager binds every legacy transition GameEvent to a stored
    /// Il2CppSystem.Action. Removing only the action stored for OpenReplay leaves
    /// the rest of the game's input maps and transition events untouched. The pie
    /// menu can still request the Replay state intentionally through GameLoopManager.
    /// </summary>
    public static class ReplayInputPatch
    {
        private const string PieMenuActionName = "RoweMod/PieMenu";
        private const string PieMenuBindingPath = "<Gamepad>/dpad/right";
        private const string OpenReplayEventName =
            "GameEvent_TitleLoop_TransitionTrigger_OpenReplay";
        private const float DiscoveryRetrySeconds = 0.5f;

        private static InputAction pieMenuAction;
        private static NativeGameLoopManager cachedGameLoopManager;
        private static GameEvent cachedOpenReplayEvent;
        private static Il2CppSystem.Action cachedOpenReplayHandler;
        private static bool initialized;
        private static bool sceneAllowsGameplayInput;
        private static bool pieMenuActionEnabled;
        private static bool replayListenerDetached;
        private static bool actionStateLogged;
        private static bool suppressionLogged;
        private static bool discoveryFailureLogged;
        private static bool gameplayGateFailureLogged;
        private static float nextDiscoveryTime;

        public static bool IsPieMenuActionPressed
        {
            get
            {
                if (!pieMenuActionEnabled || pieMenuAction == null)
                    return false;

                try
                {
                    return pieMenuAction.IsPressed();
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool CanUsePieMenuShortcut => HasLiveGameplayContext();

        public static void InitializeDpadReservation()
        {
            if (initialized)
                return;

            initialized = true;
            CreatePieMenuAction();
            nextDiscoveryTime = 0f;
            Log.Msg(
                "[RoweModInput] Dedicated pie-menu action initialized for " +
                "<Gamepad>/dpad/right; waiting for GameLoopManager OpenReplay binding.");
        }

        public static void OnSceneInitialized(bool isGameplayScene)
        {
            sceneAllowsGameplayInput = isGameplayScene;
            actionStateLogged = false;
            discoveryFailureLogged = false;
            nextDiscoveryTime = 0f;
            UpdatePieMenuActionState();

            if (!isGameplayScene)
            {
                RestoreReplayListener("left gameplay scene");
                ClearCachedReplayBinding();
            }
        }

        public static void OnLocalPlayerSpawned()
        {
            sceneAllowsGameplayInput = true;
            UpdatePieMenuActionState();
            ValidateReplaySuppression("local player spawn");
        }

        public static void EnsureDpadReservation(string reason)
        {
            if (!initialized)
                return;

            UpdatePieMenuActionState();
            if (HasLoadedMapContext())
                ValidateReplaySuppression(reason);
        }

        /// <summary>
        /// Opens replay through GameLoopManager's public replay-state transition.
        /// Native SetGameStateToReplay performs the same State(Replay) ->
        /// RequestTransition path used by the legacy OpenReplay event, without a
        /// temporary listener reattachment or re-entrancy window.
        /// </summary>
        public static bool TryOpenReplayFromPieMenu()
        {
            if (!initialized || !HasLiveGameplayContext())
                return false;

            ValidateReplaySuppression("pie Replay selected");
            if (!replayListenerDetached || cachedOpenReplayEvent == null ||
                cachedOpenReplayHandler == null)
            {
                Log.Warning(
                    "[RoweModInput] Replay was not opened because GameLoopManager's " +
                    "OpenReplay listener could not be safely authorized.");
                return false;
            }

            NativeGameLoopManager manager = NativeGameLoopManager.Instance;
            if (manager == null)
            {
                Log.Warning("[RoweModInput] GameLoopManager.Instance is null.");
                return false;
            }

            try
            {
                manager.SetGameStateToReplay();
                Log.Msg(
                    "[RoweModInput] Requested the Replay state directly through GameLoopManager " +
                    "from the pie menu.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RoweModInput] GameLoopManager replay transition failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called from Main.Update. In steady state this only compares the desired
        /// enabled state of one RoweMod InputAction. GameEvent discovery stops after
        /// the exact GameLoopManager handler has been found and removed.
        /// </summary>
        public static void TickDpadReservation()
        {
            if (!initialized)
                return;

            UpdatePieMenuActionState();

            if (!HasLoadedMapContext())
                return;

            if (replayListenerDetached && cachedOpenReplayEvent != null &&
                cachedOpenReplayHandler != null && cachedGameLoopManager != null)
            {
                return;
            }

            if (Time.unscaledTime < nextDiscoveryTime)
                return;

            ValidateReplaySuppression("deferred discovery");
            if (!replayListenerDetached)
                nextDiscoveryTime = Time.unscaledTime + DiscoveryRetrySeconds;
        }

        public static void Cleanup()
        {
            SetPieMenuActionEnabled(false);
            RestoreReplayListener("cleanup");

            if (pieMenuAction != null)
            {
                try
                {
                    pieMenuAction.Dispose();
                }
                catch
                {
                    // InputSystem can already be shutting down.
                }
            }

            pieMenuAction = null;
            cachedGameLoopManager = null;
            cachedOpenReplayEvent = null;
            cachedOpenReplayHandler = null;
            initialized = false;
            sceneAllowsGameplayInput = false;
            pieMenuActionEnabled = false;
            replayListenerDetached = false;
            actionStateLogged = false;
            suppressionLogged = false;
            discoveryFailureLogged = false;
            gameplayGateFailureLogged = false;
            nextDiscoveryTime = 0f;
        }

        private static void CreatePieMenuAction()
        {
            if (pieMenuAction != null)
                return;

            try
            {
                pieMenuAction = new InputAction(
                    PieMenuActionName,
                    InputActionType.Button,
                    PieMenuBindingPath,
                    null,
                    null,
                    "Button");
            }
            catch (Exception ex)
            {
                Log.Error($"[RoweModInput] Could not create the pie-menu InputAction: {ex.Message}");
            }
        }

        private static void UpdatePieMenuActionState()
        {
            bool shouldEnable = HasLiveGameplayContext();
            SetPieMenuActionEnabled(shouldEnable);
        }

        private static void SetPieMenuActionEnabled(bool enable)
        {
            if (pieMenuAction == null)
            {
                pieMenuActionEnabled = false;
                return;
            }

            if (pieMenuActionEnabled == enable && pieMenuAction.enabled == enable)
                return;

            try
            {
                if (enable)
                    pieMenuAction.Enable();
                else
                    pieMenuAction.Disable();

                pieMenuActionEnabled = enable;
                if (enable && !actionStateLogged)
                {
                    actionStateLogged = true;
                    Log.Msg(
                        "[RoweModInput] Pie-menu InputAction enabled for live gameplay; " +
                        "it remains disabled in title, main, loading, pause, and settings menus.");
                }
            }
            catch (Exception ex)
            {
                pieMenuActionEnabled = false;
                Log.Warning($"[RoweModInput] Could not change the pie-menu InputAction state: {ex.Message}");
            }
        }

        private static void ValidateReplaySuppression(string reason)
        {
            if (!initialized || !HasLoadedMapContext())
                return;

            try
            {
                NativeGameLoopManager manager = NativeGameLoopManager.Instance;
                if (manager == null)
                    return;

                bool managerChanged =
                    cachedGameLoopManager != null &&
                    cachedGameLoopManager.Pointer != manager.Pointer;

                if (managerChanged)
                {
                    // The old manager owns its closure and removes it from OnDestroy.
                    // Never attach an old closure to a newly-created manager.
                    ClearCachedReplayBinding();
                }

                if (cachedGameLoopManager == null)
                    cachedGameLoopManager = manager;

                if (cachedOpenReplayEvent == null || cachedOpenReplayHandler == null)
                {
                    if (!TryResolveOpenReplayBinding(manager))
                        return;
                }

                Il2CppSystem.Action current = cachedOpenReplayEvent.OnRaise_A;
                if (DelegateChainContains(current, cachedOpenReplayHandler))
                {
                    cachedOpenReplayEvent.OnRaise_A -= cachedOpenReplayHandler;
                }

                replayListenerDetached =
                    !DelegateChainContains(cachedOpenReplayEvent.OnRaise_A, cachedOpenReplayHandler);

                if (replayListenerDetached && !suppressionLogged)
                {
                    suppressionLogged = true;
                    discoveryFailureLogged = false;
                    Log.Msg(
                        $"[RoweModInput] Detached only GameLoopManager's '{OpenReplayEventName}' " +
                        $"legacy listener ({reason}). Native D-pad replay is blocked; pie Replay remains available.");
                }
            }
            catch (Exception ex)
            {
                replayListenerDetached = false;
                if (!discoveryFailureLogged)
                {
                    discoveryFailureLogged = true;
                    Log.Warning($"[RoweModInput] Could not reserve the OpenReplay transition: {ex.Message}");
                }
            }
        }

        private static bool TryResolveOpenReplayBinding(NativeGameLoopManager manager)
        {
            var handlers = manager?._codeFlowRequestEventHandlers;
            if (handlers == null)
                return false;

            NativeReplaySystem replaySystem = NativeReplaySystem.Instance;
            GameEvent openReplayEvent = replaySystem?.OpenReplayRequest_GE;
            if (openReplayEvent == null)
                return false;

            Il2CppSystem.Action handler;
            if (handlers.TryGetValue(openReplayEvent, out handler) && handler != null)
            {
                cachedGameLoopManager = manager;
                cachedOpenReplayEvent = openReplayEvent;
                cachedOpenReplayHandler = handler;
                return true;
            }

            if (!discoveryFailureLogged)
            {
                discoveryFailureLogged = true;
                Log.Msg(
                    "[RoweModInput] GameLoopManager is ready, but its OpenReplay legacy " +
                    "binding is not populated yet; RoweMod will retry briefly.");
            }

            return false;
        }

        private static void RestoreReplayListener(string reason)
        {
            if (!replayListenerDetached || cachedOpenReplayEvent == null ||
                cachedOpenReplayHandler == null)
            {
                replayListenerDetached = false;
                return;
            }

            try
            {
                NativeGameLoopManager currentManager = NativeGameLoopManager.Instance;
                if (currentManager == null || cachedGameLoopManager == null ||
                    currentManager.Pointer != cachedGameLoopManager.Pointer)
                {
                    replayListenerDetached = false;
                    return;
                }

                var handlers = currentManager._codeFlowRequestEventHandlers;
                Il2CppSystem.Action mappedHandler;
                if (handlers == null ||
                    !handlers.TryGetValue(cachedOpenReplayEvent, out mappedHandler) ||
                    mappedHandler == null || mappedHandler.Pointer != cachedOpenReplayHandler.Pointer)
                {
                    replayListenerDetached = false;
                    return;
                }

                Il2CppSystem.Action current = cachedOpenReplayEvent.OnRaise_A;
                if (!DelegateChainContains(current, mappedHandler))
                    cachedOpenReplayEvent.OnRaise_A += mappedHandler;

                Log.Msg($"[RoweModInput] Restored GameLoopManager OpenReplay listener ({reason}).");
            }
            catch
            {
                // Scene-owned objects can already be gone during shutdown.
            }

            replayListenerDetached = false;
        }

        private static bool DelegateChainContains(
            Il2CppSystem.Action chain,
            Il2CppSystem.Action target)
        {
            if (chain == null || target == null)
                return false;

            try
            {
                var invocationList = chain.GetInvocationList();
                if (invocationList == null)
                    return false;

                for (int i = 0; i < invocationList.Length; i++)
                {
                    Il2CppSystem.Delegate invocation = invocationList[i];
                    if (invocation != null && invocation == target)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Fall back to the common single-cast case. The generated action
                // wrapper preserves native pointer identity for this stored handler.
                return chain.Pointer == target.Pointer;
            }

            return false;
        }

        private static void ClearCachedReplayBinding()
        {
            cachedGameLoopManager = null;
            cachedOpenReplayEvent = null;
            cachedOpenReplayHandler = null;
            replayListenerDetached = false;
            suppressionLogged = false;
            discoveryFailureLogged = false;
        }

        private static bool HasLiveGameplayContext()
        {
            if (!HasLoadedMapContext() || global::rowemod.Menu.isOpen)
                return false;

            try
            {
                NativeGameLoopManager manager = NativeGameLoopManager.Instance;
                bool available =
                    manager != null &&
                    manager.State == NativeGameState.Gameplay &&
                    NativeMenuService.CurrentGameplayMenuStackSize == 0;

                if (available)
                    gameplayGateFailureLogged = false;

                return available;
            }
            catch (Exception ex)
            {
                if (!gameplayGateFailureLogged)
                {
                    gameplayGateFailureLogged = true;
                    Log.Warning(
                        "[RoweModInput] Could not verify GameLoop/menu state; the pie shortcut " +
                        $"remains disabled: {ex.Message}");
                }

                return false;
            }
        }

        private static bool HasLoadedMapContext()
        {
            return sceneAllowsGameplayInput &&
                   global::rowemod.Main.playableSceneLoaded &&
                   Memory.playerInputBehaviour != null;
        }
    }
}
