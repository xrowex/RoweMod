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
using GameReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;

namespace rowemod.Mods
{
    public static class ReplayInputPatch
    {
        private const string ReplayMenuName = "ReplayViewer";
        private const string NativeEmoteActionPath = "General/Emote";
        private const float AuthorizationLifetimeSeconds = 2f;
        private const float AuthorizedChainLifetimeSeconds = 1f;

        private static readonly HashSet<MethodBase> PatchedMenuMethods = new HashSet<MethodBase>();
        private static readonly HashSet<MethodBase> PatchedReplayCommandMethods = new HashSet<MethodBase>();
        private static readonly HashSet<MethodBase> PatchedReplayEditorMethods = new HashSet<MethodBase>();
        private static readonly HashSet<MethodBase> PatchedInputMethods = new HashSet<MethodBase>();
        private static readonly object ReplayGateLock = new object();
        private static MethodBase patchedGameEventRaiseMethod;
        private static int replayOpenAuthorized;
        private static float authorizationExpiresAt;
        private static float authorizedChainExpiresAt;
        private static float blockNativeReplayUntil;
        private static float nextCloseReplayAttemptTime;
        private static bool replayOpenedFromPieMenu;
        private static float ignoreReplayCloseInputUntil;
        private static bool applyLogged;
        private static bool inputManagerShapeLogged;
        private static bool inputActionPatchFailureLogged;
        private static bool replayCloseMethodFailureLogged;
        private static bool replayCloseMethodCandidatesLogged;
        private static bool replayCloseEventCandidatesLogged;
        private static int nativeEmoteActionIndex = -1;
        private static GameEvent[] cachedReplayCloseEvents;
        private static bool replayCloseEventsScanned;

        private static GameInputManager inputManager;
        public static void Patch()
        {
            inputManager = UnityEngine.GameObject.FindObjectOfType<GameInputManager>();

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
            PatchReplaySystemCommandMethods(harmony);
            PatchReplayEditorMethods(harmony);
            PatchReplayOpenGameEvent(harmony);
            PatchOpenMenuGameplayMethods(harmony);
            PatchReplayTransitionMethods(harmony);
            PatchNativeInputManagerMethods(harmony);
            LogNativeInputManagerShape();

            if (!applyLogged)
            {
                applyLogged = true;
                Log.Msg($"[PieMenu] Replay input patch applied. replayCommandGates={PatchedReplayCommandMethods.Count}, menuGates={PatchedMenuMethods.Count}.");
            }
        }

        public static void BlockNativeReplayForSeconds(float seconds)
        {
            lock (ReplayGateLock)
            {
                blockNativeReplayUntil = Mathf.Max(blockNativeReplayUntil, Time.realtimeSinceStartup + seconds);
            }

            CancelReplayOpenAuthorization();
            Patch();
        }

        public static void AuthorizeNextReplayOpen()
        {
            lock (ReplayGateLock)
            {
                blockNativeReplayUntil = 0f;
                authorizationExpiresAt = Time.realtimeSinceStartup + AuthorizationLifetimeSeconds;
            }

            Interlocked.Exchange(ref replayOpenAuthorized, 1);
            Log.Msg("[PieMenu] Authorized the next ReplayViewer menu request.");
        }

        public static void CancelReplayOpenAuthorization()
        {
            Interlocked.Exchange(ref replayOpenAuthorized, 0);
            lock (ReplayGateLock)
            {
                authorizationExpiresAt = 0f;
                authorizedChainExpiresAt = 0f;
            }
        }

        public static void MarkReplayOpenedFromPieMenu()
        {
            replayOpenedFromPieMenu = true;
            lock (ReplayGateLock)
            {
                ignoreReplayCloseInputUntil = Time.realtimeSinceStartup + 0.4f;
            }
        }

        public static void Update()
        {
            bool ignoreCloseInput;
            lock (ReplayGateLock)
            {
                ignoreCloseInput = Time.realtimeSinceStartup < ignoreReplayCloseInputUntil;
            }

            if (!replayOpenedFromPieMenu || ignoreCloseInput)
                return;

            bool closePressed = Gamepad.current?.buttonEast.wasPressedThisFrame == true
                                || Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            if (!closePressed)
                return;

            CancelReplayOpenAuthorization();
            if (CloseNativeReplayEditor("replay close input after pie replay open"))
                replayOpenedFromPieMenu = false;
        }

