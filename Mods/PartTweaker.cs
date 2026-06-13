using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using rowemod.Utils;
using UnityEngine;
using System.Linq;
using Il2CppMashBox.Addons.ContentManagment;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Development.RandD.PlayFabTesting;

namespace rowemod.Mods
{
    public class PartTweaker
    {
        public static Transform seatPostAnchor;
        public static Transform seatAnchor;
        public static Transform barsAnchor;
        public static Transform forkAnchor;
        public static Anchor[] gripsEquipSlots, barEndEquipSlots;
        public static Transform sprocketAnchor;
        public static Transform[] pegsAnchor; // Fix: Correctly declare pegsAnchor as an array of Transform 
        public static List<Transform> wheelMeshes;
        public static EquipSlotVehicle[] pegsEquipSlots;
        public static GameObject FrontLeftPeg, FrontRightPeg, RearLeftPeg, RearRightPeg;
        private static float lastBarPitch, lastBarScale, lastSeatHeight, lastSeatPitch;
        public enum PartTypeTab
        {
            Bars,
            Frame,
            Stem
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
        
        private static int selectedSprocketIndex = 0;
        public static bool sprocketListInitialized = false;
        private static string[] sprocketNames;
        private static List<GameObject> sprocketPrefabs = new();
        private static Vector2 sprocketScrollPos = Vector2.zero;
        
        private static int selectedStemIndex = 0;
        public static bool stemListInitialized = false;
        private static string[] stemNames;
        private static List<GameObject> stemPrefabs = new();
        private static Vector2 stemScrollPos = Vector2.zero;

        
        public struct PegData
        {
            public GameObject Peg;
            public EquipSlotVehicle EquipSlot;
            public bool IsFrontLeft;
            public bool IsFrontRight;
            public bool IsRearLeft;
            public bool IsRearRight;
            public bool IsEnabled;
            public GameObject Parent;
        }
        
        private static List<PegData> pegData = new();
        
        private static TireTest[] tireTest;
        
        private static void RefreshPegData()
        {
            pegData.Clear();

            if (pegsEquipSlots == null || pegsEquipSlots.Length == 0)
            {
                Log.Warning("RefreshPegData: pegsEquipSlots is empty.");
                return;
            }

            FrontLeftPeg = FrontRightPeg = RearLeftPeg = RearRightPeg = null;

            foreach (var slot in pegsEquipSlots)
            {
                if (slot == null) continue;

                // Parent is what you were already using as “the peg object”
                var parentGo = slot.transform != null && slot.transform.parent != null
                    ? slot.transform.parent.gameObject
                    : slot.gameObject;

                // Use both names to be resilient
                var slotName = slot.name ?? "";
                var parentName = parentGo != null ? parentGo.name : "";
                var key = (slotName + " " + parentName);

                bool isFL = key.Contains("FrontLeft")  || key.Contains("BMX_Peg_FrontLeft");
                bool isFR = key.Contains("FrontRight") || key.Contains("BMX_Peg_FrontRight");
                bool isRL = key.Contains("RearLeft")   || key.Contains("BMX_Peg_RearLeft");
                bool isRR = key.Contains("RearRight")  || key.Contains("BMX_Peg_RearRight");

                var data = new PegData
                {
                    EquipSlot = slot.GetComponentInChildren<EquipSlotVehicle>(),
                    Parent = parentGo,
                    Peg = parentGo,
                    IsFrontLeft = isFL,
                    IsFrontRight = isFR,
                    IsRearLeft = isRL,
                    IsRearRight = isRR,
                };

                data.IsEnabled = GetConfigEnabled(data);

                pegData.Add(data);

                // Optional: keep your old globals populated for compatibility
                if (isFL) FrontLeftPeg = parentGo;
                else if (isFR) FrontRightPeg = parentGo;
                else if (isRL) RearLeftPeg = parentGo;
                else if (isRR) RearRightPeg = parentGo;

                Log.Msg($"PegData: {slotName} (parent: {parentName}) FL:{isFL} FR:{isFR} RL:{isRL} RR:{isRR} Enabled:{data.IsEnabled}");
            }
        }

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

            gripsEquipSlots = Memory.rMbCharacter.transform.FindDeepChild("Left Grip")
                .GetComponentsInChildren<Anchor>()
                .Concat(Memory.rMbCharacter.transform.FindDeepChild("Right Grip")
                    .GetComponentsInChildren<Anchor>())
                .ToArray();
                
