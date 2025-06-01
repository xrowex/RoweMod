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
        
        // Prefabs same as session markers
        private static List<GameObject> dropperPrefabs = new List<GameObject>();
        
        // Cached list of prefab names for UI rendering
        private static List<string> prefabNames = new List<string>();
        
        // Selected prefab name for spawning, initially null for "none"
        private static string selectedPrefabName = null;

        // List to track multiple selected objects for deletion
        private static readonly List<GameObject> selectedObjects = new List<GameObject>();
        private static readonly Dictionary<GameObject, List<Material>> originalMaterialsMap = new Dictionary<GameObject, List<Material>>();

        // Maximum number of spawned objects allowed
        private const int MaxSpawnedObjects = 20;

        // Initialize the dropper by finding references and loading prefabs
        public static void Initialize()
        {
            // Logging initialization start
            Log.Msg("Initializing ObjectDropper...");

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

        // Toggle selection of a spawned object via UI and apply/remove highlighting
        private static void ToggleObjectSelection(GameObject obj)
        {
            if (obj != null && spawnedObjects.Contains(obj))
            {
                if (selectedObjects.Contains(obj))
                {
                    // Deselect the object
                    selectedObjects.Remove(obj);
                    // Restore original materials
                    if (originalMaterialsMap.TryGetValue(obj, out var originalMaterials))
                    {
                        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                        int materialIndex = 0;
                        foreach (Renderer renderer in renderers)
                        {
                            Material[] materials = renderer.materials;
                            for (int i = 0; i < materials.Length && materialIndex < originalMaterials.Count; i++)
                            {
                                materials[i].CopyPropertiesFromMaterial(originalMaterials[materialIndex]);
                                materialIndex++;
                            }
                            renderer.materials = materials;
                        }
                        originalMaterialsMap.Remove(obj);
                        Log.Msg($"Deselected object: {obj.name}");
                    }
                }
                else
                {
                    // Select the object
                    selectedObjects.Add(obj);
                    // Store original materials and apply highlight
                    Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                    List<Material> originalMaterials = new List<Material>();
                    foreach (Renderer renderer in renderers)
                    {
                        originalMaterials.AddRange(renderer.materials);
                        Material[] materials = renderer.materials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            Material highlightMat = new Material(materials[i]);
                            highlightMat.color = Color.green;
                            materials[i] = highlightMat;
                        }
                        renderer.materials = materials;
                    }
                    originalMaterialsMap[obj] = originalMaterials;
                    Log.Msg($"Selected object: {obj.name}");
                }
            }
            else
            {
                Log.Warning("Toggled object is null or not a spawned object.");
            }
        }

        // Clear all selected objects and restore their materials
        private static void ClearSelection()
        {
            foreach (var obj in selectedObjects.ToList())
            {
                if (obj != null && originalMaterialsMap.TryGetValue(obj, out var originalMaterials))
                {
                    Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                    int materialIndex = 0;
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] materials = renderer.materials;
                        for (int i = 0; i < materials.Length && materialIndex < originalMaterials.Count; i++)
                        {
                            materials[i].CopyPropertiesFromMaterial(originalMaterials[materialIndex]);
                            materialIndex++;
                        }
                        renderer.materials = materials;
                    }
                    Log.Msg($"Cleared selection for object: {obj.name}");
                }
            }
            selectedObjects.Clear();
            originalMaterialsMap.Clear();
        }

        // Delete all selected objects
        private static void DeleteSelectedObjects()
        {
            if (selectedObjects.Count == 0)
            {
                Log.Warning("No objects selected to delete.");
                return;
            }

            int deletedCount = 0;
            foreach (var obj in selectedObjects.ToList())
            {
                if (obj != null && spawnedObjects.Contains(obj))
                {
                    UnityEngine.Object.Destroy(obj);
                    spawnedObjects.Remove(obj);
                    originalMaterialsMap.Remove(obj);
                    deletedCount++;
                    Log.Msg($"Deleted object: {obj.name}");
                }
            }
            selectedObjects.Clear();
            Log.Msg($"Deleted {deletedCount} selected objects.");
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
                // Start spawn position 0.1 meter(s) below the drone
                spawnPosition = droneTransform.position - Vector3.up * 0.2f;
                sourceYRotation = droneTransform.eulerAngles.y;
                isDrone = true;
                Log.Msg($"Using position below drone for spawn: {spawnPosition}");
            }
            else if (characterTransform != null)
            {
                // Offset spawn position 1 meter(s) in front of character
                spawnPosition = characterTransform.position + characterTransform.forward * 1f;
                sourceYRotation = characterTransform.eulerAngles.y;
                Log.Msg($"Using character forward position for spawn: {spawnPosition}");
            }
            else
            {
                Log.Error("No valid spawn position (both drone and character unavailable).");
                return;
            }

            // Performing raycast to find ground using UnityEngine.Physics
            Vector3 raycastOrigin = spawnPosition;
            float raycastDistance = 100f; // Distance to cover drone/character altitude
            Quaternion spawnRotation = Quaternion.Euler(0f, sourceYRotation, 0f); // Default upright rotation
            if (UnityEngine.Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastDistance))
            {
                // Check if hit is likely a non-ground collider (e.g., Y > 2 or Y < spawnPosition.y - 2)
                if (hit.point.y > spawnPosition.y + 2f || hit.point.y < spawnPosition.y - 2f)
                {
                    // Perform secondary raycast from a lower origin to find terrain
                    Vector3 secondaryOrigin = new Vector3(spawnPosition.x, spawnPosition.y + 1f, spawnPosition.z);
                    if (UnityEngine.Physics.Raycast(secondaryOrigin, Vector3.down, out RaycastHit secondaryHit, raycastDistance))
                    {
                        spawnPosition = secondaryHit.point;
                        // Validate normal (upward-facing) and align rotation
                        Vector3 normal = secondaryHit.normal.normalized;
                        if (normal.y > 0.5f) // Ensure normal is mostly upward
                        {
                            spawnRotation = Quaternion.LookRotation(Vector3.forward, normal) * Quaternion.Euler(0f, sourceYRotation, 0f);
                            // Limit tilt to 60 degrees
                            Vector3 euler = spawnRotation.eulerAngles;
                            euler.x = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -60f, 60f);
                            euler.z = Mathf.Clamp(euler.z > 180f ? euler.z - 360f : euler.z, -60f, 60f);
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
                        euler.x = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -60f, 60f);
                        euler.z = Mathf.Clamp(euler.z > 180f ? euler.z - 360f : euler.z, -60f, 60f);
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
                    if (obj == null) continue;

                    bool isSelected = selectedObjects.Contains(obj);
                    GUIStyle toggleStyle = new GUIStyle(Menu.highQualityButtonStyle);
                    
                    // Set background color based on toggle state
                    Color backgroundColor = isSelected ? Color.red : new Color(Config.menuAccentR, Config.menuAccentG, Config.menuAccentB);
                    toggleStyle.normal.background = Menu.MakeRoundedTex(20, 40, backgroundColor, 10, 1, Color.black);
                    toggleStyle.hover.background = Menu.MakeRoundedTex(20, 40, new Color(
                        Mathf.Min(backgroundColor.r + 0.1f, 1f),
                        Mathf.Min(backgroundColor.g + 0.1f, 1f),
                        Mathf.Min(backgroundColor.b + 0.1f, 1f)), 10, 1, Color.black);
                    toggleStyle.active.background = toggleStyle.hover.background;

                    if (GUILayout.Button(label, toggleStyle))
                    {
                        ToggleObjectSelection(obj);
                    }
                }
            }

            GUILayout.Space(10);
            // Adding button to delete all spawned objects
            if (GUILayout.Button("<b>Delete All Spawned Objects</b>", Menu.highQualityButtonStyle))
            {
                DeleteAllSpawnedObjects();
            }

            // Adding button to delete selected objects
            if (GUILayout.Button("<b>Delete Selected Objects</b>", Menu.highQualityButtonStyle))
            {
                DeleteSelectedObjects();
            }

            // Displaying currently selected prefab and number of selected objects
            GUILayout.Label($"Current Selected Prefab: {(selectedPrefabName ?? "None")}", Menu.labelStyle);
            GUILayout.Label($"Selected Objects: {selectedObjects.Count}", Menu.labelStyle);
        }

        // Reset the Dropper tab to default state
        public static void ResetTab()
        {
            // Resetting selected prefab and clear selections
            selectedPrefabName = null;
            ClearSelection();
            Log.Msg("Object Dropper tab reset: Selected prefab and objects cleared.");
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