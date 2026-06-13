using Il2CppCinemachine;
using Il2CppFusion;
using Il2CppMashBox.Addons.CharacterController;
using Il2CppMashBox.Addons.ContentManagment;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation.BeyondMeat;
using Il2CppMashBox.Addons.ProtoDrone;
using Il2CppMashBox.Addons.ReplaySystem;
using Il2CppMashBox.Addons.SlowMotionSystem;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.ActivityTracking;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System;
using Il2CppMashBox.Character;
using Il2CppMashBox.Character.Scripts;
using Il2CppMashBox.Core.Runtime.Audio;
using Il2CppMashBox.Core.Runtime.Camera;
using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using Il2CppMashBox.Core.Runtime.Events;
using Il2CppMashBox.Core.Runtime.Gameplay.ActivityTracking;
using Il2CppMashBox.Core.Runtime.Physics;
using Il2CppMashBox.Core.Runtime.Physics.Skidmarks;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using Il2CppMashBox.Core.Runtime.Spawning;
using Il2CppMashBox.Development.Anim_Follow.Physics_Driven_Animation;
using Il2CppMashBox.Development.RandD.IAP;
using Il2CppMashBox.Development.RandD.PlayFabTesting;
using Il2CppMashBox.Development.RandD.Vehicle_Force_Pull;
using MelonLoader;
using rowemod.Mods;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Il2CppMashBox.Addons.SessionMarker;
using static rowemod.Config;
using ActiveRagdollBehaviour = Il2CppMashBox.Addons.PhysicsDrivenAnimation.ActiveRagdollBehaviour;
using Camera = UnityEngine.Camera;
using EquipSlot = Il2CppMashBox.Addons.ContentManagment.EquipSlot;
using Object = UnityEngine.Object;
using PlayerInputBehaviour = Il2CppMashBox.BMX_Physics_Development.NewShit.PlayerInputBehaviour;

namespace rowemod.Utils
{
    public static class Memory
    {

        // References
        public static GameObject rMbCharacter;
        public static GameObject instructionCanvas;
        public static GameObject hapticFeedBack;
        public static GameObject[] drones;
        public static GameObject helmet;
        public static GameObject bmxCameraTarget;
        public static GameObject sportsVehicleCamera;
        public static GameObject physicsDrivenCharacter;
        public static GameObject rPhysicsSkeleton;
        public static GameObject menuPlayer;
        public static GameObject gamePlayer;

        public static Transform activeMarkerTransform;
        // HUD
        public static Test mashBucksHUD;
        
        //Custom models
        public static GameObject lastEquippedBars;
        public static GameObject lastEquippedFrame;
        public static GameObject lastEquippedStem;
        
        // Camera
        public static Camera roweCam;
        public static BMXCMCameraTarget camTarget;
        public static CinemachineVirtualCamera virtualCam;
        public static CinemachineTransposer camTranspose;
        public static CinemachineBrain gameplayCameraBrain;
        public static Vector3 fpvOffset;
        public static Vector3 fpvRotation;
        public static CameraData cameraData;

        // Third person camera data
        public static ThirdPersonCamera tpCamera;
        public static CameraData tpCameraData;
        public static CinemachineVirtualCamera tpVirtualCam;

        // Character Settings
        public static CharacterData roweCharacterData;
        public static CustomCharacterManager roweCharacterManager;
        public static VehicleController vehicleController;
        public static MotorVehicleSettings[] vehicleSettingsInstances;
        public static VehicleBalancePID vehicleBalance;
        public static BMXCollisionHandler collisionHandler;
        public static TestVehicleChanger vehicleChanger;
        public static CenterOfMassBehaviour rCenterOfMassBehaviour;
        public static Character theCharacter;
        public static TimeInterpolator roweTimeInterpolator;
        public static SpinSystem spinSystem;
        public static PumpSystem pumpSystem;
        public static DriftAbility driftAbility;
        public static Rigidbody chassisRb;
        public static Rigidbody springBody;
        public static Rigidbody droneRb;
        public static GameObject droneBody;
        public static MeshRenderer[] droneMeshRenderers;
        public static List<MeshRenderer> allDroneMeshRenderers = new List<MeshRenderer>();
        public static EquipSlotVehicle[] equipSlots;
        public static Joint[] joints;
        public static PhysicsBasedEventEmitter[] droneEmitters;
        public static CustomizableEntity customizableEntity;
        public static GameObject playersBike;
        public static PhysicsSeatEventRelay physicsSeatEventRelay;
        public static PlayerInputBehaviour playerInputBehaviour;
        public static MGCharacterController mgCharacterController;
        public static ActiveRagdollBehaviour activeRagdollBehaviour;
        public static AirControlSettings airControlSettings;
        public static BikeGrindPoser bikeGrindPoser;
        public static AnimatedVehicleEventResponder animatedVehicleEventResponder;