            if (gripsEquipSlots != null && gripsEquipSlots.Length > 0)
            {
                Log.Msg($"Grips found: {gripsEquipSlots.Length} grip(s).");
            }
            else
            {
                Log.Error("Grips not found.");
            }
            
            
            wheelMeshes = UnityEngine.Object.FindObjectsOfType<Transform>()
                .Where(t => t.name == "Wheel Mesh" && t.parent != null && t.parent.gameObject.activeInHierarchy)
                .ToList();
            if (wheelMeshes != null && wheelMeshes.Count > 0)
            {
                Log.Msg($"WheelMeshes found: {wheelMeshes.Count} wheelMesh(es).");
            }
            else
            {
                Log.Error("WheelMeshes not found.");
            }
            
            barEndEquipSlots = Memory.rMbCharacter.transform.FindDeepChild("Left Bar End")
                .GetComponentsInChildren<Anchor>()
                .Concat(Memory.rMbCharacter.transform.FindDeepChild("Right Bar End")
                    .GetComponentsInChildren<Anchor>())
                .ToArray();
            if (barEndEquipSlots != null)
            {
                Log.Msg($"BarEnds found: {barEndEquipSlots.Length} barEnd(s).");
            }
            else
            {
                Log.Error("BarEnds not found.");
            }
            
            if (barEndEquipSlots != null && barEndEquipSlots.Length > 0)
                Log.Msg($"BarEnds found: {barEndEquipSlots.Length} barEnd(s).");
            else
                Log.Error("BarEnds not found.");
            
            
            //barEndAnchors = Memory.customizableEntity._equipSlots.Where(s => s._subType == "barend").Select(s => s.transform).ToArray();
            
            
            // Find Seat_Anchor deeply under rMbCharacter
            Log.Msg("Searching for Sprocket_Anchor using FindDeepChild...");
            sprocketAnchor = Memory.rMbCharacter.transform.FindDeepChild("Sprocket_Anchor");
            if (sprocketAnchor != null)
            {
                Log.Msg("Sprocket_Anchor found using FindDeepChild.");
            }
            else
            {
                Log.Error("Seat_Anchor not found under rMbCharacter.");
            }


            // Find Bars_Anchor the same way  
            Log.Msg("Searching for Bars_Anchor using FindDeepChild...");
            barsAnchor = Memory.rMbCharacter.transform.FindDeepChild("Bars_Anchor");
            if (barsAnchor == null) barsAnchor = Memory.rMbCharacter.transform.FindDeepChild("Bars");

            if (barsAnchor != null)
            {
                Log.Msg($"Bars_Anchor found: {barsAnchor.name}");
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
            
            Log.Msg("Searching for Forks...");
            forkAnchor = Memory.rMbCharacter.transform.FindDeepChild("Forks") 
                         ?? Memory.rMbCharacter.transform.FindDeepChild("Fork")
                         ?? Memory.rMbCharacter.transform.FindDeepChild("Fork_Anchor")
                         ?? Memory.rMbCharacter.transform.FindDeepChild("Forks_Anchor");

            if (forkAnchor == null)
            {
                var forkSlot = Memory.rMbCharacter.transform.GetComponentsInChildren<EquipSlotVehicle>(true)
                    .FirstOrDefault(s => s.name.ToLower().Contains("fork"));
                if (forkSlot != null) forkAnchor = forkSlot.transform;
            }

            if (forkAnchor == null)
            {
                Log.Error("Forks not found.");
            }
            else
            {
                Log.Msg($"Forks found: {forkAnchor.name}");
            }

            tireTest = UnityEngine.Object.FindObjectsOfType<TireTest>();
            if (tireTest != null)
            {
                Log.Msg($"TireTest found: {tireTest.Length} tire(s).");
            }
            else
            {
                Log.Error("TireTest not found.");
            }
            
            pegsEquipSlots = Memory.rMbCharacter.transform
                .GetComponentsInChildren<EquipSlotVehicle>(true)
                .Where(s => s._subType == "peg")
                .ToArray();

            if (pegsEquipSlots != null && pegsEquipSlots.Length > 0)
            {
                Log.Msg($"Pegs found: {pegsEquipSlots.Length} peg(s).");
            }
            else
            {
                Log.Error("Pegs not found.");
            }
            
            RefreshPegData();
            ApplyPegEnabledFromConfig();
        }


