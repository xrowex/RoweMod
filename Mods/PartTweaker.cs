using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using rowemod.Utils;
using UnityEngine;
using System.Linq;
using Il2CppMashBox.Addons.ContentManagment;
using Il2CppMashBox.BMX_Physics_Development;

namespace rowemod.Mods
{
    public class PartTweaker
    {
        public static Transform seatPostAnchor;
        public static Transform seatAnchor;
        public static Transform barsAnchor;
        public static Transform[] pegsAnchor; // Fix: Correctly declare pegsAnchor as an array of Transform  
        private static float lastBarPitch, lastBarScale, lastSeatHeight, lastSeatPitch, lastPegLength;
        public enum PartTypeTab
        {
            Bars,
            Frame
        }

        private static PartTypeTab currentPartTab = PartTypeTab.Bars;

        private static int selectedBarIndex = 0;
        public static bool barListInitialized = false;
        private static string[] barNames;
        private static List<GameObject> barPrefabs = new();
        private static Vector2 barScrollPos = Vector2.zero;

        private static int selectedFrameIndex = 0;
        public static bool frameListInitialized = false;
        private static string[] frameNames;
        private static List<GameObject> framePrefabs = new();
        private static Vector2 frameScrollPos = Vector2.zero;
        public static void FindParts()
        {
            Log.Msg("FindParts called.");

            if (Memory.rMbCharacter == null)
            {
                Log.Error("rMbCharacter is null. Cannot search for anchors.");
                return;
            }

            // Try finding the SeatPost_Anchor (any active one)  
            Log.Msg("Searching for SeatPost_Anchor transforms...");
            var allSeatPosts = UnityEngine.Object.FindObjectsOfType<Transform>()
                .Where(t => t.name == "SeatPost_Anchor" && t.parent != null && t.parent.gameObject.activeInHierarchy)
                .ToList();

            Log.Msg($"Found {allSeatPosts.Count} SeatPost_Anchor(s).");
            if (allSeatPosts.Count > 0)
            {
                seatPostAnchor = allSeatPosts[0];
                Log.Msg($"SeatPost_Anchor found: {seatPostAnchor.name} in {seatPostAnchor.parent.name}");
            }
            else
            {
                Log.Error("No active SeatPost_Anchor found.");
            }

            // Find Seat_Anchor deeply under rMbCharacter  
            Log.Msg("Searching for Seat_Anchor using FindDeepChild...");
            seatAnchor = Memory.rMbCharacter.transform.FindDeepChild("Seat_Anchor");
            if (seatAnchor != null)
            {
                Log.Msg("Seat_Anchor found using FindDeepChild.");
            }
            else
            {
                Log.Error("Seat_Anchor not found under rMbCharacter.");
            }

            // Find Bars_Anchor the same way  
            Log.Msg("Searching for Bars_Anchor using FindDeepChild...");
            barsAnchor = Memory.rMbCharacter.transform.FindDeepChild("Bars_Anchor");
            if (barsAnchor != null)
            {
                Log.Msg("Bars_Anchor found.");
            }
            else
            {
                Log.Error("Bars_Anchor not found.");
            }

            // Fix: Correctly assign pegsAnchor as an array of Transform objects  
            Log.Msg("Searching for Pegs using FindDeepChild...");
            pegsAnchor = Memory.rMbCharacter.transform.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "Pegs")
                .ToArray();

            if (pegsAnchor != null && pegsAnchor.Length > 0)
            {
                Log.Msg($"Pegs found: {pegsAnchor.Length} peg(s).");
            }
            else
            {
                Log.Error("Pegs not found.");
            }
        }

