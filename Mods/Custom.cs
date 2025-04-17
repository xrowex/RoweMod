using rowemod.Utils;
using UnityEngine;
using EquipSlot = Il2CppMashBox.Character.Scripts.EquipSlot;
using static rowemod.Menu;
using static rowemod.Utils.Memory;

namespace rowemod.Mods
{
    public static class Custom
    {
        public enum Slot
        {
            Hat,
            Hair,
            Eyes,
            Body,
            Top,
            Gloves,
            Bottoms,
            Socks,
            Shoes,
            Bust,
        }

        // Map your enum to the ACTUAL child names that appear in the screenshot:
        // "HeadGear", "EquipSlot_Body", "EquipSlot_Shirt", "EquipSlot_Pants", etc.
        public static readonly Dictionary<Slot, string> slotNameMap = new Dictionary<Slot, string>()
        {
            { Slot.Hat,      "Hat_EquipSlot"    },
            { Slot.Hair,     "Hair_EquipSlot"   },
            { Slot.Eyes,     "Eyes_EquipSlot"   },
            { Slot.Body,     "EquipSlot_Body"   },
            { Slot.Top,      "EquipSlot_Shirt"  },
            { Slot.Gloves,   "EquipSlot_Gloves" },
            { Slot.Bottoms,  "EquipSlot_Pants"  },
            { Slot.Socks,    "EquipSlot_Socks"  },
            { Slot.Shoes,    "EquipSlot_Shoes"  },
            { Slot.Bust,     "EquipSlot_Bust"   },
        };

        // Because your actual slot GameObjects live deeper in the hierarchy,
        // we can define that path here. (Adjust if yours is slightly different.)
        private const string SLOT_PARENT_PATH = "Physics Skeleton/";
        
        //Change to just skeleton to try to work with new main menu? jk fuck that
        //private const string SLOT_PARENT_PATH = "Skeleton/";

        static Transform prefab;
        static GameObject customObject;
        public static string characterRootPath = Path.Combine(
            Path.GetDirectoryName(typeof(Custom).Assembly.Location),
            @"rowemod\Character"
        );
        private static Dictionary<Slot, bool> slotVisibility = new Dictionary<Slot, bool>();

        public static bool inModelsTab = true;
        static Dictionary<Slot, string> selectedModelDirectories = new Dictionary<Slot, string>();
        static Slot? lastSelectedSlot = null;

        private static string newPresetName = "";
        private static int selectedPresetIndex = 0;
        
