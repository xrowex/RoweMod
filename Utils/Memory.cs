using Il2CppCinemachine;
using Il2CppMashBox.Addons.ContentManagment;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test;

using static rowemod.Config;

using UnityEngine;
using Il2CppMashBox.Addons.SlowMotionSystem;
using Il2CppMashBox.Addons.ProtoDrone;
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

        // --- HUD ---
        public static Test mashBucksHUD;

        // --- CAMERA ---
        public static Camera roweCam;
        public static BMXCMCameraTarget camTarget;
        public static CinemachineVirtualCamera virtualCam;
        public static CinemachineTransposer camTranspose;
        public static CinemachineBrain gameplayCameraBrain;
        public static Vector3 fpvOffset;
        public static Vector3 fpvRotation;
        public static CameraData cameraData;
        
        // --- Third person camera data ---
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
        public static DroneManager droneManager;
        public static PhysicsBasedEventEmitter[] droneEmitters;
        public static CustomizableEntity customizableEntity;
        public static PhysicsSeatEventRelay physicsSeatEventRelay;
        
        //Game Events
        public static GameEvent playerSpawned;

        public static TeleportRelay teleportRelay;
        public static PhysicsPropHandBehaviour physicsPropHandBehaviour;
        
        
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
            
            //Hop heights
            vehicleSettingsInstances = Resources.FindObjectsOfTypeAll<MotorVehicleSettings>();
            Log.Msg(vehicleSettingsInstances != null && vehicleSettingsInstances.Length > 0
                ? $"Found {vehicleSettingsInstances.Length} MotorVehicleSettings instances."
                : "No MotorVehicleSettings instances found.");

            try
            {
                Log.Msg("Starting to find all drones...");
    
                // Get all DroneController components under rMBCharacter
                var dronesComponents = rMbCharacter.GetComponentsInChildren<DroneController>(true);
    
                if (dronesComponents.Length > 0)
                {
                    Log.Msg($"Found {dronesComponents.Length} DroneController components.");

                    // Store all drone GameObjects in the drones array
                    drones = dronesComponents.Select(d => d.gameObject).ToArray();
                    foreach (var droneGameObject in drones)
                    {
                        Log.Msg($"Processing drone: {droneGameObject.name}");

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
                    }
                }
                else
                {
                    Log.Warning("No drones found in rMBCharacter.");
                }
                Log.Msg($"Total MeshRenderer components across all drones: {allDroneMeshRenderers.Count}");
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

            //helmet
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

                //virtualCam = GameObject.Find("Sports Vehicle Camera").GetComponent<CinemachineVirtualCamera>();
                /*virtualCam = rMbCharacter.transform.FindDeepChild("Sports Vehicle Camera").GetComponent<CinemachineVirtualCamera>();
                if (virtualCam != null)
                {
                    Log.Msg("virtualCam component found in Sports Vehicle Camera.");
                }
                else
                {
                    Log.Error("virtualCam component not found in Sports Vehicle Camera.");
                }

                camTranspose = virtualCam.GetComponentInChildren<CinemachineTransposer>();
                if (camTranspose != null)
                {
                    Log.Msg("camTranspose component found in Sports Vehicle Camera.");
                }
                else
                {
                    Log.Error("camTranspose component not found in Sports Vehicle Camera.");
                }
                */
            }
            catch (Exception ex)
            {
                Log.Error($"Exception while finding camera components: {ex.Message}");
            }
            
            //Third person camera
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
            
            
            
            
            
            
            
            
            Log.Msg("Running GrabTrickData()");
            TrickMods.GrabTrickData();
            
            
            
            //TransitionScannerDebugger.OnInitializeMelon();
            
            
            
            //Delayed bike materials load to bypass shop load
            MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
            
            //BikeMaterialsLoader.ApplySavedMaterialsOnSceneLoad();
            Custom.LoadPreset(lastLoadedPresetCharacter);
            
            
            

            
            Mods.Physics.Update();
            //Mods.Misc.Update();
            
            
        }

        /*public static void SetupCameraSeatRelay()
        {
            bmxCameraTarget = rMbCharacter.transform.FindDeepChild("BMX Camera Target")?.gameObject;
            sportsVehicleCamera = rMbCharacter.transform.FindDeepChild("Sports Vehicle Camera")?.gameObject;
            
                    //setup seat relay
                    physicsSeatEventRelay = rMbCharacter.GetComponentInChildren<PhysicsSeatEventRelay>();
                    // Convert your method to an IL2CPP-safe delegate
                    
                    if (physicsSeatEventRelay.OnAttached.GetPersistentEventCount() == 0 && physicsSeatEventRelay.OnDetached.GetPersistentEventCount() == 0)
                    {
                        UnityAction onAttachedAction = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnAttachedHandler);
                        UnityAction onDetachedAction = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnDetachedHandler);

                        // Register the event listeners
                        physicsSeatEventRelay.OnAttached.AddListener(onAttachedAction);
                        physicsSeatEventRelay.OnDetached.AddListener(onDetachedAction);
                        Log.Msg("OnAttached and OnDetached event listeners registered.");
                    }
            
                
                    // Handler methods
                    void OnAttachedHandler()
                    {
                        if (bUseOldCam)
                        {
                            sportsVehicleCamera.SetActive(true);
                            bmxCameraTarget.SetActive(true);
                            virtualCam.transform.gameObject.SetActive(true);

                            *//*Log.Msg(virtualCam.transform.gameObject.name + " is now active.");
                            Log.Msg("OnAttached event triggered!");*//*
                        }
                        else
                        {
                            sportsVehicleCamera.SetActive(false);
                            bmxCameraTarget.SetActive(false);
                            virtualCam.transform.gameObject.SetActive(false);
                
                            *//*Log.Msg(virtualCam.transform.gameObject.name + " is now inactive.");*//*
                        }
                    }

                    void OnDetachedHandler()
                    {
                        sportsVehicleCamera.SetActive(false);
                        bmxCameraTarget.SetActive(false);
                        virtualCam.transform.gameObject.SetActive(false);
                
                        *//*Log.Msg(virtualCam.transform.gameObject.name + " is now inactive.");
                        Log.Msg("OnDetached event triggered!");*//*
                    }
                
        }*/
        
        
        
        //prefab variables
        public static List<GameObject> prefabList = new List<GameObject>();
        public static List<GameObject> sessionMarkers = new List<GameObject>();
        public static List<string> prefabNames = new List<string>();
        public static string bundlesFolderPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Mods\\rowemod\\Bundles");
        public static AssetBundle loadedBundle;
        public static List<AssetBundle> loadedBundles = new List<AssetBundle>();
        public static GameObject roweSpokes, roweBars;
        public static GameObject newSessionMarker;
        
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
                            prefabList.Add(asset);
                            if (asset.name.Contains("Marker"))
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





        /*public static void ToggleBmxFrames()
        {
            if (rMBCharacter == null)
            {
                Log.Error("rMBCharacter is null. Cannot toggle BMX frames.");
                return;
            }
        
            try
            {
                Log.Msg("Searching for BMX frames...");
        
                // Locate BMX_Frame_Vanilla_Standard and disable it
                /*var bmxFrameVanilla = rMBCharacter.transform.FindDeepChild("BMX_Frame_Vanilla_Standard")?.gameObject;
                if (bmxFrameVanilla != null)
                {
                    bmxFrameVanilla.SetActive(false);
                    Log.Msg("Disabled BMX_Frame_Vanilla_Standard.");
                }
                else
                {
                    Log.Warning("BMX_Frame_Vanilla_Standard not found.");
                }
        
                // Locate BMX_Frame_NoBrand_Standard_Brakeless and enable it
                var bmxFrameNoBrand = rMBCharacter.transform.FindDeepChild("BMX_Frame_NoBrand_Standard_Brakeless")?.gameObject;
                if (bmxFrameNoBrand != null)
                {
                    bmxFrameNoBrand.SetActive(true);
                    Log.Msg("Enabled BMX_Frame_NoBrand_Standard_Brakeless.");

                    // Find EquipSlotVehicle component on the frame and call InstantiateItem
                    var equipSlot = bmxFrameNoBrand.transform.parent.GetComponent<EquipSlotVehicle>();
                    if (equipSlot != null)
                    {
                        equipSlot.InstantiateItem(bmxFrameNoBrand);
                        Log.Msg("EquipSlotVehicle.InstantiateItem called on Frame.");
                    }
                    else
                    {
                        Log.Warning("EquipSlotVehicle component not found on Frame.");
                    }
                }
                else
                {
                    Log.Warning("BMX_Frame_NoBrand_Standard_Brakeless not found.");
                }#1#
                

                //var spokes = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(obj => obj.name == "spokes");

                roweSpokes = prefabList.FirstOrDefault(obj => obj.name == "SpokesAndRims");
                roweBars = prefabList.FirstOrDefault(obj => obj.name == "Bars");
                
                if (roweSpokes != null)
                { 
                    var bmxFrontSpokes = rMBCharacter.transform.FindDeepChild("Proto_BMX/Chassis Body/Front Wheel/Offset/Rotator Bone/Wheel Mesh/BMX_Spokes_Front_EquipSlot/")?.gameObject;
                    var bmxRearSpokes = rMBCharacter.transform.FindDeepChild("Proto_BMX/Chassis Body/Back Wheel/Rotator Bone/Wheel Mesh/BMX_Spokes_Front_EquipSlot/")?.gameObject;
                    var bmxFrontRim = rMBCharacter.transform.FindDeepChild("Proto_BMX/Chassis Body/Front Wheel/Offset/Rotator Bone/Wheel Mesh/BMX_Rim_Front_EquipSlot/")?.gameObject;
                    var bmxRearRim = rMBCharacter.transform.FindDeepChild("Proto_BMX/Chassis Body/Back Wheel/Rotator Bone/Wheel Mesh/BMX_Rim_Front_EquipSlot/")?.gameObject;
                    
                    if(bmxFrontRim != null)
                        bmxFrontRim.SetActive(false);
                    if(bmxRearRim != null)
                        bmxRearRim.SetActive(false);
                    
                    // Replace bmxFrontSpokes with roweSpokes
                    if (bmxFrontSpokes != null)
                    {
                        //bmxFrontSpokes.SetActive(false);
                        if (roweSpokes != null)
                        {
                            bmxFrontSpokes.GetComponent<EquipSlotVehicle>().InstantiateItem(roweSpokes);
                            Log.Msg("Replaced bmxFrontSpokes with roweSpokes.");
                        }
                        else
                        {
                            Log.Warning("roweSpokes is null, cannot replace bmxFrontSpokes.");
                        }
                    }
                    else
                    {
                        Log.Warning("bmxFrontSpokes not found, cannot replace.");
                    }
               
                    // Replace bmxRearSpokes with roweSpokes
                    if (bmxRearSpokes != null)
                    {
                        //bmxRearSpokes.SetActive(false);
                        if (roweSpokes != null)
                        {
                            bmxRearSpokes.GetComponent<EquipSlotVehicle>().InstantiateItem(roweSpokes);
                            Log.Msg("Replaced bmxRearSpokes with roweSpokes.");
                        }
                        else
                        {
                            Log.Warning("roweSpokes is null, cannot replace bmxRearSpokes.");
                        }
                    }
                    else
                    {
                        Log.Warning("bmxRearSpokes not found, cannot replace.");
                    }
                }
                else
                {
                    Log.Warning("No roweSpokes found.");
                }
                
                //change bars
                roweBars = prefabList.FirstOrDefault(obj => obj.name == "bars");
                if (roweBars != null)
                {
                    var frontBars = rMBCharacter.transform
                        .FindDeepChild("Proto_BMX/Chassis Body/Front Wheel/Front End/Bars/")?.gameObject;
                    if (frontBars != null)
                    {
                        
                        if (roweBars != null)
                        {
                            /*GameObject newFrontBars = Object.Instantiate(roweBars, frontBars.transform.parent);
                            newFrontBars.SetActive(true);
                            //newFrontBars.transform.localPosition = new Vector3(0, 0, 0);
                            newFrontBars.AddComponent<EquipSlotVehicle>();
                            newFrontBars.name = "BMX_Bars_NoBrand_4PC";#1#
                            
                            frontBars.GetComponent<EquipSlotVehicle>().InstantiateItem(roweBars);
                            frontBars.GetComponent<Anchor>().SnapToAnchor();
                            Config.bikeMaterials.Remove("Bars");
                            
                            
                            Log.Msg("Replaced frontBars with roweBars.");
                        }
                        else
                        {
                            Log.Warning("roweBars is null, cannot replace frontBars.");
                        }
                    }
                    else
                    {
                        Log.Warning("frontBars not found, cannot replace.");
                    }
                }
                else
                {
                    Log.Warning("roweBars not found, cannot replace.");
                }
                
                
                
            }
            catch (Exception ex)
            {
                Log.Error($"Exception while toggling BMX frames: {ex.Message}");
            }
        }*/
        
        
        
        
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