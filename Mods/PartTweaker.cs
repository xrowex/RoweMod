using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using rowemod.Utils;
using UnityEngine;
using System.Linq;

namespace rowemod.Mods
{
    public class PartTweaker
    {
        public static Transform seatPostAnchor;
        public static Transform seatAnchor;
        public static Transform barsAnchor;

        public static void FindParts()
        {
            if (Memory.rMbCharacter == null)
            {
                Log.Error("rMbCharacter is null. Cannot search for anchors.");
                return;
            }

            // Try finding the SeatPost_Anchor (any active one)
            var allSeatPosts = UnityEngine.Object.FindObjectsOfType<Transform>()
                .Where(t => t.name == "SeatPost_Anchor" && t.parent != null && t.parent.gameObject.activeInHierarchy)
                .ToList();

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
            barsAnchor = Memory.rMbCharacter.transform.FindDeepChild("Bars_Anchor");
            if (barsAnchor != null)
            {
                Log.Msg("Bars_Anchor found.");
            }
            else
            {
                Log.Error("Bars_Anchor not found.");
            }
        }


        private static float barRotationAngle = 0f;
        private static float seatHeight = 0.15f;      // Y position
        private static float seatRotationX = 330f;   // Euler X (wraps from 330 to 19)

        public static void DrawPartTweaker()
        {
            if(seatAnchor==null || seatPostAnchor==null || barsAnchor == null)
            {
                FindParts();
            }
            if (seatPostAnchor != null)
            {
                GUILayout.Label("Seat Height", Menu.coloredBoxStyle);
                Menu.ModernSlider("Height", ref seatHeight, 0.0f, 0.25f);
                seatPostAnchor.localPosition = new Vector3(
                    seatPostAnchor.localPosition.x,
                    seatHeight,
                    seatPostAnchor.localPosition.z
                );

                GUILayout.Space(10);
                GUILayout.Label("Seat Tilt", Menu.coloredBoxStyle);
                Menu.ModernSlider("Rotation X", ref seatRotationX, 330f, 379f); // 379 wraps around to 19

                if (seatAnchor != null)
                {
                    Quaternion newRotation = Quaternion.Euler(seatRotationX % 360f, 0f, 0f);
                    seatAnchor.localRotation = newRotation;
                }
                else
                {
                    Log.Error("Skipping Seat_Anchor rotation: seatAnchor is null.");
                }
                
               // Memory.customizableEntity.RelaySnap();
            }

            if (barsAnchor != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Bars Rotation", Menu.coloredBoxStyle);
                Menu.ModernSlider("Rotation", ref barRotationAngle, -45f, 45f);
                barsAnchor.localRotation = Quaternion.Euler(barRotationAngle, 0f, 0f);
                //Memory.customizableEntity.RelaySnap();
            }
        }
    }
}
