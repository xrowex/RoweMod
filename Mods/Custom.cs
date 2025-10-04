using System.Collections;
using Il2CppMashBox.Character.Scripts;
using MelonLoader;
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
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(availablePresets[i], Menu.highQualityButtonStyle))
                        {
                            _selectedPresetIndex = i;
                            LoadPreset(availablePresets[_selectedPresetIndex]);
                        }
                        if (availablePresets[i] != "DefaultPreset" && GUILayout.Button("X", Menu.redButtonStyle, GUILayout.Width(30)))
                        {
                            ClothingPreset.Delete(availablePresets[i]);
                            if (i == _selectedPresetIndex) _selectedPresetIndex = 0;
                            break;
                        }
                        GUILayout.EndHorizontal();
                        
                    }

                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndHorizontal();
        
    }

        private static Dictionary<Slot, GameObject> _slotObjects = new Dictionary<Slot, GameObject>();

        public static void ToggleSlotVisibility(Slot slot, bool isVisible)
        {
            if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
            {
                Debug.LogError($"ToggleSlotVisibility: No mapped name found for slot '{slot}'.");
                return;
            }

            // Start with Physics Skeleton path
            string fullPath = SlotParentPath + equipSlotName;
            if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes)
                fullPath = SlotParentPath + "HeadGear/" + equipSlotName;

            

            // Try Physics Skeleton first
            GameObject slotObject = GameObject.Find(fullPath);

            // Fallback to Skeleton path if not found
            if (slotObject == null)
            {
                string fallbackPath = fullPath.Replace("Physics Skeleton", "Skeleton");
                slotObject = GameObject.Find(fallbackPath);
                if (slotObject != null)
                {
                    Log.Msg($"[rowemod] Fallback path used: {fallbackPath}");
                }
            }

            if (slotObject == null)
            {
                Debug.LogWarning($"[rowemod] Slot GameObject not found: {fullPath}");
                return;
            }

            _slotObjects[slot] = slotObject;
            slotObject.SetActive(isVisible);
            Debug.Log($"[rowemod] Slot GameObject '{slotObject.name}' set to {isVisible}.");
        }





        public static void ListCharacterBundles(Slot slot)
        {
            EnsureSlotModels(slot);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Models", highQualityButtonStyle)) inModelsTab = true;
            if (GUILayout.Button("Materials", highQualityButtonStyle)) inModelsTab = false;
            GUILayout.EndHorizontal();

            if (inModelsTab)
            {
                foreach (var entry in _slotModels[slot])
                {
                    GUILayout.BeginHorizontal();

                    if (entry.materialPaths.Length == 0)
                    {
                        // request default model icon
                        Texture2D tex = RequestOrLoadIcon(LoadPrefab(entry.modelPath), entry.modelPath);
                        if (GUILayout.Button(tex, GUILayout.Width(128), GUILayout.Height(128)))
                            ReplaceModel(slot, entry.modelPath);
                    }
                    else
                    {
                        foreach (var matFile in entry.materialPaths)
                        {
                            Material mat = LoadMaterial(matFile);
                            Texture2D tex = RequestOrLoadIcon(LoadPrefab(entry.modelPath), entry.modelPath, matFile, mat);

                            if (GUILayout.Button(tex, GUILayout.Width(128), GUILayout.Height(128)))
                            {
                                ReplaceModel(slot, entry.modelPath);
                                ReplaceMaterial(slot, matFile);
                            }
                        }
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
        }
        // Loads a prefab from a .model bundle temporarily
        private static GameObject LoadPrefab(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                return null;

            GameObject prefab = null;
            var bundle = AssetBundle.LoadFromFile(modelPath);
            if (bundle != null)
            {
                string[] assetNames = bundle.GetAllAssetNames();
                if (assetNames.Length > 0)
                    prefab = bundle.LoadAsset<GameObject>(assetNames[0]);
                bundle.Unload(false); // release bundle but keep asset alive
            }
            return prefab;
        }
        
        // Loads a Material from a .material bundle temporarily
        private static Material LoadMaterial(string matPath)
        {
            if (string.IsNullOrEmpty(matPath) || !File.Exists(matPath))
                return null;

            Material mat = null;
            var bundle = AssetBundle.LoadFromFile(matPath);
            if (bundle != null)
            {
                mat = bundle.LoadAllAssets<Material>().FirstOrDefault();
                bundle.Unload(false);
            }
            return mat;
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

            //Log.Msg($"ReplaceModel: Successfully loaded prefab {assetNames[0]} for slot '{slot}'.");

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
            if (equipSlotName == "Hat_EquipSlot" || equipSlotName == "Hair_EquipSlot" || equipSlotName == "Eyes_EquipSlot")
            {
                fullPath = SlotParentPath + "HeadGear/" + equipSlotName;
            }
            Transform slotTransform = charRoot.Find(fullPath);

            //change the slot transform name to see if it works in TheShop
            if (slotTransform == null)
            {
                Log.Error($"ReplaceModel: Could not find transform '{fullPath}' under {charRoot.name}.");
                Log.Error($"ReplaceModel: Trying to find '{fullPath.Replace("Physics Skeleton", "Skeleton")}' instead.");
                fullPath = fullPath.Replace("Physics Skeleton", "Skeleton");
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
                //Log.Msg($"ReplaceModel: Successfully equipped {slot} model.");
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
        

        // --- NEW: helper ---
        private static void OpenMaterialsTabAndAutoSelectFirst(Slot slot)
        {
            // Open the Materials tab in UI
            inModelsTab = false;

            if (slot != Slot.Body)
            {
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

        private static IEnumerator EnableClothNextFrame(Transform charRoot, GameObject equippedRoot)
        {
            // Wait one frame so parenting/binding finishes inside Equip()
            yield return null;

            if (equippedRoot == null) yield break;

            // (Optional) gather colliders from the character if your Cloth needs them
            var capsules = charRoot.GetComponentsInChildren<CapsuleCollider>(true);

            foreach (var cloth in equippedRoot.GetComponentsInChildren<Cloth>(true))
            {
                // If your game expects colliders, wire them here (optional):
                // cloth.capsuleColliders = capsules;

                // Safety: enable at the end
                cloth.enabled = true;
            }
        }
        // --- Add fields in Custom.cs ---
        private static Dictionary<string, Texture2D> _modelPreviews = new Dictionary<string, Texture2D>();
        private static Dictionary<string, Texture2D> _materialPreviews = new Dictionary<string, Texture2D>();
        private static UnityEngine.Camera _previewCamera;
        private static GameObject _previewRoot;

        // --- Utility: ensure hidden preview scene exists ---
        private static void EnsurePreviewCamera()
        {
            if (_previewCamera != null) return;

            _previewRoot = new GameObject("RoweMod_PreviewRoot");
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;

            var camGO = new GameObject("RoweMod_PreviewCam");
            camGO.transform.SetParent(_previewRoot.transform);
            _previewCamera = camGO.AddComponent<UnityEngine.Camera>();
            _previewCamera.backgroundColor = Color.clear;
            _previewCamera.clearFlags = CameraClearFlags.Color;
            _previewCamera.orthographic = false;
            _previewCamera.enabled = false; // manual rendering only

        }

        // --- Cache + queue ---
        private static Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>();
        private static Queue<PreviewJob> _previewQueue = new Queue<PreviewJob>();
        private static bool _isGenerating = false;

        private class PreviewJob
        {
            public GameObject prefab;
            public string key;
            public Material material;
            public string SavePath;
        }

        private static readonly string IconCachePath = Path.Combine(
            Path.GetDirectoryName(typeof(Custom).Assembly.Location),
            "rowemod", "BundleIcons"
        );
        
        private static Texture2D RequestOrLoadIcon(GameObject prefab, string bundlePath, string matPath = null, Material mat = null)
        {
            Directory.CreateDirectory(IconCachePath);

            // key for caching in memory
            string key = bundlePath + "::" + (matPath ?? "default");

            // 1. Already in memory?
            if (_previewCache.TryGetValue(key, out var tex) && tex != null && tex != Texture2D.grayTexture)
                return tex;

            // 2. Check disk
            string safeName = Path.GetFileNameWithoutExtension(bundlePath);
            if (!string.IsNullOrEmpty(matPath))
                safeName += "_" + Path.GetFileNameWithoutExtension(matPath);

            string iconFile = Path.Combine(IconCachePath, safeName + ".png");

            if (File.Exists(iconFile))
            {
                byte[] bytes = File.ReadAllBytes(iconFile);
                Texture2D diskTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                diskTex.LoadImage(bytes);
                _previewCache[key] = diskTex;
                return diskTex;
            }

            // 3. Not found → enqueue for generation
            _previewCache[key] = Texture2D.grayTexture;
            _previewQueue.Enqueue(new PreviewJob { prefab = prefab, key = key, material = mat, SavePath = iconFile });

            if (!_isGenerating)
                MelonCoroutines.Start(ProcessPreviewQueue());

            return Texture2D.grayTexture;
        }

        // --- Request a preview (lazy load) ---
        private static void RequestPreview(GameObject prefab, string key, Material mat = null)
        {
            if (_previewCache.ContainsKey(key)) return; // already cached

            // placeholder until real render is done
            _previewCache[key] = Texture2D.grayTexture;

            _previewQueue.Enqueue(new PreviewJob { prefab = prefab, key = key, material = mat });

            if (!_isGenerating)
                MelonCoroutines.Start(ProcessPreviewQueue());
        }

        // --- Coroutine processes queue gradually ---
        private static IEnumerator ProcessPreviewQueue()
        {
            _isGenerating = true;

            while (_previewQueue.Count > 0)
            {
                int jobsThisFrame = Mathf.Min(2, _previewQueue.Count); // limit work per frame
                for (int i = 0; i < jobsThisFrame; i++)
                {
                    var job = _previewQueue.Dequeue();
                    Texture2D tex = GeneratePreview(job.prefab, job.key, job.material);

                    if (!string.IsNullOrEmpty(job.SavePath))
                        File.WriteAllBytes(job.SavePath, tex.EncodeToPNG());

                    _previewCache[job.key] = tex;
                }
                yield return null; // give frame back to game
            }

            _isGenerating = false;
        }

        private static void EnsureSlotModels(Slot slot)
        {
            if (_slotModels.ContainsKey(slot)) return;

            string slotPath = Path.Combine(characterRootPath, slot.ToString());
            var entries = new List<ModelEntry>();

            foreach (string modelFile in Directory.GetFiles(slotPath, "*.model", SearchOption.AllDirectories))
            {
                string modelDir = Path.GetDirectoryName(modelFile);
                string[] mats = Directory.GetFiles(modelDir, "*.material", SearchOption.AllDirectories);
                entries.Add(new ModelEntry { modelPath = modelFile, materialPaths = mats });
            }

            _slotModels[slot] = entries;
        }


        // --- Heavy render (actual capture once) ---
        private static Texture2D GeneratePreview(GameObject prefab, string key, Material mat = null)
        {
            EnsurePreviewCamera();

            GameObject inst = GameObject.Instantiate(prefab, _previewRoot.transform);
            inst.transform.position = Vector3.zero;

            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 31;

            if (mat != null)
            {
                var rend = inst.GetComponentInChildren<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = mat;
            }

            // Handle SkinnedMeshRenderers (bake them for preview)
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh != null)
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked);

                    var tempGO = new GameObject("BakedMeshPreview");
                    tempGO.layer = 31;
                    tempGO.transform.SetParent(smr.transform, false);

                    var mf = tempGO.AddComponent<MeshFilter>();
                    mf.sharedMesh = baked;

                    var mr = tempGO.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = smr.sharedMaterial;
                }
            }

            // temporary light
            var lightGO = new GameObject("PreviewLight_Temp");
            lightGO.transform.SetParent(_previewRoot.transform);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(30, 30, 0);

            // calculate bounds
            Bounds b = new Bounds(inst.transform.position, Vector3.zero);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
                if (r != null) b.Encapsulate(r.bounds);

            float radius = b.extents.magnitude;
            float distance = Mathf.Max(0.1f, radius * 2.5f);

            _previewCamera.transform.position = b.center + new Vector3(0, 0, -distance);
            _previewCamera.transform.LookAt(b.center);
            _previewCamera.fieldOfView = 30f;
            _previewCamera.nearClipPlane = 0.01f;
            _previewCamera.farClipPlane = distance * 4f;

            _previewCamera.cullingMask = 1 << 31;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = Color.clear;

            // render
            RenderTexture rt = new RenderTexture(256, 256, 16);
            _previewCamera.targetTexture = rt;
            _previewCamera.Render();

            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            RenderTexture.active = null;
            _previewCamera.targetTexture = null;

            // cleanup
            GameObject.DestroyImmediate(inst);
            GameObject.DestroyImmediate(lightGO);
            rt.Release();

            return tex;
        }
        private class ModelEntry
        {
            public string modelPath;
            public string[] materialPaths;
        }
        private static Dictionary<Slot, List<ModelEntry>> _slotModels = new();










    }
}
