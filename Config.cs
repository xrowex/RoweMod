using Newtonsoft.Json;
using rowemod.Mods;
using rowemod.Utils;

namespace rowemod
{
    public struct Physics
    {
        public bool disableLevelInAir;
        public bool manualMovement;
        public bool spinAssist;
        public bool spinFlipFix;
        public bool driftAbility;
        public int lastVehicle;
        public float gravity;
        public float smallHopForce;
        public float pumpForce;
        public float steerDamp;
        public float manualAngle;
        public float noseManualAngle;
        public float spinMultiplier;
        public float sideHopPower;
        public float vertAssistCorrectionStrength;
        public float tireFrictionThreshold;
        public float bmxForceFactor;
        public float bmxMaxSpeed;
    }

    // Struct for character model and material paths
    public struct CustomCharacter
    {
        public string lastLoadedPresetCharacter;
        public string bodyModelPath;
        public string bodyMaterialPath;
        public string topModelPath;
        public string topMaterialPath;
        public string glovesModelPath;
        public string glovesMaterialPath;
        public string bottomsModelPath;
        public string bottomsMaterialPath;
        public string socksModelPath;
        public string socksMaterialPath;
        public string shoesModelPath;
        public string shoesMaterialPath;
        public string bustModelPath;
        public string bustMaterialPath;
        public string hatModelPath;
        public string hatMaterialPath;
        public string hairModelPath;
        public string hairMaterialPath;
        public string eyesModelPath;
        public string eyesMaterialPath;
    }

    // Struct for bike customization variables
    public struct CustomBike
    {
        public float barPitch;
        public float barScale;
        public float seatHeight;
        public float seatPitch;
        public float frontWheelRadius;
        public float frontWheelWidth;
        public float rearWheelRadius;
        public float rearWheelWidth;
        public float pegLength;
        public Dictionary<string, string> bikeMaterials;
        public string lastLoadedPresetBike;
        public string lastLoadedFrame;
        public string lastLoadedBars;
        public string lastLoadedWheels;
        public string lastLoadedStem;
        public string lastLoadedFork;
    }

