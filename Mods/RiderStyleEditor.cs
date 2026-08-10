using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Bike-only stance controls. Rider head tracking and all per-state pose/IK
    /// editing systems have been removed.
    /// </summary>
    public static class RiderStyleEditor
    {
        public static bool RuntimeEnabled => false;

        public static void LateUpdate()
        {
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
        }

        public static void OnTabEntered()
        {
        }

        public static void OnTabExited()
        {
        }

        public static void NotifySettingsChanged()
        {
        }

        public static void Cleanup()
        {
            BikeOnlyStance.Cleanup();
        }

        public static void ResetAll()
        {
            BikeOnlyStance.Cleanup();
            Config.bikeOnlyStanceSettings = new BikeOnlyStanceSettings();
            Config.Save();
            Log.Msg("[RiderTools] Reset bike-only stance.");
        }

        public static void DrawTab()
        {
            float paneHeight = Menu.GetContentPaneHeight(80f);
            Menu.BeginPane(
                "Rider Tools",
                "Bike stance controls without rider head, pose, or IK overrides.",
                GUILayout.ExpandWidth(true),
                GUILayout.Height(paneHeight));

            DrawBikeOnlyStance();
            Menu.EndPane();
        }

        private static void DrawBikeOnlyStance()
        {
            Config.bikeOnlyStanceSettings ??= new BikeOnlyStanceSettings();
            BikeOnlyStanceSettings settings = Config.bikeOnlyStanceSettings;

            Menu.DrawSectionTitle(
                "Bike-Only Stance",
                "Mirrors the bike, pedals, rider motion, and local IK without changing native trick or grind binds.");

            bool enabledClicked = settings.enabled
                ? Menu.PrimaryButton(
                    "Bike-Only Stance: Enabled",
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(26f))
                : Menu.SecondaryButton(
                    "Bike-Only Stance: Disabled",
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(26f));
            if (enabledClicked)
            {
                settings.enabled = !settings.enabled;
                BikeOnlyStance.NotifySettingsChanged();
                Config.RequestSave();
            }

            bool holdToSwitch = settings.holdLeftStickToSwitchStance;
            Menu.ModernToggle(
                "Hold LS 0.5s to Switch Bike Stance",
                ref holdToSwitch,
                "bike_stance_hold_left_stick");
            if (holdToSwitch != settings.holdLeftStickToSwitchStance)
            {
                settings.holdLeftStickToSwitchStance = holdToSwitch;
                Config.RequestSave();
            }

            bool oppoTrickCompatibility = settings.useOppoTrickCompatibility;
            Menu.ModernToggle(
                "Enable Opposite-Stance Tricks (3.2.3 mode)",
                ref oppoTrickCompatibility,
                "bike_stance_oppo_tricks");
            if (oppoTrickCompatibility != settings.useOppoTrickCompatibility)
            {
                settings.useOppoTrickCompatibility = oppoTrickCompatibility;
                BikeOnlyStance.NotifySettingsChanged();
                Config.RequestSave();
            }
            GUILayout.Label(
                oppoTrickCompatibility
                    ? "Uses the pre-3.2.4 rider/bike motion mirror so oppo tricks work."
                    : "Feet-only mode: moves feet without changing trick or grind behavior.",
                Menu.UiMutedWrappedStyle);

            GUILayout.BeginHorizontal();
            bool regularClicked = settings.goofy
                ? Menu.SecondaryButton(
                    "Regular Bike",
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(26f))
                : Menu.PrimaryButton(
                    "Regular Bike",
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(26f));
            bool goofyClicked = settings.goofy
                ? Menu.PrimaryButton(
                    "Goofy Bike",
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(26f))
                : Menu.SecondaryButton(
                    "Goofy Bike",
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(26f));
            GUILayout.EndHorizontal();

            if (regularClicked && settings.goofy)
            {
                settings.goofy = false;
                BikeOnlyStance.NotifySettingsChanged();
                Config.RequestSave();
            }
            else if (goofyClicked && !settings.goofy)
            {
                settings.goofy = true;
                BikeOnlyStance.NotifySettingsChanged();
                Config.RequestSave();
            }

            GUILayout.Label(BikeOnlyStance.Status, Menu.UiMutedWrappedStyle);
            if (settings.enabled && settings.holdLeftStickToSwitchStance)
                GUILayout.Label(
                    LeftStickGestureRouter.Status,
                    Menu.UiMutedWrappedStyle);
        }
    }
}