        private static float barRotationAngle = 0f;
        private static float seatHeight = 0.1f;      // Y position  
        private static float seatRotationX = 350f;
        private static float pegLength = 1.0f;
        public static void DrawPartTweaker()
        {
            if (seatAnchor == null || seatPostAnchor == null || barsAnchor == null)
            {
                FindParts();
            }

            if (seatPostAnchor != null)
            {
                GUILayout.Label("Seat Height", Menu.coloredBoxStyle);
                Menu.ModernSlider("Height", ref seatHeight, 0.0f, 0.3f);

                // Move the seat post up/down  
                seatPostAnchor.localPosition = new Vector3(
                    seatPostAnchor.localPosition.x,
                    seatHeight,
                    seatPostAnchor.localPosition.z
                );
                Config.bike.seatHeight = seatHeight;

                GUILayout.Space(10);
                GUILayout.Label("Seat Tilt", Menu.coloredBoxStyle);
                Menu.ModernSlider("Rotation X", ref seatRotationX, 330f, 379f); // 379 wraps around to 19  

                if (seatAnchor != null)
                {
                    // Update seat tilt  
                    Quaternion newRotation = Quaternion.Euler(seatRotationX % 360f, 0f, 0f);
                    seatAnchor.localRotation = newRotation;
                    Config.bike.seatPitch = seatRotationX;

                }
                else
                {
                    Log.Error("Skipping Seat_Anchor updates: seatAnchor is null.");
                }

                Memory.customizableEntity.RelaySnap();
            }

            if (barsAnchor != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Bars Rotation", Menu.coloredBoxStyle);
                Menu.ModernSlider("Rotation", ref barRotationAngle, -45f, 45f);
                barsAnchor.localRotation = Quaternion.Euler(barRotationAngle, 0f, 0f);
                Config.bike.barPitch = barRotationAngle;

                Memory.customizableEntity.RelaySnap();
            }
            if(pegsAnchor != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Peg Length", Menu.coloredBoxStyle);
                Menu.ModernSlider("Length", ref pegLength, 0f, 3f);
                foreach(var peg in pegsAnchor)
                {
                    peg.localScale = new Vector3(pegLength, peg.localScale.y, peg.localScale.z);
                }
                Config.bike.pegLength = pegLength;
            }
        }
        
        public static void UpdatePartTransforms()
        {
            Log.Msg("Updating part transforms...");
            if (Memory.customizableEntity == null) return;

            if (barsAnchor != null)
            {
                barsAnchor.localRotation = Quaternion.Euler(Config.bike.barPitch, 0f, 0f);
                if (Config.bike.barScale <= 0)
                    barsAnchor.localScale = Vector3.one;
                else
                    barsAnchor.localScale = Vector3.one * Config.bike.barScale;
            }
            else
            {
                Log.Error("Skipping Bars_Anchor updates: barsAnchor is null.");
            }

            if (seatPostAnchor != null)
            {
                // Move the seat post up/down  
                seatPostAnchor.localPosition = new Vector3(
                    seatPostAnchor.localPosition.x,
                    Config.bike.seatHeight,
                    seatPostAnchor.localPosition.z
                );

                if (seatAnchor != null)
                {
                    // Update seat tilt  
                    Quaternion newRotation = Quaternion.Euler(Config.bike.seatPitch % 360f, 0f, 0f);
                    seatAnchor.localRotation = newRotation;
                }
                else
                {
                    Log.Error("Skipping Seat_Anchor updates: seatAnchor is null.");
                }
            }
            

            if (pegsAnchor != null)
            {
                foreach (var peg in pegsAnchor)
                {
                    peg.localScale = new Vector3(Config.bike.pegLength, peg.localScale.y, peg.localScale.z);
                }
            }
            
            // Update cached values
            lastBarPitch = Config.bike.barPitch;
            lastBarScale = Config.bike.barScale;
            lastSeatHeight = Config.bike.seatHeight;
            lastSeatPitch = Config.bike.seatPitch;
            lastPegLength = Config.bike.pegLength;

            Memory.customizableEntity.RelaySnap();
        }
        public static System.Collections.IEnumerator DelayedUpdatePartTransforms()
        {
            yield return new WaitForSeconds(4f);
            FindParts();
            UpdatePartTransforms();
        }
        
