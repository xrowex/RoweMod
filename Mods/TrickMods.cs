using System.Collections.Generic;
using System.Reflection;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using Il2CppMashBox.Core.Runtime.TrickSystem;
//using Il2CppMG_Core.C_R_I_D.Animation_System.Animancer_Test;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using Il2CppSystem;
using AppDomain = System.AppDomain; // for Il2CppSystem.Type
using Array = System.Array;
using ManagedType = System.Type;
using Il2CppTypeRef = Il2CppSystem.Type;
using Math = System.Math;
using StringComparison = System.StringComparison;

namespace rowemod.Mods
{
    internal class TrickMods
    {
        #region Fields and Properties

        public static TrickSetData[]? rTrickSetData;
        public static TrickSystemBrain? rTrickSystemBrain;
        public static TrickBrainHandler rTrickBrainHandler;
        public static Character? rCharacter;

        public static InputAction? trickSet1InputAction, trickSet2InputAction,
                                  trickSet3InputAction, trickSet4InputAction,
                                  trickSet5InputAction, trickSet6InputAction;
        
        // NEW: Track toggle state per TrickSet and cache originals so we can restore.
        public static readonly Dictionary<string, bool[]> _trickEnabled = new();
        private static readonly Dictionary<string, UnityEngine.Object?[]> _originalRefs = new();
        private static readonly Dictionary<string, UnityEngine.Object?[]> _configuredRefs = new();


        // We'll store the extracted trick names in a dictionary: TrickSetData.name -> List of strings
        public static Dictionary<string, List<string>> trickDictionary { get; private set; }
            = new Dictionary<string, List<string>>();

        // For easy display in the GUI, create a pre-built list of "menu entries"
        public static List<TrickMenuEntry> trickMenuDisplayItems { get; private set; }
            = new List<TrickMenuEntry>();
        
        // === Global catalog of available tricks (deduped by clean name) ===
        private static readonly List<UnityEngine.Object> _catalog = new();
        private static string[] _catalogNames = System.Array.Empty<string>();
        private static readonly Dictionary<string, int> _catalogIndexByName = new(System.StringComparer.OrdinalIgnoreCase);

        // === Simple overlay picker state ===
        private static bool _pickerOpen = false;
        private static TrickSetData _pickerSet;
        private static int _pickerSlot = -1;
        private static Vector2 _pickerScroll;
        private static string _pickerSearch = "";

        // Optional: a little style cache
        private static GUIStyle _pickerBox, _pickerRow, _pickerSearchStyle;
        
        // Replace your DirectionGlyphMap with this:
        private static readonly Dictionary<string, string> DirectionGlyphMap = new()
        {
            { "Up",    "xbox_dpad_up"    },
            { "Right", "xbox_dpad_right" },
            { "Down",  "xbox_dpad_down"  },
            { "Left",  "xbox_dpad_left"  },
            // NOTE: No diagonals here on purpose.
        };
        private static readonly Dictionary<string, Texture2D> GlyphCache = new();


        // Popup window placement
        private static Rect _pickerRect = new Rect(0, 0, 360, 340);
        private const int PickerWindowId = 0x315C0DE; // any unique int


        #endregion

        #region Helper Class for GUI

        // This struct (or class) holds the pre-processed data
        public class TrickMenuEntry
        {
            public string? TrickSetDisplayName;
            public List<string>? TrickDirections;
        }

        #endregion

        // Example direction labels
        private static readonly string[] DefaultDirectionLabels =
        {
            "Up", "UpRight", "Right", "DownRight",
            "Down", "DownLeft", "Left", "UpLeft"
        };
        private static Il2CppTypeRef ToIl2CppType(ManagedType managed)
        {
            if (managed == null) return null;

            // Il2CppInterop.Runtime.Il2CppType.From(System.Type)
            var interop = ManagedType.GetType("Il2CppInterop.Runtime.Il2CppType, Il2CppInterop.Runtime");
            if (interop != null)
            {
                var m = interop.GetMethod("From", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { typeof(ManagedType) }, null);
                if (m != null) return (Il2CppTypeRef)m.Invoke(null, new object[] { managed });
            }

            // UnhollowerRuntimeLib.Il2CppType.From(System.Type)
            var unh = ManagedType.GetType("UnhollowerRuntimeLib.Il2CppType, UnhollowerBaseLib");
            if (unh != null)
            {
                var m = unh.GetMethod("From", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { typeof(ManagedType) }, null);
                if (m != null) return (Il2CppTypeRef)m.Invoke(null, new object[] { managed });
            }

            return null;
        }

        #region Main Methods
        private static void ApplyToggle(TrickSetData set, int index, bool enabled)
        {
            if (set == null || set._dataList == null) return;
            if (index < 0 || index >= set._dataList.Count) return;

            var setKey = set.name;
            if (!_originalRefs.TryGetValue(setKey, out var originals)) return;

            try
            {
                if (!_configuredRefs.TryGetValue(setKey, out var configured) || configured.Length <= index)
                    configured = originals;

                var newVal = enabled ? configured[index] : null;
                set._dataList[(Index)index] = newVal;

                // UPDATED: keep the UI list in sync, but with cleaned names
                if (trickDictionary.TryGetValue(setKey, out var names) && index < names.Count)
                    names[index] = CleanTrickName(configured[index]);

                ForceRefreshTrickRuntime();
                Log.Msg($"{(enabled ? "Restored" : "Disabled")} {setKey}[{index}]");
            }
            catch (System.Exception ex)
            {
                Log.Msg($"Failed to {(enabled ? "restore" : "null")} item [{index}] in {setKey}: {ex}");
            }
        }


        private static void ApplyAll(TrickSetData set, bool enableAll)
        {
            if (set == null || set._dataList == null) return;
            var setKey = set.name;
            if (!_originalRefs.TryGetValue(setKey, out var originals)) return;
            if (!_configuredRefs.TryGetValue(setKey, out var configured)) configured = originals;
            if (!_trickEnabled.TryGetValue(setKey, out var flags)) return;

            int count = Math.Min(set._dataList.Count, Math.Min(configured.Length, flags.Length));
            for (int i = 0; i < count; i++)
            {
                flags[i] = enableAll;
                set._dataList[(Index)i] = enableAll ? configured[i] : null;
            }

            // one refresh is enough for bulk ops
            ForceRefreshTrickRuntime();
            Log.Msg($"{(enableAll ? "Enabled" : "Disabled")} ALL in {setKey}");
        }

        // ---------- Runtime Refresh ----------
        // Some versions build fast lookup arrays in Start/Awake; toggling Behaviour.enabled
        // calls OnDisable/OnEnable which many systems use to rebuild.
        private static void ForceRefreshTrickRuntime()
        {
            // Try v2 types first (as in your screenshot), then legacy names.
            ToggleAllBehaviours(
                "Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2.TrickAnimator",
                "Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2.TrickControllerV2",
                "Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2.TrickSystemBrainV2",
                "Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.TrickSystemBrain",
                "Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.TrickBrainHandler"
            );

            // Opportunistically poke common “rebuild” methods if present.
            InvokeNoArgIfExistsOnAll(
                "Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2.TrickControllerV2",
                "Rebuild", "Refresh", "Init", "Initialize", "Setup", "Build"
            );
        }

        // Toggle enabled -> disabled -> enabled on every instance of these types
        private static void ToggleAllBehaviours(params string[] fullTypeNames)
        {
            foreach (var name in fullTypeNames)
            {
                var managed = FindType(name); // your existing System.Type finder
                if (managed == null) continue;

                var il2cpp = ToIl2CppType(managed); // <-- bridge
                if (il2cpp == null) continue;

                var objs = UnityEngine.Resources.FindObjectsOfTypeAll(il2cpp);
                foreach (var o in objs)
                {
                    if (o is UnityEngine.Behaviour b)
                    {
                        bool was = b.enabled;
                        b.enabled = false;
                        b.enabled = was || true; // end enabled
                    }
                }
            }
        }


        // Try to call common “rebuild-ish” methods (no args) if they exist
        private static void InvokeNoArgIfExistsOnAll(string fullTypeName, params string[] methodNames)
        {
            var managed = FindType(fullTypeName);      // System.Type
            if (managed == null) return;

            var il2cpp = ToIl2CppType(managed);       // Il2CppSystem.Type
            if (il2cpp == null) return;

            // Use the IL2CPP overload of FindObjectsOfTypeAll
            var objs = UnityEngine.Resources.FindObjectsOfTypeAll(il2cpp);
            foreach (var o in objs)
            {
                // Best-effort poke without reflection brittleness on IL2CPP:
                // SendMessage tolerates "method not found" via DontRequireReceiver.
                if (o is UnityEngine.Component c)
                {
                    foreach (var mName in methodNames)
                        c.SendMessage(mName, UnityEngine.SendMessageOptions.DontRequireReceiver);
                }
                else if (o is UnityEngine.Behaviour b)
                {
                    foreach (var mName in methodNames)
                        b.SendMessage(mName, UnityEngine.SendMessageOptions.DontRequireReceiver);
                }
                // If you *really* want reflection here, you can still do it:
                // var type = o.GetType(); ... GetMethod(...) ... Invoke(o, null);
                // but SendMessage is usually enough to trigger OnEnable/Refresh-style hooks.
            }
        }