        /*public static void DrawCharacterTab()
        {
            try
            {
                GUILayout.BeginHorizontal();

                GUILayout.BeginVertical(GUILayout.Width(125));
                foreach (Slot slot in Enum.GetValues(typeof(Slot)))
                {
                    GUILayout.BeginHorizontal(); 
                    
                    
                    if (GUILayout.Button($"<b>{slot.ToString()}</b>", Menu.buttonStyle))
                    {
                        Menu.currentSlot = slot;
                        Menu.inModelsTab = true;
                    }

                    GUILayout.EndHorizontal(); // End row
                }
                GUILayout.EndVertical();
                
                ListCharacterBundles(Menu.currentSlot);
                GUILayout.EndHorizontal();
                GUILayout.Label("Presets", Menu.labelStyle);

                // Text field to enter new preset name
                newPresetName = GUILayout.TextField(newPresetName, 25);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Preset", Menu.buttonStyle))
                {
                    if (!string.IsNullOrWhiteSpace(newPresetName))
                    {
                        SaveCurrentPreset(newPresetName);
                        newPresetName = ""; // Clear input field after saving
                    }
                }

                List<string> availablePresets = ClothingPreset.GetAvailablePresets();

                
                ClothingPreset defaultPreset = ClothingPreset.LoadPreset("DefaultPreset");
                if (defaultPreset == null || 
                    defaultPreset.modelPaths == null || 
                    defaultPreset.materialPaths == null || 
                    defaultPreset.modelPaths.Count > 0 || 
                    defaultPreset.materialPaths.Count > 0)
                {
                    defaultPreset = new ClothingPreset 
                    { 
                        name = "DefaultPreset", 
                        modelPaths = new Dictionary<Slot, string>(), 
                        materialPaths = new Dictionary<Slot, string>() 
                    };
                    ClothingPreset.SavePreset(defaultPreset);
                }
                availablePresets.Add(defaultPreset.name);
                
                if (availablePresets.Count > 0)
                {
                    selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, availablePresets.Count - 1);

                    if (availablePresets.Count == 1)
                    {
                        selectedPresetIndex = 0;
                        LoadPreset(availablePresets[0]);
                    }

                    if (availablePresets.Count > 0)
                    {
                        GUILayout.BeginVertical();
                        for (int i = 0; i < availablePresets.Count; i++)
                        {
                            if (GUILayout.Button(availablePresets[i], Menu.buttonStyle))
                            {
                                selectedPresetIndex = i;
                                LoadPreset(availablePresets[selectedPresetIndex]);
                            }
                        }
                        GUILayout.EndVertical();
                    }
                }
                GUILayout.EndHorizontal();
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred in DrawCharacterTab: {ex.Message}");
            }
        }*/
        public static void DrawCharacterTab()
        {
            try
            {
                GUILayout.BeginHorizontal();

                GUILayout.BeginVertical(GUILayout.Width(125));
                foreach (Slot slot in Enum.GetValues(typeof(Slot)))
                {
                    GUILayout.BeginHorizontal(); 

                    // Initialize toggle state if not set
                    if (!slotVisibility.ContainsKey(slot))
                    {
                        slotVisibility[slot] = true; // Default to visible
                    }

                    // Toggle button (placed to the left of the slot button)
                    bool newState = GUILayout.Toggle(slotVisibility[slot], "", GUILayout.Width(20));
                    if (newState != slotVisibility[slot])
                    {
                        slotVisibility[slot] = newState;
                        ToggleSlotVisibility(slot, newState);
                    }

                    // Slot button
                    if (GUILayout.Button($"<b>{slot.ToString()}</b>", Menu.highQualityButtonStyle))
                    {
                        Menu.currentSlot = slot;
                        Menu.inModelsTab = true;
                    }

                    GUILayout.EndHorizontal(); // End row
                }
                GUILayout.EndVertical();

                ListCharacterBundles(Menu.currentSlot);
                GUILayout.EndHorizontal();
                GUILayout.Label("Presets", Menu.labelStyle);

                // Text field to enter new preset name
                newPresetName = GUILayout.TextField(newPresetName, 25);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Preset", Menu.highQualityButtonStyle))
                {
                    if (!string.IsNullOrWhiteSpace(newPresetName))
                    {
                        SaveCurrentPreset(newPresetName);
                        newPresetName = ""; // Clear input field after saving
                    }
                }

                List<string> availablePresets = ClothingPreset.GetAvailablePresets();

                ClothingPreset defaultPreset = ClothingPreset.LoadPreset("DefaultPreset");
                if (defaultPreset == null || 
                    defaultPreset.modelPaths == null || 
                    defaultPreset.materialPaths == null || 
                    defaultPreset.modelPaths.Count > 0 || 
                    defaultPreset.materialPaths.Count > 0)
                {
                    defaultPreset = new ClothingPreset 
                    { 
                        name = "DefaultPreset", 
                        modelPaths = new Dictionary<Slot, string>(), 
                        materialPaths = new Dictionary<Slot, string>() 
                    };
                    ClothingPreset.SavePreset(defaultPreset);
                }
                availablePresets.Add(defaultPreset.name);

                if (availablePresets.Count > 0)
                {
                    selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, availablePresets.Count - 1);

                    if (availablePresets.Count == 1)
                    {
                        selectedPresetIndex = 0;
                        LoadPreset(availablePresets[0]);
                    }

                    if (availablePresets.Count > 0)
                    {
                        GUILayout.BeginVertical();
                        for (int i = 0; i < availablePresets.Count; i++)
                        {
                            if (GUILayout.Button(availablePresets[i], Menu.highQualityButtonStyle))
                            {
                                selectedPresetIndex = i;
                                LoadPreset(availablePresets[selectedPresetIndex]);
                            }
                        }
                        GUILayout.EndVertical();
                    }
                }
                GUILayout.EndHorizontal();
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred in DrawCharacterTab: {ex.Message}");
            }
        }

