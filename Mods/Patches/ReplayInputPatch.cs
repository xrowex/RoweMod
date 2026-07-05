using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using GameEvent = Il2CppMashBox.Core.Runtime.Events.GameEvent;
using GameInputManager = Il2CppMashBox.Core.Runtime.Input.InputManager;

namespace rowemod.Mods
{
    public static class ReplayInputPatch
    {
        private const string ReplayMenuName = "ReplayViewer";
        private const string NativeEmoteActionPath = "General/Emote";
        private const float AuthorizationLifetimeSeconds = 2f;
        private const float AuthorizedChainLifetimeSeconds = 1f;
        private const float BindingNeutralizeIntervalSeconds = 0.5f;

        private static readonly HashSet<MethodBase> PatchedMenuMethods = new HashSet<MethodBase>();
        private static readonly HashSet<MethodBase> PatchedReplayCommandMethods = new HashSet<MethodBase>();
        private static readonly HashSet<MethodBase> PatchedReplayEditorMethods = new HashSet<MethodBase>();
        private static readonly HashSet<MethodBase> PatchedInputMethods = new HashSet<MethodBase>();
        private static MethodBase patchedGameEventRaiseMethod;
        private static int replayOpenAuthorized;
        private static float authorizationExpiresAt;
        private static float authorizedChainExpiresAt;
        private static float blockNativeReplayUntil;
        private static float nextCloseReplayAttemptTime;
        private static bool applyLogged;
        private static bool inputManagerShapeLogged;
        private static bool inputActionPatchFailureLogged;
        private static int nativeEmoteActionIndex = -1;
        private static float nextBindingNeutralizeTime;
        
        public static Il2CppMashBox.Core.Runtime.Input.InputManager inputManager;
        public static void Patch()
        {
            inputManager = UnityEngine.GameObject.FindObjectOfType<Il2CppMashBox.Core.Runtime.Input.InputManager>();

            if (!inputManager)
            {
                if (!inputActionPatchFailureLogged)
                {
                    inputActionPatchFailureLogged = true;
                    Log.Msg("InputManager not found.");
                }

                return;
            }

            try
            {
                InputAction emote =
                    inputManager._gameplayInputActionAsset.FindAction(NativeEmoteActionPath, false);

                if (emote != null)
                {
                    nativeEmoteActionIndex = FindActionIndex(emote);
                    int removedBindings = RemoveDpadRightBindings(emote);
                    if (removedBindings > 0)
                        Log.Msg($"[PieMenu] Removed {removedBindings} D-pad Right binding(s) from native gameplay input action '{NativeEmoteActionPath}' index {nativeEmoteActionIndex}.");

                    return;
                }

                if (!inputActionPatchFailureLogged)
                {
                    inputActionPatchFailureLogged = true;
                    Log.Warning($"[PieMenu] Could not remove D-pad Right from native gameplay input action '{NativeEmoteActionPath}'; action was not found.");
                }
            }
            catch (System.Exception ex)
            {
                if (!inputActionPatchFailureLogged)
                {
                    inputActionPatchFailureLogged = true;
                    Log.Warning($"[PieMenu] Could not remove D-pad Right from native gameplay input action '{NativeEmoteActionPath}': {ex.Message}");
                }
            }
        }

        public static void ApplyLatePatch(global::HarmonyLib.Harmony harmony)
        {
            Patch();

            if (!applyLogged)
            {
                applyLogged = true;
                Log.Msg("[PieMenu] Replay input patch applied in binding-only mode.");
            }
        }

        public static void MaintainBindingNeutralization()
        {
            if (Time.realtimeSinceStartup < nextBindingNeutralizeTime)
                return;

            nextBindingNeutralizeTime = Time.realtimeSinceStartup + BindingNeutralizeIntervalSeconds;
            Patch();
        }

