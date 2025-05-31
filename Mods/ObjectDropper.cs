using UnityEngine;
using rowemod.Utils;
using System.Collections.Generic;
using Il2CppMashBox.Addons.ProtoDrone;
using Il2CppMashBox.Addons.CharacterController;
using System.Linq;
using System.IO;

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
        
        // Selected prefab name for spawning, initially null
        private static string selectedPrefabName = null;

        // Maximum number of spawned objects allowed
        private const int MaxSpawnedObjects = 20;

        // Initialize the dropper by finding references and loading prefabs
        public static void Initialize()
        {
            Log.Msg("Initializing ObjectDropper...");

            // using "default" layer now, until mash maybe enables actual layers
            try
            {
                int defaultLayer = LayerMask.NameToLayer("Default");
                if (defaultLayer != -1)
                {
                    groundLayerMask = 1 << defaultLayer;
                    Log.Msg("Ground layer mask initialized to Default layer.");
                }
                else
                {
                    groundLayerMask = 1 << 0; // Fallback to layer 0
                    Log.Warning("Default layer not found, using layer 0 as fallback.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Failed to initialize ground layer mask: {ex.Message}");
                groundLayerMask = 1 << 0; // Fallback to layer 0
                Log.Warning("Using layer 0 as fallback for ground layer mask.");
            }

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

        // Load prefabs from memory class
        private static void LoadDropperPrefabs()
        {
            // Skipping reload if prefabs are already loaded
            if (dropperPrefabs != null && dropperPrefabs.Count > 0)
            {
                Log.Msg($"Skipping prefab reload, {dropperPrefabs.Count} marker prefabs already loaded.");
                return;
            }

            // Clearing existing prefabs to avoid duplicates
            dropperPrefabs.Clear();
            Log.Msg($"Attempting to load marker prefabs from Memory.loadedBundles.");

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
            
            if (droneTransform != null && droneTransform.gameObject.activeInHierarchy)
            {
                spawnPosition = droneTransform.position;
                spawnRotation = Quaternion.Euler(0f, droneTransform.eulerAngles.y, 0f);
                Log.Msg("Using drone position for spawn.");
            }
            else if (characterTransform != null)
            {
                spawnPosition = characterTransform.position;
                spawnRotation = Quaternion.Euler(0f, characterTransform.eulerAngles.y, 0f);
                Log.Msg("Drone inactive, using character position for spawn.");
            }
            else
            {
                Log.Error("No valid spawn position (both drone and character unavailable).");
                return;
            }

            // Performing raycast to find ground using UnityEngine.Physics
            Vector3 raycastOrigin = spawnPosition + Vector3.up * 10f; // Start 10 units above
            if (UnityEngine.Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, 20f, groundLayerMask))
            {
                spawnPosition = hit.point + Vector3.up * 0.1f; // Slightly above ground to avoid clipping
                Log.Msg($"Raycast hit ground at: {hit.point}");
            }
            else
            {
                Log.Warning("Raycast did not hit ground, using original Y position.");
                spawnPosition.y += 0.1f; // Small offset to avoid ground clipping
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
            
            if (dropperPrefabs == null || dropperPrefabs.Count == 0)
            {
                GUILayout.Label("No objects available to spawn.", Menu.labelStyle);
                return;
            }

            // Listing available prefabs for selection
            foreach (GameObject prefab in dropperPrefabs.Where(p => p != null))
            {
                if (GUILayout.Button(prefab.name, Menu.highQualityButtonStyle))
                {
                    // Setting selected prefab without spawning
                    selectedPrefabName = prefab.name;
                    Log.Msg($"Selected prefab: {selectedPrefabName}");
                }
            }

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