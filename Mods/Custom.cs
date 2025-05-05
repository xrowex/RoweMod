using Il2CppMashBox.Character.Scripts;
using rowemod.Utils;
using UnityEngine;
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
        public static readonly Dictionary<Slot, string> SlotNameMap = new Dictionary<Slot, string>()
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
        private const string SlotParentPath = "Physics Skeleton/";
        
        //Change to just skeleton to try to work with new main menu? jk fuck that
        //private const string SLOT_PARENT_PATH = "Skeleton/";

        static Transform _prefab;
        static GameObject _customObject;
        public static string characterRootPath = Path.Combine(
            Path.GetDirectoryName(typeof(Custom).Assembly.Location),
            @"rowemod\Character"
        );
        private static Dictionary<Slot, bool> _slotVisibility = new Dictionary<Slot, bool>();

        public static bool inModelsTab = true;
        static Dictionary<Slot, string> _selectedModelDirectories = new Dictionary<Slot, string>();
        static Slot? _lastSelectedSlot = null;

        private static string _newPresetName = "";
        private static int _selectedPresetIndex = 0;
        
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
                    if (!_slotVisibility.ContainsKey(slot))
                    {
                        _slotVisibility[slot] = true; // Default to visible
                    }

                    // Toggle button (placed to the left of the slot button)
                    bool newState = GUILayout.Toggle(_slotVisibility[slot], "", GUILayout.Width(20));
                    if (newState != _slotVisibility[slot])
                    {
                        _slotVisibility[slot] = newState;
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

                ListCharacterBundles(currentSlot);
                GUILayout.EndHorizontal();
                GUILayout.Label("Presets", Menu.labelStyle);

                // Text field to enter new preset name
                _newPresetName = GUILayout.TextField(_newPresetName, 25);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Preset", Menu.highQualityButtonStyle))
                {
                    if (!string.IsNullOrWhiteSpace(_newPresetName))
                    {
                        SaveCurrentPreset(_newPresetName);
                        _newPresetName = ""; // Clear input field after saving
                        GUI.FocusControl(null);
                    }
                }

                List<string> availablePresets = ClothingPreset.GetAvailablePresets();

                ClothingPreset defaultPreset = ClothingPreset.LoadPreset("DefaultPreset");
                if (defaultPreset == null || 
                    defaultPreset.ModelPaths == null || 
                    defaultPreset.MaterialPaths == null || 
                    defaultPreset.ModelPaths.Count > 0 || 
                    defaultPreset.MaterialPaths.Count > 0)
                {
                    defaultPreset = new ClothingPreset 
                    { 
                        Name = "DefaultPreset", 
                        ModelPaths = new Dictionary<Slot, string>(), 
                        MaterialPaths = new Dictionary<Slot, string>() 
                    };
                    ClothingPreset.SavePreset(defaultPreset);
                }
                availablePresets.Add(defaultPreset.Name);

                if (availablePresets.Count > 0)
                {
                    _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, 0, availablePresets.Count - 1);

                    if (availablePresets.Count == 1)
                    {
                        _selectedPresetIndex = 0;
                        LoadPreset(availablePresets[0]);
                    }

                    if (availablePresets.Count > 0)
                    {
                        GUILayout.BeginVertical();
                        for (int i = 0; i < availablePresets.Count; i++)
                        {
                            if (GUILayout.Button(availablePresets[i], Menu.highQualityButtonStyle))
                            {
                                _selectedPresetIndex = i;
                                LoadPreset(availablePresets[_selectedPresetIndex]);
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

        private static Dictionary<Slot, GameObject> _slotObjects = new Dictionary<Slot, GameObject>();

        private static void ToggleSlotVisibility(Slot slot, bool isVisible)
        {
            // Ensure the slot exists in the dictionary
            if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
            {
                Debug.LogError($"ToggleSlotVisibility: No mapped name found for slot '{slot}'.");
                return;
            }

            // Construct the correct full path
            string fullPath = SlotParentPath + equipSlotName;
            if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes)
            {
                fullPath = SlotParentPath + "HeadGear/" + equipSlotName;
            }

            // Check if we already stored this GameObject
            if (!_slotObjects.TryGetValue(slot, out GameObject slotObject) || slotObject == null)
            {
                // Find and store the GameObject only once
                slotObject = GameObject.Find(fullPath);
                if (slotObject != null)
                {
                    _slotObjects[slot] = slotObject;
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
            if (_lastSelectedSlot != slot)
            {
                inModelsTab = true;
                _lastSelectedSlot = slot;
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
                if (_selectedModelDirectories.TryGetValue(slot, out string modelDirectory)
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
                _selectedModelDirectories[slot] = directory;
            }

            // Load the new asset bundle
            var newBundle = AssetBundle.LoadFromFile(newBundlePath);
            if (newBundle == null)
            {
                Log.Error($"ReplaceModel: Failed to load asset bundle from {newBundlePath}");
                return;
            }

            // Clean up old references
            if (_customObject != null)
            {
                GameObject.Destroy(_customObject);
                _customObject = null;
            }
            if (_prefab != null)
            {
                GameObject.Destroy(_prefab.gameObject);
                _prefab = null;
            }

            // Load the prefab (assuming the first asset is the prefab you want)
            string[] assetNames = newBundle.GetAllAssetNames();
            if (assetNames.Length == 0)
            {
                Log.Error("ReplaceModel: No assets found in the asset bundle.");
                newBundle.Unload(false);
                return;
            }

            _prefab = newBundle.LoadAsset<GameObject>(assetNames[0])?.transform;
            if (_prefab == null)
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
            if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
            {
                Log.Error($"ReplaceModel: No known mapping for slot '{slot}'.");
                newBundle.Unload(false);
                return;
            }

            // Find the actual slot transform. Notice the full path includes "Physics Skeleton/Character_Reference/"
            string fullPath = SlotParentPath + equipSlotName;
            if (equipSlotName == "Hat_EquipSlot"  || equipSlotName == "Hair_EquipSlot" || equipSlotName == "Eyes_EquipSlot")
            {
                fullPath = SlotParentPath + "HeadGear/" + equipSlotName;
            }
            Transform slotTransform = charRoot.Find(fullPath);
            
            //change the slot transform name to see if it works in TheShop
            if (slotTransform == null)
            {
                Log.Error($"ReplaceModel: Could not find transform '{fullPath}' under {charRoot.name}.");
                Log.Error($"ReplaceModel: Trying to find '{fullPath.Replace("Physics Skeleton","Skeleton")}' instead.");
                fullPath = fullPath.Replace("Physics Skeleton","Skeleton");
                slotTransform = charRoot.Find(fullPath);
            }
                
            
            if (slotTransform == null)
            {
                Log.Error($"ReplaceModel: Could not find transform '{fullPath}' under {charRoot.name}.");
                newBundle.Unload(false);
                return;
            }

            // In your bundle’s prefab, also look for the child named the same as the slot, if needed
            // (If your prefab root actually *is* the model, you might not need this step.)
            var prefabSlotChild = _prefab.Find(equipSlotName);
            if (prefabSlotChild == null)
            {
                // If your model is just the prefab itself, use prefab.gameObject
                _customObject = _prefab.gameObject;
                /*if (slot == Slot.Hair)
                {
                    customObject.AddComponent<ComplexCharacterPartBehaviour>();
                }*/
            }
            else
            {
                _customObject = prefabSlotChild.gameObject;
            }
            if (_customObject == null)
            {
                Log.Error($"ReplaceModel: Could not find child '{equipSlotName}' in the loaded prefab.");
                newBundle.Unload(false);
                return;
            }

            // If it has Cloth, enable cloth
            if (_customObject.GetComponent<Cloth>() != null)
            {
                Log.Msg("customObject has a Cloth Component! Enabling!");
                Cloth clothComp = slotTransform.GetComponent<Cloth>();
                if (clothComp != null) clothComp.enabled = true;
            }

            // Finally, equip
            var equipSlot = slotTransform.GetComponent<EquipSlot>();
            if (equipSlot != null)
            {
                equipSlot.Equip(_customObject);
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
            if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
            {
                Log.Error($"ReplaceMaterial: No known mapping for slot '{slot}'.");
                return;
            }
            
            
            string fullPath = SlotParentPath + equipSlotName;
            if (equipSlotName == "Hat_EquipSlot"  || equipSlotName == "Hair_EquipSlot" || equipSlotName == "Eyes_EquipSlot")
            {
                fullPath = SlotParentPath + "HeadGear/" + equipSlotName;
            }
            Transform slotTransform = charRoot.Find(fullPath);
            
            //change the slot transform name to see if it works in TheShop
            if(slotTransform == null)
                fullPath = fullPath.Replace("Physics Skeleton","Skeleton");
            
            //change the slot transform name to see if it works in TheShop
            if (slotTransform == null)
            {
                Log.Error($"ReplaceModel: Could not find transform '{fullPath}' under {charRoot.name}.");
                Log.Error($"ReplaceModel: Trying to find '{fullPath.Replace("Physics Skeleton", "Skeleton")}' instead.");
                fullPath = fullPath.Replace("Physics Skeleton", "Skeleton");
                slotTransform = charRoot.Find(fullPath);
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
            //var renderer = slotTransform.GetComponent<SkinnedMeshRenderer>() ?? (Renderer)slotTransform.GetComponent<MeshRenderer>();
            //wtf mash
            Renderer renderer = slotTransform.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null)
                renderer = slotTransform.GetComponentInChildren<MeshRenderer>();

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
            ClothingPreset preset = new ClothingPreset { Name = presetName };

            // Model paths -> relative
            preset.ModelPaths[Slot.Body] = Config.MakeRelativePath(Config.bodyModelPath);
            preset.ModelPaths[Slot.Top] = Config.MakeRelativePath(Config.topModelPath);
            preset.ModelPaths[Slot.Bottoms] = Config.MakeRelativePath(Config.bottomsModelPath);
            preset.ModelPaths[Slot.Gloves] = Config.MakeRelativePath(Config.glovesModelPath);
            preset.ModelPaths[Slot.Socks] = Config.MakeRelativePath(Config.socksModelPath);
            preset.ModelPaths[Slot.Shoes] = Config.MakeRelativePath(Config.shoesModelPath);
            preset.ModelPaths[Slot.Bust] = Config.MakeRelativePath(Config.bustModelPath);
            preset.ModelPaths[Slot.Hat] = Config.MakeRelativePath(Config.hatModelPath);
            preset.ModelPaths[Slot.Hair] = Config.MakeRelativePath(Config.hairModelPath);
            preset.ModelPaths[Slot.Eyes] = Config.MakeRelativePath(Config.eyesModelPath);

            

            // Material paths -> relative
            preset.MaterialPaths[Slot.Body] = Config.MakeRelativePath(Config.bodyMaterialPath);
            preset.MaterialPaths[Slot.Top] = Config.MakeRelativePath(Config.topMaterialPath);
            preset.MaterialPaths[Slot.Bottoms] = Config.MakeRelativePath(Config.bottomsMaterialPath);
            preset.MaterialPaths[Slot.Gloves] = Config.MakeRelativePath(Config.glovesMaterialPath);
            preset.MaterialPaths[Slot.Socks] = Config.MakeRelativePath(Config.socksMaterialPath);
            preset.MaterialPaths[Slot.Shoes] = Config.MakeRelativePath(Config.shoesMaterialPath);
            preset.MaterialPaths[Slot.Bust] = Config.MakeRelativePath(Config.bustMaterialPath);
            preset.MaterialPaths[Slot.Hat] = Config.MakeRelativePath(Config.hatMaterialPath);
            preset.MaterialPaths[Slot.Hair] = Config.MakeRelativePath(Config.hairMaterialPath);
            preset.MaterialPaths[Slot.Eyes] = Config.MakeRelativePath(Config.eyesMaterialPath);
            
            ClothingPreset.SavePreset(preset);
        }

        public static void LoadPreset(string presetName)
        {
            ClothingPreset preset = ClothingPreset.LoadPreset(presetName);
            if (preset == null) return;

            foreach (var kvp in preset.ModelPaths)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    ReplaceModel(kvp.Key, Config.MakeAbsolutePath(kvp.Value));
                }
            }

            foreach (var kvp in preset.MaterialPaths)
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
                    if (!preset.ModelPaths.ContainsKey(slot))
                    {
                        preset.ModelPaths[slot] = defaultModelPaths[slot];
                        updated = true;
                    }
                }

                // Ensure missing slots exist in materialPaths
                foreach (var slot in defaultMaterialPaths.Keys)
                {
                    if (!preset.MaterialPaths.ContainsKey(slot))
                    {
                        preset.MaterialPaths[slot] = defaultMaterialPaths[slot];
                        updated = true;
                    }
                }

                if (updated)
                {
                    ClothingPreset.SavePreset(preset);
                    Log.Error($"Preset '{preset.Name}' updated with missing slots.");
                }
            }
        }

        
    }
}
