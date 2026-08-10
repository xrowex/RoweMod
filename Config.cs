using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Newtonsoft.Json;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;

namespace rowemod
{
    public struct Physics
    {
        public bool disableLevelInAir;
        public bool manualMovement;
        public bool spinAssist;
        public bool spinCompletionAssist;
        public bool spinFlipFix;
        public bool driftAbility;
        public int lastVehicle;
        public float gravity;
        public float smallHopForce;
        public float pumpForce;
        public float steerDamp;
        public float manualAngle;
        public float noseManualAngle;
        public bool noseManualTurnTuning;
        public bool noseManualDebugLogging;
        public float noseManualChassisComForwardOffset;
        public float noseManualChassisComVerticalOffset;
        public float noseManualDriverComForwardOffset;
        public float noseManualDriverComVerticalOffset;
        public float noseManualComTurnLean;
        public float noseManualDriverInertiaMultiplier;
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
        public bool enabled { get; set; }
        public bool enabledMigrated { get; set; }
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
        public bool disableBoneBreaking;
        public float boneBreakingStrength;
        public float droneMass;
        public bool droneBodyToggle;
        public bool droneEmitterToggle;
        public bool showPlayerUserNameTargets;
        public float menuAccentR;
        public float menuAccentG;
        public float menuAccentB;
        public float menuScale;
        public int menuDesignVersion;
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

    public class CameraSettings
    {
        public bool leftStickOffsetSwitch { get; set; } = true;
    }

    public class SceneExposureSettings
    {
        public string sceneName { get; set; } = string.Empty;
        public bool exposureOverride { get; set; }
        public bool forceFixedExposure { get; set; }
        public float exposureCompensation { get; set; }
        public float fixedExposure { get; set; }
    }

    /// <summary>
    /// Graphics overrides are opt-in. Preset 0 leaves the map and the game's quality settings
    /// untouched; GraphicsEnvironmentController snapshots live values before applying anything.
    /// </summary>
    public class GraphicsSettings
    {
        // 0 = Game, 1 = Balanced, 2 = Low, 3 = Potato, 4 = Custom.
        public int performancePreset { get; set; }
        public float renderScale { get; set; } = 1f;
        public bool realtimeShadows { get; set; } = true;
        public float shadowDistance { get; set; } = 80f;
        public float lodBias { get; set; } = 1f;
        public int textureMipmapLimit { get; set; }
        // Mirrors HDAdditionalCameraData.AntialiasingMode: None, FXAA, TAA, SMAA.
        public int antiAliasingMode { get; set; } = 2;
        public float terrainDetailDensity { get; set; } = 1f;
        public float terrainDetailDistance { get; set; } = 80f;

        // "Allow" means restore/use the map's value. False forces the effect off.
        public bool allowScreenSpaceReflections { get; set; } = true;
        public bool allowAmbientOcclusion { get; set; } = true;
        public bool allowGlobalIllumination { get; set; } = true;
        public bool allowVolumetricClouds { get; set; } = true;
        public bool allowVolumetricFog { get; set; } = true;
        public bool allowContactShadows { get; set; } = true;
        public bool allowMotionBlur { get; set; } = true;
        public bool allowDepthOfField { get; set; } = true;
        public bool allowBloom { get; set; } = true;
        public bool allowRealtimeReflectionProbes { get; set; } = true;

        // Empty/"Map" restores the HDRI assigned by the current map.
        public string skySelection { get; set; } = "Map";
        public bool skyTuningOverride { get; set; }
        public float skyRotation { get; set; }
        public float skyExposure { get; set; }

        // Camera exposure is stored independently for each Unity scene containing an HDRP
        // Exposure component. The dictionary key is the stable scene path, falling back to name.
        public Dictionary<string, SceneExposureSettings> sceneExposureSettings { get; set; } =
            new Dictionary<string, SceneExposureSettings>();

        // Legacy single-scene values retained so an existing local config can be migrated into
        // the first discovered scene by GraphicsEnvironmentController.
        public bool exposureOverride { get; set; }
        public bool forceFixedExposure { get; set; }
        public float exposureCompensation { get; set; }
        public float fixedExposure { get; set; }
    }

