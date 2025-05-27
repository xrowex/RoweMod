using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using rowemod.Utils;
using UnityEngine;
using System.Linq;
using Il2CppMashBox.BMX_Physics_Development;

namespace rowemod.Mods
{
    public class PartTweaker
    {
        public static Transform seatPostAnchor;
        public static Transform seatAnchor;
        public static Transform barsAnchor;
        public static Transform[] pegsAnchor; // Fix: Correctly declare pegsAnchor as an array of Transform  

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
        private static float seatHeight = 0.15f;      // Y position  
        private static float seatRotationX = 330f;
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
                Config.seatHeight = seatHeight;

                GUILayout.Space(10);
                GUILayout.Label("Seat Tilt", Menu.coloredBoxStyle);
                Menu.ModernSlider("Rotation X", ref seatRotationX, 330f, 379f); // 379 wraps around to 19  

                if (seatAnchor != null)
                {
                    // Update seat tilt  
                    Quaternion newRotation = Quaternion.Euler(seatRotationX % 360f, 0f, 0f);
                    seatAnchor.localRotation = newRotation;
                    Config.seatRotationX = seatRotationX;

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
                Config.barRotationAngle = barRotationAngle;

                Memory.customizableEntity.RelaySnap();
            }
            if(pegsAnchor != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Peg Length", Menu.coloredBoxStyle);
                Menu.ModernSlider("Length", ref pegLength, 1f, 3f);
                foreach(var peg in pegsAnchor)
                {
                    peg.localScale = new Vector3(pegLength, peg.localScale.y, peg.localScale.z);
                }
                Config.pegLength = pegLength;
            }
        }
    }
}
