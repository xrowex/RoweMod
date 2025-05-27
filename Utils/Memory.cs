using Il2CppCinemachine;
using Il2CppMashBox.Addons.CharacterController;
using Il2CppMashBox.Addons.ContentManagment;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test;
using static rowemod.Config;
using UnityEngine;
using Il2CppMashBox.Addons.SlowMotionSystem;
using Il2CppMashBox.Addons.ProtoDrone; // Correct namespace for DroneController
using UnityEngine.Events;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using Il2CppMashBox.Character;
using Il2CppMashBox.Character.Scripts;
using Il2CppMashBox.Core.Runtime.Audio;
using Il2CppMashBox.Core.Runtime.Camera;
using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using Il2CppMashBox.Core.Runtime.Events;
using Il2CppMashBox.Core.Runtime.Physics;
using Il2CppMashBox.Core.Runtime.Spawning;
using Il2CppMashBox.Development.RandD.PlayFabTesting;
using Il2CppMashBox.Development.RandD.Vehicle_Force_Pull;
using MelonLoader;
using rowemod.Mods;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;
using Il2CppMashBox.Development.RandD.IAP;
using PlayerInputBehaviour = Il2CppMashBox.BMX_Physics_Development.NewShit.PlayerInputBehaviour;
using Il2CppMashBox.Addons.ReplaySystem;

namespace rowemod.Utils
{
    public static class Memory
    {
        // --- If this is ever false the mod will terminate --- //
        public static bool bFoundAllObjects = true;

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

        // --- HUD --- //
        public static Test mashBucksHUD;

        // --- CAMERA --- //
        public static Camera roweCam;
        public static BMXCMCameraTarget camTarget;
        public static CinemachineVirtualCamera virtualCam;
        public static CinemachineTransposer camTranspose;
        public static CinemachineBrain gameplayCameraBrain;
        public static Vector3 fpvOffset;
        public static Vector3 fpvRotation;
        public static CameraData cameraData;
        
        // --- Third person camera data --- //
        public static ThirdPersonCamera tpCamera;
        public static CameraData tpCameraData;
        public static CinemachineVirtualCamera tpVirtualCam;
        
        // --- Character Settings --- //
        public static CharacterData roweCharacterData;
        public static CharacterDataDictionary roweCharacterDataDictionary;
        public static CharacterManager roweCharacterManager;
        
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
        public static Rigidbody droneRb;
        public static GameObject droneBody;
        public static MeshRenderer[] droneMeshRenderers;
        public static List<MeshRenderer> allDroneMeshRenderers = new List<MeshRenderer>();
        public static EquipSlotVehicle[] equipSlots;
        public static Joint[] joints;
        public static PhysicsBasedEventEmitter[] droneEmitters;
        public static CustomizableEntity customizableEntity;
        public static PhysicsSeatEventRelay physicsSeatEventRelay;
        public static PlayerInputBehaviour playerInputBehaviour;
        public static MGCharacterController mgCharacterController;
        
        // Game Events
        public static GameEvent playerSpawned;

        public static TeleportRelay teleportRelay;
        public static PhysicsPropHandBehaviour physicsPropHandBehaviour;

        // Added for FreeCam collider toggle feature
        public static FreeCam freeCam;
        public static SphereCollider freeCamCollider;

        // Added for Drone collider toggle feature: Store all colliders from all drones
        public static List<Collider> droneColliders = new List<Collider>();

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
            gameplayCameraBrain = GameObject.FindObjectsOfType<CinemachineBrain>()
                .FirstOrDefault(brain => brain != null && brain.gameObject.name.Contains("Gameplay Camera"));

            // HUD shit
            var testInstances = Resources.FindObjectsOfTypeAll<Test>();
            if (testInstances != null && testInstances.Length > 0)
            {
                foreach (Test instance in testInstances)
                {
                    if (instance.name != "Fart") // lol (should be only 2 instances, the objects name were loookin for is random)
                    {
                        mashBucksHUD = instance;
                    }
                }
            }