        public static void DrawPartTweaker()
        {
            bool changed = false;

            if (seatPostAnchor != null)
            {
                GUILayout.Label("Seat Height", Menu.coloredBoxStyle);
                float oldSeatHeight = Config.bike.seatHeight;
                Menu.ModernSlider("Height", ref Config.bike.seatHeight, -0.15f, 0.15f);
                if (oldSeatHeight != Config.bike.seatHeight) changed = true;

                GUILayout.Space(10);
                GUILayout.Label("Seat Tilt", Menu.coloredBoxStyle);
                float oldSeatPitch = Config.bike.seatPitch;
                Menu.ModernSlider("Rotation X", ref Config.bike.seatPitch, 330f, 379f);
                if (oldSeatPitch != Config.bike.seatPitch) changed = true;
            }

            if (barsAnchor != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Bars Rotation", Menu.coloredBoxStyle);
                float oldBarPitch = Config.bike.barPitch;
                Menu.ModernSlider("Rotation", ref Config.bike.barPitch, -45f, 45f);
                if (oldBarPitch != Config.bike.barPitch) changed = true;
                
                float oldBarScale = Config.bike.barScale;
                Menu.ModernSlider("Scale", ref Config.bike.barScale, 0.1f, 10f);
                if (oldBarScale != Config.bike.barScale) changed = true;
            }

            if (wheelMeshes != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Wheel Scale", Menu.coloredBoxStyle);

                float oldW = Config.bike.frontWheelWidth;
                float oldR = Config.bike.frontWheelRadius;

                Menu.ModernSlider("Width", ref Config.bike.frontWheelWidth, 0.5f, 2f);
                Menu.ModernSlider("Radius", ref Config.bike.frontWheelRadius, 0.5f, 2f);

                if (!Mathf.Approximately(oldW, Config.bike.frontWheelWidth) ||
                    !Mathf.Approximately(oldR, Config.bike.frontWheelRadius))
                {
                    changed = true;
                }
            }

            /*if (forkAnchor != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Fork Scale", Menu.coloredBoxStyle);
                float oldForkScale = Config.bike.forkScale;
                Menu.ModernSlider("Length", ref Config.bike.forkScale, 1f, 3f);
                if (oldForkScale != Config.bike.forkScale) changed = true;
            }*/

            if (pegData != null && pegData.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("Pegs Enabled", Menu.coloredBoxStyle);

                for (int i = 0; i < pegData.Count; i++)
                {
                    var d = pegData[i];
                    string label = d.IsFrontLeft ? "Front Left" :
                                   d.IsFrontRight ? "Front Right" :
                                   d.IsRearLeft ? "Rear Left" :
                                   d.IsRearRight ? "Rear Right" :
                                   d.EquipSlot != null ? d.EquipSlot.name : "Peg";

                    bool enabledNow = GUILayout.Toggle(d.IsEnabled, label);
                    if (enabledNow != d.IsEnabled)
                    {
                        SetConfigEnabled(d, enabledNow);
                        d.IsEnabled = enabledNow;
                        pegData[i] = d;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                UpdatePartTransforms();
                Config.Save();
            }
        }
        
        public static void UpdatePartTransforms()
        {
            Log.Msg("[UpdatePartTransforms] Updating part transforms...");
            if (Memory.customizableEntity == null)
            {
                Log.Warning("[UpdatePartTransforms] Skipped update because Memory.customizableEntity is null.");
                return;
            }

            int barsUpdated = 0, barsSkipped = 0;
            int gripsUpdated = 0, gripsSkipped = 0;
            int barEndsUpdated = 0, barEndsSkipped = 0;
            int seatPostUpdated = 0, seatPostSkipped = 0;
            int seatUpdated = 0, seatSkipped = 0;
            int wheelsUpdated = 0, wheelsSkipped = 0;
            int tireBoundsUpdated = 0, tireBoundsSkipped = 0;
            int forksUpdated = 0, forksSkipped = 0;
            int pegsStateUpdated = 0, pegsStateSkipped = 0;

            // Bars
            if (barsAnchor != null)
            {
                float bScale = Config.bike.barScale <= 0 ? 1f : Config.bike.barScale;
                barsAnchor.localRotation = Quaternion.Euler(Config.bike.barPitch, 0f, 0f);
                barsAnchor.localScale = Vector3.one * bScale;
                barsUpdated++;
                Log.Msg($"[UpdatePartTransforms] Bars updated: '{barsAnchor.name}' rot={barsAnchor.localRotation.eulerAngles} scale={barsAnchor.localScale}");
            }
            else
            {
                barsSkipped++;
                Log.Warning("[UpdatePartTransforms] Bars skipped: barsAnchor is null.");
            }

            // Grips
            if (gripsEquipSlots == null || gripsEquipSlots.Length == 0)
            {
                Log.Warning("[UpdatePartTransforms] Grips skipped: gripsEquipSlots is null or empty.");
            }
            else
            {
                float bScale = Config.bike.barScale <= 0 ? 1f : Config.bike.barScale;
                for (int i = 0; i < gripsEquipSlots.Length; i++)
                {
                    var grip = gripsEquipSlots[i];
                    if (grip == null)
                    {
                        gripsSkipped++;
                        Log.Warning($"[UpdatePartTransforms] Grip[{i}] skipped: Anchor is null.");
                        continue;
                    }

                    if (grip._anchorTrans == null)
                    {
                        gripsSkipped++;
                        Log.Warning($"[UpdatePartTransforms] Grip[{i}] '{grip.name}' skipped: _anchorTrans is null.");
                        continue;
                    }

                    bool childOfBars = barsAnchor != null && grip._anchorTrans.IsChildOf(barsAnchor);
                    if (childOfBars)
                        grip._anchorTrans.localScale = Vector3.one;
                    else
                        grip._anchorTrans.localScale = Vector3.one * bScale;

                    gripsUpdated++;
                    Log.Msg($"[UpdatePartTransforms] Grip[{i}] updated: '{grip._anchorTrans.name}' scale={grip._anchorTrans.localScale} childOfBars={childOfBars}");
                }
            }

            // Bar Ends
            if (barEndEquipSlots == null || barEndEquipSlots.Length == 0)
            {
                Log.Warning("[UpdatePartTransforms] Bar ends skipped: barEndEquipSlots is null or empty.");
            }
            else
            {
                float bScale = Config.bike.barScale <= 0 ? 1f : Config.bike.barScale;
                for (int i = 0; i < barEndEquipSlots.Length; i++)
                {
                    var barEnd = barEndEquipSlots[i];
                    if (barEnd == null)
                    {
                        barEndsSkipped++;
                        Log.Warning($"[UpdatePartTransforms] BarEnd[{i}] skipped: Anchor is null.");
                        continue;
                    }

                    if (barEnd._anchorTrans == null)
                    {
                        barEndsSkipped++;
                        Log.Warning($"[UpdatePartTransforms] BarEnd[{i}] '{barEnd.name}' skipped: _anchorTrans is null.");
                        continue;
                    }

                    bool childOfBars = barsAnchor != null && barEnd._anchorTrans.IsChildOf(barsAnchor);
                    if (childOfBars)
                        barEnd._anchorTrans.localScale = Vector3.one;
                    else
                        barEnd._anchorTrans.localScale = Vector3.one * bScale;

                    barEndsUpdated++;
                    Log.Msg($"[UpdatePartTransforms] BarEnd[{i}] updated: '{barEnd._anchorTrans.name}' scale={barEnd._anchorTrans.localScale} childOfBars={childOfBars}");
                }
            }

            // Seat
            if (seatPostAnchor != null)
            {
                seatPostAnchor.localPosition = new Vector3(
                    seatPostAnchor.localPosition.x,
                    Config.bike.seatHeight,
                    seatPostAnchor.localPosition.z
                );
                seatPostUpdated++;
                Log.Msg($"[UpdatePartTransforms] SeatPost updated: '{seatPostAnchor.name}' pos={seatPostAnchor.localPosition}");
            }
            else
            {
                seatPostSkipped++;
                Log.Warning("[UpdatePartTransforms] Seat post skipped: seatPostAnchor is null.");
            }

            if (seatAnchor != null)
            {
                seatAnchor.localRotation = Quaternion.Euler(Config.bike.seatPitch % 360f, 0f, 0f);
                seatUpdated++;
                Log.Msg($"[UpdatePartTransforms] Seat updated: '{seatAnchor.name}' rot={seatAnchor.localRotation.eulerAngles}");
            }
            else
            {
                seatSkipped++;
                Log.Warning("[UpdatePartTransforms] Seat rotation skipped: seatAnchor is null.");
            }

            // Wheels
            if (wheelMeshes == null || wheelMeshes.Count == 0)
            {
                Log.Warning("[UpdatePartTransforms] Wheels skipped: wheelMeshes is null or empty.");
            }
            else
            {
                Vector3 wheelScale = new Vector3(
                    Config.bike.frontWheelWidth,
                    Config.bike.frontWheelRadius,
                    Config.bike.frontWheelRadius
                );

                for (int i = 0; i < wheelMeshes.Count; i++)
                {
                    var wheelMesh = wheelMeshes[i];
                    if (wheelMesh == null)
                    {
                        wheelsSkipped++;
                        Log.Warning($"[UpdatePartTransforms] Wheel[{i}] skipped: transform is null.");
                        continue;
                    }

                    wheelMesh.localScale = wheelScale;
                    wheelsUpdated++;
                    Log.Msg($"[UpdatePartTransforms] Wheel[{i}] updated: '{wheelMesh.name}' scale={wheelMesh.localScale}");
                }
            }

            if (tireTest == null || tireTest.Length == 0)
            {
                Log.Warning("[UpdatePartTransforms] Tire bounds refresh skipped: tireTest is null or empty.");
            }
            else
            {
                for (int i = 0; i < tireTest.Length; i++)
                {
                    var tire = tireTest[i];
                    if (tire == null)
                    {
                        tireBoundsSkipped++;
                        Log.Warning($"[UpdatePartTransforms] TireTest[{i}] skipped: component is null.");
                        continue;
                    }

                    tire.UpdateBounds();
                    tireBoundsUpdated++;
                    Log.Msg($"[UpdatePartTransforms] TireTest[{i}] bounds updated: '{tire.name}'.");
                }
            }

            // Forks
            if (forkAnchor != null)
            {
                float fScale = Config.bike.forkScale <= 0 ? 1f : Config.bike.forkScale;
                forkAnchor.localScale = Vector3.one * fScale;
                forksUpdated++;
                Log.Msg($"[UpdatePartTransforms] Fork updated: '{forkAnchor.name}' scale={forkAnchor.localScale}");
            }
            else
            {
                forksSkipped++;
                Log.Warning("[UpdatePartTransforms] Fork skipped: forkAnchor is null.");
            }

            // Pegs
            if (pegData == null || pegData.Count == 0)
            {
                Log.Warning("[UpdatePartTransforms] Pegs skipped: pegData is null or empty.");
            }
            else
            {
                for (int i = 0; i < pegData.Count; i++)
                {
                    var d = pegData[i];
                    bool enabledNow = GetConfigEnabled(d);
                    string pegLabel = d.EquipSlot != null
                        ? d.EquipSlot.name
                        : d.Parent != null ? d.Parent.name : $"Peg[{i}]";

                    bool parentUpdated = false;

                    if (d.Parent != null)
                    {
                        if (d.Parent.activeSelf != enabledNow)
                        {
                            d.Parent.SetActive(enabledNow);
                            parentUpdated = true;
                        }
                        pegsStateUpdated++;
                    }
                    else
                    {
                        pegsStateSkipped++;
                        Log.Warning($"[UpdatePartTransforms] Peg[{i}] '{pegLabel}' state skipped: Parent is null.");
                    }

                    d.IsEnabled = enabledNow;
                    pegData[i] = d; // write back struct
                    Log.Msg($"[UpdatePartTransforms] Peg[{i}] '{pegLabel}': enabled={enabledNow}, parentChanged={parentUpdated}");
                }
            }

            // Update cached values
            lastBarPitch = Config.bike.barPitch;
            lastBarScale = Config.bike.barScale;
            lastSeatHeight = Config.bike.seatHeight;
            lastSeatPitch = Config.bike.seatPitch;

            Memory.customizableEntity.RelaySnap();
            Log.Msg("[UpdatePartTransforms] RelaySnap triggered.");
            Log.Msg(
                $"[UpdatePartTransforms] Summary: bars {barsUpdated} updated/{barsSkipped} skipped, grips {gripsUpdated}/{gripsSkipped}, barEnds {barEndsUpdated}/{barEndsSkipped}, seatPost {seatPostUpdated}/{seatPostSkipped}, seat {seatUpdated}/{seatSkipped}, wheels {wheelsUpdated}/{wheelsSkipped}, tires {tireBoundsUpdated}/{tireBoundsSkipped}, forks {forksUpdated}/{forksSkipped}, pegState {pegsStateUpdated}/{pegsStateSkipped}."
            );
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
        public static void ResetAllCustomParts()
        {
            Log.Msg("Resetting all custom parts to default...");
            if (Memory.rMbCharacter == null)
            {
                Log.Error("ResetAllCustomParts(): rMbCharacter is null. Cannot reset parts.");
                return;
            }
            // Clear saved config entries
            Config.bike.lastLoadedBars = string.Empty;
            Config.bike.lastLoadedFrame = string.Empty;
            Config.bike.lastLoadedStem  = string.Empty;
            Config.Save();
            Memory.lastEquippedBars = null;
            Memory.lastEquippedFrame = null;
            Memory.lastEquippedStem = null;


            Memory.customizableEntity.EquipItems();
            Memory.customizableEntity.EquipItems();

            FindParts();
            UpdatePartTransforms();
        }
        public static void DrawPartSelectorUI()
        {
            // nice bordered container for the whole part selector (the red box in your screenshot)
            GUILayout.BeginVertical(GUI.skin.box); // Or use Menu.cardOuterStyle if you have it

            // Header row + RESET PARTS button (right aligned)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Bike Parts", Menu.coloredBoxStyle, GUILayout.Height(26));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("<b>RESET PARTS</b>", Menu.redButtonStyle, GUILayout.Height(26), GUILayout.Width(140)))
            {
                ResetAllCustomParts();
            }
            GUILayout.EndHorizontal();
            
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
                case PartTypeTab.Stem:
                    EnsureStemLoaded();
                    DrawPartTab("Select Stem:", stemPrefabs, stemNames, ref selectedStemIndex, ref stemScrollPos, TryReplaceStem);
                    break;
            }

            GUILayout.EndVertical();
        }
        private static void ApplyPegEnabledFromConfig()
        {
            if (pegData == null || pegData.Count == 0) return;

            for (int i = 0; i < pegData.Count; i++)
            {
                var d = pegData[i];
                bool enabled = GetConfigEnabled(d);

                if (d.Parent != null)
                    d.Parent.SetActive(enabled);

                d.IsEnabled = enabled;
                pegData[i] = d; // struct writeback
            }
        }
        private static bool GetConfigEnabled(PegData d)
        {
            if (d.IsFrontLeft)  return Config.bike.frontLeftPegsEnabled;
            if (d.IsFrontRight) return Config.bike.frontRightPegsEnabled;
            if (d.IsRearLeft)   return Config.bike.rearLeftPegsEnabled;
            if (d.IsRearRight)  return Config.bike.rearRightPegsEnabled;
            return true;
        }

        private static void SetConfigEnabled(PegData d, bool enabled)
        {
            if (d.IsFrontLeft)  Config.bike.frontLeftPegsEnabled = enabled;
            else if (d.IsFrontRight) Config.bike.frontRightPegsEnabled = enabled;
            else if (d.IsRearLeft)   Config.bike.rearLeftPegsEnabled = enabled;
            else if (d.IsRearRight)  Config.bike.rearRightPegsEnabled = enabled;
        }
        //added custom stem loading and replacing methods
        private static void EnsureStemLoaded()
        {
            if (stemListInitialized && stemPrefabs.Count > 0)
                return;
        
            stemPrefabs = Memory.prefabList
                .Where(p => p != null && p.name.ToLower().StartsWith("stem"))
                .ToList();
        
            stemNames = stemPrefabs.Select(p =>
            {
                string bundleName = Memory.prefabToBundleMap.TryGetValue(p, out var bundle) ? bundle : "UnknownBundle";
                return $"{p.name} ({bundleName})";
            }).ToArray();
        
            stemListInitialized = true;
            Log.Msg($"[Stem] Loaded {stemPrefabs.Count} stem prefabs.");
        }
        
        public static void TryReplaceStem(GameObject prefab)
        {
            var slot = Memory.rMbCharacter.transform.FindDeepChild("Stem")?.GetComponent<EquipSlotVehicle>();
            if (slot == null)
            {
                Log.Warning("[Stem] Could not find Stem EquipSlot.");
                return;
            }
        
            Log.Msg($"[Stem] Instantiating: {prefab.name}");
            slot.Equip(prefab);
        
            Config.bike.bikeMaterials.Remove("Stem");
            Config.Save();
        
            FindParts();
            UpdatePartTransforms();
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
        
        public static void LoadSavedStem()
        {
            if (!string.IsNullOrEmpty(Config.bike.lastLoadedStem))
            {
                var savedStem = Memory.prefabList.FirstOrDefault(p => p != null && p.name == Config.bike.lastLoadedStem);
                if (savedStem != null)
                {
                    TryReplaceStem(savedStem);
                    Log.Msg($"[Stem] Loaded saved stem: {savedStem.name}");
                }
                else
                {
                    Log.Warning($"[Stem] Could not find saved stem: {Config.bike.lastLoadedStem}");
                }
            }
        }
    }
}
