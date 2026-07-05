using System;
using System.Collections.Generic;
using System.Linq;
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
        public bool grindAlignAssist;
        public float grindAssistStrength;
        public float airAngularDrag;
        public float grindPoseLerpSpeed;
    }

    public class MotorTuningConfigEntry
    {
        public float forceFactor { get; set; }
        public float maxForce { get; set; }
        public float maxSpeed { get; set; }
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
        public string eyewearModelPath;
        public string eyewearMaterialPath;
    }

    // Struct for bike customization variables
    public struct CustomBike
    {
        public float barPitch;
        public float barScale;
        public float seatHeight;
        public bool seatHeightUsesDefaultOffset;
        public float seatPitch;
        public float forkScale;
        public float frontWheelRadius;
        public float frontWheelWidth;
        public float rearWheelRadius;
        public float rearWheelWidth;
        public bool frontLeftPegsEnabled;
        public bool frontRightPegsEnabled;
        public bool rearLeftPegsEnabled;
        public bool rearRightPegsEnabled;
        public Dictionary<string, string> bikeMaterials;
        public string lastLoadedPresetBike;
        public string lastLoadedFrame;
        public string lastLoadedBars;
        public string lastLoadedWheels;
        public string lastLoadedStem;
        public string lastLoadedFork;
    }

    public struct ChallengeSettings
    {
        public bool challengeVisible;
        public float challengeSizeX;
        public float challengeSizeY;
        public float challengeSizeZ;
    }

    // Struct for miscellaneous variables
    public struct Misc
    {
        public bool neverBail;
        public float droneMass;
        public bool droneBodyToggle;
        public bool droneEmitterToggle;
        public bool showPlayerUserNameTargets;
        public float menuAccentR;
        public float menuAccentG;
        public float menuAccentB;
        public bool disableEmoteOnBike;
        public bool disableFreeCamCollider;
        public bool disableDroneCollider;
        public string customSessionMarker;
    }

    public class UpdaterSettings
    {
        public bool enabled { get; set; } = true;
        public string skippedVersion { get; set; }
        public string manifestUrl { get; set; } = "https://raw.githubusercontent.com/xrowex/RoweMod/master/version.json";
    }

    public class ChallengeRuntimeSettings
    {
        public bool enabled { get; set; } = true;
        public bool networkingEnabled { get; set; } = false;
        public bool autoOpenEnabled { get; set; } = false;
        public bool trickHooksEnabled { get; set; } = true;
    }

    public class ManualCatchSettings
    {
        public bool enabled { get; set; } = false;
        public bool debugLogging { get; set; } = true;
    }

    public class TrickAnimationDebugSettings
    {
        public bool enabled { get; set; } = true;
        public bool editorEnabled { get; set; } = true;
        public Dictionary<string, TrickAnimationOverride> overrides { get; set; } = new Dictionary<string, TrickAnimationOverride>();
    }

    public class TrickAnimationOverride
    {
        public float overallSpeedMult { get; set; }
        public float enterSpeedMult { get; set; }
        public float loopSpeedMult { get; set; }
        public float loopMult { get; set; }
        public float tweakSpeedMult { get; set; }
        public float exitSpeedMult { get; set; }
        public float tweakBeginBlendNormalizedTime { get; set; }
        public bool onlyFireIfInAir { get; set; }
        public bool allowLandingHolding { get; set; }
        public bool hasClipOverride { get; set; }
        public string playerEnterClip { get; set; }
        public string playerLoopClip { get; set; }
        public string playerTweakClip { get; set; }
        public string playerExitClip { get; set; }
        public string playerMirrorEnterClip { get; set; }
        public string playerMirrorLoopClip { get; set; }
        public string playerMirrorTweakClip { get; set; }
        public string playerMirrorExitClip { get; set; }
        public string vehicleEnterClip { get; set; }
        public string vehicleLoopClip { get; set; }
        public string vehicleTweakClip { get; set; }
        public string vehicleExitClip { get; set; }
        public string vehicleMirrorEnterClip { get; set; }
        public string vehicleMirrorLoopClip { get; set; }
        public string vehicleMirrorTweakClip { get; set; }
        public string vehicleMirrorExitClip { get; set; }
        public List<TrickPoseOverride> poseOverrides { get; set; } = new List<TrickPoseOverride>();
        public List<TrickIkTargetOverride> ikTargetOverrides { get; set; } = new List<TrickIkTargetOverride>();
    }

    public class TrickPoseOverride
    {
        public bool enabled { get; set; } = true;
        public string phase { get; set; } = "Any";
        public string bone { get; set; } = "Hips";
        public SerializableVector3 localRotationEuler { get; set; } = new SerializableVector3(0f, 0f, 0f);
        public SerializableVector3 localPositionOffset { get; set; } = new SerializableVector3(0f, 0f, 0f);
        public float weight { get; set; } = 1f;
    }

    public class TrickIkTargetOverride
    {
        public bool enabled { get; set; } = true;
        public string phase { get; set; } = "Any";
        public string goal { get; set; } = "LeftHand";
        public int targetId { get; set; } = -1;
        public bool activateTarget { get; set; } = true;
        public SerializableVector3 localPositionOffset { get; set; } = new SerializableVector3(0f, 0f, 0f);
        public SerializableVector3 localRotationEuler { get; set; } = new SerializableVector3(0f, 0f, 0f);
        public float offsetWeight { get; set; } = 1f;
        public float humanIkWeight { get; set; } = 1f;
        public float limbPositionWeight { get; set; } = 1f;
        public float limbRotationWeight { get; set; } = 1f;
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

    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public class GrindPoseConfigEntry
    {
        public Dictionary<string, bool> boolFields { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, float> floatFields { get; set; } = new Dictionary<string, float>();
        public Dictionary<string, SerializableVector3> vector3Fields { get; set; } = new Dictionary<string, SerializableVector3>();
    }

    public class GrindPoseSettings
    {
        public Dictionary<string, GrindPoseConfigEntry> poses { get; set; } = new Dictionary<string, GrindPoseConfigEntry>();
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
            bmxMaxSpeed = 7.5f,
            grindAlignAssist = false,
            grindAssistStrength = 0.5f,
            airAngularDrag = 2.75f,
            grindPoseLerpSpeed = 2f
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
            eyesMaterialPath = null,
            eyewearModelPath = null,
            eyewearMaterialPath = null
        };

        public static CustomBike bike = new CustomBike
        {
            barPitch = 0f,
            barScale = 1f,
            seatHeight = 0f,
            seatHeightUsesDefaultOffset = true,
            seatPitch = 330f,
            forkScale = 1f,
            frontWheelRadius = 1f,
            frontWheelWidth = 1f,
            rearWheelRadius = 1f,
            rearWheelWidth = 1f,
            frontLeftPegsEnabled = true,
            frontRightPegsEnabled = true,
            rearLeftPegsEnabled = true,
            rearRightPegsEnabled = true,
            bikeMaterials = new Dictionary<string, string>(),
            lastLoadedPresetBike = "None",
            lastLoadedBars = "None",
            lastLoadedFrame = "None",
            lastLoadedWheels = "None",
            lastLoadedStem = "None",
            lastLoadedFork = "None"
        };
        

        public static ChallengeSettings challengeSettings = new ChallengeSettings
        {
            challengeVisible = true,
            challengeSizeX = 5f,
            challengeSizeY = 5f,
            challengeSizeZ = 20f
        };

        public static Misc misc = new Misc
        {
            neverBail = false,
            droneMass = 10f,
            droneBodyToggle = true,
            droneEmitterToggle = true,
            showPlayerUserNameTargets = true,
            menuAccentR = 0.87f,
            menuAccentG = 0.5f,
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

        public static GrindPoseSettings grindPoseData = new GrindPoseSettings();
        public static Dictionary<string, MotorTuningConfigEntry> motorTuning = new Dictionary<string, MotorTuningConfigEntry>();
        public static UpdaterSettings updaterSettings = new UpdaterSettings();
        public static ChallengeRuntimeSettings challengeRuntimeSettings = new ChallengeRuntimeSettings();
        public static ManualCatchSettings manualCatchSettings = new ManualCatchSettings();
        public static TrickAnimationDebugSettings trickAnimationDebugSettings = new TrickAnimationDebugSettings();
        public static bool disclaimerAccepted = false;
        public static bool autoSkipIntro = true;



        // Helper class for JSON deserialization
        private class ConfigData
        {
            public Physics physicsData { get; set; }
            public CustomCharacter customCharacterData { get; set; }
            public CustomBike customBikeData { get; set; }
            public Misc miscData { get; set; }
            public CustomTricks customTricksData { get; set; }
            public GrindPoseSettings grindPoseData { get; set; }
            public Dictionary<string, MotorTuningConfigEntry> motorTuningData { get; set; }
            public UpdaterSettings updaterSettingsData { get; set; }
            public ChallengeRuntimeSettings challengeRuntimeSettingsData { get; set; }
            public ManualCatchSettings manualCatchSettingsData { get; set; }
            public TrickAnimationDebugSettings trickAnimationDebugSettingsData { get; set; }
            public bool disclaimerAccepted { get; set; }
            public bool autoSkipIntro { get; set; }
        }

        public static string modFolder = Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "RoweMod");
        public static string cfgFile { get; } = Path.Combine(modFolder, "cfg.json");

        // Save configuration to JSON file
        public static void Save()
        {
            try
            {
                //Log.Msg($"Saving config to {cfgFile}");
                Directory.CreateDirectory(modFolder);
                if (tricks.trickSets == null)
                    tricks.trickSets = new Dictionary<string, List<TrickEntry>>();

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
                        eyesMaterialPath = SafeMakeRelativePath(character.eyesMaterialPath),
                        eyewearModelPath = SafeMakeRelativePath(character.eyewearModelPath),
                        eyewearMaterialPath = SafeMakeRelativePath(character.eyewearMaterialPath)
                    },
                    customBikeData = bike,
                    miscData = misc,
                    customTricksData = tricks,
                    grindPoseData = grindPoseData,
                    motorTuningData = motorTuning,
                    updaterSettingsData = updaterSettings,
                    challengeRuntimeSettingsData = challengeRuntimeSettings,
                    manualCatchSettingsData = manualCatchSettings,
                    trickAnimationDebugSettingsData = trickAnimationDebugSettings,
                    disclaimerAccepted = disclaimerAccepted,
                    autoSkipIntro = autoSkipIntro
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

            //Log.Msg($"Loading config from {cfgFile}");
            string jsonContent = File.ReadAllText(cfgFile);
            bool hasShowPlayerUserNameTargets =
                jsonContent.IndexOf("\"showPlayerUserNameTargets\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasAutoSkipIntro =
                jsonContent.IndexOf("\"autoSkipIntro\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasChallengeRuntimeSettings =
                jsonContent.IndexOf("\"challengeRuntimeSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasManualCatchSettings =
                jsonContent.IndexOf("\"manualCatchSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasTrickAnimationDebugSettings =
                jsonContent.IndexOf("\"trickAnimationDebugSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            ConfigData jsonData = JsonConvert.DeserializeObject<ConfigData>(jsonContent);
            disclaimerAccepted = jsonData.disclaimerAccepted;
            autoSkipIntro = !hasAutoSkipIntro || jsonData.autoSkipIntro;

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
                eyesMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.eyesMaterialPath),
                eyewearModelPath = MakeAbsolutePath(jsonData.customCharacterData.eyewearModelPath),
                eyewearMaterialPath = MakeAbsolutePath(jsonData.customCharacterData.eyewearMaterialPath)
            };
            bike = jsonData.customBikeData;
            misc = jsonData.miscData;
            if (!hasShowPlayerUserNameTargets)
            {
                misc.showPlayerUserNameTargets = true;
            }
            tricks = jsonData.customTricksData;
            if (tricks.trickSets == null)
            {
                tricks.trickSets = new Dictionary<string, List<TrickEntry>>();
            }
            grindPoseData = jsonData.grindPoseData ?? new GrindPoseSettings();
            motorTuning = jsonData.motorTuningData ?? new Dictionary<string, MotorTuningConfigEntry>();
            updaterSettings = jsonData.updaterSettingsData ?? new UpdaterSettings();
            challengeRuntimeSettings = jsonData.challengeRuntimeSettingsData ?? new ChallengeRuntimeSettings();
            manualCatchSettings = jsonData.manualCatchSettingsData ?? new ManualCatchSettings();
            trickAnimationDebugSettings = jsonData.trickAnimationDebugSettingsData ?? new TrickAnimationDebugSettings();
            if (trickAnimationDebugSettings.overrides == null)
            {
                trickAnimationDebugSettings.overrides = new Dictionary<string, TrickAnimationOverride>();
            }
            foreach (TrickAnimationOverride trickOverride in trickAnimationDebugSettings.overrides.Values)
            {
                if (trickOverride == null)
                    continue;

                trickOverride.poseOverrides ??= new List<TrickPoseOverride>();
                foreach (TrickPoseOverride poseOverride in trickOverride.poseOverrides)
                {
                    if (poseOverride == null)
                        continue;

                    poseOverride.phase ??= "Any";
                    poseOverride.bone ??= "Hips";
                    if (poseOverride.weight <= 0f)
                        poseOverride.weight = 1f;
                }

                trickOverride.ikTargetOverrides ??= new List<TrickIkTargetOverride>();
                foreach (TrickIkTargetOverride ikOverride in trickOverride.ikTargetOverrides)
                {
                    if (ikOverride == null)
                        continue;

                    ikOverride.phase ??= "Any";
                    ikOverride.goal ??= "LeftHand";
                    ikOverride.offsetWeight = Clamp01OrDefault(ikOverride.offsetWeight, 1f);
                    ikOverride.humanIkWeight = Clamp01OrDefault(ikOverride.humanIkWeight, 1f);
                    ikOverride.limbPositionWeight = Clamp01OrDefault(ikOverride.limbPositionWeight, 1f);
                    ikOverride.limbRotationWeight = Clamp01OrDefault(ikOverride.limbRotationWeight, 1f);
                }
            }
            if (string.IsNullOrWhiteSpace(updaterSettings.manifestUrl))
            {
                updaterSettings.manifestUrl = new UpdaterSettings().manifestUrl;
            }

            if (grindPoseData.poses == null)
            {
                grindPoseData.poses = new Dictionary<string, GrindPoseConfigEntry>();
            }

            //set new config variables to defaults if 0
            if (physics.bmxForceFactor <= 0f) physics.bmxForceFactor = 0.07f;
            if (physics.bmxMaxSpeed <= 0f) physics.bmxMaxSpeed = 7.5f;
            if (physics.grindPoseLerpSpeed <= 0f) physics.grindPoseLerpSpeed = 2f;
            if (motorTuning == null) motorTuning = new Dictionary<string, MotorTuningConfigEntry>();

            if (!hasChallengeRuntimeSettings || !hasManualCatchSettings || !hasTrickAnimationDebugSettings)
            {
                Save();
            }
            
            Log.Msg($"Config loaded successfully.");
        }

        private static float Clamp01OrDefault(float value, float defaultValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return defaultValue;

            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
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
                case Custom.Slot.Eyewear: character.eyewearModelPath = path; break;
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
                case Custom.Slot.Eyewear: character.eyewearMaterialPath = path; break;
            }
        }
// Reset physics tab settings to defaults
        public static void ResetPhysicsTab()
        {
            float grindPoseLerpSpeed = physics.grindPoseLerpSpeed > 0f
                ? physics.grindPoseLerpSpeed
                : 2f;

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
                bmxMaxSpeed = 7.5f,
                grindAlignAssist = false,
                grindAssistStrength = 0.5f,
                airAngularDrag = 2.75f,
                grindPoseLerpSpeed = grindPoseLerpSpeed
            };

            motorTuning = new Dictionary<string, MotorTuningConfigEntry>();
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
                eyesMaterialPath = null,
                eyewearModelPath = null,
                eyewearMaterialPath = null
            };
            
            Memory.roweCharacterManager.InitCharacterData();
            Memory.roweCharacterManager.InitCharacterData();
            Custom.ResetTabState();
        }

        // Reset bike tab settings to defaults
        public static void ResetBikeTab()
        {
            bike.barPitch = 0f;
            bike.barScale = 1f;
            bike.seatHeight = 0f;
            bike.seatHeightUsesDefaultOffset = true;
            bike.seatPitch = 330f;
            bike.forkScale = 1f;
            bike.frontWheelRadius = 1f;
            bike.frontWheelWidth = 1f;
            bike.rearWheelRadius = 1f;
            bike.rearWheelWidth = 1f;
            bike.frontLeftPegsEnabled = true;
            bike.frontRightPegsEnabled = true;
            bike.rearLeftPegsEnabled = true;
            bike.rearRightPegsEnabled = true;
            bike.lastLoadedPresetBike = "None";
            bike.lastLoadedFrame = "None";
            bike.lastLoadedBars = "None";
            bike.lastLoadedWheels = "None";
            bike.lastLoadedStem = "None";
            bike.lastLoadedFork = "None";
            bike.bikeMaterials = new Dictionary<string, string>();
            Memory.lastEquippedBars = null;
            Memory.lastEquippedFrame = null;
            Memory.lastEquippedStem = null;
        }

        // Reset bike materials tab settings to defaults
        public static void ResetBikeMaterialsTab()
        {
            bike.bikeMaterials ??= new Dictionary<string, string>();
            bike.bikeMaterials.Clear();
        }

        // Reset misc tab settings to defaults
        public static void ResetMiscTab()
        {
            bool showPlayerUserNameTargets = misc.showPlayerUserNameTargets;
            string customSessionMarker = misc.customSessionMarker;
            autoSkipIntro = true;

            misc = new Misc
            {
                neverBail = false,
                droneMass = 10f,
                droneBodyToggle = true,
                droneEmitterToggle = true,
                showPlayerUserNameTargets = showPlayerUserNameTargets,
                menuAccentR = 0.87f,
                menuAccentG = 0.5f,
                menuAccentB = 0.3f,
                disableEmoteOnBike = false,
                disableFreeCamCollider = false,
                disableDroneCollider = false,
                customSessionMarker = customSessionMarker
            };
        }

        public static void ResetGrindsTab()
        {
            grindPoseData = new GrindPoseSettings
            {
                poses = new Dictionary<string, GrindPoseConfigEntry>()
            };
            physics.grindPoseLerpSpeed = 2f;
        }

        public static void ResetChallengeSettings()
        {
            challengeSettings = new ChallengeSettings
            {
                challengeVisible = true,
                challengeSizeX = 5f,
                challengeSizeY = 5f,
                challengeSizeZ = 20f
            };
        }

        public static GrindPoseConfigEntry GetOrCreateGrindPoseEntry(string poseKey)
        {
            if (grindPoseData == null)
            {
                grindPoseData = new GrindPoseSettings();
            }

            if (grindPoseData.poses == null)
            {
                grindPoseData.poses = new Dictionary<string, GrindPoseConfigEntry>();
            }

            if (!grindPoseData.poses.TryGetValue(poseKey, out GrindPoseConfigEntry entry) || entry == null)
            {
                entry = new GrindPoseConfigEntry();
                grindPoseData.poses[poseKey] = entry;
            }

            entry.boolFields ??= new Dictionary<string, bool>();
            entry.floatFields ??= new Dictionary<string, float>();
            entry.vector3Fields ??= new Dictionary<string, SerializableVector3>();

            return entry;
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