            // Find specific components inside rMBCharacter instead of using GameObject.Find()
            if (rMbCharacter != null)
            {
                try
                {
                    Log.Msg("Starting to find components under rMBCharacter...");
                    CharacterManager characterManager = rMbCharacter.GetComponentInChildren<CharacterManager>();
                    
                    playerInputBehaviour = rMbCharacter.GetComponentInChildren<PlayerInputBehaviour>();
                    //physicsPropHandBehaviour = characterManager._physicsPropHandBehaviour;

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

                    customizableEntity = rMbCharacter.GetComponentInChildren<CustomizableEntity>();
                    if (customizableEntity != null)
                        Log.Msg($"Found customizable entity: {customizableEntity.name}");
                    else
                        Log.Error("CustomizableEntity is null!");

                    spinSystem = rMbCharacter.GetComponentInChildren<SpinSystem>();
                    if (spinSystem != null)
                        Log.Msg("SpinSystem component found under rMBCharacter.");
                    else
                        Log.Error("SpinSystem component not found under rMBCharacter.");

                    vehicleController = rMbCharacter.GetComponentInChildren<VehicleController>();
                    if (vehicleController != null)
                    {
                        vehicleController._airSpinAssist = bSpinAssist;
                        vehicleController._mannyAngle = manualAngle;
                        vehicleController._noseyAngle = noseManualAngle;
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

                    driftAbility = rMbCharacter.GetComponentInChildren<DriftAbility>();
                    if (driftAbility != null)
                        Log.Msg("DriftAbility component found in BMXChassis.");
                    else
                        Log.Error("DriftAbility component not found in BMXChassis.");

                    joints = rMbCharacter.GetComponentsInChildren<Joint>(true);
                    if (joints.Length > 0)
                        Log.Msg($"Found {joints.Length} Joint components in BMXChassis.");
                    else
                        Log.Error("No Joint components found in BMXChassis.");

                    roweTimeInterpolator = rMbCharacter.GetComponentInChildren<TimeInterpolator>();
                    if (roweTimeInterpolator != null)
                        Log.Msg("TimeInterpolator component found in Time Interpolator.");
                    else
                        Log.Error("TimeInterpolator component not found in Time Interpolator.");

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
                catch (Exception ex)
                {
                    Log.Error($"Exception while finding components: {ex.Message}");
                }
            }
            else
            {
                Log.Error("rMBCharacter is null! Cannot search within it.");
            }
            
            // Hop heights
            vehicleSettingsInstances = Resources.FindObjectsOfTypeAll<MotorVehicleSettings>();
            Log.Msg(vehicleSettingsInstances != null && vehicleSettingsInstances.Length > 0
                ? $"Found {vehicleSettingsInstances.Length} MotorVehicleSettings instances."
                : "No MotorVehicleSettings instances found.");

            try
            {
                Log.Msg("Starting to find all drones...");

                // First try: Get DroneController components under rMBCharacter
                var dronesComponents = rMbCharacter != null
                    ? rMbCharacter.GetComponentsInChildren<Il2CppMashBox.Addons.ProtoDrone.DroneController>(true)
                    : Array.Empty<Il2CppMashBox.Addons.ProtoDrone.DroneController>();

                // Fallback: Search scene-wide if none found under rMBCharacter
                if (dronesComponents == null || dronesComponents.Length == 0)
                {
                    Log.Warning("No DroneController components found under rMBCharacter. Attempting scene-wide search...");
                    dronesComponents = GameObject.FindObjectsOfType<Il2CppMashBox.Addons.ProtoDrone.DroneController>(true);
                }

                if (dronesComponents != null && dronesComponents.Length > 0)
                {
                    Log.Msg($"Found {dronesComponents.Length} DroneController components.");

                    // Store all drone GameObjects in the drones array
                    drones = dronesComponents.Select(d => d.gameObject).ToArray();
                    for (int i = 0; i < drones.Length; i++)
                    {
                        var droneGameObject = drones[i];
                        Log.Msg($"Processing drone [{i}]: {droneGameObject.name} (Active: {droneGameObject.activeInHierarchy}, Path: {GetGameObjectPath(droneGameObject)})");

                        // Get MeshRenderers for the drone
                        droneMeshRenderers = droneGameObject.GetComponentsInChildren<MeshRenderer>(true);
                        Log.Msg(droneMeshRenderers.Length > 0
                            ? $"Drone '{droneGameObject.name}' has {droneMeshRenderers.Length} MeshRenderer components."
                            : $"Drone '{droneGameObject.name}' has no MeshRenderer components.");
                        
                        if (droneMeshRenderers.Length > 0)
                        {
                            allDroneMeshRenderers.AddRange(droneMeshRenderers);
                        }

                        // Get Rigidbody for the drone
                        droneRb = droneGameObject.GetComponentInChildren<Rigidbody>();
                        if (droneRb != null)
                            Log.Msg($"Drone '{droneGameObject.name}' has a Rigidbody component.");
                        else
                            Log.Warning($"Drone '{droneGameObject.name}' does not have a Rigidbody component.");
                            
                        // Store a reference to the PhysicsBasedEventEmitter
                        droneEmitters = droneGameObject.GetComponentsInChildren<PhysicsBasedEventEmitter>();
                        
                        
                        TempDroneCycler[] tempDroneCyclers = droneGameObject.GetComponentsInChildren<TempDroneCycler>();
                        if (tempDroneCyclers != null && tempDroneCyclers.Length > 0)
                        {
                            foreach (var cycler in tempDroneCyclers)
                            {
                                var cyclerColliders = cycler.GetComponentsInChildren<Collider>();
                                foreach (var collider in cyclerColliders)
                                {
                                    collider.enabled = bDisableDroneCollider;
                                    droneColliders.Add(collider);
                                }
                            }
                            Log.Msg($"Found and configured {tempDroneCyclers.Length} TempDroneCycler components");
                        }
                        
                        // Added for Drone collider toggle feature: Find all Colliders on this drone
                        var colliders = droneGameObject.GetComponentsInChildren<Collider>(true);
                        if (colliders.Length > 0)
                        {
                            Log.Msg($"Found {colliders.Length} Collider components on drone '{droneGameObject.name}':");
                            foreach (var collider in colliders)
                            {
                                Log.Msg($"  - Collider on: {collider.gameObject.name} (Type: {collider.GetType().Name}, Enabled: {collider.enabled})");
                                // Reversed logic: true enables colliders, false disables
                                collider.enabled = bDisableDroneCollider;
                                droneColliders.Add(collider);
                            }
                        }
                        else
                        {
                            Log.Warning($"No Collider components found on drone: {droneGameObject.name}");
                            // Log child hierarchy for debugging
                            Log.Msg($"Child hierarchy for drone '{droneGameObject.name}':");
                            foreach (Transform child in droneGameObject.GetComponentsInChildren<Transform>(true))
                            {
                                var collider = child.GetComponent<Collider>();
                                Log.Msg($"  - {child.name} (Active: {child.gameObject.activeInHierarchy}, Collider: {(collider != null ? collider.GetType().Name : "None")})");
                            }
                        }
                    }
                }
                else
                {
                    Log.Warning("No DroneController components found in scene.");
                }
                Log.Msg($"Total MeshRenderer components across all drones: {allDroneMeshRenderers.Count}");
                Log.Msg($"Total Collider components found across all drones: {droneColliders.Count}");
            }
            catch (Exception ex)
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
            catch (Exception ex)
            {
                Log.Error($"Exception while finding vehicle changer: {ex.Message}");
            }

            // Helmet
            try
            {
                Log.Msg("Starting to find helmet...");
                helmet = rMbCharacter.transform.FindDeepChild("Extreme_Helmet_with_Goggles")?.gameObject;
                Log.Msg(helmet != null
                    ? "Helmet GameObject found under Human Temp."
                    : "Helmet GameObject not found under Human Temp.");
            }
            catch (Exception ex)
            {
                Log.Error($"Exception while finding helmet: {ex.Message}");
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
            catch (Exception ex)
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
            catch (Exception ex)
            {
                Log.Error($"Exception while finding Haptic Feedback Manager: {ex.Message}");
            }

            // Added for FreeCam collider toggle feature
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
                        freeCamCollider.enabled = !bDisableFreeCamCollider; // Apply initial state
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
            catch (Exception ex)
            {
                Log.Error($"Exception while finding FreeCam or child SphereCollider: {ex.Message}");
            }
            
            Log.Msg("Running GrabTrickData()");
            TrickMods.GrabTrickData();
            
            // Delayed bike materials load to bypass shop load
            MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
            
            //BikeMaterialsLoader.ApplySavedMaterialsOnSceneLoad();
            Custom.LoadPreset(lastLoadedPresetCharacter);
            
            Mods.Physics.Update();
            //Mods.Misc.Update();
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
        public static List<string> prefabNames = new List<string>();
        public static string bundlesFolderPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Mods\\rowemod\\Bundles");
        public static AssetBundle loadedBundle;
        public static List<AssetBundle> loadedBundles = new List<AssetBundle>();
        public static GameObject roweSpokes, roweBars;
        public static GameObject newSessionMarker;
        private static Dictionary<GameObject, string> prefabToBundleMap = new();

        public static void LoadAllAssetBundles()
        {
            string rootPath = Path.GetDirectoryName(Application.dataPath);
            
            Log.Msg($"Looking for AssetBundles in: {bundlesFolderPath}");
            
            string[] bundleFiles = Directory.GetFiles(bundlesFolderPath);
            if (bundleFiles.Length == 0)
            {
                Log.Msg("No files found in the mods folder.");
                return;
            }
            
            // Clear existing lists to avoid duplicates
            
            prefabList.Clear();
            sessionMarkers.Clear();
            loadedBundles.Clear();
            foreach (string bundlePath in bundleFiles)
            {
                Log.Msg($"Attempting to load AssetBundle from: {bundlePath}");
                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Log.Msg($"Failed to load AssetBundle from: {bundlePath}. Skipping file.");
                    continue;
                }
            
                loadedBundles.Add(bundle);
                Log.Msg($"Successfully loaded AssetBundle from: {bundlePath}");
            
                string[] assetNames = bundle.GetAllAssetNames();
            foreach (var assetName in assetNames)
            {
                Log.Msg($"Found asset in bundle: {assetName}");
                
                if (assetName.EndsWith(".prefab"))
                {
                    GameObject asset = bundle.LoadAsset<GameObject>(assetName);
                    if (asset != null)
                    {
                        if (asset.name.ToLower().Contains("bar"))
                        {
                            prefabToBundleMap[asset] = Path.GetFileName(bundlePath); 
                        }
                        prefabList.Add(asset);
                        Log.Msg($"[Prefabs] Loaded prefab: {asset.name}");
                        if (asset.name.IndexOf("marker", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            sessionMarkers.Add(asset);
                            Log.Msg($"Added Marker prefab: {asset.name}");
            
                            // Check if this is the last saved marker
                            if (!string.IsNullOrEmpty(Config.customSessionMarker) && asset.name == Config.customSessionMarker)
                            {
                                newSessionMarker = asset;
                                Log.Msg($"Loaded custom session marker from config: {newSessionMarker.name}");
                            }
                        }
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
            }
        }
        
        public static void ReloadAssetsFromCachedBundles()
        {
            Log.Msg("Reloading assets from cached AssetBundles...");

            // Clear existing lists to avoid duplicates
            prefabList.Clear();
            sessionMarkers.Clear();
            barListInitialized = false;
            frameListInitialized = false;

            foreach (AssetBundle bundle in loadedBundles)
            {
                if (bundle == null)
                {
                    Log.Warning("Found null AssetBundle in cache, skipping.");
                    continue;
                }
                string[] assetNames = bundle.GetAllAssetNames();
                foreach (var assetName in assetNames)
                {
                    Log.Msg($"Processing cached asset: {assetName}");

                    if (assetName.EndsWith(".prefab"))
                    {
                        GameObject asset = bundle.LoadAsset<GameObject>(assetName);
                        if (asset != null)
                        {
                            prefabList.Add(asset);
                            Log.Msg($"[Prefabs] Loaded prefab: {asset.name}");
                            if (asset.name.Contains("Marker"))
                            {
                                sessionMarkers.Add(asset);
                                Log.Msg($"Added Marker prefab from cache: {asset.name}");

                                // Check if this is the last saved marker
                                if (!string.IsNullOrEmpty(Config.customSessionMarker) && asset.name == Config.customSessionMarker)
                                {
                                    newSessionMarker = asset;
                                    Log.Msg($"Loaded custom session marker from cache: {newSessionMarker.name}");
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
            }

            // Reapply session marker if needed
            if (!string.IsNullOrEmpty(Config.customSessionMarker))
            {
                GameObject savedMarker = sessionMarkers
                    .FirstOrDefault(marker => marker != null && marker.name == Config.customSessionMarker);
                if (savedMarker != null)
                {
                    ReplaceSessionMarkerWithPrefab(savedMarker);
                    Log.Msg($"Reapplied saved session marker: {Config.customSessionMarker}");
                }
                else
                {
                    Log.Warning($"Saved session marker '{Config.customSessionMarker}' not found in cached assets.");
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
                        Config.customSessionMarker = selectedMarker.name;
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

        private static int selectedBarIndex = 0;
        private static bool barListInitialized = false;
        private static string[] barNames;
        private static List<GameObject> barPrefabs = new();
        private static Vector2 barScrollPos = Vector2.zero;

        public static void DrawBmxBarsSelector()
        {
            if (!barListInitialized || barPrefabs.Count == 0)
            {
                Log.Msg("[Bars] (Re)building bar prefab list...");
                barPrefabs = Memory.prefabList
                    .Where(p => p != null && p.name.ToLower().Contains("bar"))
                    .ToList();

                //barNames = barPrefabs.Select(p => p.name).ToArray();
                barNames = barPrefabs.Select(p =>
                {
                    string bundleName = prefabToBundleMap.TryGetValue(p, out var bundle) ? bundle : "UnknownBundle";
                    return $"{p.name} ({bundleName})";
                }).ToArray();
                barListInitialized = true;

                Log.Msg($"[Bars] Found {barPrefabs.Count} bar prefabs.");
            }

            GUILayout.Label("Select BMX Bars:");

            if (barNames == null || barNames.Length == 0)
            {
                GUILayout.Label("No bar prefabs found.");
                Log.Warning("[Bars] barNames array is empty.");
                return;
            }

            barScrollPos = GUILayout.BeginScrollView(barScrollPos, GUILayout.Height(150));
            for (int i = 0; i < barNames.Length; i++)
            {
                if (Menu.ModernButton(barNames[i], 300f))
                {
                    selectedBarIndex = i;
                    TryReplaceBars(barPrefabs[selectedBarIndex]);
                    PartTweaker.FindParts();
                    Log.Msg($"[Bars] Selected bar index: {selectedBarIndex}, name: {barNames[i]}");
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(10);
        }

        private static void TryReplaceBars(GameObject newBarsPrefab)
        {
            try
            {
                if (Memory.rMbCharacter == null)
                {
                    Log.Error("[Bars] rMbCharacter is null.");
                    return;
                }

                Log.Msg("[Bars] Searching for Bars EquipSlot...");
                var frontBars = rMbCharacter.transform
                    .FindDeepChild("Bars")
                    ?.gameObject.GetComponent<EquipSlotVehicle>();

                if (frontBars == null)
                {
                    Log.Warning("[Bars] Couldn't find current Bars EquipSlot.");
                    return;
                }

                Log.Msg($"[Bars] Found Bars EquipSlot, instantiating: {newBarsPrefab.name}");
                frontBars.InstantiateItem(newBarsPrefab);
                frontBars.GetComponent<Anchor>()?.SnapToAnchor();

                Log.Msg($"[Bars] Successfully switched bars to: {newBarsPrefab.name}");
                Config.bikeMaterials.Remove("Bars");
            }
            catch (Exception ex)
            {
                Log.Error($"[Bars] Error swapping bars: {ex.Message}");
            }
        }

        private static int selectedFrameIndex = 0;
        private static bool frameListInitialized = false;
        private static string[] frameNames;
        private static List<GameObject> framePrefabs = new();
        private static Vector2 frameScrollPos = Vector2.zero;

        public static void DrawBmxFramesSelector()
        {
            if (!frameListInitialized || framePrefabs.Count == 0)
            {
                Log.Msg("[Frames] (Re)building frame prefab list...");
                framePrefabs = Memory.prefabList
                    .Where(p => p != null && p.name.ToLower().Contains("frame"))
                    .ToList();

                frameNames = framePrefabs.Select(p => p.name).ToArray();
                frameListInitialized = true;

                Log.Msg($"[Frames] Found {framePrefabs.Count} frame prefabs.");
            }

            GUILayout.Space(15);
            GUILayout.Label("Select BMX Frame:");

            if (frameNames == null || frameNames.Length == 0)
            {
                GUILayout.Label("No frame prefabs found.");
                Log.Warning("[Frames] frameNames array is empty.");
                return;
            }

            frameScrollPos = GUILayout.BeginScrollView(frameScrollPos, GUILayout.Height(150));
            for (int i = 0; i < frameNames.Length; i++)
            {
                if (Menu.ModernButton(frameNames[i], 150f))
                {
                    selectedFrameIndex = i;
                    Log.Msg($"[Frames] Selected frame index: {selectedFrameIndex}, name: {frameNames[i]}");

                    // Apply frame immediately on selection
                    TryReplaceFrame(framePrefabs[selectedFrameIndex]);
                    PartTweaker.FindParts();
                }
            }
            GUILayout.EndScrollView();

        }


        private static void TryReplaceFrame(GameObject newFramePrefab)
        {
            try
            {
                if (Memory.rMbCharacter == null)
                {
                    Log.Error("[Frames] rMbCharacter is null.");
                    return;
                }

                Log.Msg("[Frames] Searching for Frame GameObject with EquipSlotVehicle...");

                var frameObj = rMbCharacter.transform
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.ToLower() == "frame" && t.GetComponent<EquipSlotVehicle>() != null);

                if (frameObj == null)
                {
                    Log.Warning("[Frames] Could not find a 'Frame' GameObject with EquipSlotVehicle.");
                    return;
                }

                var frameSlot = frameObj.GetComponent<EquipSlotVehicle>();
                if (frameSlot == null)
                {
                    Log.Warning("[Frames] 'Frame' exists, but EquipSlotVehicle is missing.");
                    return;
                }

                Log.Msg($"[Frames] Found Frame EquipSlot. Replacing with: {newFramePrefab.name}");
                frameSlot.InstantiateItem(newFramePrefab);
                frameSlot.GetComponent<Anchor>()?.SnapToAnchor();

                Log.Msg($"[Frames] Successfully replaced frame with: {newFramePrefab.name}");
                Config.bikeMaterials.Remove("Frame");
            }
            catch (Exception ex)
            {
                Log.Error($"[Frames] Exception while replacing frame: {ex.Message}");
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

        public static bool GetComponent<T>(ref GameObject obj, ref T component)
        {
            if (obj != null)
            {
                component = obj.GetComponent<T>();
                if (component == null)
                {
                    Log.Error($"Failed to find component of type {typeof(T).Name} on GameObject: {obj.name}");
                    return false;
                }
            }
            else
            {
                Log.Error("Provided GameObject is null, cannot retrieve component.");
                return false;
            }

            return true;
        }

        public static bool GetComponents<T>(ref GameObject obj, ref T[] components)
        {
            if (obj != null)
            {
                components = obj.GetComponents<T>();
                if (components == null || components.Length == 0)
                {
                    Log.Error($"Failed to find any components of type {typeof(T).Name} on GameObject: {obj.name}");
                    return false;
                }
            }
            else
            {
                Log.Error("Provided GameObject is null, cannot retrieve components.");
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
    }
}