    public class ReplaySettings
    {
        public int cameraLabVersion { get; set; } = 1;
        public float replayFov { get; set; } = 60f;
        public float replayTilt { get; set; }
        public float replayFisheye { get; set; }
        // Reuses HDRP's existing uber-post lens distortion. These controls only shape the
        // native pass; RoweMod does not allocate another full-screen texture or shader pass.
        public bool replayFisheyeOpticsEnabled { get; set; } = true;
        public float replayFisheyeXMultiplier { get; set; } = 1f;
        public float replayFisheyeYMultiplier { get; set; } = 1f;
        public float replayFisheyeCenterX { get; set; }
        public float replayFisheyeCenterY { get; set; }
        public float replayFisheyeScale { get; set; } = 1f;
        // Native HDRP approximation of the Century Optics MK1 / VX1000 death-lens stack.
        // Panini is the only additional post-process pass; fringe and grain remain in UberPost.
        public bool replayMk1Enabled { get; set; }
        public float replayMk1PaniniDistance { get; set; } = 0.25f;
        public float replayMk1PaniniCrop { get; set; } = 0.65f;
        public float replayMk1ChromaticAberration { get; set; } = 0.07f;
        public float replayMk1FilmGrain { get; set; } = 0.06f;
        public float replayVignette { get; set; } = 0.05f;
        public int replayShakeMode { get; set; }
        public bool replayDofEnabled { get; set; }
        public bool replayDofPhysicallyBased { get; set; }
        public float replayNearFocusStart { get; set; }
        public float replayNearFocusEnd { get; set; } = 2f;
        public float replayFarFocusStart { get; set; } = 8f;
        public float replayFarFocusEnd { get; set; } = 20f;
        // 0 = Off, 1 = 16:9, 2 = 4:3. This draws framing mattes only and never
        // changes the game's render resolution or camera projection.
        public int replayFramingMode { get; set; }
        public float replayMatteOpacity { get; set; } = 1f;
        public string activeReplayLensPreset { get; set; } = string.Empty;
        public bool cameraLightEnabled { get; set; }
        public float cameraLightIntensity { get; set; } = 20f;
        public float cameraLightRange { get; set; } = 14f;
        public float cameraLightSpotAngle { get; set; } = 75f;
        public float cameraLightColorR { get; set; } = 1f;
        public float cameraLightColorG { get; set; } = 0.95f;
        public float cameraLightColorB { get; set; } = 0.88f;
        public int cameraLightType { get; set; }
        public float cameraLightOffsetX { get; set; }
        public float cameraLightOffsetY { get; set; }
        public float cameraLightOffsetZ { get; set; } = 0.03f;
        public float cameraLightPitch { get; set; }
        public float cameraLightYaw { get; set; }
        public float cameraLightRoll { get; set; }
        public bool cameraLightShadows { get; set; } = true;
        public bool cameraLightSoftShadows { get; set; } = true;
        public int cameraLightShadowResolution { get; set; } = 3;
        public float cameraLightShadowStrength { get; set; } = 1f;
        public float cameraLightShadowBias { get; set; } = 0.05f;
        public float cameraLightShadowNormalBias { get; set; } = 0.4f;
        public float cameraLightShadowNearPlane { get; set; } = 0.2f;
        public bool replayCameraNoCollision { get; set; }
    }

    /// <summary>
    /// Local-only peg-grind VFX. The native BMXCollisionHandler remains the authority for
    /// whether a peg is grinding; these values only control RoweMod's presentation.
    /// </summary>
    public class PegSparksSettings
    {
        public bool enabled { get; set; } = true;
        public float minimumSlideSpeed { get; set; } = 0.35f;
        public float intensity { get; set; } = 2.07f;
        public float updateRate { get; set; } = 41.61f;
        public bool impactBursts { get; set; } = true;
        public float impactAmount { get; set; } = 1f;
        public float sparkSize { get; set; } = 3f;
        public float sparkLifetime { get; set; } = 2f;
        public float sparkSpeed { get; set; } = 3f;
        public float trailSeconds { get; set; } = 0.57f;
        public bool chingEnabled { get; set; } = true;
        public float chingVolume { get; set; } = 0.45f;
        public float chingPitch { get; set; } = 1f;
        public bool recordInReplay { get; set; } = true;
    }