    // Struct for miscellaneous variables
    public struct Misc
    {
        public bool neverBail;
        public float droneMass;
        public bool droneBodyToggle;
        public bool droneEmitterToggle;
        public float menuAccentR;
        public float menuAccentG;
        public float menuAccentB;
        public bool disableEmoteOnBike;
        public bool disableFreeCamCollider;
        public bool disableDroneCollider;
        public string customSessionMarker;
    }
    public class TrickEntry
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
    }
    public struct CustomTricks
    {
        public Dictionary<string, List<TrickEntry>> trickSets;
    }

    public static class Config
    {
        // Static instances of structs
        public static Physics physics = new Physics
        {
            disableLevelInAir = false,
            manualMovement = false,
            spinAssist = true,
            spinFlipFix = false,
            driftAbility = true,
            lastVehicle = 0,
            gravity = 12.5f,
            smallHopForce = 4.2f,
            pumpForce = 1.5f,
            steerDamp = 5.0f,
            manualAngle = 30f,
            noseManualAngle = 30f,
            spinMultiplier = 1f,
            sideHopPower = 1f,
            vertAssistCorrectionStrength = 30f,
            tireFrictionThreshold = 2f,
            bmxForceFactor = 0.07f,
            bmxMaxSpeed = 7.5f
        };

        public static CustomCharacter character = new CustomCharacter
        {
            lastLoadedPresetCharacter = "None",
            bodyModelPath = null,
            bodyMaterialPath = null,
            topModelPath = null,
            topMaterialPath = null,
            glovesModelPath = null,
            glovesMaterialPath = null,
            bottomsModelPath = null,
            bottomsMaterialPath = null,
            socksModelPath = null,
            socksMaterialPath = null,
            shoesModelPath = null,
            shoesMaterialPath = null,
            bustModelPath = null,
            bustMaterialPath = null,
            hatModelPath = null,
            hatMaterialPath = null,
            hairModelPath = null,
            hairMaterialPath = null,
            eyesModelPath = null,
            eyesMaterialPath = null
        };

        public static CustomBike bike = new CustomBike
        {
            barPitch = 0f,
            barScale = 1f,
            seatHeight = 0.05f,
            seatPitch = 330f,
            frontWheelRadius = 1f,
            frontWheelWidth = 1f,
            rearWheelRadius = 1f,
            rearWheelWidth = 1f,
            pegLength = 0.5f,
            bikeMaterials = new Dictionary<string, string>(),
            lastLoadedPresetBike = "None",
            lastLoadedBars = "None",
            lastLoadedFrame = "None",
            lastLoadedWheels = "None",
            lastLoadedStem = "None",
            lastLoadedFork = "None"
        };
        

        public static Misc misc = new Misc
        {
            neverBail = false,
            droneMass = 10f,
            droneBodyToggle = true,
            droneEmitterToggle = true,
            menuAccentR = 0.3f,
            menuAccentG = 0.3f,
            menuAccentB = 0.3f,
            disableEmoteOnBike = false,
            disableFreeCamCollider = false,
            disableDroneCollider = false,
            customSessionMarker = "None"
        };
        public static CustomTricks tricks = new CustomTricks
        {
            trickSets = new Dictionary<string, List<TrickEntry>>()
        };



        // Helper class for JSON deserialization
        private class ConfigData
        {
            public Physics physicsData { get; set; }
            public CustomCharacter customCharacterData { get; set; }
            public CustomBike customBikeData { get; set; }
            public Misc miscData { get; set; }
            public CustomTricks customTricksData { get; set; }
        }

        public static string modFolder = Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "RoweMod");
        public static string cfgFile { get; } = Path.Combine(modFolder, "cfg.json");

        // Save configuration to JSON file
        public static void Save()
        {
            try
            {
                Log.Msg($"Saving config to {cfgFile}");

                string contents = JsonConvert.SerializeObject(new
                {
                    physicsData = physics,
                    customCharacterData = new
                    {
                        character.lastLoadedPresetCharacter,
                        bodyModelPath = SafeMakeRelativePath(character.bodyModelPath),
                        bodyMaterialPath = SafeMakeRelativePath(character.bodyMaterialPath),
                        topModelPath = SafeMakeRelativePath(character.topModelPath),
                        topMaterialPath = SafeMakeRelativePath(character.topMaterialPath),
                        glovesModelPath = SafeMakeRelativePath(character.glovesModelPath),
                        glovesMaterialPath = SafeMakeRelativePath(character.glovesMaterialPath),
                        bottomsModelPath = SafeMakeRelativePath(character.bottomsModelPath),
                        bottomsMaterialPath = SafeMakeRelativePath(character.bottomsMaterialPath),
                        socksModelPath = SafeMakeRelativePath(character.socksModelPath),
                        socksMaterialPath = SafeMakeRelativePath(character.socksMaterialPath),
                        shoesModelPath = SafeMakeRelativePath(character.shoesModelPath),
                        shoesMaterialPath = SafeMakeRelativePath(character.shoesMaterialPath),
                        bustModelPath = SafeMakeRelativePath(character.bustModelPath),
                        bustMaterialPath = SafeMakeRelativePath(character.bustMaterialPath),
                        hatModelPath = SafeMakeRelativePath(character.hatModelPath),
                        hatMaterialPath = SafeMakeRelativePath(character.hatMaterialPath),
                        hairModelPath = SafeMakeRelativePath(character.hairModelPath),
                        hairMaterialPath = SafeMakeRelativePath(character.hairMaterialPath),
                        eyesModelPath = SafeMakeRelativePath(character.eyesModelPath),
                        eyesMaterialPath = SafeMakeRelativePath(character.eyesMaterialPath)
                    },
                    customBikeData = bike,
                    miscData = misc,
                    customTricksData = tricks
                }, Formatting.Indented);

                File.WriteAllText(cfgFile, contents);
                Log.Msg("Config saved successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to save config: {ex.Message}");
            }
        }

        // Load configuration from JSON file
        public static void Load()
        {
            if (!File.Exists(cfgFile))
            {
                Log.Msg($"Config file not found at {cfgFile}. Creating one with default values...");
                Save(); // Create new config with defaults
                return;
            }

            Log.Msg($"Loading config from {cfgFile}");
            string jsonContent = File.ReadAllText(cfgFile);
            ConfigData jsonData = JsonConvert.DeserializeObject<ConfigData>(jsonContent);

            // Assign values from JSON, preserving defaults if fields are missing
            physics = jsonData.physicsData;
            character = new CustomCharacter
            {
                lastLoadedPresetCharacter = jsonData.customCharacterData.lastLoadedPresetCharacter,
                bodyModelPath = MakeAbsolutePath(jsonData.customCharacterData.bodyModelPath),
                bodyMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.bodyMaterialPath),
                topModelPath = MakeAbsolutePath(jsonData.customCharacterData.topModelPath),
                topMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.topMaterialPath),
                glovesModelPath = MakeAbsolutePath(jsonData.customCharacterData.glovesModelPath),
                glovesMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.glovesMaterialPath),
                bottomsModelPath = MakeAbsolutePath(jsonData.customCharacterData.bottomsModelPath),
                bottomsMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.bottomsMaterialPath),
                socksModelPath = MakeAbsolutePath(jsonData.customCharacterData.socksModelPath),
                socksMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.socksMaterialPath),
                shoesModelPath = MakeAbsolutePath(jsonData.customCharacterData.shoesModelPath),
                shoesMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.shoesMaterialPath),
                bustModelPath = MakeAbsolutePath(jsonData.customCharacterData.bustModelPath),
                bustMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.bustMaterialPath),
                hatModelPath = MakeAbsolutePath(jsonData.customCharacterData.hatModelPath),
                hatMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.hatMaterialPath),
                hairModelPath = MakeAbsolutePath(jsonData.customCharacterData.hairModelPath),
                hairMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.hairMaterialPath),
                eyesModelPath = MakeAbsolutePath(jsonData.customCharacterData.eyesModelPath),
                eyesMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.eyesMaterialPath)
            };
            bike = jsonData.customBikeData;
            misc = jsonData.miscData;
            tricks = jsonData.customTricksData;

            //set new config variables to defaults if 0
            if (physics.bmxForceFactor <= 0f) physics.bmxForceFactor = 0.07f;
            if (physics.bmxMaxSpeed <= 0f) physics.bmxMaxSpeed = 7.5f;
            
            Log.Msg($"Config loaded successfully.");
        }
        
        public static void SetCharacterModelPath(Custom.Slot slot, string path)
        {
            switch (slot)
            {
                case Custom.Slot.Body: character.bodyModelPath = path; break;
                case Custom.Slot.Top: character.topModelPath = path; break;
                case Custom.Slot.Gloves: character.glovesModelPath = path; break;
                case Custom.Slot.Bottoms: character.bottomsModelPath = path; break;
                case Custom.Slot.Socks: character.socksModelPath = path; break;
                case Custom.Slot.Shoes: character.shoesModelPath = path; break;
                case Custom.Slot.Bust: character.bustModelPath = path; break;
                case Custom.Slot.Hat: character.hatModelPath = path; break;
                case Custom.Slot.Hair: character.hairModelPath = path; break;
                case Custom.Slot.Eyes: character.eyesModelPath = path; break;
            }
        }

        public static void SetCharacterMaterialPath(Custom.Slot slot, string path)
        {
            switch (slot)
            {
                case Custom.Slot.Body: character.bodyMaterialPath = path; break;
                case Custom.Slot.Top: character.topMaterialPath = path; break;
                case Custom.Slot.Gloves: character.glovesMaterialPath = path; break;
                case Custom.Slot.Bottoms: character.bottomsMaterialPath = path; break;
                case Custom.Slot.Socks: character.socksMaterialPath = path; break;
                case Custom.Slot.Shoes: character.shoesMaterialPath = path; break;
                case Custom.Slot.Bust: character.bustMaterialPath = path; break;
                case Custom.Slot.Hat: character.hatMaterialPath = path; break;
                case Custom.Slot.Hair: character.hairMaterialPath = path; break;
                case Custom.Slot.Eyes: character.eyesMaterialPath = path; break;
            }
        }