        private static bool IsAuthorizedReplayChain()
        {
            lock (ReplayGateLock)
            {
                return Time.realtimeSinceStartup <= authorizedChainExpiresAt;
            }
        }

        private static bool HasPendingOrActiveReplayAuthorization()
        {
            bool hasAuthorization;
            lock (ReplayGateLock)
            {
                hasAuthorization = Interlocked.CompareExchange(ref replayOpenAuthorized, 0, 0) == 1
                                   && Time.realtimeSinceStartup <= authorizationExpiresAt;
            }

            return IsAuthorizedReplayChain()
                   || hasAuthorization;
        }

        private static bool TryAuthorizeReplayChain()
        {
            if (IsAuthorizedReplayChain())
                return true;

            lock (ReplayGateLock)
            {
                if (Time.realtimeSinceStartup > authorizationExpiresAt)
                {
                    Interlocked.Exchange(ref replayOpenAuthorized, 0);
                    authorizationExpiresAt = 0f;
                    authorizedChainExpiresAt = 0f;
                    return false;
                }
            }

            bool authorized = Interlocked.Exchange(ref replayOpenAuthorized, 0) == 1;
            if (authorized)
            {
                lock (ReplayGateLock)
                {
                    authorizationExpiresAt = 0f;
                    authorizedChainExpiresAt = Time.realtimeSinceStartup + AuthorizedChainLifetimeSeconds;
                }

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
            lock (ReplayGateLock)
            {
                return Time.realtimeSinceStartup <= blockNativeReplayUntil
                       || Gamepad.current?.dpad.right.isPressed == true;
            }
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

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type.FullName == null || !type.FullName.ToLowerInvariant().Contains("replaysystem"))
                        continue;

                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (MethodInfo method in methods)
                    {
                        if (method.Name != "CommandOpenReplay"
                            && method.Name != "OpenReplayRequest")
                            continue;

                        patchedCount += PatchReplayCommandMethod(
                            harmony,
                            method,
                            prefix,
                            $"{type.FullName}.{method.Name}");
                    }
                }
            }

