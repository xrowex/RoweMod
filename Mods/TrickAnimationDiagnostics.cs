using System.Reflection;
using HarmonyLib;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using Il2CppMashBox.Core.Runtime.Common;
using Il2CppMashBox.Core.Runtime.Gameplay.ActivityTracking;
using Il2CppMashBox.Core.Runtime.TrickSystem;
using rowemod.Utils;
using UnityEngine;
using LegacyTrickLaunchState = Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.TrickLaunchState;
using LegacyTrickSystemBrain = Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.TrickSystemBrain;
using TrickAnimator = Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2.TrickAnimator;
using TrickInputController = Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.TrickInputController;

namespace rowemod.Mods
{
    internal static class TrickAnimationDiagnostics
    {
        private static int lastLoggedFrame = -1;
        private static string lastLoggedKey = string.Empty;
        private static int lastGameplayFrame = -1;
        private static string lastGameplayKey = string.Empty;
        private static float nextDiscoveryLogTime;
        private static string lastBrainStateKey = string.Empty;
        private static string lastInputStateKey = string.Empty;
        private static string lastSelectionStateKey = string.Empty;
        private static TrickSystemBrainV2 observedBrain;
        private static float nextBrainPollTime;
        private static int lastObservedFiredSetId = -999;
        private static int lastObservedFiredSlotId = -999;
        private static float lastObservedTimeSinceFire = 999f;
        private static bool installed;

        private static bool IsEnabled =>
            Config.trickAnimationDebugSettings == null ||
            Config.trickAnimationDebugSettings.enabled;

