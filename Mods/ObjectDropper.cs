using UnityEngine;
using rowemod.Utils;
using System.Collections.Generic;
using Il2CppMashBox.Addons.ProtoDrone;
using Il2CppMashBox.Addons.CharacterController;
using System.Linq;

namespace rowemod.Mods
{
    public static class ObjectDropper
    {
        // List to track spawned objects for deletion, capped at 20
        private static readonly List<GameObject> spawnedObjects = new List<GameObject>();
        
        // Cache for drone and character transforms
        private static Transform droneTransform;
        private static Transform characterTransform;
        
        // Layer mask for raycast, initialized at runtime
        private static int groundLayerMask;
        
        // Prefabs same as session markers
        private static List<GameObject> dropperPrefabs = new List<GameObject>();
        
        // Cached list of prefab names for UI rendering
        private static List<string> prefabNames = new List<string>();
        
        // Selected prefab name for spawning, initially null for "none"
        private static string selectedPrefabName = null;

        // Maximum number of spawned objects allowed
        private const int MaxSpawnedObjects = 20;

        // Scroll position for prefab button list
        private static Vector2 scrollPosition = Vector2.zero;

        // Initialize the dropper by finding references and loading prefabs
        public static void Initialize()
        {
            Log.Msg("Initializing ObjectDropper...");

            // Using all layers (idk if these are enabled, so select all)
            groundLayerMask = ~0; // Include all layers
            Log.Msg("Ground layer mask initialized to all layers (~0).");

            RefreshReferences();
            LoadDropperPrefabs();
        }

