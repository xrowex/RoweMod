using UnityEngine;
using static rowemod.Menu;
using static rowemod.Utils.Memory;
using rowemod.Utils;
using Il2CppMashBox.Addons.ContentManagment;
using MelonLoader.Utils;

namespace rowemod.Mods
{
    public static class BikeMaterialsLoader
    {
        public static readonly string BikeRootPath =
            Path.Combine(MelonEnvironment.ModsDirectory, @"RoweMod\Bike");

        private static List<string> _bikeFolders = new List<string>();
        private static string _selectedFolder = string.Empty;

        public static EquipSlotVehicle[] equipSlotVehicles = null;

        private static string _selectedCategory = null;

        private static Dictionary<string, Dictionary<string, Texture2D>> _categoryPreviews = new Dictionary<string, Dictionary<string, Texture2D>>();
        private static Dictionary<string, Texture2D> _materialPreviews = new Dictionary<string, Texture2D>();
        private static Dictionary<string, bool> _categoryVisibility = new Dictionary<string, bool>();

        public static Dictionary<string, (string displayName, List<EquipSlotVehicle> slots)> categories =
    new Dictionary<string, (string displayName, List<EquipSlotVehicle> slots)>
    {
        { "Bars", ("Bars", new List<EquipSlotVehicle>()) },
        { "Bar End", ("Bar Ends", new List<EquipSlotVehicle>()) },
        { "BB", ("Bottom Bracket", new List<EquipSlotVehicle>()) },
        { "Chain", ("Chain", new List<EquipSlotVehicle>()) },
        { "Crank Arm", ("Crank Arm", new List<EquipSlotVehicle>()) },
        { "Forks", ("Forks", new List<EquipSlotVehicle>()) },
        { "Frame", ("Frame", new List<EquipSlotVehicle>()) },
        { "Grip", ("Grips", new List<EquipSlotVehicle>()) },
        { "Guard", ("Hub Guards", new List<EquipSlotVehicle>()) },
        { "Headset", ("Headsets", new List<EquipSlotVehicle>()) },
        { "Hub", ("Hubs", new List<EquipSlotVehicle>()) },
        { "Hub_Front", ("Front Hub", new List<EquipSlotVehicle>()) },
        { "Hub_Rear", ("Rear Hub", new List<EquipSlotVehicle>()) },
        { "Pedal", ("Pedals", new List<EquipSlotVehicle>()) },
        { "BMX_Peg_FrontLeft", ("Front Left Peg", new List<EquipSlotVehicle>()) },
        { "BMX_Peg_FrontRight", ("Front Right Peg", new List<EquipSlotVehicle>()) },
        { "BMX_Peg_RearLeft", ("Rear Left Peg", new List<EquipSlotVehicle>()) },
        { "BMX_Peg_RearRight", ("Rear Right Peg", new List<EquipSlotVehicle>()) },
        { "BMX_Peg", ("Pegs", new List<EquipSlotVehicle>()) },
        { "BMX_Nipples", ("Nipples", new List<EquipSlotVehicle>()) },
        { "BMX_Nipples_Front", ("Front Nipples", new List<EquipSlotVehicle>()) },
        { "BMX_Nipples_Rear", ("Rear Nipples", new List<EquipSlotVehicle>()) },
        { "BMX_Rim_Front", ("Front Rim", new List<EquipSlotVehicle>()) },
        { "BMX_Rim_Rear", ("Rear Rim", new List<EquipSlotVehicle>()) },
        { "BMX_Rim", ("Rims", new List<EquipSlotVehicle>()) },
        { "BMX_Valve Cap_Front", ("Front Valve Cap", new List<EquipSlotVehicle>()) },
        { "BMX_Valve Cap_Rear", ("Rear Valve Cap", new List<EquipSlotVehicle>()) },
        { "BMX_Valve_Front", ("Front Valve", new List<EquipSlotVehicle>()) },
        { "BMX_Valve_Rear", ("Rear Valve", new List<EquipSlotVehicle>()) },
        { "BMX_Mag_Front", ("Front Mag", new List<EquipSlotVehicle>()) },
        { "BMX_Mag_Rear", ("Rear Mag", new List<EquipSlotVehicle>()) },
        { "Seat", ("Seats", new List<EquipSlotVehicle>()) },
        { "SeatClamp", ("Seat Clamp", new List<EquipSlotVehicle>()) },
        { "Seat Post", ("Seat Posts", new List<EquipSlotVehicle>()) },
        { "BMX_Spokes_Front", ("Front Spokes", new List<EquipSlotVehicle>()) },
        { "BMX_Spokes_Rear", ("Rear Spokes", new List<EquipSlotVehicle>()) },
        { "BMX_Spokes", ("Spokes", new List<EquipSlotVehicle>()) },
        { "Sprocket", ("Sprockets", new List<EquipSlotVehicle>()) },
        { "Stem", ("Stems", new List<EquipSlotVehicle>()) },
        { "StemBolt", ("Stem Bolts", new List<EquipSlotVehicle>()) },
        { "Stem Cap", ("Stem Caps", new List<EquipSlotVehicle>()) },
        { "BMX_Tire_Front", ("Front Tire", new List<EquipSlotVehicle>()) },
        { "BMX_Tire_Rear", ("Rear Tire", new List<EquipSlotVehicle>()) },
        { "BMX_Tire", ("Tires", new List<EquipSlotVehicle>()) },
    };