        ///CUSTOM PART LOADING AND LAYOUT
        private static void DrawPartTab(string label, List<GameObject> prefabs, string[] names, ref int selectedIndex, ref Vector2 scrollPos, Action<GameObject> onSelect)
        {
            GUILayout.Label(label, Menu.labelStyle);
    
            if (names == null || names.Length == 0)
            {
                GUILayout.Label("No prefabs found.", Menu.labelStyle);
                return;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            for (int i = 0; i < names.Length; i++)
            {
                if (GUILayout.Button($"<b>{names[i]}</b>", Menu.highQualityButtonStyle))
                {
                    selectedIndex = i;
                    GameObject selected = prefabs[i];
                    onSelect?.Invoke(selected);
                }
            }
            GUILayout.EndScrollView();
        }
        public static void DrawPartSelectorUI()
        {
            DrawPartTypeTabs();

            switch (currentPartTab)
            {
                case PartTypeTab.Bars:
                    EnsureBarsLoaded();
                    DrawPartTab("Select Bars:", barPrefabs, barNames, ref selectedBarIndex, ref barScrollPos, TryReplaceBars);
                    break;

                case PartTypeTab.Frame:
                    EnsureFramesLoaded();
                    DrawPartTab("Select Frame:", framePrefabs, frameNames, ref selectedFrameIndex, ref frameScrollPos, TryReplaceFrame);
                    break;

            }
        }
        private static void EnsureBarsLoaded()
        {
            if (barListInitialized && barPrefabs.Count > 0)
                return;

            barPrefabs = Memory.prefabList
                .Where(p => p != null && p.name.ToLower().StartsWith("bars"))
                .ToList();

            barNames = barPrefabs.Select(p =>
            {
                string bundleName = Memory.prefabToBundleMap.TryGetValue(p, out var bundle) ? bundle : "UnknownBundle";
                return $"{p.name} ({bundleName})";
            }).ToArray();

            barListInitialized = true;
            Log.Msg($"[Bars] Loaded {barPrefabs.Count} bar prefabs.");
        }

        public static void TryReplaceBars(GameObject prefab)
        {
            var slot = Memory.rMbCharacter.transform.FindDeepChild("Bars")?.GetComponent<EquipSlotVehicle>();
            if (slot == null)
            {
                Log.Warning("[Bars] Could not find Bars EquipSlot.");
                return;
            }

            Log.Msg($"[Bars] Instantiating: {prefab.name}");
            slot.Equip(prefab);

            Memory.lastEquippedBars = prefab;
            Config.bike.lastLoadedBars = prefab.name;
            Config.bike.bikeMaterials.Remove("Bars");
            Config.Save();

            FindParts();
            UpdatePartTransforms();
        }
        private static void EnsureFramesLoaded()
        {
            if (frameListInitialized && framePrefabs.Count > 0)
                return;

            framePrefabs = Memory.prefabList
                .Where(p => p != null && p.name.ToLower().StartsWith("frame"))
                .ToList();

            frameNames = framePrefabs.Select(p => p.name).ToArray();
            frameListInitialized = true;
            Log.Msg($"[Frame] Loaded {framePrefabs.Count} frame prefabs.");
        }

        public static void TryReplaceFrame(GameObject prefab)
        {
            var slot = Memory.rMbCharacter.transform
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.ToLower() == "frame" && t.GetComponent<EquipSlotVehicle>() != null)?
                .GetComponent<EquipSlotVehicle>();

            if (slot == null)
            {
                Log.Warning("[Frame] Could not find Frame EquipSlot.");
                return;
            }

            Log.Msg($"[Frame] Instantiating: {prefab.name}");
            slot.Equip(prefab);

            Memory.lastEquippedFrame = prefab;
            Config.bike.lastLoadedFrame = prefab.name;
            Config.bike.bikeMaterials.Remove("Frame");
            Config.Save();

            FindParts();
            UpdatePartTransforms();
        }
        public static void LoadSavedBars()
        {
            if (!string.IsNullOrEmpty(Config.bike.lastLoadedBars))
            {
                var savedBar = Memory.prefabList.FirstOrDefault(p => p != null && p.name == Config.bike.lastLoadedBars);
                if (savedBar != null)
                {
                    TryReplaceBars(savedBar);
                    Memory.lastEquippedBars = savedBar;
                    Log.Msg($"[Bars] Loaded saved bar: {savedBar.name}");
                }
                else
                {
                    Log.Warning($"[Bars] Could not find saved bar: {Config.bike.lastLoadedBars}");
                }
            }
        }

        public static void LoadSavedFrame()
        {
            if (!string.IsNullOrEmpty(Config.bike.lastLoadedFrame))
            {
                var savedFrame = Memory.prefabList.FirstOrDefault(p => p != null && p.name == Config.bike.lastLoadedFrame);
                if (savedFrame != null)
                {
                    TryReplaceFrame(savedFrame);
                    Memory.lastEquippedFrame = savedFrame;
                    Log.Msg($"[Frames] Loaded saved frame: {savedFrame.name}");
                }
                else
                {
                    Log.Warning($"[Frames] Could not find saved frame: {Config.bike.lastLoadedFrame}");
                }
            }
        }
        private static void DrawPartTypeTabs()
        {
            GUILayout.BeginHorizontal();

            foreach (PartTypeTab tab in Enum.GetValues(typeof(PartTypeTab)))
            {
                GUIStyle style = currentPartTab == tab ? Menu.activeTabButtonStyle : Menu.highQualityButtonStyle;
                if (GUILayout.Button($"<b>{tab}</b>", style, GUILayout.Height(30), GUILayout.Width(100)))
                {
                    currentPartTab = tab;
                }
            }

            GUILayout.EndHorizontal();
        }
        
        
    }
}
