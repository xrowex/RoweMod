using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Il2CppMashBox.Core.Runtime.GrindPoseData;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    public static class GrindPoseEditor
    {
        private const string RequiredPosePrefix = "BMX_";
        private static readonly List<BikeGrindPoseData> PoseCache = new List<BikeGrindPoseData>();
        private static readonly List<string> PoseKeys = new List<string>();
        private static readonly Dictionary<BikeGrindPoseData, PoseDefaults> DefaultPoseValues = new Dictionary<BikeGrindPoseData, PoseDefaults>();
        private static int _selectedPoseIndex;
        private static float _nextRefreshTime;
        private static Vector2 _poseTabsScroll;
        private static readonly Vector3 GrindsTabPlayerOffset = new Vector3(0f, 0.5f, 0f);
        private static bool _isGrindsTabStateApplied;
        private static bool _playerOffsetApplied;
        private static Transform _playerTransformWhenApplied;
        private static bool _springBodyStateCaptured;
        private static Rigidbody _springBodyWhenApplied;
        private static bool _springBodyOriginalIsKinematic;
        private static bool _loggedMissingBikeGrindPoser;
        private static bool _loggedEmptyPoseCache;
        private static bool _loggedNullSelectedPose;
        private static bool _loggedMissingForcePoseField;
        private static bool _loggedForcePoseTypeMismatch;
        private static bool _loggedMissingForcedGrindPoseField;
        private static bool _loggedForcedGrindPoseTypeMismatch;
        private static bool _loggedMissingAnimatedVehicleEventResponder;
        private static bool _loggedMissingAnimatedOnJump;
        private static bool _animatedOnJumpInvokedThisOpen;
        private static int _lastLoggedForcedPoseIndex = -1;
        private static string _lastLoggedForcedPoseName;
        
        private sealed class PoseDefaults
        {
            public bool AirPoseOnly;
            public bool MirrorCranks;
            public float BarsRotation;
            public float CranksRotation;
            public float FrontWheelColliderRotYaw;
            public float LeftPedalRotation;
            public float NormalPushOffset;
            public float RearWheelColliderRotYaw;
            public float RearWheelPhysicsSteerAngle;
            public float Rider_ElbowsRoll;
            public float Rider_HeadTwist;
            public float Rider_KneesRoll;
            public float Rider_LeftElbowRoll;
            public float Rider_LeftKneeRoll;
            public float Rider_RightElbowRoll;
            public float Rider_RightKneeRoll;
            public float Rider_SpineCurl;
            public float Rider_SpineTwist;
            public float RightPedalRotation;
            public Vector3 ChassisCM;
            public Vector3 DriverCM;
            public Vector3 RearWheelColliderPos;
            public Vector3 Rider_HipsPos;
            public Vector3 Rider_HipsRot;
            public Vector3 RootPosition;
            public Vector3 RootPositionConnected;
            public Vector3 RootRotation;
        }

        public static void DrawGrindPoseTab()
        {
            OnGrindsTabEntered();
            RefreshPoseCache();

            GUILayout.Label("Grind Poses", Menu.coloredBoxStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<b>REFRESH POSES</b>", Menu.highQualityButtonStyle, GUILayout.Width(170f), GUILayout.Height(28f)))
            {
                RefreshPoseCache(true);
            }

            if (GUILayout.Button("<b>APPLY SAVED</b>", Menu.highQualityButtonStyle, GUILayout.Width(170f), GUILayout.Height(28f)))
            {
                ApplyConfigToRuntime(true);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            if (GUILayout.Button("<b>RESET ALL TO DEFAULT</b>", Menu.redButtonStyle, GUILayout.Width(345f), GUILayout.Height(28f)))
            {
                ResetAllPosesToDefault();
            }
            GUILayout.Space(6f);

            if (PoseCache.Count == 0)
            {
                GUILayout.Label("No BikeGrindPoseData instances found yet.", Menu.labelStyle);
                return;
            }

            DrawPoseTabs();
            GUILayout.Space(8f);

            if (_selectedPoseIndex < 0 || _selectedPoseIndex >= PoseCache.Count)
            {
                _selectedPoseIndex = 0;
            }

            BikeGrindPoseData selectedPose = PoseCache[_selectedPoseIndex];
            string poseKey = PoseKeys[_selectedPoseIndex];
            bool changed = DrawPoseEditor(selectedPose, poseKey);
            if (changed)
            {
                Config.Save();
            }
        }

        public static void OnGrindsTabEntered()
        {
            if (!_isGrindsTabStateApplied)
            {
                Log.Msg("[Grinds] Entered grinds tab.");
            }

            _isGrindsTabStateApplied = true;

            RefreshPoseCache();
            ApplyForcedPoseFromSelection();
            InvokeAnimatedOnJumpOncePerOpen();

            if (!_playerOffsetApplied && Memory.customizableEntity != null)
            {
                _playerTransformWhenApplied = Memory.customizableEntity.transform;
                if (_playerTransformWhenApplied != null)
                {
                    _playerTransformWhenApplied.position += GrindsTabPlayerOffset;
                    _playerOffsetApplied = true;
                }
            }

            if (!_springBodyStateCaptured && Memory.springBody != null)
            {
                _springBodyWhenApplied = Memory.springBody;
                _springBodyOriginalIsKinematic = _springBodyWhenApplied.isKinematic;
                _springBodyWhenApplied.isKinematic = true;
                _springBodyStateCaptured = true;
            }
        }

        private static void ApplyForcedPoseFromSelection()
        {
            if (Memory.bikeGrindPoser == null)
            {
                if (!_loggedMissingBikeGrindPoser)
                {
                    Log.Warning("[Grinds] Memory.bikeGrindPoser is null; cannot force grind pose.");
                    _loggedMissingBikeGrindPoser = true;
                }
                return;
            }

            if (_loggedMissingBikeGrindPoser)
            {
                Log.Msg("[Grinds] Memory.bikeGrindPoser reference restored.");
                _loggedMissingBikeGrindPoser = false;
            }

            if (PoseCache.Count == 0)
            {
                if (!_loggedEmptyPoseCache)
                {
                    Log.Warning("[Grinds] Pose cache is empty; cannot set _forcedGrindPose yet.");
                    _loggedEmptyPoseCache = true;
                }
                return;
            }

            if (_loggedEmptyPoseCache)
            {
                Log.Msg($"[Grinds] Pose cache ready with {PoseCache.Count} entries.");
                _loggedEmptyPoseCache = false;
            }

            if (_selectedPoseIndex < 0 || _selectedPoseIndex >= PoseCache.Count)
            {
                Log.Warning($"[Grinds] Selected pose index {_selectedPoseIndex} out of range; resetting to 0.");
                _selectedPoseIndex = 0;
            }

            BikeGrindPoseData selectedPose = PoseCache[_selectedPoseIndex];
            if (selectedPose == null)
            {
                if (!_loggedNullSelectedPose)
                {
                    Log.Warning($"[Grinds] Selected pose at index {_selectedPoseIndex} is null.");
                    _loggedNullSelectedPose = true;
                }
                return;
            }

            _loggedNullSelectedPose = false;

            Memory.bikeGrindPoser._forcePose = true;
            Memory.bikeGrindPoser._forcedGrindPose = selectedPose;

            if (_loggedMissingForcePoseField || _loggedForcePoseTypeMismatch)
            {
                Log.Msg("[Grinds] _forcePose member resolved.");
                _loggedMissingForcePoseField = false;
                _loggedForcePoseTypeMismatch = false;
            }

            if (_loggedMissingForcedGrindPoseField || _loggedForcedGrindPoseTypeMismatch)
            {
                Log.Msg("[Grinds] _forcedGrindPose member resolved.");
                _loggedMissingForcedGrindPoseField = false;
                _loggedForcedGrindPoseTypeMismatch = false;
            }

            string selectedPoseName = GetPoseDebugName(selectedPose);
            if (_lastLoggedForcedPoseIndex != _selectedPoseIndex ||
                !string.Equals(_lastLoggedForcedPoseName, selectedPoseName, StringComparison.Ordinal))
            {
                Log.Msg($"[Grinds] Forced pose applied. index={_selectedPoseIndex}, pose='{selectedPoseName}'.");
                _lastLoggedForcedPoseIndex = _selectedPoseIndex;
                _lastLoggedForcedPoseName = selectedPoseName;
            }
        }

        private static void InvokeAnimatedOnJumpOncePerOpen()
        {
            if (_animatedOnJumpInvokedThisOpen)
            {
                return;
            }

            if (Memory.animatedVehicleEventResponder == null)
            {
                if (!_loggedMissingAnimatedVehicleEventResponder)
                {
                    Log.Warning("[Grinds] Memory.animatedVehicleEventResponder is null; cannot invoke OnJump.");
                    _loggedMissingAnimatedVehicleEventResponder = true;
                }
                return;
            }

            if (_loggedMissingAnimatedVehicleEventResponder)
            {
                Log.Msg("[Grinds] AnimatedVehicleEventResponder reference restored.");
                _loggedMissingAnimatedVehicleEventResponder = false;
            }

            var onJump = Memory.animatedVehicleEventResponder.OnJump;
            if (onJump == null)
            {
                if (!_loggedMissingAnimatedOnJump)
                {
                    Log.Warning("[Grinds] AnimatedVehicleEventResponder.OnJump is null; cannot invoke.");
                    _loggedMissingAnimatedOnJump = true;
                }
                return;
            }

            if (_loggedMissingAnimatedOnJump)
            {
                Log.Msg("[Grinds] AnimatedVehicleEventResponder.OnJump reference restored.");
                _loggedMissingAnimatedOnJump = false;
            }

            onJump.Invoke(1);
            _animatedOnJumpInvokedThisOpen = true;
            Log.Msg("[Grinds] Invoked AnimatedVehicleEventResponder.OnJump(1) for this tab open.");
        }

        private static string GetPoseDebugName(BikeGrindPoseData pose)
        {
            if (pose == null)
            {
                return "<null>";
            }

            if (!string.IsNullOrWhiteSpace(pose.GrindName))
            {
                return pose.GrindName;
            }

            if (!string.IsNullOrWhiteSpace(pose.name))
            {
                return pose.name;
            }

            if (_selectedPoseIndex >= 0 && _selectedPoseIndex < PoseKeys.Count)
            {
                return PoseKeys[_selectedPoseIndex];
            }

            return $"Pose {_selectedPoseIndex}";
        }

        public static void OnGrindsTabExited()
        {
            if (!_isGrindsTabStateApplied)
            {
                return;
            }

            if (Memory.bikeGrindPoser != null)
            {
                Memory.bikeGrindPoser._forcePose = false;
                Memory.bikeGrindPoser._forcedGrindPose = null;
                Log.Msg("[Grinds] Cleared forced pose on tab exit.");
            }

            if (_playerOffsetApplied && _playerTransformWhenApplied != null)
            {
                _playerTransformWhenApplied.position -= GrindsTabPlayerOffset;
            }

            if (_springBodyStateCaptured && _springBodyWhenApplied != null)
            {
                _springBodyWhenApplied.isKinematic = _springBodyOriginalIsKinematic;
            }

            _isGrindsTabStateApplied = false;
            _playerOffsetApplied = false;
            _playerTransformWhenApplied = null;
            _springBodyStateCaptured = false;
            _springBodyWhenApplied = null;
            _lastLoggedForcedPoseIndex = -1;
            _lastLoggedForcedPoseName = null;
            _loggedMissingBikeGrindPoser = false;
            _loggedEmptyPoseCache = false;
            _loggedNullSelectedPose = false;
            _loggedMissingForcePoseField = false;
            _loggedForcePoseTypeMismatch = false;
            _loggedMissingForcedGrindPoseField = false;
            _loggedForcedGrindPoseTypeMismatch = false;
            _loggedMissingAnimatedVehicleEventResponder = false;
            _loggedMissingAnimatedOnJump = false;
            _animatedOnJumpInvokedThisOpen = false;
            Log.Msg("[Grinds] Exited grinds tab.");
        }

        public static void ApplyConfigToRuntime(bool forceRefresh = true)
        {
            if (forceRefresh)
            {
                RefreshPoseCache(true);
            }

            if (Config.grindPoseData?.poses == null || Config.grindPoseData.poses.Count == 0)
            {
                return;
            }

            int appliedCount = 0;
            for (int i = 0; i < PoseCache.Count; i++)
            {
                BikeGrindPoseData pose = PoseCache[i];
                string poseKey = PoseKeys[i];

                if (!TryGetConfigEntryForPose(pose, poseKey, out GrindPoseConfigEntry entry) || entry == null)
                {
                    continue;
                }

                if (entry.boolFields != null)
                {
                    foreach (var kvp in entry.boolFields)
                    {
                        if (TryApplyBoolField(pose, kvp.Key, kvp.Value))
                        {
                            appliedCount++;
                        }
                    }
                }

                if (entry.floatFields != null)
                {
                    foreach (var kvp in entry.floatFields)
                    {
                        if (TryApplyFloatField(pose, kvp.Key, kvp.Value))
                        {
                            appliedCount++;
                        }
                    }
                }

                if (entry.vector3Fields != null)
                {
                    foreach (var kvp in entry.vector3Fields)
                    {
                        Vector3 value = new Vector3(kvp.Value.x, kvp.Value.y, kvp.Value.z);
                        if (TryApplyVector3Field(pose, kvp.Key, value))
                        {
                            appliedCount++;
                        }
                    }
                }
            }

            if (appliedCount > 0)
            {
                Log.Msg($"Applied {appliedCount} grind pose values from config.");
            }
        }

        public static void ResetAllPosesToDefault()
        {
            RefreshPoseCache(true);
            if (PoseCache.Count == 0)
            {
                Config.ResetGrindsTab();
                Config.Save();
                return;
            }

            int resetCount = 0;
            for (int i = 0; i < PoseCache.Count; i++)
            {
                BikeGrindPoseData pose = PoseCache[i];
                if (pose == null)
                {
                    continue;
                }

                if (!DefaultPoseValues.TryGetValue(pose, out PoseDefaults defaults) || defaults == null)
                {
                    defaults = CaptureDefaults(pose);
                    DefaultPoseValues[pose] = defaults;
                }

                ApplyDefaults(pose, defaults);
                resetCount++;
            }

            Config.ResetGrindsTab();
            Config.Save();
            Log.Msg($"Reset {resetCount} grind poses to captured defaults.");
        }

        public static IEnumerator DelayedApplyConfigRoutine()
        {
            yield return new WaitForSeconds(0.75f);
            ApplyConfigToRuntime(true);
            yield return new WaitForSeconds(2f);
            ApplyConfigToRuntime(true);
        }

        private static bool DrawPoseEditor(BikeGrindPoseData pose, string poseKey)
        {
            bool changed = false;

            GUILayout.Label($"Editing: {poseKey}", Menu.coloredBoxStyle);
            if (!string.IsNullOrWhiteSpace(pose.name) && pose.name != pose.GrindName)
            {
                GUILayout.Label($"Asset: {pose.name}", Menu.labelStyle);
            }

            changed |= DrawBoolField("Air Pose Only", poseKey, "AirPoseOnly", pose.AirPoseOnly, v => pose.AirPoseOnly = v);
            changed |= DrawBoolField("Mirror Cranks", poseKey, "MirrorCranks", pose.MirrorCranks, v => pose.MirrorCranks = v);

            changed |= DrawFloatField("Bars Rotation", poseKey, "BarsRotation", pose.BarsRotation, -360f, 360f, v => pose.BarsRotation = v);
            changed |= DrawFloatField("Cranks Rotation", poseKey, "CranksRotation", pose.CranksRotation, -360f, 360f, v => pose.CranksRotation = v);
            changed |= DrawFloatField("Front Wheel Collider Rot Yaw", poseKey, "FrontWheelColliderRotYaw", pose.FrontWheelColliderRotYaw, -360f, 360f, v => pose.FrontWheelColliderRotYaw = v);
            changed |= DrawFloatField("Left Pedal Rotation", poseKey, "LeftPedalRotation", pose.LeftPedalRotation, -360f, 360f, v => pose.LeftPedalRotation = v);
            changed |= DrawFloatField("Normal Push Offset", poseKey, "NormalPushOffset", pose.NormalPushOffset, -2f, 2f, v => pose.NormalPushOffset = v);
            changed |= DrawFloatField("Rear Wheel Collider Rot Yaw", poseKey, "RearWheelColliderRotYaw", pose.RearWheelColliderRotYaw, -360f, 360f, v => pose.RearWheelColliderRotYaw = v);
            changed |= DrawFloatField("Rear Wheel Physics Steer Angle", poseKey, "RearWheelPhysicsSteerAngle", pose.RearWheelPhysicsSteerAngle, -180f, 180f, v => pose.RearWheelPhysicsSteerAngle = v);

            changed |= DrawFloatField("Rider Elbows Roll", poseKey, "Rider_ElbowsRoll", pose.Rider_ElbowsRoll, -360f, 360f, v => pose.Rider_ElbowsRoll = v);
            changed |= DrawFloatField("Rider Head Twist", poseKey, "Rider_HeadTwist", pose.Rider_HeadTwist, -360f, 360f, v => pose.Rider_HeadTwist = v);
            changed |= DrawFloatField("Rider Knees Roll", poseKey, "Rider_KneesRoll", pose.Rider_KneesRoll, -360f, 360f, v => pose.Rider_KneesRoll = v);
            changed |= DrawFloatField("Rider Left Elbow Roll", poseKey, "Rider_LeftElbowRoll", pose.Rider_LeftElbowRoll, -360f, 360f, v => pose.Rider_LeftElbowRoll = v);
            changed |= DrawFloatField("Rider Left Knee Roll", poseKey, "Rider_LeftKneeRoll", pose.Rider_LeftKneeRoll, -360f, 360f, v => pose.Rider_LeftKneeRoll = v);
            changed |= DrawFloatField("Rider Right Elbow Roll", poseKey, "Rider_RightElbowRoll", pose.Rider_RightElbowRoll, -360f, 360f, v => pose.Rider_RightElbowRoll = v);
            changed |= DrawFloatField("Rider Right Knee Roll", poseKey, "Rider_RightKneeRoll", pose.Rider_RightKneeRoll, -360f, 360f, v => pose.Rider_RightKneeRoll = v);
            changed |= DrawFloatField("Rider Spine Curl", poseKey, "Rider_SpineCurl", pose.Rider_SpineCurl, -360f, 360f, v => pose.Rider_SpineCurl = v);
            changed |= DrawFloatField("Rider Spine Twist", poseKey, "Rider_SpineTwist", pose.Rider_SpineTwist, -360f, 360f, v => pose.Rider_SpineTwist = v);
            changed |= DrawFloatField("Right Pedal Rotation", poseKey, "RightPedalRotation", pose.RightPedalRotation, -360f, 360f, v => pose.RightPedalRotation = v);

            changed |= DrawVector3Field("Chassis CM", poseKey, "ChassisCM", pose.ChassisCM, -2f, 2f, v => pose.ChassisCM = v);
            changed |= DrawVector3Field("Driver CM", poseKey, "DriverCM", pose.DriverCM, -2f, 2f, v => pose.DriverCM = v);
            changed |= DrawVector3Field("Rear Wheel Collider Pos", poseKey, "RearWheelColliderPos", pose.RearWheelColliderPos, -2f, 2f, v => pose.RearWheelColliderPos = v);
            changed |= DrawVector3Field("Rider Hips Pos", poseKey, "Rider_HipsPos", pose.Rider_HipsPos, -2f, 2f, v => pose.Rider_HipsPos = v);
            changed |= DrawVector3Field("Rider Hips Rot", poseKey, "Rider_HipsRot", pose.Rider_HipsRot, -360f, 360f, v => pose.Rider_HipsRot = v);
            changed |= DrawVector3Field("Root Position", poseKey, "RootPosition", pose.RootPosition, -5f, 5f, v => pose.RootPosition = v);
            changed |= DrawVector3Field("Root Position Connected", poseKey, "RootPositionConnected", pose.RootPositionConnected, -5f, 5f, v => pose.RootPositionConnected = v);
            changed |= DrawVector3Field("Root Rotation", poseKey, "RootRotation", pose.RootRotation, -360f, 360f, v => pose.RootRotation = v);

            return changed;
        }

        private static bool DrawBoolField(string label, string poseKey, string fieldName, bool currentValue, Action<bool> apply)
        {
            bool newValue = currentValue;
            Menu.ModernToggle(label, ref newValue, $"grinds_{poseKey}_{fieldName}");
            if (newValue == currentValue)
            {
                return false;
            }

            apply(newValue);
            PersistBool(poseKey, fieldName, newValue);
            return true;
        }

        private static bool DrawFloatField(string label, string poseKey, string fieldName, float currentValue, float min, float max, Action<float> apply)
        {
            float newValue = currentValue;
            Menu.ModernSlider(label, ref newValue, min, max, $"grinds_{poseKey}_{fieldName}");
            if (Mathf.Approximately(newValue, currentValue))
            {
                return false;
            }

            apply(newValue);
            PersistFloat(poseKey, fieldName, newValue);
            return true;
        }

        private static bool DrawVector3Field(string label, string poseKey, string fieldName, Vector3 currentValue, float min, float max, Action<Vector3> apply)
        {
            float x = currentValue.x;
            float y = currentValue.y;
            float z = currentValue.z;

            Menu.ModernSlider($"{label} X", ref x, min, max, $"grinds_{poseKey}_{fieldName}_x");
            Menu.ModernSlider($"{label} Y", ref y, min, max, $"grinds_{poseKey}_{fieldName}_y");
            Menu.ModernSlider($"{label} Z", ref z, min, max, $"grinds_{poseKey}_{fieldName}_z");

            Vector3 newValue = new Vector3(x, y, z);
            if (Approximately(currentValue, newValue))
            {
                return false;
            }

            apply(newValue);
            PersistVector3(poseKey, fieldName, newValue);
            return true;
        }
        
        private static void DrawPoseTabs()
        {
            _poseTabsScroll = GUILayout.BeginScrollView(_poseTabsScroll, GUILayout.Height(120f));
            float availableWidth = Mathf.Max(240f, Menu.windowRect.width - 70f);
            int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / 150f));
            int index = 0;

            while (index < PoseCache.Count)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < columns && index < PoseCache.Count; col++)
                {
                    GUIStyle style = index == _selectedPoseIndex ? Menu.activeTabButtonStyle : Menu.highQualityButtonStyle;
                    string label = PoseKeys[index];
                    if (label.Length > 22)
                    {
                        label = label.Substring(0, 22) + "...";
                    }

                    int capturedIndex = index;
                    if (GUILayout.Button($"<b>{label}</b>", style, GUILayout.Width(145f), GUILayout.Height(28f)))
                    {
                        _selectedPoseIndex = capturedIndex;
                    }

                    index++;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private static void RefreshPoseCache(bool force = false)
        {
            if (!force && Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + 2f;

            PoseCache.Clear();
            PoseKeys.Clear();

            try
            {
                BikeGrindPoseData[] foundPoses = Resources.FindObjectsOfTypeAll<BikeGrindPoseData>();
                if (foundPoses == null || foundPoses.Length == 0)
                {
                    return;
                }

                PoseCache.AddRange(foundPoses.Where(p => p != null && IsBmxPose(p)));
                CaptureDefaultValuesForCachedPoses();

                var keyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < PoseCache.Count; i++)
                {
                    string baseKey = BuildPoseKey(PoseCache[i], i);
                    if (keyCounts.TryGetValue(baseKey, out int count))
                    {
                        count++;
                        keyCounts[baseKey] = count;
                        PoseKeys.Add($"{baseKey} ({count})");
                    }
                    else
                    {
                        keyCounts[baseKey] = 1;
                        PoseKeys.Add(baseKey);
                    }
                }

                if (_selectedPoseIndex >= PoseCache.Count)
                {
                    _selectedPoseIndex = 0;
                }
            }
            catch (Exception ex)
            {
                PoseCache.Clear();
                PoseKeys.Clear();
                Log.Error($"Failed to refresh BikeGrindPoseData cache: {ex.Message}");
            }
        }

        private static string BuildPoseKey(BikeGrindPoseData pose, int index)
        {
            if (!string.IsNullOrWhiteSpace(pose.GrindName))
            {
                return EnsureBmxPrefix(pose.GrindName);
            }

            if (!string.IsNullOrWhiteSpace(pose.name))
            {
                return EnsureBmxPrefix(pose.name);
            }

            return $"{RequiredPosePrefix}Grind Pose {index + 1}";
        }

        private static bool TryGetConfigEntryForPose(BikeGrindPoseData pose, string poseKey, out GrindPoseConfigEntry entry)
        {
            entry = null;
            if (Config.grindPoseData?.poses == null)
            {
                return false;
            }

            if (Config.grindPoseData.poses.TryGetValue(poseKey, out entry) && entry != null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(pose.GrindName) &&
                Config.grindPoseData.poses.TryGetValue(pose.GrindName, out entry) &&
                entry != null)
            {
                return true;
            }

            string normalizedGrindName = EnsureBmxPrefix(pose.GrindName);
            if (!string.IsNullOrWhiteSpace(normalizedGrindName) &&
                Config.grindPoseData.poses.TryGetValue(normalizedGrindName, out entry) &&
                entry != null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(pose.name) &&
                Config.grindPoseData.poses.TryGetValue(pose.name, out entry) &&
                entry != null)
            {
                return true;
            }

            string normalizedObjectName = EnsureBmxPrefix(pose.name);
            if (!string.IsNullOrWhiteSpace(normalizedObjectName) &&
                Config.grindPoseData.poses.TryGetValue(normalizedObjectName, out entry) &&
                entry != null)
            {
                return true;
            }

            return false;
        }
        
        private static void CaptureDefaultValuesForCachedPoses()
        {
            var currentPoses = new HashSet<BikeGrindPoseData>(PoseCache.Where(p => p != null));
            List<BikeGrindPoseData> staleKeys = DefaultPoseValues.Keys.Where(p => p == null || !currentPoses.Contains(p)).ToList();
            for (int i = 0; i < staleKeys.Count; i++)
            {
                DefaultPoseValues.Remove(staleKeys[i]);
            }

            for (int i = 0; i < PoseCache.Count; i++)
            {
                BikeGrindPoseData pose = PoseCache[i];
                if (pose == null || DefaultPoseValues.ContainsKey(pose))
                {
                    continue;
                }

                DefaultPoseValues[pose] = CaptureDefaults(pose);
            }
        }

        private static PoseDefaults CaptureDefaults(BikeGrindPoseData pose)
        {
            return new PoseDefaults
            {
                AirPoseOnly = pose.AirPoseOnly,
                MirrorCranks = pose.MirrorCranks,
                BarsRotation = pose.BarsRotation,
                CranksRotation = pose.CranksRotation,
                FrontWheelColliderRotYaw = pose.FrontWheelColliderRotYaw,
                LeftPedalRotation = pose.LeftPedalRotation,
                NormalPushOffset = pose.NormalPushOffset,
                RearWheelColliderRotYaw = pose.RearWheelColliderRotYaw,
                RearWheelPhysicsSteerAngle = pose.RearWheelPhysicsSteerAngle,
                Rider_ElbowsRoll = pose.Rider_ElbowsRoll,
                Rider_HeadTwist = pose.Rider_HeadTwist,
                Rider_KneesRoll = pose.Rider_KneesRoll,
                Rider_LeftElbowRoll = pose.Rider_LeftElbowRoll,
                Rider_LeftKneeRoll = pose.Rider_LeftKneeRoll,
                Rider_RightElbowRoll = pose.Rider_RightElbowRoll,
                Rider_RightKneeRoll = pose.Rider_RightKneeRoll,
                Rider_SpineCurl = pose.Rider_SpineCurl,
                Rider_SpineTwist = pose.Rider_SpineTwist,
                RightPedalRotation = pose.RightPedalRotation,
                ChassisCM = pose.ChassisCM,
                DriverCM = pose.DriverCM,
                RearWheelColliderPos = pose.RearWheelColliderPos,
                Rider_HipsPos = pose.Rider_HipsPos,
                Rider_HipsRot = pose.Rider_HipsRot,
                RootPosition = pose.RootPosition,
                RootPositionConnected = pose.RootPositionConnected,
                RootRotation = pose.RootRotation
            };
        }

        private static void ApplyDefaults(BikeGrindPoseData pose, PoseDefaults defaults)
        {
            pose.AirPoseOnly = defaults.AirPoseOnly;
            pose.MirrorCranks = defaults.MirrorCranks;
            pose.BarsRotation = defaults.BarsRotation;
            pose.CranksRotation = defaults.CranksRotation;
            pose.FrontWheelColliderRotYaw = defaults.FrontWheelColliderRotYaw;
            pose.LeftPedalRotation = defaults.LeftPedalRotation;
            pose.NormalPushOffset = defaults.NormalPushOffset;
            pose.RearWheelColliderRotYaw = defaults.RearWheelColliderRotYaw;
            pose.RearWheelPhysicsSteerAngle = defaults.RearWheelPhysicsSteerAngle;
            pose.Rider_ElbowsRoll = defaults.Rider_ElbowsRoll;
            pose.Rider_HeadTwist = defaults.Rider_HeadTwist;
            pose.Rider_KneesRoll = defaults.Rider_KneesRoll;
            pose.Rider_LeftElbowRoll = defaults.Rider_LeftElbowRoll;
            pose.Rider_LeftKneeRoll = defaults.Rider_LeftKneeRoll;
            pose.Rider_RightElbowRoll = defaults.Rider_RightElbowRoll;
            pose.Rider_RightKneeRoll = defaults.Rider_RightKneeRoll;
            pose.Rider_SpineCurl = defaults.Rider_SpineCurl;
            pose.Rider_SpineTwist = defaults.Rider_SpineTwist;
            pose.RightPedalRotation = defaults.RightPedalRotation;
            pose.ChassisCM = defaults.ChassisCM;
            pose.DriverCM = defaults.DriverCM;
            pose.RearWheelColliderPos = defaults.RearWheelColliderPos;
            pose.Rider_HipsPos = defaults.Rider_HipsPos;
            pose.Rider_HipsRot = defaults.Rider_HipsRot;
            pose.RootPosition = defaults.RootPosition;
            pose.RootPositionConnected = defaults.RootPositionConnected;
            pose.RootRotation = defaults.RootRotation;
        }

        private static void PersistBool(string poseKey, string fieldName, bool value)
        {
            GrindPoseConfigEntry entry = Config.GetOrCreateGrindPoseEntry(poseKey);
            entry.boolFields[fieldName] = value;
        }

        private static void PersistFloat(string poseKey, string fieldName, float value)
        {
            GrindPoseConfigEntry entry = Config.GetOrCreateGrindPoseEntry(poseKey);
            entry.floatFields[fieldName] = value;
        }

        private static void PersistVector3(string poseKey, string fieldName, Vector3 value)
        {
            GrindPoseConfigEntry entry = Config.GetOrCreateGrindPoseEntry(poseKey);
            entry.vector3Fields[fieldName] = new SerializableVector3(value.x, value.y, value.z);
        }

        private static bool TryApplyBoolField(BikeGrindPoseData pose, string fieldName, bool value)
        {
            switch (fieldName)
            {
                case "AirPoseOnly":
                    pose.AirPoseOnly = value;
                    return true;
                case "MirrorCranks":
                    pose.MirrorCranks = value;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryApplyFloatField(BikeGrindPoseData pose, string fieldName, float value)
        {
            switch (fieldName)
            {
                case "BarsRotation": pose.BarsRotation = value; return true;
                case "CranksRotation": pose.CranksRotation = value; return true;
                case "FrontWheelColliderRotYaw": pose.FrontWheelColliderRotYaw = value; return true;
                case "LeftPedalRotation": pose.LeftPedalRotation = value; return true;
                case "NormalPushOffset": pose.NormalPushOffset = value; return true;
                case "RearWheelColliderRotYaw": pose.RearWheelColliderRotYaw = value; return true;
                case "RearWheelPhysicsSteerAngle": pose.RearWheelPhysicsSteerAngle = value; return true;
                case "Rider_ElbowsRoll": pose.Rider_ElbowsRoll = value; return true;
                case "Rider_HeadTwist": pose.Rider_HeadTwist = value; return true;
                case "Rider_KneesRoll": pose.Rider_KneesRoll = value; return true;
                case "Rider_LeftElbowRoll": pose.Rider_LeftElbowRoll = value; return true;
                case "Rider_LeftKneeRoll": pose.Rider_LeftKneeRoll = value; return true;
                case "Rider_RightElbowRoll": pose.Rider_RightElbowRoll = value; return true;
                case "Rider_RightKneeRoll": pose.Rider_RightKneeRoll = value; return true;
                case "Rider_SpineCurl": pose.Rider_SpineCurl = value; return true;
                case "Rider_SpineTwist": pose.Rider_SpineTwist = value; return true;
                case "RightPedalRotation": pose.RightPedalRotation = value; return true;
                default:
                    return false;
            }
        }

        private static bool TryApplyVector3Field(BikeGrindPoseData pose, string fieldName, Vector3 value)
        {
            switch (fieldName)
            {
                case "ChassisCM": pose.ChassisCM = value; return true;
                case "DriverCM": pose.DriverCM = value; return true;
                case "RearWheelColliderPos": pose.RearWheelColliderPos = value; return true;
                case "Rider_HipsPos": pose.Rider_HipsPos = value; return true;
                case "Rider_HipsRot": pose.Rider_HipsRot = value; return true;
                case "RootPosition": pose.RootPosition = value; return true;
                case "RootPositionConnected": pose.RootPositionConnected = value; return true;
                case "RootRotation": pose.RootRotation = value; return true;
                default:
                    return false;
            }
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x)
                   && Mathf.Approximately(a.y, b.y)
                   && Mathf.Approximately(a.z, b.z);
        }

        private static bool IsBmxPose(BikeGrindPoseData pose)
        {
            return StartsWithRequiredPrefix(pose.GrindName) || StartsWithRequiredPrefix(pose.name);
        }

        private static bool StartsWithRequiredPrefix(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.StartsWith(RequiredPosePrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureBmxPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (StartsWithRequiredPrefix(value))
            {
                return value;
            }

            return RequiredPosePrefix + value.TrimStart('_');
        }
    }
}