        public static void BlockNativeReplayForSeconds(float seconds)
        {
            blockNativeReplayUntil = Mathf.Max(blockNativeReplayUntil, Time.realtimeSinceStartup + seconds);
            CancelReplayOpenAuthorization();
            Patch();
            CloseNativeReplayEditor("native D-pad block");
        }

        public static void AuthorizeNextReplayOpen()
        {
            blockNativeReplayUntil = 0f;
            authorizationExpiresAt = Time.realtimeSinceStartup + AuthorizationLifetimeSeconds;
            Interlocked.Exchange(ref replayOpenAuthorized, 1);
            Log.Msg("[PieMenu] Authorized the next ReplayViewer menu request.");
        }

        public static void CancelReplayOpenAuthorization()
        {
            Interlocked.Exchange(ref replayOpenAuthorized, 0);
            authorizationExpiresAt = 0f;
            authorizedChainExpiresAt = 0f;
        }

        private static bool IsAuthorizedReplayChain()
        {
            return Time.realtimeSinceStartup <= authorizedChainExpiresAt;
        }

        private static bool HasPendingOrActiveReplayAuthorization()
        {
            return IsAuthorizedReplayChain()
                   || (Interlocked.CompareExchange(ref replayOpenAuthorized, 0, 0) == 1
                       && Time.realtimeSinceStartup <= authorizationExpiresAt);
        }

        private static bool TryAuthorizeReplayChain()
        {
            if (IsAuthorizedReplayChain())
                return true;

            if (Time.realtimeSinceStartup > authorizationExpiresAt)
            {
                CancelReplayOpenAuthorization();
                return false;
            }

            bool authorized = Interlocked.Exchange(ref replayOpenAuthorized, 0) == 1;
            if (authorized)
            {
                authorizationExpiresAt = 0f;
                authorizedChainExpiresAt = Time.realtimeSinceStartup + AuthorizedChainLifetimeSeconds;
                return true;
            }

            if (IsNativeReplayBlocked())
            {
                CancelReplayOpenAuthorization();
                return false;
            }

            return false;
        }

        private static bool IsNativeReplayBlocked()
        {
            return Time.realtimeSinceStartup <= blockNativeReplayUntil
                   || Gamepad.current?.dpad.right.isPressed == true;
        }

        private static void PatchReplaySystemCommandMethods(global::HarmonyLib.Harmony harmony)
        {
            MethodInfo prefix = AccessTools.Method(typeof(ReplayInputPatch), nameof(ReplaySystemCommandOpenPrefix));
            if (prefix == null)
            {
                Log.Error("[PieMenu] Could not resolve the ReplaySystem command gate prefix.");
                return;
            }

            Type replaySystemType = AccessTools.TypeByName("Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem");
            MethodInfo commandOpenReplayMethod = replaySystemType == null
                ? null
                : AccessTools.Method(replaySystemType, "CommandOpenReplay");
            MethodInfo openReplayRequestMethod = replaySystemType == null
                ? null
                : AccessTools.Method(replaySystemType, "OpenReplayRequest");

            int patchedCount = PatchReplayCommandMethod(
                harmony,
                commandOpenReplayMethod,
                prefix,
                "ReplaySystem.CommandOpenReplay");
            patchedCount += PatchReplayCommandMethod(
                harmony,
                openReplayRequestMethod,
                prefix,
                "ReplaySystem.OpenReplayRequest");

            if (patchedCount == 0)
                Log.Warning("[PieMenu] No ReplaySystem replay-open method was found; native replay command is not gated.");
        }