        // Update method to handle object spawning input
        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.O))
            {
                if (dropperPrefabs == null || dropperPrefabs.Count == 0)
                {
                    Log.Warning("No prefabs available to spawn in ObjectDropper.");
                    return;
                }

                if (string.IsNullOrEmpty(selectedPrefabName))
                {
                    Log.Warning("No prefab selected. Please select a prefab in the Object Dropper menu.");
                    return;
                }

                // Validating selected prefab is still available
                if (!dropperPrefabs.Any(p => p != null && p.name == selectedPrefabName))
                {
                    Log.Warning($"Selected prefab '{selectedPrefabName}' is no longer valid. Clearing selection.");
                    selectedPrefabName = null;
                    return;
                }

                // Refreshing references to get latest drone/character position
                RefreshReferences();

                // Finding the selected prefab
                GameObject prefabToSpawn = dropperPrefabs.FirstOrDefault(p => p != null && p.name == selectedPrefabName);
                if (prefabToSpawn == null)
                {
                    Log.Error($"Selected prefab '{selectedPrefabName}' not found or invalid.");
                    return;
                }
                
                SpawnObject(prefabToSpawn);
            }
        }

        // Refresh references to drone and character
        private static void RefreshReferences()
        {
            // Finding drone reference
            var droneController = GameObject.FindObjectOfType<DroneController>(true);
            if (droneController != null && droneController.gameObject.activeInHierarchy)
            {
                droneTransform = droneController.transform;
                Log.Msg($"Found active drone at: {droneTransform.gameObject.name}");
            }
            else
            {
                droneTransform = null;
                Log.Warning("No active drone found.");
            }

            // Finding character reference
            if (Memory.rMbCharacter != null)
            {
                var characterController = Memory.rMbCharacter.GetComponentInChildren<MGCharacterController>();
                if (characterController != null)
                {
                    characterTransform = characterController.transform;
                    Log.Msg($"Found character at: {characterTransform.gameObject.name}");
                }
                else
                {
                    characterTransform = Memory.rMbCharacter.transform;
                    Log.Warning("Character controller not found in rMbCharacter.");
                }
            }
            else
            {
                characterTransform = null;
                Log.Error("rMbCharacter is null, cannot find character transform.");
            }
        }

        // Load prefabs from Memory.loadedBundles
        private static void LoadDropperPrefabs()
        {
            // Clearing existing prefabs and names to ensure fresh references
            dropperPrefabs.Clear();
            prefabNames.Clear();
            Log.Msg($"Loading marker prefabs from Memory.loadedBundles.");

            if (Memory.loadedBundles == null || Memory.loadedBundles.Count == 0)
            {
                Log.Error("Memory.loadedBundles is empty or null. Ensure bundles are loaded in Memory.cs.");
                return;
            }

            // Iterating over loaded bundles
            Log.Msg($"Memory.loadedBundles contains {Memory.loadedBundles.Count} bundles.");
            foreach (AssetBundle bundle in Memory.loadedBundles)
            {
                if (bundle == null)
                {
                    Log.Warning("Found null AssetBundle in Memory.loadedBundles, skipping.");
                    continue;
                }

                // Getting asset names from bundle
                string[] assetNames = bundle.GetAllAssetNames();
                Log.Msg($"Bundle contains {assetNames.Length} assets.");
                foreach (var assetName in assetNames)
                {
                    Log.Msg($"Processing asset: {assetName}");
                    if (assetName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                        assetName.IndexOf("marker", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            GameObject asset = bundle.LoadAsset<GameObject>(assetName);
                            if (asset != null)
                            {
                                dropperPrefabs.Add(asset);
                                prefabNames.Add(asset.name);
                                Log.Msg($"[ObjectDropper] Loaded marker prefab: {asset.name}");
                            }
                            else
                            {
                                Log.Warning($"Failed to load marker prefab asset: {assetName}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error($"Error loading marker prefab {assetName}: {ex.Message}");
                        }
                    }
                }
            }

            // Logging final prefab count
            if (dropperPrefabs.Count == 0)
            {
                Log.Error("No marker prefabs loaded for ObjectDropper. Check Bundles folder for prefabs containing 'marker' in their name.");
            }
            else
            {
                Log.Msg($"Loaded {dropperPrefabs.Count} marker prefabs for ObjectDropper.");
            }
        }

        // Spawn an object at the appropriate position with raycast to ground
        private static void SpawnObject(GameObject prefab)
        {
            if (prefab == null)
            {
                Log.Error("Cannot spawn null prefab.");
                return;
            }

            // Checking if spawned objects exceed the limit
            if (spawnedObjects.Count >= MaxSpawnedObjects)
            {
                // Removing the oldest object (first in the list)
                GameObject oldestObject = spawnedObjects[0];
                if (oldestObject != null)
                {
                    UnityEngine.Object.Destroy(oldestObject);
                    Log.Msg($"Destroyed oldest spawned object: {oldestObject.name} to stay within {MaxSpawnedObjects} limit.");
                }
                spawnedObjects.RemoveAt(0);
            }

            // Determining spawn position and rotation
            Vector3 spawnPosition;
            Quaternion spawnRotation;
            bool isDrone = false;
            
            if (droneTransform != null && droneTransform.gameObject.activeInHierarchy)
            {
                spawnPosition = droneTransform.position;
                spawnRotation = Quaternion.Euler(0f, droneTransform.eulerAngles.y, 0f);
                isDrone = true;
                Log.Msg($"Using drone position for spawn: {spawnPosition}");
            }
            else if (characterTransform != null)
            {
                spawnPosition = characterTransform.position;
                spawnRotation = Quaternion.Euler(0f, characterTransform.eulerAngles.y, 0f);
                Log.Msg($"Drone inactive, using character position for spawn: {spawnPosition}");
            }
            else
            {
                Log.Error("No valid spawn position (both drone and character unavailable).");
                return;
            }

            // Performing raycast to find ground using UnityEngine.Physics
            Vector3 raycastOrigin = spawnPosition + Vector3.up * 2f; // Start 2 units above
            float raycastDistance = 100f; // Distance to cover drone altitude
            if (UnityEngine.Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayerMask))
            {
                // Check if hit is likely a non-ground collider (e.g., Y > 2)
                if (hit.point.y > 2f && isDrone)
                {
                    // Perform secondary raycast from a lower origin to find terrain
                    Vector3 secondaryOrigin = new Vector3(spawnPosition.x, 2f, spawnPosition.z); // Start near ground level
                    if (UnityEngine.Physics.Raycast(secondaryOrigin, Vector3.down, out RaycastHit secondaryHit, raycastDistance, groundLayerMask))
                    {
                        spawnPosition = secondaryHit.point;
                        Log.Msg($"Secondary raycast hit ground at: {secondaryHit.point}, collider: {secondaryHit.collider.gameObject.name}, layer: {LayerMask.LayerToName(secondaryHit.collider.gameObject.layer)}, distance: {secondaryHit.distance}");
                    }
                    else
                    {
                        // Fallback to Y=0 if secondary raycast fails
                        spawnPosition.y = 0f;
                        Log.Warning($"Secondary raycast did not hit ground from {secondaryOrigin} within {raycastDistance} units. Using fallback position: {spawnPosition}");
                    }
                }
                else
                {
                    spawnPosition = hit.point;
                    Log.Msg($"Raycast hit ground at: {hit.point}, collider: {hit.collider.gameObject.name}, layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}, distance: {hit.distance}");
                }
            }
            else
            {
                // Fallback to Y=0 if primary raycast fails
                Log.Warning($"Primary raycast did not hit ground from {raycastOrigin} within {raycastDistance} units.");
                spawnPosition.y = 0f;
                Log.Msg($"Using fallback ground position: {spawnPosition}");
            }

            // Instantiating the object
            GameObject spawnedObject = UnityEngine.Object.Instantiate(prefab, spawnPosition, spawnRotation);
            if (spawnedObject != null)
            {
                spawnedObjects.Add(spawnedObject);
                Log.Msg($"Spawned object: {spawnedObject.name} at {spawnPosition}");
            }
            else
            {
                Log.Error("Failed to instantiate prefab.");
            }
        }

        // Draw the UI for the Dropper tab
        public static void DrawDropperTab()
        {
            // Displaying header for Object Dropper tab
            GUILayout.Box("Object Dropper", Menu.coloredBoxStyle, GUILayout.Height(Menu.coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
            
            if (prefabNames == null || prefabNames.Count == 0)
            {
                GUILayout.Label("No objects available to spawn.", Menu.labelStyle);
                return;
            }

            // Adding scroll view for prefab buttons
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200f));

            // Listing cached prefab names for selection
            foreach (string prefabName in prefabNames)
            {
                if (GUILayout.Button(prefabName, Menu.highQualityButtonStyle))
                {
                    // Setting selected prefab without spawning
                    selectedPrefabName = prefabName;
                    Log.Msg($"Selected prefab: {selectedPrefabName}");
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);
            // Adding button to delete all spawned objects
            if (GUILayout.Button("<b>Delete All Spawned Objects</b>", Menu.highQualityButtonStyle))
            {
                DeleteAllSpawnedObjects();
            }

            // Displaying currently selected prefab
            GUILayout.Label($"Current Selected Object: {(selectedPrefabName ?? "None")}", Menu.labelStyle);
        }

        // Reset the Dropper tab to default state
        public static void ResetTab()
        {
            // Resetting selected prefab to null
            selectedPrefabName = null;
            Log.Msg("Object Dropper tab reset: Selected prefab set to None.");
        }

        // Delete all spawned objects
        private static void DeleteAllSpawnedObjects()
        {
            // Destroying all tracked spawned objects
            int count = spawnedObjects.Count;
            foreach (var obj in spawnedObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
            spawnedObjects.Clear();
            Log.Msg($"Deleted {count} spawned objects.");
        }
    }
}