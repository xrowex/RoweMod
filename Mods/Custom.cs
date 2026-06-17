using System.Collections;
using Il2CppInterop.Runtime;
using Il2CppMashBox.Character.Scripts;
using rowemod;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;
using static rowemod.Utils.Memory;
using static rowemod.Menu;
using MelonLoader;
using Il2CppMagicaCloth2;

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
            Eyewear
        }

        // Map your enum to the ACTUAL child names that appear in the screenshot:
        // "HeadGear", "EquipSlot_Body", "EquipSlot_Shirt", "EquipSlot_Pants", etc.
        public static readonly Dictionary<Slot, string> SlotNameMap = new Dictionary<Slot, string>()
        {
            { Slot.Hat, "Hat_EquipSlot" },
            { Slot.Hair, "Hair_EquipSlot" },
            { Slot.Eyes, "Eyes_EquipSlot" },
            { Slot.Body, "EquipSlot_Body" },
            { Slot.Top, "EquipSlot_Shirt" },
            { Slot.Gloves, "EquipSlot_Gloves" },
            { Slot.Bottoms, "EquipSlot_Pants" },
            { Slot.Socks, "EquipSlot_Socks" },
            { Slot.Shoes, "EquipSlot_Shoes" },
            { Slot.Bust, "EquipSlot_Bust" },
            { Slot.Eyewear, "Eyewear_EquipSlot" }
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

        public static void ResetTabState()
        {
            foreach (Slot slot in Enum.GetValues(typeof(Slot)))
            {
                _slotVisibility[slot] = true;
                ToggleSlotVisibility(slot, true);
            }

            _selectedModelDirectories.Clear();
            _slotObjects.Clear();
            _lastSelectedSlot = null;
            _newPresetName = string.Empty;
            _selectedPresetIndex = 0;
            inModelsTab = true;
        }


        public static void DrawCharacterTab()
        {
            // --- TOP BAR ------------------------------------------------------------
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Character</b>", Menu.labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset Character", Menu.redButtonStyle, GUILayout.Height(26)))
            {
                Config.ResetCharacterTab();
                Config.Save();
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
                    MelonCoroutines.Start(LoadPreset(availablePresets[0]));
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
                            MelonCoroutines.Start(LoadPreset(availablePresets[_selectedPresetIndex]));
                        }

                        if (availablePresets[i] != "DefaultPreset" &&
                            GUILayout.Button("X", Menu.redButtonStyle, GUILayout.Width(30)))
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
                Log.Error($"[ToggleSlotVisibility] No mapped name found for slot '{slot}'.");
                return;
            }

            string basePath = SlotParentPath + equipSlotName;
            if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes || slot == Slot.Eyewear)
                basePath = SlotParentPath + "HeadGear/" + equipSlotName;

            // Helper function to toggle visibility on a specific character
            void ApplyToCharacter(GameObject character, string label)
            {
                if (character == null)
                {
                    Log.Warning($"[ToggleSlotVisibility] {label} character is null — skipping.");
                    return;
                }

                string fullPath = basePath;
                GameObject slotObject = null;

                // Try Physics Skeleton path
                slotObject = character.transform.Find(fullPath)?.gameObject;

                // Fallback to Skeleton
                if (slotObject == null)
                {
                    string fallbackPath = fullPath.Replace("Physics Skeleton", "Skeleton");
                    slotObject = character.transform.Find(fallbackPath)?.gameObject;

                    if (slotObject != null)
                    {
                        fullPath = fallbackPath;
                        Log.Msg($"[ToggleSlotVisibility] {label}: Fallback succeeded — found '{fallbackPath}'.");
                    }
                }

                if (slotObject == null)
                {
                    Log.Warning($"[ToggleSlotVisibility] {label}: Could not find '{basePath}' or fallback path.");
                    return;
                }

                // Cache slot per-character separately if desired
                _slotObjects[slot] = slotObject;

                slotObject.SetActive(isVisible);
                Log.Msg($"[ToggleSlotVisibility] {label}: '{fullPath}' set to {isVisible}");
            }

            // Apply to both characters if they exist
            ApplyToCharacter(Memory.menuPlayer, "MenuPlayer");
            ApplyToCharacter(Memory.gamePlayer, "GamePlayer");
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
                        ReplaceModel(slot, file);;
                    }
                }
            }
            else
            {
                // If we have a selected model directory, show any *.material files in it
                if (_selectedModelDirectories.TryGetValue(slot, out string modelDirectory)
                    && Directory.Exists(modelDirectory))
                {
                    string[] materialFiles =
                        Directory.GetFiles(modelDirectory, "*.material", SearchOption.AllDirectories);
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

            string directory = Path.GetDirectoryName(newBundlePath);
            if (!string.IsNullOrEmpty(directory))
                _selectedModelDirectories[slot] = directory;

            var newBundle = AssetBundle.LoadFromFile(newBundlePath);
            if (newBundle == null)
            {
                Log.Error($"ReplaceModel: Failed to load asset bundle from {newBundlePath}");
                return;
            }

            string[] assetNames = newBundle.GetAllAssetNames();
            if (assetNames.Length == 0)
            {
                Log.Error("ReplaceModel: No assets found in the asset bundle.");
                newBundle.Unload(false);
                return;
            }

            Transform prefab = newBundle.LoadAsset<GameObject>(assetNames[0])?.transform;
            if (prefab == null)
            {
                Log.Error("ReplaceModel: Failed to load prefab from asset bundle.");
                newBundle.Unload(false);
                return;
            }
    
            // Store config path
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
                case Slot.Eyewear: Config.character.eyewearModelPath = relPath; break;
                
            }

            // Reusable function to apply model to a specific character
            void ApplyToCharacter(GameObject character, string label)
            {
                if (character == null)
                {
                    Log.Warning($"[ReplaceModel] {label} character is null — skipping.");
                    return;
                }

                if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
                {
                    Log.Error($"[ReplaceModel] {label}: No known mapping for slot '{slot}'.");
                    return;
                }

                string fullPath = SlotParentPath + equipSlotName;
                if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes)
                    fullPath = SlotParentPath + "HeadGear/" + equipSlotName;

                Transform slotTransform = character.transform.Find(fullPath);
                if (slotTransform == null)
                {
                    string fallbackPath = fullPath.Replace("Physics Skeleton", "Skeleton");
                    slotTransform = character.transform.Find(fallbackPath);

                    if (slotTransform != null)
                    {
                        fullPath = fallbackPath;
                        Log.Msg($"[ReplaceModel] {label}: Fallback succeeded — found '{fallbackPath}'.");
                    }
                }

                if (slotTransform == null)
                {
                    Log.Warning($"[ReplaceModel] {label}: Could not find transform '{fullPath}'.");
                    return;
                }

                // Find prefab’s slot child
                GameObject modelObject = prefab.Find(equipSlotName)?.gameObject ?? prefab.gameObject;
                if (modelObject == null)
                {
                    Log.Error($"[ReplaceModel] {label}: No valid prefab object for slot {slot}.");
                    return;
                }
                
                // Disable SM_Body GameObjects
                foreach (Transform child in modelObject.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.Contains("SM_Body")) 
                    {
                        Log.Msg("Removing erroneous skinned mesh renderer : " + child.name);
                        UnityEngine.Object.Destroy(child.gameObject);
                    }
                    if (child.name.Contains("SM_Bust")) 
                    {
                        Log.Msg("Removing erroneous skinned mesh renderer : " + child.name);
                        UnityEngine.Object.Destroy(child.gameObject);
                    }
                }
                
                // Check for MagicaCloth component
                var magicaCloth = modelObject.GetComponent<MagicaCloth>();
                if (magicaCloth != null)
                {
                    Log.Msg("Found MagicaCloth component, running BuildAndRun");
                    magicaCloth.BuildAndRun();
                }
                
                var equipSlot = slotTransform.GetComponent<EquipSlot>();
                if (equipSlot == null)
                {
                    Log.Error($"[ReplaceModel] {label}: No EquipSlot component found on {slotTransform.name}.");
                    return;
                }
                
                
                    
                equipSlot.Equip(modelObject);
                Log.Msg($"[ReplaceModel] {label}: Successfully equipped {slot} model.");
                
                
                
            }
            
            //Update references to menuPlayer and gamePlayer
            
            
            // Apply to both characters
            ApplyToCharacter(menuPlayer, "MenuPlayer");
            ApplyToCharacter(gamePlayer, "GamePlayer");
            
                
            newBundle.Unload(false);

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

            // ---------- 2) Resolve an absolute path for loading ----------
            string loadPath = Path.IsPathRooted(selectedPath)
                ? selectedPath
                : Config.MakeAbsolutePath(selectedPath);

            if (!File.Exists(loadPath))
            {
                Log.Error($"ReplaceMaterial: File does not exist: {loadPath}");
                return;
            }

            Log.Msg($"ReplaceMaterial: Loading material bundle from: {loadPath}");

            AssetBundle bundle = AssetBundle.LoadFromFile(loadPath);
            if (bundle == null)
            {
                Log.Error($"ReplaceMaterial: AssetBundle.LoadFromFile returned null for: {loadPath}");
                return;
            }

            Material newMat = null;

            // ---------- 3) Inspect bundle assets ----------
            var assetNames = bundle.GetAllAssetNames();
            if (assetNames == null || assetNames.Length == 0)
            {
                Log.Error("[ReplaceMaterial] No assets found in material bundle.");
                return;
            }

            // 3A) Try to load a .mat directly
            string loadedMatAssetName = null;
            foreach (string n in assetNames)
            {
                Log.Msg("Bundle contains : " + n);
                if (n.EndsWith(".mat"))
                {
                    loadedMatAssetName = n;
                    newMat = bundle.LoadAsset<Material>(n);
                    break;
                }
            }

            if (loadedMatAssetName != null)
            {
                if (newMat == null)
                {
                    Log.Error($"[ReplaceMaterial] LoadAsset<Material>(\"{loadedMatAssetName}\") returned null.");
                }
                else
                {
                    Log.Msg($"[ReplaceMaterial] Loaded material asset '{loadedMatAssetName}'.");
                }
            }

            // 3B) Fallback: bundle is like your model bundles (prefab-only)
            if (newMat == null)
            {
                string firstAsset = assetNames[0];
                Log.Warning($"[ReplaceMaterial] No valid .mat assets in bundle, first asset is '{firstAsset}'. " +
                            "Trying to grab a material off the prefab (model-style bundle).");

                var prefab = bundle.LoadAsset<GameObject>(firstAsset);
                if (prefab == null)
                {
                    Log.Error($"[ReplaceMaterial] Failed to load prefab '{firstAsset}' as GameObject.");
                }
                else
                {
                    var prefabRenderer = prefab.GetComponentInChildren<Renderer>();
                    if (prefabRenderer == null || prefabRenderer.sharedMaterial == null)
                    {
                        Log.Error($"[ReplaceMaterial] Prefab '{firstAsset}' has no renderer/material to steal.");
                    }
                    else
                    {
                        newMat = prefabRenderer.sharedMaterial;
                        Log.Msg($"[ReplaceMaterial] Using material '{newMat.name}' from prefab '{firstAsset}'.");
                    }
                }
            }

            // If we *still* don't have a material, bail out safely.
            if (newMat == null)
            {
                Log.Warning("[ReplaceMaterial] Could not resolve a material from bundle — aborting apply.");
                try
                {
                    bundle.Unload(false);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ReplaceMaterial] AssetBundle.Unload(false) threw: {ex}");
                }
                return;
            }

            // ---------- 4) Apply to any available character(s) ----------
                void ApplyToCharacter(GameObject character, string label)
                {
                    if (!character)
                    {
                        Log.Warning($"[ReplaceMaterial] {label} is null — skipping.");
                        return;
                    }

                    if (!SlotNameMap.TryGetValue(slot, out string equipSlotName))
                    {
                        Log.Error($"[ReplaceMaterial] {label}: No mapping for slot '{slot}'.");
                        return;
                    }

                    // Find the slot transform on this character (Physics Skeleton first, then Skeleton)
                    string fullPath = SlotParentPath + equipSlotName;
                    if (slot == Slot.Hat || slot == Slot.Hair || slot == Slot.Eyes)
                        fullPath = SlotParentPath + "HeadGear/" + equipSlotName;

                    Transform slotTransform = character.transform.Find(fullPath);
                    if (!slotTransform)
                    {
                        string fallback = fullPath.Replace("Physics Skeleton", "Skeleton");
                        slotTransform = character.transform.Find(fallback);
                        if (slotTransform) fullPath = fallback;
                    }

                    if (!slotTransform)
                    {
                        Log.Warning($"[ReplaceMaterial] {label}: Could not find slot at '{fullPath}'.");
                        return;
                    }

                    var equipSlot = slotTransform.GetComponent<EquipSlot>();
                    if (equipSlot == null)
                    {
                        Log.Error($"[ReplaceMaterial] {label}: No EquipSlot on {slotTransform.name}.");
                        return;
                    }

                    
                    
                    
                    var renderer = equipSlot.GetComponentInChildren<Renderer>();
                    if (renderer == null)
                    {
                        Log.Warning($"[ReplaceMaterial] {label}: No renderers under equipped object for {slot}.");
                        return;
                    }
                    if (newMat!=null && renderer!=null)
                    {

                        renderer.sharedMaterial = newMat;
                        
                        Log.Msg($"[ReplaceMaterial] {label}: Applied material to {slot} on renderer.");
                    }
                    else
                    {
                        Log.Warning($"[ReplaceMaterial] {label}: newMat is null, cannot apply material.");
                    }
                        
                }

                ApplyToCharacter(menuPlayer, "MenuPlayer");
                ApplyToCharacter(gamePlayer, "GamePlayer");
                
            
                // Guard against IL2CPP weirdness on Unload
                try
                {
                    if (bundle != null)
                    {
                        bundle.Unload(false); // keep assets alive, just release file handle
                    }
                        
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ReplaceMaterial] AssetBundle.Unload(false) threw: {ex}");
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

        public static IEnumerator LoadPreset(string presetName)
        {
            ClothingPreset preset = ClothingPreset.Load(presetName);
            if (preset == null) yield break;
        
            foreach (var kvp in preset.ModelPaths)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    Log.Msg("Loading models for preset : " + preset.Name + " " + kvp.Key + " ," + Config.MakeAbsolutePath(kvp.Value));
                    ReplaceModel(kvp.Key, Config.MakeAbsolutePath(kvp.Value));
                }
                else
                {
                    Log.Error("[LoadPreset] Value is null or empty : " + kvp);
                }
            }

            yield return new WaitForSeconds(0.15f);

            foreach (var kvp in preset.MaterialPaths)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    Log.Msg("Loading materials for preset : " + preset.Name + " " + kvp.Key + " ," + Config.MakeAbsolutePath(kvp.Value));
                    ReplaceMaterial(kvp.Key, Config.MakeAbsolutePath(kvp.Value));
                }
            }

            yield return new WaitForSeconds(0.15f);

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
            else
            {
                Log.Error("Selected slot is Body, not auto applying.");
            }



        }
    }
}