        private static void PatchReplayEditorMethods(global::HarmonyLib.Harmony harmony)
        {
            MethodInfo prefix = AccessTools.Method(
                typeof(ReplayInputPatch),
                nameof(ReplayEditorEntryPrefix));
            Type replayEditorType = AccessTools.TypeByName(
                "Il2CppMashBox.Addons.ReplaySystem.Replay_System.ReplayEditor");

            if (prefix == null || replayEditorType == null)
            {
                Log.Warning("[PieMenu] Could not resolve ReplayEditor entry gate.");
                return;
            }

            int patchedCount = 0;
            foreach (string methodName in new[] { "ShowUI", "ChangeMode", "SetPlaybackMode" })
            {
                MethodInfo target = AccessTools.Method(replayEditorType, methodName);
                if (target == null || PatchedReplayEditorMethods.Contains(target))
                    continue;

                try
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                    PatchedReplayEditorMethods.Add(target);
                    patchedCount++;
                    Log.Msg($"[PieMenu] Gated ReplayEditor.{methodName}.");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[PieMenu] Could not gate ReplayEditor.{methodName}: {ex.Message}");
                }
            }

            if (patchedCount == 0 && PatchedReplayEditorMethods.Count == 0)
                Log.Warning("[PieMenu] No ReplayEditor entry methods were found.");
        }

        private static void PatchReplayOpenGameEvent(global::HarmonyLib.Harmony harmony)
        {
            if (patchedGameEventRaiseMethod != null)
                return;

            MethodInfo target = AccessTools.Method(typeof(GameEvent), "Raise");
            MethodInfo prefix = AccessTools.Method(
                typeof(ReplayInputPatch),
                nameof(GameEventRaisePrefix));

            if (target == null || prefix == null)
            {
                Log.Warning("[PieMenu] Could not resolve GameEvent.Raise replay gate.");
                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                patchedGameEventRaiseMethod = target;
                Log.Msg("[PieMenu] Gated GameEvent.Raise for replay-open events.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not gate GameEvent.Raise: {ex.Message}");
            }
        }

        private static void PatchNativeInputManagerMethods(global::HarmonyLib.Harmony harmony)
        {
            MethodInfo prefix = AccessTools.Method(typeof(ReplayInputPatch), nameof(InputActionIndexPrefix));
            MethodInfo setInputAssetPostfix = AccessTools.Method(typeof(ReplayInputPatch), nameof(SetInputActionAssetPostfix));
            if (prefix == null)
            {
                Log.Error("[PieMenu] Could not resolve the native input gate prefix.");
                return;
            }

            Log.Msg($"[PieMenu] Inspecting native input manager type {typeof(GameInputManager).FullName}.");
            PatchInputActionIndexMethod(harmony, "IsDown", prefix, "InputManager.IsDown");
            PatchInputActionIndexMethod(harmony, "IsUp", prefix, "InputManager.IsUp");
            PatchInputActionIndexMethod(harmony, "IsTrickSetDown", prefix, "InputManager.IsTrickSetDown");
            Patch();
        }

        private static void PatchInputManagerUpdateMethod(global::HarmonyLib.Harmony harmony)
        {
            MethodInfo prefix = AccessTools.Method(typeof(ReplayInputPatch), nameof(InputManagerUpdatePrefix));
            MethodInfo target = AccessTools.Method(typeof(GameInputManager), "Update");
            if (target == null || prefix == null || PatchedInputMethods.Contains(target))
            {
                if (target == null)
                    Log.Warning("[PieMenu] Could not find native InputManager.Update.");

                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                PatchedInputMethods.Add(target);
                Log.Msg("[PieMenu] Gated native InputManager.Update for Replay D-pad handling.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not gate native InputManager.Update: {ex.Message}");
            }
        }

        private static void PatchSetInputActionAssetMethod(
            global::HarmonyLib.Harmony harmony,
            MethodInfo postfix)
        {
            MethodInfo target = AccessTools.Method(typeof(GameInputManager), "SetInputActionAsset");
            if (target == null || postfix == null || PatchedInputMethods.Contains(target))
            {
                if (target == null)
                    Log.Warning("[PieMenu] Could not find native InputManager.SetInputActionAsset.");

                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                PatchedInputMethods.Add(target);
                Log.Msg("[PieMenu] Gated native InputManager.SetInputActionAsset for Emote D-pad binding.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not gate native InputManager.SetInputActionAsset: {ex.Message}");
            }
        }