        public static void Install(global::HarmonyLib.Harmony harmony)
        {
            if (installed || harmony == null)
                return;

            int patched = 0;
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickControllerV2), nameof(TrickControllerV2.Fire)),
                AccessTools.Method(typeof(TrickControllerFirePatch), nameof(TrickControllerFirePatch.Postfix)),
                "TrickControllerV2.Fire");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickControllerV2), nameof(TrickControllerV2.TryBlendTo)),
                AccessTools.Method(typeof(TrickControllerTryBlendToPatch), nameof(TrickControllerTryBlendToPatch.Postfix)),
                "TrickControllerV2.TryBlendTo");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(LegacyTrickLaunchState), nameof(LegacyTrickLaunchState.PlayTrick)),
                AccessTools.Method(typeof(LegacyLaunchPlayTrickPatch), nameof(LegacyLaunchPlayTrickPatch.Postfix)),
                "Legacy TrickLaunchState.PlayTrick");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(PlayerTrickGameplay), nameof(PlayerTrickGameplay.PerformedTrick)),
                AccessTools.Method(typeof(PlayerTrickGameplayPerformedPatch), nameof(PlayerTrickGameplayPerformedPatch.Postfix)),
                "PlayerTrickGameplay.PerformedTrick");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(PlayerTrickGameplay), nameof(PlayerTrickGameplay.ConfirmCombo)),
                AccessTools.Method(typeof(PlayerTrickGameplayConfirmPatch), nameof(PlayerTrickGameplayConfirmPatch.Postfix)),
                "PlayerTrickGameplay.ConfirmCombo");
            patched += PatchPrefix(
                harmony,
                AccessTools.Method(typeof(ActivityTracker), nameof(ActivityTracker.RecordActivity)),
                AccessTools.Method(typeof(ActivityTrackerRecordPatch), nameof(ActivityTrackerRecordPatch.Prefix)),
                "ActivityTracker.RecordActivity");
            patched += PatchPrefix(
                harmony,
                AccessTools.Method(typeof(MasterActivityLog), nameof(MasterActivityLog.Log)),
                AccessTools.Method(typeof(MasterActivityLogPatch), nameof(MasterActivityLogPatch.Prefix)),
                "MasterActivityLog.Log");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickAnimator), nameof(TrickAnimator.PlayEnter)),
                AccessTools.Method(typeof(TrickAnimatorPlayEnterPatch), nameof(TrickAnimatorPlayEnterPatch.Postfix)),
                "TrickAnimator.PlayEnter");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickAnimator), nameof(TrickAnimator.PlayTweak)),
                AccessTools.Method(typeof(TrickAnimatorPlayTweakPatch), nameof(TrickAnimatorPlayTweakPatch.Postfix)),
                "TrickAnimator.PlayTweak");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickAnimator), nameof(TrickAnimator.PlayLoop)),
                AccessTools.Method(typeof(TrickAnimatorPlayLoopPatch), nameof(TrickAnimatorPlayLoopPatch.Postfix)),
                "TrickAnimator.PlayLoop");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickAnimator), nameof(TrickAnimator.PlayExit)),
                AccessTools.Method(typeof(TrickAnimatorPlayExitPatch), nameof(TrickAnimatorPlayExitPatch.Postfix)),
                "TrickAnimator.PlayExit");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickAnimator), "PlayPhase"),
                AccessTools.Method(typeof(TrickAnimatorPlayPhasePatch), nameof(TrickAnimatorPlayPhasePatch.Postfix)),
                "TrickAnimator.PlayPhase");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickAnimator), "ApplySpeed"),
                AccessTools.Method(typeof(TrickAnimatorApplySpeedPatch), nameof(TrickAnimatorApplySpeedPatch.Postfix)),
                "TrickAnimator.ApplySpeed");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickInputController), nameof(TrickInputController.ConsumeInputs)),
                AccessTools.Method(typeof(TrickInputControllerConsumeInputsPatch), nameof(TrickInputControllerConsumeInputsPatch.Postfix)),
                "TrickInputController.ConsumeInputs");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickSystemBrainV2), "FixedUpdate"),
                AccessTools.Method(typeof(TrickSystemBrainV2FixedUpdatePatch), nameof(TrickSystemBrainV2FixedUpdatePatch.Postfix)),
                "TrickSystemBrainV2.FixedUpdate");
            patched += PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TrickControllerV2), nameof(TrickControllerV2.UpdateSelection)),
                AccessTools.Method(typeof(TrickControllerUpdateSelectionPatch), nameof(TrickControllerUpdateSelectionPatch.Postfix)),
                "TrickControllerV2.UpdateSelection");

            installed = true;
            Log.Msg($"[TrickAnimDiag] Installed trick animation diagnostics. patched={patched}/16.");
        }

        public static void Update()
        {
            if (!IsEnabled)
                return;

            PollLiveBrainState();

            if (Time.unscaledTime < nextDiscoveryLogTime)
                return;

            nextDiscoveryLogTime = Time.unscaledTime + 10f;

            try
            {
                TrickAnimator[] animators = UnityEngine.Object.FindObjectsOfType<TrickAnimator>();
                int count = animators?.Length ?? 0;
                if (count <= 0)
                {
                    Log.Msg("[TrickAnimDiag] Discovery: TrickAnimator components=0.");
                    return;
                }

                TrickAnimator animator = animators[0];
                Log.Msg(
                    "[TrickAnimDiag] Discovery: " +
                    $"TrickAnimator components={count}, " +
                    $"first='{SafeRead(() => animator.name, "(unnamed)")}', " +
                    $"enabled={SafeRead(() => animator.enabled.ToString(), "?")}, " +
                    $"active={SafeRead(() => animator.isActiveAndEnabled.ToString(), "?")}, " +
                    $"mirror={SafeRead(() => animator.Mirror.ToString(), "?")}, " +
                    $"masterSpeed={SafeFloat(() => animator.MasterSpeed)}, " +
                    $"baseSpeed={SafeFloat(() => animator.baseSpeed)}, " +
                    $"player={SafeRead(() => animator.Player == null ? "(null)" : animator.Player.name, "(unreadable)")}, " +
                    $"vehicle={SafeRead(() => animator.Vehicle == null ? "(null)" : animator.Vehicle.name, "(unreadable)")}");

                LogBrainDiscovery();
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TrickAnimDiag] Discovery failed: {ex.Message}");
            }
        }

        private static int PatchPostfix(global::HarmonyLib.Harmony harmony, MethodInfo target, MethodInfo postfix, string label)
        {
            if (target == null || postfix == null)
            {
                Log.Warning($"[TrickAnimDiag] Could not resolve patch target for {label}.");
                return 0;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Log.Msg($"[TrickAnimDiag] Hooked {label}.");
                return 1;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TrickAnimDiag] Failed to hook {label}: {ex.Message}");
                return 0;
            }
        }

        private static int PatchPrefix(global::HarmonyLib.Harmony harmony, MethodInfo target, MethodInfo prefix, string label)
        {
            if (target == null || prefix == null)
            {
                Log.Warning($"[TrickAnimDiag] Could not resolve patch target for {label}.");
                return 0;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Log.Msg($"[TrickAnimDiag] Hooked {label}.");
                return 1;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TrickAnimDiag] Failed to hook {label}: {ex.Message}");
                return 0;
            }
        }

        private static class TrickControllerFirePatch
        {
            internal static void Postfix(
                SyncTrickAnimationData data,
                int setId,
                int slotId,
                bool __result)
            {
                if (!__result)
                    return;

                LogTrickData("TrickControllerV2.Fire", data, setId, slotId);
            }
        }

        private static class TrickControllerTryBlendToPatch
        {
            internal static void Postfix(
                SyncTrickAnimationData data,
                int setId,
                int slotId,
                bool __result)
            {
                if (!__result)
                    return;

                LogTrickData("TrickControllerV2.TryBlendTo", data, setId, slotId);
            }
        }

        private static class LegacyLaunchPlayTrickPatch
        {
            internal static void Postfix(LegacyTrickLaunchState __instance, float fadeDuration)
            {
                if (__instance == null)
                    return;

                LegacyTrickSystemBrain brain = SafeRead(() => __instance._brain, null);
                if (brain == null)
                    return;

                int setId = SafeRead(() => brain.TrickSetID, -1);
                int slotId = SafeRead(() => brain.CurrentTrickID, -1);
                SyncTrickAnimationData data = ResolveLegacyTrickData(brain, slotId);

                LogTrickData(
                    $"Legacy TrickLaunchState.PlayTrick fade={fadeDuration:0.###}",
                    data,
                    setId,
                    slotId);
            }
        }

        private static class PlayerTrickGameplayPerformedPatch
        {
            internal static void Postfix(PlayerTrickGameplay __instance, string trick)
            {
                LogPlayerTrickGameplay("PlayerTrickGameplay.PerformedTrick", __instance, $"trick='{trick ?? "(null)"}'");
            }
        }

        private static class PlayerTrickGameplayConfirmPatch
        {
            internal static void Postfix(PlayerTrickGameplay __instance)
            {
                LogPlayerTrickGameplay("PlayerTrickGameplay.ConfirmCombo", __instance, "combo confirmed");
            }
        }

        private static class ActivityTrackerRecordPatch
        {
            internal static void Prefix(string verb, string preposition, string adjective, bool isSportsTrick)
            {
                if (!IsEnabled)
                    return;

                LogGameplay(
                    "ActivityTracker.RecordActivity",
                    $"verb='{verb ?? "(null)"}', prep='{preposition ?? "(null)"}', adj='{adjective ?? "(null)"}', sports={isSportsTrick}");
            }
        }

        private static class MasterActivityLogPatch
        {
            internal static void Prefix(string text)
            {
                if (!IsEnabled)
                    return;

                LogGameplay("MasterActivityLog.Log", $"text='{text ?? "(null)"}'");
            }
        }

        private static class TrickAnimatorPlayEnterPatch
        {
            internal static void Postfix(TrickAnimator __instance, object[] __args)
            {
                LogTrickAnimatorPhase("TrickAnimator.PlayEnter", __instance, __args);
            }
        }

        private static class TrickAnimatorPlayTweakPatch
        {
            internal static void Postfix(TrickAnimator __instance, object[] __args)
            {
                LogTrickAnimatorPhase("TrickAnimator.PlayTweak", __instance, __args);
            }
        }

        private static class TrickAnimatorPlayLoopPatch
        {
            internal static void Postfix(TrickAnimator __instance, object[] __args)
            {
                LogTrickAnimatorPhase("TrickAnimator.PlayLoop", __instance, __args);
            }
        }

        private static class TrickAnimatorPlayExitPatch
        {
            internal static void Postfix(TrickAnimator __instance, object[] __args)
            {
                LogTrickAnimatorPhase("TrickAnimator.PlayExit", __instance, __args);
            }
        }

        private static class TrickAnimatorPlayPhasePatch
        {
            internal static void Postfix(TrickAnimator __instance, object[] __args)
            {
                LogTrickAnimatorPhase("TrickAnimator.PlayPhase", __instance, __args);
            }
        }

        private static class TrickAnimatorApplySpeedPatch
        {
            internal static void Postfix(TrickAnimator __instance, object[] __args)
            {
                LogTrickAnimatorPhase("TrickAnimator.ApplySpeed", __instance, __args);
            }
        }

        private static class TrickInputControllerConsumeInputsPatch
        {
            internal static void Postfix(TrickInputController __instance, object[] __args)
            {
                LogTrickInputState("TrickInputController.ConsumeInputs", __instance, __args);
            }
        }

        private static class TrickSystemBrainV2FixedUpdatePatch
        {
            internal static void Postfix(TrickSystemBrainV2 __instance)
            {
                LogBrainState("TrickSystemBrainV2.FixedUpdate", __instance);
            }
        }

        private static class TrickControllerUpdateSelectionPatch
        {
            internal static void Postfix(TrickControllerV2 __instance, object[] __args)
            {
                LogControllerSelection("TrickControllerV2.UpdateSelection", __instance, __args);
            }
        }

        private static void LogPlayerTrickGameplay(string source, PlayerTrickGameplay gameplay, string detail)
        {
            if (!IsEnabled || gameplay == null)
                return;

            LogGameplay(
                source,
                $"{detail}, " +
                $"current='{SafeRead(() => gameplay._currentTrick, "(unreadable)")}', " +
                $"last='{SafeRead(() => gameplay._lastTrick, "(unreadable)")}', " +
                $"comboPts={SafeFloat(() => gameplay.GetCurrentComboPoints())}, " +
                $"linePts={SafeFloat(() => gameplay.GetCurrentLinePoints())}, " +
                $"running={SafeRead(() => gameplay.IsRunningCombo.ToString(), "?")}, " +
                $"performing={SafeRead(() => gameplay._performingCombo.ToString(), "?")}, " +
                $"confirmedLanding={SafeRead(() => gameplay._confirmedLanding.ToString(), "?")}");
        }

        private static void LogGameplay(string source, string detail)
        {
            string key = $"{source}|{detail}";
            if (Time.frameCount == lastGameplayFrame && key == lastGameplayKey)
                return;

            lastGameplayFrame = Time.frameCount;
            lastGameplayKey = key;

            Log.Msg($"[TrickAnimDiag] {source}: {detail}");
        }

        private static void LogBrainDiscovery()
        {
            try
            {
                TrickSystemBrainV2[] brains = UnityEngine.Object.FindObjectsOfType<TrickSystemBrainV2>();
                TrickInputController[] inputs = UnityEngine.Object.FindObjectsOfType<TrickInputController>();
                TrickControllerV2[] controllers = UnityEngine.Object.FindObjectsOfType<TrickControllerV2>();

                Log.Msg(
                    "[TrickAnimDiag] Discovery V2: " +
                    $"brains={brains?.Length ?? 0}, " +
                    $"inputs={inputs?.Length ?? 0}, " +
                    $"controllers={controllers?.Length ?? 0}.");

                if (brains != null && brains.Length > 0)
                {
                    observedBrain = brains[0];
                    LogBrainState("Discovery Brain", brains[0], force: true);
                }

                if (inputs != null && inputs.Length > 0)
                    LogTrickInputState("Discovery Input", inputs[0], null, force: true);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TrickAnimDiag] V2 discovery failed: {ex.Message}");
            }
        }

        private static void PollLiveBrainState()
        {
            if (Time.unscaledTime < nextBrainPollTime)
                return;

            nextBrainPollTime = Time.unscaledTime + 0.05f;

            try
            {
                TrickSystemBrainV2 brain = observedBrain;
                if (brain == null)
                {
                    TrickSystemBrainV2[] brains = UnityEngine.Object.FindObjectsOfType<TrickSystemBrainV2>();
                    if (brains == null || brains.Length <= 0)
                        return;

                    brain = brains[0];
                    observedBrain = brain;
                }

                int firedSetId = SafeRead(() => brain._lastFiredSetId, -999);
                int firedSlotId = SafeRead(() => brain._lastFiredSlotId, -999);
                if (firedSetId < 0 || firedSlotId < 0)
                {
                    lastObservedTimeSinceFire = SafeRead(() => brain.TimeSinceLastFire, 999f);
                    return;
                }

                float timeSinceFire = SafeRead(() => brain.TimeSinceLastFire, 999f);
                bool changedFiredTrick =
                    firedSetId != lastObservedFiredSetId ||
                    firedSlotId != lastObservedFiredSlotId;
                bool sameTrickFiredAgain =
                    !changedFiredTrick &&
                    lastObservedTimeSinceFire > 0.2f &&
                    timeSinceFire >= 0f &&
                    timeSinceFire < 0.2f;

                if ((changedFiredTrick || sameTrickFiredAgain) && timeSinceFire < 2f)
                {
                    LogBrainState("Polled Brain Fired", brain, force: true);
                    lastObservedFiredSetId = firedSetId;
                    lastObservedFiredSlotId = firedSlotId;
                }

                lastObservedTimeSinceFire = timeSinceFire;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TrickAnimDiag] Brain poll failed: {ex.Message}");
            }
        }

        private static void LogTrickInputState(string source, TrickInputController input, object[] args, bool force = false)
        {
            if (!IsEnabled || input == null)
                return;

            int setId = SafeRead(() => input.TrickSetID, -999);
            int slotId = SafeRead(() => input.TrickSlotID, -999);
            string key =
                $"{source}|set={setId}|slot={slotId}|" +
                $"candidate={SafeRead(() => input._candidateTrickSetID, -999)}|" +
                $"newCandidate={SafeRead(() => input._newTrickSetCandidate.ToString(), "?")}|" +
                $"frames={SafeRead(() => input._candidateFrames, -999)}|" +
                $"lock={SafeRead(() => input._slotLockTimer, -999)}|" +
                $"requireZero={SafeRead(() => input._requireZeroOut.ToString(), "?")}|" +
                $"tweak={SafeRead(() => input.TweakInput.ToString(), "?")}";

            if (!force && key == lastInputStateKey)
                return;

            lastInputStateKey = key;

            Log.Msg(
                "[TrickAnimDiag] " +
                $"{source}: " +
                $"set={setId}, slot={slotId}, " +
                $"stick={SafeRead(() => input._stickInput.ToString(), "?")}, " +
                $"args={FormatArgs(args)}, " +
                $"candidateSet={SafeRead(() => input._candidateTrickSetID, -999)}, " +
                $"newCandidate={SafeRead(() => input._newTrickSetCandidate.ToString(), "?")}, " +
                $"candidateFrames={SafeRead(() => input._candidateFrames, -999)}, " +
                $"slotLock={SafeRead(() => input._slotLockTimer, -999)}, " +
                $"requireZero={SafeRead(() => input._requireZeroOut.ToString(), "?")}, " +
                $"tweak={SafeRead(() => input.TweakInput.ToString(), "?")}, " +
                $"tweakAngle={SafeFloat(() => input._tweakInputAngle)}");
        }

        private static void LogBrainState(string source, TrickSystemBrainV2 brain, bool force = false)
        {
            if (!IsEnabled || brain == null)
                return;

            int setId = SafeRead(() => brain.setId, -999);
            int slotId = SafeRead(() => brain.slotId, -999);
            int lastSetId = SafeRead(() => brain._lastFiredSetId, -999);
            int lastSlotId = SafeRead(() => brain._lastFiredSlotId, -999);
            TrickControllerV2 controller = SafeRead(() => brain._trickController, null);
            TrickInputController input = SafeRead(() => brain._trickInputController, null);

            string key =
                $"{source}|set={setId}|slot={slotId}|" +
                $"next={SafeRead(() => brain._nextSlotID, -999)}|" +
                $"last={lastSetId}:{lastSlotId}|" +
                $"zeroed={SafeRead(() => brain._zeroedSinceLastFire.ToString(), "?")}|" +
                $"grounded={SafeRead(() => brain._enteredGrounded.ToString(), "?")}|" +
                $"engaged={SafeRead(() => brain.TrickSetEngaged().ToString(), "?")}|" +
                $"idle={SafeRead(() => brain.IsIdleState().ToString(), "?")}|" +
                $"holding={SafeRead(() => brain.IsHoldingTrick().ToString(), "?")}|" +
                $"controllerData={SafeRead(() => controller == null || controller._data == null ? "(null)" : controller._data.name, "(unreadable)")}";

            if (!force && key == lastBrainStateKey)
                return;

            lastBrainStateKey = key;

            SyncTrickAnimationData selectedData = ResolveV2TrickData(brain, setId, slotId);
            SyncTrickAnimationData lastData = ResolveV2TrickData(brain, lastSetId, lastSlotId);

            Log.Msg(
                "[TrickAnimDiag] " +
                $"{source}: " +
                $"set={setId}, slot={slotId}, nextSlot={SafeRead(() => brain._nextSlotID, -999)}, " +
                $"lastFired={lastSetId}:{lastSlotId}, " +
                $"timeSinceFire={SafeFloat(() => brain.TimeSinceLastFire)}, " +
                $"zeroed={SafeRead(() => brain._zeroedSinceLastFire.ToString(), "?")}, " +
                $"enteredGrounded={SafeRead(() => brain._enteredGrounded.ToString(), "?")}, " +
                $"engaged={SafeRead(() => brain.TrickSetEngaged().ToString(), "?")}, " +
                $"idle={SafeRead(() => brain.IsIdleState().ToString(), "?")}, " +
                $"holding={SafeRead(() => brain.IsHoldingTrick().ToString(), "?")}, " +
                $"inputSet={SafeRead(() => input == null ? -999 : input.TrickSetID, -999)}, " +
                $"inputSlot={SafeRead(() => input == null ? -999 : input.TrickSlotID, -999)}, " +
                $"controllerBusy={SafeRead(() => controller == null ? "?" : controller.IsBusy.ToString(), "?")}, " +
                $"controllerPhase={SafeRead(() => controller == null ? "?" : controller._phase.ToString(), "?")}, " +
                $"controllerData='{SafeRead(() => controller == null || controller._data == null ? "(null)" : controller._data.name, "(unreadable)")}', " +
                $"selected='{TrickName(selectedData)}', last='{TrickName(lastData)}'");

            LogTrickData($"{source} selected", selectedData, setId, slotId);
            LogTrickData($"{source} lastFired", lastData, lastSetId, lastSlotId);
        }

        private static void LogControllerSelection(string source, TrickControllerV2 controller, object[] args)
        {
            if (!IsEnabled || controller == null)
                return;

            string key =
                $"{source}|args={FormatArgs(args)}|" +
                $"cur={SafeRead(() => controller._curSetId, -999)}:{SafeRead(() => controller._curSlotId, -999)}|" +
                $"fired={SafeRead(() => controller._firedSetId, -999)}:{SafeRead(() => controller._firedSlotId, -999)}|" +
                $"data={SafeRead(() => controller._data == null ? "(null)" : controller._data.name, "(unreadable)")}|phase={SafeRead(() => controller._phase.ToString(), "?")}";

            if (key == lastSelectionStateKey)
                return;

            lastSelectionStateKey = key;

            SyncTrickAnimationData data = SafeRead(() => controller._data, null);
            Log.Msg(
                "[TrickAnimDiag] " +
                $"{source}: " +
                $"args={FormatArgs(args)}, " +
                $"cur={SafeRead(() => controller._curSetId, -999)}:{SafeRead(() => controller._curSlotId, -999)}, " +
                $"fired={SafeRead(() => controller._firedSetId, -999)}:{SafeRead(() => controller._firedSlotId, -999)}, " +
                $"busy={SafeRead(() => controller.IsBusy.ToString(), "?")}, " +
                $"holding={SafeRead(() => controller.IsHoldingTrick.ToString(), "?")}, " +
                $"exiting={SafeRead(() => controller.IsExiting.ToString(), "?")}, " +
                $"phase={SafeRead(() => controller._phase.ToString(), "?")}, " +
                $"data='{TrickName(data)}'");

            LogTrickData(source, data, SafeRead(() => controller._curSetId, -1), SafeRead(() => controller._curSlotId, -1));
        }

        private static void LogTrickAnimatorPhase(string source, TrickAnimator animator, object[] args)
        {
            SyncTrickAnimationData data = FindTrickDataArg(args);
            string phaseName = FindPhaseNameArg(args);
            TrickAnimationEditor.NotifyAnimatorPhase(animator, data, phaseName);

            if (!IsEnabled || animator == null)
                return;

            string trickName = data == null
                ? "(null)"
                : SafeRead(() => data.TrickName, data.name ?? "(unnamed)");

            LogGameplay(
                source,
                $"trick='{trickName}', " +
                $"mirror={SafeRead(() => animator.Mirror.ToString(), "?")}, " +
                $"masterSpeed={SafeFloat(() => animator.MasterSpeed)}, " +
                $"baseSpeed={SafeFloat(() => animator.baseSpeed)}, " +
                $"enterFade={SafeFloat(() => animator.defaultEnterFade)}, " +
                $"tweakFade={SafeFloat(() => animator.defaultTweakFade)}, " +
                $"loopFade={SafeFloat(() => animator.defaultLoopFade)}, " +
                $"exitFade={SafeFloat(() => animator.defaultExitFade)}, " +
                $"args={FormatArgs(args)}");

            LogTrickData(source, data, -1, -1);
        }

        private static string FindPhaseNameArg(object[] args)
        {
            if (args == null || args.Length == 0 || args[0] == null)
                return "Any";

            return args[0].ToString();
        }

        private static SyncTrickAnimationData FindTrickDataArg(object[] args)
        {
            if (args == null)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is SyncTrickAnimationData data)
                    return data;
            }

            return null;
        }

        private static string FormatArgs(object[] args)
        {
            if (args == null || args.Length == 0)
                return "[]";

            int max = args.Length > 5 ? 5 : args.Length;
            string text = "[";
            for (int i = 0; i < max; i++)
            {
                if (i > 0)
                    text += ", ";

                object arg = args[i];
                if (arg == null)
                {
                    text += "null";
                }
                else if (arg is SyncTrickAnimationData trickData)
                {
                    text += SafeRead(() => trickData.TrickName, trickData.name ?? "(unnamed)");
                }
                else
                {
                    text += arg.ToString();
                }
            }

            if (args.Length > max)
                text += ", ...";

            return text + "]";
        }

        private static void LogTrickData(string source, SyncTrickAnimationData data, int setId, int slotId)
        {
            if (!IsEnabled || data == null)
                return;

            string trickName = SafeRead(() => data.TrickName, data.name ?? "(unnamed)");
            string key = $"{source}|{setId}|{slotId}|{trickName}";
            if (Time.frameCount == lastLoggedFrame && key == lastLoggedKey)
                return;

            lastLoggedFrame = Time.frameCount;
            lastLoggedKey = key;

            Log.Msg(
                "[TrickAnimDiag] " +
                $"{source}: set={setId}, slot={slotId}, trick='{trickName}', " +
                $"vehicle={SafeRead(() => data.Vehicle.ToString(), "?")}, " +
                $"onlyAir={SafeRead(() => data.OnlyFireIfInAir.ToString(), "?")}, " +
                $"allowLandingHold={SafeRead(() => data.AllowLandingHolding.ToString(), "?")}, " +
                $"speed overall={SafeFloat(() => data.OverallSpeedMult)}, " +
                $"enter={SafeFloat(() => data.EnterSpeedMult)}, " +
                $"loop={SafeFloat(() => data.LoopSpeedMult)}, " +
                $"loopMult={SafeFloat(() => data.LoopMult)}, " +
                $"tweak={SafeFloat(() => data.TweakSpeedMult)}, " +
                $"exit={SafeFloat(() => data.ExitSpeedMult)}, " +
                $"tweakBlendAt={SafeFloat(() => data.TweakBeginBlendNormalizedTime)}");

            Log.Msg(
                "[TrickAnimDiag] Player clips: " +
                $"enter='{ClipName(() => data.PlayerEnterClip)}', " +
                $"loop='{ClipName(() => data.PlayerLoopClip)}', " +
                $"tweak='{ClipName(() => data.PlayerTweakClip)}', " +
                $"exit='{ClipName(() => data.PlayerExitClip)}', " +
                $"mirrorEnter='{ClipName(() => data.PlayerEnterClipMirror)}', " +
                $"mirrorLoop='{ClipName(() => data.PlayerLoopClipMirror)}', " +
                $"mirrorTweak='{ClipName(() => data.PlayerTweakClipMirror)}', " +
                $"mirrorExit='{ClipName(() => data.PlayerExitClipMirror)}'");

            Log.Msg(
                "[TrickAnimDiag] Vehicle clips: " +
                $"enter='{ClipName(() => data.VehicleEnterClip)}', " +
                $"loop='{ClipName(() => data.VehicleLoopClip)}', " +
                $"tweak='{ClipName(() => data.VehicleTweakClip)}', " +
                $"exit='{ClipName(() => data.VehicleExitClip)}', " +
                $"mirrorEnter='{ClipName(() => data.VehicleEnterClipMirror)}', " +
                $"mirrorLoop='{ClipName(() => data.VehicleLoopClipMirror)}', " +
                $"mirrorTweak='{ClipName(() => data.VehicleTweakClipMirror)}', " +
                $"mirrorExit='{ClipName(() => data.VehicleExitClipMirror)}'");
        }

        private static SyncTrickAnimationData ResolveLegacyTrickData(LegacyTrickSystemBrain brain, int slotId)
        {
            if (brain == null || slotId < 0)
                return null;

            try
            {
                TrickSetData set = brain._currentTrickSet;
                if (set == null || set._dataList == null || slotId >= set._dataList.Count)
                    return null;

                return set._dataList[(Index)slotId] as SyncTrickAnimationData;
            }
            catch
            {
                return null;
            }
        }

        private static SyncTrickAnimationData ResolveV2TrickData(TrickSystemBrainV2 brain, int setId, int slotId)
        {
            if (brain == null || setId < 0 || slotId < 0)
                return null;

            try
            {
                TrickDataSets dataSets = brain._trickDataSets;
                if (dataSets == null || dataSets.TrickSets == null || setId >= dataSets.TrickSets.Length)
                    return null;

                TrickSetData set = dataSets.TrickSets[setId];
                if (set == null || set._dataList == null || slotId >= set._dataList.Count)
                    return null;

                return set._dataList[(Index)slotId] as SyncTrickAnimationData;
            }
            catch
            {
                return null;
            }
        }

        private static string TrickName(SyncTrickAnimationData data)
        {
            if (data == null)
                return "(null)";

            return SafeRead(() => data.TrickName, data.name ?? "(unnamed)");
        }

        private static string ClipName(System.Func<AnimationClip> read)
        {
            AnimationClip clip = SafeRead(read, null);
            return clip == null ? "(null)" : clip.name ?? "(unnamed)";
        }

        private static string SafeFloat(System.Func<float> read)
        {
            try
            {
                return read().ToString("0.###");
            }
            catch
            {
                return "?";
            }
        }

        private static T SafeRead<T>(System.Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch
            {
                return fallback;
            }
        }
    }
}
