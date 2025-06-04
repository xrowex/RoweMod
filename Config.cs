using rowemod.Utils;
using Newtonsoft.Json;
using rowemod.Mods;
using UnityEngine;
using System;
using System.IO;

namespace rowemod
{
    public static class Config
    {
        // Default values for resetting
        private static readonly bool DefaultBBreakBike = false;
        private static readonly bool DefaultBDisableLevelInAir = false;
        private static readonly bool DefaultBManualMovement = false;
        private static readonly bool DefaultAllowTrickLanders = false;
        private static readonly bool DefaultAlwaysAllowFireTricks = false;
        private static readonly bool DefaultSpinAssist = true;
        private static readonly bool DefaultSpinFlipFix = false;
        private static readonly bool DefaultDriftAbilityEnabled = true;
        private static readonly int DefaultLastVehicle = 0;
        private static readonly float DefaultGravity = 12.5f;
        private static readonly float DefaultSmallHopForce = 4.2f;
        //private static readonly float DefaultHopForce = 5.5f;
        private static readonly float DefaultPumpForce = 1.5f;
        private static readonly float DefaultSpinTorque = 1f;
        private static readonly float DefaultPedalForce = 5f;
        private static readonly float DefaultMaxSpeed = 9f;
        private static readonly float DefaultSteerDamp = 5f;
        private static readonly float DefaultBreakForce = 100_000f;
        private static readonly float DefaultManualAngle = 30f;
        private static readonly float DefaultNoseManualAngle = 30f;
        private static readonly float DefaultQuickSpinMultiplier = 1.0f;
        private static readonly float DefaultSideHopPower = 1.0f;
        private static readonly float DefaultBarRotationAngle = 0f;
        private static readonly float DefaultSeatHeight = 0.15f;
        private static readonly float DefaultSeatRotationX = 330f;
        private static readonly float DefaultPegLength = 1f; 

        private static readonly string DefaultBodyModelPath = null;
        private static readonly string DefaultBodyMaterialPath = null;
        private static readonly string DefaultTopModelPath = null;
        private static readonly string DefaultTopMaterialPath = null;
        private static readonly string DefaultGlovesModelPath = null;
        private static readonly string DefaultGlovesMaterialPath = null;
        private static readonly string DefaultBottomsModelPath = null;
        private static readonly string DefaultBottomsMaterialPath = null;
        private static readonly string DefaultSocksModelPath = null;
        private static readonly string DefaultSocksMaterialPath = null;
        private static readonly string DefaultShoesModelPath = null;
        private static readonly string DefaultShoesMaterialPath = null;
        private static readonly string DefaultBustModelPath = null;
        private static readonly string DefaultBustMaterialPath = null;
        private static readonly string DefaultHatModelPath = null;
        private static readonly string DefaultHatMaterialPath = null;
        private static readonly string DefaultHairModelPath = null;
        private static readonly string DefaultHairMaterialPath = null;
        private static readonly string DefaultEyesModelPath = null;
        private static readonly string DefaultEyesMaterialPath = null;

        private static readonly float DefaultCamLerp = 6f;
        private static readonly float DefaultFovValue = 60f;
        private static readonly Vector3 DefaultCamOffset = new Vector3(-0.35f, 0.65f, -2.92f);
        private static readonly bool DefaultFpvCamera = false;
        
        private static readonly float DefaultTpRecenterSpeed = 90f;
        private static readonly Vector3 DefaultTpCameraOffset = new Vector3(0f, -0.1f, -3f);
        private static readonly float DefaultTpCameraPitch = 15f;
        private static readonly float DefaultTpFovValue = 60f;

        private static readonly bool DefaultBNeverBail = false;
        private static readonly bool DefaultBShowHUD = true;
        private static readonly bool DefaultBDiscoMode = false;
        private static readonly bool DefaultBVibration = true;
        private static readonly float DefaultDroneMass = 10f;
        private static readonly bool DefaultDroneBodyToggle = true;
        private static readonly bool DefaultDroneEmitterToggle = true;
        private static readonly float DefaultMenuAccentR = 0.2f;
        private static readonly float DefaultMenuAccentG = 0.6f;
        private static readonly float DefaultMenuAccentB = 1.0f;
        private static readonly float DefaultSloMoTimer = 1.0f;

        // Added for FreeCam collider toggle feature
        private static readonly bool DefaultBDisableFreeCamCollider = false;

