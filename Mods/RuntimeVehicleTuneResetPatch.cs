using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppInterop.Runtime;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion;
using Il2CppMashBox.Core.Runtime.Physics.Raycasting.SurfaceScanning;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using rowemod.Utils;
using UnityEngine;
using Il2CppFieldInfo = Il2CppSystem.Reflection.FieldInfo;
using Il2CppObject = Il2CppSystem.Object;
using Il2CppType = Il2CppSystem.Type;

namespace rowemod.Mods
{
    /// <summary>
    /// Replaces the native Ctrl+Shift+U vehicle inspector with a RoweMod-owned
    /// window. Each label, editor, and Reset button is laid out in one row, so
    /// reset controls cannot drift away from the values they restore.
    /// </summary>
    internal static class RuntimeVehicleTuneResetSupport
    {
        private const int WindowId = 1874126;
        private const int MaxObjectDepth = 5;
        private const float MinimumWindowWidth = 760f;
        private const float MinimumWindowHeight = 520f;
        private const float ScreenMargin = 24f;
        private const float RowHeight = 38f;
        private const float ResetWidth = 78f;
        private const float AxisLabelWidth = 18f;

        private static readonly Dictionary<string, string> TextEdits =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> ExpandedPaths =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, SliderRange> SliderRanges =
            new Dictionary<string, SliderRange>(StringComparer.Ordinal);

        private static RuntimeVehicleTuneMenu _menu;
        private static MotorVehicleSettings _defaultSettings;
        private static TransitionSettings _defaultTransitionSettings;
        private static FlightAugmentSettings _defaultFlightAugmentSettings;
        private static GrindMagnetZEM _tireRideMagnet;
        private static TireRideMagnetSnapshot _defaultTireRideMagnet;
        private static int _settingsInstanceId = int.MinValue;
        private static OnePointOhFlipSystem _flipSystem;
        private static FlipSystemSnapshot _flipSystemDefaults;
        private static int _flipSystemInstanceId = int.MinValue;
        private static bool _customOpen;
        private static bool _loggedInspector;
        private static Rect _windowRect;
        private static Vector2 _scroll;
        private static string _search = string.Empty;
        private static Vector2 _presetScroll;
        private static string _newPresetName = string.Empty;
        private static int _selectedPresetIndex;
        private static string _loadedPresetName;
        private static bool _presetsExpanded = true;
        private static bool _flipSystemExpanded = true;
        private static bool _flipDynamicsExpanded;
        private static bool _spinDynamicsExpanded;
        private static bool _rotationDriveExpanded;
        private static bool _transitionSettingsExpanded;
        private static bool _flightAugmentSettingsExpanded;
        private static bool _tireRideSettingsExpanded;
        private static float _controllerScrollViewHeight = 320f;
        private static float _controllerScrollContentHeight = 320f;
        private static string _openEnumPath;
        private static readonly Dictionary<string, float> PendingNumericValues =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private static bool _numericEditorOpen;
        private static bool _numericEditorIsInteger;
        private static bool _numericEditorFocusPending;
        private static string _numericEditorPath;
        private static string _numericEditorLabel;
        private static string _numericEditorText;
        private static float _numericEditorMinimum;
        private static float _numericEditorMaximum;

        private static GUIStyle _windowStyle;
        private static GUIStyle _rowStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _mutedStyle;
        private static GUIStyle _resetStyle;
        private static GUIStyle _resetDefaultStyle;
        private static Texture2D _windowTexture;
        private static Texture2D _rowTexture;
        private static Texture2D _resetTexture;
        private static Texture2D _resetHoverTexture;
        private static Texture2D _resetActiveTexture;
        private static Texture2D _resetDefaultTexture;

        public static bool IsOpen => _customOpen;

        private sealed class FlipSystemSnapshot
        {
            public Vector3 FlipAxis;
            public Vector3 SpinAxis;
            public bool SpinFirst;
            public float SpinMultiplier;
            public float FlipMultiplier;
            public float DefaultTrickDuration;
            public float MinimumTrickDuration;
            public float LandingSafetySeconds;
            public float BackflipDirectionMultiplier;
            public float SpinDirectionMultiplier;
            public AngularSnapshot Flip;
            public AngularSnapshot Spin;
            public DriveSnapshot Drive;
        }

        private sealed class AngularSnapshot
        {
            public float Inertia;
            public float AngularDrag;
            public float MaximumAngularVelocity;
            public bool UseDrive;
            public float TargetAngle;
            public float Stiffness;
            public float Damping;
            public bool UseLimits;
            public float MinimumAngle;
            public float MaximumAngle;
        }

        private sealed class DriveSnapshot
        {
            public float ProportionalGain;
            public float DerivativeGain;
            public float MaximumAngularAcceleration;
            public float MaximumAngularVelocity;
            public bool InertiaCompensation;
        }

        private sealed class TireRideMagnetSnapshot
        {
            public bool UserEnabled;
            public bool UserAlign;
            public bool AlignAssist;
            public float ForceMultiplier;
            public float LengthMultiplier;
            public float MinimumDistance;
            public float NormalPushOff;
            public float MaximumMagnetVelocity;
            public float MaximumLateralAcceleration;
            public float Rotation;
        }

        public static void ResetCapturedDefaults()
        {
            SetCustomOpen(false, "scene change", false);
            ReleaseDefaultSnapshot();
            _menu = null;
            _flipSystem = null;
            _flipSystemDefaults = null;
            _tireRideMagnet = null;
            _defaultTireRideMagnet = null;
            _flipSystemInstanceId = int.MinValue;
            _customOpen = false;
            _loggedInspector = false;
            _windowRect = default;
            _scroll = Vector2.zero;
            _search = string.Empty;
            _presetScroll = Vector2.zero;
            _newPresetName = string.Empty;
            _selectedPresetIndex = 0;
            _loadedPresetName = null;
            _presetsExpanded = true;
            _flipSystemExpanded = true;
            _flipDynamicsExpanded = false;
            _spinDynamicsExpanded = false;
            _rotationDriveExpanded = false;
            _transitionSettingsExpanded = false;
            _flightAugmentSettingsExpanded = false;
            _tireRideSettingsExpanded = false;
            _controllerScrollViewHeight = 320f;
            _controllerScrollContentHeight = 320f;
            _openEnumPath = null;
            CloseNumericEditor();
            PendingNumericValues.Clear();
            TextEdits.Clear();
            ExpandedPaths.Clear();
            SliderRanges.Clear();
            DestroyStyles();
        }

        public static bool OpenInspector(string source = "RoweMod menu")
        {
            RuntimeVehicleTuneMenu menu = ResolveMenu();
            if (menu == null)
            {
                Log.Warning("[RuntimeVehicleReset] Vehicle inspector is unavailable until a supported vehicle is loaded.");
                return false;
            }

            _menu = menu;
            RefreshVehicle(menu, false);
            EnsureWindowRect();
            SetCustomOpen(true, source);
            return true;
        }

        public static void CloseInspector()
        {
            SetCustomOpen(false, "controller");
        }

        public static bool TryCancelNumericEditor()
        {
            if (!_numericEditorOpen)
                return false;

            CloseNumericEditor();
            return true;
        }

        private static void SetCustomOpen(bool open, string source, bool manageInput = true)
        {
            if (_customOpen == open)
            {
                if (open && manageInput)
                    ControllerMenuInput.SetGameplayInputBlocked(true);
                return;
            }

            _customOpen = open;
            if (open)
            {
                _loggedInspector = false;
                if (manageInput)
                    ControllerMenuInput.SetGameplayInputBlocked(true);
                ControllerMenuInput.ResumeNavigationForOverlay();
                Menu.ResetControllerNavigation(true);
                Log.Msg($"[RuntimeVehicleReset] RoweMod vehicle inspector opened from {source}.");
                return;
            }

            _openEnumPath = null;
            CloseNumericEditor();
            PendingNumericValues.Clear();
            Menu.ResetControllerNavigation(true);
            if (manageInput && !Menu.isOpen && !PieMenu.IsOpen)
                ControllerMenuInput.SetGameplayInputBlocked(false);
            Log.Msg($"[RuntimeVehicleReset] RoweMod vehicle inspector closed ({source}).");
        }

        public static void DrawOverlay()
        {
            RuntimeVehicleTuneMenu menu = ResolveMenu();
            if (menu == null)
                return;

            // The native component still owns the Ctrl+Shift+U hotkey and vehicle
            // discovery. Consume its open transition, hide its window, and toggle
            // our replacement inspector instead.
            if (menu._isOpen)
            {
                menu._isOpen = false;
                bool open = !_customOpen;
                if (open)
                {
                    RefreshVehicle(menu, false);
                    EnsureWindowRect();
                }
                SetCustomOpen(open, "Ctrl+Shift+U");
            }

            if (!_customOpen)
                return;

            EnsureWindowRect();
            EnsureStyles();

            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            Color previousBackgroundColor = GUI.backgroundColor;
            int previousDepth = GUI.depth;
            try
            {
                GUI.enabled = true;
                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                GUI.depth = -900;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                _windowRect = GUI.Window(
                    WindowId,
                    _windowRect,
                    (GUI.WindowFunction)DrawWindow,
                    "Vehicle Tuning",
                    _windowStyle);
                ClampWindowToScreen();

                if (!_loggedInspector && Event.current?.type == EventType.Repaint)
                {
                    _loggedInspector = true;
                    Log.Msg($"[RuntimeVehicleReset] Replacement inspector active; window={_windowRect}.");
                }
            }
            catch (Exception ex)
            {
                if (!_loggedInspector)
                {
                    _loggedInspector = true;
                    Log.Error($"[RuntimeVehicleReset] Replacement inspector failed: {ex}");
                }
            }
            finally
            {
                GUI.enabled = previousEnabled;
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackgroundColor;
                GUI.depth = previousDepth;
            }
        }