    public class BikeOnlyStanceSettings
    {
        public bool enabled { get; set; } = true;
        // Distinguishes old configs whose default was accidentally false from a user who
        // intentionally disables the feature after this migration has run.
        public int bikeOnlyStanceVersion { get; set; } = 1;
        public bool goofy { get; set; }
        public bool holdLeftStickToSwitchStance { get; set; } = true;
        // Restores the pre-3.2.4 rider/bike motion mirror so the game can perform
        // opposite-stance tricks. Disabled keeps the strictly feet-only mode.
        public bool useOppoTrickCompatibility { get; set; }
    }

    public class TrickAnimationDebugSettings
    {
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
        public bool showCenterOfMassVisual { get; set; } = false;
        public bool showLiveCenterOfMassVisual { get; set; } = false;
        public float centerOfMassVisualScale { get; set; } = 0.18f;
        public bool debugPoseApplyLogging { get; set; } = false;
    }

    public static class Config
    {
        // Static instances of structs
        public static Physics physics = new Physics
        {
            disableLevelInAir = false,
            manualMovement = false,
            spinAssist = true,
            spinCompletionAssist = false,
            spinFlipFix = false,
            driftAbility = true,
            lastVehicle = 0,
            gravity = 12.5f,
            smallHopForce = 4.2f,
            pumpForce = 1.5f,
            steerDamp = 5.0f,
            manualAngle = 30f,
            noseManualAngle = 30f,
            noseManualTurnTuning = false,
            noseManualDebugLogging = false,
            noseManualChassisComForwardOffset = 0f,
            noseManualChassisComVerticalOffset = 0f,
            noseManualDriverComForwardOffset = 0f,
            noseManualDriverComVerticalOffset = 0f,
            noseManualComTurnLean = 0f,
            noseManualDriverInertiaMultiplier = 1f,
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
            disableBoneBreaking = false,
            boneBreakingStrength = 1f,
            droneMass = 10f,
            droneBodyToggle = true,
            droneEmitterToggle = true,
            showPlayerUserNameTargets = true,
            menuAccentR = 1f,
            menuAccentG = 0.54f,
            menuAccentB = 0.30f,
            menuScale = 1f,
            menuDesignVersion = 1,
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
        public static CameraSettings cameraSettings = new CameraSettings();
        public static GraphicsSettings graphicsSettings = new GraphicsSettings();
        public static ReplaySettings replaySettings = new ReplaySettings();
        public static PegSparksSettings pegSparksSettings = new PegSparksSettings();
        public static BikeOnlyStanceSettings bikeOnlyStanceSettings = new BikeOnlyStanceSettings();
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
            public CameraSettings cameraSettingsData { get; set; }
            public GraphicsSettings graphicsSettingsData { get; set; }
            public ReplaySettings replaySettingsData { get; set; }
            public PegSparksSettings pegSparksSettingsData { get; set; }
            public BikeOnlyStanceSettings bikeOnlyStanceSettingsData { get; set; }
            public TrickAnimationDebugSettings trickAnimationDebugSettingsData { get; set; }
            public bool disclaimerAccepted { get; set; }
            public bool autoSkipIntro { get; set; }
        }

        public static string modFolder = Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "RoweMod");
        public static string cfgFile { get; } = Path.Combine(modFolder, "cfg.json");
        private const float DeferredSaveDelaySeconds = 2f;
        private static bool _deferredSavePending;
        private static bool _deferredSaveRoutineRunning;
        private static float _deferredSaveNotBefore;
        private static bool _configDirty;

        public static void MarkDirty()
        {
            _configDirty = true;
        }