        // Game Events
        public static GameEvent playerSpawned;
        public static TeleportRelay teleportRelay;
        public static PhysicsPropHandBehaviour physicsPropHandBehaviour;
        
        public static FreeCam freeCam;
        public static SphereCollider freeCamCollider;
        // Drone Colliders

        public static List<Collider> droneColliders = new List<Collider>();

        //8bitt challenge
        public static ActivityTracker eightbittActivityTracker;
        public static PlayerTrickGameplay playerTrickGameplay;
        
        //mulltiplayer shit
        public static NetworkHuman localNetworkHuman;
        public static NetworkHuman[] networkHumans;
        public static NetworkPlayer localNetworkPlayer;
        public static NetworkPlayer[] networkPlayers;
        public static NetworkObject networkObject;
        private static Il2CppSystem.Collections.Generic.List<GameObject> localVisuals;
        public static Dictionary<NetworkString<_32>, NetworkPlayer> playerNameDictionary = new Dictionary<NetworkString<_32>, NetworkPlayer>();
        public static Dictionary<NetworkString<_32>, EquipSlot> playerSlotsDictionary = new Dictionary<NetworkString<_32>, EquipSlot>();
        public static object LocalCustomCharacterData { get; set; }

        //skidmarks
        public static SkidmarkManager skidmarkManager;
        
        //GRIND MAGNET
        public static GrindMagnetZEM grindMagnetZEM;

