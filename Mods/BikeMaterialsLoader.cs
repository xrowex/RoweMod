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
            Path.Combine(MelonEnvironment.ModsDirectory, @"rowemod\Bike");

        private static List<string> _bikeFolders = new List<string>();
        private static string _selectedFolder = string.Empty;

        private static string _selectedCategory = null;

        private static Dictionary<string, Dictionary<string, Texture2D>> _categoryPreviews = new Dictionary<string, Dictionary<string, Texture2D>>();
        private static Dictionary<string, Texture2D> _materialPreviews = new Dictionary<string, Texture2D>();
    
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
                    if (GUILayout.Button(category.Value.displayName, Menu.highQualityButtonStyle))
                    {
                        _selectedCategory = category.Key;
                        _selectedFolder = Path.Combine(BikeRootPath, category.Value.displayName);
                    }
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
                            if (loadedMaterial != null && _selectedCategory != null)
                            {
                                ApplyMaterialToCategory(_selectedCategory, loadedMaterial, materialFile);
                            }
                            else
                            {
                                Log.Error($"Failed to load material: {materialName}");
                            }
                        }

                        /*if (buttonRect.Contains(Event.current.mousePosition))
                        {
                            hoveredMaterial = materialName;
                            hoveredPreview = GetMaterialPreview(materialFile);
                        }
                        else if (hoveredMaterial == materialName)
                        {
                            hoveredMaterial = null;
                            hoveredPreview = null;
                        }*/
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

            Log.Msg($"rMBCharacter value: {(rMbCharacter != null ? rMbCharacter.name : "NULL")}");

            if (rMbCharacter != null)
            {
                EquipSlotVehicle[] equipSlotVehicles = rMbCharacter.GetComponentsInChildren<EquipSlotVehicle>(true);
                //Log.Msg($"Found {equipSlotVehicles.Length} EquipSlotVehicle components.");
                CategorizeEquipSlots(equipSlotVehicles);
            }
            else
            {
                Log.Warning("rMBCharacter GameObject not found!");
            }

            Log.Msg("BikeMaterialsLoader initialization complete.");
        }

        /*private static void CategorizeEquipSlots(EquipSlotVehicle[] equipSlotVehicles)
        {
            Log.Msg("Listing all EquipSlotVehicle names:");
            foreach (var slot in equipSlotVehicles)
            {
                Log.Msg($" - {slot.gameObject.name}");
            }

            if (equipSlotVehicles.Length == 0)
            {
                Log.Warning("No EquipSlotVehicle components found in rMBCharacter.");
                return;
            }

            int slotsCategorized = 0;

            foreach (var slot in equipSlotVehicles)
            {
                Log.Msg($"Checking slot: {slot.gameObject.name}");

                bool isFront = slot.gameObject.name.Contains("Front", StringComparison.OrdinalIgnoreCase);
                bool isPeg = slot.gameObject.name.Contains("Peg", StringComparison.OrdinalIgnoreCase);

                foreach (var category in categories.Keys)
                {
                    if (slot.gameObject.name.Contains(category, StringComparison.OrdinalIgnoreCase))
                    {
                        // Specific Peg Handling
                        if (isPeg)
                        {
                            // Find the most specific peg category first
                            var specificPegCategory = categories.Keys
                                .Where(c => c.StartsWith("BMX_Peg_", StringComparison.OrdinalIgnoreCase) 
                                         && slot.gameObject.name.Contains(c, StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(c => c.Length) // Prioritize longer (more specific) matches
                                .FirstOrDefault();

                            if (!string.IsNullOrEmpty(specificPegCategory))
                            {
                                categories[specificPegCategory].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to '{categories[specificPegCategory].displayName}'.");
                                categories["BMX_Peg"].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to 'Pegs'.");
                                
                            }
                            else
                            {
                                // If no specific peg category is found, default to general Pegs category
                                categories["BMX_Peg"].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to 'Pegs'.");
                            }
                        }
                        else if (isFront && category.StartsWith("BMX_", StringComparison.OrdinalIgnoreCase))
                        {
                            string wheelType = GetWheelType(slot.gameObject);

                            if (wheelType == "Back Wheel")
                            {
                                string rearCategory = category.Replace("Front", "Rear");
                                if (categories.ContainsKey(rearCategory))
                                {
                                    categories[rearCategory].slots.Add(slot);
                                    Log.Msg($"Assigned '{slot.gameObject.name}' to '{rearCategory}'.");
                                }

                                categories[category.Replace("_Front", "")].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to general '{category.Replace("_Front", "")}' category.");
                            }
                            else
                            {
                                categories[category].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to '{categories[category].displayName}'.");
                            }
                        }
                        else
                        {
                            categories[category].slots.Add(slot);
                            Log.Msg($"Categorized slot '{slot.gameObject.name}' under '{categories[category].displayName}'.");
                        }

                        slotsCategorized++;
                        break;
                    }
                }
            }
            Log.Msg($"Finished categorizing. {slotsCategorized} slots assigned.");
        }*/
        private static void CategorizeEquipSlots(EquipSlotVehicle[] equipSlotVehicles)
        {
            Log.Msg("Listing all EquipSlotVehicle names:");
            foreach (var slot in equipSlotVehicles)
            {
                Log.Msg($" - {slot.gameObject.name}");
            }

            if (equipSlotVehicles.Length == 0)
            {
                Log.Warning("No EquipSlotVehicle components found in rMBCharacter.");
                return;
            }

            int slotsCategorized = 0;

            foreach (var slot in equipSlotVehicles)
            {
                Log.Msg($"Checking slot: {slot.gameObject.name}");

                bool isPeg = slot.gameObject.name.Contains("Peg", StringComparison.OrdinalIgnoreCase);
                string wheelType = GetWheelType(slot.gameObject);
                bool isFront = wheelType == "Front Wheel";
                bool isBack = wheelType == "Back Wheel";

                foreach (var category in categories.Keys)
                {
                    if (slot.gameObject.name.Contains(category, StringComparison.OrdinalIgnoreCase))
                    {
                        // 🛞 Specific Peg Handling
                        if (isPeg)
                        {
                            var specificPegCategory = categories.Keys
                                .Where(c => c.StartsWith("BMX_Peg_", StringComparison.OrdinalIgnoreCase)
                                         && slot.gameObject.name.Contains(c, StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(c => c.Length)
                                .FirstOrDefault();

                            if (!string.IsNullOrEmpty(specificPegCategory))
                            {
                                categories[specificPegCategory].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to '{categories[specificPegCategory].displayName}'.");
                                categories["BMX_Peg"].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to 'Pegs'.");
                            }
                            else
                            {
                                categories["BMX_Peg"].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to 'Pegs'.");
                            }
                        }

                        // 🛞 Front/Back differentiation for BMX_ parts
                        else if ((isFront || isBack) && category.StartsWith("BMX_", StringComparison.OrdinalIgnoreCase))
                        {
                            if (isBack)
                            {
                                string rearCategory = category.Replace("Front", "Rear");
                                if (categories.ContainsKey(rearCategory))
                                {
                                    categories[rearCategory].slots.Add(slot);
                                    Log.Msg($"Assigned '{slot.gameObject.name}' to '{rearCategory}'.");
                                }

                                string generalCategory = category.Replace("_Front", "");
                                if (categories.ContainsKey(generalCategory))
                                {
                                    categories[generalCategory].slots.Add(slot);
                                    Log.Msg($"Assigned '{slot.gameObject.name}' to general '{generalCategory}' category.");
                                }
                            }
                            else // Front Wheel
                            {
                                categories[category].slots.Add(slot);
                                Log.Msg($"Assigned '{slot.gameObject.name}' to '{categories[category].displayName}'.");
                            }
                        }

                        // ✅ General fallback
                        else
                        {
                            categories[category].slots.Add(slot);
                            Log.Msg($"Categorized slot '{slot.gameObject.name}' under '{categories[category].displayName}'.");
                        }

                        slotsCategorized++;
                        break;
                    }
                }
            }

            Log.Msg($"Finished categorizing. {slotsCategorized} slots assigned.");
        }



        // Helper function to check the parent hierarchy for wheel type
        private static string GetWheelType(GameObject obj)
        {
            Transform current = obj.transform;
            Log.Msg($"🔍 Checking wheel type for: {obj.name}");

            while (current != null)
            {
                Log.Msg($"   ↳ Checking parent: {current.name}");

                string parentName = current.name.ToLowerInvariant();
                if (parentName.Contains("front"))
                {
                    Log.Msg($"   ✅ '{obj.name}' belongs to the FRONT wheel (found '{current.name}').");
                    return "Front Wheel";
                }
                if (parentName.Contains("back"))
                {
                    Log.Msg($"   ✅ '{obj.name}' belongs to the REAR wheel (found '{current.name}').");
                    return "Back Wheel";
                }

                current = current.parent;
            }

            Log.Warning($"⚠ '{obj.name}' does NOT belong to a known wheel type! No valid parent found.");
            return "Unknown";
        }



        /*private static Texture2D GetMaterialPreview(string materialPath)
        {
            if (materialPreviews.TryGetValue(materialPath, out Texture2D cachedPreview))
            {
                //Log.Msg($"Using cached preview for {materialPath}");
                return cachedPreview;
            }

            try
            {
                //Log.Msg($"Attempting to load preview for {materialPath}");
                string folderPath = Path.GetDirectoryName(materialPath);
                string materialName = Path.GetFileNameWithoutExtension(materialPath);

                // Look for image files matching the material name
                string[] imageFiles = Directory.GetFiles(folderPath, $"{materialName}.*")
                    .Where(file => file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (imageFiles.Length > 0)
                {
                    //Log.Msg($"Found image file for preview: {imageFiles[0]}");
                    byte[] imageData = File.ReadAllBytes(imageFiles[0]);
                    Texture2D imageTexture = new Texture2D(2, 2);
                    if (imageTexture.LoadImage(imageData))
                    {
                        //Log.Msg($"Loaded image preview for {materialName}");
                        materialPreviews[materialPath] = imageTexture;
                        return imageTexture;
                    }
                    else
                    {
                        Log.Warning($"Failed to load image data for {imageFiles[0]}");
                    }
                }

                // Attempt to generate a texture from the material
                Material material = LoadMaterialFromFile(materialPath);
                if (material == null)
                {
                    Log.Warning($"Material could not be loaded for {materialPath}");
                    return null;
                }

                Texture2D baseMapTexture = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") is Texture2D baseMap
                    ? ResizeTexture(baseMap, 250, 250)
                    : CreateColorTexture(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.gray, 250, 250);

                //Log.Msg($"Generated preview texture for {materialName}");
                materialPreviews[materialPath] = baseMapTexture;
                return baseMapTexture;
            }
            catch (Exception ex)
            {
                Log.Error($"Error generating material preview for '{materialPath}': {ex.Message}");
                return null;
            }
        }*/




        public static System.Collections.IEnumerator DelayedApplySavedMaterials()
        {
            Initialize();
            yield return new WaitForSeconds(5f);

            Log.Msg("Applying saved bike materials after delay...");
            
            ApplySavedMaterialsOnSceneLoad();
        }
        public static void ApplySavedMaterialsOnSceneLoad()
        {

            foreach (var category in Config.bikeMaterials)
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
        private static Texture2D ResizeTexture(Texture2D original, int width, int height)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height);
            RenderTexture.active = renderTexture;

            Graphics.Blit(original, renderTexture);

            Texture2D resizedTexture = new Texture2D(width, height);
            resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resizedTexture.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);

            return resizedTexture;
        }

        private static Texture2D CreateColorTexture(Color color, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            Color[] pixels = Enumerable.Repeat(color, width * height).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
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


        /*private static void ApplyMaterialToCategory(string category, Material material, string materialPath)
        {
            if (!categories.ContainsKey(category))
            {
                Log.Warning($"Category '{category}' does not exist.");
                return;
            }

            Log.Msg($"Applying material '{material.name}' to category '{category}'");

            foreach (var slot in categories[category].slots)
            {
                if (slot._equipItem == null)
                {
                    Log.Warning($"EquipItem in category '{category}' is null.");
                    continue;
                }

                Renderer[] renderers = slot._equipItem.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0)
                {
                    Log.Warning($"No renderers found in category '{category}'.");
                    continue;
                }

                foreach (var renderer in renderers)
                {
                    Log.Msg($"Applying material '{material.name}' to renderer '{renderer.name}'.");

                    Material[] mats = renderer.materials; // get a copy
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = new Material(material); // Assign new material instance
                    }
                    renderer.materials = mats; // Apply the modified array
                    renderer.material = material; // Assign the new material to the renderer
                }
            }
            scrollOffset = 0;
            // Update the saved config
            Config.bikeMaterials[category] = Path.GetFileNameWithoutExtension(materialPath);
            Config.Save();
        }*/
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
            Config.bikeMaterials[category] = Path.GetFileNameWithoutExtension(materialPath);
            Config.Save();
        }
        public static void SaveBikeMaterialPreset(string presetName)
        {
            BikeMaterialPreset preset = new BikeMaterialPreset { name = presetName };

            foreach (var material in Config.bikeMaterials)
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
                if (!categories.ContainsKey(kvp.Key)) continue;

                string materialPath = Path.Combine(BikeRootPath, categories[kvp.Key].displayName, $"{kvp.Value}.material");
                if (!File.Exists(materialPath))
                {
                    Log.Error($"Material file '{materialPath}' not found for category '{kvp.Key}'.");
                    continue;
                }

                Material loadedMaterial = LoadMaterialFromFile(materialPath);
                if (loadedMaterial != null)
                {
                    ApplyMaterialToCategory(kvp.Key, loadedMaterial, materialPath);
                }
            }

            //Log.Msg($"Bike material preset '{presetName}' loaded.");
        }




    }
}