        private static void LogNativeInputManagerShape()
        {
            if (inputManagerShapeLogged)
                return;

            inputManagerShapeLogged = true;

            try
            {
                GameInputManager[] managers = Resources.FindObjectsOfTypeAll<GameInputManager>();
                Log.Msg($"[PieMenu] Native InputManager component count={managers?.Length ?? 0}.");

                if (managers != null)
                {
                    foreach (GameInputManager manager in managers)
                    {
                        string objectName = manager == null ? "<null>" : manager.name;
                        string active = manager?.gameObject == null ? "<no gameObject>" : manager.gameObject.activeInHierarchy.ToString();
                        Log.Msg($"[PieMenu] Native InputManager component name='{objectName}', active={active}.");
                    }
                }

                LogRelevantInputManagerFields();
                LogRelevantInputManagerMethods();
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not inspect native InputManager component: {ex.Message}");
            }
        }

        private static void LogRelevantInputManagerFields()
        {
            FieldInfo[] fields = typeof(GameInputManager).GetFields(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            foreach (FieldInfo field in fields)
            {
                string name = field.Name.ToLowerInvariant();
                string typeName = field.FieldType.FullName?.ToLowerInvariant() ?? string.Empty;
                if (!name.Contains("input") && !name.Contains("action") && !name.Contains("emote")
                    && !typeName.Contains("input") && !typeName.Contains("action"))
                {
                    continue;
                }

                Log.Msg($"[PieMenu] Native InputManager field {field.Name}: {field.FieldType.FullName}.");
            }
        }

        private static void LogRelevantInputManagerMethods()
        {
            MethodInfo[] methods = typeof(GameInputManager).GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            foreach (MethodInfo method in methods)
            {
                string name = method.Name.ToLowerInvariant();
                if (!name.Contains("input") && !name.Contains("action") && !name.Contains("emote")
                    && !name.Contains("enable") && !name.Contains("update"))
                {
                    continue;
                }

                Log.Msg(
                    $"[PieMenu] Native InputManager method {method.Name}: return={method.ReturnType.FullName}, params={FormatParameters(method.GetParameters())}.");
            }
        }

        private static void PatchInputActionIndexMethod(
            global::HarmonyLib.Harmony harmony,
            string methodName,
            MethodInfo prefix,
            string description)
        {
            MethodInfo target = AccessTools.Method(typeof(GameInputManager), methodName);
            if (target == null)
            {
                Log.Warning($"[PieMenu] Could not find native {description}.");
                LogInputManagerMethods(methodName);
                return;
            }

            if (prefix == null || PatchedInputMethods.Contains(target))
                return;

            ParameterInfo[] parameters = target.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(int) || target.ReturnType != typeof(bool))
            {
                Log.Warning(
                    $"[PieMenu] Skipped native {description}; signature is return={target.ReturnType.FullName}, params={FormatParameters(parameters)}.");
                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                PatchedInputMethods.Add(target);
                Log.Msg($"[PieMenu] Gated native {description} for '{NativeEmoteActionPath}'.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not gate native {description}: {ex.Message}");
            }
        }

        private static void LogInputManagerMethods(string methodName)
        {
            try
            {
                MethodInfo[] candidates = typeof(GameInputManager).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                int found = 0;
                foreach (MethodInfo candidate in candidates)
                {
                    if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                        continue;

                    found++;
                    Log.Msg(
                        $"[PieMenu] Native InputManager candidate {candidate.Name}: return={candidate.ReturnType.FullName}, params={FormatParameters(candidate.GetParameters())}.");
                }

                if (found == 0)
                    Log.Msg($"[PieMenu] Native InputManager has no visible method named {methodName}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not inspect native InputManager methods: {ex.Message}");
            }
        }