        // Robust type finder without Harmony
        private static ManagedType FindType(string fullName)
        {
            // Try direct first
            var t = ManagedType.GetType(fullName, false);
            if (t != null) return t;

            // Search loaded assemblies (IL2CPP will still expose managed names)
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                t = asms[i].GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        public static void DrawTrickPickerPopup()
        {
            if (!_pickerOpen || _pickerSet == null || _pickerSlot < 0) return;

            // Close if clicked outside the popup
            if (Event.current.type == EventType.MouseDown && !_pickerRect.Contains(Event.current.mousePosition))
            {
                _pickerOpen = false;
            }

            // Clamp so it doesn’t run off-screen
            _pickerRect = ClampToScreen(_pickerRect, 10f);

            // Draw the floating popup window
            _pickerRect = GUI.Window(PickerWindowId, _pickerRect, (GUI.WindowFunction)PickerWindow, "Trick Picker");
        }

        private static void PickerWindow(int id)
        {
            // Dark background like your main cards
            GUILayout.BeginVertical(_card);   // instead of "box"

            GUILayout.Label($"Replace Trick in {_pickerSet.name} [Slot {_pickerSlot}]", _cardHeader);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(60));
            _pickerSearch = GUILayout.TextField(_pickerSearch ?? "", _searchField, GUILayout.Width(200));
            if (GUILayout.Button("×", _miniBtn, GUILayout.Width(24)))
                _pickerOpen = false;
            GUILayout.EndHorizontal();

            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll, GUILayout.Height(200));