            if (patchedCount == 0)
                Log.Warning("[PieMenu] No ReplaySystem replay-open method was found; native replay command is not gated.");
        }

        private static void PatchReplayEditorMethods(global::HarmonyLib.Harmony harmony)
        {
            // ReplayEditor methods are used by normal replay exit/restore paths too.
            // Replay opening is gated earlier through commands, menu requests, events, and transitions.
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
            PatchInputManagerUpdateMethod(harmony);
            PatchSetInputActionAssetMethod(harmony, setInputAssetPostfix);
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

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (MethodInfo method in methods)
                    {
                        if (method.Name != "OpenMenuGameplay" || PatchedMenuMethods.Contains(method))
                            continue;

                        try
                        {
                            harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                            PatchedMenuMethods.Add(method);
                            patchedCount++;
                            Log.Msg($"[PieMenu] Gated {type.FullName}.{method.Name}.");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[PieMenu] Could not gate {type.FullName}.{method.Name}: {ex.Message}");
                        }
                    }
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
            if (inputManager?._gameplayInputActionAsset == null)
                return -1;

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
                // Unity InputSystem uses an empty override path to disable an existing binding.
                if (binding.overridePath == string.Empty || !IsDpadRightBinding(binding))
                    continue;

                action.ApplyBindingOverride(i, string.Empty);
                removedBindings++;
            }

            return removedBindings;
        }

        private static bool IsDpadRightBinding(InputBinding binding)
        {
            return IsDpadRightPath(binding.path)
                   || IsDpadRightPath(binding.effectivePath)
                   || IsDpadRightPath(binding.overridePath);
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
            if (ContainsReplayCloseArgument(__args))
                return true;

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

            lock (ReplayGateLock)
            {
                blockNativeReplayUntil = Mathf.Max(
                    blockNativeReplayUntil,
                    Time.realtimeSinceStartup + 1.5f);
            }
        }

        public static bool CloseNativeReplayEditor(string reason)
        {
            lock (ReplayGateLock)
            {
                if (Time.realtimeSinceStartup < nextCloseReplayAttemptTime)
                    return false;

                nextCloseReplayAttemptTime = Time.realtimeSinceStartup + 0.25f;
            }

            int invoked = InvokeReplayCloseMethods(reason);
            if (invoked > 0)
            {
                OnReplayCloseDetected(reason);
                return true;
            }

            try
            {
                GameEvent[] events = GetReplayCloseEvents();
                int raised = 0;

                foreach (GameEvent gameEvent in events)
                {
                    if (gameEvent == null || string.IsNullOrEmpty(gameEvent.name))
                        continue;

                    if (!IsReplayCloseIdentifier(gameEvent.name))
                        continue;

                    gameEvent.Raise();
                    raised++;
                }

                if (raised > 0)
                {
                    OnReplayCloseDetected(reason);
                    Log.Msg($"[PieMenu] Raised CloseReplay event while handling {reason}. events={raised}.");
                    return true;
                }

                LogReplayCloseCandidates();
                Log.Warning($"[PieMenu] Could not find a CloseReplay event while handling {reason}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenu] Could not raise CloseReplay event while handling {reason}: {ex.Message}");
            }

            return false;
        }

        private static void OnReplayCloseDetected(string reason)
        {
            replayOpenedFromPieMenu = false;
            CancelReplayOpenAuthorization();
            if (!global::rowemod.Menu.isOpen)
            {
                ControllerMenuInput.SetGameplayInputBlocked(false);
                Log.Msg($"[PieMenu] Replay close detected; gameplay input unblocked. reason={reason}.");
                return;
            }

            Log.Msg($"[PieMenu] Replay close detected; RoweMod menu is still open, so gameplay input stays blocked. reason={reason}.");
        }

        private static int InvokeReplayCloseMethods(string reason)
        {
            try
            {
                GameReplaySystem replaySystem = GameReplaySystem.Instance;
                if (replaySystem == null)
                    return 0;

                MethodInfo closeMethod = FindReplayCloseMethod(replaySystem.GetType());
                if (closeMethod == null)
                {
                    LogReplayCloseMethodCandidates(replaySystem.GetType());
                    return 0;
                }

                closeMethod.Invoke(replaySystem, null);
                Log.Msg($"[PieMenu] Invoked {closeMethod.DeclaringType?.FullName}.{closeMethod.Name} while handling {reason}.");
                return 1;
            }
            catch (Exception ex)
            {
                if (!replayCloseMethodFailureLogged)
                {
                    replayCloseMethodFailureLogged = true;
                    Log.Warning($"[PieMenu] Could not invoke a ReplaySystem close method while handling {reason}: {ex.Message}");
                }

                return 0;
            }
        }

        private static GameEvent[] GetReplayCloseEvents()
        {
            if (replayCloseEventsScanned)
                return cachedReplayCloseEvents ?? Array.Empty<GameEvent>();

            try
            {
                List<GameEvent> closeEvents = new List<GameEvent>();
                GameEvent[] events = Resources.FindObjectsOfTypeAll<GameEvent>();

                foreach (GameEvent gameEvent in events)
                {
                    if (gameEvent == null || string.IsNullOrEmpty(gameEvent.name))
                        continue;

                    if (IsReplayCloseIdentifier(gameEvent.name))
                        closeEvents.Add(gameEvent);
                }

                cachedReplayCloseEvents = closeEvents.ToArray();
            }
            catch (Exception ex)
            {
                cachedReplayCloseEvents = Array.Empty<GameEvent>();
                Log.Warning($"[PieMenu] Could not cache replay close events: {ex.Message}");
            }

            replayCloseEventsScanned = true;
            return cachedReplayCloseEvents;
        }

        private static MethodInfo FindReplayCloseMethod(Type replaySystemType)
        {
            if (replaySystemType == null)
                return null;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            string[] preferredNames =
            {
                "CommandCloseReplay",
                "CloseReplay",
                "CloseReplayRequest",
                "CommandExitReplay",
                "ExitReplay",
                "LeaveReplay",
                "StopReplay",
                "EndReplay",
                "CloseEditor",
                "ExitEditor"
            };

            foreach (string methodName in preferredNames)
            {
                MethodInfo method = AccessTools.Method(replaySystemType, methodName);
                if (IsCallableReplayCloseMethod(method))
                    return method;
            }

            foreach (MethodInfo method in replaySystemType.GetMethods(flags))
            {
                if (!IsCallableReplayCloseMethod(method))
                    continue;

                if (IsReplayCloseIdentifier(method.Name) || IsReplayCloseMethodName(method.Name))
                    return method;
            }

            return null;
        }

        private static bool IsCallableReplayCloseMethod(MethodInfo method)
        {
            return method != null
                   && !method.IsGenericMethod
                   && !method.IsSpecialName
                   && method.GetParameters().Length == 0;
        }

        private static bool IsReplayCloseMethodName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string normalized = value.Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

            return normalized.Contains("stopreplay")
                   || normalized.Contains("endreplay")
                   || normalized.Contains("closeeditor")
                   || normalized.Contains("exiteditor")
                   || normalized.Contains("closeviewer")
                   || normalized.Contains("hideviewer");
        }

        private static void LogReplayCloseMethodCandidates(Type replaySystemType)
        {
            if (replayCloseMethodCandidatesLogged || replaySystemType == null)
                return;

            replayCloseMethodCandidatesLogged = true;

            try
            {
                const BindingFlags flags =
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;

                List<string> names = new List<string>();
                foreach (MethodInfo method in replaySystemType.GetMethods(flags))
                {
                    if (method == null || method.IsSpecialName || method.GetParameters().Length != 0)
                        continue;

                    string normalized = method.Name.ToLowerInvariant();
                    if (normalized.Contains("replay")
                        || normalized.Contains("close")
                        || normalized.Contains("exit")
                        || normalized.Contains("stop")
                        || normalized.Contains("end"))
                    {
                        names.Add(method.Name);
                    }
                }

                Log.Msg($"[PieMenuDiag] ReplaySystem zero-arg close candidates: {string.Join(", ", names)}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenuDiag] Could not inspect ReplaySystem close candidates: {ex.Message}");
            }
        }

        private static void LogReplayCloseCandidates()
        {
            if (replayCloseEventCandidatesLogged)
                return;

            replayCloseEventCandidatesLogged = true;

            try
            {
                GameEvent[] events = Resources.FindObjectsOfTypeAll<GameEvent>();
                List<string> names = new List<string>();

                foreach (GameEvent gameEvent in events)
                {
                    if (gameEvent == null || string.IsNullOrEmpty(gameEvent.name))
                        continue;

                    string normalized = gameEvent.name.ToLowerInvariant();
                    if (normalized.Contains("replay")
                        || normalized.Contains("close")
                        || normalized.Contains("exit")
                        || normalized.Contains("leave"))
                    {
                        names.Add(gameEvent.name);
                    }
                }

                Log.Msg($"[PieMenuDiag] Replay-related GameEvents: {string.Join(", ", names)}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PieMenuDiag] Could not inspect replay GameEvents: {ex.Message}");
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
            return false;
        }

        internal static bool ReplayEditorEntryPrefix(MethodBase __originalMethod)
        {
            // Intentionally permissive: replay editor lifecycle methods must run so native close/restore can finish.
            return true;
        }

        internal static bool GameEventRaisePrefix(GameEvent __instance)
        {
            string eventName = __instance?.name;
            if (IsReplayCloseIdentifier(eventName))
            {
                OnReplayCloseDetected($"GameEvent '{eventName}'");
                return true;
            }

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

            if (IsReplayCloseIdentifier(triggerId) || IsReplayCloseIdentifier(objectName))
            {
                OnReplayCloseDetected($"transition trigger='{triggerId}', object='{objectName}'");
                return true;
            }

            if (!IsReplayOpenIdentifier(triggerId) && !IsReplayOpenIdentifier(objectName))
                return true;

            return AllowReplayStage(__originalMethod, $"trigger='{triggerId}', object='{objectName}'");
        }

        internal static bool LegacyTriggerPrefix(string __0, MethodBase __originalMethod)
        {
            if (IsReplayCloseIdentifier(__0))
            {
                OnReplayCloseDetected($"legacy trigger='{__0}'");
                return true;
            }

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

        private static bool IsReplayCloseIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string normalized = value.Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

            return normalized.Contains("closereplay")
                   || normalized.Contains("exitreplay")
                   || normalized.Contains("hidereplay")
                   || normalized.Contains("leavereplay")
                   || normalized.Contains("transitiontriggerclosereplay");
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
                if (IsReplayCloseIdentifier(value))
                    continue;

                if (string.Equals(value, ReplayMenuName, StringComparison.OrdinalIgnoreCase)
                    || IsReplayOpenIdentifier(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsReplayCloseArgument(object[] args)
        {
            if (args == null)
                return false;

            foreach (object arg in args)
            {
                if (arg == null)
                    continue;

                if (IsReplayCloseIdentifier(arg.ToString()))
                    return true;
            }

            return false;
        }

        private static string FormatMethod(MethodBase method)
        {
            return $"{method?.DeclaringType?.FullName ?? "<unknown>"}.{method?.Name ?? "<unknown>"}";
        }
    }
}
