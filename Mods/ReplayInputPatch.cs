using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    public static class ReplayInputPatch
    {
        private const string ReplayMenuName = "ReplayViewer";
        private const float AuthorizationLifetimeSeconds = 2f;
        private const float AuthorizedChainLifetimeSeconds = 1f;

        private static readonly HashSet<MethodBase> PatchedMenuMethods = new HashSet<MethodBase>();
        private static int replayOpenAuthorized;
        private static float authorizationExpiresAt;
        private static float authorizedChainExpiresAt;

        public static void ApplyLatePatch(global::HarmonyLib.Harmony harmony)
        {
            PatchOpenMenuGameplayMethods(harmony);
            PatchReplayTransitionMethods(harmony);
        }

        public static void AuthorizeNextReplayOpen()
        {
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
            }

            return authorized;
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

        internal static bool OpenMenuGameplayPrefix(MethodBase __originalMethod, object[] __args)
        {
            if (!ContainsReplayViewerArgument(__args))
                return true;

            if (TryAuthorizeReplayChain())
            {
                Log.Msg($"[PieMenu] Allowed authorized ReplayViewer request through {FormatMethod(__originalMethod)}.");
                return true;
            }

            Log.Msg($"[PieMenu] Blocked unauthorized ReplayViewer request at {FormatMethod(__originalMethod)}.");
            return false;
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
                if (arg != null && string.Equals(arg.ToString(), ReplayMenuName, StringComparison.OrdinalIgnoreCase))
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