        // Added for Drone collider toggle feature
        private static readonly bool DefaultBDisableDroneCollider = false;

        // Physics variables
        public static bool bBreakBike = DefaultBBreakBike;
        public static bool bDisableLevelInAir = DefaultBDisableLevelInAir;
        public static bool bManualMovement = DefaultBManualMovement;
        public static bool allowTrickLanders = DefaultAllowTrickLanders;
        public static bool alwaysAllowFireTricks = DefaultAlwaysAllowFireTricks;
        public static bool bSpinAssist = DefaultSpinAssist;
        public static bool bSpinFlipFix = DefaultSpinFlipFix;
        public static bool bDriftAbility = DefaultDriftAbilityEnabled;
        public static int lastVehicle = DefaultLastVehicle;
        public static float gravity = DefaultGravity;
        public static float smallHopForce = DefaultSmallHopForce;
        //public static float hopForce = DefaultHopForce;
        public static float pumpForce = DefaultPumpForce;
        public static float spinTorque = DefaultSpinTorque;
        public static float pedalForce = DefaultPedalForce;
        public static float maxSpeed = DefaultMaxSpeed;
        public static float steerDamp = DefaultSteerDamp;
        public static float breakForce = DefaultBreakForce;
        public static float manualAngle = DefaultManualAngle;
        public static float noseManualAngle = DefaultNoseManualAngle;
        public static float quickSpinMultiplier = DefaultQuickSpinMultiplier;
        public static float sideHopPower = DefaultSideHopPower;
        public static float vertAssistCorrectionStrength = 30f; 

        // Custom model and material paths
        public static string bodyModelPath = DefaultBodyModelPath;
        public static string bodyMaterialPath = DefaultBodyMaterialPath;
        public static string topModelPath = DefaultTopModelPath;
        public static string topMaterialPath = DefaultTopMaterialPath;
        public static string glovesModelPath = DefaultGlovesModelPath;
        public static string glovesMaterialPath = DefaultGlovesMaterialPath;
        public static string bottomsModelPath = DefaultBottomsModelPath;
        public static string bottomsMaterialPath = DefaultBottomsMaterialPath;
        public static string socksModelPath = DefaultSocksModelPath;
        public static string socksMaterialPath = DefaultSocksMaterialPath;
        public static string shoesModelPath = DefaultShoesModelPath;
        public static string shoesMaterialPath = DefaultShoesMaterialPath;
        public static string bustModelPath = DefaultBustModelPath;
        public static string bustMaterialPath = DefaultBustMaterialPath;
        public static string hatModelPath = DefaultHatModelPath;
        public static string hatMaterialPath = DefaultHatMaterialPath;
        public static string hairModelPath = DefaultHairModelPath;
        public static string hairMaterialPath = DefaultHairMaterialPath;
        public static string eyesModelPath = DefaultEyesModelPath;
        public static string eyesMaterialPath = DefaultEyesMaterialPath;

        public static string customSessionMarker = "None";
        public static float barRotationAngle = DefaultBarRotationAngle;
        public static float seatHeight = DefaultSeatHeight;
        public static float seatRotationX = DefaultSeatRotationX; 
        public static float pegLength = DefaultPegLength; 
        public static Dictionary<string, string> bikeMaterials = new Dictionary<string, string>();
        public static string lastLoadedPresetCharacter = "None"; // Default to None
        public static string lastLoadedPresetBike = "None"; // Default to None

        // Camera variables
        public static float camLerp = DefaultCamLerp;
        public static float fovValue = DefaultFovValue;
        public static Vector3 camOffset = DefaultCamOffset;
        public static bool bFpvCamera = DefaultFpvCamera;
        public static bool bUseOldCam = false;
        
        // Third-person camera
        public static float tpRecenterSpeed = DefaultTpRecenterSpeed;
        public static Vector3 tpCameraOffset = DefaultTpCameraOffset;
        public static float tpCameraPitch = DefaultTpCameraPitch;
        public static float tpFovValue = DefaultTpFovValue;

        // Misc variables
        public static bool bNeverBail = DefaultBNeverBail;
        public static bool bShowHUD = DefaultBShowHUD;
        public static bool bDiscoMode = DefaultBDiscoMode;
        public static bool bVibration = DefaultBVibration;
        public static float droneMass = DefaultDroneMass;
        public static bool droneBodyToggle = DefaultDroneBodyToggle;
        public static bool droneEmitterToggle = DefaultDroneEmitterToggle;
        public static float menuAccentR = DefaultMenuAccentR;
        public static float menuAccentG = DefaultMenuAccentG;
        public static float menuAccentB = DefaultMenuAccentB;
        public static float sloMoTimer = DefaultSloMoTimer;

