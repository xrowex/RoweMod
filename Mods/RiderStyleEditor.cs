using System;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Animator_Motion_Systems;
using Il2CppMashBox.Character;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Local rider utilities that do not author per-state poses. The former
    /// Style Lab posture, IK, lean, preview, and preset systems were removed.
    /// </summary>
    public static class RiderStyleEditor
    {
        private const float ResolveIntervalSeconds = 1f;

        private static CharacterManager _characterManager;
        private static VehicleController _vehicle;
        private static Animator _animator;
        private static GameObject _resolvedRoot;
        private static int _resolvedRootId;
        private static int _resolvedVehicleId;
        private static float _nextResolveTime;
        private static float _trackingYaw;
        private static float _trackingPitch;
        private static bool _trackingExpanded = true;
        private static string _runtimeStatus = "Waiting for the local rider.";
        private static RiderToolsSettings _normalizedSettings;

        private static BoneOverlayState _spineOverlay;
        private static BoneOverlayState _chestOverlay;
        private static BoneOverlayState _neckOverlay;
        private static BoneOverlayState _headOverlay;

        private sealed class BoneOverlayState
        {
            public Transform Transform;
            public Quaternion BaseRotation = Quaternion.identity;
            public Quaternion AppliedRotation = Quaternion.identity;
            public bool Applied;
        }

        public static bool RuntimeEnabled =>
            Config.riderToolsSettings?.headTracking?.enabled == true;

        public static void LateUpdate()
        {
            EnsureSettings();
            EnsureRuntimeReferences();

            if (!CanApplyTracking())
            {
                RestoreHeadOverlays();
                return;
            }

            ApplyHeadTracking();
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            RestoreHeadOverlays();
            ClearRuntimeReferences();
            _runtimeStatus = gameplayScene
                ? "Waiting for the local rider."
                : "Rider tools are available in gameplay.";
        }

        public static void OnTabEntered()
        {
            EnsureSettings();
            EnsureRuntimeReferences(true);
        }

        public static void OnTabExited()
        {
        }

        public static void NotifySettingsChanged()
        {
            EnsureSettings();
            if (!RuntimeEnabled)
                RestoreHeadOverlays();
        }

        public static void Cleanup()
        {
            RestoreHeadOverlays();
            ClearRuntimeReferences();
        }

        public static void ResetAll()
        {
            Cleanup();
            BikeOnlyStance.Cleanup();
            Config.bikeOnlyStanceSettings = new BikeOnlyStanceSettings();
            Config.riderToolsSettings = new RiderToolsSettings();
            Config.Save();
            Log.Msg("[RiderTools] Reset bike-only stance and head tracking.");
        }

        public static void DrawTab()
        {
            EnsureSettings();
            EnsureRuntimeReferences();

            float paneHeight = Menu.GetContentPaneHeight(80f);
            Menu.BeginPane(
                "Rider Tools",
                "Local rider utilities without per-state pose, IK, lean, or preset overrides.",
                GUILayout.ExpandWidth(true),
                GUILayout.Height(paneHeight));

            DrawBikeOnlyStance();
            GUILayout.Space(12f);
            DrawHeadTracking(Config.riderToolsSettings.headTracking);
            GUILayout.Space(8f);
            GUILayout.Label(_runtimeStatus, Menu.UiMutedWrappedStyle);
            Menu.EndPane();
        }

        public static void NormalizeSettings(RiderToolsSettings settings)
        {
            if (settings == null)
                return;

            settings.headTracking ??= new RiderHeadTrackingSettings();
            RiderHeadTrackingSettings tracking = settings.headTracking;
            tracking.amount = ClampFinite(tracking.amount, 0f, 1f, 0.5f);
            tracking.maximumYaw =
                ClampFinite(tracking.maximumYaw, 0f, 60f, 24f);
            tracking.maximumPitch =
                ClampFinite(tracking.maximumPitch, 0f, 30f, 12f);
            tracking.smoothing =
                ClampFinite(tracking.smoothing, 0.5f, 20f, 8f);
            tracking.spineWeight =
                ClampFinite(tracking.spineWeight, 0f, 1f, 0.1f);
            tracking.chestWeight =
                ClampFinite(tracking.chestWeight, 0f, 1f, 0.2f);
            tracking.neckWeight =
                ClampFinite(tracking.neckWeight, 0f, 1f, 0.25f);
            tracking.headWeight =
                ClampFinite(tracking.headWeight, 0f, 1f, 0.45f);
            _normalizedSettings = settings;
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
            if (Menu.ModernToggle(
                    "Hold LS 0.5s to Switch Bike Stance",
                    ref holdToSwitch,
                    "bike_stance_hold_left_stick"))
            {
                settings.holdLeftStickToSwitchStance = holdToSwitch;
                Config.RequestSave();
            }

            bool oppoTrickCompatibility = settings.useOppoTrickCompatibility;
            if (Menu.ModernToggle(
                    "Enable Opposite-Stance Tricks (3.2.3 mode)",
                    ref oppoTrickCompatibility,
                    "bike_stance_oppo_tricks"))
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

        private static void DrawHeadTracking(
            RiderHeadTrackingSettings tracking)
        {
            _trackingExpanded =
                Menu.ModernFoldout("Local Head Tracking", _trackingExpanded);
            if (!_trackingExpanded)
                return;

            bool changed = false;
            bool enabled = tracking.enabled;
            if (Menu.ModernToggle(
                    "Enable Look Ahead",
                    ref enabled,
                    "rider_tools_tracking_enabled"))
            {
                tracking.enabled = enabled;
                changed = true;
                if (!enabled)
                    RestoreHeadOverlays();
            }

            tracking.amount = DrawSlider(
                "Tracking Amount",
                tracking.amount,
                0f,
                1f,
                "rider_tools_tracking_amount",
                out bool amountChanged);
            changed |= amountChanged;
            tracking.maximumYaw = DrawSlider(
                "Maximum Yaw",
                tracking.maximumYaw,
                0f,
                60f,
                "rider_tools_tracking_yaw",
                out bool yawChanged);
            changed |= yawChanged;
            tracking.maximumPitch = DrawSlider(
                "Maximum Pitch",
                tracking.maximumPitch,
                0f,
                30f,
                "rider_tools_tracking_pitch",
                out bool pitchChanged);
            changed |= pitchChanged;
            tracking.smoothing = DrawSlider(
                "Smoothing",
                tracking.smoothing,
                0.5f,
                20f,
                "rider_tools_tracking_smoothing",
                out bool smoothingChanged);
            changed |= smoothingChanged;
            tracking.spineWeight = DrawSlider(
                "Spine Weight",
                tracking.spineWeight,
                0f,
                1f,
                "rider_tools_tracking_spine",
                out bool spineChanged);
            changed |= spineChanged;
            tracking.chestWeight = DrawSlider(
                "Chest Weight",
                tracking.chestWeight,
                0f,
                1f,
                "rider_tools_tracking_chest",
                out bool chestChanged);
            changed |= chestChanged;
            tracking.neckWeight = DrawSlider(
                "Neck Weight",
                tracking.neckWeight,
                0f,
                1f,
                "rider_tools_tracking_neck",
                out bool neckChanged);
            changed |= neckChanged;
            tracking.headWeight = DrawSlider(
                "Head Weight",
                tracking.headWeight,
                0f,
                1f,
                "rider_tools_tracking_head",
                out bool headChanged);
            changed |= headChanged;

            if (changed)
            {
                NormalizeSettings(Config.riderToolsSettings);
                Config.RequestSave();
            }
        }

        private static float DrawSlider(
            string label,
            float value,
            float minimum,
            float maximum,
            string controlId,
            out bool changed)
        {
            float previous = value;
            Menu.ModernSlider(
                label,
                ref value,
                minimum,
                maximum,
                controlId);
            changed = !Mathf.Approximately(previous, value);
            return value;
        }

        private static void EnsureSettings()
        {
            Config.riderToolsSettings ??= new RiderToolsSettings();
            if (!ReferenceEquals(
                    _normalizedSettings,
                    Config.riderToolsSettings))
            {
                NormalizeSettings(Config.riderToolsSettings);
            }
        }

        private static void EnsureRuntimeReferences(bool force = false)
        {
            GameObject root = Memory.rMbCharacter;
            int rootId = root != null ? root.GetInstanceID() : 0;
            int vehicleId = Memory.vehicleController != null
                ? Memory.vehicleController.GetInstanceID()
                : 0;
            bool changed =
                rootId != _resolvedRootId ||
                vehicleId != _resolvedVehicleId;
            bool missing =
                _characterManager == null ||
                _vehicle == null ||
                _animator == null;

            if (!force && !changed && !missing)
                return;
            if (!force && !changed && Time.unscaledTime < _nextResolveTime)
                return;

            _nextResolveTime =
                Time.unscaledTime + ResolveIntervalSeconds;
            if (changed)
            {
                RestoreHeadOverlays();
                ClearRuntimeReferences();
            }

            if (root == null)
            {
                _runtimeStatus = "Waiting for the local rider.";
                return;
            }

            _resolvedRoot = root;
            _resolvedRootId = rootId;
            _resolvedVehicleId = vehicleId;
            _characterManager =
                root.GetComponentInChildren<CharacterManager>(true);
            _vehicle = Memory.vehicleController ??
                       root.GetComponentInChildren<VehicleController>(true);
            FullBodyMotionComponent motion =
                root.GetComponentInChildren<FullBodyMotionComponent>(true);
            _animator = motion?._animator ??
                        root.GetComponentInChildren<Animator>(true);
            ResolveHeadBones();

            _runtimeStatus = _animator == null || !_animator.isHuman
                ? "Humanoid rider head bones are unavailable."
                : "Head tracking ready.";
        }

        private static bool CanApplyTracking()
        {
            if (!RuntimeEnabled ||
                !Main.playableSceneLoaded ||
                _resolvedRoot == null ||
                _characterManager == null ||
                _vehicle == null ||
                _animator == null)
            {
                return false;
            }

            try
            {
                return _characterManager.IsLocalPlayer &&
                       _characterManager.IsAlive &&
                       _characterManager.IsDrivingVehicle;
            }
            catch
            {
                return false;
            }
        }

        private static void ResolveHeadBones()
        {
            RestoreHeadOverlays();
            if (_animator == null || !_animator.isHuman)
                return;

            _spineOverlay = CreateBoneOverlay(HumanBodyBones.Spine);
            _chestOverlay = CreateBoneOverlay(HumanBodyBones.Chest);
            _neckOverlay = CreateBoneOverlay(HumanBodyBones.Neck);
            _headOverlay = CreateBoneOverlay(HumanBodyBones.Head);
        }

        private static BoneOverlayState CreateBoneOverlay(
            HumanBodyBones bone)
        {
            Transform target = _animator.GetBoneTransform(bone);
            return target == null
                ? null
                : new BoneOverlayState { Transform = target };
        }

        private static void ApplyHeadTracking()
        {
            RiderHeadTrackingSettings tracking =
                Config.riderToolsSettings.headTracking;
            Vector3 rotation = ComputeTrackingRotation(tracking);

            float spineWeight = Mathf.Max(0f, tracking.spineWeight);
            float chestWeight = Mathf.Max(0f, tracking.chestWeight);
            float neckWeight = Mathf.Max(0f, tracking.neckWeight);
            float headWeight = Mathf.Max(0f, tracking.headWeight);
            float totalWeight =
                spineWeight + chestWeight + neckWeight + headWeight;
            if (totalWeight < 0.0001f)
            {
                headWeight = 1f;
                totalWeight = 1f;
            }

            ApplyBoneOverlay(
                _spineOverlay,
                rotation * (spineWeight / totalWeight));
            ApplyBoneOverlay(
                _chestOverlay,
                rotation * (chestWeight / totalWeight));
            ApplyBoneOverlay(
                _neckOverlay,
                rotation * (neckWeight / totalWeight));
            ApplyBoneOverlay(
                _headOverlay,
                rotation * (headWeight / totalWeight));
        }

        private static Vector3 ComputeTrackingRotation(
            RiderHeadTrackingSettings tracking)
        {
            float targetYaw = 0f;
            float targetPitch = 0f;
            Transform reference = _vehicle.transform;
            Vector3 direction = _vehicle.Velocity;
            if (!_characterManager.Grounded &&
                _vehicle.BestLandingForward.sqrMagnitude > 0.01f)
            {
                direction = _vehicle.BestLandingForward;
            }

            if (direction.sqrMagnitude > 0.01f)
            {
                Vector3 localDirection =
                    reference.InverseTransformDirection(
                        direction.normalized);
                float velocityYaw =
                    Mathf.Atan2(
                        localDirection.x,
                        Mathf.Max(0.001f, localDirection.z)) *
                    Mathf.Rad2Deg;
                float horizontal =
                    Mathf.Sqrt(
                        localDirection.x * localDirection.x +
                        localDirection.z * localDirection.z);
                float directionPitch =
                    -Mathf.Atan2(
                        localDirection.y,
                        Mathf.Max(0.001f, horizontal)) *
                    Mathf.Rad2Deg;
                float steerYaw =
                    _vehicle.SteerInput * tracking.maximumYaw;
                targetYaw = Mathf.Lerp(velocityYaw, steerYaw, 0.35f);
                targetPitch = directionPitch;
            }

            targetYaw = Mathf.Clamp(
                targetYaw,
                -tracking.maximumYaw,
                tracking.maximumYaw) * tracking.amount;
            targetPitch = Mathf.Clamp(
                targetPitch,
                -tracking.maximumPitch,
                tracking.maximumPitch) * tracking.amount;

            float alpha = 1f - Mathf.Exp(
                -tracking.smoothing *
                Mathf.Max(Time.unscaledDeltaTime, 0.001f));
            _trackingYaw =
                Mathf.LerpAngle(_trackingYaw, targetYaw, alpha);
            _trackingPitch =
                Mathf.LerpAngle(_trackingPitch, targetPitch, alpha);
            return new Vector3(
                _trackingPitch,
                _trackingYaw,
                0f);
        }

        private static void ApplyBoneOverlay(
            BoneOverlayState state,
            Vector3 rotationEuler)
        {
            if (state?.Transform == null)
                return;

            Quaternion current = state.Transform.localRotation;
            Quaternion baseRotation =
                state.Applied &&
                Quaternion.Angle(
                    current,
                    state.AppliedRotation) < 0.15f
                    ? state.BaseRotation
                    : current;
            Quaternion applied =
                baseRotation * Quaternion.Euler(rotationEuler);
            state.Transform.localRotation = applied;
            state.BaseRotation = baseRotation;
            state.AppliedRotation = applied;
            state.Applied = true;
        }

        private static void RestoreHeadOverlays()
        {
            RestoreBoneOverlay(_spineOverlay);
            RestoreBoneOverlay(_chestOverlay);
            RestoreBoneOverlay(_neckOverlay);
            RestoreBoneOverlay(_headOverlay);
            _trackingYaw = 0f;
            _trackingPitch = 0f;
        }

        private static void RestoreBoneOverlay(BoneOverlayState state)
        {
            if (state?.Transform == null || !state.Applied)
                return;

            if (Quaternion.Angle(
                    state.Transform.localRotation,
                    state.AppliedRotation) < 0.15f)
            {
                state.Transform.localRotation = state.BaseRotation;
            }

            state.Applied = false;
        }

        private static void ClearRuntimeReferences()
        {
            _characterManager = null;
            _vehicle = null;
            _animator = null;
            _resolvedRoot = null;
            _resolvedRootId = 0;
            _resolvedVehicleId = 0;
            _nextResolveTime = 0f;
            _spineOverlay = null;
            _chestOverlay = null;
            _neckOverlay = null;
            _headOverlay = null;
        }

        private static float ClampFinite(
            float value,
            float minimum,
            float maximum,
            float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;

            return Mathf.Clamp(value, minimum, maximum);
        }
    }
}
