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

        // Selected object for deletion, initially null
        private static GameObject selectedObject = null;
        private static List<Material> originalMaterials = new List<Material>(); // Store original materials for highlight restoration

        // Maximum number of spawned objects allowed
        private const int MaxSpawnedObjects = 20;

        // Initialize the dropper by finding references and loading prefabs
        public static void Initialize()
        {
            // Logging initialization start
            Log.Msg("Initializing ObjectDropper...");

            // Using all layers due to limited layer support in the game
            groundLayerMask = ~0; // Include all layers
            Log.Msg("Ground layer mask initialized to all layers (~0).");

            RefreshReferences();
            LoadDropperPrefabs();
        }

        // Update method to handle object spawning
        public static void Update()
        {
            // Handle object spawning
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

        // Select a spawned object via UI and apply highlighting
        private static void SelectObject(GameObject obj)
        {
            // Clear current selection
            ClearSelection();

            if (obj != null && spawnedObjects.Contains(obj))
            {
                selectedObject = obj;
                // Store original materials and apply highlight
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                originalMaterials.Clear();
                foreach (Renderer renderer in renderers)
                {
                    originalMaterials.AddRange(renderer.materials);
                    foreach (Material mat in renderer.materials)
                    {
                        mat.color = Color.red; // Highlight with red tint
                    }
                }
                Log.Msg($"Selected spawned object via UI: {selectedObject.name}");
            }
            else
            {
                Log.Warning("Selected object is null or not a spawned object.");
            }
        }

        // Clear the current object selection and restore materials
        private static void ClearSelection()
        {
            if (selectedObject != null)
            {
                // Restore original materials
                Renderer[] renderers = selectedObject.GetComponentsInChildren<Renderer>();
                int materialIndex = 0;
                foreach (Renderer renderer in renderers)
                {
                    Material[] materials = renderer.materials;
                    for (int i = 0; i < materials.Length && materialIndex < originalMaterials.Count; i++)
                    {
                        materials[i].color = originalMaterials[materialIndex].color;
                        materialIndex++;
                    }
                    renderer.materials = materials;
                }
                Log.Msg($"Deselected object: {selectedObject.name}");
                selectedObject = null;
                originalMaterials.Clear();
            }
        }

        // Delete the selected object
        private static void DeleteSelectedObject()
        {
            if (selectedObject == null)
            {
                Log.Warning("No object selected to delete.");
                return;
            }

            if (spawnedObjects.Contains(selectedObject))
            {
                UnityEngine.Object.Destroy(selectedObject);
                spawnedObjects.Remove(selectedObject);
                Log.Msg($"Deleted selected object: {selectedObject.name}");
                selectedObject = null;
                originalMaterials.Clear();
            }
            else
            {
                Log.Warning($"Selected object {selectedObject.name} is no longer valid.");
                ClearSelection();
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
            float sourceYRotation;
            bool isDrone = false;
            
            if (droneTransform != null && droneTransform.gameObject.activeInHierarchy)
            {
                spawnPosition = droneTransform.position;
                sourceYRotation = droneTransform.eulerAngles.y;
                isDrone = true;
                Log.Msg($"Using drone position for spawn: {spawnPosition}");
            }
            else if (characterTransform != null)
            {
                // Offset spawn position 2 meters in front of character
                spawnPosition = characterTransform.position + characterTransform.forward * 2f;
                sourceYRotation = characterTransform.eulerAngles.y;
                Log.Msg($"Using character forward position for spawn: {spawnPosition}");
            }
            else
            {
                Log.Error("No valid spawn position (both drone and character unavailable).");
                return;
            }

            // Performing raycast to find ground using UnityEngine.Physics
            Vector3 raycastOrigin = spawnPosition + Vector3.up * (isDrone ? 2f : 5f); // 2m for drone, 5m for character
            float raycastDistance = 100f; // Distance to cover drone/character altitude
            Quaternion spawnRotation = Quaternion.Euler(0f, sourceYRotation, 0f); // Default upright rotation
            if (UnityEngine.Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayerMask))
            {
                // Check if hit is likely a non-ground collider (e.g., Y > 2 or Y < spawnPosition.y - 2)
                if (hit.point.y > spawnPosition.y + 2f || hit.point.y < spawnPosition.y - 2f)
                {
                    // Perform secondary raycast from a lower origin to find terrain
                    Vector3 secondaryOrigin = new Vector3(spawnPosition.x, spawnPosition.y + 2f, spawnPosition.z);
                    if (UnityEngine.Physics.Raycast(secondaryOrigin, Vector3.down, out RaycastHit secondaryHit, raycastDistance, groundLayerMask))
                    {
                        spawnPosition = secondaryHit.point;
                        // Validate normal (upward-facing) and align rotation
                        Vector3 normal = secondaryHit.normal.normalized;
                        if (normal.y > 0.5f) // Ensure normal is mostly upward
                        {
                            spawnRotation = Quaternion.LookRotation(Vector3.forward, normal) * Quaternion.Euler(0f, sourceYRotation, 0f);
                            // Limit tilt to 45 degrees
                            Vector3 euler = spawnRotation.eulerAngles;
                            euler.x = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -45f, 45f);
                            euler.z = Mathf.Clamp(euler.z > 180f ? euler.z - 360f : euler.z, -45f, 45f);
                            spawnRotation = Quaternion.Euler(euler);
                            Log.Msg($"Secondary raycast hit ground at: {secondaryHit.point}, collider: {secondaryHit.collider.gameObject.name}, layer: {LayerMask.LayerToName(secondaryHit.collider.gameObject.layer)}, distance: {secondaryHit.distance}, normal: {normal}, rotation: {spawnRotation.eulerAngles}");
                        }
                        else
                        {
                            Log.Warning($"Secondary raycast normal invalid: {normal}. Using default rotation.");
                        }
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
                    // Validate normal and align rotation
                    Vector3 normal = hit.normal.normalized;
                    if (normal.y > 0.5f)
                    {
                        spawnRotation = Quaternion.LookRotation(Vector3.forward, normal) * Quaternion.Euler(0f, sourceYRotation, 0f);
                        Vector3 euler = spawnRotation.eulerAngles;
                        euler.x = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -45f, 45f);
                        euler.z = Mathf.Clamp(euler.z > 180f ? euler.z - 360f : euler.z, -45f, 45f);
                        spawnRotation = Quaternion.Euler(euler);
                        Log.Msg($"Primary raycast hit ground at: {hit.point}, collider: {hit.collider.gameObject.name}, layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}, distance: {hit.distance}, normal: {normal}, rotation: {spawnRotation.eulerAngles}");
                    }
                    else
                    {
                        Log.Warning($"Primary raycast normal invalid: {normal}. Using default rotation.");
                    }
                }
            }
            else
            {
                // Fallback to Y=0 if primary raycast fails
                Log.Warning($"Primary raycast did not hit ground from {raycastOrigin} within {raycastDistance} units.");
                spawnPosition.y = 0f;
                Log.Msg($"Using fallback ground position: {spawnPosition}, rotation: {spawnRotation.eulerAngles}");
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

            // Adding prefab buttons
            GUILayout.Label("Available Prefabs:", Menu.labelStyle);
            foreach (string prefabName in prefabNames)
            {
                if (GUILayout.Button(prefabName, Menu.highQualityButtonStyle))
                {
                    // Setting selected prefab without spawning
                    selectedPrefabName = prefabName;
                    Log.Msg($"Selected prefab: {selectedPrefabName}");
                }
            }

            // Adding spawned objects list
            GUILayout.Space(10);
            GUILayout.Label("Spawned Objects:", Menu.labelStyle);
            if (spawnedObjects.Count == 0)
            {
                GUILayout.Label("No objects spawned.", Menu.labelStyle);
            }
            else
            {
                for (int i = 0; i < spawnedObjects.Count; i++)
                {
                    GameObject obj = spawnedObjects[i];
                    string label = obj != null ? $"Object {i + 1}: {obj.name}" : $"Object {i + 1}: (Destroyed)";
                    if (GUILayout.Button(label, Menu.highQualityButtonStyle))
                    {
                        SelectObject(obj);
                    }
                }
            }

            GUILayout.Space(10);
            // Adding button to delete all spawned objects
            if (GUILayout.Button("<b>Delete All Spawned Objects</b>", Menu.highQualityButtonStyle))
            {
                DeleteAllSpawnedObjects();
            }

            // Adding button to delete selected object
            if (GUILayout.Button("<b>Delete Selected Object</b>", Menu.highQualityButtonStyle))
            {
                DeleteSelectedObject();
            }

            // Displaying currently selected prefab and object
            GUILayout.Label($"Current Selected Prefab: {(selectedPrefabName ?? "None")}", Menu.labelStyle);
            GUILayout.Label($"Current Selected Object: {(selectedObject != null ? selectedObject.name : "None")}", Menu.labelStyle);
        }

        // Reset the Dropper tab to default state
        public static void ResetTab()
        {
            // Resetting selected prefab and object to null
            selectedPrefabName = null;
            ClearSelection();
            Log.Msg("Object Dropper tab reset: Selected prefab and object set to None.");
        }

        // Delete all spawned objects
        private static void DeleteAllSpawnedObjects()
        {
            // Clear current selection to avoid referencing destroyed objects
            ClearSelection();

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