using rowemod.Utils;
using Newtonsoft.Json;
using UnityEngine;

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
        private static readonly float DefaultHopForce = 5.5f;
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

        private static readonly bool DefaultBHideHelmet = true;
        private static string? DefaultBodyModelPath = null;
        private static string? DefaultBodyMaterialPath = null;
        private static string? DefaultTopModelPath = null;
        private static string? DefaultTopMaterialPath = null;
        private static string? DefaultGlovesModelPath = null;
        private static string? DefaultGlovesMaterialPath = null;
        private static string? DefaultBottomsModelPath = null;
        private static string? DefaultBottomsMaterialPath = null;
        private static string? DefaultSocksModelPath = null;
        private static string? DefaultSocksMaterialPath = null;
        private static string? DefaultShoesModelPath = null;
        private static string? DefaultShoesMaterialPath = null;
        private static string? DefaultBustModelPath = null;
        private static string? DefaultBustMaterialPath = null;
        private static string? DefaultHatModelPath = null;
        private static string? DefaultHatMaterialPath = null;
        private static string? DefaultHairModelPath = null;
        private static string? DefaultHairMaterialPath = null;
        private static string? DefaultEyesModelPath = null;
        private static string? DefaultEyesMaterialPath = null;

        private static readonly float DefaultCamLerp = 6f;
        private static readonly float DefaultFovValue = 60f;
        private static readonly Vector3 DefaultCamOffset = new Vector3(-0.35f, 0.65f, -2.92f);
        private static readonly bool DefaultFPVCamera = false;
        
        
        private static readonly float DefaulttpRecenterSpeed = 90f;
        private static readonly Vector3 DefaulttpCameraOffset = new Vector3(0f, -0.1f, -3f);
        private static readonly float DefaulttpCameraPitch = 15f;
        private static readonly float DefaulttpFovValue = 60f;

        private static readonly bool DefaultBShowInstructions = true;
        private static readonly bool DefaultBDiscoMode = false;
        private static readonly bool DefaultBVibration = true;
        private static readonly float DefaultDroneMass = 10f;
        private static readonly bool DefaultDroneBodyToggle = true;
        private static readonly float DefaultMenuAccentR = 0f;
        private static readonly float DefaultMenuAccentG = 0.2f;
        private static readonly float DefaultMenuAccentB = 0.6f;
        private static readonly float DefaultSloMoTimer = 1.0f;

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
        public static float hopForce = DefaultHopForce;
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
        
        
        // Custom model and material paths
        public static bool bHideHelmet = DefaultBHideHelmet;
        public static string? bodyModelPath = DefaultBodyModelPath;
        public static string? bodyMaterialPath = DefaultBodyMaterialPath;
        public static string? topModelPath = DefaultTopModelPath;
        public static string? topMaterialPath = DefaultTopMaterialPath;
        public static string? glovesModelPath = DefaultGlovesModelPath;
        public static string? glovesMaterialPath = DefaultGlovesMaterialPath;
        public static string? bottomsModelPath = DefaultBottomsModelPath;
        public static string? bottomsMaterialPath = DefaultBottomsMaterialPath;
        public static string? socksModelPath = DefaultSocksModelPath;
        public static string? socksMaterialPath = DefaultSocksMaterialPath;
        public static string? shoesModelPath = DefaultShoesModelPath;
        public static string? shoesMaterialPath = DefaultShoesMaterialPath;
        public static string? bustModelPath = DefaultBustModelPath;
        public static string? bustMaterialPath = DefaultBustMaterialPath;
        public static string? hatModelPath = DefaultHatModelPath;
        public static string? hatMaterialPath = DefaultHatMaterialPath;
        public static string? hairModelPath = DefaultHairModelPath;
        public static string? hairMaterialPath = DefaultHairMaterialPath;
        public static string? eyesModelPath = DefaultEyesModelPath;
        public static string? eyesMaterialPath = DefaultEyesMaterialPath;

        public static string customSessionMarker = "None";

        public static Dictionary<string, string> bikeMaterials { get; set; } = new Dictionary<string, string>();
        public static string lastLoadedPresetCharacter = "None"; // Default to None
        public static string lastLoadedPresetBike = "None"; // Default to None
        // Camera variables
        public static float camLerp = DefaultCamLerp;
        public static float fovValue = DefaultFovValue;
        public static Vector3 camOffset = DefaultCamOffset;
        public static bool bFpvCamera = DefaultFPVCamera;
        public static bool bUseOldCam = false;
        
        //third person camera
        public static float tpRecenterSpeed = DefaulttpRecenterSpeed;
        public static Vector3 tpCameraOffset = DefaulttpCameraOffset;
        public static float tpCameraPitch = DefaulttpCameraPitch;
        public static float tpFovValue = DefaulttpFovValue;
        
        
        
        // Misc variables
        public static bool bShowInstructions = DefaultBShowInstructions;
        public static bool bDiscoMode = DefaultBDiscoMode;
        public static bool bVibration = DefaultBVibration;
        public static float droneMass = DefaultDroneMass;
        public static bool droneBodyToggle = DefaultDroneBodyToggle;
        public static float menuAccentR = DefaultMenuAccentR;
        public static float menuAccentG = DefaultMenuAccentG;
        public static float menuAccentB = DefaultMenuAccentB;
        public static float sloMoTimer = DefaultSloMoTimer;

        public static string modFolder = Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "rowemod");
        public static string cfgFile { get; } = Path.Combine(modFolder, "cfg.json");



        public static void Save()
        {
            //Log.Msg("Saving config...");
            //Log.Msg($"Config Save: modFolder = {modFolder}");

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
                hopForce,
                sideHopPower,
                pumpForce,
                spinTorque,
                pedalForce,
                maxSpeed,
                steerDamp,
                breakForce,
                manualAngle,
                noseManualAngle,
                bHideHelmet,
                bodyModelPath = SafeMakeRelativePath(bodyModelPath),
                bodyMaterialPath = SafeMakeRelativePath(bodyMaterialPath),
                topModelPath = SafeMakeRelativePath(topModelPath),
                topMaterialPath = SafeMakeRelativePath(topMaterialPath),
                glovesModelPath = SafeMakeRelativePath(glovesModelPath),
                glovesMaterialPath = SafeMakeRelativePath(glovesMaterialPath),
                bottomsModelPath = SafeMakeRelativePath(bottomsModelPath),
                bottomsMaterialPath = SafeMakeRelativePath(bottomsMaterialPath),
                socksModelPath = SafeMakeRelativePath(socksMaterialPath),
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
                bShowInstructions,
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
                customSessionMarker
            });

            File.WriteAllText(cfgFile, contents);
            //Log.Msg("Config saved successfully.");
        }


        public static void Load()
        {
            JsonData jsonData = JsonConvert.DeserializeObject<JsonData>(File.ReadAllText(cfgFile));
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
            hopForce = jsonData.hopForce;
            sideHopPower = jsonData.sideHopPower;
            pumpForce = jsonData.pumpForce;
            spinTorque = jsonData.spinTorque;
            pedalForce = jsonData.pedalForce;
            maxSpeed = jsonData.maxSpeed;
            steerDamp = jsonData.steerDamp;
            breakForce = jsonData.breakForce;
            manualAngle = jsonData.manualAngle;
            noseManualAngle = jsonData.noseManualAngle;
            bHideHelmet = jsonData.bHideHelmet;
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
            shoesModelPath = MakeAbsolutePath(jsonData.shoesMaterialPath);
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
            bShowInstructions = jsonData.bShowInstructions;
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
        }
        
        /*public static void SaveCurrentLoadedItems()
        {
            if (Memory.physicsDrivenCharacter == null)
            {
                Log.Error("SaveCurrentLoadedItems: physicsDrivenCharacter is null!");
                return;
            }

            Transform charRoot = Memory.physicsDrivenCharacter.transform;
            
            foreach (var entry in slotNameMap)
            {
                Slot slot = entry.Key;
                string slotObjectName = entry.Value;

                // Determine full path based on parent structure
                string fullPath = "Physics Skeleton/" + slotObjectName;
                if (slotObjectName == "Hat_EquipSlot" || slotObjectName == "Hair_EquipSlot" || slotObjectName == "Eyes_EquipSlot")
                {
                    fullPath = "Physics Skeleton/HeadGear/" + slotObjectName;
                }

                Transform slotTransform = charRoot.Find(fullPath);
                if (slotTransform == null)
                {
                    Log.Warning($"SaveCurrentLoadedItems: Slot '{fullPath}' not found!");
                    continue;
                }

                // Get the current model name (mesh name)
                string modelName = GetModelName(slotTransform);
                string materialName = GetMaterialName(slotTransform);

                // Save to Config using the correct slot
                switch (slot)
                {
                    case Slot.Body:
                        Config.DefaultBodyModelPath = modelName;
                        Config.DefaultBodyMaterialPath = materialName;
                        break;
                    case Slot.Top:
                        Config.DefaultTopModelPath = modelName;
                        Config.DefaultTopMaterialPath = materialName;
                        break;
                    case Slot.Gloves:
                        Config.DefaultGlovesModelPath = modelName;
                        Config.DefaultGlovesMaterialPath = materialName;
                        break;
                    case Slot.Bottoms:
                        Config.DefaultBottomsModelPath = modelName;
                        Config.DefaultBottomsMaterialPath = materialName;
                        break;
                    case Slot.Socks:
                        Config.DefaultSocksModelPath = modelName;
                        Config.DefaultSocksMaterialPath = materialName;
                        break;
                    case Slot.Shoes:
                        Config.DefaultShoesModelPath = modelName;
                        Config.DefaultShoesMaterialPath = materialName;
                        break;
                    case Slot.Bust:
                        Config.DefaultBustModelPath = modelName;
                        Config.DefaultBustMaterialPath = materialName;
                        break;
                    case Slot.Hat:
                        Config.DefaultHatModelPath = modelName;
                        Config.DefaultHatMaterialPath = materialName;
                        break;
                    case Slot.Hair:
                        Config.DefaultHairModelPath = modelName;
                        Config.DefaultHairMaterialPath = materialName;
                        break;
                    case Slot.Eyes:
                        Config.DefaultEyesModelPath = modelName;
                        Config.DefaultEyesMaterialPath = materialName;
                        break;
                }
            }

            Log.Msg("Saved current loaded items as defaults.");
        }*/

        // Gets the model (mesh) name from the slot
        private static string GetModelName(Transform slotTransform)
        {
            MeshFilter meshFilter = slotTransform.GetComponentInChildren<MeshFilter>();
            return meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "UnknownModel";
        }

        // Gets the material name from the slot
        private static string GetMaterialName(Transform slotTransform)
        {
            Renderer renderer = slotTransform.GetComponentInChildren<Renderer>();
            return renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "UnknownMaterial";
        }


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
            hopForce = DefaultHopForce;
            pumpForce = DefaultPumpForce;
            spinTorque = DefaultSpinTorque;
            steerDamp = DefaultSteerDamp;
            breakForce = DefaultBreakForce;
            manualAngle = DefaultManualAngle;
            noseManualAngle = DefaultNoseManualAngle;
            quickSpinMultiplier = DefaultQuickSpinMultiplier;
            
        }

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
            
        }

        public static void ResetBikeTab()
        {
            //lastVehicle = DefaultLastVehicle;
        }

        public static void ResetBikeMaterialsTab()
        {
            bikeMaterials.Clear();
        }

        public static void ResetMiscTab()
        {
            bHideHelmet = DefaultBHideHelmet;
            bShowInstructions = DefaultBShowInstructions;
            bDiscoMode = DefaultBDiscoMode;
            bVibration = DefaultBVibration;
            droneMass = DefaultDroneMass;
            sloMoTimer = DefaultSloMoTimer;
            menuAccentR = DefaultMenuAccentR;
            menuAccentG = DefaultMenuAccentG;
            menuAccentB = DefaultMenuAccentB;
            
        }

        public static void ResetCameraTab()
        {
            Log.Warning("Camera tab resetting.");
            camLerp = DefaultCamLerp;
            fovValue = DefaultFovValue;
            camOffset = DefaultCamOffset;
            bFpvCamera = DefaultFPVCamera;
            bUseOldCam = false;
            tpRecenterSpeed = DefaulttpRecenterSpeed;
            tpCameraOffset = DefaulttpCameraOffset;
            tpCameraPitch = DefaulttpCameraPitch;
            tpFovValue = DefaulttpFovValue;
        }

        public static void ResetGraphicsTab()
        {
            //
        }
        public static string MakeRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return fullPath; // Prevents errors on empty paths

            if (!Path.IsPathRooted(fullPath))
            {
                //Log.Warning($"MakeRelativePath: Path is already relative: {fullPath}");
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

        public static string MakeAbsolutePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return relativePath;
            return Path.GetFullPath(Path.Combine(modFolder, relativePath));
        }

        private static string SafeMakeRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (!Path.IsPathRooted(path))
            {
                //Log.Msg($"SafeMakeRelativePath: Path is already relative -> {path}");
                return path;
            }

            return MakeRelativePath(path);
        }


    }
}
