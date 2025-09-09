using Il2CppMashBox.Character.Scripts;
using rowemod;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;
using static rowemod.Utils.Memory;
using static rowemod.Menu;

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
            Bust
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

        public static Dictionary<Slot, bool> _slotVisibility = new Dictionary<Slot, bool>();

        public static bool inModelsTab = true;
        static Dictionary<Slot, string> _selectedModelDirectories = new Dictionary<Slot, string>();
        static Slot? _lastSelectedSlot = null;

        private static string _newPresetName = "";
        private static int _selectedPresetIndex = 0;


        public static void DrawCharacterTab()
        {
            // --- TOP BAR ------------------------------------------------------------
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Character</b>", Menu.labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset Character", Menu.redButtonStyle, GUILayout.Height(26)))
            {
                Config.ResetCharacterTab();
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            // -----------------------------------------------------------------------

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

            ClothingPreset defaultPreset = ClothingPreset.Load("DefaultPreset");
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
                ClothingPreset.Save(defaultPreset);
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

        private static Dictionary<Slot, GameObject> _slotObjects = new Dictionary<Slot, GameObject>();

        public static void ToggleSlotVisibility(Slot slot, bool isVisible)
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
                case Slot.Body: Config.character.bodyModelPath = relPath; break;
                case Slot.Top: Config.character.topModelPath = relPath; break;
                case Slot.Gloves: Config.character.glovesModelPath = relPath; break;
                case Slot.Bottoms: Config.character.bottomsModelPath = relPath; break;
                case Slot.Socks: Config.character.socksModelPath = relPath; break;
                case Slot.Shoes: Config.character.shoesModelPath = relPath; break;
                case Slot.Bust: Config.character.bustModelPath = relPath; break;
                case Slot.Hat: Config.character.hatModelPath = relPath; break;
                case Slot.Hair: Config.character.hairModelPath = relPath; break;
                case Slot.Eyes: Config.character.eyesModelPath = relPath; break;
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
            
            // --- NEW: switch to Materials tab and auto-apply the first available material ---
            // (Also ensure the UI is looking at the active slot)
            Menu.currentSlot = slot;
            OpenMaterialsTabAndAutoSelectFirst(slot);
        }

        public static void ReplaceMaterial(Slot slot, string selectedPath)
        {
            // ---------- 0) Validate ----------
            if (string.IsNullOrEmpty(selectedPath))
            {
                Log.Error("ReplaceMaterial: selectedPath is null/empty.");
                return;
            }

            // ---------- 1) Persist relative path (for config), but DO NOT round-trip for loading ----------
            string relativeForConfig = Config.MakeRelativePath(selectedPath);
            switch (slot)
            {
                case Slot.Body: Config.character.bodyMaterialPath = relativeForConfig; break;
                case Slot.Top: Config.character.topMaterialPath = relativeForConfig; break;
                case Slot.Gloves: Config.character.glovesMaterialPath = relativeForConfig; break;
                case Slot.Bottoms: Config.character.bottomsMaterialPath = relativeForConfig; break;
                case Slot.Socks: Config.character.socksMaterialPath = relativeForConfig; break;
                case Slot.Shoes: Config.character.shoesMaterialPath = relativeForConfig; break;
                case Slot.Bust: Config.character.bustMaterialPath = relativeForConfig; break;
                case Slot.Hat: Config.character.hatMaterialPath = relativeForConfig; break;
                case Slot.Hair: Config.character.hairMaterialPath = relativeForConfig; break;
                case Slot.Eyes: Config.character.eyesMaterialPath = relativeForConfig; break;
            }

            // ---------- 2) Resolve an absolute path for loading (no re-encode/decode loop) ----------
            string loadPath = selectedPath;
            if (!Path.IsPathRooted(loadPath))
                loadPath = Config.MakeAbsolutePath(loadPath);

            if (!File.Exists(loadPath))
            {
                Log.Error($"ReplaceMaterial: File does not exist: {loadPath}");
                return;
            }

            Log.Msg($"ReplaceMaterial: Loading material bundle from: {loadPath}");

            // ---------- 3) Load the bundle & pull a Material ----------
            var bundle = AssetBundle.LoadFromFile(loadPath);
            if (bundle == null)
            {
                Log.Error($"ReplaceMaterial: AssetBundle.LoadFromFile returned null for: {loadPath}");
                return;
            }

            try
            {
                Material newMat = null;

                // Prefer typed load (safer than first asset name)
                var mats = bundle.LoadAllAssets<Material>();
                if (mats != null && mats.Length > 0)
                {
                    newMat = mats[0];
                }
                else
                {
                    // Extra diagnostics if bundle has no Materials
                    var names = bundle.GetAllAssetNames();
                    Log.Error("ReplaceMaterial: No Material assets found in bundle. Assets present: " +
                              (names == null ? "(none)" : string.Join(", ", names)));
                    return;
                }

                if (newMat == null)
                {
                    Log.Error("ReplaceMaterial: Loaded material is null.");
                    return;
                }

                // ---------- 4) Find the correct slot transform ----------
                if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
                {
                    Log.Error($"ReplaceMaterial: No mapping for slot '{slot}'.");
                    return;
                }

                Transform charRoot = physicsDrivenCharacter?.transform;
                if (charRoot == null)
                {
                    Log.Error("ReplaceMaterial: physicsDrivenCharacter is null.");
                    return;
                }

                string fullPath = SlotParentPath + equipSlotName;
                if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes)
                    fullPath = SlotParentPath + "HeadGear/" + equipSlotName;

                Transform slotTransform = charRoot.Find(fullPath);
                if (slotTransform == null)
                {
                    // Proper immediate fallback + retry
                    string fallbackPath = fullPath.Replace("Physics Skeleton", "Skeleton");
                    slotTransform = charRoot.Find(fallbackPath);
                }

                if (slotTransform == null)
                {
                    Log.Error($"ReplaceMaterial: Could not find slot at '{fullPath}' (and Skeleton fallback).");
                    return;
                }

                // Assign to renderer
                Renderer renderer = slotTransform.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer == null)
                    renderer = slotTransform.GetComponentInChildren<MeshRenderer>();

                if (renderer != null && newMat != null)
                {
                    renderer.sharedMaterial = newMat;
                    //Log.Msg($"ReplaceMaterial: Material replaced for slot {slot} (decals disabled).");
                }
                else
                {
                    Log.Error($"ReplaceMaterial: Could not apply new material on slot {slot}.");
                }

            }
            finally
            {
                // Keep loaded assets alive; just release the file handle
                bundle.Unload(false);
            }
        }



        public static void SaveCurrentPreset(string presetName)
        {
            ClothingPreset preset = new ClothingPreset { Name = presetName };

            // Model paths -> relative
            preset.ModelPaths[Slot.Body] = Config.MakeRelativePath(Config.character.bodyModelPath);
            preset.ModelPaths[Slot.Top] = Config.MakeRelativePath(Config.character.topModelPath);
            preset.ModelPaths[Slot.Bottoms] = Config.MakeRelativePath(Config.character.bottomsModelPath);
            preset.ModelPaths[Slot.Gloves] = Config.MakeRelativePath(Config.character.glovesModelPath);
            preset.ModelPaths[Slot.Socks] = Config.MakeRelativePath(Config.character.socksModelPath);
            preset.ModelPaths[Slot.Shoes] = Config.MakeRelativePath(Config.character.shoesModelPath);
            preset.ModelPaths[Slot.Bust] = Config.MakeRelativePath(Config.character.bustModelPath);
            preset.ModelPaths[Slot.Hat] = Config.MakeRelativePath(Config.character.hatModelPath);
            preset.ModelPaths[Slot.Hair] = Config.MakeRelativePath(Config.character.hairModelPath);
            preset.ModelPaths[Slot.Eyes] = Config.MakeRelativePath(Config.character.eyesModelPath);

            

            // Material paths -> relative
            preset.MaterialPaths[Slot.Body] = Config.MakeRelativePath(Config.character.bodyMaterialPath);
            preset.MaterialPaths[Slot.Top] = Config.MakeRelativePath(Config.character.topMaterialPath);
            preset.MaterialPaths[Slot.Bottoms] = Config.MakeRelativePath(Config.character.bottomsMaterialPath);
            preset.MaterialPaths[Slot.Gloves] = Config.MakeRelativePath(Config.character.glovesMaterialPath);
            preset.MaterialPaths[Slot.Socks] = Config.MakeRelativePath(Config.character.socksMaterialPath);
            preset.MaterialPaths[Slot.Shoes] = Config.MakeRelativePath(Config.character.shoesMaterialPath);
            preset.MaterialPaths[Slot.Bust] = Config.MakeRelativePath(Config.character.bustMaterialPath);
            preset.MaterialPaths[Slot.Hat] = Config.MakeRelativePath(Config.character.hatMaterialPath);
            preset.MaterialPaths[Slot.Hair] = Config.MakeRelativePath(Config.character.hairMaterialPath);
            preset.MaterialPaths[Slot.Eyes] = Config.MakeRelativePath(Config.character.eyesMaterialPath);
            
            ClothingPreset.Save(preset);
        }

        public static void LoadPreset(string presetName)
        {
            ClothingPreset preset = ClothingPreset.Load(presetName);
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
            // Apply visibility
            foreach (var kvp in preset.SlotVisibility)
            {
                _slotVisibility[kvp.Key] = kvp.Value;
                ToggleSlotVisibility(kvp.Key, kvp.Value);
            }
            Log.Msg($"Preset '{presetName}' loaded.");
            Config.character.lastLoadedPresetCharacter = presetName;
            Config.Save();
        }
        public static void UpdateAllPresets()
        {
            List<string> presetNames = ClothingPreset.GetAvailablePresets();
            foreach (string presetName in presetNames)
            {
                if (presetName == "DefaultPreset") continue;

                ClothingPreset preset = ClothingPreset.Load(presetName);
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
                    ClothingPreset.Save(preset);
                    Log.Error($"Preset '{preset.Name}' updated with missing slots.");
                }
            }
        }
        public static GameObject GetSlotObject(Slot slot)
        {
            if (_slotObjects.TryGetValue(slot, out GameObject go) && go != null)
                return go;

            if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
                return null;

            string fullPath = SlotParentPath + equipSlotName;
            if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes)
                fullPath = SlotParentPath + "HeadGear/" + equipSlotName;

            Transform charRoot = physicsDrivenCharacter.transform;
            Transform slotTransform = charRoot.Find(fullPath);

            if (slotTransform == null)
            {
                // Try Skeleton fallback
                fullPath = fullPath.Replace("Physics Skeleton", "Skeleton");
                slotTransform = charRoot.Find(fullPath);
            }

            if (slotTransform == null)
                return null;

            GameObject slotObject = slotTransform.gameObject;
            _slotObjects[slot] = slotObject;

            return slotObject;
        }
        // Add near other fields (already present): 
// static Dictionary<Slot, string> _selectedModelDirectories = new Dictionary<Slot, string>();

// --- NEW: helper ---
        private static void OpenMaterialsTabAndAutoSelectFirst(Slot slot)
        {
            // Open the Materials tab in UI
            inModelsTab = false;

            // Find the first .material inside the selected model's directory
            if (_selectedModelDirectories.TryGetValue(slot, out string modelDir) && Directory.Exists(modelDir))
            {
                string[] materialFiles = Directory.GetFiles(modelDir, "*.material", SearchOption.AllDirectories);
                if (materialFiles != null && materialFiles.Length > 0)
                {
                    string firstMat = materialFiles[0];
                    Log.Msg($"Auto-selecting first material for {slot}: {Path.GetFileName(firstMat)}");
                    ReplaceMaterial(slot, firstMat);
                    return;
                }
                else
                {
                    Log.Msg($"No *.material files found for {slot} in '{modelDir}'.");
                }
            }
            else
            {
                Log.Msg($"No model directory stored for {slot}; cannot auto-select material.");
            }
        }

        
    }
}
