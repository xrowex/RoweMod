using System.Collections.Generic;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using Il2CppMashBox.Core.Runtime.TrickSystem;
using rowemod.Utils;
using UnityEngine;
using HumanIK = Il2CppMashBox.Core.Runtime.InverseKinematics.HumanIK;
using IKTarget = Il2CppMashBox.Core.Runtime.InverseKinematics.IKTarget;
using UnityIKLimb = Il2CppMashBox.Core.Runtime.InverseKinematics.UnityIKLimb;

namespace rowemod.Mods
{
    internal static class TrickAnimationEditor
    {
        private const float BrainPollInterval = 0.05f;
        private const float OverrideScanInterval = 5f;

        private static TrickSystemBrainV2 observedBrain;
        private static SyncTrickAnimationData currentData;
        private static float nextBrainPollTime;
        private static float nextOverrideScanTime;
        private static int currentSetId = -1;
        private static int currentSlotId = -1;
        private static string currentKey = string.Empty;
        private static string currentName = string.Empty;
        private static float lastObservedTimeSinceFire = 999f;
        private static int lastObservedSetId = -999;
        private static int lastObservedSlotId = -999;
        private static Vector2 trickCatalogScroll;
        private static Vector2 selectedTrickScroll;
        private static Vector2 poseScroll;
        private static Vector2 ikTargetScroll;
        private static bool showClipDetails;
        private static bool showAdvancedFlags;
        private static bool showClipCopyTools = true;
        private static bool showPoseOverlayTools;
        private static bool showIkTargetTools;
        private static string clipSourceSearch = string.Empty;
        private static string trickCatalogSearch = string.Empty;
        private static int selectedClipSourceIndex = -1;
        private static int selectedTrickCatalogIndex = -1;
        private static float nextClipCatalogRefreshTime;
        private static float nextTrickCatalogRefreshTime;
        private static string selectedTrickCatalogKey = string.Empty;
        private static string status = "Throw a trick to load animation data.";
        private static TrickAnimator activeAnimator;
        private static SyncTrickAnimationData activePoseData;
        private static string activePoseDataKey = string.Empty;
        private static string activePhase = "Any";
        private static float activePoseUntil;
        private static string lastPoseLogKey = string.Empty;
        private static string lastAppliedPoseLogKey = string.Empty;
        private static string lastAppliedIkLogKey = string.Empty;

        private static GUIStyle cardStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle mutedStyle;
        private static GUIStyle smallButtonStyle;
        private static GUIStyle panelAltStyle;
        private static GUIStyle rowButtonStyle;
        private static GUIStyle searchFieldStyle;
        private static int styleRevision = -1;

        private static readonly Dictionary<int, TrickAnimationOverride> runtimeDefaults = new Dictionary<int, TrickAnimationOverride>();
        private static readonly List<SyncTrickAnimationData> trickCatalog = new List<SyncTrickAnimationData>();
        private static readonly List<SyncTrickAnimationData> clipSources = new List<SyncTrickAnimationData>();
        private static readonly Dictionary<string, AnimationClip> clipLookup = new Dictionary<string, AnimationClip>();
        private static readonly Dictionary<int, AppliedPoseState> appliedPoseStates = new Dictionary<int, AppliedPoseState>();
        private static readonly Dictionary<int, AppliedIkTargetState> appliedIkTargetStates = new Dictionary<int, AppliedIkTargetState>();
        private static readonly Dictionary<string, AppliedHumanIkWeightState> appliedHumanIkWeightStates = new Dictionary<string, AppliedHumanIkWeightState>();
        private static readonly Dictionary<int, AppliedLimbWeightState> appliedLimbWeightStates = new Dictionary<int, AppliedLimbWeightState>();
        private static readonly HashSet<int> poseTouchedThisFrame = new HashSet<int>();
        private static readonly HashSet<int> ikTargetTouchedThisFrame = new HashSet<int>();
        private static readonly HashSet<string> humanIkTouchedThisFrame = new HashSet<string>();
        private static readonly HashSet<int> limbTouchedThisFrame = new HashSet<int>();
        private static float suppressAutoApplyUntil;

        private static readonly string[] PhaseOptions = { "Any", "Enter", "Tweak", "Loop", "Exit" };
        private static readonly string[] IkGoalOptions = { "LeftHand", "RightHand", "LeftFoot", "RightFoot" };

        private static readonly HumanBodyBones[] EditableBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes
        };

        private sealed class AppliedPoseState
        {
            public Transform Transform;
            public Quaternion BaseRotation = Quaternion.identity;
            public Quaternion AppliedRotation = Quaternion.identity;
            public Vector3 BasePosition = Vector3.zero;
            public Vector3 AppliedPosition = Vector3.zero;
            public int Frame;
        }

        private sealed class AppliedIkTargetState
        {
            public IKTarget Target;
            public Transform Transform;
            public Quaternion BaseRotation = Quaternion.identity;
            public Quaternion AppliedRotation = Quaternion.identity;
            public Vector3 BasePosition = Vector3.zero;
            public Vector3 AppliedPosition = Vector3.zero;
            public bool BaseActive;
            public bool AppliedActive;
        }

        private sealed class AppliedHumanIkWeightState
        {
            public HumanIK HumanIK;
            public AvatarIKGoal Goal;
            public float BaseWeight;
            public float AppliedWeight;
        }

        private sealed class AppliedLimbWeightState
        {
            public UnityIKLimb Limb;
            public float BasePositionWeight;
            public float AppliedPositionWeight;
            public float BaseRotationWeight;
            public float AppliedRotationWeight;
        }

        public static void Update()
        {
            EnsureSettings();
            if (!Config.trickAnimationDebugSettings.editorEnabled)
            {
                RestorePoseOverlays();
                RestoreIkTargetOverlays();
                return;
            }

            PollBrain();

            if (Time.unscaledTime >= nextOverrideScanTime && Time.unscaledTime >= suppressAutoApplyUntil)
            {
                nextOverrideScanTime = Time.unscaledTime + OverrideScanInterval;
                ApplySavedOverridesToLoadedData(false);
            }
        }

        public static void LateUpdate()
        {
            EnsureSettings();
            RestorePoseOverlays();
            RestoreIkTargetOverlays();
        }

        public static void NotifyAnimatorPhase(TrickAnimator animator, SyncTrickAnimationData data, string phaseName)
        {
            if (animator == null || data == null)
                return;

            string dataKey = GetDataKey(data);
            string normalizedPhase = NormalizePhaseName(phaseName);
            string logKey = $"{dataKey}|{normalizedPhase}";

            activeAnimator = animator;
            activePoseData = data;
            activePoseDataKey = dataKey;
            activePhase = normalizedPhase;
            activePoseUntil = Time.unscaledTime + 10f;

            if (logKey != lastPoseLogKey)
            {
                lastPoseLogKey = logKey;
                Log.Msg($"[TrickAnimEditor] Active animation phase: trick='{TrickName(data)}', phase={normalizedPhase}.");
            }
        }

