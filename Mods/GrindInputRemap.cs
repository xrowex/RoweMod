using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBoxBridge.Common.Interfaces;
using Il2CppMashBox.Core.Runtime.GrindPoseData;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Routes the eight native HookGrind requests once, at the same point the game selects a
    /// grind pose. This deliberately does not run a continuous pose override.
    /// </summary>
    public static class GrindInputRemap
    {
        private static readonly HookGrind[] HookGrinds =
        {
            HookGrind.SouthHookEast,
            HookGrind.SouthHookWest,
            HookGrind.NorthHookEast,
            HookGrind.NorthHookWest,
            HookGrind.EastHookNorth,
            HookGrind.EastHookSouth,
            HookGrind.WestHookNorth,
            HookGrind.WestHookSouth
        };

        private static readonly Dictionary<int, string> LearnedGrindNames = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> NativeMotionNames = new Dictionary<int, string>();
        private static readonly HashSet<string> LoggedRoutes = new HashSet<string>(StringComparer.Ordinal);
        private static bool _expanded;
        private static int _editingSource = -1;

        public static void DrawControls()
        {
            Config.grindPoseData ??= new GrindPoseSettings();
            Config.NormalizeGrindPoseSettings(Config.grindPoseData);

            RefreshNativeMotionNames();
            _expanded = Menu.ModernFoldout("Grind Motion Mapping", _expanded);
            if (!_expanded)
                return;

            GUILayout.BeginVertical(Menu.UiPanelAltStyle);
            GUILayout.Label(
                "Choose what each native grind input should perform. Selecting a motion that is already " +
                "assigned swaps the two inputs, so every motion keeps one home.",
                Menu.UiMutedWrappedStyle);
            GUILayout.Space(4f);

            bool enabled = Config.grindPoseData.grindInputRemapEnabled;
            Menu.ModernToggle("Enable Grind Input Remap", ref enabled, "grind_input_enabled");
            if (enabled != Config.grindPoseData.grindInputRemapEnabled)
            {
                Config.grindPoseData.grindInputRemapEnabled = enabled;
                Config.RequestSave();
            }

            GUILayout.BeginHorizontal();
            if (Menu.SecondaryButton("Restore Native Mapping", GUILayout.Height(28f)))
            {
                ResetToNative();
                _editingSource = -1;
                Config.RequestSave();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            foreach (HookGrind source in HookGrinds)
            {
                int sourceIndex = (int)source;
                int targetIndex = Config.grindPoseData.grindInputMap[sourceIndex];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Input: {DescribeMotion(sourceIndex)}", Menu.UiRowLabelStyle,
                    GUILayout.Width(280f * Menu.EffectiveUiScale));

                string mappedLabel = targetIndex == 0
                    ? "Disabled"
                    : DescribeMotion(targetIndex);
                if (Menu.ControllerButton($"grind_input_route_{sourceIndex}", $"Maps to: {mappedLabel}",
                        _editingSource == sourceIndex ? Menu.UiRowButtonSelectedStyle : Menu.UiRowButtonStyle,
                        GUILayout.Height(30f * Menu.EffectiveUiScale)))
                {
                    _editingSource = _editingSource == sourceIndex ? -1 : sourceIndex;
                }
                GUILayout.EndHorizontal();

                if (_editingSource != sourceIndex)
                    continue;

                GUILayout.BeginVertical(Menu.UiPanelStyle);
                GUILayout.Label($"Choose a motion for: {DescribeMotion(sourceIndex)}", Menu.UiHeaderStyle);
                GUILayout.Label(
                    "If the chosen motion is already mapped elsewhere, RoweMod swaps the two mappings.",
                    Menu.UiMutedWrappedStyle);

                GUILayout.BeginHorizontal();
                if (Menu.DangerButton("Disable This Input", GUILayout.Height(28f * Menu.EffectiveUiScale)))
                {
                    AssignMapping(sourceIndex, 0);
                    _editingSource = -1;
                    Config.RequestSave();
                }
                if (Menu.SecondaryButton("Cancel", GUILayout.Height(28f * Menu.EffectiveUiScale)))
                    _editingSource = -1;
                GUILayout.EndHorizontal();

                foreach (HookGrind target in HookGrinds)
                {
                    int targetValue = (int)target;
                    bool isSelected = targetIndex == targetValue;
                    if (Menu.ControllerButton($"grind_input_pick_{sourceIndex}_{targetValue}",
                            DescribeMotion(targetValue),
                            isSelected ? Menu.UiRowButtonSelectedStyle : Menu.UiRowButtonStyle,
                            GUILayout.Height(28f * Menu.EffectiveUiScale)))
                    {
                        AssignMapping(sourceIndex, targetValue);
                        _editingSource = -1;
                        Config.RequestSave();
                    }
                }
                GUILayout.EndVertical();
            }

            GUILayout.Label(
                "This uses the game's native grind-selection call once per input. It does not continuously force a pose.",
                Menu.UiMutedWrappedStyle);
            GUILayout.EndVertical();
            GUILayout.Space(6f * Menu.EffectiveUiScale);
        }

        internal static HookGrind Route(HookGrind requested)
        {
            GrindPoseSettings settings = Config.grindPoseData;
            int source = (int)requested;
            if (settings == null || !settings.grindInputRemapEnabled || source < 1 || source > 8)
                return requested;

            Config.NormalizeGrindPoseSettings(settings);
            int target = settings.grindInputMap[source];
            HookGrind routed = target is >= 1 and <= 8 ? (HookGrind)target : HookGrind.Null;
            if (routed != requested)
            {
                string routeKey = $"{source}:{target}";
                if (LoggedRoutes.Add(routeKey))
                    Log.Msg($"[GrindInput] {DescribeHook(requested)} -> {DescribeHook(routed)}.");
            }

            return routed;
        }

        internal static void LearnNativeResult(BikeGrindPoser poser, HookGrind routed)
        {
            if (poser == null || routed == HookGrind.Null)
                return;

            try
            {
                string name = poser.CurrrentGrindPosData?.GrindName;
                if (!string.IsNullOrWhiteSpace(name))
                    LearnedGrindNames[(int)routed] = name;
            }
            catch (Exception ex)
            {
                Log.Warning("[GrindInput] Could not read the selected native grind: " + ex.Message);
            }
        }

        private static void ResetToNative()
        {
            Config.grindPoseData.grindInputRemapEnabled = true;
            Config.grindPoseData.grindInputMap = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            LoggedRoutes.Clear();
        }

        private static void AssignMapping(int source, int target)
        {
            int[] map = Config.grindPoseData.grindInputMap;
            int previousTarget = map[source];
            if (target > 0)
            {
                // Preserve a one-to-one map. Choosing another input's motion swaps it
                // into this row instead of creating an invisible duplicate binding.
                for (int i = 1; i < map.Length; i++)
                {
                    if (i != source && map[i] == target)
                    {
                        map[i] = previousTarget;
                        break;
                    }
                }
            }

            map[source] = target;
            LoggedRoutes.Clear();
        }

        private static void RefreshNativeMotionNames()
        {
            try
            {
                GrindPoseDataSet[] dataSets = Resources.FindObjectsOfTypeAll<GrindPoseDataSet>();
                foreach (GrindPoseDataSet dataSet in dataSets)
                {
                    if (dataSet == null)
                        continue;

                    StoreNativeMotion(HookGrind.SouthHookEast, dataSet.SouthHookEast);
                    StoreNativeMotion(HookGrind.SouthHookWest, dataSet.SouthHookWest);
                    StoreNativeMotion(HookGrind.NorthHookEast, dataSet.NorthHookEast);
                    StoreNativeMotion(HookGrind.NorthHookWest, dataSet.NorthHookWest);
                    StoreNativeMotion(HookGrind.EastHookNorth, dataSet.EastHookNorth);
                    StoreNativeMotion(HookGrind.EastHookSouth, dataSet.EastHookSouth);
                    StoreNativeMotion(HookGrind.WestHookNorth, dataSet.WestHookNorth);
                    StoreNativeMotion(HookGrind.WestHookSouth, dataSet.WestHookSouth);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[GrindInput] Could not discover the native grind motions: " + ex.Message);
            }
        }

        private static void StoreNativeMotion(HookGrind hook, BikeGrindPoseData pose)
        {
            if (pose == null || string.IsNullOrWhiteSpace(pose.GrindName))
                return;

            NativeMotionNames[(int)hook] = pose.GrindName;
        }

        private static string DescribeMotion(int hookValue)
        {
            if (hookValue > 0 && NativeMotionNames.TryGetValue(hookValue, out string nativeName))
                return CleanMotionName(nativeName);
            if (hookValue > 0 && LearnedGrindNames.TryGetValue(hookValue, out string learnedName))
                return CleanMotionName(learnedName);
            return DescribeHook((HookGrind)hookValue) + " (native motion unavailable)";
        }

        private static string CleanMotionName(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? "Unnamed Grind"
                : name.Replace("BMX_", string.Empty).Replace('_', ' ');
        }

        private static string DescribeHook(HookGrind hook)
        {
            return hook switch
            {
                HookGrind.SouthHookEast => "South → East",
                HookGrind.SouthHookWest => "South → West",
                HookGrind.NorthHookEast => "North → East",
                HookGrind.NorthHookWest => "North → West",
                HookGrind.EastHookNorth => "East → North",
                HookGrind.EastHookSouth => "East → South",
                HookGrind.WestHookNorth => "West → North",
                HookGrind.WestHookSouth => "West → South",
                _ => "Disabled"
            };
        }
    }

    [HarmonyPatch(typeof(BikeGrindPoser), nameof(BikeGrindPoser.SetInputData))]
    internal static class BikeGrindPoserInputRemapPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref HookGrind hookGrind)
        {
            hookGrind = GrindInputRemap.Route(hookGrind);
        }

        [HarmonyPostfix]
        private static void Postfix(BikeGrindPoser __instance, HookGrind hookGrind)
        {
            GrindInputRemap.LearnNativeResult(__instance, hookGrind);
            // The native selection path can restore its own transition rates. Reapply once
            // after each selected grind instead of overriding it continuously every frame.
            GrindPoseEditor.ApplyLerpSpeedToRuntime(__instance);
        }
    }
}