        private static string _newPresetName = "";
        private static int _selectedPresetIndex = 0;

        public static void DrawBikeMaterialsTabUI()
        {
            try
            {
                GUILayout.BeginHorizontal();
                
                GUILayout.BeginVertical(GUILayout.Width(200));
                GUILayout.Label("Categories:", Menu.labelStyle);

                foreach (var category in categories)
                {
                    GUILayout.BeginHorizontal();

                    // Toggle
                    if (_categoryVisibility == null)
                    {
                        _categoryVisibility = new Dictionary<string, bool>(); // fail-safe init
                    }

                    bool currentVisible = true;
                    if (_categoryVisibility.ContainsKey(category.Key))
                        currentVisible = _categoryVisibility[category.Key];
                    else
                        _categoryVisibility[category.Key] = true; // fallback default


                    bool newVisible = GUILayout.Toggle(currentVisible, "", GUILayout.Width(20));
                    if (newVisible != currentVisible)
                    {
                        _categoryVisibility[category.Key] = newVisible;
                        SetRenderersActive(category.Key, newVisible);
                    }

                    // Button
                    if (GUILayout.Button(category.Value.displayName, highQualityButtonStyle))
                    {
                        _selectedCategory = category.Key;
                        _selectedFolder = Path.Combine(BikeRootPath, category.Value.displayName);
                    }

                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label("Materials:", Menu.labelStyle);

                if (!string.IsNullOrEmpty(_selectedFolder) && Directory.Exists(_selectedFolder))
                {
                    foreach (var materialFile in Directory.GetFiles(_selectedFolder, "*.material"))
                    {
                        string materialName = Path.GetFileNameWithoutExtension(materialFile);

                        GUILayout.BeginHorizontal();
                        Rect buttonRect = GUILayoutUtility.GetRect(new GUIContent(materialName), Menu.highQualityButtonStyle);

                        if (GUI.Button(buttonRect, materialName, Menu.highQualityButtonStyle))
                        {
                            Material loadedMaterial = LoadMaterialFromFile(materialFile);

                            // 🔧 Disable decals if applicable
                            if (loadedMaterial != null && loadedMaterial.HasProperty("_SupportDecals"))
                            {
                                loadedMaterial.SetFloat("_SupportDecals", 0f);
                                Log.Msg("Decals disabled on material " + loadedMaterial.name);
                            }

                            if (loadedMaterial != null && _selectedCategory != null && categories.ContainsKey(_selectedCategory))
                            {
                                ApplyMaterialToCategory(_selectedCategory, loadedMaterial, materialFile);
                            }
                            else
                            {
                                Log.Error($"[rowemod] Failed to apply material. Invalid category: {_selectedCategory}");
                            }

                        }

                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label("No materials found or folder does not exist.", Menu.labelStyle);
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.Label("Bike Material Presets", Menu.labelStyle);

                // Text field to enter new preset name
                _newPresetName = GUILayout.TextField(_newPresetName, 25);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Preset", Menu.highQualityButtonStyle))
                {
                    if (!string.IsNullOrWhiteSpace(_newPresetName))
                    {
                        if (Config.bike.bikeMaterials == null)
                            Config.bike.bikeMaterials = new Dictionary<string, string>();

                        SaveBikeMaterialPreset(_newPresetName);
                        _newPresetName = ""; // Clear input field after saving
                        GUI.FocusControl(null); // Reset focus to the GUI window
                    }
                }

                List<string> availableBikePresets = BikeMaterialPreset.GetAvailablePresets();

                if (availableBikePresets.Count > 0)
                {
                    GUILayout.BeginVertical();
                    for (int i = 0; i < availableBikePresets.Count; i++)
                    {
                        if (GUILayout.Button(availableBikePresets[i], Menu.highQualityButtonStyle))
                        {
                            _selectedPresetIndex = i;
                            LoadBikeMaterialPreset(availableBikePresets[_selectedPresetIndex]);
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred in DrawBikeMaterialsTabUI: {ex.Message}");
            }

        }



        public static void Initialize()
        {
            Log.Msg("Initializing BikeMaterialsLoader...");

            categories.Values.ToList().ForEach(c => c.slots.Clear());
            _selectedFolder = string.Empty;
            _selectedCategory = null;

            LoadBikeFolders();
            
            // ✅ Always initialize visibility map — not just if character exists
            foreach (var category in categories.Keys)
            {
                if (!_categoryVisibility.ContainsKey(category))
                    _categoryVisibility[category] = true; // default to visible
            }
            
            Log.Msg($"rMBCharacter value: {(rMbCharacter != null ? rMbCharacter.name : "NULL")}");

            if (rMbCharacter != null)
            {
                equipSlotVehicles = rMbCharacter.GetComponentsInChildren<EquipSlotVehicle>(true);
                //Log.Msg($"Found {equipSlotVehicles.Length} EquipSlotVehicle components.");
                CategorizeEquipSlots(equipSlotVehicles);
            }
            else
            {
                Log.Warning("rMBCharacter GameObject not found!");
            }

            Log.Msg("BikeMaterialsLoader initialization complete.");
        }

        public static void CategorizeEquipSlots(EquipSlotVehicle[] equipSlotVehicles)
        {
            //Log.Msg("Listing all EquipSlotVehicle names:");
            foreach (var slot in equipSlotVehicles)
            {
                //Log.Msg($" - {slot.gameObject.name}");
            }

            if (equipSlotVehicles.Length == 0)
            {
                //Log.Warning("No EquipSlotVehicle components found in rMBCharacter.");
                return;
            }

            int slotsCategorized = 0;

            foreach (var slot in equipSlotVehicles)
            {
                string slotName = slot.gameObject.name;
                //Log.Msg($"Checking slot: {slotName}");

                bool isPeg = slotName.Contains("Peg", StringComparison.OrdinalIgnoreCase);
                string wheelType = GetWheelType(slot.gameObject);
                bool isFront = wheelType == "Front Wheel";
                bool isBack = wheelType == "Back Wheel";

                // Handle BMX_ parts with Front/Rear position using hierarchy, not name
                if (slotName.StartsWith("BMX_", StringComparison.OrdinalIgnoreCase))
                {
                    string baseName = slotName
                        .Replace("_Front_EquipSlot", "")
                        .Replace("_Rear_EquipSlot", "")
                        .Replace("_Back_EquipSlot", "")
                        .Replace("_EquipSlot", ""); // fallback cleanup

                    if (isFront)
                    {
                        string frontKey = $"{baseName}_Front";
                        if (categories.ContainsKey(frontKey))
                        {
                            categories[frontKey].slots.Add(slot);
                            //Log.Msg($"Assigned '{slotName}' to '{frontKey}'.");
                        }

                        if (categories.ContainsKey(baseName))
                        {
                            categories[baseName].slots.Add(slot);
                            //Log.Msg($"Also assigned '{slotName}' to general '{baseName}' category.");
                        }
                    }
                    else if (isBack)
                    {
                        string rearKey = $"{baseName}_Rear";
                        if (categories.ContainsKey(rearKey))
                        {
                            categories[rearKey].slots.Add(slot);
                            //Log.Msg($"Assigned '{slotName}' to '{rearKey}'.");
                        }

                        if (categories.ContainsKey(baseName))
                        {
                            categories[baseName].slots.Add(slot);
                            //Log.Msg($"Also assigned '{slotName}' to general '{baseName}' category.");
                        }
                    }

                    else if (categories.ContainsKey(baseName))
                    {
                        categories[baseName].slots.Add(slot);
                        //Log.Msg($"Assigned '{slotName}' to generic '{baseName}' category.");
                    }

                    // Add to 'Pegs' if applicable
                    if (isPeg && categories.ContainsKey("BMX_Peg"))
                    {
                        categories["BMX_Peg"].slots.Add(slot);
                        //Log.Msg($"Assigned '{slotName}' to 'Pegs'.");
                    }

                    slotsCategorized++;
                    continue;
                }

                // Pegs with specific naming
                if (isPeg)
                {
                    var specificPegCategory = categories.Keys
                        .Where(c => c.StartsWith("BMX_Peg_", StringComparison.OrdinalIgnoreCase)
                                    && slotName.Contains(c, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(c => c.Length)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(specificPegCategory))
                    {
                        categories[specificPegCategory].slots.Add(slot);
                        //Log.Msg($"Assigned '{slotName}' to '{categories[specificPegCategory].displayName}'.");
                    }

                    if (categories.ContainsKey("BMX_Peg"))
                    {
                        categories["BMX_Peg"].slots.Add(slot);
                        //Log.Msg($"Assigned '{slotName}' to 'Pegs'.");
                    }

                    slotsCategorized++;
                    continue;
                }

                // General fallback: find a matching category by containment
                foreach (var category in categories.Keys)
                {
                    if (slotName.Contains(category, StringComparison.OrdinalIgnoreCase))
                    {
                        categories[category].slots.Add(slot);
                        //Log.Msg($"Categorized slot '{slotName}' under '{categories[category].displayName}'.");
                        slotsCategorized++;
                        break;
                    }
                }
            }

            //Log.Msg($"Finished categorizing. {slotsCategorized} slots assigned.");
        }





        // Helper function to check the parent hierarchy for wheel type
        private static string GetWheelType(GameObject obj)
        {
            Transform current = obj.transform;
            //Log.Msg($"🔍 Checking wheel type for: {obj.name}");

            while (current != null)
            {
                //Log.Msg($"   ↳ Checking parent: {current.name}");

                string parentName = current.name.ToLowerInvariant();
                if (parentName.Contains("front"))
                {
                    //Log.Msg($"   ✅ '{obj.name}' belongs to the FRONT wheel (found '{current.name}').");
                    return "Front Wheel";
                }
                if (parentName.Contains("back"))
                {
                    //Log.Msg($"   ✅ '{obj.name}' belongs to the REAR wheel (found '{current.name}').");
                    return "Back Wheel";
                }
                if (parentName.Contains("rear"))
                {
                    //Log.Msg($"   ✅ '{obj.name}' belongs to the REAR wheel (found '{current.name}').");
                    return "Back Wheel";
                }

                current = current.parent;
            }

            //Log.Warning($"⚠ '{obj.name}' does NOT belong to a known wheel type! No valid parent found.");
            return "Unknown";
        }

        private static void SetRenderersActive(string categoryKey, bool isVisible)
        {
            if (!categories.ContainsKey(categoryKey)) return;

            foreach (var slot in categories[categoryKey].slots)
            {
                if (slot?._itemSlot == null || !slot._itemSlot.HasItemEquip) continue;

                GameObject equippedObject = slot._itemSlot.EquippedItem;
                if (equippedObject == null) continue;

                Renderer[] renderers = equippedObject.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    r.enabled = isVisible;
                }
            }
        }



        public static System.Collections.IEnumerator DelayedApplySavedMaterials()
        {
            Initialize();
            yield return new WaitForSeconds(5f);

            Log.Msg("Applying saved bike materials after delay...");
            
            ApplySavedMaterialsOnSceneLoad();
        }
        public static void ApplySavedMaterialsOnSceneLoad()
        {
            if (Config.bike.bikeMaterials == null)
            {
                Config.bike.bikeMaterials = new Dictionary<string, string>();
            }
            foreach (var category in Config.bike.bikeMaterials)
            {
                if (!categories.ContainsKey(category.Key))
                {
                    Log.Warning($"Saved category '{category.Key}' does not exist in the current configuration.");
                    continue;
                }

                var materialName = category.Value;
                var materialPath = Path.Combine(BikeRootPath, categories[category.Key].displayName, $"{materialName}.material");

                if (!File.Exists(materialPath))
                {
                    Log.Warning($"Material file '{materialPath}' does not exist for category '{category.Key}'.");
                    continue;
                }

                Material loadedMaterial = LoadMaterialFromFile(materialPath);
                if (loadedMaterial != null)
                {
                    //Log.Msg($"Applying saved material '{materialName}' to category '{category.Key}'.");
                    ApplyMaterialToCategory(category.Key, loadedMaterial, materialPath);
                }
            }
        }


        private static Material LoadMaterialFromFile(string materialPath)
        {
            try
            {
                var assetBundle = AssetBundle.LoadFromFile(materialPath);
                if (assetBundle == null)
                {
                    return null;
                }

                string[] assetNames = assetBundle.GetAllAssetNames();
                Material material = assetNames.Length > 0 ? assetBundle.LoadAsset<Material>(assetNames[0]) : null;
                assetBundle.Unload(false);
                return material;
            }
            catch
            {
                return null;
            }
        }


        private static void LoadBikeFolders()
        {
            foreach (var category in categories.Values)
            {
                string categoryFolder = Path.Combine(BikeRootPath, category.displayName);
            }
        }


        private static void ApplyMaterialToCategory(string category, Material material, string materialPath)
        {
            if (!categories.ContainsKey(category))
            {
                Log.Warning($"Category '{category}' does not exist.");
                return;
            }

            Log.Msg($"Applying material '{material.name}' to category '{category}'");
            
            foreach (var slot in categories[category].slots)
            {
                if (slot == null)
                {
                    Log.Warning($"Null slot found in category '{category}'. Skipping.");
                    continue;
                }
                if (slot?._itemSlot == null || !slot._itemSlot.HasItemEquip)
                {
                    Log.Warning($"Slot '{slot?.name}' has no equipped item yet.");
                    continue;
                }

                GameObject equippedObject = slot._itemSlot.EquippedItem;
                if (equippedObject == null)
                {
                    Log.Warning($"Equipped object is null for slot '{slot.name}'.");
                    continue;
                }
                
                Renderer[] renderers = equippedObject.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0)
                {
                    renderers = equippedObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (renderers.Length == 0)
                    {
                        Log.Warning($"No renderers (Mesh or Skinned) found under equipped object for slot '{slot.name}'.");
                        continue;
                    }
                    else
                    {
                        Log.Msg($"Fallback to SkinnedMeshRenderer successful for slot '{slot.name}'.");
                    }
                }

                foreach (var renderer in renderers)
                {
                    Log.Msg($"Applying material '{material.name}' to renderer '{renderer.name}'.");

                    Material[] mats = renderer.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = new Material(material);
                    }
                    renderer.materials = mats;
                    renderer.material = material;
                }
            }

            scrollOffset = 0;
            if (Config.bike.bikeMaterials == null)
            {
                Config.bike.bikeMaterials = new Dictionary<string, string>();
            }
            Config.bike.bikeMaterials[category] = Path.GetFileNameWithoutExtension(materialPath);
            Config.Save();
        }
        /*public static void SaveBikeMaterialPreset(string presetName)
        {
            BikeMaterialPreset preset = new BikeMaterialPreset { name = presetName };

            foreach (var material in Config.bike.bikeMaterials)
            {
                preset.Materials[material.Key] = material.Value;
            }

            BikeMaterialPreset.SavePreset(preset);
        }*/
        public static void SaveBikeMaterialPreset(string presetName)
        {
            if (Config.bike.bikeMaterials == null)
            {
                Log.Warning("bikeMaterials was null. Initializing an empty dictionary before saving preset.");
                Config.bike.bikeMaterials = new Dictionary<string, string>();
            }

            BikeMaterialPreset preset = new BikeMaterialPreset { name = presetName };

            foreach (var material in Config.bike.bikeMaterials)
            {
                preset.Materials[material.Key] = material.Value;
            }

            BikeMaterialPreset.SavePreset(preset);
        }

        public static void LoadBikeMaterialPreset(string presetName)
        {
            BikeMaterialPreset? preset = BikeMaterialPreset.LoadPreset(presetName);
            if (preset == null) return;

            foreach (var kvp in preset.Materials)
            {
                string categoryKey = kvp.Key;

                // 🔄 Convert old "Back" keys to "Rear" keys for compatibility
                if (categoryKey.Contains("Back") && !BikeMaterialsLoader.categories.ContainsKey(categoryKey))
                {
                    string convertedKey = categoryKey.Replace("Back", "Rear");
                    if (BikeMaterialsLoader.categories.ContainsKey(convertedKey))
                    {
                        Log.Warning($"[rowemod] Converting old category '{categoryKey}' to '{convertedKey}' for compatibility.");
                        categoryKey = convertedKey;
                    }
                }

                if (!BikeMaterialsLoader.categories.ContainsKey(categoryKey))
                {
                    Log.Error($"[rowemod] Skipping unknown category '{categoryKey}' in preset.");
                    continue;
                }

                string materialPath = Path.Combine(BikeRootPath, BikeMaterialsLoader.categories[categoryKey].displayName, $"{kvp.Value}.material");
                if (!File.Exists(materialPath))
                {
                    Log.Error($"[rowemod] Material file '{materialPath}' not found for category '{categoryKey}'.");
                    continue;
                }

                Material loadedMaterial = LoadMaterialFromFile(materialPath);
                if (loadedMaterial != null)
                {
                    ApplyMaterialToCategory(categoryKey, loadedMaterial, materialPath);
                }
            }
        }




    }
}