        //bones
        public static BeyondMeatSystem beyondMeatSystem;
        public static void FindObjects(GameObject player)
        {
            Log.Msg("FindObjects called...");

            if (player != null)
            {
                Log.Msg($"Player GameObject: {player.name}");
                physicsDrivenCharacter = player;

                var driven = player.transform.FindDeepChild("Physics Driven Character");
                if (driven != null)
                {
                    physicsDrivenCharacter = driven.gameObject;
                    Log.Msg("physicsDrivenCharacter set.");
                }
                else
                {
                    Log.Warning("FindObjects: 'Physics Driven Character' not found. Using player GameObject as fallback.");
                    physicsDrivenCharacter = player;
                }
            }
            
            //Active Ragdoll
            if(physicsDrivenCharacter!=null)
            {
                activeRagdollBehaviour = physicsDrivenCharacter.GetComponent<ActiveRagdollBehaviour>();
                if(activeRagdollBehaviour != null)
                    Log.Msg("ActiveRagdollBehaviour component found.");
                else
                    Log.Error("ActiveRagdollBehaviour component not found on physicsDrivenCharacter.");
            }
            
            gameplayCameraBrain = GameObject.FindObjectsOfType<CinemachineBrain>()
                .FirstOrDefault(brain => brain != null && brain.gameObject.name.Contains("Gameplay Camera"));

            
            skidmarkManager = GameObject.FindObjectOfType<SkidmarkManager>();
            if (skidmarkManager != null)
                Log.Msg("SkidmarkManager component found.");
            else
                Log.Error("SkidmarkManager component not found.");
            
            beyondMeatSystem = GameObject.FindObjectOfType<BeyondMeatSystem>();
            if(beyondMeatSystem != null)
                Log.Msg("BeyondMeatSystem component found.");
            else
                Log.Error("BeyondMeatSystem component not found.");

            // Find specific components inside rMBCharacter instead of using GameObject.Find()
            if (rMbCharacter != null)
            {
                try
                {
                    Log.Msg("Starting to find components under rMBCharacter...");
                    CharacterManager characterManager = rMbCharacter.GetComponentInChildren<CharacterManager>();

                    playerInputBehaviour = rMbCharacter.GetComponentInChildren<PlayerInputBehaviour>();

                    equipSlots = rMbCharacter.GetComponentsInChildren<EquipSlotVehicle>(true);
                    if (equipSlots.Length > 0)
                        Log.Msg($"Found {equipSlots.Length} EquipSlotVehicle components.");
                    else
                        Log.Error("No EquipSlotVehicle components found under rMBCharacter.");

                    theCharacter = rMbCharacter.GetComponentInChildren<Character>();
                    if (theCharacter != null)
                        Log.Msg($"Found character component: {theCharacter.name}");
                    else
                        Log.Error("Character component not found under rMBCharacter.");
                    
                    roweCharacterManager = rMbCharacter.GetComponentInChildren<CustomCharacterManager>();
                    if (roweCharacterManager != null)
                    {
                        Log.Msg("CharacterManager component found under rMBCharacter.");
                    }
                    else
                    {
                        Log.Error("CharacterManager component not found under rMBCharacter.");
                    }
                    
                    customizableEntity = rMbCharacter.GetComponentInChildren<CustomizableEntity>();
                    if (customizableEntity != null)
                    {
                        Log.Msg($"Found customizable entity: {customizableEntity.name}");
                        playersBike = customizableEntity.gameObject;
                    }
                    else
                        Log.Error("CustomizableEntity is null!");

                    spinSystem = rMbCharacter.GetComponentInChildren<SpinSystem>();
                    if (spinSystem != null)
                    {
                        airControlSettings = spinSystem._airControlSettings;
                        Log.Msg("SpinSystem component found under rMBCharacter.");
                    }
                        
                    else
                        Log.Error("SpinSystem component not found under rMBCharacter.");

                    vehicleController = rMbCharacter.GetComponentInChildren<VehicleController>();
                    if (vehicleController != null)
                    {
                        vehicleController._airSpinAssist = Config.physics.spinAssist;;
                        vehicleController._mannyAngle = Config.physics.manualAngle;
                        vehicleController._noseyAngle = Config.physics.noseManualAngle;
                        Log.Msg("VehicleController component found.");
                    }
                    else
                    {
                        Log.Error("VehicleController component not found in BMXChassis.");
                    }

                    rCenterOfMassBehaviour = rMbCharacter.GetComponentInChildren<CenterOfMassBehaviour>();
                    if (rCenterOfMassBehaviour != null)
                        Log.Msg("rCenterOfMassBehaviour component found in BMXChassis.");
                    else
                        Log.Error("rCenterOfMassBehaviour component not found in BMXChassis.");

                    collisionHandler = rMbCharacter.GetComponentInChildren<BMXCollisionHandler>();
                    if (collisionHandler != null)
                        Log.Msg("BMXCollisionHandler component found in BMXChassis.");
                    else
                        Log.Error("BMXCollisionHandler component not found in BMXChassis.");

                    pumpSystem = rMbCharacter.GetComponentInChildren<PumpSystem>();
                    if (pumpSystem != null)
                        Log.Msg("PumpSystem component found in BMXChassis.");
                    else
                        Log.Error("PumpSystem component not found in BMXChassis.");

                    vehicleBalance = rMbCharacter.GetComponentInChildren<VehicleBalancePID>();
                    if (vehicleBalance != null) 
                        Log.Msg("VehicleBalancePID component found in BMXChassis.");
                    else
                        Log.Error("VehicleBalancePID component not found in BMXChassis.");

                    chassisRb = rMbCharacter.GetComponentInChildren<Rigidbody>();
                    if (chassisRb != null)
                        Log.Msg("Rigidbody component found in BMXChassis.");
                    else
                        Log.Error("Rigidbody component not found in BMXChassis.");

                    bikeGrindPoser = rMbCharacter.GetComponentInChildren<BikeGrindPoser>();
                    if(bikeGrindPoser != null)
                        Log.Msg("BikeGrindPoser component found in BMXChassis.");
                    else
                        Log.Error("BikeGrindPoser component not found in BMXChassis.");

                    animatedVehicleEventResponder = null;
                    if (bikeGrindPoser != null)
                    {
                        animatedVehicleEventResponder = bikeGrindPoser.gameObject.GetComponent<AnimatedVehicleEventResponder>();
                    }

                    if (animatedVehicleEventResponder != null)
                        Log.Msg("AnimatedVehicleEventResponder component found on the BikeGrindPoser GameObject.");
                    else
                        Log.Error("AnimatedVehicleEventResponder component not found on the BikeGrindPoser GameObject.");
                    
                    driftAbility = rMbCharacter.GetComponentInChildren<DriftAbility>();
                    if (driftAbility != null)
                        Log.Msg("DriftAbility component found in BMXChassis.");
                    else
                        Log.Error("DriftAbility component not found in BMXChassis.");
                    
                    springBody = driftAbility.transform.FindChild("Spring Body")
                        .GetComponent<Rigidbody>();
                    if(springBody != null)
                        Log.Msg("Rigidbody component found in rMbCharacter parent (Spring Body)");
                    else
                        Log.Error("Rigidbody component found in rMbCharacter parent (Spring Body)");
                    
                    vehicleSettingsInstances = Resources.FindObjectsOfTypeAll<MotorVehicleSettings>();
                    if (vehicleSettingsInstances != null && vehicleSettingsInstances.Length > 0)
                        Log.Msg($"Found {vehicleSettingsInstances.Length} MotorVehicleSettings instances.");
                    else
                        Log.Error("No MotorVehicleSettings instances found.");

                    theCharacter = rMbCharacter.GetComponentInChildren<Character>();
                    if (theCharacter != null)
                        Log.Msg("Character component found in Trick Launcher.");
                    else
                        Log.Error("Character component not found in Trick Launcher.");

                    Log.Msg("Completed finding components under rMBCharacter.");
                }
                catch (System.Exception ex)
                {
                    Log.Error($"Exception while finding components: {ex.Message}");
                }
            }
            else
            {
                Log.Error("rMBCharacter is null! Cannot search within it.");
            }

           
            try
            {
                Log.Msg("Starting to find all drones...");
                RefreshDroneComponents();
                Log.Msg("Initial drone component refresh completed.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while finding drones: {ex.Message}");
            }

            // Find vehicle changer
            try
            {
                Log.Msg("Starting to find vehicle changer...");
                vehicleChanger = rMbCharacter.GetComponentInChildren<TestVehicleChanger>();
                Log.Msg(vehicleChanger != null
                    ? "TestVehicleChanger component found in Vehicle Changer."
                    : "TestVehicleChanger component not found in Vehicle Changer.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while finding vehicle changer: {ex.Message}");
            }

            // Find camera components
            try
            {
                Log.Msg("Starting to find camera components...");

                camTarget = GameObject.FindObjectOfType<BMXCMCameraTarget>();
                if (camTarget != null)
                {
                    Log.Msg("BMXCMCameraTarget component found in BMX Camera Target.");
                }
                else
                {
                    Log.Error("BMXCMCameraTarget component not found in BMX Camera Target.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while finding camera components: {ex.Message}");
            }

            // Third person camera
            tpCamera = rMbCharacter.GetComponentInChildren<ThirdPersonCamera>();
            if (tpCamera != null)
            {
                Log.Msg("ThirdPersonCamera component found in Mash Box Character.");

                // Find all CameraData objects and assign the one with "BMX" in the name to tpCameraData
                CameraData[] allCameraData = Resources.FindObjectsOfTypeAll<CameraData>();
                foreach (var cd in allCameraData)
                {
                    if (cd.name.Contains("ProtoVehicle"))
                    {
                        tpCameraData = cd;
                        break;
                    }
                }
                if (tpCameraData != null)
                {
                    Log.Msg($"tpCameraData component found with name: {tpCameraData.name}");
                }
                else
                {
                    Log.Error("tpCameraData component with 'BMX' in the name not found.");
                }

                tpVirtualCam = tpCamera.GetComponentInChildren<CinemachineVirtualCamera>();
                if (tpVirtualCam != null)
                {
                    Log.Msg("tpVirtualCam component found in ThirdPersonCamera.");
                }
                else
                {
                    Log.Error("tpVirtualCam component not found in ThirdPersonCamera.");
                }
            }
            else
            {
                Log.Error("ThirdPersonCamera component not found in BMX Camera Target.");
            }

            // Find Haptic Feedback Manager
            try
            {
                Log.Msg("Starting to find Haptic Feedback Manager...");
                if (GetGameObject("Haptic Feedback Manager(Clone)", ref hapticFeedBack))
                {
                    Log.Msg("Haptic Feedback Manager found.");
                }
                else
                {
                    Log.Warning("Haptic Feedback Manager not found.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while finding Haptic Feedback Manager: {ex.Message}");
            }

            try
            {
                Log.Msg("Starting to find eightbittActivityTracker...");
                //eightbittActivityTracker = rMbCharacter.GetComponentInChildren<ActivityTracker>(true);
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while finding eightbittActivityTracker: {ex.Message}");
            }

            try
            {
                Log.Msg("Starting to find PlayerTrickGameplay...");
                playerTrickGameplay = rMbCharacter.GetComponentInChildren<PlayerTrickGameplay>(true);
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while finding PlayerTrickGameplay: {ex.Message}");
            }

            try
            {
                Log.Msg("Starting to find SessionMarker...");
                activeMarkerTransform = rMbCharacter.GetComponentInChildren<SessionMarker>(true).Root;
                if (activeMarkerTransform != null)
                {
                    Log.Msg("SessionMarker component found.");
                }
                else
                {
                    Log.Warning("SessionMarker component not found.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception while finding SessionMarker.");
            }


            // FreeCam collider toggle
            try
            {
                Log.Msg("Starting to find FreeCam and its child SphereCollider...");
                freeCam = GameObject.FindObjectOfType<FreeCam>(true);
                if (freeCam != null)
                {
                    Log.Msg($"FreeCam component found: {freeCam.name}");
                    freeCamCollider = freeCam.GetComponentInChildren<SphereCollider>(true);
                    if (freeCamCollider != null)
                    {
                        Log.Msg($"FreeCam child SphereCollider found on: {freeCamCollider.gameObject.name}");
                        freeCamCollider.enabled = !Config.misc.disableFreeCamCollider;
                    }
                    else
                    {
                        Log.Warning("No SphereCollider found on FreeCam's child GameObjects.");
                    }
                }
                else
                {
                    Log.Warning("FreeCam component not found in scene.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while finding FreeCam or child SphereCollider: {ex.Message}");
            }

            Log.Msg("Running GrabTrickData()");
            TrickMods.GrabTrickData();

            Mods.Physics.Update();
            
            // Initializing ObjectDropper
            ObjectDropper.Initialize();
        }

        public static void RemoveSkidmarks()
        {
            if(skidmarkManager == null) return;
            skidmarkManager.Start();
        }
        // New: Refresh drone components when toggles are used
        public static void RefreshDroneComponents()
        {
            try
            {
                Log.Msg("Refreshing drone components via DroneController...");

                // Clear existing collections
                droneColliders.Clear();
                allDroneMeshRenderers.Clear();
                drones = System.Array.Empty<GameObject>();
                droneEmitters = System.Array.Empty<PhysicsBasedEventEmitter>();
                droneRb = null;

                // Find all DroneController components
                var droneControllers = GameObject.FindObjectsOfType<DroneController>(true);
                Log.Msg($"Found {droneControllers?.Length ?? 0} DroneController components in scene.");

                if (droneControllers != null && droneControllers.Length > 0)
                {
                    // Select active drones (prefer active GameObjects)
                    var activeDrones = droneControllers
                        .Where(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy)
                        .Select(c => c.gameObject)
                        .Distinct()
                        .ToList();

                    if (activeDrones.Count == 0)
                    {
                        Log.Warning("No active DroneController GameObjects found, including inactive drones.");

                        activeDrones = droneControllers
                            .Where(c => c != null && c.gameObject != null)
                            .Select(c => c.gameObject)
                            .Distinct()
                            .ToList();
                    }

                    drones = activeDrones.ToArray();
                    Log.Msg($"Total unique drones found: {drones.Length}");

                    // Process the first active drone (or first available)
                    if (drones.Length > 0)
                    {
                        var drone = drones[0];
                        Log.Msg($"Processing primary drone: {drone.name} (Active: {drone.activeInHierarchy}, Path: {GetGameObjectPath(drone)})");

                        // Get MeshRenderers
                        droneMeshRenderers = drone.GetComponentsInChildren<MeshRenderer>(true);
                        if (droneMeshRenderers.Length > 0)
                        {
                            allDroneMeshRenderers.AddRange(droneMeshRenderers.Where(r => r != null));
                            Log.Msg($"Found {droneMeshRenderers.Length} MeshRenderer components on drone {drone.name}.");
                        }
                        else
                        {
                            Log.Warning($"No MeshRenderer components found on drone {drone.name}.");
                        }

                        // Get PhysicsBasedEventEmitters
                        droneEmitters = drone.GetComponentsInChildren<PhysicsBasedEventEmitter>(true);
                        if (droneEmitters.Length > 0)
                        {
                            Log.Msg($"Found {droneEmitters.Length} PhysicsBasedEventEmitter components on drone {drone.name}.");
                        }
                        else
                        {
                            Log.Warning($"No PhysicsBasedEventEmitter components found on drone {drone.name}.");
                        }

                        // Get Colliders
                        var colliders = drone.GetComponentsInChildren<Collider>(true);
                        if (colliders.Length > 0)
                        {
                            Log.Msg($"Found {colliders.Length} Collider components on drone {drone.name}:");
                            foreach (var collider in colliders)
                            {
                                if (collider != null)
                                {
                                    Log.Msg($"  - Collider on: {collider.gameObject.name} (Type: {collider.GetType().Name}, Enabled: {collider.enabled})");
                                    collider.enabled = Config.misc.disableDroneCollider;
                                    droneColliders.Add(collider);
                                }
                            }
                        }
                        else
                        {
                            Log.Warning($"No Collider components found on drone {drone.name}.");
                        }

                        // Get Rigidbody
                        droneRb = drone.GetComponentInChildren<Rigidbody>(true);
                        if (droneRb != null)
                        {
                            Log.Msg($"Found Rigidbody on drone {drone.name}.");
                        }
                        else
                        {
                            Log.Warning($"No Rigidbody found on drone {drone.name}.");
                        }
                    }

                    Log.Msg($"Total MeshRenderer components: {allDroneMeshRenderers.Count}");
                    Log.Msg($"Total PhysicsBasedEventEmitter components: {droneEmitters.Length}");
                    Log.Msg($"Total Collider components: {droneColliders.Count}");
                }
                else
                {
                    Log.Warning("No DroneController components found in scene.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception while refreshing drone components: {ex.Message}");
            }
        }

        // Helper method to get GameObject path for logging
        private static string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        // Prefab variables
        public static List<GameObject> prefabList = new List<GameObject>();
        public static List<GameObject> sessionMarkers = new List<GameObject>();
        public static List<GameObject> dropperPrefabs = new List<GameObject>();
        public static List<string> prefabNames = new List<string>();
        public static List<string> dropperPrefabNames = new List<string>();
        public static string bundlesFolderPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Mods\\rowemod\\Bundles");
        public static AssetBundle loadedBundle;
        public static List<AssetBundle> loadedBundles = new List<AssetBundle>();
        public static GameObject roweSpokes, roweBars;
        public static GameObject newSessionMarker;
        public static Dictionary<GameObject, string> prefabToBundleMap = new();

        public static void LoadAllAssetBundles()
        {
            Log.Msg($"Looking for AssetBundles in: {bundlesFolderPath}");

            string[] bundleFiles = Directory.GetFiles(bundlesFolderPath, "*", SearchOption.AllDirectories);

            if (bundleFiles.Length == 0)
            {
                Log.Msg("No files found in the mods folder.");
                return;
            }

            // Clear existing lists to avoid duplicates
            prefabList.Clear();
            sessionMarkers.Clear();
            dropperPrefabs.Clear();
            loadedBundles.Clear();
            prefabNames.Clear();
            dropperPrefabNames.Clear();

            foreach (string bundlePath in bundleFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(bundlePath).ToLower();
                //Log.Msg($"Attempting to load AssetBundle from: {bundlePath}");

                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Log.Msg($"Failed to load AssetBundle from: {bundlePath}. Skipping file.");
                    continue;
                }

                loadedBundles.Add(bundle);
                //Log.Msg($"Successfully loaded AssetBundle from: {bundlePath}");

                string[] assetNames = bundle.GetAllAssetNames();
                foreach (var assetName in assetNames)
                {
                    //Log.Msg($"Found asset in bundle: {assetName}");

                    // Handle marker prefabs
                    if (assetName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        GameObject asset = bundle.LoadAsset<GameObject>(assetName);
                        if (asset != null)
                        {
                            // Handle general prefabs
                            // Updated to check for "bars" prefix instead of "bar" substring
                            if (asset.name.ToLower().StartsWith("bars"))
                            {
                                prefabToBundleMap[asset] = Path.GetFileName(bundlePath);
                            }
                            prefabList.Add(asset);
                            //Log.Msg($"[Prefabs] Loaded prefab: {asset.name}");

                            // Handle session markers
                            if (asset.name.ToLower().StartsWith("marker"))
                            {
                                sessionMarkers.Add(asset);
                                Log.Msg($"Added Marker prefab: {asset.name}");

                                // Check if this is the last saved marker
                                if (!string.IsNullOrEmpty(Config.misc.customSessionMarker) && asset.name == Config.misc.customSessionMarker)
                                {
                                    newSessionMarker = asset;
                                    Log.Msg($"Loaded custom session marker from config: {newSessionMarker.name}");
                                }
                            }

                            // Handle dropper prefabs
                            if (fileName.ToLower().StartsWith("dropper_"))
                            {
                                string displayName = fileName.Substring("dropper_".Length);
                                if (!string.IsNullOrEmpty(displayName))
                                {
                                    dropperPrefabs.Add(asset);
                                    dropperPrefabNames.Add(displayName);
                                    //Log.Msg($"[Dropper] Loaded dropper prefab: {asset.name} (display name: {displayName})");
                                }
                                else
                                {
                                    Log.Warning($"Skipping dropper prefab {asset.name}: empty display name after removing 'dropper_' prefix.");
                                }
                            }
                        }
                        else
                        {
                            Log.Warning($"Failed to load prefab asset: {assetName}");
                        }
                    }
                }
            }

            if (loadedBundles.Count == 0)
            {
                Log.Msg("No valid AssetBundles were loaded.");
            }
            else
            {
                Log.Msg($"Successfully loaded {loadedBundles.Count} AssetBundles.");
                Log.Msg($"Loaded {sessionMarkers.Count} session marker prefabs.");
                Log.Msg($"Loaded {dropperPrefabs.Count} dropper prefabs.");
            }
        }

        public static void ReloadAssetsFromCachedBundles()
        {
            Log.Msg("Reloading assets from cached AssetBundles...");

            // Clear existing lists to avoid duplicates
            prefabList.Clear();
            sessionMarkers.Clear();
            dropperPrefabs.Clear();
            PartTweaker.barListInitialized = false;
            PartTweaker.frameListInitialized = false;
            PartTweaker.stemListInitialized = false;
            prefabNames.Clear();
            dropperPrefabNames.Clear();

            foreach (AssetBundle bundle in loadedBundles)
            {
                if (bundle == null)
                {
                    Log.Warning("Found null AssetBundle in cache, skipping.");
                    continue;
                }
                string[] assetNames = bundle.GetAllAssetNames();
                string bundlePath = bundle.name; // Note: bundle.name may not be reliable, using for logging
                string fileName = Path.GetFileNameWithoutExtension(bundlePath).ToLower();
                foreach (var assetName in assetNames)
                {
                    Log.Msg($"Processing cached asset: {assetName}");

                    // Handle marker prefabs
                    if (assetName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        GameObject asset = bundle.LoadAsset<GameObject>(assetName);
                        if (asset != null)
                        {
                            // Handle general prefabs
                            prefabList.Add(asset);
                            Log.Msg($"[Prefabs] Loaded prefab: {asset.name}");


                            // Handle session markers
                            if (asset.name.ToLower().StartsWith("marker"))
                            {
                                sessionMarkers.Add(asset);
                                Log.Msg($"Added Marker prefab from cache: {asset.name}");

                                // Check if this is the last saved marker
                                if (!string.IsNullOrEmpty(Config.misc.customSessionMarker) && asset.name == Config.misc.customSessionMarker)
                                {
                                    newSessionMarker = asset;
                                    Log.Msg($"Loaded custom session marker from cache: {newSessionMarker.name}");
                                }
                            }

                            // Handle dropper prefabs
                            if (fileName.StartsWith("dropper_"))
                            {
                                string displayName = fileName.Substring("dropper_".Length);
                                if (!string.IsNullOrEmpty(displayName))
                                {
                                    dropperPrefabs.Add(asset);
                                    dropperPrefabNames.Add(displayName);
                                    Log.Msg($"[Dropper] Loaded dropper prefab from cache: {asset.name} (display name: {displayName})");
                                }
                            }
                        }
                    }
                }
            }

            if (prefabList.Count == 0)
            {
                Log.Warning("No prefabs loaded from cached AssetBundles.");
            }
            else
            {
                Log.Msg($"Successfully reloaded {prefabList.Count} prefabs from cached AssetBundles.");
                Log.Msg($"Reloaded {sessionMarkers.Count} session marker prefabs.");
                Log.Msg($"Reloaded {dropperPrefabs.Count} dropper prefabs.");
            }

            // Reapply session marker if needed
            if (!string.IsNullOrEmpty(Config.misc.customSessionMarker))
            {
                GameObject savedMarker = sessionMarkers
                    .FirstOrDefault(marker => marker != null && marker.name == Config.misc.customSessionMarker);

                if (savedMarker != null)
                {
                    ReplaceSessionMarkerWithPrefab(savedMarker);
                    Log.Msg($"Reapplied saved session marker: {Config.misc.customSessionMarker}");
                }
                else
                {
                    Log.Warning($"Saved session marker '{Config.misc.customSessionMarker}' not found in cached assets.");
                }
            }
        }

        public static void ReplaceSessionMarkerWithPrefab(GameObject selectedMarker)
        {
            if (selectedMarker == null)
            {
                Log.Warning("Selected marker is null, cannot replace SessionMarker.");
                return;
            }

            if (rMbCharacter != null)
            {
                Log.Msg("Searching for SessionMarker...");
                Il2CppSystem.Object sessionMarkerObj = rMbCharacter.transform.FindDeepChild("Session Marker");

                if (sessionMarkerObj != null)
                {
                    Transform sessionMarkerTransform = sessionMarkerObj.TryCast<Transform>(); // Safe casting

                    if (sessionMarkerTransform != null)
                    {
                        Log.Msg("SessionMarker found.");

                        // Destroy existing marker children
                        foreach (Transform child in sessionMarkerTransform.GetComponentsInChildren<Transform>(true))
                        {
                            if (child != sessionMarkerTransform) // Prevent destroying the parent
                                Object.Destroy(child.gameObject);
                        }

                        // Instantiate the new marker
                        GameObject instantiatedMarker = GameObject.Instantiate(selectedMarker);
                        instantiatedMarker.transform.SetParent(sessionMarkerTransform, false);
                        instantiatedMarker.transform.localPosition = new Vector3(-0.5f, 0.0f, 0.0f);
                        instantiatedMarker.transform.localRotation = selectedMarker.transform.localRotation;
                        instantiatedMarker.transform.localScale = selectedMarker.transform.localScale;

                        Log.Msg($"Replaced SessionMarker with prefab: {selectedMarker.name}");

                        // Save the selected marker name to config and persist it
                        Config.misc.customSessionMarker = selectedMarker.name;
                        Config.Save();
                    }
                    else
                    {
                        Log.Warning("SessionMarker transform casting failed.");
                    }
                }
                else
                {
                    Log.Warning("SessionMarker not found.");
                }
            }
        }

        public static bool GetGameObject(string name, ref GameObject obj, bool bSometimesNull = false)
        {
            obj = GameObject.Find(name);
            if (obj == null)
            {
                if (!bSometimesNull)
                    Log.Error($"Failed to find GameObject with name: {name}");
                return false;
            }
            return true;
        }


        public static bool GetComponentsInChildren<T>(ref GameObject obj, ref T[] components, bool includeInactive = false)
        {
            if (obj != null)
            {
                components = obj.GetComponentsInChildren<T>(includeInactive);
                if (components == null || components.Length == 0)
                {
                    Log.Error($"Failed to find any components of type {typeof(T).Name} on GameObject: {obj.name} or its children.");
                    return false;
                }
            }
            else
            {
                Log.Error("Provided GameObject is null, cannot retrieve components in children.");
                return false;
            }
            return true;
        }
        public static System.Collections.IEnumerator DelayedLoadEquippedParts()
        {
            yield return new WaitForSeconds(2f); // Wait for bundles to finish loading

            Log.Msg("Attempting to load saved custom parts...");
            PartTweaker.LoadSavedBars();
            yield return new WaitForSeconds(0.5f);
            PartTweaker.LoadSavedFrame();
            yield return new WaitForSeconds(0.5f);
            PartTweaker.LoadSavedStem();
            
            
                
            yield return null;
        }

        public static void UpdateCharacters()
        {
            menuPlayer = FindObjectWithComponentInScene<CustomCharacterManager>("MainMenu");
            
            if (menuPlayer == null)
                Log.Error("MainMenu character not found!");
            
            gamePlayer = Object.FindObjectOfType<CustomCharacterManager>().gameObject;
            
            if(gamePlayer == null)
                Log.Error("GamePlayer character not found!");
        }
        public static GameObject FindObjectWithComponentInScene<T>(string sceneName) where T : Component
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"[SceneSearch] Scene '{sceneName}' is not loaded or invalid.");
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            foreach (GameObject root in roots)
            {
                // true = include inactive
                T comp = root.GetComponentInChildren<T>(true);
                if (comp != null)
                {
                    return comp.gameObject.transform.parent.gameObject;   // <- GameObject that holds the component
                }
            }

            return null;
        }
        public static void CopyPlayersBike()
        {
            if (playersBike == null)
            {
                Log.Error("CopyPlayersBike: playersBike is null!");
                return;
            }

            Log.Msg("Copying player's bike...");
            GameObject copiedBike = GameObject.Instantiate(playersBike);
            copiedBike.name = "Copied Bike";
            
            // Disable all scripts on the copied bike and its children
            var scripts = copiedBike.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var script in scripts)
            {
                if (script == null) continue;
                script.enabled = false;
            }

            // Make all rigidbodies kinematic
            var rigidbodies = copiedBike.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rigidbodies)
            {
                if (rb == null) continue;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Ensure the copied bike is inactive initially or positioned away
            copiedBike.SetActive(false);

            // Create a root object to adjust the origin to the bottom
            GameObject bikeRoot = new GameObject("Copied Bike Root");
            bikeRoot.SetActive(false);
            
            // Calculate bounds to find the bottom
            Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool boundsInitialized = false;
            var renderers = copiedBike.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!boundsInitialized)
                {
                    combinedBounds = renderer.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            if (boundsInitialized)
            {
                // Move the bike so its bottom center is at (0,0,0) relative to the root
                Vector3 bottomCenter = new Vector3(combinedBounds.center.x, combinedBounds.min.y, combinedBounds.center.z);
                copiedBike.transform.position -= bottomCenter;
                copiedBike.transform.SetParent(bikeRoot.transform, true);
                Log.Msg($"Adjusted origin for copied bike. Offset applied: {-bottomCenter}");
            }
            else
            {
                copiedBike.transform.SetParent(bikeRoot.transform, true);
                Log.Warning("Could not calculate bounds for copied bike; origin remains unchanged.");
            }
            
            // Add to dropper list
            if (!dropperPrefabNames.Contains("Copied Bike"))
            {
                dropperPrefabs.Add(bikeRoot);
                dropperPrefabNames.Add("Copied Bike");
                Log.Msg("Copied bike added to object dropper list.");
            }
            else
            {
                int index = dropperPrefabNames.IndexOf("Copied Bike");
                GameObject oldRoot = dropperPrefabs[index];
                dropperPrefabs[index] = bikeRoot;
                GameObject.Destroy(oldRoot);
                Log.Msg("Updated copied bike in object dropper list.");
            }
        }
    }
}