        private static string FormatParameters(ParameterInfo[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return "<none>";

            List<string> parts = new List<string>();
            foreach (ParameterInfo parameter in parameters)
                parts.Add($"{parameter.ParameterType.FullName} {parameter.Name}");

            return string.Join(", ", parts);
        }

        private static int PatchReplayCommandMethod(
            global::HarmonyLib.Harmony harmony,
            MethodInfo target,
            MethodInfo prefix,
            string description)
        {
            if (target == null || prefix == null || PatchedReplayCommandMethods.Contains(target))
                return 0;

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                PatchedReplayCommandMethods.Add(target);
                Log.Msg($"[PieMenu] Gated {description}.");
                return 1;
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not gate {description}: {ex.Message}");
                return 0;
            }
        }

        private static void PatchOpenMenuGameplayMethods(global::HarmonyLib.Harmony harmony)
        {
            MethodInfo prefix = AccessTools.Method(typeof(ReplayInputPatch), nameof(OpenMenuGameplayPrefix));
            if (prefix == null)
            {
                Log.Error("[PieMenu] Could not resolve the ReplayViewer menu gate prefix.");
                return;
            }

            int patchedCount = 0;
            Type menuServiceType = AccessTools.TypeByName("Il2CppMashBoxBridge.Common.Sys.MenuService");
            MethodInfo openMenuGameplayMethod = menuServiceType == null
                ? null
                : AccessTools.Method(menuServiceType, "OpenMenuGameplay");

            if (openMenuGameplayMethod != null && !PatchedMenuMethods.Contains(openMenuGameplayMethod))
            {
                try
                {
                    harmony.Patch(openMenuGameplayMethod, prefix: new HarmonyMethod(prefix));
                    PatchedMenuMethods.Add(openMenuGameplayMethod);
                    patchedCount++;
                    Log.Msg("[PieMenu] Gated MenuService.OpenMenuGameplay.");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[PieMenu] Could not gate MenuService.OpenMenuGameplay: {ex.Message}");
                }
            }

            if (patchedCount == 0 && PatchedMenuMethods.Count == 0)
                Log.Warning("[PieMenu] No OpenMenuGameplay method was found; ReplayViewer is not gated.");
        }

        private static void PatchReplayTransitionMethods(global::HarmonyLib.Harmony harmony)
        {
            MethodInfo transitionPrefix = AccessTools.Method(
                typeof(ReplayInputPatch),
                nameof(ReplayTransitionPrefix));
            MethodInfo legacyTriggerPrefix = AccessTools.Method(
                typeof(ReplayInputPatch),
                nameof(LegacyTriggerPrefix));

            Type transitionType = AccessTools.TypeByName(
                "Il2CppMashBox.Addons.GameLoop.TransitionTriggerBehaviour");
            MethodInfo transitionMethod = transitionType == null
                ? null
                : AccessTools.Method(transitionType, "Trigger");

            Type gameLoopFlowType = AccessTools.TypeByName(
                "Il2CppMashBox.Addons.GameLoop.GameLoopFlow");
            MethodInfo legacyTriggerMethod = gameLoopFlowType == null
                ? null
                : AccessTools.Method(gameLoopFlowType, "TryHandleLegacyTrigger");

            PatchMethod(
                harmony,
                transitionMethod,
                transitionPrefix,
                "TransitionTriggerBehaviour.Trigger");
            PatchMethod(
                harmony,
                legacyTriggerMethod,
                legacyTriggerPrefix,
                "GameLoopFlow.TryHandleLegacyTrigger");
        }