        private static Dictionary<Slot, GameObject> slotObjects = new Dictionary<Slot, GameObject>();

        private static void ToggleSlotVisibility(Slot slot, bool isVisible)
        {
            // Ensure the slot exists in the dictionary
            if (!slotNameMap.TryGetValue(slot, out string equipSlotName))
            {
                Debug.LogError($"ToggleSlotVisibility: No mapped name found for slot '{slot}'.");
                return;
            }

            // Construct the correct full path
            string fullPath = SLOT_PARENT_PATH + equipSlotName;
            if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes)
            {
                fullPath = SLOT_PARENT_PATH + "HeadGear/" + equipSlotName;
            }

            // Check if we already stored this GameObject
            if (!slotObjects.TryGetValue(slot, out GameObject slotObject) || slotObject == null)
            {
                // Find and store the GameObject only once
                slotObject = GameObject.Find(fullPath);
                if (slotObject != null)
                {
                    slotObjects[slot] = slotObject;
                }
                else
                {
                    Debug.LogWarning($"[rowemod] Slot GameObject not found: {fullPath}");
                    return;
                }
            }

            // Enable or disable the object
            slotObject.SetActive(isVisible);
            Debug.Log($"[rowemod] Slot GameObject '{fullPath}' set to {isVisible}.");
        }
        
        
        
        
        public static void ListCharacterBundles(Slot slot)
        {
            if (lastSelectedSlot != slot)
            {
                inModelsTab = true;
                lastSelectedSlot = slot;
            }
            
            string slotPath = Path.Combine(characterRootPath, slot.ToString());
            
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Models", highQualityButtonStyle)) inModelsTab = true;
            if (GUILayout.Button("Materials", highQualityButtonStyle)) inModelsTab = false;
            GUILayout.EndHorizontal();

            if (inModelsTab)
            {
                // List all *.model files in this slot’s folder
                foreach (string file in Directory.GetFiles(slotPath, "*.model", SearchOption.AllDirectories))
                {
                    string buttonText = Path.GetFileNameWithoutExtension(file);
                    if (GUILayout.Button(buttonText, highQualityButtonStyle))
                    {
                        ReplaceModel(slot, file);
                    }
                }
            }
            else
            {
                // If we have a selected model directory, show any *.material files in it
                if (selectedModelDirectories.TryGetValue(slot, out string modelDirectory)
                    && Directory.Exists(modelDirectory))
                {
                    string[] materialFiles = Directory.GetFiles(modelDirectory, "*.material", SearchOption.AllDirectories);
                    if (materialFiles.Length == 0)
                    {
                        GUILayout.Label("No materials found in this model's directory.", labelStyle);
                    }
                    else
                    {
                        foreach (string file in materialFiles)
                        {
                            string buttonText = Path.GetFileNameWithoutExtension(file);
                            if (GUILayout.Button(buttonText, highQualityButtonStyle))
                            {
                                ReplaceMaterial(slot, file);
                            }
                        }
                    }
                }
                else
                {
                    GUILayout.Label("No model selected. Select a model first.", labelStyle);
                }
            }

            GUILayout.EndVertical();
        }

        public static void ReplaceModel(Slot slot, string newBundlePath)
        {
            if (string.IsNullOrEmpty(newBundlePath))
            {
                Log.Error($"ReplaceModel: newBundlePath is null or empty for slot {slot}.");
                return;
            }

            // Store the directory where the *.model was found (so we can look for materials next).
            string directory = Path.GetDirectoryName(newBundlePath);
            if (!string.IsNullOrEmpty(directory))
            {
                selectedModelDirectories[slot] = directory;
            }

            // Load the new asset bundle
            var newBundle = AssetBundle.LoadFromFile(newBundlePath);
            if (newBundle == null)
            {
                Log.Error($"ReplaceModel: Failed to load asset bundle from {newBundlePath}");
                return;
            }

            // Clean up old references
            if (customObject != null)
            {
                GameObject.Destroy(customObject);
                customObject = null;
            }
            if (prefab != null)
            {
                GameObject.Destroy(prefab.gameObject);
                prefab = null;
            }

            // Load the prefab (assuming the first asset is the prefab you want)
            string[] assetNames = newBundle.GetAllAssetNames();
            if (assetNames.Length == 0)
            {
                Log.Error("ReplaceModel: No assets found in the asset bundle.");
                newBundle.Unload(false);
                return;
            }

            prefab = newBundle.LoadAsset<GameObject>(assetNames[0])?.transform;
            if (prefab == null)
            {
                Log.Error("ReplaceModel: Failed to load prefab from asset bundle.");
                newBundle.Unload(false);
                return;
            }

            Log.Msg($"ReplaceModel: Successfully loaded prefab {assetNames[0]} for slot '{slot}'.");

            // Grab the "Physics Driven Character" as your root
            Transform charRoot = physicsDrivenCharacter.transform;
            if (charRoot == null)
            {
                Log.Error("ReplaceModel: charRoot (physicsDrivenCharacter) is null.");
                newBundle.Unload(false);
                return;
            }

            // Update our config path for the chosen slot (so you can persist it):
            string relPath = Config.MakeRelativePath(newBundlePath);
            switch (slot)
            {
                case Slot.Body: Config.bodyModelPath = relPath; break;
                case Slot.Top: Config.topModelPath = relPath; break;
                case Slot.Gloves: Config.glovesModelPath = relPath; break;
                case Slot.Bottoms: Config.bottomsModelPath = relPath; break;
                case Slot.Socks: Config.socksModelPath = relPath; break;
                case Slot.Shoes: Config.shoesModelPath = relPath; break;
                case Slot.Bust: Config.bustModelPath = relPath; break;
                case Slot.Hat: Config.hatModelPath = relPath; break;
                case Slot.Hair: Config.hairModelPath = relPath; break;
                case Slot.Eyes: Config.eyesModelPath = relPath; break;
            }

            // **Use the dictionary** to find which child name we should look for.
            if (!slotNameMap.TryGetValue(slot, out string equipSlotName))
            {
                Log.Error($"ReplaceModel: No known mapping for slot '{slot}'.");
                newBundle.Unload(false);
                return;
            }

            // Find the actual slot transform. Notice the full path includes "Physics Skeleton/Character_Reference/"
            string fullPath = SLOT_PARENT_PATH + equipSlotName;
            if (equipSlotName == "Hat_EquipSlot"  || equipSlotName == "Hair_EquipSlot" || equipSlotName == "Eyes_EquipSlot")
            {
                fullPath = SLOT_PARENT_PATH + "HeadGear/" + equipSlotName;
            }
            Transform slotTransform = charRoot.Find(fullPath);
            if (slotTransform == null)
            {
                Log.Error($"ReplaceModel: Could not find transform '{fullPath}' under {charRoot.name}.");
                newBundle.Unload(false);
                return;
            }

            // In your bundle’s prefab, also look for the child named the same as the slot, if needed
            // (If your prefab root actually *is* the model, you might not need this step.)
            var prefabSlotChild = prefab.Find(equipSlotName);
            if (prefabSlotChild == null)
            {
                // If your model is just the prefab itself, use prefab.gameObject
                customObject = prefab.gameObject;
                /*if (slot == Slot.Hair)
                {
                    customObject.AddComponent<ComplexCharacterPartBehaviour>();
                }*/
            }
            else
            {
                customObject = prefabSlotChild.gameObject;
            }
            if (customObject == null)
            {
                Log.Error($"ReplaceModel: Could not find child '{equipSlotName}' in the loaded prefab.");
                newBundle.Unload(false);
                return;
            }

            // If it has Cloth, enable cloth
            if (customObject.GetComponent<Cloth>() != null)
            {
                Log.Msg("customObject has a Cloth Component! Enabling!");
                Cloth clothComp = slotTransform.GetComponent<Cloth>();
                if (clothComp != null) clothComp.enabled = true;
            }

            // Finally, equip
            var equipSlot = slotTransform.GetComponent<EquipSlot>();
            if (equipSlot != null)
            {
                equipSlot.Equip(customObject);
                Log.Msg($"ReplaceModel: Successfully equipped {slot} model.");
            }
            else
            {
                Log.Error($"ReplaceModel: No EquipSlot component found on {slotTransform.name}.");
            }

            // Unload the bundle
            newBundle.Unload(false);
        }

        public static void ReplaceMaterial(Slot slot, string newBundlePath)
        {
            if (string.IsNullOrEmpty(newBundlePath)) return;

            // Convert to relative path for config, then back to absolute for loading
            string relativePath = Config.MakeRelativePath(newBundlePath);
            string absolutePath = Config.MakeAbsolutePath(relativePath);

            // Grab the root
            Transform charRoot = physicsDrivenCharacter.transform;
            if (charRoot == null)
            {
                Log.Error("ReplaceMaterial: charRoot is null.");
                return;
            }

            // Use the dictionary to figure out which GameObject to get
            if (!slotNameMap.TryGetValue(slot, out string equipSlotName))
            {
                Log.Error($"ReplaceMaterial: No known mapping for slot '{slot}'.");
                return;
            }
            
            
            string fullPath = SLOT_PARENT_PATH + equipSlotName;
            if (equipSlotName == "Hat_EquipSlot"  || equipSlotName == "Hair_EquipSlot" || equipSlotName == "Eyes_EquipSlot")
            {
                fullPath = SLOT_PARENT_PATH + "HeadGear/" + equipSlotName;
            }
            Transform slotTransform = charRoot.Find(fullPath);
            
            if (slotTransform == null)
            {
                Log.Error($"ReplaceMaterial: Could not find transform '{fullPath}'.");
                return;
            }

            // Set the user’s config so you can persist the material path
            switch (slot)
            {
                case Slot.Body: Config.bodyMaterialPath = relativePath; break;
                case Slot.Top: Config.topMaterialPath = relativePath; break;
                case Slot.Gloves: Config.glovesMaterialPath = relativePath; break;
                case Slot.Bottoms: Config.bottomsMaterialPath = relativePath; break;
                case Slot.Socks: Config.socksMaterialPath = relativePath; break;
                case Slot.Shoes: Config.shoesMaterialPath = relativePath; break;
                case Slot.Bust: Config.bustMaterialPath = relativePath; break;
                case Slot.Hat: Config.hatMaterialPath = relativePath; break;
                case Slot.Hair: Config.hairMaterialPath = relativePath; break;
                case Slot.Eyes: Config.eyesMaterialPath = relativePath; break;
            }

            var newBundle = AssetBundle.LoadFromFile(absolutePath);
            if (newBundle == null)
            {
                Log.Error($"ReplaceMaterial: Failed to load {Path.GetFileName(absolutePath)} asset bundle.");
                return;
            }

            string[] assetNames = newBundle.GetAllAssetNames();
            if (assetNames.Length == 0)
            {
                Log.Error("ReplaceMaterial: No materials found in asset bundle.");
                newBundle.Unload(false);
                return;
            }

            Material newMaterial = newBundle.LoadAsset<Material>(assetNames[0]);
            var skinnedRenderer = slotTransform.GetComponent<SkinnedMeshRenderer>();
            var renderer = (Renderer)skinnedRenderer ?? slotTransform.GetComponent<MeshRenderer>();

            if (renderer != null && newMaterial != null)
            {
                renderer.sharedMaterial = newMaterial;
                Log.Msg($"ReplaceMaterial: Material replaced for slot {slot}.");
            }
            else
            {
                Log.Error($"ReplaceMaterial: Could not apply new material on slot {slot}.");
            }

            newBundle.Unload(false);
        }
        
       
        public static void SaveCurrentPreset(string presetName)
        {
            ClothingPreset preset = new ClothingPreset { name = presetName };

            // Model paths -> relative
            preset.modelPaths[Slot.Body] = Config.MakeRelativePath(Config.bodyModelPath);
            preset.modelPaths[Slot.Top] = Config.MakeRelativePath(Config.topModelPath);
            preset.modelPaths[Slot.Bottoms] = Config.MakeRelativePath(Config.bottomsModelPath);
            preset.modelPaths[Slot.Gloves] = Config.MakeRelativePath(Config.glovesModelPath);
            preset.modelPaths[Slot.Socks] = Config.MakeRelativePath(Config.socksModelPath);
            preset.modelPaths[Slot.Shoes] = Config.MakeRelativePath(Config.shoesModelPath);
            preset.modelPaths[Slot.Bust] = Config.MakeRelativePath(Config.bustModelPath);
            preset.modelPaths[Slot.Hat] = Config.MakeRelativePath(Config.hatModelPath);
            preset.modelPaths[Slot.Hair] = Config.MakeRelativePath(Config.hairModelPath);
            preset.modelPaths[Slot.Eyes] = Config.MakeRelativePath(Config.eyesModelPath);

            

            // Material paths -> relative
            preset.materialPaths[Slot.Body] = Config.MakeRelativePath(Config.bodyMaterialPath);
            preset.materialPaths[Slot.Top] = Config.MakeRelativePath(Config.topMaterialPath);
            preset.materialPaths[Slot.Bottoms] = Config.MakeRelativePath(Config.bottomsMaterialPath);
            preset.materialPaths[Slot.Gloves] = Config.MakeRelativePath(Config.glovesMaterialPath);
            preset.materialPaths[Slot.Socks] = Config.MakeRelativePath(Config.socksMaterialPath);
            preset.materialPaths[Slot.Shoes] = Config.MakeRelativePath(Config.shoesMaterialPath);
            preset.materialPaths[Slot.Bust] = Config.MakeRelativePath(Config.bustMaterialPath);
            preset.materialPaths[Slot.Hat] = Config.MakeRelativePath(Config.hatMaterialPath);
            preset.materialPaths[Slot.Hair] = Config.MakeRelativePath(Config.hairMaterialPath);
            preset.materialPaths[Slot.Eyes] = Config.MakeRelativePath(Config.eyesMaterialPath);
            
            ClothingPreset.SavePreset(preset);
        }

        public static void LoadPreset(string presetName)
        {
            ClothingPreset preset = ClothingPreset.LoadPreset(presetName);
            if (preset == null) return;

            foreach (var kvp in preset.modelPaths)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    ReplaceModel(kvp.Key, Config.MakeAbsolutePath(kvp.Value));
                }
            }

            foreach (var kvp in preset.materialPaths)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    ReplaceMaterial(kvp.Key, Config.MakeAbsolutePath(kvp.Value));
                }
            }

            Log.Msg($"Preset '{presetName}' loaded.");
            Config.lastLoadedPresetCharacter = presetName;
            Config.Save();
        }
        public static void UpdateAllPresets()
        {
            List<string> presetNames = ClothingPreset.GetAvailablePresets();
            foreach (string presetName in presetNames)
            {
                if (presetName == "DefaultPreset") continue;

                ClothingPreset preset = ClothingPreset.LoadPreset(presetName);
                if (preset == null) continue;

                bool updated = false;

                // Default paths for missing slots
                Dictionary<Slot, string> defaultModelPaths = new Dictionary<Slot, string>()
                {
                    { Slot.Bust, "Character\\Bust\\nonebust\\nonebust.model" },
                    { Slot.Hat, "Character\\Hat\\nonehat\\nonehat.model" },
                    { Slot.Hair, "Character\\Hair\\nonehair\\nonehair.model" },
                    { Slot.Eyes, "Character\\Eyes\\noneeyes\\noneeyes.model" }
                };

                Dictionary<Slot, string> defaultMaterialPaths = new Dictionary<Slot, string>()
                {
                    { Slot.Bust, "Character\\Bust\\nonebust\\nonebust.material" },
                    { Slot.Hat, "Character\\Hat\\nonehat\\nonehat.material" },
                    { Slot.Hair, "Character\\Hair\\nonehair\\nonehair.material" },
                    { Slot.Eyes, "Character\\Eyes\\noneeyes\\noneeyes.material" }
                };

                // Ensure missing slots exist in modelPaths
                foreach (var slot in defaultModelPaths.Keys)
                {
                    if (!preset.modelPaths.ContainsKey(slot))
                    {
                        preset.modelPaths[slot] = defaultModelPaths[slot];
                        updated = true;
                    }
                }

                // Ensure missing slots exist in materialPaths
                foreach (var slot in defaultMaterialPaths.Keys)
                {
                    if (!preset.materialPaths.ContainsKey(slot))
                    {
                        preset.materialPaths[slot] = defaultMaterialPaths[slot];
                        updated = true;
                    }
                }

                if (updated)
                {
                    ClothingPreset.SavePreset(preset);
                    Log.Msg($"Preset '{preset.name}' updated with missing slots.");
                }
            }
        }

        
    }
}
