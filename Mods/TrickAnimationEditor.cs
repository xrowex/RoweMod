using System.Collections.Generic;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using Il2CppMashBox.Core.Runtime.TrickSystem;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    internal static class TrickAnimationEditor
    {
        private const float BrainPollInterval = 0.05f;

        private static TrickSystemBrainV2 observedBrain;
        private static SyncTrickAnimationData currentData;
        private static float nextBrainPollTime;
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
        private static bool showClipDetails;
        private static bool showAdvancedControls;
        private static bool showPoseOverlayTools;
        private static string clipSourceSearch = string.Empty;
        private static string trickCatalogSearch = string.Empty;
        private static int selectedTrickCatalogIndex = -1;
        private static string selectedTrickCatalogKey = string.Empty;
        private static string status = "Throw a trick to load animation data.";
        private static TrickAnimator activeAnimator;
        private static SyncTrickAnimationData activePoseData;
        private static string activePoseDataKey = string.Empty;
        private static string activePhase = "Any";
        private static float activePoseUntil;
        private static string lastAppliedPoseLogKey = string.Empty;
        private static bool activePoseFromBrainFallback;

        private static GUIStyle cardStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle mutedStyle;
        private static GUIStyle smallButtonStyle;
        private static GUIStyle panelAltStyle;
        private static GUIStyle rowButtonStyle;
        private static GUIStyle clipSourceButtonStyle;
        private static GUIStyle searchFieldStyle;
        private static int styleRevision = -1;

        private static readonly Dictionary<int, TrickAnimationOverride> runtimeDefaults = new Dictionary<int, TrickAnimationOverride>();
        private static readonly Dictionary<int, SyncTrickAnimationData> runtimeDataCache = new Dictionary<int, SyncTrickAnimationData>();
        private static readonly List<SyncTrickAnimationData> trickCatalog = new List<SyncTrickAnimationData>();
        private static readonly List<SyncTrickAnimationData> clipSources = new List<SyncTrickAnimationData>();
        private static readonly Dictionary<string, AnimationClip> clipLookup = new Dictionary<string, AnimationClip>();
        private static readonly Dictionary<int, AppliedPoseState> appliedPoseStates = new Dictionary<int, AppliedPoseState>();
        private static readonly HashSet<int> poseTouchedThisFrame = new HashSet<int>();
        private static float suppressAutoApplyUntil;
        private static bool runtimeRefreshRequested = true;
        private static bool runtimeRefreshIncludeAllLoadedData;
        private static bool trickCatalogDirty = true;
        private static bool clipSourceCatalogDirty = true;
        private static string runtimeRefreshReason = "startup";
        private static SyncTrickAnimationData pendingAutoSaveData;
        private static string pendingAutoSaveKey = string.Empty;
        private static string pendingAutoSaveName = string.Empty;
        private static float pendingAutoSaveAt;
        private const float AutoSaveDelay = 0.4f;

        private static bool animationSourcePickerOpen;
        private static bool animationSourcePickerScooter;
        private static bool animationSourceCopyPlayer;
        private static bool animationSourceCopyVehicle;
        private static SyncTrickAnimationData animationSourceTarget;
        private static Vector2 animationSourcePickerScroll;
        private static Rect animationSourcePickerRect = new Rect(0f, 0f, 540f, 500f);
        private const int AnimationSourcePickerWindowId = 0x31A501;

        private static readonly string[] PhaseOptions = { "Any", "Enter", "Tweak", "Loop", "Exit" };

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

        public static void Update()
        {
            EnsureSettings();
            ProcessPendingAutoSave();
            if (!Config.trickAnimationDebugSettings.editorEnabled)
            {
                RestorePoseOverlays();
                return;
            }

            PollBrain();

            if (runtimeRefreshRequested && Time.unscaledTime >= suppressAutoApplyUntil)
            {
                bool includeAllLoadedData = runtimeRefreshIncludeAllLoadedData;
                string reason = runtimeRefreshReason;
                runtimeRefreshRequested = false;
                runtimeRefreshIncludeAllLoadedData = false;
                runtimeRefreshReason = string.Empty;
                RefreshRuntimeDataCache(includeAllLoadedData, false, reason);
            }
        }

        public static void LateUpdate()
        {
            EnsureSettings();
            RestorePoseOverlays();
        }

        public static void NotifyPreviewTrick(SyncTrickAnimationData data)
        {
            ActivatePosePreviewFromBrain(data);
        }

        public static void NotifyPreviewEnded()
        {
            if (!activePoseFromBrainFallback)
                return;

            activePoseFromBrainFallback = false;
            activePoseUntil = 0f;
            activePoseData = null;
            activePoseDataKey = string.Empty;
        }

        public static void CloseTricksUi()
        {
            if (pendingAutoSaveData != null)
            {
                pendingAutoSaveAt = 0f;
                ProcessPendingAutoSave();
            }

            animationSourcePickerOpen = false;
            animationSourceTarget = null;
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            RestorePoseOverlays();
            observedBrain = null;
            currentData = null;
            activeAnimator = null;
            activePoseData = null;
            activePoseDataKey = string.Empty;
            currentKey = string.Empty;
            currentName = string.Empty;
            currentSetId = -1;
            currentSlotId = -1;
            lastObservedSetId = -999;
            lastObservedSlotId = -999;
            lastObservedTimeSinceFire = 999f;
            nextBrainPollTime = 0f;
            runtimeDefaults.Clear();
            runtimeDataCache.Clear();
            trickCatalog.Clear();
            clipSources.Clear();
            clipLookup.Clear();
            trickCatalogDirty = true;
            clipSourceCatalogDirty = true;
            runtimeRefreshRequested = false;
            runtimeRefreshIncludeAllLoadedData = false;
            runtimeRefreshReason = string.Empty;
            pendingAutoSaveData = null;
            pendingAutoSaveKey = string.Empty;
            pendingAutoSaveName = string.Empty;
            animationSourcePickerOpen = false;
            animationSourceTarget = null;

            if (gameplayScene)
                RequestRuntimeRefresh("gameplay scene initialized");
        }

        public static void RequestRuntimeRefresh(string reason, bool includeAllLoadedData = false)
        {
            runtimeRefreshRequested = true;
            runtimeRefreshIncludeAllLoadedData |= includeAllLoadedData;
            if (!string.IsNullOrWhiteSpace(reason))
                runtimeRefreshReason = reason;
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
                int applied = RefreshRuntimeDataCache(true, true, "manual override apply");
                status = $"Applied saved animation overrides to {applied} loaded trick data objects.";
                EnsureTrickCatalog(false);
            }

            if (GUILayout.Button("Refresh Tricks", smallButtonStyle, GUILayout.Width(120)))
            {
                RefreshRuntimeDataCache(true, true, "manual trick refresh");
                EnsureTrickCatalog(false);
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
            return RefreshRuntimeDataCache(true, true, "explicit override apply");
        }

        private static void DrawDataControls(SyncTrickAnimationData data, string dataKey, string title)
        {
            if (data == null)
                return;

            ProcessPendingAutoSave();

            string trickName = TrickName(data);
            if (!string.IsNullOrWhiteSpace(title))
                GUILayout.Label(title, headerStyle);
            GUILayout.Label($"Animation data: {SafeRead(() => data.name, "(unnamed)")}", mutedStyle);

            bool changed = false;
            changed |= DrawFloatSlider("Overall Speed", data._overallSpeedMult, 0.05f, 10f, v => data._overallSpeedMult = v);

            showAdvancedControls = GUILayout.Toggle(
                showAdvancedControls,
                showAdvancedControls ? "v Advanced Timing & Flags" : "> Advanced Timing & Flags",
                headerStyle);
            if (showAdvancedControls)
            {
                changed |= DrawFloatSlider("Enter Speed", data._enterSpeedMult, 0.05f, 5f, v => data._enterSpeedMult = v);
                GUILayout.Label($"Derived Loop Speed: {SafeRead(() => data.LoopSpeedMult, 1f):0.###}", mutedStyle);
                changed |= DrawFloatSlider("Loop Mult", data._loopMult, 0.05f, 5f, v => data._loopMult = v);
                changed |= DrawFloatSlider("Tweak Speed", data._tweakSpeedMult, 0.05f, 10f, v => data._tweakSpeedMult = v);
                changed |= DrawFloatSlider("Exit Speed", data._exitSpeedMult, 0.05f, 5f, v => data._exitSpeedMult = v);
                changed |= DrawFloatSlider("Tweak Blend At", data._tweakBeginBlendNormalizedTime, 0f, 1f, v => data._tweakBeginBlendNormalizedTime = v);

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
                ScheduleAutoSave(data, dataKey, trickName);
            }

            Menu.BeginToolbar();
            if (Menu.DangerButton("Reset Trick Animation", GUILayout.Width(158f), GUILayout.Height(26f)))
                ResetAnimationOverride(data, dataKey, trickName);

            bool hasSavedOverride = Config.trickAnimationDebugSettings.overrides.ContainsKey(dataKey);
            bool savePending = pendingAutoSaveData != null &&
                string.Equals(pendingAutoSaveKey, dataKey, System.StringComparison.OrdinalIgnoreCase);
            GUILayout.Label(
                savePending ? "Saving changes..." : hasSavedOverride ? "Saved" : "Using game defaults",
                mutedStyle,
                GUILayout.Height(24f));
            Menu.EndToolbar();

            DrawClipCopyTools(data);

            GUILayout.Space(8);
            showClipDetails = GUILayout.Toggle(showClipDetails, showClipDetails ? "v Clip Details" : "> Clip Details", headerStyle);
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
                    RequestRuntimeRefresh("trick brain discovered");
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
            RegisterRuntimeData(data, true);
            EnsureTrickCatalog(false);
            selectedTrickCatalogKey = currentKey;
            selectedTrickCatalogIndex = FindCatalogIndexByKey(currentKey);
            status = $"Captured {currentName}.";
            ActivatePosePreviewFromBrain(data);
        }

        private static void ActivatePosePreviewFromBrain(SyncTrickAnimationData data)
        {
            if (data == null)
                return;

            string dataKey = GetDataKey(data);
            if (string.IsNullOrEmpty(dataKey))
                return;

            activePoseData = data;
            activePoseDataKey = dataKey;
            activePhase = "Any";
            activePoseUntil = Time.unscaledTime + 1.25f;
            activePoseFromBrainFallback = true;
            if (activeAnimator == null)
                activeAnimator = ResolveActiveTrickAnimator();
        }

        private static void EnsureTrickCatalog(bool force)
        {
            if (force)
                RefreshRuntimeDataCache(true, false, "trick catalog refresh");

            if (!trickCatalogDirty)
                return;

            string previousKey = selectedTrickCatalogKey;
            if (string.IsNullOrEmpty(previousKey) && selectedTrickCatalogIndex >= 0 && selectedTrickCatalogIndex < trickCatalog.Count)
                previousKey = GetDataKey(trickCatalog[selectedTrickCatalogIndex]);

            trickCatalog.Clear();
            HashSet<string> seen = new HashSet<string>();

            foreach (SyncTrickAnimationData data in runtimeDataCache.Values)
            {
                AddTrickCatalogCandidate(data, seen);
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

            trickCatalogDirty = false;
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

        private static int RefreshRuntimeDataCache(bool includeAllLoadedData, bool logResult, string reason)
        {
            EnsureSettings();
            if (includeAllLoadedData)
                runtimeDataCache.Clear();
            else
                PruneRuntimeDataCache();

            TrickSystemBrainV2 brain = observedBrain;
            if (brain == null)
            {
                TrickSystemBrainV2[] brains = SafeRead(
                    () => UnityEngine.Object.FindObjectsOfType<TrickSystemBrainV2>(),
                    null);
                if (brains != null && brains.Length > 0)
                {
                    brain = brains[0];
                    observedBrain = brain;
                }
            }

            AddBrainDataToRuntimeCache(brain);
            RegisterRuntimeData(currentData, false);

            bool usedGlobalDiscovery = includeAllLoadedData || runtimeDataCache.Count == 0;
            if (usedGlobalDiscovery)
            {
                try
                {
                    SyncTrickAnimationData[] allData = Resources.FindObjectsOfTypeAll<SyncTrickAnimationData>();
                    if (allData != null)
                    {
                        for (int i = 0; i < allData.Length; i++)
                            RegisterRuntimeData(allData[i], false);
                    }
                }
                catch (System.Exception ex)
                {
                    status = $"Trick data discovery failed: {ex.Message}";
                }
            }

            int applied = ApplySavedOverridesToCachedData();
            trickCatalogDirty = true;
            clipSourceCatalogDirty = true;
            EnsureTrickCatalog(false);

            if (logResult)
            {
                Log.Msg(
                    $"[TrickAnimEditor] Runtime refresh '{reason}': cached={runtimeDataCache.Count}, " +
                    $"applied={applied}, globalDiscovery={usedGlobalDiscovery}.");
            }

            return applied;
        }

        private static void PruneRuntimeDataCache()
        {
            if (runtimeDataCache.Count == 0)
                return;

            List<int> staleIds = null;
            foreach (KeyValuePair<int, SyncTrickAnimationData> pair in runtimeDataCache)
            {
                if (pair.Value != null)
                    continue;

                staleIds ??= new List<int>();
                staleIds.Add(pair.Key);
            }

            if (staleIds == null)
                return;

            for (int i = 0; i < staleIds.Count; i++)
                runtimeDataCache.Remove(staleIds[i]);

            trickCatalogDirty = true;
            clipSourceCatalogDirty = true;
        }

        private static void AddBrainDataToRuntimeCache(TrickSystemBrainV2 brain)
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
                        RegisterRuntimeData(data, false);
                    }
                }
            }
            catch (System.Exception ex)
            {
                status = $"Trick brain traversal failed: {ex.Message}";
            }
        }

        private static void RegisterRuntimeData(SyncTrickAnimationData data, bool applySavedOverride)
        {
            if (data == null)
                return;

            int instanceId = SafeRead(() => data.GetInstanceID(), 0);
            if (instanceId == 0)
                return;

            bool added = !runtimeDataCache.ContainsKey(instanceId);
            runtimeDataCache[instanceId] = data;
            RememberRuntimeDefault(data);
            if (applySavedOverride)
                ApplySavedOverride(data);

            if (added)
            {
                trickCatalogDirty = true;
                clipSourceCatalogDirty = true;
            }
        }

        private static int ApplySavedOverridesToCachedData()
        {
            if (Config.trickAnimationDebugSettings.overrides.Count == 0)
                return 0;

            int applied = 0;
            foreach (SyncTrickAnimationData data in runtimeDataCache.Values)
            {
                if (data != null && ApplySavedOverride(data))
                    applied++;
            }

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
            }

            return values;
        }

        private static void DrawClipCopyTools(SyncTrickAnimationData target)
        {
            Menu.BeginAltPane("Animation Source", "Copy human or bike clips from another loaded trick.");
            EnsureClipSourceCatalog(false);

            if (clipSources.Count == 0)
            {
                GUILayout.Label("No loaded SyncTrickAnimationData sources found yet.", mutedStyle);
                if (Menu.SecondaryButton("Refresh Sources", GUILayout.Width(120f), GUILayout.Height(24f)))
                    EnsureClipSourceCatalog(true);
                Menu.EndPane();
                return;
            }

            GUILayout.Label($"Human clips: {ClipSummary(SafeRead(() => target.PlayerEnterClip, null))}", mutedStyle);
            GUILayout.Label($"Bike clips: {ClipSummary(SafeRead(() => target.VehicleEnterClip, null))}", mutedStyle);
            Menu.BeginToolbar();
            if (Menu.SecondaryButton("Change Human", GUILayout.Width(120f), GUILayout.Height(24f)))
                OpenAnimationSourcePicker(target, true, false);
            if (Menu.SecondaryButton("Change Bike", GUILayout.Width(110f), GUILayout.Height(24f)))
                OpenAnimationSourcePicker(target, false, true);
            if (Menu.PrimaryButton("Change Both", GUILayout.Width(110f), GUILayout.Height(24f)))
                OpenAnimationSourcePicker(target, true, true);
            Menu.EndToolbar();
            GUILayout.Label("Selecting a source copies and saves the chosen clips immediately.", mutedStyle);
            Menu.EndPane();
        }

        public static void DrawAnimationSourcePickerPopup()
        {
            if (!animationSourcePickerOpen || animationSourceTarget == null)
                return;

            EnsureSettings();
            InitStyles();
            if (Event.current.type == EventType.MouseDown && !animationSourcePickerRect.Contains(Event.current.mousePosition))
            {
                animationSourcePickerOpen = false;
                return;
            }

            animationSourcePickerRect.x = Mathf.Clamp(animationSourcePickerRect.x, 10f, Mathf.Max(10f, Screen.width - animationSourcePickerRect.width - 10f));
            animationSourcePickerRect.y = Mathf.Clamp(animationSourcePickerRect.y, 10f, Mathf.Max(10f, Screen.height - animationSourcePickerRect.height - 10f));
            animationSourcePickerRect = GUI.Window(
                AnimationSourcePickerWindowId,
                animationSourcePickerRect,
                (GUI.WindowFunction)DrawAnimationSourcePickerWindow,
                "Animation Source");
        }

        private static void OpenAnimationSourcePicker(SyncTrickAnimationData target, bool copyPlayer, bool copyVehicle)
        {
            if (target == null)
                return;

            EnsureClipSourceCatalog(false);
            animationSourceTarget = target;
            animationSourceCopyPlayer = copyPlayer;
            animationSourceCopyVehicle = copyVehicle;
            animationSourcePickerScroll = Vector2.zero;
            clipSourceSearch = string.Empty;
            animationSourcePickerRect = new Rect(
                Mathf.Max(10f, (Screen.width - 540f) * 0.5f),
                Mathf.Max(10f, (Screen.height - 500f) * 0.5f),
                540f,
                500f);
            animationSourcePickerOpen = true;
        }

        private static void DrawAnimationSourcePickerWindow(int id)
        {
            GUILayout.BeginVertical(cardStyle);
            string mode = animationSourceCopyPlayer && animationSourceCopyVehicle
                ? "human and bike"
                : animationSourceCopyPlayer ? "human" : "bike";
            GUILayout.Label($"Copy {mode} animation into {TrickName(animationSourceTarget)}", headerStyle);
            GUILayout.Label("Choose a source trick. The replacement saves immediately.", mutedStyle);

            Menu.BeginToolbar();
            Menu.SearchRow(ref clipSourceSearch, 280f, "Search");
            if (Menu.SecondaryButton("Refresh", GUILayout.Width(72f), GUILayout.Height(24f)))
                EnsureClipSourceCatalog(true);
            if (Menu.DangerButton("Close", GUILayout.Width(64f), GUILayout.Height(24f)))
                animationSourcePickerOpen = false;
            Menu.EndToolbar();

            Menu.BeginToolbar();
            if (GUILayout.Toggle(!animationSourcePickerScooter, "BMX", !animationSourcePickerScooter ? Menu.UiPillActiveStyle : Menu.UiPillStyle, GUILayout.Width(100f), GUILayout.Height(24f)))
            {
                if (animationSourcePickerScooter)
                    animationSourcePickerScroll = Vector2.zero;
                animationSourcePickerScooter = false;
            }
            if (GUILayout.Toggle(animationSourcePickerScooter, "Scooter", animationSourcePickerScooter ? Menu.UiPillActiveStyle : Menu.UiPillStyle, GUILayout.Width(100f), GUILayout.Height(24f)))
            {
                if (!animationSourcePickerScooter)
                    animationSourcePickerScroll = Vector2.zero;
                animationSourcePickerScooter = true;
            }
            Menu.EndToolbar();

            string search = (clipSourceSearch ?? string.Empty).Trim();
            int matches = 0;
            animationSourcePickerScroll = GUILayout.BeginScrollView(animationSourcePickerScroll, GUILayout.Height(330f));
            for (int i = 0; i < clipSources.Count; i++)
            {
                SyncTrickAnimationData source = clipSources[i];
                if (source == null || source == animationSourceTarget || IsScooterAnimationSource(source) != animationSourcePickerScooter)
                    continue;

                string sourceName = TrickName(source);
                if (search.Length > 0 && sourceName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                matches++;
                if (GUILayout.Button(sourceName, clipSourceButtonStyle, GUILayout.Height(28f), GUILayout.ExpandWidth(true)))
                {
                    CopyClipsFromSource(source, animationSourceTarget, animationSourceCopyPlayer, animationSourceCopyVehicle);
                    animationSourcePickerOpen = false;
                }
            }
            if (matches == 0)
                GUILayout.Label(search.Length > 0 ? "No source tricks match this search." : "No sources are available in this category.", mutedStyle);
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private static string ClipSummary(AnimationClip clip)
        {
            if (clip == null)
                return "game default / unavailable";

            return string.IsNullOrWhiteSpace(clip.name) ? "unnamed clip" : clip.name;
        }

        private static bool IsScooterAnimationSource(SyncTrickAnimationData source)
        {
            string rawName = GetDataKey(source) ?? string.Empty;
            return rawName.IndexOf("Scooter", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
        }

        private static TrickAnimationOverride GetOrCreateAnimationOverride(SyncTrickAnimationData data, string dataKey)
        {
            EnsureSettings();
            if (Config.trickAnimationDebugSettings.overrides.TryGetValue(dataKey, out TrickAnimationOverride values) && values != null)
            {
                values.poseOverrides ??= new List<TrickPoseOverride>();
                return values;
            }

            values = CaptureOverride(data, dataKey);
            values.poseOverrides ??= new List<TrickPoseOverride>();
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
            string targetKey = GetDataKey(target);
            SaveAnimationOverride(
                target,
                targetKey,
                $"Copied and auto-saved {label} animation from {TrickName(source)} to {TrickName(target)}.");
            Log.Msg($"[TrickAnimEditor] Copied {label} clips from {TrickName(source)} to {TrickName(target)}.");
        }

        private static void ScheduleAutoSave(SyncTrickAnimationData data, string dataKey, string trickName)
        {
            if (pendingAutoSaveData != null &&
                !string.Equals(pendingAutoSaveKey, dataKey, System.StringComparison.OrdinalIgnoreCase))
            {
                pendingAutoSaveAt = 0f;
                ProcessPendingAutoSave();
            }

            pendingAutoSaveData = data;
            pendingAutoSaveKey = dataKey ?? string.Empty;
            pendingAutoSaveName = trickName ?? "trick";
            pendingAutoSaveAt = Time.unscaledTime + AutoSaveDelay;
            status = $"Saving {pendingAutoSaveName}...";
        }

        private static void ProcessPendingAutoSave()
        {
            if (pendingAutoSaveData == null || Time.unscaledTime < pendingAutoSaveAt)
                return;

            SyncTrickAnimationData data = pendingAutoSaveData;
            string dataKey = pendingAutoSaveKey;
            string trickName = pendingAutoSaveName;
            pendingAutoSaveData = null;
            pendingAutoSaveKey = string.Empty;
            pendingAutoSaveName = string.Empty;

            SaveAnimationOverride(data, dataKey, $"Saved animation changes for {trickName}.");
        }

        private static void ClearPendingAutoSave(string dataKey)
        {
            if (pendingAutoSaveData == null ||
                !string.Equals(pendingAutoSaveKey, dataKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            pendingAutoSaveData = null;
            pendingAutoSaveKey = string.Empty;
            pendingAutoSaveName = string.Empty;
        }

        private static void SaveAnimationOverride(SyncTrickAnimationData data, string dataKey, string message)
        {
            EnsureSettings();
            ClearPendingAutoSave(dataKey);
            Config.trickAnimationDebugSettings.overrides[dataKey] = CaptureOverride(data, dataKey);
            RegisterRuntimeData(data, false);
            ApplySavedOverrideToCachedKey(dataKey);
            Config.Save();
            status = message;
            Log.Msg($"[TrickAnimEditor] Saved override for {TrickName(data)} ({dataKey}).");
        }

        private static void ResetAnimationOverride(SyncTrickAnimationData data, string dataKey, string trickName)
        {
            EnsureSettings();
            ClearPendingAutoSave(dataKey);
            bool removed = Config.trickAnimationDebugSettings.overrides.Remove(dataKey);
            RegisterRuntimeData(data, false);
            Config.Save();
            RestoreRuntimeDefaultsForKey(dataKey);
            suppressAutoApplyUntil = Time.unscaledTime + 5f;
            status = removed
                ? $"Reset {trickName} to runtime defaults and removed saved override."
                : $"Reset {trickName} to runtime defaults.";
            Log.Msg($"[TrickAnimEditor] Reset override for {trickName} ({dataKey}). removedSaved={removed}.");
        }

        private static int ApplySavedOverrideToCachedKey(string dataKey)
        {
            if (string.IsNullOrEmpty(dataKey))
                return 0;

            int applied = 0;
            foreach (SyncTrickAnimationData cached in runtimeDataCache.Values)
            {
                if (cached == null ||
                    !string.Equals(GetDataKey(cached), dataKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ApplySavedOverride(cached))
                    applied++;
            }

            return applied;
        }

        private static void RestoreRuntimeDefaultsForKey(string dataKey)
        {
            if (string.IsNullOrEmpty(dataKey))
                return;

            foreach (SyncTrickAnimationData cached in runtimeDataCache.Values)
            {
                if (cached != null &&
                    string.Equals(GetDataKey(cached), dataKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    RestoreRuntimeDefault(cached);
                }
            }
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
            if (force)
                RefreshRuntimeDataCache(true, false, "clip source refresh");

            if (!clipSourceCatalogDirty)
                return;

            clipSources.Clear();

            HashSet<string> seen = new HashSet<string>();
            foreach (SyncTrickAnimationData data in runtimeDataCache.Values)
            {
                if (data == null)
                    continue;

                string key = GetDataKey(data);
                if (string.IsNullOrEmpty(key) || seen.Contains(key))
                    continue;

                seen.Add(key);
                clipSources.Add(data);
            }

            clipSources.Sort((a, b) => string.Compare(TrickName(a), TrickName(b), System.StringComparison.OrdinalIgnoreCase));

            clipSourceCatalogDirty = false;
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

        private static TrickAnimator ResolveActiveTrickAnimator()
        {
            try
            {
                TrickAnimator[] animators = UnityEngine.Object.FindObjectsOfType<TrickAnimator>();
                if (animators == null || animators.Length == 0)
                    return null;

                for (int i = 0; i < animators.Length; i++)
                {
                    TrickAnimator animator = animators[i];
                    if (animator != null && SafeRead(() => animator.isActiveAndEnabled, false))
                        return animator;
                }

                return animators[0];
            }
            catch
            {
                return null;
            }
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
            clipSourceButtonStyle = new GUIStyle(Menu.UiPillStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontSize = 12,
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(2, 2, 2, 2)
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