        public static bool disableEmoteOnBike = false;

        // Added for FreeCam collider toggle feature
        public static bool bDisableFreeCamCollider = DefaultBDisableFreeCamCollider;

        // Added for Drone collider toggle feature
        public static bool bDisableDroneCollider = DefaultBDisableDroneCollider;

        public static string modFolder = Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "rowemod");
        public static string cfgFile { get; } = Path.Combine(modFolder, "cfg.json");

        // Save configuration to JSON file
        public static void Save()
        {
            try
            {
                // Logging save operation
                Log.Msg($"Saving config to {cfgFile}");

                string contents = JsonConvert.SerializeObject(new
                {
                    bBreakBike,
                    bDisableLevelInAir,
                    bManualMovement,
                    allowTrickLanders,
                    alwaysAllowFireTricks,
                    bSpinAssist,
                    bSpinFlipFix,
                    bDriftAbility,
                    lastVehicle,
                    gravity,
                    smallHopForce,
                    sideHopPower,
                    pumpForce,
                    spinTorque,
                    pedalForce,
                    maxSpeed,
                    steerDamp,
                    breakForce,
                    manualAngle,
                    noseManualAngle,
                    bodyModelPath = SafeMakeRelativePath(bodyModelPath),
                    bodyMaterialPath = SafeMakeRelativePath(bodyMaterialPath),
                    topModelPath = SafeMakeRelativePath(topModelPath),
                    topMaterialPath = SafeMakeRelativePath(topMaterialPath),
                    glovesModelPath = SafeMakeRelativePath(glovesModelPath),
                    glovesMaterialPath = SafeMakeRelativePath(glovesMaterialPath),
                    bottomsModelPath = SafeMakeRelativePath(bottomsModelPath),
                    bottomsMaterialPath = SafeMakeRelativePath(bottomsMaterialPath),
                    socksModelPath = SafeMakeRelativePath(socksModelPath),
                    socksMaterialPath = SafeMakeRelativePath(socksMaterialPath),
                    shoesModelPath = SafeMakeRelativePath(shoesModelPath),
                    shoesMaterialPath = SafeMakeRelativePath(shoesMaterialPath),
                    bustModelPath = SafeMakeRelativePath(bustModelPath),
                    bustMaterialPath = SafeMakeRelativePath(bustMaterialPath),
                    hatModelPath = SafeMakeRelativePath(hatModelPath),
                    hatMaterialPath = SafeMakeRelativePath(hatMaterialPath),
                    hairModelPath = SafeMakeRelativePath(hairModelPath),
                    hairMaterialPath = SafeMakeRelativePath(hairMaterialPath),
                    eyesModelPath = SafeMakeRelativePath(eyesModelPath),
                    eyesMaterialPath = SafeMakeRelativePath(eyesMaterialPath),
                    lastLoadedPresetCharacter,
                    lastLoadedPresetBike,
                    camLerp,
                    fovValue,
                    camOffset = new { camOffset.x, camOffset.y, camOffset.z },
                    bNeverBail,
                    bShowHUD,
                    bDiscoMode,
                    bVibration,
                    droneMass,
                    droneBodyToggle,
                    menuAccentR,
                    menuAccentG,
                    menuAccentB,
                    sloMoTimer,
                    bikeMaterials,
                    quickSpinMultiplier,
                    bFpvCamera,
                    bUseOldCam,
                    tpRecenterSpeed,
                    tpCameraOffset = new { tpCameraOffset.x, tpCameraOffset.y, tpCameraOffset.z },
                    tpCameraPitch,
                    tpFovValue,
                    customSessionMarker,
                    droneEmitterToggle,
                    barRotationAngle,
                    seatHeight,
                    seatRotationX,
                    pegLength,
                    bDisableFreeCamCollider,
                    bDisableDroneCollider
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
            // Check if config file exists
            if (!File.Exists(cfgFile))
            {
                Log.Msg($"Config file not found at {cfgFile}. Using default values (menu accent: R={menuAccentR}, G={menuAccentG}, B={menuAccentB}).");
                Save(); // Create new config with defaults
                return;
            }

            try
            {
                Log.Msg($"Loading config from {cfgFile}");
                string jsonContent = File.ReadAllText(cfgFile);
                JsonData jsonData = JsonConvert.DeserializeObject<JsonData>(jsonContent);

                // Assign values from JSON, preserving defaults if fields are missing
                bBreakBike = jsonData.bBreakBike;
                bDisableLevelInAir = jsonData.bDisableLevelInAir;
                bManualMovement = jsonData.bManualMovement;
                allowTrickLanders = jsonData.allowTrickLanders;
                alwaysAllowFireTricks = jsonData.alwaysAllowFireTricks;
                bSpinAssist = jsonData.bSpinAssist;
                bSpinFlipFix = jsonData.bSpinFlipFix;
                bDriftAbility = jsonData.bDriftAbility;
                lastVehicle = jsonData.lastVehicle;
                gravity = jsonData.gravity;
                smallHopForce = jsonData.smallHopForce;
                sideHopPower = jsonData.sideHopPower;
                pumpForce = jsonData.pumpForce;
                spinTorque = jsonData.spinTorque;
                pedalForce = jsonData.pedalForce;
                maxSpeed = jsonData.maxSpeed;
                steerDamp = jsonData.steerDamp;
                breakForce = jsonData.breakForce;
                manualAngle = jsonData.manualAngle;
                noseManualAngle = jsonData.noseManualAngle;
                bodyModelPath = MakeAbsolutePath(jsonData.bodyModelPath);
                bodyMaterialPath = MakeAbsolutePath(jsonData.bodyMaterialPath);
                topModelPath = MakeAbsolutePath(jsonData.topModelPath);
                topMaterialPath = MakeAbsolutePath(jsonData.topMaterialPath);
                glovesModelPath = MakeAbsolutePath(jsonData.glovesModelPath);
                glovesMaterialPath = MakeAbsolutePath(jsonData.glovesMaterialPath);
                bottomsModelPath = MakeAbsolutePath(jsonData.bottomsModelPath);
                bottomsMaterialPath = MakeAbsolutePath(jsonData.bottomsMaterialPath);
                socksModelPath = MakeAbsolutePath(jsonData.socksModelPath);
                socksMaterialPath = MakeAbsolutePath(jsonData.socksMaterialPath);
                shoesModelPath = MakeAbsolutePath(jsonData.shoesModelPath);
                shoesMaterialPath = MakeAbsolutePath(jsonData.shoesMaterialPath);
                bustModelPath = MakeAbsolutePath(jsonData.bustModelPath);
                bustMaterialPath = MakeAbsolutePath(jsonData.bustMaterialPath);
                hatModelPath = MakeAbsolutePath(jsonData.hatModelPath);
                hatMaterialPath = MakeAbsolutePath(jsonData.hatMaterialPath);
                hairModelPath = MakeAbsolutePath(jsonData.hairModelPath);
                hairMaterialPath = MakeAbsolutePath(jsonData.hairMaterialPath);
                eyesModelPath = MakeAbsolutePath(jsonData.eyesModelPath);
                eyesMaterialPath = MakeAbsolutePath(jsonData.eyesMaterialPath);
                lastLoadedPresetCharacter = jsonData.lastLoadedPresetCharacter;
                lastLoadedPresetBike = jsonData.lastLoadedPresetBike;
                camLerp = jsonData.camLerp;
                fovValue = jsonData.fovValue;
                camOffset = new Vector3(jsonData.camOffset.x, jsonData.camOffset.y, jsonData.camOffset.z);
                bNeverBail = jsonData.bNeverBail;
                bShowHUD = jsonData.bShowHUD;
                bDiscoMode = jsonData.bDiscoMode;
                bVibration = jsonData.bVibration;
                droneMass = jsonData.droneMass;
                droneBodyToggle = jsonData.droneBodyToggle;
                menuAccentR = jsonData.menuAccentR;
                menuAccentG = jsonData.menuAccentG;
                menuAccentB = jsonData.menuAccentB;
                sloMoTimer = jsonData.sloMoTimer;
                bikeMaterials = jsonData.bikeMaterials ?? new Dictionary<string, string>();
                quickSpinMultiplier = jsonData.quickSpinMultiplier;
                bFpvCamera = jsonData.bFpvCamera;
                bUseOldCam = jsonData.bUseOldCam;
                tpRecenterSpeed = jsonData.tpRecenterSpeed;
                tpCameraOffset = new Vector3(jsonData.tpCameraOffset.x, jsonData.tpCameraOffset.y, jsonData.tpCameraOffset.z);
                tpCameraPitch = jsonData.tpCameraPitch;
                tpFovValue = jsonData.tpFovValue;
                customSessionMarker = jsonData.customSessionMarker;
                droneEmitterToggle = jsonData.droneEmitterToggle;
                barRotationAngle = jsonData.barRotationAngle;
                seatHeight = jsonData.seatHeight;
                seatRotationX = jsonData.seatRotationX;
                pegLength = jsonData.pegLength;
                bDisableFreeCamCollider = jsonData.bDisableFreeCamCollider;
                bDisableDroneCollider = jsonData.bDisableDroneCollider;

                Log.Msg($"Config loaded successfully. Menu accent: R={menuAccentR}, G={menuAccentG}, B={menuAccentB}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load config: {ex.Message}. Using default values (menu accent: R={menuAccentR}, G={menuAccentG}, B={menuAccentB}).");
                Save(); // Create new config with defaults
            }
        }

        // Reset physics tab settings to defaults
        public static void ResetPhysicsTab()
        {
            bBreakBike = DefaultBBreakBike;
            bDisableLevelInAir = DefaultBDisableLevelInAir;
            bManualMovement = DefaultBManualMovement;
            allowTrickLanders = DefaultAllowTrickLanders;
            alwaysAllowFireTricks = DefaultAlwaysAllowFireTricks;
            bSpinAssist = DefaultSpinAssist;
            gravity = DefaultGravity;
            smallHopForce = DefaultSmallHopForce;
            //hopForce = DefaultHopForce;
            pumpForce = DefaultPumpForce;
            spinTorque = DefaultSpinTorque;
            steerDamp = DefaultSteerDamp;
            breakForce = DefaultBreakForce;
            manualAngle = DefaultManualAngle;
            noseManualAngle = DefaultNoseManualAngle;
            quickSpinMultiplier = DefaultQuickSpinMultiplier;
        }

        // Reset character tab settings to defaults
        public static void ResetCharacterTab()
        {
            bodyModelPath = DefaultBodyModelPath;
            bodyMaterialPath = DefaultBodyMaterialPath;
            topModelPath = DefaultTopModelPath;
            topMaterialPath = DefaultTopMaterialPath;
            glovesModelPath = DefaultGlovesModelPath;
            glovesMaterialPath = DefaultGlovesMaterialPath;
            bottomsModelPath = DefaultBottomsModelPath;
            bottomsMaterialPath = DefaultBottomsMaterialPath;
            socksModelPath = DefaultSocksModelPath;
            socksMaterialPath = DefaultSocksMaterialPath;
            shoesModelPath = DefaultShoesModelPath;
            shoesMaterialPath = DefaultShoesMaterialPath;
            bustModelPath = DefaultBustModelPath;
            bustMaterialPath = DefaultBustMaterialPath;
            hatModelPath = DefaultHatModelPath;
            hatMaterialPath = DefaultHatMaterialPath;
            hairModelPath = DefaultHairModelPath;
            hairMaterialPath = DefaultHairMaterialPath;
            eyesModelPath = DefaultEyesModelPath;
            eyesMaterialPath = DefaultEyesMaterialPath;
            RoweCustomCharacter.rCharacterManager.ChangeData(RoweCustomCharacter.defaultCharacterData);
        }

        // Reset bike tab settings to defaults
        public static void ResetBikeTab()
        {
            // No settings to reset currently
        }

        // Reset bike materials tab settings to defaults
        public static void ResetBikeMaterialsTab()
        {
            bikeMaterials.Clear();
        }

        // Reset misc tab settings to defaults
        public static void ResetMiscTab()
        {
            bNeverBail = DefaultBNeverBail;
            bShowHUD = DefaultBShowHUD;
            bDiscoMode = DefaultBDiscoMode;
            bVibration = DefaultBVibration;
            droneMass = DefaultDroneMass;
            sloMoTimer = DefaultSloMoTimer;
            menuAccentR = DefaultMenuAccentR;
            menuAccentG = DefaultMenuAccentG;
            menuAccentB = DefaultMenuAccentB;
            bDisableFreeCamCollider = DefaultBDisableFreeCamCollider;
            bDisableDroneCollider = DefaultBDisableDroneCollider;
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