// Reset physics tab settings to defaults
        public static void ResetPhysicsTab()
        {
            physics = new Physics
            {
                disableLevelInAir = false,
                manualMovement = false,
                spinAssist = true,
                spinFlipFix = false,
                driftAbility = true,
                lastVehicle = 0,
                gravity = 12.5f,
                smallHopForce = 4.2f,
                pumpForce = 1.5f,
                steerDamp = 5.0f,
                manualAngle = 30f,
                noseManualAngle = 30f,
                spinMultiplier = 1f,
                sideHopPower = 1f,
                vertAssistCorrectionStrength = 30f,
                tireFrictionThreshold = 2f,
                bmxForceFactor = 0.07f,
                bmxMaxSpeed = 7.5f
            };
        }

        // Reset character tab settings to defaults
        // Reset character tab settings to defaults
        public static void ResetCharacterTab()
        {
            // Load the preset by name
            var preset = ClothingPreset.Load(Config.character.lastLoadedPresetCharacter);
            if (preset != null && preset.SlotVisibility != null)
            {
                foreach (var slot in preset.SlotVisibility.Keys.ToList())
                {
                    Custom.ToggleSlotVisibility(slot, true);
                }

                Log.Msg($"All SlotVisibility set to true for preset '{preset.Name}'.");
            }
            else
            {
                Log.Warning("Could not load preset or SlotVisibility dictionary was null.");
            }

            
            character = new CustomCharacter
            {
                lastLoadedPresetCharacter = "None",
                bodyModelPath = null,
                bodyMaterialPath = null,
                topModelPath = null,
                topMaterialPath = null,
                glovesModelPath = null,
                glovesMaterialPath = null,
                bottomsModelPath = null,
                bottomsMaterialPath = null,
                socksModelPath = null,
                socksMaterialPath = null,
                shoesModelPath = null,
                shoesMaterialPath = null,
                bustModelPath = null,
                bustMaterialPath = null,
                hatModelPath = null,
                hatMaterialPath = null,
                hairModelPath = null,
                hairMaterialPath = null,
                eyesModelPath = null,
                eyesMaterialPath = null
            };
            
            Memory.roweCharacterManager.InitCharacterData();
        }

        // Reset bike tab settings to defaults
        public static void ResetBikeTab()
        {
            bike.barPitch = 0f;
            bike.barScale = 1f;
            bike.seatHeight = 0.05f;
            bike.seatPitch = 330f;
            bike.frontWheelRadius = 1f;
            bike.frontWheelWidth = 1f;
            bike.rearWheelRadius = 1f;
            bike.rearWheelWidth = 1f;
            bike.pegLength = 0.5f;
            bike.lastLoadedPresetBike = "None";
            bike.lastLoadedFrame = "None";
            bike.lastLoadedBars = "None";
            bike.lastLoadedWheels = "None";
            bike.lastLoadedStem = "None";
            bike.lastLoadedFork = "None";
            bike.bikeMaterials = new Dictionary<string, string>();
            Memory.lastEquippedBars = null;
            Memory.lastEquippedFrame = null;
            Memory.customizableEntity.EquipItems();
            Memory.customizableEntity.EquipItems();
        }

        // Reset bike materials tab settings to defaults
        public static void ResetBikeMaterialsTab()
        {
            bike.bikeMaterials.Clear();
        }

        // Reset misc tab settings to defaults
        public static void ResetMiscTab()
        {
            misc = new Misc
            {
                neverBail = false,
                droneMass = 10f,
                droneBodyToggle = true,
                droneEmitterToggle = true,
                menuAccentR = 0.3f,
                menuAccentG = 0.3f,
                menuAccentB = 0.3f,
                disableEmoteOnBike = false,
                disableFreeCamCollider = false,
                disableDroneCollider = false,
                customSessionMarker = "None"
            };
        }

        // Convert absolute path to relative path for storage
        public static string MakeRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return fullPath;

            if (!Path.IsPathRooted(fullPath))
            {
                return fullPath;
            }

            try
            {
                Uri fullPathUri = new Uri(fullPath, UriKind.Absolute);
                Uri baseUri = new Uri(modFolder + Path.DirectorySeparatorChar, UriKind.Absolute);
                return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullPathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to convert to relative path: {fullPath} - Error: {ex.Message}");
                return fullPath; // Return original path if conversion fails
            }
        }

        // Convert relative path to absolute path
        public static string MakeAbsolutePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return relativePath;
            return Path.GetFullPath(Path.Combine(modFolder, relativePath));
        }

        // Safely convert to relative path
        private static string SafeMakeRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (!Path.IsPathRooted(path))
            {
                return path;
            }
            return MakeRelativePath(path);
        }
    }
}