        public static void DrawEditor()
        {
            EnsureSettings();
            InitStyles();

            GUILayout.BeginVertical(cardStyle);

            bool enabled = Config.trickAnimationDebugSettings.editorEnabled;
            bool nextEnabled = Menu.ModernToggle("Trick Animation Editor", ref enabled, "trick_animation_editor_enabled");
            if (nextEnabled != Config.trickAnimationDebugSettings.editorEnabled)
            {
                Config.trickAnimationDebugSettings.editorEnabled = nextEnabled;
                Config.Save();
            }

            if (!Config.trickAnimationDebugSettings.editorEnabled)
            {
                GUILayout.Label("Editor polling is disabled.", mutedStyle);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Space(4);
            GUILayout.Label("Trick Animation Catalog", headerStyle);
            GUILayout.Label(status, mutedStyle);
            EnsureTrickCatalog(false);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Saved Overrides", smallButtonStyle, GUILayout.Width(160)))
            {
                int applied = ApplySavedOverridesToLoadedData(true);
                status = $"Applied saved animation overrides to {applied} loaded trick data objects.";
                EnsureTrickCatalog(true);
            }

            if (GUILayout.Button("Refresh Tricks", smallButtonStyle, GUILayout.Width(120)))
            {
                EnsureTrickCatalog(true);
                status = $"Loaded {trickCatalog.Count} trick animation data objects.";
            }

            GUI.enabled = currentData != null;
            if (GUILayout.Button("Select Live Trick", smallButtonStyle, GUILayout.Width(130)))
            {
                selectedTrickCatalogKey = currentKey;
                selectedTrickCatalogIndex = FindCatalogIndexByKey(currentKey);
                status = selectedTrickCatalogIndex >= 0
                    ? $"Selected live trick {currentName}."
                    : $"Live trick {currentName} is not in the current catalog yet.";
            }
            GUI.enabled = true;

            if (GUILayout.Button("Forget Live Trick", smallButtonStyle, GUILayout.Width(135)))
            {
                currentData = null;
                currentKey = string.Empty;
                currentName = string.Empty;
                currentSetId = -1;
                currentSlotId = -1;
                status = "Live trick cleared. Use the catalog or throw a trick to recapture live phase data.";
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            DrawTrickCatalogList();

            GUILayout.BeginVertical(cardStyle, GUILayout.ExpandWidth(true));
            SyncTrickAnimationData selected = GetSelectedCatalogData();
            if (selected == null)
            {
                GUILayout.Label("No trick selected. Pick a trick on the left to edit its animation data.", mutedStyle);
            }
            else
            {
                RememberRuntimeDefault(selected);
                string selectedKey = GetDataKey(selected);
                string context = selected == currentData
                    ? $"{TrickName(selected)}  live set={currentSetId}, slot={currentSlotId}"
                    : $"{TrickName(selected)}  catalog";

                selectedTrickScroll = GUILayout.BeginScrollView(
                    selectedTrickScroll,
                    GUILayout.Height(Mathf.Min(720f, Mathf.Max(420f, Screen.height - 270f))));
                DrawDataControls(selected, selectedKey, context);
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(8);
        }

        public static void DrawInlineEditor(SyncTrickAnimationData data, string contextKey, string contextLabel)
        {
            EnsureSettings();
            InitStyles();

            if (data == null)
            {
                GUILayout.Label("This trick row has no editable SyncTrickAnimationData loaded.", mutedStyle);
                return;
            }

            RememberRuntimeDefault(data);

            GUILayout.BeginVertical(cardStyle);
            DrawDataControls(data, GetDataKey(data), contextLabel);
            GUILayout.EndVertical();
        }

        public static void DrawSelectedTrickPanel(SyncTrickAnimationData data, string contextKey, string contextLabel)
        {
            EnsureSettings();
            InitStyles();

            if (data == null)
            {
                GUILayout.Label("This trick has no editable SyncTrickAnimationData loaded.", mutedStyle);
                return;
            }

            RememberRuntimeDefault(data);
            DrawDataControls(data, GetDataKey(data), contextLabel);
        }

        public static int ApplySavedOverridesNow()
        {
            return ApplySavedOverridesToLoadedData(true);
        }

        private static void DrawDataControls(SyncTrickAnimationData data, string dataKey, string title)
        {
            if (data == null)
                return;

            string trickName = TrickName(data);
            if (!string.IsNullOrWhiteSpace(title))
                GUILayout.Label(title, headerStyle);
            GUILayout.Label($"Animation data: {SafeRead(() => data.name, "(unnamed)")}", mutedStyle);

            bool changed = false;
            changed |= DrawFloatSlider("Overall Speed", data._overallSpeedMult, 0.05f, 10f, v => data._overallSpeedMult = v);
            changed |= DrawFloatSlider("Enter Speed", data._enterSpeedMult, 0.05f, 5f, v => data._enterSpeedMult = v);
            GUILayout.Label($"Derived Loop Speed: {SafeRead(() => data.LoopSpeedMult, 1f):0.###}", mutedStyle);
            changed |= DrawFloatSlider("Loop Mult", data._loopMult, 0.05f, 5f, v => data._loopMult = v);
            changed |= DrawFloatSlider("Tweak Speed", data._tweakSpeedMult, 0.05f, 10f, v => data._tweakSpeedMult = v);
            changed |= DrawFloatSlider("Exit Speed", data._exitSpeedMult, 0.05f, 5f, v => data._exitSpeedMult = v);
            changed |= DrawFloatSlider("Tweak Blend At", data._tweakBeginBlendNormalizedTime, 0f, 1f, v => data._tweakBeginBlendNormalizedTime = v);

            showAdvancedFlags = GUILayout.Toggle(showAdvancedFlags, showAdvancedFlags ? "v Advanced Flags" : "> Advanced Flags", headerStyle);
            if (showAdvancedFlags)
            {
                bool onlyAir = data._onlyFireIfInAir;
                bool nextOnlyAir = GUILayout.Toggle(onlyAir, "Only Fire If In Air");
                if (nextOnlyAir != onlyAir)
                {
                    data._onlyFireIfInAir = nextOnlyAir;
                    changed = true;
                }

                bool allowLandingHold = data._allowLandingHolding;
                bool nextAllowLandingHold = GUILayout.Toggle(allowLandingHold, "Allow Landing Holding");
                if (nextAllowLandingHold != allowLandingHold)
                {
                    data._allowLandingHolding = nextAllowLandingHold;
                    changed = true;
                }
            }

            if (changed)
            {
                suppressAutoApplyUntil = Time.unscaledTime + 5f;
                status = $"Edited {trickName}. Changes are live in memory.";
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Override", smallButtonStyle, GUILayout.Width(130)))
            {
                Config.trickAnimationDebugSettings.overrides[dataKey] = CaptureOverride(data, dataKey);
                Config.Save();
                status = $"Saved animation override for {trickName}.";
                Log.Msg($"[TrickAnimEditor] Saved override for {trickName} ({dataKey}).");
            }

            if (GUILayout.Button("Reset Runtime", smallButtonStyle, GUILayout.Width(130)))
            {
                RestoreRuntimeDefault(data);
                status = $"Reset runtime values for {trickName}.";
            }

            if (GUILayout.Button("Remove Saved Override", smallButtonStyle, GUILayout.Width(180)))
            {
                if (Config.trickAnimationDebugSettings.overrides.Remove(dataKey))
                {
                    Config.Save();
                    RestoreRuntimeDefault(data);
                    status = $"Removed saved override for {trickName}.";
                }
                else
                {
                    status = $"No saved override exists for {trickName}.";
                }
            }

            GUILayout.EndHorizontal();

            DrawClipCopyTools(data);

            GUILayout.Space(8);
            Menu.BeginAltPane("Pose / IK Editing", "Temporarily disabled for performance. Saved pose and IK data remains in config, but the runtime editor does not expose or apply it in this UI pass.");
            GUILayout.Label("Use speed controls and human/bike animation replacement for now.", mutedStyle);
            Menu.EndPane();

            GUILayout.Space(8);
            showClipDetails = GUILayout.Toggle(showClipDetails, showClipDetails ? "v Animation Clips" : "> Animation Clips", headerStyle);
            if (showClipDetails)
            {
                DrawClipInfo("Player Enter", SafeRead(() => data.PlayerEnterClip, null));
                DrawClipInfo("Player Loop", SafeRead(() => data.PlayerLoopClip, null));
                DrawClipInfo("Player Tweak", SafeRead(() => data.PlayerTweakClip, null));
                DrawClipInfo("Player Exit", SafeRead(() => data.PlayerExitClip, null));
                DrawClipInfo("Player Mirror Enter", SafeRead(() => data.PlayerEnterClipMirror, null));
                DrawClipInfo("Player Mirror Loop", SafeRead(() => data.PlayerLoopClipMirror, null));
                DrawClipInfo("Player Mirror Tweak", SafeRead(() => data.PlayerTweakClipMirror, null));
                DrawClipInfo("Player Mirror Exit", SafeRead(() => data.PlayerExitClipMirror, null));
                GUILayout.Space(6);
                DrawClipInfo("Vehicle Enter", SafeRead(() => data.VehicleEnterClip, null));
                DrawClipInfo("Vehicle Loop", SafeRead(() => data.VehicleLoopClip, null));
                DrawClipInfo("Vehicle Tweak", SafeRead(() => data.VehicleTweakClip, null));
                DrawClipInfo("Vehicle Exit", SafeRead(() => data.VehicleExitClip, null));

                GUILayout.Label(
                    "Curve keyframes are not exposed by Unity's runtime AnimationClip API. In game we can show clip metadata and animation events; full curve keyframes require UnityEditor.AnimationUtility in an editor/export tool.",
                    mutedStyle);
            }
        }

        private static void PollBrain()
        {
            if (Time.unscaledTime < nextBrainPollTime)
                return;

            nextBrainPollTime = Time.unscaledTime + BrainPollInterval;

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
                bool changedFiredTrick = firedSetId != lastObservedSetId || firedSlotId != lastObservedSlotId;
                bool sameTrickFiredAgain =
                    !changedFiredTrick &&
                    lastObservedTimeSinceFire > 0.2f &&
                    timeSinceFire >= 0f &&
                    timeSinceFire < 0.2f;

                if ((changedFiredTrick || sameTrickFiredAgain) && timeSinceFire < 2f)
                {
                    SyncTrickAnimationData data = ResolveV2TrickData(brain, firedSetId, firedSlotId);
                    if (data != null)
                    {
                        TrackCurrentTrick(data, firedSetId, firedSlotId);
                    }

                    lastObservedSetId = firedSetId;
                    lastObservedSlotId = firedSlotId;
                }

                lastObservedTimeSinceFire = timeSinceFire;
            }
            catch (System.Exception ex)
            {
                status = $"Trick animation editor poll failed: {ex.Message}";
            }
        }

        private static void TrackCurrentTrick(SyncTrickAnimationData data, int setId, int slotId)
        {
            currentData = data;
            currentSetId = setId;
            currentSlotId = slotId;
            currentKey = GetDataKey(data);
            currentName = TrickName(data);
            RememberRuntimeDefault(data);
            ApplySavedOverride(data);
            EnsureTrickCatalog(true);
            selectedTrickCatalogKey = currentKey;
            selectedTrickCatalogIndex = FindCatalogIndexByKey(currentKey);
            status = $"Captured {currentName}.";
        }

        private static void EnsureTrickCatalog(bool force)
        {
            if (!force && Time.unscaledTime < nextTrickCatalogRefreshTime && trickCatalog.Count > 0)
                return;

            nextTrickCatalogRefreshTime = Time.unscaledTime + 5f;
            string previousKey = selectedTrickCatalogKey;
            if (string.IsNullOrEmpty(previousKey) && selectedTrickCatalogIndex >= 0 && selectedTrickCatalogIndex < trickCatalog.Count)
                previousKey = GetDataKey(trickCatalog[selectedTrickCatalogIndex]);

            trickCatalog.Clear();
            HashSet<string> seen = new HashSet<string>();

            try
            {
                SyncTrickAnimationData[] allData = Resources.FindObjectsOfTypeAll<SyncTrickAnimationData>();
                if (allData != null)
                {
                    for (int i = 0; i < allData.Length; i++)
                        AddTrickCatalogCandidate(allData[i], seen);
                }

                TrickSystemBrainV2 brain = observedBrain;
                if (brain == null)
                {
                    TrickSystemBrainV2[] brains = UnityEngine.Object.FindObjectsOfType<TrickSystemBrainV2>();
                    if (brains != null && brains.Length > 0)
                    {
                        brain = brains[0];
                        observedBrain = brain;
                    }
                }

                AddBrainTrickDataToCatalog(brain, seen);
            }
            catch (System.Exception ex)
            {
                status = $"Trick catalog refresh failed: {ex.Message}";
            }

            trickCatalog.Sort((a, b) => string.Compare(TrickName(a), TrickName(b), System.StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(previousKey))
            {
                selectedTrickCatalogKey = previousKey;
                selectedTrickCatalogIndex = FindCatalogIndexByKey(previousKey);
            }

            if (selectedTrickCatalogIndex < 0 && !string.IsNullOrEmpty(currentKey))
            {
                selectedTrickCatalogKey = currentKey;
                selectedTrickCatalogIndex = FindCatalogIndexByKey(currentKey);
            }

            if (selectedTrickCatalogIndex < 0 && trickCatalog.Count > 0)
            {
                selectedTrickCatalogIndex = 0;
                selectedTrickCatalogKey = GetDataKey(trickCatalog[0]);
            }

            if (selectedTrickCatalogIndex >= trickCatalog.Count)
                selectedTrickCatalogIndex = trickCatalog.Count - 1;
        }

        private static void AddTrickCatalogCandidate(SyncTrickAnimationData data, HashSet<string> seen)
        {
            if (data == null)
                return;

            if (!TrickMods.IsBmxTrickObject(data))
                return;

            string key = GetDataKey(data);
            if (string.IsNullOrEmpty(key) || seen.Contains(key))
                return;

            seen.Add(key);
            RememberRuntimeDefault(data);
            ApplySavedOverride(data);
            trickCatalog.Add(data);
        }

        private static void AddBrainTrickDataToCatalog(TrickSystemBrainV2 brain, HashSet<string> seen)
        {
            if (brain == null)
                return;

            try
            {
                TrickDataSets dataSets = brain._trickDataSets;
                if (dataSets?.TrickSets == null)
                    return;

                for (int setIndex = 0; setIndex < dataSets.TrickSets.Length; setIndex++)
                {
                    TrickSetData set = dataSets.TrickSets[setIndex];
                    if (set?._dataList == null)
                        continue;

                    for (int slotIndex = 0; slotIndex < set._dataList.Count; slotIndex++)
                    {
                        SyncTrickAnimationData data = set._dataList[(System.Index)slotIndex] as SyncTrickAnimationData;
                        AddTrickCatalogCandidate(data, seen);
                    }
                }
            }
            catch
            {
                // Some menus load before the trick data set is fully wired. Resource scanning still covers loaded assets.
            }
        }

        private static void DrawTrickCatalogList()
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(280));
            GUILayout.Label($"Tricks ({trickCatalog.Count})", headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(55));
            trickCatalogSearch = GUILayout.TextField(trickCatalogSearch ?? string.Empty, searchFieldStyle, GUILayout.Width(145), GUILayout.Height(24));
            if (GUILayout.Button("Refresh", smallButtonStyle, GUILayout.Width(68)))
                EnsureTrickCatalog(true);
            GUILayout.EndHorizontal();

            string search = (trickCatalogSearch ?? string.Empty).Trim();
            float height = Mathf.Min(720f, Mathf.Max(420f, Screen.height - 270f));
            trickCatalogScroll = GUILayout.BeginScrollView(trickCatalogScroll, GUILayout.Height(height));

            int shown = 0;
            for (int i = 0; i < trickCatalog.Count; i++)
            {
                SyncTrickAnimationData data = trickCatalog[i];
                if (data == null)
                    continue;

                string key = GetDataKey(data);
                string name = TrickName(data);
                if (search.Length > 0 &&
                    name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    key.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                shown++;
                bool selected = i == selectedTrickCatalogIndex || string.Equals(key, selectedTrickCatalogKey, System.StringComparison.OrdinalIgnoreCase);
                bool saved = Config.trickAnimationDebugSettings.overrides.ContainsKey(key);
                string label = $"{(selected ? "> " : string.Empty)}{name}{(saved ? " *" : string.Empty)}";
                if (GUILayout.Button(label, selected ? Menu.UiRowButtonSelectedStyle : rowButtonStyle))
                {
                    selectedTrickCatalogIndex = i;
                    selectedTrickCatalogKey = key;
                    selectedTrickScroll = Vector2.zero;
                    RememberRuntimeDefault(data);
                    ApplySavedOverride(data);
                    status = $"Selected {name}.";
                }
            }

            if (shown == 0)
                GUILayout.Label(search.Length > 0 ? "No tricks match the search." : "No trick animation data loaded yet.", mutedStyle);

            GUILayout.EndScrollView();
            GUILayout.Label("* has saved override", mutedStyle);
            GUILayout.EndVertical();
        }

        private static SyncTrickAnimationData GetSelectedCatalogData()
        {
            if (selectedTrickCatalogIndex >= 0 && selectedTrickCatalogIndex < trickCatalog.Count)
            {
                SyncTrickAnimationData data = trickCatalog[selectedTrickCatalogIndex];
                if (data != null && string.Equals(GetDataKey(data), selectedTrickCatalogKey, System.StringComparison.OrdinalIgnoreCase))
                    return data;
            }

            if (!string.IsNullOrEmpty(selectedTrickCatalogKey))
            {
                int index = FindCatalogIndexByKey(selectedTrickCatalogKey);
                if (index >= 0)
                {
                    selectedTrickCatalogIndex = index;
                    return trickCatalog[index];
                }
            }

            return null;
        }

        private static int FindCatalogIndexByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return -1;

            for (int i = 0; i < trickCatalog.Count; i++)
            {
                SyncTrickAnimationData data = trickCatalog[i];
                if (data != null && string.Equals(GetDataKey(data), key, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static int ApplySavedOverridesToLoadedData(bool logResult)
        {
            EnsureSettings();
            if (Config.trickAnimationDebugSettings.overrides.Count == 0)
                return 0;

            int applied = 0;
            try
            {
                SyncTrickAnimationData[] allData = Resources.FindObjectsOfTypeAll<SyncTrickAnimationData>();
                if (allData == null)
                    return 0;

                for (int i = 0; i < allData.Length; i++)
                {
                    SyncTrickAnimationData data = allData[i];
                    if (data == null)
                        continue;

                    RememberRuntimeDefault(data);
                    if (ApplySavedOverride(data))
                        applied++;
                }
            }
            catch (System.Exception ex)
            {
                status = $"Saved override scan failed: {ex.Message}";
            }

            if (logResult)
                Log.Msg($"[TrickAnimEditor] Applied saved overrides to {applied} loaded trick animation data objects.");

            return applied;
        }

        private static bool ApplySavedOverride(SyncTrickAnimationData data)
        {
            if (data == null)
                return false;

            string key = GetDataKey(data);
            if (!Config.trickAnimationDebugSettings.overrides.TryGetValue(key, out TrickAnimationOverride saved) || saved == null)
                return false;

            ApplyOverride(data, saved);
            return true;
        }

        private static void ApplyOverride(SyncTrickAnimationData data, TrickAnimationOverride values)
        {
            data._overallSpeedMult = values.overallSpeedMult;
            data._enterSpeedMult = values.enterSpeedMult;
            data._loopMult = values.loopMult;
            data._tweakSpeedMult = values.tweakSpeedMult;
            data._exitSpeedMult = values.exitSpeedMult;
            data._tweakBeginBlendNormalizedTime = values.tweakBeginBlendNormalizedTime;
            data._onlyFireIfInAir = values.onlyFireIfInAir;
            data._allowLandingHolding = values.allowLandingHolding;

            if (values.hasClipOverride)
                ApplyClipOverride(data, values);
        }

        private static void RememberRuntimeDefault(SyncTrickAnimationData data)
        {
            if (data == null)
                return;

            int id = data.GetInstanceID();
            if (!runtimeDefaults.ContainsKey(id))
                runtimeDefaults[id] = CaptureOverride(data, null);
        }

        private static void RestoreRuntimeDefault(SyncTrickAnimationData data)
        {
            if (data == null)
                return;

            int id = data.GetInstanceID();
            if (runtimeDefaults.TryGetValue(id, out TrickAnimationOverride original) && original != null)
                ApplyOverride(data, original);
        }

        private static TrickAnimationOverride CaptureOverride(SyncTrickAnimationData data, string dataKey)
        {
            TrickAnimationOverride values = new TrickAnimationOverride
            {
                overallSpeedMult = SafeRead(() => data._overallSpeedMult, 1f),
                enterSpeedMult = SafeRead(() => data._enterSpeedMult, 1f),
                loopSpeedMult = SafeRead(() => data.LoopSpeedMult, 1f),
                loopMult = SafeRead(() => data._loopMult, 1f),
                tweakSpeedMult = SafeRead(() => data._tweakSpeedMult, 1f),
                exitSpeedMult = SafeRead(() => data._exitSpeedMult, 1f),
                tweakBeginBlendNormalizedTime = SafeRead(() => data._tweakBeginBlendNormalizedTime, 0.7f),
                onlyFireIfInAir = SafeRead(() => data._onlyFireIfInAir, false),
                allowLandingHolding = SafeRead(() => data._allowLandingHolding, false),
                hasClipOverride = true,
                playerEnterClip = ClipName(SafeRead(() => data.PlayerEnterClip, null)),
                playerLoopClip = ClipName(SafeRead(() => data.PlayerLoopClip, null)),
                playerTweakClip = ClipName(SafeRead(() => data.PlayerTweakClip, null)),
                playerExitClip = ClipName(SafeRead(() => data.PlayerExitClip, null)),
                playerMirrorEnterClip = ClipName(SafeRead(() => data.PlayerEnterClipMirror, null)),
                playerMirrorLoopClip = ClipName(SafeRead(() => data.PlayerLoopClipMirror, null)),
                playerMirrorTweakClip = ClipName(SafeRead(() => data.PlayerTweakClipMirror, null)),
                playerMirrorExitClip = ClipName(SafeRead(() => data.PlayerExitClipMirror, null)),
                vehicleEnterClip = ClipName(SafeRead(() => data.VehicleEnterClip, null)),
                vehicleLoopClip = ClipName(SafeRead(() => data.VehicleLoopClip, null)),
                vehicleTweakClip = ClipName(SafeRead(() => data.VehicleTweakClip, null)),
                vehicleExitClip = ClipName(SafeRead(() => data.VehicleExitClip, null)),
                vehicleMirrorEnterClip = ClipName(SafeRead(() => data.VehicleEnterClipMirror, null)),
                vehicleMirrorLoopClip = ClipName(SafeRead(() => data.VehicleLoopClipMirror, null)),
                vehicleMirrorTweakClip = ClipName(SafeRead(() => data.VehicleTweakClipMirror, null)),
                vehicleMirrorExitClip = ClipName(SafeRead(() => data.VehicleExitClipMirror, null))
            };

            if (!string.IsNullOrEmpty(dataKey) &&
                Config.trickAnimationDebugSettings.overrides != null &&
                Config.trickAnimationDebugSettings.overrides.TryGetValue(dataKey, out TrickAnimationOverride existing) &&
                existing != null)
            {
                if (existing.poseOverrides != null)
                    values.poseOverrides = ClonePoseOverrides(existing.poseOverrides);
                if (existing.ikTargetOverrides != null)
                    values.ikTargetOverrides = CloneIkTargetOverrides(existing.ikTargetOverrides);
            }

            return values;
        }

        private static void DrawClipCopyTools(SyncTrickAnimationData target)
        {
            Menu.BeginAltPane("Quick Animation Picker", "Pick a source trick, then replace this trick's human animation, bike animation, or both.");
            showClipCopyTools = GUILayout.Toggle(
                showClipCopyTools,
                showClipCopyTools ? "Hide Picker" : "Show Picker",
                showClipCopyTools ? Menu.UiPillActiveStyle : Menu.UiPillStyle,
                GUILayout.Width(110f),
                GUILayout.Height(24f));
            if (!showClipCopyTools)
            {
                Menu.EndPane();
                return;
            }

            EnsureClipSourceCatalog(false);

            Menu.BeginToolbar();
            Menu.SearchRow(ref clipSourceSearch, 220f, "Search");
            if (Menu.SecondaryButton("Refresh", GUILayout.Width(80f), GUILayout.Height(24f)))
                EnsureClipSourceCatalog(true);
            Menu.EndToolbar();

            if (clipSources.Count == 0)
            {
                GUILayout.Label("No loaded SyncTrickAnimationData sources found yet.", mutedStyle);
                Menu.EndPane();
                return;
            }

            string search = (clipSourceSearch ?? string.Empty).Trim();
            int shown = 0;
            for (int i = 0; i < clipSources.Count && shown < 12; i++)
            {
                SyncTrickAnimationData source = clipSources[i];
                if (source == null)
                    continue;

                string sourceName = TrickName(source);
                if (search.Length > 0 && sourceName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                shown++;
                string prefix = i == selectedClipSourceIndex ? "> " : string.Empty;
                if (GUILayout.Button(prefix + sourceName, i == selectedClipSourceIndex ? Menu.UiRowButtonSelectedStyle : rowButtonStyle))
                    selectedClipSourceIndex = i;
            }

            if (shown == 0)
                GUILayout.Label(search.Length > 0 ? "No source tricks match the search." : "No source tricks available.", mutedStyle);
            else if (shown >= 12)
                GUILayout.Label("Showing first 12 matches. Search to narrow the replacement source.", mutedStyle);

            SyncTrickAnimationData selectedSource = GetSelectedClipSource();
            if (selectedSource == null)
            {
                GUILayout.Label("Select a source trick to copy clips from.", mutedStyle);
                Menu.EndPane();
                return;
            }

            GUILayout.Label($"Source: {TrickName(selectedSource)}", mutedStyle);
            Menu.BeginToolbar();
            if (Menu.PrimaryButton("Use Human Animation", GUILayout.Width(160f), GUILayout.Height(24f)))
                CopyClipsFromSource(selectedSource, target, true, false);
            if (Menu.PrimaryButton("Use Bike Animation", GUILayout.Width(155f), GUILayout.Height(24f)))
                CopyClipsFromSource(selectedSource, target, false, true);
            if (Menu.PrimaryButton("Use Both", GUILayout.Width(95f), GUILayout.Height(24f)))
                CopyClipsFromSource(selectedSource, target, true, true);
            Menu.EndToolbar();
            GUILayout.Label("After replacing clips, use Save Override above to persist it.", mutedStyle);
            Menu.EndPane();
        }

        private static void DrawPoseOverlayTools(SyncTrickAnimationData target, string dataKey)
        {
            showPoseOverlayTools = GUILayout.Toggle(
                showPoseOverlayTools,
                showPoseOverlayTools ? "v Pose Overlay / Bone Edits" : "> Pose Overlay / Bone Edits",
                headerStyle);

            if (!showPoseOverlayTools)
                return;

            GUILayout.Label(
                "Runtime bone offsets are applied after the game's Animancer pose. Use this for visible per-trick edits when imported clip curves are not runtime-readable.",
                mutedStyle);

            TrickAnimationOverride values = GetOrCreateAnimationOverride(target, dataKey);
            values.poseOverrides ??= new List<TrickPoseOverride>();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Bone Edit", smallButtonStyle, GUILayout.Width(125)))
            {
                values.poseOverrides.Add(new TrickPoseOverride());
                suppressAutoApplyUntil = Time.unscaledTime + 5f;
                status = $"Added pose edit for {TrickName(target)}. Adjust it live, then save.";
            }

            if (GUILayout.Button("Save Pose Edits", smallButtonStyle, GUILayout.Width(130)))
            {
                Config.Save();
                status = $"Saved pose edits for {TrickName(target)}.";
                Log.Msg($"[TrickAnimEditor] Saved pose edits for {TrickName(target)} ({dataKey}).");
            }

            if (GUILayout.Button("Clear Pose Edits", smallButtonStyle, GUILayout.Width(135)))
            {
                values.poseOverrides.Clear();
                Config.Save();
                RestorePoseOverlays();
                status = $"Cleared pose edits for {TrickName(target)}.";
            }

            GUILayout.EndHorizontal();

            if (values.poseOverrides.Count == 0)
            {
                GUILayout.Label("No pose edits yet. Add one, choose a bone, then throw the trick to see it live.", mutedStyle);
                DrawIkSnapshot();
                return;
            }

            poseScroll = GUILayout.BeginScrollView(poseScroll, GUILayout.Height(320));
            for (int i = 0; i < values.poseOverrides.Count; i++)
            {
                TrickPoseOverride pose = values.poseOverrides[i];
                if (pose == null)
                {
                    pose = new TrickPoseOverride();
                    values.poseOverrides[i] = pose;
                }

                GUILayout.BeginVertical(panelAltStyle);
                GUILayout.BeginHorizontal();
                pose.enabled = GUILayout.Toggle(pose.enabled, $"Edit {i + 1}", GUILayout.Width(90));
                GUILayout.Label($"Phase: {NormalizePhaseName(pose.phase)}", mutedStyle, GUILayout.Width(115));
                if (GUILayout.Button("<", smallButtonStyle, GUILayout.Width(28)))
                    pose.phase = StepOption(NormalizePhaseName(pose.phase), PhaseOptions, -1);
                if (GUILayout.Button(">", smallButtonStyle, GUILayout.Width(28)))
                    pose.phase = StepOption(NormalizePhaseName(pose.phase), PhaseOptions, 1);

                GUILayout.Label($"Bone: {NormalizeBoneName(pose.bone)}", mutedStyle, GUILayout.Width(170));
                if (GUILayout.Button("<", smallButtonStyle, GUILayout.Width(28)))
                    pose.bone = StepBone(NormalizeBoneName(pose.bone), -1);
                if (GUILayout.Button(">", smallButtonStyle, GUILayout.Width(28)))
                    pose.bone = StepBone(NormalizeBoneName(pose.bone), 1);
                if (GUILayout.Button("Remove", smallButtonStyle, GUILayout.Width(78)))
                {
                    values.poseOverrides.RemoveAt(i);
                    i--;
                    suppressAutoApplyUntil = Time.unscaledTime + 5f;
                    status = $"Removed pose edit for {TrickName(target)}.";
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    continue;
                }

                GUILayout.EndHorizontal();

                bool changed = false;
                changed |= DrawFloatSlider("Weight", pose.weight, 0f, 1f, v => pose.weight = v);
                changed |= DrawVector3Slider(
                    "Rotation XYZ",
                    pose.localRotationEuler,
                    -180f,
                    180f,
                    v => pose.localRotationEuler = v);
                changed |= DrawVector3Slider(
                    "Position XYZ",
                    pose.localPositionOffset,
                    -0.5f,
                    0.5f,
                    v => pose.localPositionOffset = v);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Zero Rotation", smallButtonStyle, GUILayout.Width(115)))
                {
                    pose.localRotationEuler = new SerializableVector3(0f, 0f, 0f);
                    changed = true;
                }

                if (GUILayout.Button("Zero Position", smallButtonStyle, GUILayout.Width(115)))
                {
                    pose.localPositionOffset = new SerializableVector3(0f, 0f, 0f);
                    changed = true;
                }

                GUILayout.EndHorizontal();

                if (changed)
                {
                    suppressAutoApplyUntil = Time.unscaledTime + 5f;
                    status = $"Editing pose overlay for {TrickName(target)}. Throw or hold the trick to preview it.";
                }

                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
            DrawIkSnapshot();
        }

        private static void DrawIkTargetTools(SyncTrickAnimationData target, string dataKey)
        {
            showIkTargetTools = GUILayout.Toggle(
                showIkTargetTools,
                showIkTargetTools ? "v IK Target Edits" : "> IK Target Edits",
                headerStyle);

            if (!showIkTargetTools)
                return;

            GUILayout.Label(
                "Moves the game's existing IK target transforms while this trick phase is active. This is separate from bone edits and lets the IK solver pull hands or feet toward edited targets.",
                mutedStyle);

            TrickAnimationOverride values = GetOrCreateAnimationOverride(target, dataKey);
            values.ikTargetOverrides ??= new List<TrickIkTargetOverride>();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add IK Edit", smallButtonStyle, GUILayout.Width(125)))
            {
                values.ikTargetOverrides.Add(new TrickIkTargetOverride());
                suppressAutoApplyUntil = Time.unscaledTime + 5f;
                status = $"Added IK target edit for {TrickName(target)}. Throw the trick to preview it live.";
            }

            if (GUILayout.Button("Save IK Edits", smallButtonStyle, GUILayout.Width(130)))
            {
                Config.Save();
                status = $"Saved IK target edits for {TrickName(target)}.";
                Log.Msg($"[TrickAnimEditor] Saved IK target edits for {TrickName(target)} ({dataKey}).");
            }

            if (GUILayout.Button("Clear IK Edits", smallButtonStyle, GUILayout.Width(135)))
            {
                values.ikTargetOverrides.Clear();
                Config.Save();
                RestoreIkTargetOverlays();
                status = $"Cleared IK target edits for {TrickName(target)}.";
            }

            GUILayout.EndHorizontal();

            if (values.ikTargetOverrides.Count == 0)
            {
                GUILayout.Label("No IK target edits yet. Add one, choose a hand or foot goal, then throw the trick to see it live.", mutedStyle);
                DrawIkSnapshot();
                DrawIkTargetCatalog();
                return;
            }

            ikTargetScroll = GUILayout.BeginScrollView(ikTargetScroll, GUILayout.Height(360));
            for (int i = 0; i < values.ikTargetOverrides.Count; i++)
            {
                TrickIkTargetOverride edit = values.ikTargetOverrides[i];
                if (edit == null)
                {
                    edit = new TrickIkTargetOverride();
                    values.ikTargetOverrides[i] = edit;
                }

                NormalizeIkTargetOverride(edit);

                GUILayout.BeginVertical(panelAltStyle);
                GUILayout.BeginHorizontal();
                edit.enabled = GUILayout.Toggle(edit.enabled, $"IK {i + 1}", GUILayout.Width(70));
                GUILayout.Label($"Phase: {NormalizePhaseName(edit.phase)}", mutedStyle, GUILayout.Width(115));
                if (GUILayout.Button("<", smallButtonStyle, GUILayout.Width(28)))
                    edit.phase = StepOption(NormalizePhaseName(edit.phase), PhaseOptions, -1);
                if (GUILayout.Button(">", smallButtonStyle, GUILayout.Width(28)))
                    edit.phase = StepOption(NormalizePhaseName(edit.phase), PhaseOptions, 1);

                GUILayout.Label($"Goal: {NormalizeIkGoalName(edit.goal)}", mutedStyle, GUILayout.Width(125));
                if (GUILayout.Button("<", smallButtonStyle, GUILayout.Width(28)))
                    edit.goal = StepOption(NormalizeIkGoalName(edit.goal), IkGoalOptions, -1);
                if (GUILayout.Button(">", smallButtonStyle, GUILayout.Width(28)))
                    edit.goal = StepOption(NormalizeIkGoalName(edit.goal), IkGoalOptions, 1);
                if (GUILayout.Button("Remove", smallButtonStyle, GUILayout.Width(78)))
                {
                    values.ikTargetOverrides.RemoveAt(i);
                    i--;
                    suppressAutoApplyUntil = Time.unscaledTime + 5f;
                    status = $"Removed IK target edit for {TrickName(target)}.";
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    continue;
                }

                GUILayout.EndHorizontal();

                bool changed = false;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Target ID: {(edit.targetId < 0 ? "Auto" : edit.targetId.ToString())}", mutedStyle, GUILayout.Width(125));
                if (GUILayout.Button("-", smallButtonStyle, GUILayout.Width(28)))
                {
                    edit.targetId = Mathf.Max(-1, edit.targetId - 1);
                    changed = true;
                }
                if (GUILayout.Button("+", smallButtonStyle, GUILayout.Width(28)))
                {
                    edit.targetId++;
                    changed = true;
                }
                if (GUILayout.Button("Auto", smallButtonStyle, GUILayout.Width(58)))
                {
                    edit.targetId = -1;
                    changed = true;
                }

                bool nextActivate = GUILayout.Toggle(edit.activateTarget, "Activate Target", GUILayout.Width(140));
                if (nextActivate != edit.activateTarget)
                {
                    edit.activateTarget = nextActivate;
                    changed = true;
                }

                GUILayout.EndHorizontal();

                IKTarget resolved = ResolveIkTarget(edit);
                if (resolved != null)
                {
                    string resolvedName = SafeRead(() => resolved.name, "(unnamed)");
                    int resolvedId = SafeRead(() => resolved.TargetID, -1);
                    bool active = SafeRead(() => resolved.Active, false);
                    float refWeight = SafeRead(() => resolved.ReferenceLimbPositionWeight, 0f);
                    GUILayout.Label($"Resolved: {resolvedName}, TargetID={resolvedId}, Active={active}, RefLimbPosWeight={refWeight:0.###}", mutedStyle);
                }
                else
                {
                    GUILayout.Label("Resolved: no matching IKTarget loaded yet. Throw the trick, then reopen this panel if needed.", mutedStyle);
                }

                changed |= DrawFloatSlider("Offset Weight", edit.offsetWeight, 0f, 1f, v => edit.offsetWeight = v);
                changed |= DrawFloatSlider("Human IK Weight", edit.humanIkWeight, 0f, 1f, v => edit.humanIkWeight = v);
                changed |= DrawFloatSlider("Limb Pos Weight", edit.limbPositionWeight, 0f, 1f, v => edit.limbPositionWeight = v);
                changed |= DrawFloatSlider("Limb Rot Weight", edit.limbRotationWeight, 0f, 1f, v => edit.limbRotationWeight = v);
                changed |= DrawVector3Slider(
                    "Target Local Position",
                    edit.localPositionOffset,
                    -0.5f,
                    0.5f,
                    v => edit.localPositionOffset = v);
                changed |= DrawVector3Slider(
                    "Target Local Rotation",
                    edit.localRotationEuler,
                    -180f,
                    180f,
                    v => edit.localRotationEuler = v);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Zero Position", smallButtonStyle, GUILayout.Width(115)))
                {
                    edit.localPositionOffset = new SerializableVector3(0f, 0f, 0f);
                    changed = true;
                }

                if (GUILayout.Button("Zero Rotation", smallButtonStyle, GUILayout.Width(115)))
                {
                    edit.localRotationEuler = new SerializableVector3(0f, 0f, 0f);
                    changed = true;
                }

                GUILayout.EndHorizontal();

                if (changed)
                {
                    NormalizeIkTargetOverride(edit);
                    suppressAutoApplyUntil = Time.unscaledTime + 5f;
                    status = $"Editing IK target for {TrickName(target)}. Throw or hold the trick to preview it.";
                }

                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
            DrawIkSnapshot();
            DrawIkTargetCatalog();
        }

        private static void DrawIkTargetCatalog()
        {
            IKTarget[] targets = SafeRead(() => UnityEngine.Object.FindObjectsOfType<IKTarget>(), null);
            if (targets == null || targets.Length == 0)
                return;

            GUILayout.Label("Runtime IK targets:", mutedStyle);
            int max = Mathf.Min(targets.Length, 12);
            for (int i = 0; i < max; i++)
            {
                IKTarget target = targets[i];
                if (target == null)
                    continue;

                string name = SafeRead(() => target.name, "(unnamed)");
                AvatarIKGoal goal = SafeRead(() => target.GoalType, AvatarIKGoal.LeftHand);
                int id = SafeRead(() => target.TargetID, -1);
                bool active = SafeRead(() => target.Active, false);
                GUILayout.Label($"  {i}: {name}, Goal={goal}, TargetID={id}, Active={active}", mutedStyle);
            }

            if (targets.Length > max)
                GUILayout.Label($"  ... {targets.Length - max} more", mutedStyle);
        }

        private static void DrawIkSnapshot()
        {
            int humanIkCount = SafeRead(() => UnityEngine.Object.FindObjectsOfType<HumanIK>()?.Length ?? 0, 0);
            int targetCount = SafeRead(() => UnityEngine.Object.FindObjectsOfType<IKTarget>()?.Length ?? 0, 0);
            int twoBoneCount = SafeRead(() => UnityEngine.Object.FindObjectsOfType<Il2CppMashBox.Core.Runtime.InverseKinematics.TwoBoneIK>()?.Length ?? 0, 0);
            GUILayout.Label($"IK present: HumanIK={humanIkCount}, IKTarget={targetCount}, TwoBoneIK={twoBoneCount}", mutedStyle);
        }

        private static TrickAnimationOverride GetOrCreateAnimationOverride(SyncTrickAnimationData data, string dataKey)
        {
            EnsureSettings();
            if (Config.trickAnimationDebugSettings.overrides.TryGetValue(dataKey, out TrickAnimationOverride values) && values != null)
            {
                values.poseOverrides ??= new List<TrickPoseOverride>();
                values.ikTargetOverrides ??= new List<TrickIkTargetOverride>();
                return values;
            }

            values = CaptureOverride(data, dataKey);
            values.poseOverrides ??= new List<TrickPoseOverride>();
            values.ikTargetOverrides ??= new List<TrickIkTargetOverride>();
            Config.trickAnimationDebugSettings.overrides[dataKey] = values;
            return values;
        }

        private static List<TrickPoseOverride> ClonePoseOverrides(List<TrickPoseOverride> source)
        {
            List<TrickPoseOverride> clone = new List<TrickPoseOverride>();
            if (source == null)
                return clone;

            for (int i = 0; i < source.Count; i++)
            {
                TrickPoseOverride pose = source[i];
                if (pose == null)
                    continue;

                clone.Add(new TrickPoseOverride
                {
                    enabled = pose.enabled,
                    phase = NormalizePhaseName(pose.phase),
                    bone = NormalizeBoneName(pose.bone),
                    localRotationEuler = pose.localRotationEuler,
                    localPositionOffset = pose.localPositionOffset,
                    weight = Mathf.Clamp01(pose.weight)
                });
            }

            return clone;
        }

        private static List<TrickIkTargetOverride> CloneIkTargetOverrides(List<TrickIkTargetOverride> source)
        {
            List<TrickIkTargetOverride> clone = new List<TrickIkTargetOverride>();
            if (source == null)
                return clone;

            for (int i = 0; i < source.Count; i++)
            {
                TrickIkTargetOverride edit = source[i];
                if (edit == null)
                    continue;

                NormalizeIkTargetOverride(edit);
                clone.Add(new TrickIkTargetOverride
                {
                    enabled = edit.enabled,
                    phase = NormalizePhaseName(edit.phase),
                    goal = NormalizeIkGoalName(edit.goal),
                    targetId = edit.targetId,
                    activateTarget = edit.activateTarget,
                    localPositionOffset = edit.localPositionOffset,
                    localRotationEuler = edit.localRotationEuler,
                    offsetWeight = Clamp01(edit.offsetWeight),
                    humanIkWeight = Clamp01(edit.humanIkWeight),
                    limbPositionWeight = Clamp01(edit.limbPositionWeight),
                    limbRotationWeight = Clamp01(edit.limbRotationWeight)
                });
            }

            return clone;
        }

        private static void CopyClipsFromSource(SyncTrickAnimationData source, SyncTrickAnimationData target, bool player, bool vehicle)
        {
            if (source == null || target == null)
                return;

            if (source == target)
            {
                status = "Pick a different source trick before replacing animations.";
                return;
            }

            bool copied = false;
            if (player)
                copied |= CopyAnimationData(SafeRead(() => source._playerAnimationData, null), SafeRead(() => target._playerAnimationData, null));
            if (vehicle)
                copied |= CopyAnimationData(SafeRead(() => source._vehicleAnimationData, null), SafeRead(() => target._vehicleAnimationData, null));

            if (!copied)
            {
                status = "Clip copy failed because source or target animation data was unavailable.";
                return;
            }

            suppressAutoApplyUntil = Time.unscaledTime + 5f;
            string label = player && vehicle ? "human+bike" : player ? "human" : "bike";
            status = $"Copied {label} animation from {TrickName(source)} to {TrickName(target)}. Save Override to persist.";
            Log.Msg($"[TrickAnimEditor] Copied {label} clips from {TrickName(source)} to {TrickName(target)}.");
        }

        private static bool CopyAnimationData(TrickAnimationData source, TrickAnimationData target)
        {
            if (source == null || target == null)
                return false;

            target._enterAnimationClip = source._enterAnimationClip;
            target._loopAnimationClip = source._loopAnimationClip;
            target._tweakAnimationClip = source._tweakAnimationClip;
            target._exitAnimationClip = source._exitAnimationClip;
            target._enterAnimationClipMirror = source._enterAnimationClipMirror;
            target._loopAnimationClipMirror = source._loopAnimationClipMirror;
            target._tweakAnimationClipMirror = source._tweakAnimationClipMirror;
            target._exitAnimationClipMirror = source._exitAnimationClipMirror;
            target._overallSpeedMult = source._overallSpeedMult;
            return true;
        }

        private static void ApplyClipOverride(SyncTrickAnimationData data, TrickAnimationOverride values)
        {
            ApplyClipNames(
                SafeRead(() => data._playerAnimationData, null),
                values.playerEnterClip,
                values.playerLoopClip,
                values.playerTweakClip,
                values.playerExitClip,
                values.playerMirrorEnterClip,
                values.playerMirrorLoopClip,
                values.playerMirrorTweakClip,
                values.playerMirrorExitClip);

            ApplyClipNames(
                SafeRead(() => data._vehicleAnimationData, null),
                values.vehicleEnterClip,
                values.vehicleLoopClip,
                values.vehicleTweakClip,
                values.vehicleExitClip,
                values.vehicleMirrorEnterClip,
                values.vehicleMirrorLoopClip,
                values.vehicleMirrorTweakClip,
                values.vehicleMirrorExitClip);
        }

        private static void ApplyClipNames(
            TrickAnimationData animationData,
            string enter,
            string loop,
            string tweak,
            string exit,
            string mirrorEnter,
            string mirrorLoop,
            string mirrorTweak,
            string mirrorExit)
        {
            if (animationData == null)
                return;

            BuildClipLookupIfNeeded();

            AnimationClip clip;
            if (TryFindClip(enter, out clip)) animationData._enterAnimationClip = clip;
            if (TryFindClip(loop, out clip)) animationData._loopAnimationClip = clip;
            if (TryFindClip(tweak, out clip)) animationData._tweakAnimationClip = clip;
            if (TryFindClip(exit, out clip)) animationData._exitAnimationClip = clip;
            if (TryFindClip(mirrorEnter, out clip)) animationData._enterAnimationClipMirror = clip;
            if (TryFindClip(mirrorLoop, out clip)) animationData._loopAnimationClipMirror = clip;
            if (TryFindClip(mirrorTweak, out clip)) animationData._tweakAnimationClipMirror = clip;
            if (TryFindClip(mirrorExit, out clip)) animationData._exitAnimationClipMirror = clip;
        }

        private static bool TryFindClip(string clipName, out AnimationClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(clipName))
                return false;

            BuildClipLookupIfNeeded();
            return clipLookup.TryGetValue(clipName, out clip) && clip != null;
        }

        private static void BuildClipLookupIfNeeded()
        {
            if (clipLookup.Count > 0)
                return;

            AnimationClip[] clips = Resources.FindObjectsOfTypeAll<AnimationClip>();
            if (clips == null)
                return;

            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                string name = SafeRead(() => clip.name, null);
                if (!string.IsNullOrEmpty(name) && !clipLookup.ContainsKey(name))
                    clipLookup[name] = clip;
            }
        }

        private static void EnsureClipSourceCatalog(bool force)
        {
            if (!force && Time.unscaledTime < nextClipCatalogRefreshTime && clipSources.Count > 0)
                return;

            nextClipCatalogRefreshTime = Time.unscaledTime + 5f;
            clipSources.Clear();

            SyncTrickAnimationData[] allData = Resources.FindObjectsOfTypeAll<SyncTrickAnimationData>();
            if (allData == null)
                return;

            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < allData.Length; i++)
            {
                SyncTrickAnimationData data = allData[i];
                if (data == null)
                    continue;

                if (!TrickMods.IsBmxTrickObject(data))
                    continue;

                string key = GetDataKey(data);
                if (string.IsNullOrEmpty(key) || seen.Contains(key))
                    continue;

                seen.Add(key);
                clipSources.Add(data);
            }

            clipSources.Sort((a, b) => string.Compare(TrickName(a), TrickName(b), System.StringComparison.OrdinalIgnoreCase));
            if (selectedClipSourceIndex >= clipSources.Count)
                selectedClipSourceIndex = clipSources.Count - 1;
        }

        private static SyncTrickAnimationData GetSelectedClipSource()
        {
            if (selectedClipSourceIndex < 0 || selectedClipSourceIndex >= clipSources.Count)
                return null;

            return clipSources[selectedClipSourceIndex];
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

                return set._dataList[(System.Index)slotId] as SyncTrickAnimationData;
            }
            catch
            {
                return null;
            }
        }

        private static bool DrawFloatSlider(string label, float value, float min, float max, System.Action<float> apply)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.###}", GUILayout.Width(170));
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(260));
            GUILayout.Label($"{min:0.##}", mutedStyle, GUILayout.Width(34));
            GUILayout.Label($"{max:0.##}", mutedStyle, GUILayout.Width(34));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(next - value) <= 0.001f)
                return false;

            apply(next);
            return true;
        }

        private static bool DrawVector3Slider(string label, SerializableVector3 value, float min, float max, System.Action<SerializableVector3> apply)
        {
            bool changed = false;
            float x = value.x;
            float y = value.y;
            float z = value.z;

            GUILayout.Label($"{label}: X={x:0.###}, Y={y:0.###}, Z={z:0.###}", mutedStyle);
            changed |= DrawAxisSlider("X", ref x, min, max);
            changed |= DrawAxisSlider("Y", ref y, min, max);
            changed |= DrawAxisSlider("Z", ref z, min, max);

            if (!changed)
                return false;

            apply(new SerializableVector3(x, y, z));
            return true;
        }

        private static bool DrawAxisSlider(string label, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(24));
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(260));
            GUILayout.Label($"{next:0.###}", mutedStyle, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(next - value) <= 0.001f)
                return false;

            value = next;
            return true;
        }