            foreach (var trickName in _catalogNames)
            {
                if (!string.IsNullOrEmpty(_pickerSearch) &&
                    trickName.IndexOf(_pickerSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (GUILayout.Button(trickName, _rowLabelRight))
                {
                    ReplaceTrick(_pickerSet, _pickerSlot, trickName);
                    _pickerOpen = false;
                }
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Cancel", _miniBtn)) _pickerOpen = false;

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }



        public static void GrabTrickData()
        {
            Log.Msg("GrabTrickData() called...");

            // Prefer the container asset "BMX_TrickDataSets"
            TrickSetData[] bmxSetsFromAsset = null;
            var allSetsAssets = Resources.FindObjectsOfTypeAll<TrickDataSets>();
            if (allSetsAssets != null && allSetsAssets.Length > 0)
            {
                for (int i = 0; i < allSetsAssets.Length; i++)
                {
                    var ds = allSetsAssets[i];
                    if (ds == null) continue;

                    if ((ds.name ?? "").IndexOf("BMX", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var arr = ds.TrickSets; // Il2CppReferenceArray<TrickSetData>
                        if (arr != null && arr.Length > 0)
                        {
                            var tmp = new List<TrickSetData>(arr.Length);
                            for (int k = 0; k < arr.Length; k++)
                            {
                                var ts = arr[k];
                                if (ts != null) tmp.Add(ts);
                            }

                            bmxSetsFromAsset = tmp.ToArray();
                            break;
                        }
                    }
                }
            }

            if (bmxSetsFromAsset != null && bmxSetsFromAsset.Length > 0)
            {
                rTrickSetData = bmxSetsFromAsset;
                Log.Msg($"Using BMX TrickDataSets asset: {rTrickSetData.Length} sets.");
            }
            else
            {
                // Fallback: keep only sets whose names indicate BMX.
                var all = Resources.FindObjectsOfTypeAll<TrickSetData>();
                rTrickSetData = (all == null || all.Length == 0)
                    ? Array.Empty<TrickSetData>()
                    : FilterBmxSets(all);

                Log.Msg($"Using name filter fallback: {rTrickSetData.Length} BMX sets.");
            }

            // DO NOT overwrite rTrickSetData again here.
            // rTrickSetData = Resources.FindObjectsOfTypeAll<TrickSetData>();  <-- remove this line

            rTrickSystemBrain = GameObject.FindObjectOfType<TrickSystemBrain>();

            trickDictionary.Clear();
            trickMenuDisplayItems.Clear();

            if (rTrickSetData != null && rTrickSetData.Length > 0)
            {
                Log.Msg($"TrickSetData found: {rTrickSetData.Length}");

                for (int t = 0; t < rTrickSetData.Length; t++)
                {
                    var trickSetData = rTrickSetData[t];
                    if (trickSetData == null) continue;

                    var trickNames = new List<string>();

                    if (trickSetData._dataList == null)
                    {
                        trickDictionary[trickSetData.name] = trickNames;
                        continue;
                    }

                    try
                    {
                        int count = trickSetData._dataList.Count;

                        var originals = new UnityEngine.Object?[count];
                        var enabled = new bool[count];

                        for (int i = 0; i < count; i++)
                        {
                            var item = trickSetData._dataList[(Index)i];
                            var obj = item as UnityEngine.Object;
                            originals[i] = obj;
                            enabled[i] = obj != null;

                            trickNames.Add(CleanTrickName(obj));
                        }

                        _originalRefs[trickSetData.name] = originals;
                        _configuredRefs[trickSetData.name] = (UnityEngine.Object?[])originals.Clone();
                        _trickEnabled[trickSetData.name] = enabled;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Msg($"Exception reading _dataList of {trickSetData.name}: {ex}");
                    }

                    trickDictionary[trickSetData.name] = trickNames;
                }
            }
            else
            {
                Log.Msg("No BMX TrickSetData objects found!");
            }

            if (rTrickSystemBrain != null)
            {
                rCharacter = rTrickSystemBrain.GetComponent<Character>();
                trickSet1InputAction = rTrickSystemBrain._trickSet1InputAction;
                trickSet2InputAction = rTrickSystemBrain._trickSet2InputAction;
                trickSet3InputAction = rTrickSystemBrain._trickSet3InputAction;
                trickSet4InputAction = rTrickSystemBrain._trickSet4InputAction;
                trickSet5InputAction = rTrickSystemBrain._trickSet5InputAction;
                trickSet6InputAction = rTrickSystemBrain._trickSet6InputAction;
            }

            BuildTrickMenuDisplay();
            BuildCatalog();
        }


        private static string _uiSearch = "";
        private static string _presetName = "";
        private static string _selectedPresetName = "";
        private static string _presetStatus = "Save and restore trick toggles, replacements, and animation overrides.";
        private static readonly Dictionary<string, bool> _foldouts = new();
        private static readonly Dictionary<string, bool> _animationEditorFoldouts = new();
        private static bool _stylesInited;
        private static int _styleRevision = -1;
        private static GUIStyle _card, _cardHeader, _pill, _pillOn, _rowLabelLeft, _rowLabelRight, _rowButton, _rowButtonSelected, _miniBtn, _badge, _searchField;
        private static GUIStyle _toolbarLabel, _setBlock, _setHeaderButton, _rowStrip, _rowStripSelected, _directionBadge, _directionBadgeSelected, _emptyState;

        // tiny “dirty” flag so we only rebuild runtime when something changed
        private static bool _pendingRefresh;
        private static float _lastTrickDataRefreshAttempt = -10f;
        private static TrickSetData _selectedTrickSet;
        private static string _selectedTrickSetKey = string.Empty;
        private static int _selectedTrickSlot = -1;
        private static string _selectedTrickDirection = string.Empty;
        private static string _selectedTrickName = string.Empty;
        private static bool _tricksTabActive;
        private static bool _needsAutoSelectTrick;
        private static bool _previewEnabled;
        private static bool _previewStateApplied;
        private static bool _previewPlayerOffsetApplied;
        private static bool _previewSpringBodyCaptured;
        private static Transform _previewPlayerTransform;
        private static Rigidbody _previewSpringBody;
        private static bool _previewSpringBodyOriginalIsKinematic;
        private static bool _previewJumpInvoked;
        private static float _nextPreviewFireTime;
        private static string _lastPreviewSelectionKey = string.Empty;
        private static string _lastPreviewLogKey = string.Empty;
        private static bool _tricksNoBailOverrideActive;
        private static bool _tricksNoBailUserValue;
        private static float _tricksNoBailRestoreTime = -1f;
        private static readonly Vector3 TrickPreviewPlayerOffset = new Vector3(0f, 1.25f, 0f);
        private const float TrickPreviewFireInterval = 2f;
        private const float TrickNoBailExitGraceSeconds = 1f;

        public static void DrawTrickMenuPro()
        {
            InitStylesIfNeeded();

            if (Config.trickAnimationDebugSettings == null)
                Config.trickAnimationDebugSettings = new TrickAnimationDebugSettings();

            Menu.BeginToolbar();
            GUILayout.Label("Search", _toolbarLabel, GUILayout.Width(54));
            GUI.SetNextControlName("trickSearch");
            var newSearch = GUILayout.TextField(_uiSearch, _searchField, GUILayout.Width(240), GUILayout.Height(24));
            if (newSearch != _uiSearch) _uiSearch = newSearch;

            bool animationDebugLogs = Config.trickAnimationDebugSettings.enabled;
            bool updatedAnimationDebugLogs = GUILayout.Toggle(
                animationDebugLogs,
                animationDebugLogs ? "Logs On" : "Logs Off",
                animationDebugLogs ? _pillOn : _pill,
                GUILayout.Width(84),
                GUILayout.Height(24));
            if (updatedAnimationDebugLogs != Config.trickAnimationDebugSettings.enabled)
            {
                Config.trickAnimationDebugSettings.enabled = updatedAnimationDebugLogs;
                Config.Save();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Expand All", _miniBtn, GUILayout.Width(82), GUILayout.Height(24))) SetAllFoldouts(true);
            if (GUILayout.Button("Collapse All", _miniBtn, GUILayout.Width(92), GUILayout.Height(24))) SetAllFoldouts(false);
            if (GUILayout.Button("Reset Tricks", Menu.UiDangerButtonStyle, GUILayout.Width(96), GUILayout.Height(24))) ResetCustomTricks();
            Menu.EndToolbar();

            EnsureTrickDataReadyForMenu();
            EnsureTrickSelection();

            if (!HasUsableTrickSetData())
            {
                Menu.BeginPanel();
                GUILayout.Label("No trick sets are loaded yet. Enter gameplay or respawn, then reopen this tab.", _emptyState);
                Menu.EndPanel();
                return;
            }

            float presetManagerHeight = GetTrickPresetManagerHeight();
            DrawTrickPresetManager(presetManagerHeight);

            float paneHeight = GetTricksTwoPaneHeight(presetManagerHeight);
            GUILayout.BeginHorizontal();
            DrawTrickListPane(paneHeight);
            GUILayout.Space(8);
            DrawSelectedTrickPane(paneHeight);
            GUILayout.EndHorizontal();

            // final refresh once if anything changed this frame
            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                ForceRefreshTrickRuntime();
            }

            // keyboard shortcuts (A/D in the current hovered card)
            HandleKeyboardShortcuts();

        }

        private static float GetTrickPresetManagerHeight()
        {
            List<string> presets = TrickPreset.GetAvailablePresets();
            return presets.Count == 0 ? 116f : 150f;
        }

        private static float GetTricksTwoPaneHeight(float presetManagerHeight)
        {
            const float topToolbarHeight = 34f;
            const float verticalSpacing = 18f;
            const float bottomSafetyPadding = 18f;

            float available = Menu.viewHeight - topToolbarHeight - presetManagerHeight - verticalSpacing - bottomSafetyPadding;
            return Mathf.Max(180f, available);
        }

        private static void DrawTrickPresetManager(float paneHeight)
        {
            List<string> presets = TrickPreset.GetAvailablePresets();
            if (string.IsNullOrEmpty(_selectedPresetName) && presets.Count > 0)
                _selectedPresetName = presets[0];

            Menu.BeginPane("Preset Manager", "Save the full Tricks tab state: enabled tricks, replacements, and animation speed/clip overrides.");

            Menu.BeginToolbar();
            GUILayout.Label("Name", _toolbarLabel, GUILayout.Width(42f));
            _presetName = GUILayout.TextField(_presetName ?? string.Empty, Menu.UiSearchFieldStyle, GUILayout.Width(200f), GUILayout.Height(24f));

            if (Menu.PrimaryButton("Save Current", GUILayout.Width(110f), GUILayout.Height(24f)))
            {
                string saveName = string.IsNullOrWhiteSpace(_presetName)
                    ? $"Tricks {System.DateTime.Now:yyyy-MM-dd HH-mm}"
                    : _presetName.Trim();

                TrickPreset.SaveCurrent(saveName);
                _selectedPresetName = saveName;
                _presetName = string.Empty;
                _presetStatus = $"Saved preset '{saveName}'.";
                GUI.FocusControl(null);
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(_selectedPresetName);
            if (Menu.SecondaryButton("Load", GUILayout.Width(70f), GUILayout.Height(24f)))
            {
                if (TrickPreset.Apply(_selectedPresetName))
                {
                    _pendingRefresh = true;
                    _presetStatus = $"Loaded preset '{_selectedPresetName}'.";
                }
                else
                {
                    _presetStatus = $"Could not load preset '{_selectedPresetName}'.";
                }
            }

            if (Menu.DangerButton("Delete", GUILayout.Width(74f), GUILayout.Height(24f)))
            {
                string deleted = _selectedPresetName;
                if (TrickPreset.Delete(deleted))
                {
                    List<string> remaining = TrickPreset.GetAvailablePresets();
                    _selectedPresetName = remaining.Count > 0 ? remaining[0] : string.Empty;
                    _presetStatus = $"Deleted preset '{deleted}'.";
                }
                else
                {
                    _presetStatus = $"Could not delete preset '{deleted}'.";
                }
            }
            GUI.enabled = true;
            Menu.EndToolbar();

            GUILayout.Label(_presetStatus, Menu.UiMutedWrappedStyle);

            if (presets.Count == 0)
            {
                GUILayout.Label("No trick presets saved yet.", Menu.UiMutedWrappedStyle);
                Menu.EndPane();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Selected", _toolbarLabel, GUILayout.Width(58f));
            if (Menu.SecondaryButton("<", GUILayout.Width(28f), GUILayout.Height(24f)))
                StepSelectedPreset(presets, -1);

            GUILayout.Label(
                string.IsNullOrWhiteSpace(_selectedPresetName) ? "(none)" : _selectedPresetName,
                _rowButtonSelected,
                GUILayout.Height(24f),
                GUILayout.ExpandWidth(true));

            if (Menu.SecondaryButton(">", GUILayout.Width(28f), GUILayout.Height(24f)))
                StepSelectedPreset(presets, 1);
            GUILayout.EndHorizontal();

            Menu.EndPane();
        }

        private static void StepSelectedPreset(List<string> presets, int delta)
        {
            if (presets == null || presets.Count == 0)
                return;

            int index = presets.FindIndex(p => string.Equals(p, _selectedPresetName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                index = 0;
            else
            {
                index += delta;
                if (index < 0)
                    index = presets.Count - 1;
                if (index >= presets.Count)
                    index = 0;
            }

            _selectedPresetName = presets[index];
        }

        private static void DrawTrickListPane(float paneHeight)
        {
            float contentWidth = Mathf.Max(620f, Menu.windowRect.width - 180f);
            float mapWidth = Mathf.Clamp(contentWidth * 0.52f, 560f, 760f);
            GUILayout.BeginVertical(_card, GUILayout.Width(mapWidth), GUILayout.MinHeight(paneHeight));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Trick Map", _cardHeader);
            GUILayout.FlexibleSpace();
            GUILayout.Label("BMX only", _badge, GUILayout.Width(66));
            GUILayout.EndHorizontal();
            GUILayout.Label("Pick a direction row to edit, replace, or preview that trick.", _rowLabelRight);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Direction", _rowLabelLeft, GUILayout.Width(76));
            GUILayout.Label("Trick", _rowLabelLeft);
            GUILayout.Label("Action", _rowLabelLeft, GUILayout.Width(64));
            GUILayout.EndHorizontal();

            int visibleSetCount = 0;
            foreach (var set in rTrickSetData)
            {
                if (set == null) continue;

                var setKey = set.name;
                var setDisplay = CleanSetName(setKey);

                EnsureBackingArrays(set);
                if (!SetMatchesSearch(setKey, set)) continue;
                visibleSetCount++;

                GUILayout.BeginVertical(_setBlock);
                GUILayout.BeginHorizontal();
                bool currentFold = _foldouts.TryGetValue(setKey, out var f) ? f : true;
                bool newFold = FoldoutButton(currentFold, setDisplay, _setHeaderButton);
                _foldouts[setKey] = newFold;

                GUILayout.FlexibleSpace();
                GetEnabledCount(setKey, set, out int enabledCount, out int totalCount);
                GUILayout.Label($"{enabledCount}/{totalCount}", _badge, GUILayout.Width(46));
                GUILayout.Space(4);

                if (GUILayout.Button("Enable All", _pillOn, GUILayout.Width(78), GUILayout.Height(22)))
                {
                    ApplyAll(set, true);
                    SaveTricksToConfig();
                    _pendingRefresh = true;
                }

                if (GUILayout.Button("Disable All", _pill, GUILayout.Width(82), GUILayout.Height(22)))
                {
                    ApplyAll(set, false);
                    SaveTricksToConfig();
                    _pendingRefresh = true;
                }

                GUILayout.EndHorizontal();

                if (newFold)
                {
                    var flags = _trickEnabled[setKey];
                    int itemCount = Mathf.Min(set._dataList.Count, flags.Length);

                    for (int i = 0; i < itemCount; i++)
                    {
                        var obj = set._dataList[(Index)i] as UnityEngine.Object;
                        if (!IsBmxTrickObject(obj)) continue;

                        string dir = (i < DefaultDirectionLabels.Length) ? DefaultDirectionLabels[i] : $"Index {i}";
                        string trickName = CleanTrickName(obj);
                        if (!RowMatchesSearch(dir, trickName)) continue;

                        bool isSelected = IsSelectedTrick(setKey, i);
                        GUILayout.BeginHorizontal(isSelected ? _rowStripSelected : _rowStrip, GUILayout.Height(30));
                        var newFlag = GUILayout.Toggle(flags[i], GUIContent.none, GUILayout.Width(18), GUILayout.Height(22));
                        DrawDirectionCell(dir, isSelected);

                        using (new GUIContentColor(flags[i] ? Color.white : new Color(1f, 1f, 1f, 0.5f)))
                        {
                            string rowLabel = isSelected ? $"Selected: {trickName}" : trickName;
                            if (GUILayout.Button(rowLabel, isSelected ? _rowButtonSelected : _rowButton, GUILayout.MinWidth(150), GUILayout.Height(24)))
                                SelectTrick(set, i, dir, trickName);
                        }

                        if (GUILayout.Button("Replace", _miniBtn, GUILayout.Width(64), GUILayout.Height(24)))
                            OpenTrickPicker(set, i);

                        GUILayout.EndHorizontal();

                        if (newFlag != flags[i])
                        {
                            flags[i] = newFlag;
                            ApplyToggle(set, i, newFlag);
                            _pendingRefresh = true;
                            SaveTricksToConfig();
                        }
                    }
                }

                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            if (visibleSetCount == 0)
                GUILayout.Label($"No BMX tricks match \"{_uiSearch.Trim()}\".", _emptyState);

            GUILayout.EndVertical();
        }

        private static void DrawSelectedTrickPane(float paneHeight)
        {
            GUILayout.BeginVertical(_card, GUILayout.ExpandWidth(true), GUILayout.MinHeight(paneHeight));

            SyncTrickAnimationData animationData = GetSelectedAnimationData();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Selected Trick", _cardHeader);
            GUILayout.FlexibleSpace();
            bool nextPreview = GUILayout.Toggle(
                _previewEnabled,
                _previewEnabled ? "Preview On" : "Preview Off",
                _previewEnabled ? _pillOn : _pill,
                GUILayout.Width(110));
            if (nextPreview != _previewEnabled)
            {
                _previewEnabled = nextPreview;
                _nextPreviewFireTime = 0f;
                if (!_previewEnabled)
                    RestorePreviewState();
            }

            if (GUILayout.Button("Stop", _miniBtn, GUILayout.Width(62)))
            {
                _previewEnabled = false;
                RestorePreviewState();
            }
            GUILayout.EndHorizontal();

            if (animationData == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a trick", _emptyState);
                GUILayout.Label("Pick a BMX trick on the left to edit animation speeds, copy clips, or preview it.", _rowLabelRight);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(_selectedTrickDirection, _directionBadge, GUILayout.Width(82), GUILayout.Height(22));
            GUILayout.Label(_selectedTrickName, _cardHeader);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("Preview lifts and freezes the player, then fires the selected trick every 2 seconds.", _rowLabelRight);
            GUILayout.Space(8);

            string contextKey = $"{_selectedTrickSetKey}:{_selectedTrickSlot}";
            TrickAnimationEditor.DrawSelectedTrickPanel(
                animationData,
                contextKey,
                string.Empty);

            GUILayout.EndVertical();
        }

        private static void SelectTrick(TrickSetData set, int slot, string direction, string trickName)
        {
            TrickAnimationEditor.NotifyPreviewEnded();
            _selectedTrickSet = set;
            _selectedTrickSetKey = set?.name ?? string.Empty;
            _selectedTrickSlot = slot;
            _selectedTrickDirection = direction;
            _selectedTrickName = trickName;
            _needsAutoSelectTrick = false;
            _previewEnabled = true;
            _nextPreviewFireTime = 0f;

            string selectionKey = $"{_selectedTrickSetKey}:{_selectedTrickSlot}";
            if (!string.Equals(selectionKey, _lastPreviewSelectionKey, StringComparison.Ordinal))
            {
                _lastPreviewSelectionKey = selectionKey;
                Log.Msg($"[TricksPreview] Selected {_selectedTrickName} ({_selectedTrickDirection}) set={FindSetIndex(set)}, slot={slot}.");
            }
        }

        private static bool IsSelectedTrick(string setKey, int slot)
            => string.Equals(_selectedTrickSetKey, setKey, StringComparison.Ordinal) && _selectedTrickSlot == slot;

        private static SyncTrickAnimationData GetSelectedAnimationData()
        {
            if (_selectedTrickSet == null || _selectedTrickSet._dataList == null || _selectedTrickSlot < 0 || _selectedTrickSlot >= _selectedTrickSet._dataList.Count)
                return null;

            return _selectedTrickSet._dataList[(Index)_selectedTrickSlot] as SyncTrickAnimationData;
        }

        private static void EnsureTrickSelection()
        {
            if (!HasUsableTrickSetData())
                return;

            if (!_needsAutoSelectTrick && GetSelectedAnimationData() != null)
                return;

            SelectFirstAvailableTrick();
        }

        private static bool SelectFirstAvailableTrick()
        {
            if (rTrickSetData == null)
                return false;

            foreach (TrickSetData set in rTrickSetData)
            {
                if (set == null || set._dataList == null)
                    continue;

                EnsureBackingArrays(set);
                string setKey = set.name;
                if (!_trickEnabled.TryGetValue(setKey, out bool[] flags))
                    continue;

                int itemCount = Mathf.Min(set._dataList.Count, flags.Length);
                for (int i = 0; i < itemCount; i++)
                {
                    if (!flags[i])
                        continue;

                    UnityEngine.Object obj = set._dataList[(Index)i] as UnityEngine.Object;
                    if (!IsBmxTrickObject(obj))
                        continue;

                    string dir = i < DefaultDirectionLabels.Length ? DefaultDirectionLabels[i] : $"Index {i}";
                    SelectTrick(set, i, dir, CleanTrickName(obj));
                    return true;
                }
            }

            return false;
        }

        private static void OpenTrickPicker(TrickSetData set, int slot)
        {
            var btnRect = GUILayoutUtility.GetLastRect();
            Vector2 screenPos = GUIUtility.GUIToScreenPoint(new Vector2(btnRect.x, btnRect.y + btnRect.height));
            float popupX = Menu.previousWindowPosition.x + Menu.windowRect.width + 280f + 20f;
            float popupY = screenPos.y;

            _pickerRect = new Rect(popupX, popupY, 280f, 320f);
            _pickerOpen = true;
            _pickerSet = set;
            _pickerSlot = slot;
            _pickerScroll = Vector2.zero;
            _pickerSearch = "";

            if (_catalogNames == null || _catalogNames.Length == 0)
                BuildCatalog();
        }

        public static void Update()
        {
            if (!_tricksTabActive || !Menu.isOpen || Menu.currentTab != Menu.Tab.Tricks)
            {
                RestorePreviewState();
                TrickAnimationEditor.NotifyPreviewEnded();
                ProcessPendingNoBailRestore();
                return;
            }

            if (!_previewEnabled)
            {
                RestorePreviewState();
                TrickAnimationEditor.NotifyPreviewEnded();
                return;
            }

            SyncTrickAnimationData data = GetSelectedAnimationData();
            if (data == null)
            {
                RestorePreviewState();
                TrickAnimationEditor.NotifyPreviewEnded();
                return;
            }

            EnsurePreviewState();

            if (Time.unscaledTime >= _nextPreviewFireTime)
            {
                TrickAnimationEditor.NotifyPreviewEnded();
                _nextPreviewFireTime = Time.unscaledTime + TrickPreviewFireInterval;
                FirePreviewTrick(data);
            }
        }

        public static void OnTricksTabEntered()
        {
            _tricksTabActive = true;
            _tricksNoBailRestoreTime = -1f;
            _needsAutoSelectTrick = GetSelectedAnimationData() == null;
            EnableTricksNoBailOverride();
        }

        public static void OnTricksTabExited()
        {
            _tricksTabActive = false;
            _previewEnabled = false;
            RestorePreviewState();
            TrickAnimationEditor.NotifyPreviewEnded();
            ScheduleTricksNoBailRestore();
        }

        private static void EnableTricksNoBailOverride()
        {
            if (_tricksNoBailOverrideActive)
                return;

            _tricksNoBailUserValue = Config.misc.neverBail;
            _tricksNoBailOverrideActive = true;
            _tricksNoBailRestoreTime = -1f;
            Misc.SetTemporaryNeverBailOverride(true, true);
            Log.Msg($"[TricksPreview] No Bail forced while Tricks tab is active. userNoBail={_tricksNoBailUserValue}.");
        }

        private static void ScheduleTricksNoBailRestore()
        {
            if (!_tricksNoBailOverrideActive)
                return;

            _tricksNoBailRestoreTime = Time.unscaledTime + TrickNoBailExitGraceSeconds;
            Log.Msg($"[TricksPreview] No Bail restore scheduled in {TrickNoBailExitGraceSeconds:0.#}s after Tricks tab exit.");
        }

        private static void ProcessPendingNoBailRestore()
        {
            if (!_tricksNoBailOverrideActive || _tricksNoBailRestoreTime < 0f)
                return;

            if (Time.unscaledTime < _tricksNoBailRestoreTime)
                return;

            RestoreTricksNoBailOverride();
        }

        private static void RestoreTricksNoBailOverride()
        {
            if (!_tricksNoBailOverrideActive)
                return;

            _tricksNoBailOverrideActive = false;
            _tricksNoBailRestoreTime = -1f;
            Misc.SetTemporaryNeverBailOverride(false);
            Log.Msg($"[TricksPreview] No Bail restored after Tricks tab exit. userNoBail={Config.misc.neverBail}.");
        }

        private static void EnsurePreviewState()
        {
            if (!_previewPlayerOffsetApplied && Memory.customizableEntity != null)
            {
                _previewPlayerTransform = Memory.customizableEntity.transform;
                if (_previewPlayerTransform != null)
                {
                    _previewPlayerTransform.position += TrickPreviewPlayerOffset;
                    _previewPlayerOffsetApplied = true;
                }
            }

            if (!_previewSpringBodyCaptured && Memory.springBody != null)
            {
                _previewSpringBody = Memory.springBody;
                _previewSpringBodyOriginalIsKinematic = _previewSpringBody.isKinematic;
                _previewSpringBody.isKinematic = true;
                _previewSpringBodyCaptured = true;
            }

            _previewStateApplied = _previewPlayerOffsetApplied || _previewSpringBodyCaptured;
            InvokePreviewJump();
        }

        private static void RestorePreviewState()
        {
            TrickAnimationEditor.NotifyPreviewEnded();

            if (!_previewStateApplied)
                return;

            if (_previewPlayerOffsetApplied && _previewPlayerTransform != null)
                _previewPlayerTransform.position -= TrickPreviewPlayerOffset;

            if (_previewSpringBodyCaptured && _previewSpringBody != null)
                _previewSpringBody.isKinematic = _previewSpringBodyOriginalIsKinematic;

            _previewStateApplied = false;
            _previewPlayerOffsetApplied = false;
            _previewSpringBodyCaptured = false;
            _previewPlayerTransform = null;
            _previewSpringBody = null;
            _previewJumpInvoked = false;
            _lastPreviewLogKey = string.Empty;
        }

        private static void InvokePreviewJump()
        {
            if (_previewJumpInvoked)
                return;

            try
            {
                var onJump = Memory.animatedVehicleEventResponder?.OnJump;
                if (onJump != null)
                {
                    onJump.Invoke(1);
                    _previewJumpInvoked = true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TricksPreview] Failed to invoke jump setup: {ex.Message}");
                _previewJumpInvoked = true;
            }
        }

        private static void FirePreviewTrick(SyncTrickAnimationData data)
        {
            TrickControllerV2 controller = FindPreviewController();
            if (controller == null)
            {
                Log.Warning("[TricksPreview] Cannot preview trick because no TrickControllerV2 is loaded.");
                return;
            }

            int setId = FindSetIndex(_selectedTrickSet);
            int slotId = _selectedTrickSlot;
            string logKey = $"{setId}:{slotId}:{GetUnityObjectName(data)}:{Time.frameCount / 120}";

            try
            {
                InvokePreviewJump();
                bool fired = controller.Fire(data, setId, slotId);
                if (!fired)
                    fired = controller.TryBlendTo(data, setId, slotId);
                if (fired)
                    TrickAnimationEditor.NotifyPreviewTrick(data);

                if (!string.Equals(logKey, _lastPreviewLogKey, StringComparison.Ordinal))
                {
                    _lastPreviewLogKey = logKey;
                    Log.Msg($"[TricksPreview] Preview fired={fired} trick='{_selectedTrickName}' set={setId}, slot={slotId}.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TricksPreview] Failed to fire {_selectedTrickName}: {ex.Message}");
            }
        }

        private static TrickControllerV2 FindPreviewController()
        {
            try
            {
                TrickControllerV2[] controllers = UnityEngine.Object.FindObjectsOfType<TrickControllerV2>();
                if (controllers == null || controllers.Length == 0)
                    return null;

                return controllers[0];
            }
            catch
            {
                return null;
            }
        }

        private static int FindSetIndex(TrickSetData set)
        {
            if (set == null || rTrickSetData == null)
                return -1;

            for (int i = 0; i < rTrickSetData.Length; i++)
            {
                TrickSetData candidate = rTrickSetData[i];
                if (candidate == null)
                    continue;

                if (ReferenceEquals(candidate, set) || string.Equals(candidate.name, set.name, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static string GetUnityObjectName(UnityEngine.Object obj)
            => obj == null ? string.Empty : obj.name ?? string.Empty;

        public static void ReplaceTrick(TrickSetData set, int index, string trickName)
        {
            if (set == null || set._dataList == null) return;
            if (index < 0 || index >= set._dataList.Count) return;

            if (!_catalogIndexByName.TryGetValue(trickName, out int newIndex))
            {
                Log.Error($"ReplaceTrick: Trick '{trickName}' not found in catalog.");
                return;
            }

            var newTrick = _catalog[newIndex];
            if (!_configuredRefs.TryGetValue(set.name, out var configured) || configured.Length <= index)
            {
                EnsureBackingArrays(set);
                _configuredRefs.TryGetValue(set.name, out configured);
            }

            if (configured != null && index < configured.Length)
                configured[index] = newTrick;

            set._dataList[(Index)index] = newTrick;

            if (trickDictionary.TryGetValue(set.name, out var names) && index < names.Count)
                names[index] = CleanTrickName(newTrick);

            if (_trickEnabled.TryGetValue(set.name, out var flags) && index < flags.Length)
                flags[index] = newTrick != null;

            if (IsSelectedTrick(set.name, index))
            {
                _selectedTrickName = CleanTrickName(newTrick);
                _nextPreviewFireTime = 0f;
            }

            ForceRefreshTrickRuntime();
            SaveTricksToConfig();
            Log.Msg($"Replaced {set.name}[{index}] with {trickName}");
        }
        private static void BuildCatalog()
        {
            _catalog.Clear();
            _catalogIndexByName.Clear();

            var names = new List<string>();

            if (rTrickSetData != null)
            {
                foreach (var set in rTrickSetData)
                {
                    if (set == null || set._dataList == null) continue;

                    for (int i = 0; i < set._dataList.Count; i++)
                    {
                        var obj = set._dataList[(Index)i] as UnityEngine.Object;
                        if (obj == null) continue;
                        if (!IsBmxTrickObject(obj)) continue;

                        string cleanName = CleanTrickName(obj);


                        // skip dupes
                        if (_catalogIndexByName.ContainsKey(cleanName))
                            continue;

                        _catalogIndexByName[cleanName] = _catalog.Count;
                        _catalog.Add(obj);
                        names.Add(cleanName);
                    }
                }
            }

            _catalogNames = names.ToArray();
            Log.Msg($"BMX catalog built: {_catalogNames.Length} tricks available.");
        }
        public static void ResetCustomTricks()
        {
            if (Config.tricks.trickSets == null)
                Config.tricks.trickSets = new Dictionary<string, List<TrickEntry>>();

            if (rTrickSetData == null)
            {
                Config.tricks.trickSets.Clear();
                Config.Save();
                Log.Warning("Trick runtime data was unavailable; cleared saved trick overrides only.");
                return;
            }

            foreach (var set in rTrickSetData)
            {
                if (set == null) continue;

                // Reset the underlying data
                set.InitializeOrResizeTrickData();

                // Reset toggle flags to true
                var setKey = set.name;
                if (_trickEnabled.TryGetValue(setKey, out var flags))
                {
                    for (int i = 0; i < flags.Length; i++)
                        flags[i] = true;
                }

                // Restore original references into the data list
                if (_originalRefs.TryGetValue(setKey, out var originals))
                {
                    for (int i = 0; i < set._dataList.Count && i < originals.Length; i++)
                        set._dataList[(Index)i] = originals[i];

                    _configuredRefs[setKey] = (UnityEngine.Object?[])originals.Clone();

                    if (trickDictionary.TryGetValue(setKey, out var names))
                    {
                        for (int i = 0; i < names.Count && i < originals.Length; i++)
                            names[i] = CleanTrickName(originals[i]);
                    }
                }
            }

            Config.tricks.trickSets.Clear();
            SaveTricksToConfig();

            // Refresh runtime systems
            ForceRefreshTrickRuntime();

            Log.Msg("All trick sets reset: all toggles enabled and TrickData re-initialized.");
        }

        public static void SaveTricksToConfig()
        {
            if (rTrickSetData == null) return;

            var dict = new Dictionary<string, List<TrickEntry>>();
            foreach (var set in rTrickSetData)
            {
                if (set == null || set._dataList == null) continue;

                var entries = new List<TrickEntry>();
                _configuredRefs.TryGetValue(set.name, out var configured);
                _trickEnabled.TryGetValue(set.name, out var flags);

                for (int i = 0; i < set._dataList.Count; i++)
                {
                    var obj = set._dataList[(Index)i] as UnityEngine.Object;
                    var configuredObj = configured != null && i < configured.Length ? configured[i] : obj;
                    string name = CleanTrickName(configuredObj);
                    bool enabled = flags != null && i < flags.Length ? flags[i] : obj != null;

                    entries.Add(new TrickEntry { Name = name, Enabled = enabled });
                }

                dict[set.name] = entries;
            }

            Config.tricks.trickSets = dict;
            Config.Save();
            Log.Msg("Custom tricks (with enabled state) saved to config.");
        }

        public static void LoadTricksFromConfig()
        {
            if (rTrickSetData == null || Config.tricks.trickSets == null) return;
            if (_catalogIndexByName.Count == 0) BuildCatalog();

            foreach (var set in rTrickSetData)
            {
                if (set == null || set._dataList == null) continue;

                if (!Config.tricks.trickSets.TryGetValue(set.name, out var savedTricks)) continue;

                EnsureBackingArrays(set);
                if (!_configuredRefs.TryGetValue(set.name, out var configured))
                {
                    configured = new UnityEngine.Object?[set._dataList.Count];
                    _configuredRefs[set.name] = configured;
                }

                if (!_trickEnabled.TryGetValue(set.name, out var flags))
                {
                    flags = new bool[set._dataList.Count];
                    _trickEnabled[set.name] = flags;
                }

                for (int i = 0; i < set._dataList.Count && i < savedTricks.Count; i++)
                {
                    var entry = savedTricks[i];
                    UnityEngine.Object configuredTrick = null;
                    if (entry != null && _catalogIndexByName.TryGetValue(entry.Name, out int newIndex))
                    {
                        configuredTrick = _catalog[newIndex];
                    }

                    bool isEnabled = entry?.Enabled == true && configuredTrick != null;

                    if (i < configured.Length)
                        configured[i] = configuredTrick;

                    if (i < flags.Length)
                        flags[i] = isEnabled;

                    set._dataList[(Index)i] = isEnabled
                        ? configuredTrick
                        : null;

                    if (trickDictionary.TryGetValue(set.name, out var names) && i < names.Count)
                        names[i] = CleanTrickName(configuredTrick);
                }
            }

            ForceRefreshTrickRuntime();
            BuildTrickMenuDisplay();
            Log.Msg("Custom tricks (with enabled state) restored from config.");
        }

        public static string[] GetAvailableTrickNames()
        {
            if (_catalogNames == null || _catalogNames.Length == 0)
            {
                BuildCatalog();
            }

            if (_catalogNames != null && _catalogNames.Length > 0)
            {
                return _catalogNames
                    .Where(name => !string.IsNullOrWhiteSpace(name) && name != "(null)")
                    .Distinct(System.StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToArray();
            }

            return trickDictionary.Values
                .SelectMany(names => names)
                .Where(name => !string.IsNullOrWhiteSpace(name) && name != "(null)")
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToArray();
        }



        #endregion

        #region Private Helpers
        private static string NormalizeTitle(string title)
        {
            string s = title.Trim().ToLowerInvariant();
            s = s.Replace("/", " / ")
                .Replace("\\", " / ")
                .Replace("&", " and ")
                .Replace("+", " and ")
                .Replace(",", " ");
            while (s.Contains("  ")) s = s.Replace("  ", " ");

            // Expand combined patterns into explicit pairs *per noun*
            s = ExpandLeftRightCombos(s, "shoulder"); // e.g. "left/right shoulder" -> "left shoulder right shoulder"
            s = ExpandLeftRightCombos(s, "trigger");  // e.g. "left and right trigger" -> "left trigger right trigger"
            return s;
        }

        private static string ExpandLeftRightCombos(string s, string noun)
        {
            // patterns where noun comes after "left/right"
            string[] pats1 = {
                $"left and right {noun}", $"left right {noun}",
                $"left / right {noun}",   $"left & right {noun}",
                $"left + right {noun}",   $"left, right {noun}"
            };
            foreach (var p in pats1)
                if (s.Contains(p)) s = s.Replace(p, $"left {noun} right {noun}");

            // patterns where noun comes first
            string[] pats2 = {
                $"{noun} left and right", $"{noun} left right",
                $"{noun} left / right",   $"{noun} left & right",
                $"{noun} left + right",   $"{noun} left, right"
            };
            foreach (var p in pats2)
                if (s.Contains(p)) s = s.Replace(p, $"{noun} left {noun} right");

            // clean up extra spaces
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s;
        }

        // Parse a cleaned set title like "Left Shoulder Right Trigger" into glyph IDs
        private static readonly (string key, string glyph)[] _titleTokens =
        {
            ("left shoulder",  "xbox_lb"),
            ("right shoulder", "xbox_rb"),
            ("left trigger",   "xbox_lt"),
            ("right trigger",  "xbox_rt"),
            // add more if your titles include face buttons, e.g. ("button a","xbox_button_a")
        };
        

        // Draw the glyph sequence; fallback to the text title if nothing matched
        // Parse a cleaned set title (e.g., "Left Shoulder Right Trigger") into glyph IDs (LB, RB, etc.)
        private static List<string> TitleToGlyphs(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;

            // normalize & expand "left/right shoulder" style phrases to explicit pairs
            string s = NormalizeTitle(title);

            var hits = new List<(int idx, string id)>();

            // helper: add all occurrences of a PHRASE (keeps true order)
            void AddPhrase(string phrase, string id)
            {
                int start = 0;
                while (true)
                {
                    int i = s.IndexOf(phrase, start, StringComparison.Ordinal);
                    if (i < 0) break;
                    hits.Add((i, id));
                    start = i + phrase.Length;
                }
            }

            // helper: add occurrences of a WORD (bounded by spaces)
            void AddWord(string word, string id)
            {
                string padded = " " + s + " ";
                string needle = " " + word + " ";
                int start = 0;
                while (true)
                {
                    int i = padded.IndexOf(needle, start, StringComparison.Ordinal);
                    if (i < 0) break;
                    // subtract the padding so ordering is roughly original
                    hits.Add((Mathf.Max(0, i - 1), id));
                    start = i + needle.Length;
                }
            }

            // explicit phrases
            AddPhrase("left shoulder", "xbox_lb");
            AddPhrase("right shoulder", "xbox_rb");
            AddPhrase("left trigger", "xbox_lt");
            AddPhrase("right trigger", "xbox_rt");

            // shorthand / alt labels
            AddWord("lb", "xbox_lb");
            AddWord("rb", "xbox_rb");
            AddWord("lt", "xbox_lt");
            AddWord("rt", "xbox_rt");

            // PlayStation style (map to Xbox-equivalent glyphs you've got loaded)
            AddWord("l1", "xbox_lb");
            AddWord("r1", "xbox_rb");
            AddWord("l2", "xbox_lt");
            AddWord("r2", "xbox_rt");

            if (hits.Count == 0) return null;

            hits.Sort((a, b) => a.idx.CompareTo(b.idx));

            // de-dupe but preserve visual order
            var outIds = new List<string>(hits.Count);
            foreach (var h in hits)
                if (!outIds.Contains(h.id))
                    outIds.Add(h.id);

            return outIds.Count > 0 ? outIds : null;
        }


// Draw the glyph sequence; fallback to the text title if nothing matched
        private static void DrawSetHeaderGlyphs(string setTitle)
        {
            GUILayout.Label(setTitle, _cardHeader);
        }


        // --- DPI helper (add once, near your fields) ---
        private static float UIScale => Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.6f);

        // --- Robust loader: Texture2D OR Sprite; Resources OR already-loaded assets ---
        private static Texture2D GetGlyph(string resourceName)
        {
            if (GlyphCache.TryGetValue(resourceName, out var tex) && tex) return tex;

            // 1) Try Resources as Texture2D
            tex = Resources.Load<Texture2D>(resourceName);
            if (tex) return GlyphCache[resourceName] = tex;

            // 2) Try Resources as Sprite (use underlying texture)
            var spr = Resources.Load<Sprite>(resourceName);
            if (spr && spr.texture) return GlyphCache[resourceName] = spr.texture;

            // 3) Search already loaded assets by name (works in IL2CPP builds)
            var allTex = Resources.FindObjectsOfTypeAll<Texture2D>();
            for (int i = 0; i < allTex.Length; i++)
                if (string.Equals(allTex[i].name, resourceName, StringComparison.OrdinalIgnoreCase))
                    return GlyphCache[resourceName] = allTex[i];

            var allSpr = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < allSpr.Length; i++)
                if (string.Equals(allSpr[i].name, resourceName, StringComparison.OrdinalIgnoreCase) &&
                    allSpr[i].texture)
                    return GlyphCache[resourceName] = allSpr[i].texture;

            return null; // fallback will draw text
        }

        private static void DrawDirectionCell(string dir, bool selected = false)
        {
            GUILayout.Label(FormatDirectionLabel(dir), selected ? _directionBadgeSelected : _directionBadge, GUILayout.Width(74), GUILayout.Height(24));
        }

        private static string FormatDirectionLabel(string dir)
        {
            switch (dir)
            {
                case "UpRight": return "Up Right";
                case "DownRight": return "Down Right";
                case "DownLeft": return "Down Left";
                case "UpLeft": return "Up Left";
                default: return dir;
            }
        }



        private static void InitStylesIfNeeded()
        {
            if (_stylesInited && _styleRevision == Menu.styleRevision) return;
            _stylesInited = true;
            _styleRevision = Menu.styleRevision;

            _card = new GUIStyle(Menu.UiPanelStyle)
            {
                padding = new RectOffset(14, 14, 12, 14),
                margin = new RectOffset(0, 0, 4, 0)
            };

            _cardHeader = new GUIStyle(Menu.UiHeaderStyle)
            {
                richText = true,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };

            _pill = new GUIStyle(Menu.UiPillStyle)
            {
                padding = new RectOffset(10, 10, 3, 3),
                margin = new RectOffset(4, 0, 0, 0)
            };
            _pillOn = new GUIStyle(Menu.UiPillActiveStyle);

            _rowLabelLeft = new GUIStyle(Menu.UiRowLabelStyle)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 6, 2, 2)
            };
            _rowLabelRight = new GUIStyle(Menu.UiRowMutedLabelStyle)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 0, 2, 2)
            };
            _rowButton = new GUIStyle(Menu.UiRowButtonStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 2, 2),
                margin = new RectOffset(2, 2, 0, 0)
            };
            _rowButtonSelected = new GUIStyle(_rowButton)
            {
                fontStyle = FontStyle.Bold
            };
            Texture2D selectedButtonBg = Menu.MakeRoundedTex(64, 24, new Color(0.10f, 0.34f, 0.18f, 0.96f), 6, 1, new Color(0.24f, 0.86f, 0.44f, 0.68f));
            Texture2D selectedButtonHoverBg = Menu.MakeRoundedTex(64, 24, new Color(0.13f, 0.42f, 0.22f, 0.98f), 6, 1, new Color(0.34f, 1f, 0.56f, 0.78f));
            _rowButtonSelected.normal.background = selectedButtonBg;
            _rowButtonSelected.hover.background = selectedButtonHoverBg;
            _rowButtonSelected.active.background = selectedButtonBg;
            _rowButtonSelected.normal.textColor = new Color(0.88f, 1f, 0.91f, 1f);
            _rowButtonSelected.hover.textColor = Color.white;
            _rowButtonSelected.active.textColor = Color.white;

            _miniBtn = new GUIStyle(Menu.UiMiniButtonStyle)
            {
                fontSize = 11,
                padding = new RectOffset(7, 7, 3, 3),
                margin = new RectOffset(2, 2, 0, 0)
            };

            _badge = new GUIStyle(Menu.UiBadgeStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fixedHeight = 20,
                padding = new RectOffset(6, 6, 2, 2)
            };

            _searchField = new GUIStyle(Menu.UiSearchFieldStyle)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(9, 9, 3, 3),
                margin = new RectOffset(0, 8, 0, 0)
            };
            Texture2D searchBg = Menu.MakeRoundedTex(64, 24, new Color(0.06f, 0.065f, 0.075f, 0.96f), 6, 1, new Color(1f, 1f, 1f, 0.14f));
            _searchField.normal.background = searchBg;
            _searchField.hover.background = searchBg;
            _searchField.focused.background = searchBg;
            _searchField.active.background = searchBg;

            _toolbarLabel = new GUIStyle(Menu.UiMutedStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                padding = new RectOffset(0, 4, 2, 2)
            };

            _setBlock = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(8, 8, 8, 9),
                margin = new RectOffset(0, 0, 0, 8)
            };
            _setBlock.normal.background = Menu.MakeRoundedTex(64, 32, new Color(1f, 1f, 1f, 0.018f), 7, 1, new Color(1f, 1f, 1f, 0.045f));

            _setHeaderButton = new GUIStyle(Menu.UiHeaderStyle)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 8, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fixedHeight = 24f
            };

            _rowStrip = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(5, 5, 3, 3),
                margin = new RectOffset(0, 0, 1, 1)
            };
            _rowStrip.normal.background = Menu.MakeRoundedTex(64, 26, new Color(0.048f, 0.05f, 0.057f, 0.72f), 5, 1, new Color(1f, 1f, 1f, 0.045f));

            _rowStripSelected = new GUIStyle(_rowStrip);
            _rowStripSelected.normal.background = Menu.MakeRoundedTex(64, 30, new Color(0.06f, 0.22f, 0.12f, 0.72f), 6, 1, new Color(0.22f, 0.95f, 0.42f, 0.58f));

            _directionBadge = new GUIStyle(Menu.UiBadgeStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(6, 6, 2, 2)
            };
            _directionBadge.normal.textColor = new Color(0.95f, 0.9f, 0.86f, 1f);
            _directionBadge.normal.background = Menu.MakeRoundedTex(64, 22, new Color(0.085f, 0.073f, 0.064f, 0.96f), 6, 1, new Color(0.9f, 0.48f, 0.24f, 0.22f));

            _directionBadgeSelected = new GUIStyle(_directionBadge);
            _directionBadgeSelected.normal.textColor = new Color(0.9f, 1f, 0.92f, 1f);
            _directionBadgeSelected.normal.background = Menu.MakeRoundedTex(64, 24, new Color(0.08f, 0.30f, 0.16f, 0.98f), 6, 1, new Color(0.28f, 1f, 0.48f, 0.62f));

            _emptyState = new GUIStyle(Menu.UiMutedWrappedStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(16, 16, 10, 10)
            };
        }

        private static void EnsureBackingArrays(TrickSetData set)
        {
            if (set == null || set._dataList == null) return;
            var setKey = set.name;

            if (!_originalRefs.ContainsKey(setKey))
            {
                int count = set._dataList.Count;
                var originals = new UnityEngine.Object?[count];
                var flags     = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    var obj = set._dataList[(Index)i] as UnityEngine.Object;
                    originals[i] = obj;
                    flags[i] = obj != null;
                }
                _originalRefs[setKey] = originals;
                _configuredRefs[setKey] = (UnityEngine.Object?[])originals.Clone();
                _trickEnabled[setKey] = flags;
                if (!_foldouts.ContainsKey(setKey)) _foldouts[setKey] = true;
            }
            else if (!_configuredRefs.ContainsKey(setKey) &&
                     _originalRefs.TryGetValue(setKey, out var originals))
            {
                _configuredRefs[setKey] = (UnityEngine.Object?[])originals.Clone();
            }
        }

        private static bool HasUsableTrickSetData()
        {
            if (rTrickSetData == null || rTrickSetData.Length == 0) return false;

            for (int i = 0; i < rTrickSetData.Length; i++)
            {
                var set = rTrickSetData[i];
                if (set == null) continue;

                try
                {
                    if (set._dataList != null) return true;
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[Tricks] Ignoring stale TrickSetData reference: {ex.Message}");
                }
            }

            return false;
        }

        private static void EnsureTrickDataReadyForMenu()
        {
            if (HasUsableTrickSetData()) return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastTrickDataRefreshAttempt < 2f) return;
            _lastTrickDataRefreshAttempt = now;

            Log.Msg("[Tricks] No usable trick-set data in menu; refreshing trick data.");
            GrabTrickData();

            if (HasUsableTrickSetData() && Config.tricks.trickSets != null && Config.tricks.trickSets.Count > 0)
            {
                LoadTricksFromConfig();
            }
        }

        private static bool FoldoutButton(bool open, string title, GUIStyle style)
        {
            string caret = open ? "▼" : "▶";
            return GUILayout.Toggle(open, $"{caret}  <b>{title}</b>", style);
        }

        private static void DrawRowBackground(int index)
            => DrawRowBackground(index, false);

        private static void DrawRowBackground(int index, bool selected)
        {
            var r = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
            Color bg = selected
                ? new Color(0.9f, 0.42f, 0.2f, 0.28f)
                : (index % 2 == 0) ? new Color(1,1,1,0.06f) : new Color(1,1,1,0.03f);
            EditorishFill(r, bg);
            GUI.skin.label.CalcHeight(GUIContent.none, r.width); // keep layout happy
            GUI.BeginGroup(r);
            GUI.EndGroup();
            GUILayout.Space(-22); // pull back; we’ll draw actual contents next
        }

        // tiny immediate-mode rect fill without UnityEditor
        private static Texture2D _fillTex;
        private static void EditorishFill(Rect r, Color c)
        {
            if (_fillTex == null) { _fillTex = new Texture2D(1,1); _fillTex.SetPixel(0,0,Color.white); _fillTex.Apply(); }
            var prev = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, _fillTex);
            GUI.color = prev;
        }

        private static bool RowMatchesSearch(string dir, string trick)
        {
            if (string.IsNullOrWhiteSpace(_uiSearch)) return true;
            var s = _uiSearch.Trim();
            return dir.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                || trick.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool SetMatchesSearch(string setKey, TrickSetData set)
        {
            if (string.IsNullOrWhiteSpace(_uiSearch)) return true;
            // quick accept if set name matches
            if (CleanSetName(setKey).IndexOf(_uiSearch, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // else scan rows until we find one that matches
            if (set?._dataList == null) return false;
            int n = set._dataList.Count;
            for (int i = 0; i < n; i++)
            {
                var obj = set._dataList[(Index)i] as UnityEngine.Object;
                if (!IsBmxTrickObject(obj)) continue;
                if (RowMatchesSearch(i < DefaultDirectionLabels.Length ? DefaultDirectionLabels[i] : $"Index {i}",
                                     CleanTrickName(obj)))
                    return true;
            }
            return false;
        }

        private static void GetEnabledCount(string setKey, TrickSetData set, out int enabled, out int total)
        {
            enabled = 0; total = 0;
            if (set?._dataList == null) return;
            var flags = _trickEnabled[setKey];
            int count = Mathf.Min(set._dataList.Count, flags.Length);
            for (int i = 0; i < count; i++)
            {
                var obj = set._dataList[(Index)i] as UnityEngine.Object;
                if (!IsBmxTrickObject(obj)) continue;

                total++;
                if (flags[i]) enabled++;
            }
        }

        private static void SetAllFoldouts(bool open)
        {
            if (rTrickSetData == null) return;
            foreach (var s in rTrickSetData)
                if (s != null) _foldouts[s.name] = open;
        }

        private struct GUIContentColor : System.IDisposable
        {
            private readonly Color _prev;
            public GUIContentColor(Color c){ _prev = GUI.color; GUI.color = c; }
            public void Dispose(){ GUI.color = _prev; }
        }

        private static void HandleKeyboardShortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            // A = enable all, D = disable all, when mouse is over a card
            if (e.keyCode != KeyCode.A && e.keyCode != KeyCode.D) return;

            // naive hit test: find last card under mouse
            if (rTrickSetData == null) return;
            foreach (var set in rTrickSetData)
            {
                if (set == null) continue;
                // if card is open and mouse is inside current layout rect, trigger (kept simple)
                // (IMGUI doesn't give us an easy per-card rect without extra bookkeeping, so skip for brevity)
            }
        }

        private static TrickSetData[] FilterBmxSets(TrickSetData[] all)
        {
            var list = new List<TrickSetData>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                var n = s.name ?? "";
                if (n.IndexOf("BMX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("_TrickSetData_BMX", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    list.Add(s);
                }
            }
            return list.ToArray();
        }

        
        private static void BuildTrickMenuDisplay()
        {
            trickMenuDisplayItems.Clear();
            if (rTrickSetData == null || rTrickSetData.Length == 0) return;

            foreach (var trickSetData in rTrickSetData)
            {
                if (trickSetData == null) continue;

                // UPDATED: clean set name
                string displaySetName = CleanSetName(trickSetData.name);

                var directions = new List<string>();
                if (trickDictionary.TryGetValue(trickSetData.name, out var trickNames))
                {
                    for (int i = 0; i < trickNames.Count; i++)
                    {
                        string direction = (i < DefaultDirectionLabels.Length) ? DefaultDirectionLabels[i] : $"Index {i}";

                        // UPDATED: clean trick name from the cached list too
                        string displayTrickName = CleanTrickName(trickNames[i]);

                        directions.Add($"{direction} => {displayTrickName}");
                    }
                }
                else
                {
                    directions.Add("(No data found for this TrickSet)");
                }

                trickMenuDisplayItems.Add(new TrickMenuEntry
                {
                    TrickSetDisplayName = displaySetName,
                    TrickDirections = directions
                });
            }
        }

        
        private static string CleanTrickName(UnityEngine.Object obj)
            => obj == null ? "(null)" : CleanTrickName(obj.name);

        internal static bool IsBmxTrickObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return false;

            string raw = obj.name ?? string.Empty;
            if (raw.IndexOf("Scooter", StringComparison.OrdinalIgnoreCase) >= 0)
                return IsAllowedScooterNamedBmxTrick(CleanTrickName(raw));

            return true;
        }

        private static bool IsAllowedScooterNamedBmxTrick(string cleanName)
        {
            if (string.IsNullOrEmpty(cleanName))
                return false;

            return cleanName.StartsWith("Bri Flip", StringComparison.OrdinalIgnoreCase)
                || cleanName.StartsWith("Grand Slam", StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanTrickName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "(null)";

            // Common suffixes seen in your screenshots/builds
            string[] suffixes = {
                "_SyncAnimatorData_PlayerScooter",
                "_SyncAnimatorData_PlayerBMX",
                "_SyncAnimatorData_Player",
                "_SyncAnimatorData",
                "_SyncTrickAnimationData_PlayerScooter",
                "_SyncTrickAnimationData_PlayerBMX",
                "_SyncTrickAnimationData",
                // typo variants you mentioned
                "_SyncAnimationData_PlayerScooter",
                "_SyncAnimationData_PlayerBMX",
                "_SyncAnimationData"
            };

            foreach (var s in suffixes)
                if (raw.EndsWith(s, System.StringComparison.OrdinalIgnoreCase))
                    return raw.Substring(0, raw.Length - s.Length).TrimEnd('_', ' ');

            return raw;
        }

        private static string CleanSetName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            foreach (var s in new[] { "_TrickSetData_BMX", "_TrickSetData_Scooter" })
                if (raw.EndsWith(s, System.StringComparison.OrdinalIgnoreCase))
                    return raw.Substring(0, raw.Length - s.Length).TrimEnd('_', ' ');
            return raw;
        }

        private static Rect ClampToScreen(Rect r, float padding)
        {
            float x = Mathf.Clamp(r.x, padding, Screen.width - r.width - padding);
            float y = Mathf.Clamp(r.y, padding, Screen.height - r.height - padding);
            r.x = x; r.y = y;
            return r;
        }

        #endregion


    }
}
