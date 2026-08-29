using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Bike-only stance and manual IK pose controls.
    /// </summary>
    public static class RiderStyleEditor
    {
        public static bool RuntimeEnabled => ManualIkPoseEditor.RuntimeEnabled;

        public static void LateUpdate()
        {
            ManualIkPoseEditor.LateUpdate();
        }

        public static void FixedUpdate()
        {
            ManualIkPoseEditor.FixedUpdate();
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            ManualIkPoseEditor.OnSceneInitialized(gameplayScene);
        }

        public static void OnTabEntered()
        {
        }

        public static void OnTabExited()
        {
            ManualIkPoseEditor.SetEditing(false);
        }

        public static void NotifySettingsChanged()
        {
            ManualIkPoseEditor.NotifySettingsChanged();
        }

        public static void Cleanup()
        {
            ManualIkPoseEditor.Cleanup();
            BikeOnlyStance.Cleanup();
        }

        public static void ResetAll()
        {
            BikeOnlyStance.Cleanup();
            ManualIkPoseEditor.Cleanup();
            Config.bikeOnlyStanceSettings = new BikeOnlyStanceSettings();
            Config.manualIkPoseSettings = new ManualIkPoseSettings();
            Config.Save();
            Log.Msg("[RiderTools] Reset bike-only stance.");
        }

        public static void DrawTab()
        {
            float paneHeight = Menu.GetContentPaneHeight(80f);
            Menu.BeginPane(
                "Rider Tools",
                "Bike stance plus manual and nose-manual foot/hip positioning.",
                GUILayout.ExpandWidth(true),
                GUILayout.Height(paneHeight));

            DrawBikeOnlyStance();
            GUILayout.Space(12f);
            DrawManualIkPose();
            Menu.EndPane();
        }

        private static void DrawManualIkPose()
        {
            Config.manualIkPoseSettings ??= new ManualIkPoseSettings();
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;

            Menu.DrawSectionTitle(
                "Manual IK Pose",
                "Move the rider's feet and hips during manuals. World handles follow the bike and are removed when editing closes.");

            bool beforeChanged = GUI.changed;
            GUI.changed = false;

            Menu.BeginAltPane(
                "Activation",
                "Choose when this saved pose replaces the game's normal manual targets.");
            bool enabled = settings.enabled;
            Menu.ModernToggle("Enable Custom Manual Pose", ref enabled, "manual_ik_enabled");
            settings.enabled = enabled;

            bool manual = settings.applyDuringManual;
            Menu.ModernToggle("Use During Rear-Wheel Manuals", ref manual, "manual_ik_manny");
            settings.applyDuringManual = manual;

            bool nose = settings.applyDuringNoseManual;
            Menu.ModernToggle("Use During Nose Manuals", ref nose, "manual_ik_nosey");
            settings.applyDuringNoseManual = nose;
            Menu.EndPane();

            GUILayout.Space(8f);

            bool editClicked = ManualIkPoseEditor.IsEditing
                ? Menu.PrimaryButton("Finish World Editing", GUILayout.ExpandWidth(true), GUILayout.Height(28f))
                : Menu.SecondaryButton("Edit With Spheres In World", GUILayout.ExpandWidth(true), GUILayout.Height(28f));
            if (editClicked)
                ManualIkPoseEditor.SetEditing(!ManualIkPoseEditor.IsEditing);

            if (Menu.SecondaryButton("Capture Live Feet While Riding (12 Seconds)",
                    GUILayout.ExpandWidth(true), GUILayout.Height(28f)))
                LiveFootDiagnostics.Arm();
            GUILayout.Label(LiveFootDiagnostics.Status, Menu.UiMutedWrappedStyle);

            GUILayout.Space(8f);
            DrawLeftFootCard(settings);
            GUILayout.Space(8f);
            DrawRightFootCard(settings);
            GUILayout.Space(8f);
            DrawHipsCard(settings);

            if (Menu.SecondaryButton("Reset Manual Pose", GUILayout.ExpandWidth(true), GUILayout.Height(26f)))
            {
                Config.manualIkPoseSettings = new ManualIkPoseSettings();
                settings = Config.manualIkPoseSettings;
                GUI.changed = true;
            }

            bool changed = GUI.changed;
            GUI.changed = beforeChanged || changed;
            if (changed)
            {
                ManualIkPoseEditor.NotifySettingsChanged();
                Config.RequestSave();
            }

            GUILayout.Label(ManualIkPoseEditor.Status, Menu.UiMutedWrappedStyle);
        }

        private static bool DrawTargetCard(
            string title,
            string handleDescription,
            string id,
            ref bool enabled,
            ref float x,
            ref float y,
            ref float z,
            ref float pitch,
            ref float yaw,
            ref float roll,
            System.Action reset)
        {
            Menu.BeginAltPane(title, handleDescription + ". Position is relative to the bike; rotation is relative to the captured manual pose.");
            Menu.ModernToggle("Override " + title, ref enabled, "manual_ik_" + id + "_enabled");

            GUILayout.Space(4f);
            GUILayout.Label("POSITION", Menu.UiMutedStyle);
            Menu.ModernSlider("X  -  Left / Right", ref x, -2f, 2f, "manual_ik_" + id + "_position_x");
            Menu.ModernSlider("Y  -  Down / Up", ref y, -2f, 2f, "manual_ik_" + id + "_position_y");
            Menu.ModernSlider("Z  -  Back / Forward", ref z, -2f, 2f, "manual_ik_" + id + "_position_z");

            GUILayout.Space(4f);
            GUILayout.Label("ROTATION", Menu.UiMutedStyle);
            Menu.ModernSlider("Pitch  (X)", ref pitch, -180f, 180f, "manual_ik_" + id + "_rotation_x");
            Menu.ModernSlider("Yaw  (Y)", ref yaw, -180f, 180f, "manual_ik_" + id + "_rotation_y");
            Menu.ModernSlider("Roll  (Z)", ref roll, -180f, 180f, "manual_ik_" + id + "_rotation_z");

            if (Menu.SecondaryButton("Reset " + title, GUILayout.ExpandWidth(true), GUILayout.Height(24f)))
            {
                reset();
                GUI.changed = true;
                Menu.EndPane();
                return true;
            }
            Menu.EndPane();
            return false;
        }

        private static void DrawLeftFootCard(ManualIkPoseSettings settings)
        {
            bool enabled = settings.leftFootEnabled;
            float x = settings.leftFootX, y = settings.leftFootY, z = settings.leftFootZ;
            float pitch = settings.leftFootPitch, yaw = settings.leftFootYaw, roll = settings.leftFootRoll;
            if (DrawTargetCard("Left Foot", "Blue sphere", "left", ref enabled,
                    ref x, ref y, ref z, ref pitch, ref yaw, ref roll, () => ResetLeftFoot(settings)))
                return;
            settings.leftFootEnabled = enabled;
            settings.leftFootX = x; settings.leftFootY = y; settings.leftFootZ = z;
            settings.leftFootPitch = pitch; settings.leftFootYaw = yaw; settings.leftFootRoll = roll;
        }

        private static void DrawRightFootCard(ManualIkPoseSettings settings)
        {
            bool enabled = settings.rightFootEnabled;
            float x = settings.rightFootX, y = settings.rightFootY, z = settings.rightFootZ;
            float pitch = settings.rightFootPitch, yaw = settings.rightFootYaw, roll = settings.rightFootRoll;
            if (DrawTargetCard("Right Foot", "Red sphere", "right", ref enabled,
                    ref x, ref y, ref z, ref pitch, ref yaw, ref roll, () => ResetRightFoot(settings)))
                return;
            settings.rightFootEnabled = enabled;
            settings.rightFootX = x; settings.rightFootY = y; settings.rightFootZ = z;
            settings.rightFootPitch = pitch; settings.rightFootYaw = yaw; settings.rightFootRoll = roll;
        }

        private static void DrawHipsCard(ManualIkPoseSettings settings)
        {
            bool enabled = settings.hipsEnabled;
            float x = settings.hipsX, y = settings.hipsY, z = settings.hipsZ;
            float pitch = settings.hipsPitch, yaw = settings.hipsYaw, roll = settings.hipsRoll;
            if (DrawTargetCard("Hips", "Yellow sphere", "hips", ref enabled,
                    ref x, ref y, ref z, ref pitch, ref yaw, ref roll, () => ResetHips(settings)))
                return;
            settings.hipsEnabled = enabled;
            settings.hipsX = x; settings.hipsY = y; settings.hipsZ = z;
            settings.hipsPitch = pitch; settings.hipsYaw = yaw; settings.hipsRoll = roll;
        }

        private static void ResetLeftFoot(ManualIkPoseSettings settings)
        {
            ManualIkPoseSettings defaults = new ManualIkPoseSettings();
            settings.leftFootX = defaults.leftFootX;
            settings.leftFootY = defaults.leftFootY;
            settings.leftFootZ = defaults.leftFootZ;
            settings.leftFootPitch = settings.leftFootYaw = settings.leftFootRoll = 0f;
        }

        private static void ResetRightFoot(ManualIkPoseSettings settings)
        {
            ManualIkPoseSettings defaults = new ManualIkPoseSettings();
            settings.rightFootX = defaults.rightFootX;
            settings.rightFootY = defaults.rightFootY;
            settings.rightFootZ = defaults.rightFootZ;
            settings.rightFootPitch = settings.rightFootYaw = settings.rightFootRoll = 0f;
        }

        private static void ResetHips(ManualIkPoseSettings settings)
        {
            ManualIkPoseSettings defaults = new ManualIkPoseSettings();
            settings.hipsX = defaults.hipsX;
            settings.hipsY = defaults.hipsY;
            settings.hipsZ = defaults.hipsZ;
            settings.hipsPitch = settings.hipsYaw = settings.hipsRoll = 0f;
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
