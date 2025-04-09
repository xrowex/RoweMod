using Il2CppMashBox.Character.Scripts;
using Il2CppSalvage.ClothingCuller.Configuration;
using rowemod.Utils;
using UnityEngine;
using static rowemod.Utils.Memory;

namespace rowemod.Mods;

public class roweCustomCharacter
{
    public static CharacterData defaultCharacterData;
    public static CustomCharacterManager rCharacterManager;
    public static GameObject defaultBody, defaultBust,  defaultEyes, defaultHair, defaultHat, defaultPants, defaultShirt, defaultShoes;
    public static string defaultName;
    public static ClothingCullerConfiguration defaultcullerConfig;
    
    
    public static void GetDefaultCharacter()
    {
        rCharacterManager = physicsDrivenCharacter.GetComponent<CustomCharacterManager>();
        if (rCharacterManager != null)
        {
            defaultCharacterData = rCharacterManager._characterData;
            Log.Msg($"Default Character: {defaultCharacterData.name}");
            defaultcullerConfig = defaultCharacterData._clothingCullerConfiguration;
            rCharacterManager._clothingCullerConfiguration = defaultcullerConfig;
            defaultBody = defaultCharacterData._body;
            defaultBust = defaultCharacterData._bust;
            defaultName = defaultCharacterData.name;
            defaultEyes = defaultCharacterData._eyes;
            defaultHair = defaultCharacterData._hair;
            defaultHat = defaultCharacterData._hat;
            defaultPants = defaultCharacterData._pants;
            defaultShirt = defaultCharacterData._shirt;
            defaultShoes = defaultCharacterData._shoes;
        }
        else
        {
            Log.Error("CustomCharacterManager not found on the player character!");
        }
        
        
        // Log errors if required components aren't found
        if (defaultBody == null) Log.Error("Default body not found in character data!");
        if (defaultBust == null) Log.Error("Default bust not found in character data!");
        if (defaultName == null) Log.Error("Default name not found in character data!");
        if (defaultEyes == null) Log.Error("Default eyes not found in character data!");
        if (defaultHair == null) Log.Error("Default hair not found in character data!");
        if (defaultHat == null) Log.Error("Default hat not found in character data!");
        if (defaultPants == null) Log.Error("Default pants not found in character data!");
        if (defaultShirt == null) Log.Error("Default shirt not found in character data!");
        if (defaultShoes == null) Log.Error("Default shoes not found in character data!");
        
        
    }
    public static void LoadCharacter()
    {
        
    }
    
    public static void UpdateCharacter()
    {
        
    }
    
    
}