        private static void PatchMethod(
            global::HarmonyLib.Harmony harmony,
            MethodInfo target,
            MethodInfo prefix,
            string description)
        {
            if (target == null || prefix == null)
            {
                Log.Warning($"[PieMenu] Could not resolve replay gate target {description}.");
                return;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Log.Msg($"[PieMenu] Gated {description}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not gate {description}: {ex.Message}");
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                List<Type> loadableTypes = new List<Type>();
                foreach (Type type in ex.Types)
                {
                    if (type != null)
                        loadableTypes.Add(type);
                }

                return loadableTypes;
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static int FindActionIndex(InputAction targetAction)
        {
            int currentIndex = 0;
            foreach (InputActionMap actionMap in inputManager._gameplayInputActionAsset.actionMaps)
            {
                foreach (InputAction action in actionMap.actions)
                {
                    if (action == targetAction)
                        return currentIndex;

                    currentIndex++;
                }
            }

            return -1;
        }

        private static int RemoveDpadRightBindings(InputAction action)
        {
            int removedBindings = 0;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.overridePath == string.Empty || !IsDpadRightBinding(binding))
                    continue;

                action.ApplyBindingOverride(i, string.Empty);
                removedBindings++;
            }

            return removedBindings;
        }

        private static bool IsDpadRightBinding(InputBinding binding)
        {
            return IsDpadRightPath(binding.path) || IsDpadRightPath(binding.effectivePath);
        }