        private static string StepOption(string current, string[] options, int delta)
        {
            if (options == null || options.Length == 0)
                return current;

            int index = System.Array.IndexOf(options, current);
            if (index < 0)
                index = 0;

            index = (index + delta) % options.Length;
            if (index < 0)
                index += options.Length;

            return options[index];
        }

        private static string StepBone(string current, int delta)
        {
            int index = 0;
            for (int i = 0; i < EditableBones.Length; i++)
            {
                if (EditableBones[i].ToString() == current)
                {
                    index = i;
                    break;
                }
            }

            index = (index + delta) % EditableBones.Length;
            if (index < 0)
                index += EditableBones.Length;

            return EditableBones[index].ToString();
        }

        private static string NormalizePhaseName(string phaseName)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                return "Any";

            for (int i = 0; i < PhaseOptions.Length; i++)
            {
                if (string.Equals(phaseName, PhaseOptions[i], System.StringComparison.OrdinalIgnoreCase))
                    return PhaseOptions[i];
            }

            return phaseName.IndexOf("enter", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Enter" :
                phaseName.IndexOf("tweak", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Tweak" :
                phaseName.IndexOf("loop", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Loop" :
                phaseName.IndexOf("exit", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Exit" :
                "Any";
        }

        private static string NormalizeBoneName(string boneName)
        {
            if (string.IsNullOrWhiteSpace(boneName))
                return HumanBodyBones.Hips.ToString();

            for (int i = 0; i < EditableBones.Length; i++)
            {
                if (string.Equals(boneName, EditableBones[i].ToString(), System.StringComparison.OrdinalIgnoreCase))
                    return EditableBones[i].ToString();
            }

            return HumanBodyBones.Hips.ToString();
        }

        private static bool TryParseBone(string boneName, out HumanBodyBones bone)
        {
            bone = HumanBodyBones.Hips;
            string normalized = NormalizeBoneName(boneName);
            try
            {
                bone = (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), normalized);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void NormalizeIkTargetOverride(TrickIkTargetOverride edit)
        {
            if (edit == null)
                return;

            edit.phase = NormalizePhaseName(edit.phase);
            edit.goal = NormalizeIkGoalName(edit.goal);
            if (edit.targetId < -1)
                edit.targetId = -1;

            edit.offsetWeight = Clamp01(edit.offsetWeight);
            edit.humanIkWeight = Clamp01(edit.humanIkWeight);
            edit.limbPositionWeight = Clamp01(edit.limbPositionWeight);
            edit.limbRotationWeight = Clamp01(edit.limbRotationWeight);
        }

        private static string NormalizeIkGoalName(string goalName)
        {
            if (string.IsNullOrWhiteSpace(goalName))
                return "LeftHand";

            for (int i = 0; i < IkGoalOptions.Length; i++)
            {
                if (string.Equals(goalName, IkGoalOptions[i], System.StringComparison.OrdinalIgnoreCase))
                    return IkGoalOptions[i];
            }

            string lower = goalName.ToLowerInvariant();
            if (lower.Contains("right") && lower.Contains("hand"))
                return "RightHand";
            if (lower.Contains("left") && lower.Contains("foot"))
                return "LeftFoot";
            if (lower.Contains("right") && lower.Contains("foot"))
                return "RightFoot";

            return "LeftHand";
        }

        private static bool TryParseIkGoal(string goalName, out AvatarIKGoal goal)
        {
            goal = AvatarIKGoal.LeftHand;
            string normalized = NormalizeIkGoalName(goalName);
            try
            {
                goal = (AvatarIKGoal)System.Enum.Parse(typeof(AvatarIKGoal), normalized);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IKTarget ResolveIkTarget(TrickIkTargetOverride edit)
        {
            if (edit == null || !TryParseIkGoal(edit.goal, out AvatarIKGoal goal))
                return null;

            IKTarget[] targets = SafeRead(() => UnityEngine.Object.FindObjectsOfType<IKTarget>(), null);
            if (targets == null || targets.Length == 0)
                return null;

            IKTarget fallback = null;
            IKTarget active = null;
            for (int i = 0; i < targets.Length; i++)
            {
                IKTarget target = targets[i];
                if (target == null)
                    continue;

                AvatarIKGoal targetGoal = SafeRead(() => target.GoalType, AvatarIKGoal.LeftHand);
                if (targetGoal != goal)
                    continue;

                int targetId = SafeRead(() => target.TargetID, -9999);
                if (edit.targetId >= 0 && targetId != edit.targetId)
                    continue;

                if (fallback == null)
                    fallback = target;
                if (SafeRead(() => target.Active, false))
                {
                    active = target;
                    break;
                }
            }

            return active ?? fallback;
        }

        private static void ApplyIkTargetOverlays()
        {
            ikTargetTouchedThisFrame.Clear();
            humanIkTouchedThisFrame.Clear();
            limbTouchedThisFrame.Clear();

            if (activeAnimator == null ||
                activePoseData == null ||
                string.IsNullOrEmpty(activePoseDataKey) ||
                Time.unscaledTime > activePoseUntil)
            {
                RestoreInactiveIkTargetStates();
                RestoreInactiveHumanIkWeightStates();
                RestoreInactiveLimbWeightStates();
                return;
            }

            if (!Config.trickAnimationDebugSettings.overrides.TryGetValue(activePoseDataKey, out TrickAnimationOverride values) ||
                values?.ikTargetOverrides == null ||
                values.ikTargetOverrides.Count == 0)
            {
                RestoreInactiveIkTargetStates();
                RestoreInactiveHumanIkWeightStates();
                RestoreInactiveLimbWeightStates();
                return;
            }

            bool playing = SafeRead(() => activeAnimator.character != null && activeAnimator.character.IsPlayingTrickAnimation(), true);
            if (!playing)
            {
                RestoreInactiveIkTargetStates();
                RestoreInactiveHumanIkWeightStates();
                RestoreInactiveLimbWeightStates();
                return;
            }

            int appliedCount = 0;
            for (int i = 0; i < values.ikTargetOverrides.Count; i++)
            {
                TrickIkTargetOverride edit = values.ikTargetOverrides[i];
                if (edit == null || !edit.enabled || edit.offsetWeight <= 0f)
                    continue;

                NormalizeIkTargetOverride(edit);
                if (!PhaseMatches(edit.phase, activePhase))
                    continue;

                IKTarget target = ResolveIkTarget(edit);
                if (target == null)
                    continue;

                ApplyIkTargetToTransform(target, edit);
                ApplyIkWeights(target, edit);
                appliedCount++;
            }

            if (appliedCount > 0)
            {
                string applyLogKey = $"{activePoseDataKey}|{activePhase}|{appliedCount}|{Time.frameCount / 120}";
                if (applyLogKey != lastAppliedIkLogKey)
                {
                    lastAppliedIkLogKey = applyLogKey;
                    Log.Msg($"[TrickAnimEditor] Applied {appliedCount} IK target edit(s) to {TrickName(activePoseData)} during {activePhase}.");
                }
            }

            RestoreInactiveIkTargetStates();
            RestoreInactiveHumanIkWeightStates();
            RestoreInactiveLimbWeightStates();
        }

        private static void ApplyIkTargetToTransform(IKTarget target, TrickIkTargetOverride edit)
        {
            if (target == null)
                return;

            Transform transform = SafeRead(() => target.transform, null);
            if (transform == null)
                return;

            int id = target.GetInstanceID();
            if (!appliedIkTargetStates.TryGetValue(id, out AppliedIkTargetState state) || state == null)
            {
                state = new AppliedIkTargetState
                {
                    Target = target,
                    Transform = transform,
                    BaseActive = SafeRead(() => target.Active, false)
                };
                appliedIkTargetStates[id] = state;
            }

            Quaternion currentRotation = transform.localRotation;
            Vector3 currentPosition = transform.localPosition;

            Quaternion baseRotation = Quaternion.Angle(currentRotation, state.AppliedRotation) < 0.1f
                ? state.BaseRotation
                : currentRotation;
            Vector3 basePosition = Vector3.Distance(currentPosition, state.AppliedPosition) < 0.0005f
                ? state.BasePosition
                : currentPosition;

            bool currentActive = SafeRead(() => target.Active, false);
            bool baseActive = currentActive == state.AppliedActive ? state.BaseActive : currentActive;

            float weight = Clamp01(edit.offsetWeight);
            Quaternion appliedRotation = baseRotation * Quaternion.Euler(ToVector3(edit.localRotationEuler) * weight);
            Vector3 appliedPosition = basePosition + ToVector3(edit.localPositionOffset) * weight;
            bool appliedActive = edit.activateTarget || baseActive;

            transform.localRotation = appliedRotation;
            transform.localPosition = appliedPosition;
            target.Active = appliedActive;

            state.Target = target;
            state.Transform = transform;
            state.BaseRotation = baseRotation;
            state.BasePosition = basePosition;
            state.AppliedRotation = appliedRotation;
            state.AppliedPosition = appliedPosition;
            state.BaseActive = baseActive;
            state.AppliedActive = appliedActive;

            ikTargetTouchedThisFrame.Add(id);
        }

        private static void ApplyIkWeights(IKTarget target, TrickIkTargetOverride edit)
        {
            if (target == null || !TryParseIkGoal(edit.goal, out AvatarIKGoal goal))
                return;

            HumanIK[] humanIks = SafeRead(() => UnityEngine.Object.FindObjectsOfType<HumanIK>(), null);
            if (humanIks != null)
            {
                for (int i = 0; i < humanIks.Length; i++)
                {
                    HumanIK humanIk = humanIks[i];
                    if (humanIk != null)
                        ApplyHumanIkWeight(humanIk, goal, edit.humanIkWeight);
                }
            }

            UnityIKLimb limb = SafeRead(() => target.ReferenceLimbIK, null);
            if (limb != null)
                ApplyLimbWeights(limb, edit);
        }

        private static void ApplyHumanIkWeight(HumanIK humanIk, AvatarIKGoal goal, float weight)
        {
            if (humanIk == null)
                return;

            string key = $"{humanIk.GetInstanceID()}|{goal}";
            float current = ReadHumanIkWeight(humanIk, goal);
            if (!appliedHumanIkWeightStates.TryGetValue(key, out AppliedHumanIkWeightState state) || state == null)
            {
                state = new AppliedHumanIkWeightState
                {
                    HumanIK = humanIk,
                    Goal = goal,
                    BaseWeight = current
                };
                appliedHumanIkWeightStates[key] = state;
            }

            float baseWeight = Mathf.Abs(current - state.AppliedWeight) < 0.001f ? state.BaseWeight : current;
            float appliedWeight = Clamp01(weight);
            SetHumanIkWeight(humanIk, goal, appliedWeight);

            state.HumanIK = humanIk;
            state.Goal = goal;
            state.BaseWeight = baseWeight;
            state.AppliedWeight = appliedWeight;

            humanIkTouchedThisFrame.Add(key);
        }

        private static float ReadHumanIkWeight(HumanIK humanIk, AvatarIKGoal goal)
        {
            return goal switch
            {
                AvatarIKGoal.LeftHand => SafeRead(() => humanIk.LeftHandWeight, 0f),
                AvatarIKGoal.RightHand => SafeRead(() => humanIk.RightHandWeight, 0f),
                AvatarIKGoal.LeftFoot => SafeRead(() => humanIk.LeftFootWeight, 0f),
                AvatarIKGoal.RightFoot => SafeRead(() => humanIk.RightFootWeight, 0f),
                _ => 0f
            };
        }

        private static void SetHumanIkWeight(HumanIK humanIk, AvatarIKGoal goal, float weight)
        {
            switch (goal)
            {
                case AvatarIKGoal.LeftHand:
                    humanIk.SetLeftHandWeight(weight);
                    break;
                case AvatarIKGoal.RightHand:
                    humanIk.SetRightHandWeight(weight);
                    break;
                case AvatarIKGoal.LeftFoot:
                    humanIk.SetLeftFootWeight(weight);
                    break;
                case AvatarIKGoal.RightFoot:
                    humanIk.SetRightFootWeight(weight);
                    break;
            }
        }

        private static void ApplyLimbWeights(UnityIKLimb limb, TrickIkTargetOverride edit)
        {
            if (limb == null)
                return;

            int id = limb.GetInstanceID();
            float currentPosition = SafeRead(() => limb.PositionWeight, 0f);
            float currentRotation = SafeRead(() => limb.RotationWeight, 0f);
            if (!appliedLimbWeightStates.TryGetValue(id, out AppliedLimbWeightState state) || state == null)
            {
                state = new AppliedLimbWeightState
                {
                    Limb = limb,
                    BasePositionWeight = currentPosition,
                    BaseRotationWeight = currentRotation
                };
                appliedLimbWeightStates[id] = state;
            }

            float basePosition = Mathf.Abs(currentPosition - state.AppliedPositionWeight) < 0.001f
                ? state.BasePositionWeight
                : currentPosition;
            float baseRotation = Mathf.Abs(currentRotation - state.AppliedRotationWeight) < 0.001f
                ? state.BaseRotationWeight
                : currentRotation;
            float appliedPosition = Clamp01(edit.limbPositionWeight);
            float appliedRotation = Clamp01(edit.limbRotationWeight);

            limb.PositionWeight = appliedPosition;
            limb.RotationWeight = appliedRotation;

            state.Limb = limb;
            state.BasePositionWeight = basePosition;
            state.BaseRotationWeight = baseRotation;
            state.AppliedPositionWeight = appliedPosition;
            state.AppliedRotationWeight = appliedRotation;

            limbTouchedThisFrame.Add(id);
        }

        private static void ApplyPoseOverlays()
        {
            poseTouchedThisFrame.Clear();

            if (activeAnimator == null ||
                activePoseData == null ||
                string.IsNullOrEmpty(activePoseDataKey) ||
                Time.unscaledTime > activePoseUntil)
            {
                RestoreInactivePoseStates();
                return;
            }

            if (!Config.trickAnimationDebugSettings.overrides.TryGetValue(activePoseDataKey, out TrickAnimationOverride values) ||
                values?.poseOverrides == null ||
                values.poseOverrides.Count == 0)
            {
                RestoreInactivePoseStates();
                return;
            }

            bool playing = SafeRead(() => activeAnimator.character != null && activeAnimator.character.IsPlayingTrickAnimation(), true);
            if (!playing)
            {
                RestoreInactivePoseStates();
                return;
            }

            Animator animator = SafeRead(() => activeAnimator.Player == null ? null : activeAnimator.Player.Animator, null);
            if (animator == null)
            {
                RestoreInactivePoseStates();
                return;
            }

            int appliedCount = 0;
            for (int i = 0; i < values.poseOverrides.Count; i++)
            {
                TrickPoseOverride pose = values.poseOverrides[i];
                if (pose == null || !pose.enabled || pose.weight <= 0f)
                    continue;

                if (!PhaseMatches(pose.phase, activePhase))
                    continue;

                if (!TryParseBone(pose.bone, out HumanBodyBones bone))
                    continue;

                Transform target = SafeRead(() => animator.GetBoneTransform(bone), null);
                if (target == null)
                    continue;

                ApplyPoseToTransform(target, pose);
                appliedCount++;
            }

            if (appliedCount > 0)
            {
                string applyLogKey = $"{activePoseDataKey}|{activePhase}|{appliedCount}|{Time.frameCount / 120}";
                if (applyLogKey != lastAppliedPoseLogKey)
                {
                    lastAppliedPoseLogKey = applyLogKey;
                    Log.Msg($"[TrickAnimEditor] Applied {appliedCount} pose overlay(s) to {TrickName(activePoseData)} during {activePhase}.");
                }
            }

            RestoreInactivePoseStates();
        }

        private static bool PhaseMatches(string configuredPhase, string currentPhase)
        {
            string configured = NormalizePhaseName(configuredPhase);
            if (configured == "Any")
                return true;

            return string.Equals(configured, NormalizePhaseName(currentPhase), System.StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyPoseToTransform(Transform target, TrickPoseOverride pose)
        {
            int id = target.GetInstanceID();
            if (!appliedPoseStates.TryGetValue(id, out AppliedPoseState state) || state == null)
            {
                state = new AppliedPoseState { Transform = target };
                appliedPoseStates[id] = state;
            }

            Quaternion currentRotation = target.localRotation;
            Vector3 currentPosition = target.localPosition;

            Quaternion baseRotation = Quaternion.Angle(currentRotation, state.AppliedRotation) < 0.1f
                ? state.BaseRotation
                : currentRotation;
            Vector3 basePosition = Vector3.Distance(currentPosition, state.AppliedPosition) < 0.0005f
                ? state.BasePosition
                : currentPosition;

            float weight = Mathf.Clamp01(pose.weight);
            Vector3 rotationEuler = ToVector3(pose.localRotationEuler) * weight;
            Vector3 positionOffset = ToVector3(pose.localPositionOffset) * weight;

            Quaternion appliedRotation = baseRotation * Quaternion.Euler(rotationEuler);
            Vector3 appliedPosition = basePosition + positionOffset;

            target.localRotation = appliedRotation;
            target.localPosition = appliedPosition;

            state.Transform = target;
            state.BaseRotation = baseRotation;
            state.BasePosition = basePosition;
            state.AppliedRotation = appliedRotation;
            state.AppliedPosition = appliedPosition;
            state.Frame = Time.frameCount;

            poseTouchedThisFrame.Add(id);
        }

        private static void RestorePoseOverlays()
        {
            poseTouchedThisFrame.Clear();
            RestoreInactivePoseStates(forceAll: true);
        }

        private static void RestoreInactivePoseStates(bool forceAll = false)
        {
            if (appliedPoseStates.Count == 0)
                return;

            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, AppliedPoseState> pair in appliedPoseStates)
            {
                if (!forceAll && poseTouchedThisFrame.Contains(pair.Key))
                    continue;

                AppliedPoseState state = pair.Value;
                if (state?.Transform != null)
                {
                    Quaternion currentRotation = state.Transform.localRotation;
                    Vector3 currentPosition = state.Transform.localPosition;
                    if (Quaternion.Angle(currentRotation, state.AppliedRotation) < 0.1f)
                        state.Transform.localRotation = state.BaseRotation;
                    if (Vector3.Distance(currentPosition, state.AppliedPosition) < 0.0005f)
                        state.Transform.localPosition = state.BasePosition;
                }

                remove.Add(pair.Key);
            }

            for (int i = 0; i < remove.Count; i++)
                appliedPoseStates.Remove(remove[i]);
        }

        private static void RestoreIkTargetOverlays()
        {
            ikTargetTouchedThisFrame.Clear();
            humanIkTouchedThisFrame.Clear();
            limbTouchedThisFrame.Clear();
            RestoreInactiveIkTargetStates(forceAll: true);
            RestoreInactiveHumanIkWeightStates(forceAll: true);
            RestoreInactiveLimbWeightStates(forceAll: true);
        }

        private static void RestoreInactiveIkTargetStates(bool forceAll = false)
        {
            if (appliedIkTargetStates.Count == 0)
                return;

            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, AppliedIkTargetState> pair in appliedIkTargetStates)
            {
                if (!forceAll && ikTargetTouchedThisFrame.Contains(pair.Key))
                    continue;

                AppliedIkTargetState state = pair.Value;
                if (state?.Transform != null)
                {
                    Quaternion currentRotation = state.Transform.localRotation;
                    Vector3 currentPosition = state.Transform.localPosition;
                    if (Quaternion.Angle(currentRotation, state.AppliedRotation) < 0.1f)
                        state.Transform.localRotation = state.BaseRotation;
                    if (Vector3.Distance(currentPosition, state.AppliedPosition) < 0.0005f)
                        state.Transform.localPosition = state.BasePosition;
                }

                if (state?.Target != null)
                {
                    bool currentActive = SafeRead(() => state.Target.Active, false);
                    if (currentActive == state.AppliedActive)
                        state.Target.Active = state.BaseActive;
                }

                remove.Add(pair.Key);
            }

            for (int i = 0; i < remove.Count; i++)
                appliedIkTargetStates.Remove(remove[i]);
        }

        private static void RestoreInactiveHumanIkWeightStates(bool forceAll = false)
        {
            if (appliedHumanIkWeightStates.Count == 0)
                return;

            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, AppliedHumanIkWeightState> pair in appliedHumanIkWeightStates)
            {
                if (!forceAll && humanIkTouchedThisFrame.Contains(pair.Key))
                    continue;

                AppliedHumanIkWeightState state = pair.Value;
                if (state?.HumanIK != null)
                {
                    float current = ReadHumanIkWeight(state.HumanIK, state.Goal);
                    if (Mathf.Abs(current - state.AppliedWeight) < 0.001f)
                        SetHumanIkWeight(state.HumanIK, state.Goal, state.BaseWeight);
                }

                remove.Add(pair.Key);
            }

            for (int i = 0; i < remove.Count; i++)
                appliedHumanIkWeightStates.Remove(remove[i]);
        }

        private static void RestoreInactiveLimbWeightStates(bool forceAll = false)
        {
            if (appliedLimbWeightStates.Count == 0)
                return;

            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, AppliedLimbWeightState> pair in appliedLimbWeightStates)
            {
                if (!forceAll && limbTouchedThisFrame.Contains(pair.Key))
                    continue;

                AppliedLimbWeightState state = pair.Value;
                if (state?.Limb != null)
                {
                    float currentPosition = SafeRead(() => state.Limb.PositionWeight, 0f);
                    float currentRotation = SafeRead(() => state.Limb.RotationWeight, 0f);
                    if (Mathf.Abs(currentPosition - state.AppliedPositionWeight) < 0.001f)
                        state.Limb.PositionWeight = state.BasePositionWeight;
                    if (Mathf.Abs(currentRotation - state.AppliedRotationWeight) < 0.001f)
                        state.Limb.RotationWeight = state.BaseRotationWeight;
                }

                remove.Add(pair.Key);
            }

            for (int i = 0; i < remove.Count; i++)
                appliedLimbWeightStates.Remove(remove[i]);
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Mathf.Clamp01(value);
        }

        private static Vector3 ToVector3(SerializableVector3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void DrawClipInfo(string label, AnimationClip clip)
        {
            if (clip == null)
                return;

            float length = SafeRead(() => clip.length, 0f);
            float frameRate = SafeRead(() => clip.frameRate, 0f);
            int frameCount = frameRate <= 0f ? 0 : Mathf.RoundToInt(length * frameRate);
            string clipName = SafeRead(() => clip.name, "(unnamed)");
            bool looping = SafeRead(() => clip.isLooping, false);
            bool legacy = SafeRead(() => clip.legacy, false);
            bool empty = SafeRead(() => clip.empty, false);
            bool humanMotion = SafeRead(() => clip.humanMotion, false);
            WrapMode wrapMode = SafeRead(() => clip.wrapMode, WrapMode.Default);

            GUILayout.BeginVertical(panelAltStyle);
            GUILayout.Label($"{label}: {clipName}", headerStyle);
            GUILayout.Label(
                $"length={length:0.###}s, frameRate={frameRate:0.##}, frames~={frameCount}, loop={looping}, legacy={legacy}, empty={empty}, humanMotion={humanMotion}, wrap={wrapMode}",
                mutedStyle);

            AnimationEvent[] events = SafeRead(() => clip.events, null);
            if (events != null && events.Length > 0)
            {
                GUILayout.Label($"Runtime events: {events.Length}", mutedStyle);
                int max = Mathf.Min(events.Length, 10);
                for (int i = 0; i < max; i++)
                {
                    AnimationEvent ev = events[i];
                    GUILayout.Label(
                        $"  {i}: t={SafeRead(() => ev.time, 0f):0.###}, fn={SafeRead(() => ev.functionName, "(null)")}",
                        mutedStyle);
                }

                if (events.Length > max)
                    GUILayout.Label($"  ... {events.Length - max} more", mutedStyle);
            }
            else
            {
                GUILayout.Label("Runtime events: none", mutedStyle);
            }

            GUILayout.EndVertical();
        }

        private static void EnsureSettings()
        {
            if (Config.trickAnimationDebugSettings == null)
                Config.trickAnimationDebugSettings = new TrickAnimationDebugSettings();

            if (Config.trickAnimationDebugSettings.overrides == null)
                Config.trickAnimationDebugSettings.overrides = new Dictionary<string, TrickAnimationOverride>();

            foreach (TrickAnimationOverride values in Config.trickAnimationDebugSettings.overrides.Values)
            {
                if (values == null)
                    continue;

                if (values.poseOverrides == null)
                    values.poseOverrides = new List<TrickPoseOverride>();
                if (values.ikTargetOverrides == null)
                    values.ikTargetOverrides = new List<TrickIkTargetOverride>();

                for (int i = 0; i < values.ikTargetOverrides.Count; i++)
                    NormalizeIkTargetOverride(values.ikTargetOverrides[i]);
            }
        }

        private static void InitStyles()
        {
            if (cardStyle != null && styleRevision == Menu.styleRevision)
                return;

            styleRevision = Menu.styleRevision;

            cardStyle = new GUIStyle(Menu.UiPanelStyle)
            {
                padding = new RectOffset(12, 12, 10, 12),
                margin = new RectOffset(0, 0, 6, 8)
            };
            headerStyle = new GUIStyle(Menu.UiHeaderStyle)
            {
                fontStyle = FontStyle.Bold,
                richText = true
            };
            mutedStyle = new GUIStyle(Menu.UiMutedWrappedStyle)
            {
                wordWrap = true,
                normal = { textColor = new Color(1f, 1f, 1f, 0.72f) }
            };
            smallButtonStyle = new GUIStyle(Menu.UiMiniButtonStyle)
            {
                fontSize = 12,
                padding = new RectOffset(8, 8, 4, 4)
            };
            panelAltStyle = new GUIStyle(Menu.UiPanelAltStyle)
            {
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(0, 0, 4, 6)
            };
            rowButtonStyle = new GUIStyle(Menu.UiRowButtonStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 4, 4),
                margin = new RectOffset(0, 0, 1, 1)
            };
            searchFieldStyle = new GUIStyle(Menu.UiSearchFieldStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(9, 9, 3, 3),
                margin = new RectOffset(0, 6, 0, 0)
            };
        }

        private static string GetDataKey(SyncTrickAnimationData data)
        {
            return SafeRead(() => data.name, null) ?? TrickName(data);
        }

        private static string TrickName(SyncTrickAnimationData data)
        {
            if (data == null)
                return "(null)";

            return SafeRead(() => data.TrickName, SafeRead(() => data.name, "(unnamed)"));
        }

        private static string ClipName(AnimationClip clip)
        {
            return clip == null ? null : SafeRead(() => clip.name, null);
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
