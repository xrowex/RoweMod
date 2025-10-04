using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.Character.Scripts;
using rowemod.Utils;
using static Il2CppMashBox.Addons.NetworkingFusion.NetworkPlayer;
using static Il2CppMashBox.Addons.NetworkingFusion.NetworkHuman;
using UnityEngine;


namespace rowemod.Mods;

public class Multiplayer
{
    
    public static void SyncDrip()
    {

        GameObject hatGo = Memory.rMbCharacter.GetComponent<CustomCharacterManager>()._hat._go;
        
        if (Memory.networkPlayers == null)
        {
            Log.Warning("NetworkPlayers is null. Skipping...");
            return;
        }
        foreach (var player in Memory.networkPlayers)
        {
            Log.Msg($"Processing network player {player.name}");
            if (player == null)
            {
                Log.Warning("NetworkPlayer is null. Skipping...");
                return;
            }
            if (player.IsLocalPlayer == false)
            {
                Log.Msg($"Processing network player {player.name}");
                var human = player.GetComponentInParent<NetworkHuman>();
                if (human != null)
                {
                    Log.Msg($"Found NetworkHuman component for player {player.name}");
                    foreach (var humVis in human._visuals)
                    {
                        Log.Msg($"Processing visual {humVis.name}");
                        if(humVis.name.Contains("Hat") && hatGo != null)
                        {
                            Log.Msg($"Equipping hat on {humVis.name}");
                            humVis.GetComponent<EquipSlot>().Equip(hatGo);
                        }
                    }
                }
                else
                {
                    Log.Warning($"NetworkHuman component not found for player {player.name}. Skipping...");
                }
            }
        }
        
    }

    public static void DrawMultiplayerTab()
    {
        if (GUILayout.Button("Sync Drip"))
        {
            SyncDrip();
        }
    }
}