        private static void DrawWindow(int windowId)
        {
            // GUI.Window invokes its callback after the caller has returned on some IMGUI
            // event passes. Begin the controller frame here—not around GUI.Window—so every
            // registered Vehicle Tuning control keeps the overlay scope between repaint and
            // the next controller Update.
            Menu.BeginControllerOverlayFrame(
                "vehicle-tuning",
                _scroll.y,
                _controllerScrollViewHeight,
                value => _scroll = new Vector2(
                    0f,
                    Mathf.Clamp(
                        value,
                        0f,
                        Mathf.Max(0f, _controllerScrollContentHeight - _controllerScrollViewHeight))));

            RuntimeVehicleTuneMenu menu = _menu;
            if (menu == null)
            {
                _customOpen = false;
                Menu.EndControllerOverlayFrame();
                return;
            }

            try
            {
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal(GUILayout.Height(38f));
            GUILayout.Label(
                menu._currentSettings != null
                    ? $"Live settings: {menu._currentSettings.name}"
                    : "No live vehicle settings found",
                _labelStyle,
                GUILayout.ExpandWidth(true));

            if (Menu.ControllerButton("vehicle_refresh", "Refresh", Menu.UiButtonStyle,
                    GUILayout.Width(105f), GUILayout.Height(30f)))
                RefreshVehicle(menu, true);

            if (Menu.ControllerButton("vehicle_close", "Close", Menu.UiButtonStyle,
                    GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                SetCustomOpen(false, "Close button");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Height(31f));
            GUILayout.Label("Search", _mutedStyle, GUILayout.Width(58f));
            string nextSearch = GUILayout.TextField(
                _search ?? string.Empty,
                Menu.UiSearchFieldStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(26f));
            if (!string.Equals(nextSearch, _search, StringComparison.Ordinal))
                _search = nextSearch;
            GUILayout.EndHorizontal();

            DrawPresetControls(menu);

            if (menu._currentSettings == null)
            {
                GUILayout.Space(12f);
                GUILayout.Label(
                    "Drive a supported vehicle, then press Refresh.",
                    _mutedStyle,
                    GUILayout.ExpandWidth(true));
            }
            else if (EnsureDefaultSnapshot(menu))
            {
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                // GUILayoutUtility.GetLastRect is stripped from this IL2CPP player. Estimate the
                // visible body from the known window size; controller auto-scroll only needs a
                // stable viewport height, not the exact native clip rectangle.
                _controllerScrollViewHeight = Mathf.Max(200f, _windowRect.height - 245f);
                Menu.BeginControllerScrollRegion(
                    _scroll.y,
                    _controllerScrollViewHeight,
                    _controllerScrollContentHeight,
                    value => _scroll = new Vector2(0f, value),
                    useLogicalRows: true);
                Il2CppType rootType = Il2CppInterop.Runtime.Il2CppType.From(
                    typeof(MotorVehicleSettings));
                bool changed = false;
                try
                {
                    DrawFlipSystemEditor();
                    GUILayout.Space(8f);
                    changed |= DrawLiveVehicleSystems(menu);
                    GUILayout.Space(8f);
                    changed |= DrawObjectEditor(
                        menu,
                        string.Empty,
                        menu._currentSettings,
                        _defaultSettings,
                        rootType,
                        0);

                    // GetRect is supported by the IL2CPP player and gives us a stable content
                    // endpoint without the stripped GUILayoutUtility.GetLastRect API.
                    Rect contentEnd = GUILayoutUtility.GetRect(
                        0f,
                        0f,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(0f));
                    if (Event.current?.type == EventType.Repaint)
                    {
                        _controllerScrollContentHeight = Mathf.Max(
                            _controllerScrollViewHeight,
                            contentEnd.yMax + 4f);
                        _scroll.y = Mathf.Clamp(
                            _scroll.y,
                            0f,
                            Mathf.Max(0f, _controllerScrollContentHeight - _controllerScrollViewHeight));
                    }
                }
                finally
                {
                    Menu.EndControllerScrollRegion();
                    GUILayout.EndScrollView();
                }

                if (changed)
                    ApplyChanges(menu);
            }

                GUILayout.EndVertical();
                DrawNumericEditor();
                GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, _windowRect.width - 220f), 34f));
            }
            finally
            {
                Menu.EndControllerOverlayFrame();
            }
        }

        private static void DrawPresetControls(RuntimeVehicleTuneMenu menu)
        {
            GUILayout.BeginVertical(_rowStyle);
            GUILayout.BeginHorizontal(GUILayout.Height(30f));
            if (Menu.ControllerButton(
                "vehicle_presets_toggle",
                _presetsExpanded ? "▼" : "▶",
                Menu.UiMiniButtonStyle,
                GUILayout.Width(30f),
                GUILayout.Height(26f)))
            {
                _presetsExpanded = !_presetsExpanded;
            }
            GUILayout.Label("Vehicle Presets", _labelStyle, GUILayout.Width(130f));

            if (_presetsExpanded)
            {
                _newPresetName = GUILayout.TextField(
                    _newPresetName ?? string.Empty,
                    40,
                    Menu.UiSearchFieldStyle,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(26f));
                bool canSaveNew = menu._currentSettings != null &&
                    !string.IsNullOrWhiteSpace(_newPresetName);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = canSaveNew;
                if (Menu.ControllerButton("vehicle_preset_save_new", "Save New", Menu.UiButtonStyle,
                        GUILayout.Width(92f), GUILayout.Height(27f)))
                {
                    string presetName = _newPresetName.Trim();
                    RuntimeVehicleTunePreset preset = CapturePreset(menu, presetName);
                    if (preset != null && RuntimeVehicleTunePreset.Save(preset))
                    {
                        _loadedPresetName = preset.name;
                        _newPresetName = string.Empty;
                        GUI.FocusControl(null);
                    }
                }
                GUI.enabled = previousEnabled;

                bool canUpdateLoaded = menu._currentSettings != null &&
                    !string.IsNullOrWhiteSpace(_loadedPresetName);
                GUI.enabled = canUpdateLoaded;
                string updateLabel = canUpdateLoaded
                    ? $"Update {_loadedPresetName}"
                    : "Update Loaded";
                if (Menu.ControllerButton("vehicle_preset_update_loaded", updateLabel, Menu.UiButtonStyle,
                        GUILayout.Width(142f), GUILayout.Height(27f)))
                {
                    RuntimeVehicleTunePreset preset = CapturePreset(menu, _loadedPresetName);
                    if (preset != null && RuntimeVehicleTunePreset.Save(preset))
                        Log.Msg($"[RuntimeVehiclePreset] Updated loaded preset '{preset.name}'.");
                }
                GUI.enabled = previousEnabled;
            }
            GUILayout.EndHorizontal();

            if (_presetsExpanded)
            {
                List<string> presets = RuntimeVehicleTunePreset.GetAvailablePresets();
                if (presets.Count == 0)
                {
                    GUILayout.Label("No vehicle presets saved yet.", _mutedStyle);
                }
                else
                {
                    _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, 0, presets.Count - 1);
                    _presetScroll = GUILayout.BeginScrollView(
                        _presetScroll,
                        false,
                        true,
                        GUILayout.Height(Mathf.Min(104f, (presets.Count * 32f) + 4f)));
                    bool deleted = false;
                    for (int i = 0; i < presets.Count; i++)
                    {
                        string presetName = presets[i];
                        GUILayout.BeginHorizontal(GUILayout.Height(29f));
                        GUIStyle buttonStyle = i == _selectedPresetIndex
                            ? Menu.UiRowButtonSelectedStyle
                            : Menu.UiRowButtonStyle;
                        if (Menu.ControllerButton($"vehicle_preset_{presetName}", presetName, buttonStyle,
                                GUILayout.Height(27f), GUILayout.ExpandWidth(true)))
                        {
                            _selectedPresetIndex = i;
                            LoadPresetAndApply(menu, presetName);
                        }
                        if (Menu.ControllerButton($"vehicle_preset_delete_{presetName}", "Delete", _resetStyle,
                                GUILayout.Width(72f), GUILayout.Height(27f)))
                        {
                            if (RuntimeVehicleTunePreset.Delete(presetName))
                            {
                                deleted = true;
                                if (string.Equals(_loadedPresetName, presetName, StringComparison.OrdinalIgnoreCase))
                                    _loadedPresetName = null;
                                _selectedPresetIndex = Mathf.Max(0, _selectedPresetIndex - 1);
                            }
                        }
                        GUILayout.EndHorizontal();
                        if (deleted)
                            break;
                    }
                    GUILayout.EndScrollView();
                }
            }

            GUILayout.EndVertical();
        }

        // These are ScriptableObject-backed live systems rather than fields on
        // MotorVehicleSettings. Keep them here, alongside the vehicle settings,
        // and store them in the same vehicle preset file.
        private static bool DrawLiveVehicleSystems(RuntimeVehicleTuneMenu menu)
        {
            VehicleController vehicle = menu?._currentVehicle;
            TransitionSettings transition = vehicle?.TransitionSettings;
            FlightAugmentSettings flight = vehicle?.FlightAugmentSettings;
            if (transition == null && flight == null)
                return false;

            bool changed = false;
            if (transition != null && _defaultTransitionSettings != null)
            {
                DrawLiveSystemFoldout("vehicle_transition_settings", "Transition Settings", ref _transitionSettingsExpanded);
                if (_transitionSettingsExpanded)
                {
                    changed |= DrawLiveFloat("Transition Settings/Acceptable Slope", "Acceptable Slope", transition.acceptableSlope, _defaultTransitionSettings.acceptableSlope, value => transition.acceptableSlope = value, 0f, 90f);
                    changed |= DrawLiveFloat("Transition Settings/Scan Radius", "Scan Radius", transition.scanRadius, _defaultTransitionSettings.scanRadius, value => transition.scanRadius = value, 0.01f, 10f);
                    changed |= DrawLiveFloat("Transition Settings/Scan Radius Per Velocity Min", "Scan Radius / Velocity Min", transition.scanRadiusPerVelMin, _defaultTransitionSettings.scanRadiusPerVelMin, value => transition.scanRadiusPerVelMin = value, 0f, 10f);
                    changed |= DrawLiveFloat("Transition Settings/Scan Radius Per Velocity Max", "Scan Radius / Velocity Max", transition.scanRadiusPerVelMax, _defaultTransitionSettings.scanRadiusPerVelMax, value => transition.scanRadiusPerVelMax = value, 0f, 10f);
                    changed |= DrawLiveInt("Transition Settings/Horizontal Rays", "Horizontal Rays", transition.numRaysHorizontal, _defaultTransitionSettings.numRaysHorizontal, value => transition.numRaysHorizontal = value, 1, 32);
                    changed |= DrawLiveInt("Transition Settings/Vertical Rays", "Vertical Rays", transition.numRaysVertical, _defaultTransitionSettings.numRaysVertical, value => transition.numRaysVertical = value, 1, 32);
                    changed |= DrawLiveFloat("Transition Settings/Minimum Y Dot", "Minimum Y Dot", transition.MinYDotAngleTo, _defaultTransitionSettings.MinYDotAngleTo, value => transition.MinYDotAngleTo = value, -1f, 1f);
                    changed |= DrawLiveToggle("Transition Settings/Run Under Scanner", "Run Under Scanner", transition.runUnderScanner, _defaultTransitionSettings.runUnderScanner, value => transition.runUnderScanner = value);
                    changed |= DrawLiveFloat("Transition Settings/Secondary Side Scanner Length", "Secondary Side Scanner Length", transition.secondarySideScannerMaxLength, _defaultTransitionSettings.secondarySideScannerMaxLength, value => transition.secondarySideScannerMaxLength = value, 0f, 20f);
                    changed |= DrawLiveFloat("Transition Settings/Secondary Under Scanner Length", "Secondary Under Scanner Length", transition.secondaryUnderScannerMaxLength, _defaultTransitionSettings.secondaryUnderScannerMaxLength, value => transition.secondaryUnderScannerMaxLength = value, 0f, 20f);
                }
            }

            if (flight != null && _defaultFlightAugmentSettings != null)
            {
                GUILayout.Space(5f);
                DrawLiveSystemFoldout("vehicle_flight_augment_settings", "Flight Augment Settings", ref _flightAugmentSettingsExpanded);
                if (_flightAugmentSettingsExpanded)
                {
                    changed |= DrawLiveFloat("Flight Augment Settings/Correction Strength", "Correction Strength", flight._correctionStrength, _defaultFlightAugmentSettings._correctionStrength, value => flight._correctionStrength = value, 0f, 100f);
                    changed |= DrawLiveFloat("Flight Augment Settings/Force Cap", "Force Cap", flight.forceCap, _defaultFlightAugmentSettings.forceCap, value => flight.forceCap = value, 0f, 1000f);
                    changed |= DrawLiveFloat("Flight Augment Settings/Normal Push Off", "Normal Push Off", flight._normalPushOff, _defaultFlightAugmentSettings._normalPushOff, value => flight._normalPushOff = value, -100f, 100f);
                    changed |= DrawLiveFloat("Flight Augment Settings/Maximum Velocity Adjust", "Maximum Velocity Adjust", flight.maxVelAdjust, _defaultFlightAugmentSettings.maxVelAdjust, value => flight.maxVelAdjust = value, 0f, 100f);
                    changed |= DrawLiveFloat("Flight Augment Settings/Minimum Air Time", "Minimum Air Time", flight.minTimeInAir, _defaultFlightAugmentSettings.minTimeInAir, value => flight.minTimeInAir = value, 0f, 5f);
                    changed |= DrawLiveFloat("Flight Augment Settings/Maximum Tick Time", "Maximum Tick Time", flight.maxTickTime, _defaultFlightAugmentSettings.maxTickTime, value => flight.maxTickTime = value, 0f, 5f);
                    changed |= DrawLiveFloat("Flight Augment Settings/Landing Pitch", "Landing Pitch", flight._landingPitch, _defaultFlightAugmentSettings._landingPitch, value => flight._landingPitch = value, -90f, 90f);
                    changed |= DrawLiveToggle("Flight Augment Settings/Closest To Body", "Closest To Body", flight.closestToBody, _defaultFlightAugmentSettings.closestToBody, value => flight.closestToBody = value);
                    changed |= DrawLiveToggle("Flight Augment Settings/Furthest Point", "Furthest Point", flight.furthestPoint, _defaultFlightAugmentSettings.furthestPoint, value => flight.furthestPoint = value);
                    changed |= DrawLiveToggle("Flight Augment Settings/Steepest Angle", "Steepest Angle", flight.steepestAngle, _defaultFlightAugmentSettings.steepestAngle, value => flight.steepestAngle = value);
                    changed |= DrawLiveToggle("Flight Augment Settings/Do Not Tick If Falling", "Do Not Tick If Falling", flight.DontTickIfFalling, _defaultFlightAugmentSettings.DontTickIfFalling, value => flight.DontTickIfFalling = value);
                }
            }

            GrindMagnetZEM tireRideMagnet = ResolveTireRideMagnet(vehicle);
            if (tireRideMagnet != null)
            {
                EnsureTireRideDefaults(tireRideMagnet);
                if (_defaultTireRideMagnet != null)
                {
                    GUILayout.Space(5f);
                    DrawLiveSystemFoldout(
                        "vehicle_tire_ride_magnet",
                        "Tire Ride Alignment",
                        ref _tireRideSettingsExpanded);
                    if (_tireRideSettingsExpanded)
                    {
                        GUILayout.Label(
                            tireRideMagnet._alignTireRide
                                ? "Native tire-ride alignment is active."
                                : "Uses the native grind magnet when a tire ride is detected.",
                            _mutedStyle);
                        const string root = "Tire Ride Alignment";
                        changed |= DrawLiveToggle(
                            $"{root}/Magnet Enabled",
                            "Magnet Enabled",
                            tireRideMagnet._userEnabled,
                            _defaultTireRideMagnet.UserEnabled,
                            value => tireRideMagnet.SetUserEnabled(value));
                        changed |= DrawLiveToggle(
                            $"{root}/User Alignment",
                            "User Alignment",
                            tireRideMagnet._userAlign,
                            _defaultTireRideMagnet.UserAlign,
                            value => tireRideMagnet.SetUserAlign(value));
                        changed |= DrawLiveToggle(
                            $"{root}/Alignment Assist",
                            "Alignment Assist",
                            tireRideMagnet._alignAssist,
                            _defaultTireRideMagnet.AlignAssist,
                            value => tireRideMagnet.SetUserAlignAssist(value));
                        changed |= DrawLiveFloat(
                            $"{root}/Force Multiplier",
                            "Force Multiplier",
                            tireRideMagnet._forceMult,
                            _defaultTireRideMagnet.ForceMultiplier,
                            value => tireRideMagnet._forceMult = value,
                            0f,
                            5f);
                        changed |= DrawLiveFloat(
                            $"{root}/Length Multiplier",
                            "Length Multiplier",
                            tireRideMagnet._userLengthMult,
                            _defaultTireRideMagnet.LengthMultiplier,
                            value => tireRideMagnet.SetUserLength(value),
                            0f,
                            5f);
                        changed |= DrawLiveFloat(
                            $"{root}/Minimum Distance",
                            "Minimum Distance",
                            tireRideMagnet._minDist,
                            _defaultTireRideMagnet.MinimumDistance,
                            value => tireRideMagnet._minDist = value,
                            0f,
                            5f);
                        changed |= DrawLiveFloat(
                            $"{root}/Normal Push Off",
                            "Normal Push Off",
                            tireRideMagnet._normalPushOff,
                            _defaultTireRideMagnet.NormalPushOff,
                            value => tireRideMagnet._normalPushOff = value,
                            -100f,
                            100f);
                        changed |= DrawLiveFloat(
                            $"{root}/Maximum Magnet Velocity",
                            "Maximum Magnet Velocity",
                            tireRideMagnet._maxMagnetVelThres,
                            _defaultTireRideMagnet.MaximumMagnetVelocity,
                            value => tireRideMagnet._maxMagnetVelThres = value,
                            0f,
                            100f);
                        changed |= DrawLiveFloat(
                            $"{root}/Maximum Lateral Acceleration",
                            "Maximum Lateral Acceleration",
                            tireRideMagnet._maxLatAccel,
                            _defaultTireRideMagnet.MaximumLateralAcceleration,
                            value => tireRideMagnet._maxLatAccel = value,
                            0f,
                            100f);
                        changed |= DrawLiveFloat(
                            $"{root}/Rotation",
                            "Rotation",
                            tireRideMagnet._rotate,
                            _defaultTireRideMagnet.Rotation,
                            value => tireRideMagnet._rotate = value,
                            -100f,
                            100f);
                    }
                }
            }
            return changed;
        }

        private static void DrawLiveSystemFoldout(string id, string title, ref bool expanded)
        {
            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            if (Menu.ControllerButton(id, expanded ? "▼" : "▶", Menu.UiMiniButtonStyle, GUILayout.Width(30f), GUILayout.Height(27f)))
                expanded = !expanded;
            GUILayout.Label(title, _labelStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("Saved with Vehicle Presets", _mutedStyle, GUILayout.Width(180f));
            GUILayout.EndHorizontal();
        }

        private static GrindMagnetZEM ResolveTireRideMagnet(VehicleController vehicle)
        {
            if (vehicle == null)
                return null;

            return vehicle.GetComponentInChildren<GrindMagnetZEM>(true);
        }

        private static void EnsureTireRideDefaults(GrindMagnetZEM magnet)
        {
            if (magnet == null || ReferenceEquals(_tireRideMagnet, magnet) && _defaultTireRideMagnet != null)
                return;

            _tireRideMagnet = magnet;
            _defaultTireRideMagnet = new TireRideMagnetSnapshot
            {
                UserEnabled = magnet._userEnabled,
                UserAlign = magnet._userAlign,
                AlignAssist = magnet._alignAssist,
                ForceMultiplier = magnet._forceMult,
                LengthMultiplier = magnet._userLengthMult,
                MinimumDistance = magnet._minDist,
                NormalPushOff = magnet._normalPushOff,
                MaximumMagnetVelocity = magnet._maxMagnetVelThres,
                MaximumLateralAcceleration = magnet._maxLatAccel,
                Rotation = magnet._rotate
            };
            Log.Msg($"[RuntimeVehicleReset] Captured native tire-ride alignment defaults from {magnet.name}.");
        }

        private static bool DrawLiveFloat(string path, string label, float current, float baseline, Action<float> apply, float minimum, float maximum)
        {
            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            GUILayout.Space(18f);
            GUILayout.Label(label, _labelStyle, GUILayout.Width(Mathf.Clamp(_windowRect.width * 0.34f - 18f, 210f, 390f)));
            bool changed = DrawRangedFloatField(path, current, minimum, maximum, out float next);
            bool atDefault = Mathf.Approximately(current, baseline);
            if (Menu.ControllerButton($"vehicle_reset_{path}", "Reset", atDefault ? _resetDefaultStyle : _resetStyle, GUILayout.Width(ResetWidth), GUILayout.Height(30f)) && !atDefault)
            {
                next = baseline;
                changed = true;
                TextEdits.Remove(path);
            }
            GUILayout.EndHorizontal();
            if (!changed)
                return false;
            apply(next);
            return true;
        }

        private static bool DrawLiveInt(string path, string label, int current, int baseline, Action<int> apply, int minimum, int maximum)
        {
            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            GUILayout.Space(18f);
            GUILayout.Label(label, _labelStyle, GUILayout.Width(Mathf.Clamp(_windowRect.width * 0.34f - 18f, 210f, 390f)));
            bool changed = DrawRangedIntField(path, current, minimum, maximum, out int next);
            bool atDefault = current == baseline;
            if (Menu.ControllerButton($"vehicle_reset_{path}", "Reset", atDefault ? _resetDefaultStyle : _resetStyle, GUILayout.Width(ResetWidth), GUILayout.Height(30f)) && !atDefault)
            {
                next = baseline;
                changed = true;
                TextEdits.Remove(path);
            }
            GUILayout.EndHorizontal();
            if (!changed)
                return false;
            apply(next);
            return true;
        }

        private static bool DrawLiveToggle(string path, string label, bool current, bool baseline, Action<bool> apply)
        {
            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            GUILayout.Space(18f);
            bool next = current;
            Menu.ModernToggle(label, ref next, $"vehicle_bool_{path}");
            bool atDefault = current == baseline;
            if (Menu.ControllerButton($"vehicle_reset_{path}", "Reset", atDefault ? _resetDefaultStyle : _resetStyle, GUILayout.Width(ResetWidth), GUILayout.Height(30f)) && !atDefault)
                next = baseline;
            GUILayout.EndHorizontal();
            if (next == current)
                return false;
            apply(next);
            return true;
        }

        private static RuntimeVehicleTunePreset CapturePreset(RuntimeVehicleTuneMenu menu, string presetName)
        {
            if (menu?._currentSettings == null || string.IsNullOrWhiteSpace(presetName))
                return null;

            var preset = new RuntimeVehicleTunePreset
            {
                name = presetName.Trim(),
                sourceSettings = menu._currentSettings.name
            };
            Il2CppType rootType = Il2CppInterop.Runtime.Il2CppType.From(typeof(MotorVehicleSettings));
            CollectPresetValues(menu, string.Empty, menu._currentSettings, rootType, 0, preset.values);
            CollectFlipSystemPresetValues(preset.values);
            CollectLiveVehicleSystemPresetValues(menu._currentVehicle, preset.values);
            return preset;
        }

        private static void CollectPresetValues(
            RuntimeVehicleTuneMenu menu,
            string parentPath,
            Il2CppObject target,
            Il2CppType type,
            int depth,
            Dictionary<string, RuntimeVehicleTunePresetValue> values)
        {
            if (target == null || type == null || depth > MaxObjectDepth)
                return;

            var fields = menu.GetEditableFields(type);
            if (fields == null)
                return;

            for (int i = 0; i < fields.Length; i++)
            {
                Il2CppFieldInfo field = fields[i];
                if (field == null)
                    continue;

                string label = RuntimeVehicleTuneMenu.Nicify(field.Name);
                string path = string.IsNullOrEmpty(parentPath) ? label : $"{parentPath}/{label}";
                Il2CppObject value = field.GetValue(target);
                Il2CppType fieldType = field.FieldType;
                if (value == null)
                    continue;

                if (IsEditableLeaf(fieldType))
                {
                    if (TrySerializePresetValue(fieldType, value, out RuntimeVehicleTunePresetValue serialized))
                        values[path] = serialized;
                    continue;
                }

                if (!IsLabelOnlyType(fieldType))
                    CollectPresetValues(menu, path, value, fieldType, depth + 1, values);
            }
        }

        private static void LoadPresetAndApply(RuntimeVehicleTuneMenu menu, string presetName)
        {
            RuntimeVehicleTunePreset preset = RuntimeVehicleTunePreset.Load(presetName);
            if (preset == null || menu?._currentSettings == null)
                return;

            Il2CppType rootType = Il2CppInterop.Runtime.Il2CppType.From(typeof(MotorVehicleSettings));
            int applied = 0;
            ApplyPresetValues(
                menu,
                string.Empty,
                menu._currentSettings,
                rootType,
                0,
                preset.values,
                ref applied);
            ApplyFlipSystemPresetValues(preset.values, ref applied);
            ApplyLiveVehicleSystemPresetValues(menu._currentVehicle, preset.values, ref applied);
            TextEdits.Clear();
            ApplyChanges(menu);
            _loadedPresetName = preset.name;
            Log.Msg(
                $"[RuntimeVehiclePreset] Loaded '{presetName}' from " +
                $"'{preset.sourceSettings ?? "unknown"}'; applied {applied} values.");
        }

        private static void CollectFlipSystemPresetValues(
            Dictionary<string, RuntimeVehicleTunePresetValue> values)
        {
            if (values == null)
                return;
            if (_flipSystem == null)
                RefreshFlipSystem();
            if (_flipSystem == null)
                return;

            const string root = "One Point Oh";
            StorePresetFloat(values, $"{root}/Flip Axis/X", _flipSystem._flipAxisLocal.x);
            StorePresetFloat(values, $"{root}/Flip Axis/Y", _flipSystem._flipAxisLocal.y);
            StorePresetFloat(values, $"{root}/Flip Axis/Z", _flipSystem._flipAxisLocal.z);
            StorePresetFloat(values, $"{root}/Spin Axis/X", _flipSystem._spinAxisLocal.x);
            StorePresetFloat(values, $"{root}/Spin Axis/Y", _flipSystem._spinAxisLocal.y);
            StorePresetFloat(values, $"{root}/Spin Axis/Z", _flipSystem._spinAxisLocal.z);
            StorePresetBool(values, $"{root}/Spin Before Flip", _flipSystem._spinFirst);
            StorePresetFloat(values, $"{root}/Spin Multiplier", _flipSystem._spinMult);
            StorePresetFloat(values, $"{root}/Flip Multiplier", _flipSystem._flipMult);
            StorePresetFloat(values, $"{root}/Default Trick Duration", _flipSystem._defaultTrickDuration);
            StorePresetFloat(values, $"{root}/Minimum Trick Duration", _flipSystem._minTrickDuration);
            StorePresetFloat(values, $"{root}/Landing Safety Time", _flipSystem._landingSafetySeconds);
            StorePresetFloat(values, $"{root}/Backflip Direction Multiplier", _flipSystem._backflipDirMult);
            StorePresetFloat(values, $"{root}/Spin Direction Multiplier", _flipSystem._spinDirMult);
            StorePresetBool(values, $"{root}/Forward-Only Landing Finish", Config.physics.spinCompletionAssist);
            CollectAngularPresetValues(values, $"{root}/Flip Dynamics", _flipSystem._flip);
            CollectAngularPresetValues(values, $"{root}/Spin Dynamics", _flipSystem._spin);

            QuaternionPDDrive drive = _flipSystem._drive;
            if (drive != null)
            {
                string driveRoot = $"{root}/Rotation Drive";
                StorePresetFloat(values, $"{driveRoot}/Proportional Gain", drive.kp);
                StorePresetFloat(values, $"{driveRoot}/Derivative Gain", drive.kd);
                StorePresetFloat(values, $"{driveRoot}/Maximum Angular Acceleration", drive.maxAngularAccel);
                StorePresetFloat(values, $"{driveRoot}/Maximum Angular Velocity", drive.maxAngularVelocity);
                StorePresetBool(values, $"{driveRoot}/Inertia Compensation", drive.inertiaCompensation);
            }
        }

        private static void CollectAngularPresetValues(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string root,
            Angular1D angular)
        {
            if (angular == null)
                return;

            StorePresetFloat(values, $"{root}/Inertia", angular.inertia);
            StorePresetFloat(values, $"{root}/Angular Drag", angular.angularDrag);
            StorePresetFloat(values, $"{root}/Maximum Angular Velocity", angular.maxAbsAngularVelocityDeg);
            StorePresetBool(values, $"{root}/Use Angle Drive", angular.useDrive);
            StorePresetFloat(values, $"{root}/Target Angle", angular.targetAngleDeg);
            StorePresetFloat(values, $"{root}/Stiffness", angular.stiffness);
            StorePresetFloat(values, $"{root}/Damping", angular.damping);
            StorePresetBool(values, $"{root}/Use Angle Limits", angular.useLimits);
            StorePresetFloat(values, $"{root}/Minimum Angle", angular.minAngleDeg);
            StorePresetFloat(values, $"{root}/Maximum Angle", angular.maxAngleDeg);
        }

        private static void CollectLiveVehicleSystemPresetValues(
            VehicleController vehicle,
            Dictionary<string, RuntimeVehicleTunePresetValue> values)
        {
            if (vehicle == null || values == null)
                return;

            TransitionSettings transition = vehicle.TransitionSettings;
            if (transition != null)
            {
                const string root = "Transition Settings";
                StorePresetFloat(values, $"{root}/Acceptable Slope", transition.acceptableSlope);
                StorePresetFloat(values, $"{root}/Scan Radius", transition.scanRadius);
                StorePresetFloat(values, $"{root}/Scan Radius Per Velocity Min", transition.scanRadiusPerVelMin);
                StorePresetFloat(values, $"{root}/Scan Radius Per Velocity Max", transition.scanRadiusPerVelMax);
                StorePresetInt(values, $"{root}/Horizontal Rays", transition.numRaysHorizontal);
                StorePresetInt(values, $"{root}/Vertical Rays", transition.numRaysVertical);
                StorePresetFloat(values, $"{root}/Minimum Y Dot", transition.MinYDotAngleTo);
                StorePresetBool(values, $"{root}/Run Under Scanner", transition.runUnderScanner);
                StorePresetFloat(values, $"{root}/Secondary Side Scanner Length", transition.secondarySideScannerMaxLength);
                StorePresetFloat(values, $"{root}/Secondary Under Scanner Length", transition.secondaryUnderScannerMaxLength);
            }

            FlightAugmentSettings flight = vehicle.FlightAugmentSettings;
            if (flight != null)
            {
                const string root = "Flight Augment Settings";
                StorePresetFloat(values, $"{root}/Correction Strength", flight._correctionStrength);
                StorePresetFloat(values, $"{root}/Force Cap", flight.forceCap);
                StorePresetFloat(values, $"{root}/Normal Push Off", flight._normalPushOff);
                StorePresetFloat(values, $"{root}/Maximum Velocity Adjust", flight.maxVelAdjust);
                StorePresetFloat(values, $"{root}/Minimum Air Time", flight.minTimeInAir);
                StorePresetFloat(values, $"{root}/Maximum Tick Time", flight.maxTickTime);
                StorePresetFloat(values, $"{root}/Landing Pitch", flight._landingPitch);
                StorePresetBool(values, $"{root}/Closest To Body", flight.closestToBody);
                StorePresetBool(values, $"{root}/Furthest Point", flight.furthestPoint);
                StorePresetBool(values, $"{root}/Steepest Angle", flight.steepestAngle);
                StorePresetBool(values, $"{root}/Do Not Tick If Falling", flight.DontTickIfFalling);
            }

            GrindMagnetZEM tireRideMagnet = ResolveTireRideMagnet(vehicle);
            if (tireRideMagnet != null)
            {
                const string root = "Tire Ride Alignment";
                StorePresetBool(values, $"{root}/Magnet Enabled", tireRideMagnet._userEnabled);
                StorePresetBool(values, $"{root}/User Alignment", tireRideMagnet._userAlign);
                StorePresetBool(values, $"{root}/Alignment Assist", tireRideMagnet._alignAssist);
                StorePresetFloat(values, $"{root}/Force Multiplier", tireRideMagnet._forceMult);
                StorePresetFloat(values, $"{root}/Length Multiplier", tireRideMagnet._userLengthMult);
                StorePresetFloat(values, $"{root}/Minimum Distance", tireRideMagnet._minDist);
                StorePresetFloat(values, $"{root}/Normal Push Off", tireRideMagnet._normalPushOff);
                StorePresetFloat(values, $"{root}/Maximum Magnet Velocity", tireRideMagnet._maxMagnetVelThres);
                StorePresetFloat(values, $"{root}/Maximum Lateral Acceleration", tireRideMagnet._maxLatAccel);
                StorePresetFloat(values, $"{root}/Rotation", tireRideMagnet._rotate);
            }
        }

        private static void ApplyFlipSystemPresetValues(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            ref int applied)
        {
            if (values == null)
                return;
            if (_flipSystem == null)
                RefreshFlipSystem();
            if (_flipSystem == null)
                return;

            const string root = "One Point Oh";
            Vector3 flipAxis = _flipSystem._flipAxisLocal;
            ApplyPresetFloat(values, $"{root}/Flip Axis/X", value => flipAxis.x = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Flip Axis/Y", value => flipAxis.y = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Flip Axis/Z", value => flipAxis.z = value, ref applied);
            _flipSystem._flipAxisLocal = flipAxis;

            Vector3 spinAxis = _flipSystem._spinAxisLocal;
            ApplyPresetFloat(values, $"{root}/Spin Axis/X", value => spinAxis.x = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Spin Axis/Y", value => spinAxis.y = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Spin Axis/Z", value => spinAxis.z = value, ref applied);
            _flipSystem._spinAxisLocal = spinAxis;

            ApplyPresetBool(values, $"{root}/Spin Before Flip", value => _flipSystem._spinFirst = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Spin Multiplier", value => _flipSystem._spinMult = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Flip Multiplier", value => _flipSystem._flipMult = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Default Trick Duration", value => _flipSystem._defaultTrickDuration = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Minimum Trick Duration", value => _flipSystem._minTrickDuration = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Landing Safety Time", value => _flipSystem._landingSafetySeconds = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Backflip Direction Multiplier", value => _flipSystem._backflipDirMult = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Spin Direction Multiplier", value => _flipSystem._spinDirMult = value, ref applied);
            ApplyPresetBool(values, $"{root}/Forward-Only Landing Finish", value =>
            {
                Config.physics.spinCompletionAssist = value;
                Config.RequestSave();
                if (!value)
                    Physics.ReleaseSpinCompletionAssist();
            }, ref applied);
            ApplyAngularPresetValues(values, $"{root}/Flip Dynamics", _flipSystem._flip, ref applied);
            ApplyAngularPresetValues(values, $"{root}/Spin Dynamics", _flipSystem._spin, ref applied);

            QuaternionPDDrive drive = _flipSystem._drive;
            if (drive != null)
            {
                string driveRoot = $"{root}/Rotation Drive";
                ApplyPresetFloat(values, $"{driveRoot}/Proportional Gain", value => drive.kp = value, ref applied);
                ApplyPresetFloat(values, $"{driveRoot}/Derivative Gain", value => drive.kd = value, ref applied);
                ApplyPresetFloat(values, $"{driveRoot}/Maximum Angular Acceleration", value => drive.maxAngularAccel = value, ref applied);
                ApplyPresetFloat(values, $"{driveRoot}/Maximum Angular Velocity", value => drive.maxAngularVelocity = value, ref applied);
                ApplyPresetBool(values, $"{driveRoot}/Inertia Compensation", value => drive.inertiaCompensation = value, ref applied);
            }
        }

        private static void ApplyLiveVehicleSystemPresetValues(
            VehicleController vehicle,
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            ref int applied)
        {
            if (vehicle == null || values == null)
                return;

            TransitionSettings transition = vehicle.TransitionSettings;
            if (transition != null)
            {
                const string root = "Transition Settings";
                ApplyPresetFloat(values, $"{root}/Acceptable Slope", value => transition.acceptableSlope = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Scan Radius", value => transition.scanRadius = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Scan Radius Per Velocity Min", value => transition.scanRadiusPerVelMin = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Scan Radius Per Velocity Max", value => transition.scanRadiusPerVelMax = value, ref applied);
                ApplyPresetInt(values, $"{root}/Horizontal Rays", value => transition.numRaysHorizontal = value, ref applied);
                ApplyPresetInt(values, $"{root}/Vertical Rays", value => transition.numRaysVertical = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Minimum Y Dot", value => transition.MinYDotAngleTo = value, ref applied);
                ApplyPresetBool(values, $"{root}/Run Under Scanner", value => transition.runUnderScanner = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Secondary Side Scanner Length", value => transition.secondarySideScannerMaxLength = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Secondary Under Scanner Length", value => transition.secondaryUnderScannerMaxLength = value, ref applied);
            }

            FlightAugmentSettings flight = vehicle.FlightAugmentSettings;
            if (flight != null)
            {
                const string root = "Flight Augment Settings";
                ApplyPresetFloat(values, $"{root}/Correction Strength", value => flight._correctionStrength = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Force Cap", value => flight.forceCap = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Normal Push Off", value => flight._normalPushOff = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Maximum Velocity Adjust", value => flight.maxVelAdjust = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Minimum Air Time", value => flight.minTimeInAir = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Maximum Tick Time", value => flight.maxTickTime = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Landing Pitch", value => flight._landingPitch = value, ref applied);
                ApplyPresetBool(values, $"{root}/Closest To Body", value => flight.closestToBody = value, ref applied);
                ApplyPresetBool(values, $"{root}/Furthest Point", value => flight.furthestPoint = value, ref applied);
                ApplyPresetBool(values, $"{root}/Steepest Angle", value => flight.steepestAngle = value, ref applied);
                ApplyPresetBool(values, $"{root}/Do Not Tick If Falling", value => flight.DontTickIfFalling = value, ref applied);
            }

            GrindMagnetZEM tireRideMagnet = ResolveTireRideMagnet(vehicle);
            if (tireRideMagnet != null)
            {
                const string root = "Tire Ride Alignment";
                ApplyPresetBool(values, $"{root}/Magnet Enabled", value => tireRideMagnet.SetUserEnabled(value), ref applied);
                ApplyPresetBool(values, $"{root}/User Alignment", value => tireRideMagnet.SetUserAlign(value), ref applied);
                ApplyPresetBool(values, $"{root}/Alignment Assist", value => tireRideMagnet.SetUserAlignAssist(value), ref applied);
                ApplyPresetFloat(values, $"{root}/Force Multiplier", value => tireRideMagnet._forceMult = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Length Multiplier", value => tireRideMagnet.SetUserLength(value), ref applied);
                ApplyPresetFloat(values, $"{root}/Minimum Distance", value => tireRideMagnet._minDist = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Normal Push Off", value => tireRideMagnet._normalPushOff = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Maximum Magnet Velocity", value => tireRideMagnet._maxMagnetVelThres = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Maximum Lateral Acceleration", value => tireRideMagnet._maxLatAccel = value, ref applied);
                ApplyPresetFloat(values, $"{root}/Rotation", value => tireRideMagnet._rotate = value, ref applied);
            }
        }

        private static void ApplyAngularPresetValues(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string root,
            Angular1D angular,
            ref int applied)
        {
            if (angular == null)
                return;

            ApplyPresetFloat(values, $"{root}/Inertia", value => angular.inertia = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Angular Drag", value => angular.angularDrag = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Maximum Angular Velocity", value => angular.maxAbsAngularVelocityDeg = value, ref applied);
            ApplyPresetBool(values, $"{root}/Use Angle Drive", value => angular.useDrive = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Target Angle", value => angular.targetAngleDeg = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Stiffness", value => angular.stiffness = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Damping", value => angular.damping = value, ref applied);
            ApplyPresetBool(values, $"{root}/Use Angle Limits", value => angular.useLimits = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Minimum Angle", value => angular.minAngleDeg = value, ref applied);
            ApplyPresetFloat(values, $"{root}/Maximum Angle", value => angular.maxAngleDeg = value, ref applied);
        }

        private static void StorePresetFloat(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string path,
            float value)
        {
            values[path] = new RuntimeVehicleTunePresetValue
            {
                type = "System.Single",
                value = value.ToString("R", CultureInfo.InvariantCulture)
            };
        }

        private static void StorePresetInt(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string path,
            int value)
        {
            values[path] = new RuntimeVehicleTunePresetValue
            {
                type = "System.Int32",
                value = value.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static void StorePresetBool(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string path,
            bool value)
        {
            values[path] = new RuntimeVehicleTunePresetValue
            {
                type = "System.Boolean",
                value = value ? "true" : "false"
            };
        }

        private static void ApplyPresetFloat(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string path,
            Action<float> apply,
            ref int applied)
        {
            if (!values.TryGetValue(path, out RuntimeVehicleTunePresetValue stored) ||
                stored == null ||
                !float.TryParse(stored.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return;

            apply(value);
            applied++;
        }

        private static void ApplyPresetBool(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string path,
            Action<bool> apply,
            ref int applied)
        {
            if (!values.TryGetValue(path, out RuntimeVehicleTunePresetValue stored) ||
                stored == null ||
                !bool.TryParse(stored.value, out bool value))
                return;

            apply(value);
            applied++;
        }

        private static void ApplyPresetInt(
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            string path,
            Action<int> apply,
            ref int applied)
        {
            if (!values.TryGetValue(path, out RuntimeVehicleTunePresetValue stored) ||
                stored == null ||
                !int.TryParse(stored.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return;

            apply(value);
            applied++;
        }

        private static bool ApplyPresetValues(
            RuntimeVehicleTuneMenu menu,
            string parentPath,
            Il2CppObject target,
            Il2CppType type,
            int depth,
            Dictionary<string, RuntimeVehicleTunePresetValue> values,
            ref int applied)
        {
            if (target == null || type == null || values == null || depth > MaxObjectDepth)
                return false;

            var fields = menu.GetEditableFields(type);
            if (fields == null)
                return false;

            bool changed = false;
            for (int i = 0; i < fields.Length; i++)
            {
                Il2CppFieldInfo field = fields[i];
                if (field == null)
                    continue;

                string label = RuntimeVehicleTuneMenu.Nicify(field.Name);
                string path = string.IsNullOrEmpty(parentPath) ? label : $"{parentPath}/{label}";
                Il2CppType fieldType = field.FieldType;
                Il2CppObject currentValue = field.GetValue(target);

                if (IsEditableLeaf(fieldType))
                {
                    if (values.TryGetValue(path, out RuntimeVehicleTunePresetValue stored) &&
                        TryDeserializePresetValue(fieldType, stored, out Il2CppObject nextValue))
                    {
                        field.SetValue(target, nextValue);
                        applied++;
                        changed = true;
                    }
                    continue;
                }

                if (currentValue == null || IsLabelOnlyType(fieldType))
                    continue;

                bool childChanged = ApplyPresetValues(
                    menu,
                    path,
                    currentValue,
                    fieldType,
                    depth + 1,
                    values,
                    ref applied);
                if (!childChanged)
                    continue;

                if (fieldType.IsValueType)
                    field.SetValue(target, currentValue);
                changed = true;
            }

            return changed;
        }

        private static bool TrySerializePresetValue(
            Il2CppType type,
            Il2CppObject value,
            out RuntimeVehicleTunePresetValue serialized)
        {
            serialized = null;
            if (type == null || value == null)
                return false;

            string typeName = type.FullName ?? string.Empty;
            string stored;
            if (typeName == "System.Single")
                stored = value.Unbox<float>().ToString("R", CultureInfo.InvariantCulture);
            else if (typeName == "System.Int32")
                stored = value.Unbox<int>().ToString(CultureInfo.InvariantCulture);
            else if (typeName == "System.Boolean")
                stored = value.Unbox<bool>() ? "true" : "false";
            else if (type.IsEnum)
                stored = value.ToString();
            else if (typeName == "UnityEngine.Vector2")
            {
                Vector2 v = value.Unbox<Vector2>();
                stored = JoinFloats(v.x, v.y);
            }
            else if (typeName == "UnityEngine.Vector3")
            {
                Vector3 v = value.Unbox<Vector3>();
                stored = JoinFloats(v.x, v.y, v.z);
            }
            else if (typeName == "UnityEngine.Vector4")
            {
                Vector4 v = value.Unbox<Vector4>();
                stored = JoinFloats(v.x, v.y, v.z, v.w);
            }
            else
                return false;

            serialized = new RuntimeVehicleTunePresetValue { type = typeName, value = stored };
            return true;
        }

        private static bool TryDeserializePresetValue(
            Il2CppType type,
            RuntimeVehicleTunePresetValue stored,
            out Il2CppObject value)
        {
            value = null;
            if (type == null || stored == null || stored.value == null)
                return false;

            try
            {
                string typeName = type.FullName ?? string.Empty;
                if (!string.IsNullOrEmpty(stored.type) &&
                    !string.Equals(typeName, stored.type, StringComparison.Ordinal))
                    return false;

                if (typeName == "System.Single" && float.TryParse(stored.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    value = BoxValue(f);
                else if (typeName == "System.Int32" && int.TryParse(stored.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                    value = BoxValue(i);
                else if (typeName == "System.Boolean" && bool.TryParse(stored.value, out bool b))
                    value = BoxValue(b);
                else if (type.IsEnum)
                    value = Il2CppSystem.Enum.Parse(type, stored.value, true);
                else if (typeName == "UnityEngine.Vector2" && TryParseFloats(stored.value, 2, out float[] v2))
                    value = new Vector2(v2[0], v2[1]).BoxIl2CppObject();
                else if (typeName == "UnityEngine.Vector3" && TryParseFloats(stored.value, 3, out float[] v3))
                    value = new Vector3(v3[0], v3[1], v3[2]).BoxIl2CppObject();
                else if (typeName == "UnityEngine.Vector4" && TryParseFloats(stored.value, 4, out float[] v4))
                    value = new Vector4(v4[0], v4[1], v4[2], v4[3]).BoxIl2CppObject();

                return value != null;
            }
            catch
            {
                return false;
            }
        }

        private static string JoinFloats(params float[] values)
        {
            string[] formatted = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                formatted[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join("|", formatted);
        }

        private static bool TryParseFloats(string stored, int count, out float[] values)
        {
            values = null;
            string[] parts = stored.Split('|');
            if (parts.Length != count)
                return false;

            var parsed = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]))
                    return false;
            }
            values = parsed;
            return true;
        }

        private static bool DrawObjectEditor(
            RuntimeVehicleTuneMenu menu,
            string parentPath,
            Il2CppObject currentTarget,
            Il2CppObject defaultTarget,
            Il2CppType type,
            int depth)
        {
            if (currentTarget == null || defaultTarget == null || type == null || depth > MaxObjectDepth)
                return false;

            var fields = menu.GetEditableFields(type);
            if (fields == null)
                return false;

            bool changed = false;
            bool searching = !string.IsNullOrWhiteSpace(_search);

            for (int i = 0; i < fields.Length; i++)
            {
                Il2CppFieldInfo field = fields[i];
                if (field == null)
                    continue;

                string label = RuntimeVehicleTuneMenu.Nicify(field.Name);
                string path = string.IsNullOrEmpty(parentPath) ? label : $"{parentPath}/{label}";
                Il2CppType fieldType = field.FieldType;
                Il2CppObject currentValue = field.GetValue(currentTarget);
                Il2CppObject defaultValue = field.GetValue(defaultTarget);

                if (IsEditableLeaf(fieldType))
                {
                    if (searching && path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (DrawLeafRow(field, path, label, currentValue, defaultValue, depth, out Il2CppObject nextValue))
                    {
                        field.SetValue(currentTarget, nextValue);
                        changed = true;
                    }
                    continue;
                }

                if (currentValue == null || IsLabelOnlyType(fieldType))
                {
                    if (!searching || path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                        DrawReadOnlyRow(label, currentValue, depth);
                    continue;
                }

                bool expanded = searching || ExpandedPaths.Contains(path);
                if (!searching)
                {
                    GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
                    GUILayout.Space(depth * 18f);
                    if (Menu.ControllerButton(
                        $"vehicle_foldout_{path}",
                        expanded ? "▼" : "▶",
                        Menu.UiMiniButtonStyle,
                        GUILayout.Width(30f),
                        GUILayout.Height(27f)))
                    {
                        if (expanded)
                            ExpandedPaths.Remove(path);
                        else
                            ExpandedPaths.Add(path);
                        expanded = !expanded;
                    }
                    GUILayout.Label(label, _labelStyle, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();
                }

                if (!expanded || depth >= MaxObjectDepth || defaultValue == null)
                    continue;

                bool childChanged = DrawObjectEditor(
                    menu,
                    path,
                    currentValue,
                    defaultValue,
                    fieldType,
                    depth + 1);
                if (!childChanged)
                    continue;

                if (fieldType.IsValueType)
                    field.SetValue(currentTarget, currentValue);
                changed = true;
            }

            return changed;
        }

        private static bool DrawLeafRow(
            Il2CppFieldInfo field,
            string path,
            string label,
            Il2CppObject currentValue,
            Il2CppObject defaultValue,
            int depth,
            out Il2CppObject nextValue)
        {
            nextValue = currentValue;
            bool changed = false;
            bool alreadyDefault = ValuesMatch(field.FieldType, currentValue, defaultValue);

            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            GUILayout.Space(depth * 18f);
            float labelWidth = Mathf.Clamp(_windowRect.width * 0.34f - (depth * 18f), 210f, 390f);
            bool editorOwnsTitle = field.FieldType?.FullName == "System.Boolean" || field.FieldType?.IsEnum == true;
            if (!editorOwnsTitle)
                GUILayout.Label(label, _labelStyle, GUILayout.Width(labelWidth));

            if (currentValue != null)
                changed = DrawLeafEditor(field, path, label, currentValue, out nextValue);
            else
                GUILayout.Label("null", _mutedStyle, GUILayout.ExpandWidth(true));

            if (Menu.ControllerButton(
                $"vehicle_reset_{path}",
                "Reset",
                alreadyDefault ? _resetDefaultStyle : _resetStyle,
                GUILayout.Width(ResetWidth),
                GUILayout.Height(30f)) &&
                !alreadyDefault &&
                defaultValue != null)
            {
                nextValue = defaultValue;
                TextEdits.Remove(path);
                changed = true;
                Log.Msg($"[RuntimeVehicleReset] Restored {path}.");
            }

            GUILayout.EndHorizontal();
            return changed;
        }

        private static bool DrawLeafEditor(
            Il2CppFieldInfo field,
            string path,
            string label,
            Il2CppObject currentValue,
            out Il2CppObject nextValue)
        {
            nextValue = currentValue;
            Il2CppType type = field?.FieldType;
            if (type == null)
                return false;
            string typeName = type.FullName ?? string.Empty;

            if (typeName == "System.Boolean")
            {
                bool value = currentValue.Unbox<bool>();
                bool next = value;
                Menu.ModernToggle(label, ref next, $"vehicle_bool_{path}");
                if (next == value)
                    return false;
                nextValue = BoxValue(next);
                return true;
            }

            if (typeName == "System.Single")
            {
                float value = currentValue.Unbox<float>();
                bool hasRange = TryGetSliderRange(field, out float minimum, out float maximum);
                bool changed = hasRange
                    ? DrawRangedFloatField(path, value, minimum, maximum, out float next)
                    : DrawFloatField(path, value, out next);
                if (!changed)
                    return false;
                nextValue = BoxValue(next);
                return true;
            }

            if (typeName == "System.Int32")
            {
                int value = currentValue.Unbox<int>();
                bool hasRange = TryGetSliderRange(field, out float minimum, out float maximum);
                bool changed = hasRange
                    ? DrawRangedIntField(path, value, Mathf.RoundToInt(minimum), Mathf.RoundToInt(maximum), out int next)
                    : DrawIntField(path, value, out next);
                if (!changed)
                    return false;
                nextValue = BoxValue(next);
                return true;
            }

            if (type.IsEnum)
                return DrawEnumField(path, label, type, currentValue, out nextValue);

            if (typeName == "UnityEngine.Vector2")
            {
                Vector2 value = currentValue.Unbox<Vector2>();
                Vector2 next = value;
                bool changed = DrawFloatField(path + ".x", value.x, out next.x, "X") |
                               DrawFloatField(path + ".y", value.y, out next.y, "Y");
                if (changed)
                    nextValue = next.BoxIl2CppObject();
                return changed;
            }

            if (typeName == "UnityEngine.Vector3")
            {
                Vector3 value = currentValue.Unbox<Vector3>();
                Vector3 next = value;
                bool changed = DrawFloatField(path + ".x", value.x, out next.x, "X") |
                               DrawFloatField(path + ".y", value.y, out next.y, "Y") |
                               DrawFloatField(path + ".z", value.z, out next.z, "Z");
                if (changed)
                    nextValue = next.BoxIl2CppObject();
                return changed;
            }

            if (typeName == "UnityEngine.Vector4")
            {
                Vector4 value = currentValue.Unbox<Vector4>();
                Vector4 next = value;
                bool changed = DrawFloatField(path + ".x", value.x, out next.x, "X") |
                               DrawFloatField(path + ".y", value.y, out next.y, "Y") |
                               DrawFloatField(path + ".z", value.z, out next.z, "Z") |
                               DrawFloatField(path + ".w", value.w, out next.w, "W");
                if (changed)
                    nextValue = next.BoxIl2CppObject();
                return changed;
            }

            GUILayout.Label(currentValue.ToString(), _mutedStyle, GUILayout.ExpandWidth(true));
            return false;
        }

        private static void OpenNumericEditor(
            string path,
            float value,
            float minimum,
            float maximum,
            bool isInteger)
        {
            _numericEditorOpen = true;
            _numericEditorPath = path;
            _numericEditorLabel = path;
            _numericEditorIsInteger = isInteger;
            _numericEditorMinimum = minimum;
            _numericEditorMaximum = maximum;
            _numericEditorText = isInteger
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.###", CultureInfo.InvariantCulture);
            _numericEditorFocusPending = true;
        }

        private static void CloseNumericEditor()
        {
            _numericEditorOpen = false;
            _numericEditorIsInteger = false;
            _numericEditorFocusPending = false;
            _numericEditorPath = null;
            _numericEditorLabel = null;
            _numericEditorText = null;
            _numericEditorMinimum = 0f;
            _numericEditorMaximum = 0f;
        }

        private static bool TryTakeNumericValue(string path, out float value)
        {
            if (PendingNumericValues.TryGetValue(path, out value))
            {
                PendingNumericValues.Remove(path);
                return true;
            }

            value = 0f;
            return false;
        }

        private static void DrawNumericEditor()
        {
            if (!_numericEditorOpen)
                return;

            const float popupWidth = 408f;
            const float popupHeight = 326f;
            Rect panel = new Rect(
                Mathf.Max(18f, (_windowRect.width - popupWidth) * 0.5f),
                78f,
                popupWidth,
                popupHeight);
            GUI.Box(panel, GUIContent.none, _rowStyle);

            GUI.Label(
                new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, 22f),
                "Enter exact value",
                _labelStyle);
            GUI.Label(
                new Rect(panel.x + 16f, panel.y + 35f, panel.width - 32f, 19f),
                _numericEditorLabel ?? "Value",
                _mutedStyle);

            Rect fieldRect = new Rect(panel.x + 16f, panel.y + 60f, panel.width - 32f, 33f);
            _numericEditorText = GUI.TextField(
                fieldRect,
                _numericEditorText ?? string.Empty,
                Menu.UiSearchFieldStyle);
            GUI.Label(
                new Rect(panel.x + 16f, panel.y + 95f, panel.width - 32f, 18f),
                $"Range: {_numericEditorMinimum:0.###} to {_numericEditorMaximum:0.###}",
                _mutedStyle);

            string[] keys = _numericEditorIsInteger
                ? new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "-", "0", "⌫" }
                : new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "-", "0", ".", "⌫" };
            const float keyWidth = 84f;
            const float keyHeight = 34f;
            const float keyGap = 7f;
            int columns = 4;
            for (int i = 0; i < keys.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect keyRect = new Rect(
                    panel.x + 16f + column * (keyWidth + keyGap),
                    panel.y + 119f + row * (keyHeight + keyGap),
                    keyWidth,
                    keyHeight);
                if (Menu.ControllerButton(keyRect, $"vehicle_numeric_{i}", keys[i], Menu.UiButtonStyle))
                    AppendNumericKey(keys[i]);
            }

            float buttonsY = panel.yMax - 48f;
            if (Menu.ControllerButton(
                    new Rect(panel.x + 16f, buttonsY, 138f, 32f),
                    "vehicle_numeric_cancel",
                    "Cancel",
                    Menu.UiButtonStyle))
            {
                CloseNumericEditor();
                return;
            }

            if (Menu.ControllerButton(
                    new Rect(panel.xMax - 154f, buttonsY, 138f, 32f),
                    "vehicle_numeric_apply",
                    "Apply",
                    _resetStyle))
            {
                ApplyNumericEditor();
                return;
            }

            if (_numericEditorFocusPending && Menu.FocusControllerTarget("vehicle_numeric_0"))
                _numericEditorFocusPending = false;
        }

        private static void AppendNumericKey(string key)
        {
            string current = _numericEditorText ?? string.Empty;
            if (key == "⌫")
            {
                if (current.Length > 0)
                    _numericEditorText = current.Substring(0, current.Length - 1);
                return;
            }

            if (key == "-")
            {
                _numericEditorText = current.StartsWith("-", StringComparison.Ordinal)
                    ? current.Substring(1)
                    : "-" + current;
                return;
            }

            if (key == "." && (current.Contains(".") || _numericEditorIsInteger))
                return;

            _numericEditorText = current + key;
        }

        private static void ApplyNumericEditor()
        {
            if (string.IsNullOrWhiteSpace(_numericEditorPath))
            {
                CloseNumericEditor();
                return;
            }

            if (float.TryParse(
                    _numericEditorText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value))
            {
                value = Mathf.Clamp(value, _numericEditorMinimum, _numericEditorMaximum);
                if (_numericEditorIsInteger)
                    value = Mathf.Round(value);
                PendingNumericValues[_numericEditorPath] = value;
            }

            CloseNumericEditor();
        }

        private static bool DrawRangedFloatField(
            string path,
            float value,
            float minimum,
            float maximum,
            out float next)
        {
            float sliderValue = Menu.ControllerSlider(
                $"vehicle_float_slider_{path}",
                value,
                minimum,
                maximum,
                out bool openNumericEditor,
                GUILayout.MinWidth(150f),
                GUILayout.ExpandWidth(true));
            if (openNumericEditor)
                OpenNumericEditor(path, value, minimum, maximum, false);
            if (TryTakeNumericValue(path, out float exactValue))
                sliderValue = exactValue;
            bool sliderChanged = !Mathf.Approximately(sliderValue, value);
            float displayedValue = sliderChanged ? sliderValue : value;
            if (sliderChanged)
                TextEdits[path] = displayedValue.ToString("0.###", CultureInfo.InvariantCulture);

            bool textChanged = DrawFloatTextBox(path, displayedValue, out float textValue, 92f);
            next = textChanged ? Mathf.Clamp(textValue, minimum, maximum) : displayedValue;
            if (textChanged)
                TextEdits[path] = next.ToString("0.###", CultureInfo.InvariantCulture);
            return sliderChanged || textChanged;
        }

        private static bool DrawRangedIntField(
            string path,
            int value,
            int minimum,
            int maximum,
            out int next)
        {
            int sliderValue = Mathf.RoundToInt(Menu.ControllerSlider(
                $"vehicle_int_slider_{path}",
                value,
                minimum,
                maximum,
                out bool openNumericEditor,
                GUILayout.MinWidth(150f),
                GUILayout.ExpandWidth(true)));
            if (openNumericEditor)
                OpenNumericEditor(path, value, minimum, maximum, true);
            if (TryTakeNumericValue(path, out float exactValue))
                sliderValue = Mathf.RoundToInt(exactValue);
            bool sliderChanged = sliderValue != value;
            int displayedValue = sliderChanged ? sliderValue : value;
            if (sliderChanged)
                TextEdits[path] = displayedValue.ToString(CultureInfo.InvariantCulture);

            bool textChanged = DrawIntTextBox(path, displayedValue, out int textValue, 92f);
            next = textChanged ? Mathf.Clamp(textValue, minimum, maximum) : displayedValue;
            if (textChanged)
                TextEdits[path] = next.ToString(CultureInfo.InvariantCulture);
            return sliderChanged || textChanged;
        }

        private static bool DrawFloatField(
            string path,
            float value,
            out float next,
            string axis = null)
        {
            if (!string.IsNullOrEmpty(axis))
                GUILayout.Label(axis, _mutedStyle, GUILayout.Width(AxisLabelWidth));

            string formatted = value.ToString("0.###", CultureInfo.InvariantCulture);
            if (!TextEdits.TryGetValue(path, out string edit))
            {
                edit = formatted;
                TextEdits[path] = edit;
            }

            Rect fieldRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                Menu.UiSearchFieldStyle,
                GUILayout.MinWidth(string.IsNullOrEmpty(axis) ? 120f : 64f),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(27f));
            string nextText = GUI.TextField(fieldRect, edit, Menu.UiSearchFieldStyle);
            int controllerDirection = Menu.ControllerStepControl($"vehicle_float_{path}", fieldRect);
            TextEdits[path] = nextText;

            next = value;
            if (controllerDirection != 0)
            {
                float magnitude = Mathf.Abs(value);
                float step = magnitude >= 100f ? 1f : magnitude >= 10f ? 0.1f : 0.05f;
                next = value + (controllerDirection * step);
                TextEdits[path] = next.ToString("0.###", CultureInfo.InvariantCulture);
                return true;
            }
            return !string.Equals(nextText, edit, StringComparison.Ordinal) &&
                float.TryParse(nextText, NumberStyles.Float, CultureInfo.InvariantCulture, out next) &&
                !Mathf.Approximately(next, value);
        }

        private static bool DrawFloatTextBox(
            string path,
            float value,
            out float next,
            float width)
        {
            string formatted = value.ToString("0.###", CultureInfo.InvariantCulture);
            if (!TextEdits.TryGetValue(path, out string edit))
            {
                edit = formatted;
                TextEdits[path] = edit;
            }

            string nextText = GUILayout.TextField(
                edit,
                Menu.UiSearchFieldStyle,
                GUILayout.Width(width),
                GUILayout.Height(27f));
            TextEdits[path] = nextText;
            next = value;
            return !string.Equals(nextText, edit, StringComparison.Ordinal) &&
                float.TryParse(nextText, NumberStyles.Float, CultureInfo.InvariantCulture, out next) &&
                !Mathf.Approximately(next, value);
        }

        private static bool DrawIntField(string path, int value, out int next)
        {
            string formatted = value.ToString(CultureInfo.InvariantCulture);
            if (!TextEdits.TryGetValue(path, out string edit))
            {
                edit = formatted;
                TextEdits[path] = edit;
            }

            Rect fieldRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                Menu.UiSearchFieldStyle,
                GUILayout.MinWidth(120f),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(27f));
            string nextText = GUI.TextField(fieldRect, edit, Menu.UiSearchFieldStyle);
            int controllerDirection = Menu.ControllerStepControl($"vehicle_int_{path}", fieldRect);
            TextEdits[path] = nextText;

            next = value;
            if (controllerDirection != 0)
            {
                next = value + controllerDirection;
                TextEdits[path] = next.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            return !string.Equals(nextText, edit, StringComparison.Ordinal) &&
                int.TryParse(nextText, NumberStyles.Integer, CultureInfo.InvariantCulture, out next) &&
                next != value;
        }

        private static bool DrawIntTextBox(
            string path,
            int value,
            out int next,
            float width)
        {
            string formatted = value.ToString(CultureInfo.InvariantCulture);
            if (!TextEdits.TryGetValue(path, out string edit))
            {
                edit = formatted;
                TextEdits[path] = edit;
            }

            string nextText = GUILayout.TextField(
                edit,
                Menu.UiSearchFieldStyle,
                GUILayout.Width(width),
                GUILayout.Height(27f));
            TextEdits[path] = nextText;
            next = value;
            return !string.Equals(nextText, edit, StringComparison.Ordinal) &&
                int.TryParse(nextText, NumberStyles.Integer, CultureInfo.InvariantCulture, out next) &&
                next != value;
        }

        private static bool TryGetSliderRange(
            Il2CppFieldInfo field,
            out float minimum,
            out float maximum)
        {
            minimum = 0f;
            maximum = 0f;
            if (field == null)
                return false;

            string cacheKey = $"{field.DeclaringType?.FullName ?? string.Empty}.{field.Name}";
            if (SliderRanges.TryGetValue(cacheKey, out SliderRange cached))
            {
                minimum = cached.Minimum;
                maximum = cached.Maximum;
                return cached.HasRange;
            }

            try
            {
                Il2CppType rangeType = Il2CppInterop.Runtime.Il2CppType.From(typeof(RangeAttribute));
                if (!field.IsDefined(rangeType, true))
                {
                    SliderRanges[cacheKey] = default;
                    return false;
                }

                var attributes = field.GetCustomAttributes(rangeType, true);
                if (attributes == null || attributes.Length == 0)
                {
                    SliderRanges[cacheKey] = default;
                    return false;
                }

                RangeAttribute range = attributes[0]?.TryCast<RangeAttribute>();
                if (range == null || range.max <= range.min)
                {
                    SliderRanges[cacheKey] = default;
                    return false;
                }

                minimum = range.min;
                maximum = range.max;
                SliderRanges[cacheKey] = new SliderRange
                {
                    HasRange = true,
                    Minimum = minimum,
                    Maximum = maximum
                };
                return true;
            }
            catch (Exception ex)
            {
                SliderRanges[cacheKey] = default;
                Log.Warning($"[RuntimeVehicleReset] Could not read range for {field.Name}: {ex.Message}");
                return false;
            }
        }

        private struct SliderRange
        {
            public bool HasRange;
            public float Minimum;
            public float Maximum;
        }

        private static bool DrawEnumField(
            string path,
            string label,
            Il2CppType type,
            Il2CppObject currentValue,
            out Il2CppObject nextValue)
        {
            nextValue = currentValue;
            var names = Il2CppSystem.Enum.GetNames(type);
            if (names == null || names.Length == 0)
            {
                GUILayout.Label(currentValue.ToString(), _mutedStyle, GUILayout.ExpandWidth(true));
                return false;
            }

            string currentName = currentValue.ToString();
            int index = 0;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], currentName, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            bool isOpen = string.Equals(_openEnumPath, path, StringComparison.Ordinal);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (Menu.ControllerButton(
                    $"vehicle_enum_{path}",
                    $"{label}: {names[index]} {(isOpen ? "▲" : "▼")}",
                    isOpen ? Menu.UiRowButtonSelectedStyle : Menu.UiRowButtonStyle,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(30f)))
            {
                _openEnumPath = isOpen ? null : path;
                isOpen = !isOpen;
            }

            bool changed = false;
            if (isOpen)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    int optionIndex = i;
                    if (Menu.ControllerButton(
                            $"vehicle_enum_{path}_{names[i]}",
                            names[i],
                            i == index ? Menu.UiRowButtonSelectedStyle : Menu.UiRowButtonStyle,
                            GUILayout.ExpandWidth(true),
                            GUILayout.Height(28f)))
                    {
                        index = optionIndex;
                        changed = !string.Equals(names[index], currentName, StringComparison.Ordinal);
                        _openEnumPath = null;
                    }
                }
            }
            GUILayout.EndVertical();

            if (changed)
                nextValue = Il2CppSystem.Enum.Parse(type, names[index]);
            return changed;
        }

        private static void DrawReadOnlyRow(string label, Il2CppObject value, int depth)
        {
            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            GUILayout.Space(depth * 18f);
            GUILayout.Label(label, _labelStyle, GUILayout.Width(330f));
            GUILayout.Label(value?.ToString() ?? "null", _mutedStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(ResetWidth + 4f);
            GUILayout.EndHorizontal();
        }

        private static unsafe Il2CppObject BoxValue<T>(T value) where T : unmanaged
        {
            IntPtr boxed = IL2CPP.il2cpp_value_box(
                Il2CppClassPointerStore<T>.NativeClassPtr,
                (IntPtr)(&value));
            return new Il2CppObject(boxed);
        }

        private static RuntimeVehicleTuneMenu ResolveMenu()
        {
            if (_menu != null)
                return _menu;

            _menu = UnityEngine.Object.FindObjectOfType<RuntimeVehicleTuneMenu>();
            return _menu;
        }

        private static void RefreshFlipSystem()
        {
            OnePointOhFlipSystem system = UnityEngine.Object.FindObjectOfType<OnePointOhFlipSystem>();
            if (system == null)
            {
                _flipSystem = null;
                _flipSystemDefaults = null;
                _flipSystemInstanceId = int.MinValue;
                return;
            }

            int instanceId = system.GetInstanceID();
            if (_flipSystem == system && _flipSystemDefaults != null && _flipSystemInstanceId == instanceId)
                return;

            _flipSystem = system;
            _flipSystemInstanceId = instanceId;
            _flipSystemDefaults = CaptureFlipSystem(system);
            Log.Msg($"[RuntimeVehicleReset] Captured One Point Oh flip-system defaults from {system.name}.");
        }

        private static FlipSystemSnapshot CaptureFlipSystem(OnePointOhFlipSystem system)
        {
            if (system == null)
                return null;

            return new FlipSystemSnapshot
            {
                FlipAxis = system._flipAxisLocal,
                SpinAxis = system._spinAxisLocal,
                SpinFirst = system._spinFirst,
                SpinMultiplier = system._spinMult,
                FlipMultiplier = system._flipMult,
                DefaultTrickDuration = system._defaultTrickDuration,
                MinimumTrickDuration = system._minTrickDuration,
                LandingSafetySeconds = system._landingSafetySeconds,
                BackflipDirectionMultiplier = system._backflipDirMult,
                SpinDirectionMultiplier = system._spinDirMult,
                Flip = CaptureAngular(system._flip),
                Spin = CaptureAngular(system._spin),
                Drive = CaptureDrive(system._drive)
            };
        }

        private static AngularSnapshot CaptureAngular(Angular1D angular)
        {
            if (angular == null)
                return null;

            return new AngularSnapshot
            {
                Inertia = angular.inertia,
                AngularDrag = angular.angularDrag,
                MaximumAngularVelocity = angular.maxAbsAngularVelocityDeg,
                UseDrive = angular.useDrive,
                TargetAngle = angular.targetAngleDeg,
                Stiffness = angular.stiffness,
                Damping = angular.damping,
                UseLimits = angular.useLimits,
                MinimumAngle = angular.minAngleDeg,
                MaximumAngle = angular.maxAngleDeg
            };
        }

        private static DriveSnapshot CaptureDrive(QuaternionPDDrive drive)
        {
            if (drive == null)
                return null;

            return new DriveSnapshot
            {
                ProportionalGain = drive.kp,
                DerivativeGain = drive.kd,
                MaximumAngularAcceleration = drive.maxAngularAccel,
                MaximumAngularVelocity = drive.maxAngularVelocity,
                InertiaCompensation = drive.inertiaCompensation
            };
        }

        private static void DrawFlipSystemEditor()
        {
            if (_flipSystem == null || _flipSystemDefaults == null)
                RefreshFlipSystem();

            string title = _flipSystemExpanded
                ? "One Point Oh Flip System  ▲"
                : "One Point Oh Flip System  ▼";
            if (Menu.ControllerButton("vehicle_flip_system_foldout", title, Menu.UiButtonStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(34f)))
                _flipSystemExpanded = !_flipSystemExpanded;

            if (!_flipSystemExpanded)
                return;

            if (_flipSystem == null || _flipSystemDefaults == null)
            {
                GUILayout.Label("No active One Point Oh flip system was found on this vehicle.", _mutedStyle);
                return;
            }

            GUILayout.BeginVertical(_rowStyle);
            GUILayout.Label("Live One Point Oh trick rotation", _labelStyle);
            GUILayout.Label(
                $"Current flip: {_flipSystem.CurrentFlipDeg:0.0}°    Current spin: {_flipSystem.CurrentSpinDeg:0.0}°",
                _mutedStyle);
            GUILayout.Label("Only authored tuning values are shown; live trick state and internal transforms stay protected.", _mutedStyle);
            GUILayout.EndVertical();

            DrawFlipToggle(
                "forward_only_landing",
                "Forward-Only Landing Finish",
                Config.physics.spinCompletionAssist,
                false,
                value =>
                {
                    Config.physics.spinCompletionAssist = value;
                    Config.RequestSave();
                    if (!value)
                        Physics.ReleaseSpinCompletionAssist();
                });
            if (Config.physics.spinCompletionAssist)
            {
                GUILayout.Label(
                    "Keeps the landing correction in your current spin direction and, once per planned 360, shortens only the final native spin segment when it is still under-rotated.",
                    _mutedStyle,
                    GUILayout.ExpandWidth(true));
            }

            DrawFlipVector("flip_axis", "Flip Axis", _flipSystem._flipAxisLocal, _flipSystemDefaults.FlipAxis,
                value => _flipSystem._flipAxisLocal = value);
            DrawFlipVector("spin_axis", "Spin Axis", _flipSystem._spinAxisLocal, _flipSystemDefaults.SpinAxis,
                value => _flipSystem._spinAxisLocal = value);
            DrawFlipToggle("spin_first", "Spin Before Flip", _flipSystem._spinFirst, _flipSystemDefaults.SpinFirst,
                value => _flipSystem._spinFirst = value);
            DrawFlipFloat("spin_mult", "Spin Multiplier", _flipSystem._spinMult, _flipSystemDefaults.SpinMultiplier,
                -5f, 5f, value => _flipSystem._spinMult = value);
            DrawFlipFloat("flip_mult", "Flip Multiplier", _flipSystem._flipMult, _flipSystemDefaults.FlipMultiplier,
                -5f, 5f, value => _flipSystem._flipMult = value);
            DrawFlipFloat("default_duration", "Default Trick Duration", _flipSystem._defaultTrickDuration,
                _flipSystemDefaults.DefaultTrickDuration, 0.01f, 5f, value => _flipSystem._defaultTrickDuration = value);
            DrawFlipFloat("minimum_duration", "Minimum Trick Duration", _flipSystem._minTrickDuration,
                _flipSystemDefaults.MinimumTrickDuration, 0.01f, 5f, value => _flipSystem._minTrickDuration = value);
            DrawFlipFloat("landing_safety", "Landing Safety Time", _flipSystem._landingSafetySeconds,
                _flipSystemDefaults.LandingSafetySeconds, 0f, 3f, value => _flipSystem._landingSafetySeconds = value);
            DrawFlipFloat("backflip_direction", "Backflip Direction Multiplier", _flipSystem._backflipDirMult,
                _flipSystemDefaults.BackflipDirectionMultiplier, -5f, 5f, value => _flipSystem._backflipDirMult = value);
            DrawFlipFloat("spin_direction", "Spin Direction Multiplier", _flipSystem._spinDirMult,
                _flipSystemDefaults.SpinDirectionMultiplier, -5f, 5f, value => _flipSystem._spinDirMult = value);

            DrawAngularEditor("flip", "Flip Dynamics", _flipSystem._flip, _flipSystemDefaults.Flip, ref _flipDynamicsExpanded);
            DrawAngularEditor("spin", "Spin Dynamics", _flipSystem._spin, _flipSystemDefaults.Spin, ref _spinDynamicsExpanded);
            DrawDriveEditor(_flipSystem._drive, _flipSystemDefaults.Drive);
        }

        private static void DrawAngularEditor(
            string id,
            string label,
            Angular1D angular,
            AngularSnapshot defaults,
            ref bool expanded)
        {
            bool nextExpanded = expanded;
            if (Menu.ControllerButton($"vehicle_{id}_dynamics_foldout",
                    expanded ? $"{label}  ▲" : $"{label}  ▼", Menu.UiButtonStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(30f)))
                nextExpanded = !expanded;
            expanded = nextExpanded;

            if (!expanded || angular == null || defaults == null)
                return;

            DrawFlipFloat($"{id}_inertia", "Inertia", angular.inertia, defaults.Inertia, 0f, 100f,
                value => angular.inertia = value);
            DrawFlipFloat($"{id}_drag", "Angular Drag", angular.angularDrag, defaults.AngularDrag, 0f, 100f,
                value => angular.angularDrag = value);
            DrawFlipFloat($"{id}_max_velocity", "Maximum Angular Velocity", angular.maxAbsAngularVelocityDeg,
                defaults.MaximumAngularVelocity, 0f, 3000f, value => angular.maxAbsAngularVelocityDeg = value);
            DrawFlipToggle($"{id}_use_drive", "Use Angle Drive", angular.useDrive, defaults.UseDrive,
                value => angular.useDrive = value);
            DrawFlipFloat($"{id}_target", "Target Angle", angular.targetAngleDeg, defaults.TargetAngle,
                -1440f, 1440f, value => angular.targetAngleDeg = value);
            DrawFlipFloat($"{id}_stiffness", "Stiffness", angular.stiffness, defaults.Stiffness,
                0f, 5000f, value => angular.stiffness = value);
            DrawFlipFloat($"{id}_damping", "Damping", angular.damping, defaults.Damping,
                0f, 1000f, value => angular.damping = value);
            DrawFlipToggle($"{id}_use_limits", "Use Angle Limits", angular.useLimits, defaults.UseLimits,
                value => angular.useLimits = value);
            DrawFlipFloat($"{id}_minimum", "Minimum Angle", angular.minAngleDeg, defaults.MinimumAngle,
                -1440f, 1440f, value => angular.minAngleDeg = value);
            DrawFlipFloat($"{id}_maximum", "Maximum Angle", angular.maxAngleDeg, defaults.MaximumAngle,
                -1440f, 1440f, value => angular.maxAngleDeg = value);
        }

        private static void DrawDriveEditor(QuaternionPDDrive drive, DriveSnapshot defaults)
        {
            if (Menu.ControllerButton("vehicle_rotation_drive_foldout",
                    _rotationDriveExpanded ? "Rotation Drive  ▲" : "Rotation Drive  ▼", Menu.UiButtonStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(30f)))
                _rotationDriveExpanded = !_rotationDriveExpanded;

            if (!_rotationDriveExpanded || drive == null || defaults == null)
                return;

            DrawFlipFloat("drive_kp", "Proportional Gain", drive.kp, defaults.ProportionalGain,
                0f, 5000f, value => drive.kp = value);
            DrawFlipFloat("drive_kd", "Derivative Gain", drive.kd, defaults.DerivativeGain,
                0f, 1000f, value => drive.kd = value);
            DrawFlipFloat("drive_max_accel", "Maximum Angular Acceleration", drive.maxAngularAccel,
                defaults.MaximumAngularAcceleration, 0f, 10000f, value => drive.maxAngularAccel = value);
            DrawFlipFloat("drive_max_velocity", "Maximum Angular Velocity", drive.maxAngularVelocity,
                defaults.MaximumAngularVelocity, 0f, 3000f, value => drive.maxAngularVelocity = value);
            DrawFlipToggle("drive_inertia", "Inertia Compensation", drive.inertiaCompensation,
                defaults.InertiaCompensation, value => drive.inertiaCompensation = value);
        }

        private static void DrawFlipVector(
            string id,
            string label,
            Vector3 value,
            Vector3 defaults,
            Action<Vector3> apply)
        {
            GUILayout.Label(label, _labelStyle);
            DrawFlipFloat($"{id}_x", "X", value.x, defaults.x, -1f, 1f,
                next => { Vector3 changed = value; changed.x = next; apply(changed); });
            DrawFlipFloat($"{id}_y", "Y", value.y, defaults.y, -1f, 1f,
                next => { Vector3 changed = value; changed.y = next; apply(changed); });
            DrawFlipFloat($"{id}_z", "Z", value.z, defaults.z, -1f, 1f,
                next => { Vector3 changed = value; changed.z = next; apply(changed); });
        }

        private static void DrawFlipFloat(
            string id,
            string label,
            float value,
            float defaultValue,
            float minimum,
            float maximum,
            Action<float> apply)
        {
            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            GUILayout.Label(label, _labelStyle, GUILayout.Width(280f));
            float effectiveMinimum = Mathf.Min(minimum, value, defaultValue);
            float effectiveMaximum = Mathf.Max(maximum, value, defaultValue);
            float next = Menu.ControllerSlider($"vehicle_flip_{id}", value, effectiveMinimum, effectiveMaximum,
                GUILayout.MinWidth(170f), GUILayout.ExpandWidth(true), GUILayout.Height(24f));
            GUILayout.Label(next.ToString("0.###", CultureInfo.InvariantCulture), _mutedStyle, GUILayout.Width(72f));
            if (Menu.ControllerButton($"vehicle_flip_reset_{id}", "Reset",
                    Mathf.Approximately(value, defaultValue) ? _resetDefaultStyle : _resetStyle,
                    GUILayout.Width(ResetWidth), GUILayout.Height(28f)))
                next = defaultValue;
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(next, value))
                apply(next);
        }

        private static void DrawFlipToggle(
            string id,
            string label,
            bool value,
            bool defaultValue,
            Action<bool> apply)
        {
            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(RowHeight));
            bool next = value;
            Menu.ModernToggle(label, ref next, $"vehicle_flip_{id}");
            if (Menu.ControllerButton($"vehicle_flip_reset_{id}", "Reset",
                    value == defaultValue ? _resetDefaultStyle : _resetStyle,
                    GUILayout.Width(ResetWidth), GUILayout.Height(28f)))
                next = defaultValue;
            GUILayout.EndHorizontal();

            if (next != value)
                apply(next);
        }

        private static void RefreshVehicle(RuntimeVehicleTuneMenu menu, bool log)
        {
            try
            {
                menu.RefreshCurrentVehicle(true);
                RefreshFlipSystem();
                TextEdits.Clear();
                _scroll = Vector2.zero;

                // RefreshCurrentVehicle commonly returns the same live settings object.
                // Preserve its original snapshot across window closes and refreshes; only
                // discard the baseline when the game actually changes settings instances.
                int refreshedInstanceId = menu._currentSettings != null
                    ? menu._currentSettings.GetInstanceID()
                    : int.MinValue;
                if (_defaultSettings != null && _settingsInstanceId != refreshedInstanceId)
                    ReleaseDefaultSnapshot();

                if (log)
                {
                    Log.Msg(menu._currentSettings != null
                        ? $"[RuntimeVehicleReset] Refreshed {menu._currentSettings.name}."
                        : "[RuntimeVehicleReset] Refresh found no supported vehicle settings.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RuntimeVehicleReset] Refresh failed: {ex}");
            }
        }

        private static bool ValuesMatch(
            Il2CppType type,
            Il2CppObject currentValue,
            Il2CppObject defaultValue)
        {
            if (currentValue == null || defaultValue == null)
                return currentValue == null && defaultValue == null;
            if (type == null)
                return false;

            try
            {
                string typeName = type.FullName ?? string.Empty;
                if (typeName == "System.Single")
                    return Mathf.Approximately(
                        currentValue.Unbox<float>(),
                        defaultValue.Unbox<float>());
                if (typeName == "System.Int32")
                    return currentValue.Unbox<int>() == defaultValue.Unbox<int>();
                if (typeName == "System.Boolean")
                    return currentValue.Unbox<bool>() == defaultValue.Unbox<bool>();
                if (type.IsEnum)
                    return string.Equals(
                        currentValue.ToString(),
                        defaultValue.ToString(),
                        StringComparison.Ordinal);
                if (typeName == "UnityEngine.Vector2")
                {
                    Vector2 current = currentValue.Unbox<Vector2>();
                    Vector2 baseline = defaultValue.Unbox<Vector2>();
                    return Mathf.Approximately(current.x, baseline.x) &&
                           Mathf.Approximately(current.y, baseline.y);
                }
                if (typeName == "UnityEngine.Vector3")
                {
                    Vector3 current = currentValue.Unbox<Vector3>();
                    Vector3 baseline = defaultValue.Unbox<Vector3>();
                    return Mathf.Approximately(current.x, baseline.x) &&
                           Mathf.Approximately(current.y, baseline.y) &&
                           Mathf.Approximately(current.z, baseline.z);
                }
                if (typeName == "UnityEngine.Vector4")
                {
                    Vector4 current = currentValue.Unbox<Vector4>();
                    Vector4 baseline = defaultValue.Unbox<Vector4>();
                    return Mathf.Approximately(current.x, baseline.x) &&
                           Mathf.Approximately(current.y, baseline.y) &&
                           Mathf.Approximately(current.z, baseline.z) &&
                           Mathf.Approximately(current.w, baseline.w);
                }

                return RuntimeVehicleTuneMenu.ValuesEqual(currentValue, defaultValue);
            }
            catch
            {
                return false;
            }
        }

        private static bool EnsureDefaultSnapshot(RuntimeVehicleTuneMenu menu)
        {
            if (menu?._currentSettings == null)
                return false;

            EnsureTireRideDefaults(ResolveTireRideMagnet(menu._currentVehicle));
            int instanceId = menu._currentSettings.GetInstanceID();
            if (_defaultSettings != null && _settingsInstanceId == instanceId)
                return true;

            ReleaseDefaultSnapshot();
            TextEdits.Clear();
            _settingsInstanceId = instanceId;

            try
            {
                _defaultSettings = UnityEngine.Object.Instantiate(menu._currentSettings);
                if (_defaultSettings == null)
                    return false;

                _defaultSettings.name = $"{menu._currentSettings.name}_RoweModDefaults";
                _defaultSettings.hideFlags = HideFlags.HideAndDontSave;
                VehicleController vehicle = menu._currentVehicle;
                TransitionSettings transition = vehicle?.TransitionSettings;
                if (transition != null)
                {
                    _defaultTransitionSettings = UnityEngine.Object.Instantiate(transition);
                    _defaultTransitionSettings.name = $"{transition.name}_RoweModDefaults";
                    _defaultTransitionSettings.hideFlags = HideFlags.HideAndDontSave;
                }

                FlightAugmentSettings flight = vehicle?.FlightAugmentSettings;
                if (flight != null)
                {
                    _defaultFlightAugmentSettings = UnityEngine.Object.Instantiate(flight);
                    _defaultFlightAugmentSettings.name = $"{flight.name}_RoweModDefaults";
                    _defaultFlightAugmentSettings.hideFlags = HideFlags.HideAndDontSave;
                }
                Log.Msg($"[RuntimeVehicleReset] Captured defaults for {menu._currentSettings.name} ({instanceId}).");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RuntimeVehicleReset] Could not capture defaults: {ex}");
                return false;
            }
        }

        private static void ApplyChanges(RuntimeVehicleTuneMenu menu)
        {
            menu._textEdits?.Clear();
            menu._currentVehicle?.ApplyRuntimeTuningSettings();
        }

        private static void EnsureWindowRect()
        {
            float availableWidth = Mathf.Max(320f, Screen.width - (ScreenMargin * 2f));
            float availableHeight = Mathf.Max(280f, Screen.height - (ScreenMargin * 2f));
            float width = Mathf.Min(1120f, availableWidth);
            float height = Mathf.Min(900f, availableHeight);

            if (_windowRect.width <= 0f || _windowRect.height <= 0f)
            {
                _windowRect = new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height);
            }
            else
            {
                _windowRect.width = Mathf.Clamp(_windowRect.width, Mathf.Min(MinimumWindowWidth, availableWidth), availableWidth);
                _windowRect.height = Mathf.Clamp(_windowRect.height, Mathf.Min(MinimumWindowHeight, availableHeight), availableHeight);
            }

            ClampWindowToScreen();
        }

        private static void ClampWindowToScreen()
        {
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
        }

        private static void EnsureStyles()
        {
            if (_windowStyle != null)
                return;

            _windowTexture = MakeSolidTexture(new Color(0.025f, 0.03f, 0.038f, 0.985f));
            _rowTexture = MakeSolidTexture(new Color(0.08f, 0.09f, 0.11f, 0.96f));
            _resetTexture = MakeSolidTexture(new Color(0.72f, 0.07f, 0.07f, 1f));
            _resetHoverTexture = MakeSolidTexture(new Color(0.95f, 0.12f, 0.10f, 1f));
            _resetActiveTexture = MakeSolidTexture(new Color(0.52f, 0.03f, 0.03f, 1f));
            _resetDefaultTexture = MakeSolidTexture(new Color(0.43f, 0.06f, 0.06f, 1f));

            _windowStyle = new GUIStyle(Menu.windowStyle ?? GUI.skin.window)
            {
                padding = new RectOffset(18, 18, 42, 16),
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            SetAllBackgrounds(_windowStyle, _windowTexture);
            SetAllTextColors(_windowStyle, Color.white);

            _rowStyle = new GUIStyle(Menu.UiPanelAltStyle ?? GUI.skin.box)
            {
                padding = new RectOffset(9, 9, 4, 4),
                margin = new RectOffset(0, 0, 2, 2),
                alignment = TextAnchor.MiddleLeft
            };
            SetAllBackgrounds(_rowStyle, _rowTexture);

            _labelStyle = new GUIStyle(Menu.UiRowLabelStyle ?? GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            SetAllTextColors(_labelStyle, Color.white);

            _mutedStyle = new GUIStyle(Menu.UiMutedStyle ?? GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
            };
            SetAllTextColors(_mutedStyle, new Color(0.72f, 0.78f, 0.84f, 1f));

            _resetStyle = CreateResetStyle(
                _resetTexture,
                _resetHoverTexture,
                _resetActiveTexture,
                Color.white);
            _resetDefaultStyle = CreateResetStyle(
                _resetDefaultTexture,
                _resetTexture,
                _resetActiveTexture,
                new Color(1f, 1f, 1f, 0.72f));
        }

        private static GUIStyle CreateResetStyle(Texture2D normal, Texture2D hover, Texture2D active, Color textColor)
        {
            GUIStyle style = new GUIStyle(Menu.UiDangerButtonStyle ?? GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.background = normal;
            style.normal.textColor = textColor;
            style.hover.background = hover;
            style.hover.textColor = Color.white;
            style.active.background = active;
            style.active.textColor = Color.white;
            style.focused.background = normal;
            style.focused.textColor = textColor;
            return style;
        }

        private static void SetAllBackgrounds(GUIStyle style, Texture2D texture)
        {
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.onNormal.background = texture;
            style.onHover.background = texture;
            style.onActive.background = texture;
            style.onFocused.background = texture;
        }

        private static void SetAllTextColors(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static Texture2D MakeSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "RoweMod Runtime Vehicle Tune UI"
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static void ReleaseDefaultSnapshot()
        {
            if (_defaultSettings != null)
                UnityEngine.Object.Destroy(_defaultSettings);
            if (_defaultTransitionSettings != null)
                UnityEngine.Object.Destroy(_defaultTransitionSettings);
            if (_defaultFlightAugmentSettings != null)
                UnityEngine.Object.Destroy(_defaultFlightAugmentSettings);
            _defaultSettings = null;
            _defaultTransitionSettings = null;
            _defaultFlightAugmentSettings = null;
            _tireRideMagnet = null;
            _defaultTireRideMagnet = null;
            _settingsInstanceId = int.MinValue;
        }

        private static void DestroyStyles()
        {
            DestroyTexture(ref _windowTexture);
            DestroyTexture(ref _rowTexture);
            DestroyTexture(ref _resetTexture);
            DestroyTexture(ref _resetHoverTexture);
            DestroyTexture(ref _resetActiveTexture);
            DestroyTexture(ref _resetDefaultTexture);
            _windowStyle = null;
            _rowStyle = null;
            _labelStyle = null;
            _mutedStyle = null;
            _resetStyle = null;
            _resetDefaultStyle = null;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        private static bool IsEditableLeaf(Il2CppType type)
        {
            if (type == null)
                return false;

            string name = type.FullName ?? string.Empty;
            return type.IsEnum ||
                name == "System.Single" ||
                name == "System.Int32" ||
                name == "System.Boolean" ||
                name == "UnityEngine.Vector2" ||
                name == "UnityEngine.Vector3" ||
                name == "UnityEngine.Vector4";
        }

        private static bool IsLabelOnlyType(Il2CppType type)
        {
            if (type == null)
                return true;

            string name = type.FullName ?? string.Empty;
            if (name == "System.String" || name == "UnityEngine.AnimationCurve")
                return true;

            for (Il2CppType cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                if ((cursor.FullName ?? string.Empty) == "UnityEngine.Object")
                    return true;
            }

            return false;
        }
    }
}