        private static bool IsDpadRightPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalized = path.Replace("\\", "/").ToLowerInvariant();
            return normalized.Contains("dpad/right") || normalized.Contains("dpadright");
        }

        internal static bool OpenMenuGameplayPrefix(
            MethodBase __originalMethod,
            object[] __args)
        {
            if (!ContainsReplayViewerArgument(__args))
                return true;

            if (TryAuthorizeReplayChain())
                return true;

            Log.Msg($"[PieMenu] Blocked ReplayViewer request at {FormatMethod(__originalMethod)}.");
            return false;
        }

        internal static bool InputActionIndexPrefix(int __0, ref bool __result, MethodBase __originalMethod)
        {
            if (nativeEmoteActionIndex < 0 || __0 != nativeEmoteActionIndex)
                return true;

            __result = false;
            return false;
        }

        internal static void SetInputActionAssetPostfix(InputActionAsset __0)
        {
            Patch();
        }

        internal static void InputManagerUpdatePrefix(GameInputManager __instance)
        {
            if (HasPendingOrActiveReplayAuthorization())
                return;

            if (Gamepad.current?.dpad.right.isPressed != true)
                return;

            blockNativeReplayUntil = Mathf.Max(
                blockNativeReplayUntil,
                Time.realtimeSinceStartup + 1.5f);
        }

        public static void CloseNativeReplayEditor(string reason)
        {
            if (Time.realtimeSinceStartup < nextCloseReplayAttemptTime)
                return;

            nextCloseReplayAttemptTime = Time.realtimeSinceStartup + 0.25f;

            try
            {
                GameEvent[] events = Resources.FindObjectsOfTypeAll<GameEvent>();
                int raised = 0;

                foreach (GameEvent gameEvent in events)
                {
                    if (gameEvent == null || string.IsNullOrEmpty(gameEvent.name))
                        continue;

                    string normalized = gameEvent.name.Replace("_", string.Empty)
                        .Replace(" ", string.Empty)
                        .ToLowerInvariant();

                    if (!normalized.Contains("closereplay"))
                        continue;

                    gameEvent.Raise();
                    raised++;
                }

                if (raised > 0)
                    Log.Msg($"[PieMenu] Raised CloseReplay event while handling {reason}. events={raised}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not raise CloseReplay event while handling {reason}: {ex.Message}");
            }
        }

        internal static bool ReplaySystemCommandOpenPrefix(MethodBase __originalMethod)
        {
            if (TryAuthorizeReplayChain())
            {
                Log.Msg($"[PieMenu] Allowed authorized replay command at {FormatMethod(__originalMethod)}.");
                return true;
            }

            Log.Msg($"[PieMenu] Blocked unauthorized replay command at {FormatMethod(__originalMethod)}.");
            LogReplayCommandStack();
            CloseNativeReplayEditor("blocked replay command");
            return false;
        }

        internal static bool ReplayEditorEntryPrefix(MethodBase __originalMethod)
        {
            if (TryAuthorizeReplayChain())
            {
                Log.Msg($"[PieMenu] Allowed authorized replay editor entry at {FormatMethod(__originalMethod)}.");
                return true;
            }

            Log.Msg($"[PieMenu] Blocked native replay editor entry at {FormatMethod(__originalMethod)}.");
            return false;
        }

        internal static bool GameEventRaisePrefix(GameEvent __instance)
        {
            string eventName = __instance?.name;
            if (!IsReplayOpenIdentifier(eventName))
                return true;

            if (TryAuthorizeReplayChain())
            {
                Log.Msg($"[PieMenu] Allowed authorized replay-open GameEvent '{eventName}'.");
                return true;
            }

            Log.Msg($"[PieMenu] Blocked replay-open GameEvent '{eventName}'.");
            return false;
        }

        private static void LogReplayCommandStack()
        {
            try
            {
                Log.Msg($"[PieMenuDiag] Replay command stack: {Environment.StackTrace}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenuDiag] Could not log replay command stack: {ex.Message}");
            }
        }

        internal static bool ReplayTransitionPrefix(object __instance, MethodBase __originalMethod)
        {
            string triggerId = GetMemberString(__instance, "_triggerID", "triggerID", "TriggerID");
            string objectName = GetUnityObjectName(__instance);

            if (!IsReplayOpenIdentifier(triggerId) && !IsReplayOpenIdentifier(objectName))
                return true;

            return AllowReplayStage(__originalMethod, $"trigger='{triggerId}', object='{objectName}'");
        }

        internal static bool LegacyTriggerPrefix(string __0, MethodBase __originalMethod)
        {
            if (!IsReplayOpenIdentifier(__0))
                return true;

            return AllowReplayStage(__originalMethod, $"trigger='{__0}'");
        }

        private static bool AllowReplayStage(MethodBase method, string detail)
        {
            if (TryAuthorizeReplayChain())
            {
                Log.Msg($"[PieMenu] Allowed authorized replay transition at {FormatMethod(method)} ({detail}).");
                return true;
            }

            Log.Msg($"[PieMenu] Blocked unauthorized replay transition at {FormatMethod(method)} ({detail}).");
            return false;
        }

        private static bool IsReplayOpenIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string normalized = value.Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

            return normalized.Contains("openreplay")
                   || normalized.Contains("replayviewer")
                   || normalized.Contains("transitiontriggeropenreplay");
        }

        private static string GetMemberString(object instance, params string[] memberNames)
        {
            if (instance == null)
                return string.Empty;

            Type type = instance.GetType();
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            foreach (string memberName in memberNames)
            {
                try
                {
                    object value = type.GetProperty(memberName, flags)?.GetValue(instance)
                                   ?? type.GetField(memberName, flags)?.GetValue(instance);
                    if (value != null)
                        return value.ToString();
                }
                catch
                {
                    // Try the next generated member name.
                }
            }

            return string.Empty;
        }

        private static string GetUnityObjectName(object instance)
        {
            try
            {
                PropertyInfo nameProperty = instance?.GetType().GetProperty(
                    "name",
                    BindingFlags.Instance | BindingFlags.Public);
                return nameProperty?.GetValue(instance)?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ContainsReplayViewerArgument(object[] args)
        {
            if (args == null)
                return false;

            foreach (object arg in args)
            {
                if (arg == null)
                    continue;

                string value = arg.ToString();
                if (string.Equals(value, ReplayMenuName, StringComparison.OrdinalIgnoreCase)
                    || IsReplayOpenIdentifier(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatMethod(MethodBase method)
        {
            return $"{method?.DeclaringType?.FullName ?? "<unknown>"}.{method?.Name ?? "<unknown>"}";
        }
    }
}
