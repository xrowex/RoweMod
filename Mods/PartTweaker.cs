using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods;

public class PartTweaker
{
    public static Transform seatPostAnchor;
    public static Transform barsAnchor;
    
    public static void FindParts()
    {
        
            seatPostAnchor = Memory.rMbCharacter.transform.FindDeepChild("SeatPost_Anchor").transform;
            if (seatPostAnchor != null)
            {
                Log.Msg("SeatPost_Anchor found.");
            }
            else
            {
                Log.Error("SeatPost_Anchor not found.");
            }
            
            barsAnchor = Memory.rMbCharacter.transform.FindDeepChild("Bars_Anchor").transform;
            if (barsAnchor != null)
            {
                Log.Msg("Bars_Anchor found.");
            }
            else
            {
                Log.Error("Bars_Anchor not found.");
            }
        
    }

    public static void DrawPartTweaker()
    {
        /*GUILayout.Label("Seat Height");
        if (GUILayout.Button("-"))
        {
            if (seatPostAnchor != null)
            {
                seatPostAnchor.localPosition -= new Vector3(0, 1f, 0);
                Memory.customizableEntity.RelaySnap();
            }
        }
        if (GUILayout.Button("+"))
        {
            if (seatPostAnchor != null)
            {
                seatPostAnchor.localPosition += new Vector3(0, 1f, 0);
                Memory.customizableEntity.RelaySnap();
            }
        }*/
        
        GUILayout.Label("Bars Rotation");
        if (GUILayout.Button("-"))
        {
            if (barsAnchor != null)
            {
                barsAnchor.localRotation *= Quaternion.Euler(-5f, 0, 0);
                Memory.customizableEntity.RelaySnap();
            }
        }
        if (GUILayout.Button("+"))
        {
            if (barsAnchor != null)
            {
                barsAnchor.localRotation *= Quaternion.Euler(5f, 0, 0);
                Memory.customizableEntity.RelaySnap();
            } 
        }
    }
}