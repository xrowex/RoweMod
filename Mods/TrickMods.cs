using System.Reflection;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System;
using Il2CppMashBox.Core.Runtime.TrickSystem;
//using Il2CppMG_Core.C_R_I_D.Animation_System.Animancer_Test;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using Il2CppSystem;
using AppDomain = System.AppDomain; // for Il2CppSystem.Type
using ManagedType = System.Type;
using Il2CppTypeRef = Il2CppSystem.Type;
using Math = System.Math;

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
                var newVal = enabled ? originals[index] : null;
                set._dataList[(Index)index] = newVal;

                // UPDATED: keep the UI list in sync, but with cleaned names
                if (trickDictionary.TryGetValue(setKey, out var names) && index < names.Count)
                    names[index] = CleanTrickName(newVal as UnityEngine.Object);

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
            if (!_trickEnabled.TryGetValue(setKey, out var flags)) return;

            int count = Math.Min(set._dataList.Count, Math.Min(originals.Length, flags.Length));
            for (int i = 0; i < count; i++)
            {
                flags[i] = enableAll;
                set._dataList[(Index)i] = enableAll ? originals[i] : null;
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


        public static void GrabTrickData()
        {
            Log.Msg("GrabTrickData() called...");

            rTrickSetData = Resources.FindObjectsOfTypeAll<TrickSetData>();
            rTrickSystemBrain = GameObject.FindObjectOfType<TrickSystemBrain>();

            trickDictionary.Clear();
            trickMenuDisplayItems.Clear();

            if (rTrickSetData != null && rTrickSetData.Length > 0)
            {
                Log.Msg($"TrickSetData Found: {rTrickSetData.Length}");

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

                        // Cache originals for this set (so we can null/restore later)
                        var originals = new UnityEngine.Object?[count];
                        var enabled = new bool[count];

                        for (int i = 0; i < count; i++)
                        {
                            var item = trickSetData._dataList[(Index)i];
                            var obj = item as UnityEngine.Object;
                            originals[i] = obj;
                            enabled[i] = obj != null; // default: enabled if it exists

                            // Display name fallback
                            trickNames.Add(CleanTrickName(obj));
                        }

                        _originalRefs[trickSetData.name] = originals;
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
                Log.Msg("No TrickSetData objects found!");
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

            BuildTrickMenuDisplay(); // optional (kept if you still want the prebuilt view elsewhere)
        }


        public static void DrawTrickMenu()
        {
            GUILayout.Label("=== Trick Menu ===");

            if (rTrickSetData == null || rTrickSetData.Length == 0)
            {
                GUILayout.Label("No Trick Sets found...");
                return;
            }

            foreach (var set in rTrickSetData)
            {
                if (set == null) continue;

                string setKey = set.name;
                // UPDATED: clean the set display name
                string displaySetName = CleanSetName(setKey);

                if (!_trickEnabled.ContainsKey(setKey) || !_originalRefs.ContainsKey(setKey))
                {
                    if (set._dataList != null)
                    {
                        int count = set._dataList.Count;
                        var originals = new UnityEngine.Object?[count];
                        var flags = new bool[count];
                        for (int i = 0; i < count; i++)
                        {
                            var objInit = set._dataList[(Index)i] as UnityEngine.Object;
                            originals[i] = objInit;
                            flags[i] = objInit != null;
                        }

                        _originalRefs[setKey] = originals;
                        _trickEnabled[setKey] = flags;
                    }
                }

                GUILayout.Space(6);
                GUILayout.Label($"Trick Set: {displaySetName}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Enable All", GUILayout.Width(110))) ApplyAll(set, true);
                if (GUILayout.Button("Disable All", GUILayout.Width(110))) ApplyAll(set, false);
                GUILayout.EndHorizontal();

                if (set._dataList == null)
                {
                    GUILayout.Label("  (No data list)");
                    continue;
                }

                var flagsRef = _trickEnabled[setKey];
                int itemCount = Mathf.Min(set._dataList.Count, flagsRef.Length);

                for (int i = 0; i < itemCount; i++)
                {
                    string direction = (i < DefaultDirectionLabels.Length) ? DefaultDirectionLabels[i] : $"Index {i}";

                    // UPDATED: clean trick name
                    var obj = set._dataList[(Index)i] as UnityEngine.Object;
                    string trickName = CleanTrickName(obj);

                    GUILayout.BeginHorizontal();
                    bool newFlag = GUILayout.Toggle(flagsRef[i], GUIContent.none, GUILayout.Width(20));
                    GUILayout.Label($"{direction} ⇒ {trickName}");
                    GUILayout.EndHorizontal();

                    if (newFlag != flagsRef[i])
                    {
                        flagsRef[i] = newFlag;
                        ApplyToggle(set, i, newFlag);
                    }
                }
            }
        }



        #endregion

        #region Private Helpers
        
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


        #endregion
        

    }
}