        public static void RequestSave()
        {
            _configDirty = true;
            _deferredSavePending = true;
            _deferredSaveNotBefore =
                Time.realtimeSinceStartup + DeferredSaveDelaySeconds;

            if (_deferredSaveRoutineRunning)
                return;

            _deferredSaveRoutineRunning = true;
            try
            {
                MelonCoroutines.Start(DeferredSaveRoutine());
            }
            catch
            {
                _deferredSaveRoutineRunning = false;
                Save();
            }
        }

        public static void FlushPendingSave()
        {
            if (!_deferredSavePending && !_configDirty)
                return;

            _deferredSavePending = false;
            Save();
        }

        private static IEnumerator DeferredSaveRoutine()
        {
            while (_deferredSavePending)
            {
                while (_deferredSavePending &&
                       Time.realtimeSinceStartup < _deferredSaveNotBefore)
                {
                    yield return null;
                }

                if (!_deferredSavePending)
                    break;

                _deferredSavePending = false;
                Save();
            }

            _deferredSaveRoutineRunning = false;
        }

        // Save configuration to JSON file
        public static void Save()
        {
            try
            {
                _deferredSavePending = false;
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
                    cameraSettingsData = cameraSettings,
                    graphicsSettingsData = graphicsSettings,
                    replaySettingsData = replaySettings,
                    pegSparksSettingsData = pegSparksSettings,
                    bikeOnlyStanceSettingsData = bikeOnlyStanceSettings,
                    trickAnimationDebugSettingsData = trickAnimationDebugSettings,
                    disclaimerAccepted = disclaimerAccepted,
                    autoSkipIntro = autoSkipIntro
                }, Formatting.Indented);

                File.WriteAllText(cfgFile, contents);
                _configDirty = false;
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
            bool hasBoneBreakingStrength =
                jsonContent.IndexOf("\"boneBreakingStrength\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasMenuScale =
                jsonContent.IndexOf("\"menuScale\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasMenuDesignVersion =
                jsonContent.IndexOf("\"menuDesignVersion\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasNoseManualComTuning =
                jsonContent.IndexOf("\"noseManualDriverInertiaMultiplier\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasAutoSkipIntro =
                jsonContent.IndexOf("\"autoSkipIntro\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasChallengeRuntimeSettings =
                jsonContent.IndexOf("\"challengeRuntimeSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasManualCatchSettings =
                jsonContent.IndexOf("\"manualCatchSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasCameraSettings =
                jsonContent.IndexOf("\"cameraSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasGraphicsSettings =
                jsonContent.IndexOf("\"graphicsSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasReplaySettings =
                jsonContent.IndexOf("\"replaySettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasReplayCameraLabVersion =
                jsonContent.IndexOf("\"cameraLabVersion\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasPegSparksSettings =
                jsonContent.IndexOf("\"pegSparksSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasBikeOnlyStanceSettings =
                jsonContent.IndexOf("\"bikeOnlyStanceSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasBikeOnlyStanceVersion =
                jsonContent.IndexOf("\"bikeOnlyStanceVersion\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasTrickAnimationDebugSettings =
                jsonContent.IndexOf("\"trickAnimationDebugSettingsData\"", StringComparison.OrdinalIgnoreCase) >= 0;
            ConfigData jsonData = JsonConvert.DeserializeObject<ConfigData>(jsonContent);
            disclaimerAccepted = jsonData.disclaimerAccepted;
            autoSkipIntro = !hasAutoSkipIntro || jsonData.autoSkipIntro;

            // Assign values from JSON, preserving defaults if fields are missing
            physics = jsonData.physicsData;
            if (!hasNoseManualComTuning)
            {
                physics.noseManualChassisComForwardOffset = 0f;
                physics.noseManualChassisComVerticalOffset = 0f;
                physics.noseManualDriverComForwardOffset = 0f;
                physics.noseManualDriverComVerticalOffset = 0f;
                physics.noseManualComTurnLean = 0f;
                physics.noseManualDriverInertiaMultiplier = 1f;
            }
            else
            {
                physics.noseManualChassisComForwardOffset = Math.Max(-1f, Math.Min(1f, physics.noseManualChassisComForwardOffset));
                physics.noseManualChassisComVerticalOffset = Math.Max(-1f, Math.Min(1f, physics.noseManualChassisComVerticalOffset));
                physics.noseManualDriverComForwardOffset = Math.Max(-1f, Math.Min(1f, physics.noseManualDriverComForwardOffset));
                physics.noseManualDriverComVerticalOffset = Math.Max(-1f, Math.Min(1f, physics.noseManualDriverComVerticalOffset));
                physics.noseManualComTurnLean = Math.Max(-0.5f, Math.Min(0.5f, physics.noseManualComTurnLean));
                physics.noseManualDriverInertiaMultiplier = Math.Max(0.25f, Math.Min(3f, physics.noseManualDriverInertiaMultiplier));
            }
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
            if (!hasBoneBreakingStrength)
            {
                misc.boneBreakingStrength = 1f;
            }
            else
            {
                misc.boneBreakingStrength = Math.Max(0.25f, Math.Min(5f, misc.boneBreakingStrength));
            }
            if (!hasMenuScale || !float.IsFinite(misc.menuScale))
                misc.menuScale = 1f;
            else
                misc.menuScale = Math.Max(0.8f, Math.Min(1.35f, misc.menuScale));

            if (!hasMenuDesignVersion)
            {
                // Preserve custom accents, but migrate RoweMod's previous stock orange to the
                // more readable warm orange used by the new interface system.
                if (Math.Abs(misc.menuAccentR - 1f) < 0.001f &&
                    Math.Abs(misc.menuAccentG - 0.38f) < 0.001f &&
                    Math.Abs(misc.menuAccentB - 0.19f) < 0.001f)
                {
                    misc.menuAccentR = 1f;
                    misc.menuAccentG = 0.54f;
                    misc.menuAccentB = 0.30f;
                }

                misc.menuDesignVersion = 1;
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
            cameraSettings = jsonData.cameraSettingsData ?? new CameraSettings();
            graphicsSettings = jsonData.graphicsSettingsData ?? new GraphicsSettings();
            NormalizeGraphicsSettings(graphicsSettings);
            replaySettings = jsonData.replaySettingsData ?? new ReplaySettings();
            if (!hasReplayCameraLabVersion)
            {
                // Camera Lab is visually neutral when migrating an existing config.
                replaySettings.cameraLabVersion = 1;
                replaySettings.replayFramingMode = 0;
                replaySettings.activeReplayLensPreset = string.Empty;
            }
            NormalizeReplaySettings(replaySettings);
            pegSparksSettings = jsonData.pegSparksSettingsData ?? new PegSparksSettings();
            NormalizePegSparksSettings(pegSparksSettings);
            bikeOnlyStanceSettings =
                jsonData.bikeOnlyStanceSettingsData ?? new BikeOnlyStanceSettings();
            bool migratedBikeOnlyStanceEnabled = false;
            if (!hasBikeOnlyStanceVersion)
            {
                bikeOnlyStanceSettings.enabled = true;
                bikeOnlyStanceSettings.bikeOnlyStanceVersion = 1;
                migratedBikeOnlyStanceEnabled = true;
                Log.Msg("[BikeOnlyStance] Migrated bike-only stance to enabled; hold LS switching restored.");
            }
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
            }
            if (string.IsNullOrWhiteSpace(updaterSettings.manifestUrl))
            {
                updaterSettings.manifestUrl = new UpdaterSettings().manifestUrl;
            }

            if (grindPoseData.poses == null)
            {
                grindPoseData.poses = new Dictionary<string, GrindPoseConfigEntry>();
            }

            if (grindPoseData.centerOfMassVisualScale <= 0f)
            {
                grindPoseData.centerOfMassVisualScale = 0.18f;
            }

            //set new config variables to defaults if 0
            if (physics.bmxForceFactor <= 0f) physics.bmxForceFactor = 0.07f;
            if (physics.bmxMaxSpeed <= 0f) physics.bmxMaxSpeed = 7.5f;
            if (physics.grindPoseLerpSpeed <= 0f) physics.grindPoseLerpSpeed = 2f;
            if (motorTuning == null) motorTuning = new Dictionary<string, MotorTuningConfigEntry>();

            if (!hasChallengeRuntimeSettings || !hasManualCatchSettings || !hasCameraSettings || !hasGraphicsSettings || !hasReplaySettings ||
                !hasReplayCameraLabVersion ||
                !hasPegSparksSettings ||
                !hasBikeOnlyStanceSettings ||
                migratedBikeOnlyStanceEnabled ||
                !hasTrickAnimationDebugSettings ||
                !hasMenuScale || !hasMenuDesignVersion)
            {
                Save();
            }
            
            Log.Msg($"Config loaded successfully.");
        }

        public static void NormalizeReplaySettings(ReplaySettings settings)
        {
            if (settings == null)
                return;

            if (settings.cameraLabVersion < 2)
                settings.replayFisheye *= 100f;
            if (settings.cameraLabVersion < 4)
                settings.replayVignette *= 100f;

            settings.cameraLabVersion = 5;
            settings.replayFov = ClampFinite(settings.replayFov, 5f, 140f, 60f);
            settings.replayTilt = ClampFinite(settings.replayTilt, -180f, 180f, 0f);
            settings.replayFisheye = ClampFinite(settings.replayFisheye, 0f, 100f, 0f);
            settings.replayFisheyeXMultiplier = ClampFinite(settings.replayFisheyeXMultiplier, 0f, 1f, 1f);
            settings.replayFisheyeYMultiplier = ClampFinite(settings.replayFisheyeYMultiplier, 0f, 1f, 1f);
            settings.replayFisheyeCenterX = ClampFinite(settings.replayFisheyeCenterX, -0.5f, 0.5f, 0f);
            settings.replayFisheyeCenterY = ClampFinite(settings.replayFisheyeCenterY, -0.5f, 0.5f, 0f);
            settings.replayFisheyeScale = ClampFinite(settings.replayFisheyeScale, 0.5f, 2f, 1f);
            settings.replayMk1PaniniDistance = ClampFinite(settings.replayMk1PaniniDistance, 0f, 1f, 0.25f);
            settings.replayMk1PaniniCrop = ClampFinite(settings.replayMk1PaniniCrop, 0f, 1f, 0.65f);
            settings.replayMk1ChromaticAberration = ClampFinite(settings.replayMk1ChromaticAberration, 0f, 1f, 0.07f);
            settings.replayMk1FilmGrain = ClampFinite(settings.replayMk1FilmGrain, 0f, 1f, 0.06f);
            settings.replayVignette = ClampFinite(settings.replayVignette, 0f, 100f, 5f);
            settings.replayShakeMode = Math.Max(0, Math.Min(3, settings.replayShakeMode));
            settings.replayNearFocusStart = ClampFinite(settings.replayNearFocusStart, 0f, 1000f, 0f);
            settings.replayNearFocusEnd = ClampFinite(settings.replayNearFocusEnd, 0f, 1000f, 2f);
            settings.replayFarFocusStart = ClampFinite(settings.replayFarFocusStart, 0f, 1000f, 8f);
            settings.replayFarFocusEnd = ClampFinite(settings.replayFarFocusEnd, 0f, 1000f, 20f);
            if (settings.replayNearFocusEnd < settings.replayNearFocusStart)
                settings.replayNearFocusEnd = settings.replayNearFocusStart;
            if (settings.replayFarFocusStart < settings.replayNearFocusEnd)
                settings.replayFarFocusStart = settings.replayNearFocusEnd;
            if (settings.replayFarFocusEnd < settings.replayFarFocusStart)
                settings.replayFarFocusEnd = settings.replayFarFocusStart;
            settings.replayFramingMode = Math.Max(0, Math.Min(2, settings.replayFramingMode));
            settings.replayMatteOpacity = Clamp01OrDefault(settings.replayMatteOpacity, 1f);
            settings.activeReplayLensPreset ??= string.Empty;
            settings.cameraLightIntensity = ClampFinite(settings.cameraLightIntensity, 0f, 100f, 20f);
            settings.cameraLightRange = ClampFinite(settings.cameraLightRange, 1f, 50f, 14f);
            settings.cameraLightSpotAngle = ClampFinite(settings.cameraLightSpotAngle, 10f, 179f, 75f);
            settings.cameraLightColorR = Clamp01OrDefault(settings.cameraLightColorR, 1f);
            settings.cameraLightColorG = Clamp01OrDefault(settings.cameraLightColorG, 0.95f);
            settings.cameraLightColorB = Clamp01OrDefault(settings.cameraLightColorB, 0.88f);
            settings.cameraLightType = Math.Max(0, Math.Min(1, settings.cameraLightType));
            settings.cameraLightOffsetX = ClampFinite(settings.cameraLightOffsetX, -10f, 10f, 0f);
            settings.cameraLightOffsetY = ClampFinite(settings.cameraLightOffsetY, -10f, 10f, 0f);
            settings.cameraLightOffsetZ = ClampFinite(settings.cameraLightOffsetZ, -10f, 10f, 0.03f);
            settings.cameraLightPitch = ClampFinite(settings.cameraLightPitch, -180f, 180f, 0f);
            settings.cameraLightYaw = ClampFinite(settings.cameraLightYaw, -180f, 180f, 0f);
            settings.cameraLightRoll = ClampFinite(settings.cameraLightRoll, -180f, 180f, 0f);
            settings.cameraLightShadowResolution = Math.Max(0, Math.Min(4, settings.cameraLightShadowResolution));
            settings.cameraLightShadowStrength = Clamp01OrDefault(settings.cameraLightShadowStrength, 1f);
            settings.cameraLightShadowBias = ClampFinite(settings.cameraLightShadowBias, 0f, 2f, 0.05f);
            settings.cameraLightShadowNormalBias = ClampFinite(settings.cameraLightShadowNormalBias, 0f, 3f, 0.4f);
            settings.cameraLightShadowNearPlane = ClampFinite(settings.cameraLightShadowNearPlane, 0.01f, 10f, 0.2f);
        }

        public static void NormalizeGraphicsSettings(GraphicsSettings settings)
        {
            if (settings == null)
                return;

            settings.performancePreset = Math.Max(0, Math.Min(4, settings.performancePreset));
            settings.renderScale = ClampFinite(settings.renderScale, 0.5f, 1f, 1f);
            settings.shadowDistance = ClampFinite(settings.shadowDistance, 0f, 500f, 80f);
            settings.lodBias = ClampFinite(settings.lodBias, 0.25f, 2f, 1f);
            settings.textureMipmapLimit = Math.Max(0, Math.Min(3, settings.textureMipmapLimit));
            settings.antiAliasingMode = Math.Max(0, Math.Min(3, settings.antiAliasingMode));
            settings.terrainDetailDensity = ClampFinite(settings.terrainDetailDensity, 0f, 1f, 1f);
            settings.terrainDetailDistance = ClampFinite(settings.terrainDetailDistance, 0f, 500f, 80f);
            settings.skySelection = string.IsNullOrWhiteSpace(settings.skySelection)
                ? "Map"
                : settings.skySelection.Trim();
            if (!string.Equals(settings.skySelection, "Map", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.skySelection, "Clear", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.skySelection, "Sunset", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.skySelection, "Night", StringComparison.OrdinalIgnoreCase))
                settings.skySelection = "Map";
            settings.skyRotation = ClampFinite(settings.skyRotation, 0f, 360f, 0f);
            settings.skyExposure = ClampFinite(settings.skyExposure, -16f, 16f, 0f);
            settings.exposureCompensation = ClampFinite(settings.exposureCompensation, -16f, 16f, 0f);
            settings.fixedExposure = ClampFinite(settings.fixedExposure, -16f, 16f, 0f);

            settings.sceneExposureSettings ??= new Dictionary<string, SceneExposureSettings>();
            foreach (string key in settings.sceneExposureSettings.Keys.ToArray())
            {
                SceneExposureSettings sceneSettings = settings.sceneExposureSettings[key];
                if (string.IsNullOrWhiteSpace(key) || sceneSettings == null)
                {
                    settings.sceneExposureSettings.Remove(key);
                    continue;
                }

                sceneSettings.sceneName = string.IsNullOrWhiteSpace(sceneSettings.sceneName)
                    ? key
                    : sceneSettings.sceneName.Trim();
                sceneSettings.exposureCompensation = ClampFinite(
                    sceneSettings.exposureCompensation,
                    -16f,
                    16f,
                    0f);
                sceneSettings.fixedExposure = ClampFinite(
                    sceneSettings.fixedExposure,
                    -16f,
                    16f,
                    0f);
            }
        }

        public static void NormalizePegSparksSettings(PegSparksSettings settings)
        {
            if (settings == null)
                return;

            settings.minimumSlideSpeed = ClampFinite(settings.minimumSlideSpeed, 0.1f, 20f, 0.35f);
            settings.intensity = ClampFinite(settings.intensity, 0f, 3f, 2.07f);
            settings.updateRate = ClampFinite(settings.updateRate, 10f, 60f, 41.61f);
            settings.impactAmount = ClampFinite(settings.impactAmount, 0f, 3f, 1f);
            settings.sparkSize = ClampFinite(settings.sparkSize, 0.25f, 3f, 3f);
            settings.sparkLifetime = ClampFinite(settings.sparkLifetime, 0.25f, 2f, 2f);
            settings.sparkSpeed = ClampFinite(settings.sparkSpeed, 0.25f, 3f, 3f);
            settings.trailSeconds = ClampFinite(settings.trailSeconds, 0.05f, 1f, 0.57f);
            settings.chingVolume = ClampFinite(settings.chingVolume, 0f, 1f, 0.45f);
            settings.chingPitch = ClampFinite(settings.chingPitch, 0.5f, 1.75f, 1f);
        }

        private static float ClampFinite(float value, float min, float max, float defaultValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return defaultValue;

            return Math.Max(min, Math.Min(max, value));
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
                spinCompletionAssist = false,
                spinFlipFix = false,
                driftAbility = true,
                lastVehicle = 0,
                gravity = 12.5f,
                smallHopForce = 4.2f,
                pumpForce = 1.5f,
                steerDamp = 5.0f,
                manualAngle = 30f,
                noseManualAngle = 30f,
                noseManualTurnTuning = false,
                noseManualDebugLogging = false,
                noseManualChassisComForwardOffset = 0f,
                noseManualChassisComVerticalOffset = 0f,
                noseManualDriverComForwardOffset = 0f,
                noseManualDriverComVerticalOffset = 0f,
                noseManualComTurnLean = 0f,
                noseManualDriverInertiaMultiplier = 1f,
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
                disableBoneBreaking = false,
                boneBreakingStrength = 1f,
                droneMass = 10f,
                droneBodyToggle = true,
                droneEmitterToggle = true,
                showPlayerUserNameTargets = showPlayerUserNameTargets,
                menuAccentR = 1f,
                menuAccentG = 0.54f,
                menuAccentB = 0.30f,
                menuScale = 1f,
                menuDesignVersion = 1,
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
                poses = new Dictionary<string, GrindPoseConfigEntry>(),
                showCenterOfMassVisual = false,
                showLiveCenterOfMassVisual = false,
                centerOfMassVisualScale = 0.18f,
                debugPoseApplyLogging = false
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

        public static void ResetCameraTab()
        {
            cameraSettings = new CameraSettings();
        }

        public static void ResetGraphicsSettings()
        {
            graphicsSettings = new GraphicsSettings();
        }

        public static void ResetReplayTab()
        {
            replaySettings = new ReplaySettings();
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
