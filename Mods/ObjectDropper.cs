using UnityEngine;
using rowemod.Utils;
using Il2CppMashBox.Addons.ProtoDrone;
using Il2CppMashBox.Addons.CharacterController;

namespace rowemod.Mods
{
    public static class ObjectDropper
    {
        // List to track spawned objects for deletion, capped at 20
        private static readonly List<GameObject> spawnedObjects = new List<GameObject>();
        
        // Cache for drone and character transforms
        private static Transform droneTransform;
        private static Transform characterTransform;
        
        // Selected prefab name for spawning, initially null for "none"
        private static string selectedPrefabName = null;

        // List to track multiple selected objects for deletion or transformation
        private static readonly List<GameObject> selectedObjects = new List<GameObject>();
        private static readonly Dictionary<GameObject, List<Material>> originalMaterialsMap = new Dictionary<GameObject, List<Material>>();

        // Maximum number of spawned objects allowed
        private static readonly int MaxSpawnedObjects = 20;

        // Cached GUI styles and textures for toggles
        private static GUIStyle toggleOnStyle;
        private static GUIStyle toggleOffStyle;
        private static Texture2D toggleOnNormalTexture;
        private static Texture2D toggleOnHoverTexture;
        private static Texture2D toggleOffNormalTexture;
        private static Texture2D toggleOffHoverTexture;
        private static Color lastMenuAccentColor;

        // Transform adjustment offsets for selected objects
        private static Vector3 positionOffset = Vector3.zero;
        private static Vector3 rotationOffset = Vector3.zero;

        // Track menu state for deselection on toggle
        private static bool wasMenuOpen = false;

        // Layer mask for ground raycasts
        private static readonly LayerMask groundLayerMask = 1 << 0; // Default layer

        // Initialize the dropper by finding references
        public static void Initialize()
        {
            // Logging initialization start
            Log.Msg("Initializing ObjectDropper...");

            // Clear spawned objects to prevent stale references after scene change
            ClearSpawnedObjects();

            // Initialize cached GUI styles and textures
            InitializeToggleStyles();

            RefreshReferences();
        }

        // Clear all spawned objects to ensure no stale references
        private static void ClearSpawnedObjects()
        {
            foreach (var obj in spawnedObjects.ToList())
            {
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
            spawnedObjects.Clear();
            selectedObjects.Clear();
            originalMaterialsMap.Clear();
            Log.Msg("Cleared all spawned objects on initialization.");
        }

        // Initialize or update cached GUI styles and textures for toggles
        private static void InitializeToggleStyles()
        {
            // Create or update textures if menu accent color has changed
            Color currentMenuAccentColor = new Color(Config.menuAccentR, Config.menuAccentG, Config.menuAccentB);
            if (toggleOnNormalTexture == null || lastMenuAccentColor != currentMenuAccentColor)
            {
                // ON state textures (red)
                toggleOnNormalTexture = Menu.MakeRoundedTex(128, 20, Color.red, 10, 1, Color.black);
                toggleOnHoverTexture = Menu.MakeRoundedTex(128, 20, new Color(
                    Mathf.Min(1f, 1f), // Red hover: slightly brighter
                    Mathf.Min(0.1f, 1f),
                    Mathf.Min(0.1f, 1f)), 10, 1, Color.black);

                // OFF state textures (menu accent color)
                toggleOffNormalTexture = Menu.MakeRoundedTex(128, 20, currentMenuAccentColor, 10, 1, Color.black);
                toggleOffHoverTexture = Menu.MakeRoundedTex(128, 20, new Color(
                    Mathf.Min(currentMenuAccentColor.r + 0.1f, 1f),
                    Mathf.Min(currentMenuAccentColor.g + 0.1f, 1f),
                    Mathf.Min(currentMenuAccentColor.b + 0.1f, 1f)), 10, 1, Color.black);

                // Create GUI styles with exact highQualityButtonStyle properties
                toggleOnStyle = new GUIStyle(Menu.highQualityButtonStyle)
                {
                    normal = { background = toggleOnNormalTexture, textColor = Color.white },
                    hover = { background = toggleOnHoverTexture, textColor = Color.yellow },
                    active = { background = toggleOnHoverTexture, textColor = Color.green },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    border = new RectOffset(10, 10, 10, 10),
                    padding = Menu.highQualityButtonStyle.padding,
                    margin = Menu.highQualityButtonStyle.margin,
                    overflow = Menu.highQualityButtonStyle.overflow,
                    contentOffset = new Vector2(0, 0), // Center text vertically
                    stretchWidth = false,
                    stretchHeight = false
                };

                toggleOffStyle = new GUIStyle(Menu.highQualityButtonStyle)
                {
                    normal = { background = toggleOffNormalTexture, textColor = Color.white },
                    hover = { background = toggleOffHoverTexture, textColor = Color.yellow },
                    active = { background = toggleOffHoverTexture, textColor = Color.green },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    border = new RectOffset(10, 10, 10, 10),
                    padding = Menu.highQualityButtonStyle.padding,
                    margin = Menu.highQualityButtonStyle.margin,
                    overflow = Menu.highQualityButtonStyle.overflow,
                    contentOffset = new Vector2(0, 0), // Center text vertically
                    stretchWidth = false,
                    stretchHeight = false
                };

                lastMenuAccentColor = currentMenuAccentColor;
                Log.Msg("Initialized toggle styles and textures.");
            }
        }

        // Update method to handle object spawning and menu toggle deselection
        public static void Update()
        {
            // Handle object spawning
            if (Input.GetKeyDown(KeyCode.O))
            {
                if (Memory.dropperPrefabs == null || Memory.dropperPrefabs.Count == 0)
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
                if (!Memory.dropperPrefabNames.Contains(selectedPrefabName))
                {
                    Log.Warning($"Selected prefab '{selectedPrefabName}' is no longer valid. Clearing selection.");
                    selectedPrefabName = null;
                    return;
                }

                // Refreshing references to get latest drone/character position
                RefreshReferences();

                // Finding the selected prefab by matching display name
                int prefabIndex = Memory.dropperPrefabNames.IndexOf(selectedPrefabName);
                if (prefabIndex < 0 || prefabIndex >= Memory.dropperPrefabs.Count)
                {
                    Log.Error($"Selected prefab '{selectedPrefabName}' not found or invalid.");
                    return;
                }

                GameObject prefabToSpawn = Memory.dropperPrefabs[prefabIndex];
                if (prefabToSpawn == null)
                {
                    Log.Error($"Selected prefab '{selectedPrefabName}' is null.");
                    return;
                }
                
                SpawnObject(prefabToSpawn);
            }

            // Deselect objects on menu toggle
            bool isMenuOpen = Menu.isOpen;
            if (isMenuOpen != wasMenuOpen)
            {
                if (selectedObjects.Count > 0)
                {
                    ClearSelection();
                    Log.Msg("Deselected all objects due to menu toggle.");
                }
                wasMenuOpen = isMenuOpen;
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
                            highlightMat.color = Color.green; // Highlight with green tint
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

            // Reset transform offsets when clearing selection
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
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

            // Reset transform offsets after deletion
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
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

            // Determining raycast origin and rotation
            Vector3 raycastOrigin;
            float sourceYRotation;
            bool isDrone = false;
            
            if (droneTransform != null && droneTransform.gameObject.activeInHierarchy)
            {
                // Raycast starts 0.3 meter(s) below the drone
                raycastOrigin = droneTransform.position - Vector3.up * 0.3f;
                sourceYRotation = droneTransform.eulerAngles.y;
                isDrone = true;
                Log.Msg($"Raycast starting 0.3m below drone at: {raycastOrigin}");
            }
            else if (characterTransform != null)
            {
                // Raycast starts 1 meter(s) in front of character
                raycastOrigin = characterTransform.position + characterTransform.forward * 1f;
                sourceYRotation = characterTransform.eulerAngles.y;
                Log.Msg($"Raycast starting 1m in front of character at: {raycastOrigin}");
            }
            else
            {
                Log.Error("No valid raycast origin (both drone and character unavailable).");
                return;
            }

            // Performing initial raycast to find ground and rotation
            float raycastDistance = 1000f; // Distance to cover drone/character altitude
            Quaternion spawnRotation = Quaternion.Euler(0f, sourceYRotation, 0f); // Default upright rotation
            Vector3 spawnPosition;
            Vector3 groundNormal = Vector3.up;

            if (UnityEngine.Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayerMask))
            {
                spawnPosition = hit.point;
                // Validate normal and align rotation
                groundNormal = hit.normal.normalized;
                if (groundNormal.y > 0.0872f) // Allow slopes up to ~85 degrees (cos(85°))
                {
                    // Align object up direction with surface normal, forward with source Y-rotation
                    Vector3 forward = Quaternion.Euler(0f, sourceYRotation, 0f) * Vector3.forward;
                    // Project forward onto plane perpendicular to normal to avoid twisting
                    forward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;
                    spawnRotation = Quaternion.LookRotation(forward, groundNormal);
                    Log.Msg($"Initial raycast hit ground at: {hit.point}, collider: {hit.collider.gameObject.name}, layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}, distance: {hit.distance}, normal: {groundNormal}, rotation: {spawnRotation.eulerAngles}");
                }
                else
                {
                    Log.Warning($"Initial raycast normal too steep: {groundNormal} (y={groundNormal.y}). Using default rotation.");
                }
            }
            else
            {
                // Fallback to Y=0 at raycast origin's X/Z position
                spawnPosition = new Vector3(raycastOrigin.x, 0f, raycastOrigin.z);
                Log.Warning($"Initial raycast did not hit ground from {raycastOrigin} within {raycastDistance} units. Using fallback position: {spawnPosition}");
            }

            // Calculate bottom offset at safe position
            float bottomOffset = 0f;
            GameObject tempObject = UnityEngine.Object.Instantiate(prefab, Vector3.zero, spawnRotation);
            Renderer[] renderers = tempObject.GetComponentsInChildren<Renderer>();
            Collider[] colliders = tempObject.GetComponentsInChildren<Collider>();
            if (renderers.Length > 0 || colliders.Length > 0)
            {
                Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
                if (renderers.Length > 0)
                {
                    foreach (Renderer renderer in renderers)
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
                else
                {
                    foreach (Collider collider in colliders)
                    {
                        combinedBounds.Encapsulate(collider.bounds);
                    }
                }

                // Calculate offset from origin to bottom in world space
                bottomOffset = -combinedBounds.min.y; // Distance from origin to bottom
                Log.Msg($"Bottom offset (origin to bottom): {bottomOffset}");
            }
            else
            {
                Log.Warning($"Prefab {prefab.name} has no renderers or colliders, using unadjusted spawn position.");
            }
            UnityEngine.Object.Destroy(tempObject);

            // Adjust spawn position to align bottom with ground
            spawnPosition.y += bottomOffset;
            Log.Msg($"Adjusted spawn position by bottom offset: {bottomOffset}, new position: {spawnPosition}");

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
            // Update toggle styles if menu accent color has changed
            InitializeToggleStyles();

            // Displaying header for Object Dropper tab
            GUILayout.Box("Object Dropper", Menu.coloredBoxStyle, GUILayout.Height(Menu.coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));

            // Adding prefab buttons, conditional on prefab availability
            if (Memory.dropperPrefabs == null || Memory.dropperPrefabs.Count == 0)
            {
                GUILayout.Label("No objects available to spawn.", Menu.labelStyle);
            }
            else
            {
                GUILayout.Label("Available Prefabs:", Menu.labelStyle);
                foreach (string prefabName in Memory.dropperPrefabNames)
                {
                    if (GUILayout.Button(prefabName, Menu.highQualityButtonStyle, GUILayout.Width(500f), GUILayout.Height(20f)))
                    {
                        // Setting selected prefab
                        selectedPrefabName = prefabName;
                        Log.Msg($"Selected prefab: {selectedPrefabName}");
                    }
                }
            }

            // Display spawned objects list
            GUILayout.Space(10);
            GUILayout.BeginVertical(GUILayout.Width(500f));
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
                    // Use the prefab display name from Memory.dropperPrefabNames based on the prefab's name
                    string displayName = Memory.dropperPrefabNames.FirstOrDefault(name => obj != null && obj.name.Contains(name)) ?? obj?.name ?? "(Destroyed)";
                    string label = obj != null ? $"Object {i + 1}: {displayName}" : $"Object {i + 1}: (Destroyed)";
                    if (obj == null) continue;

                    bool isSelected = selectedObjects.Contains(obj);
                    GUIStyle toggleStyle = isSelected ? toggleOnStyle : toggleOffStyle;

                    if (GUILayout.Button(label, toggleStyle, GUILayout.Width(500f), GUILayout.Height(20f)))
                    {
                        ToggleObjectSelection(obj);
                    }
                }
            }

            // Delete buttons (always shown, below the spawned objects list)
            GUILayout.Space(10);
            if (GUILayout.Button("<b>Delete All Spawned Objects</b>", Menu.highQualityButtonStyle, GUILayout.Width(200f), GUILayout.Height(30f)))
            {
                DeleteAllSpawnedObjects();
            }
            if (GUILayout.Button("<b>Delete Selected Objects</b>", Menu.highQualityButtonStyle, GUILayout.Width(200f), GUILayout.Height(30f)))
            {
                DeleteSelectedObjects();
            }
            GUILayout.EndVertical();

            // Adding transform controls for selected objects
            if (selectedObjects.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Box("Transform Controls", Menu.coloredBoxStyle, GUILayout.Height(Menu.coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));

                // Position sliders with live updates
                Vector3 prevPositionOffset = positionOffset;
                
                Menu.ModernSlider("Position X", ref positionOffset.x, -5f, 5f);
                if (positionOffset.x != prevPositionOffset.x)
                {
                    ApplyTransformOffsets(new Vector3(positionOffset.x - prevPositionOffset.x, 0, 0), Vector3.zero);
                    if (positionOffset.x == 0f) positionOffset.x = 0f; // Reset to zero if slider is at zero
                    Log.Msg($"Live-updated Position X by {positionOffset.x - prevPositionOffset.x} for selected objects.");
                }

                prevPositionOffset = positionOffset;
                Menu.ModernSlider("Position Up/Down", ref positionOffset.y, -5f, 5f);
                if (positionOffset.y != prevPositionOffset.y)
                {
                    ApplyTransformOffsets(new Vector3(0, positionOffset.y - prevPositionOffset.y, 0), Vector3.zero);
                    if (positionOffset.y == 0f) positionOffset.y = 0f; // Reset to zero if slider is at zero
                    Log.Msg($"Live-updated Position Y by {positionOffset.y - prevPositionOffset.y} for selected objects.");
                }

                prevPositionOffset = positionOffset;
                Menu.ModernSlider("Position Z", ref positionOffset.z, -5f, 5f);
                if (positionOffset.z != prevPositionOffset.z)
                {
                    ApplyTransformOffsets(new Vector3(0, 0, positionOffset.z - prevPositionOffset.z), Vector3.zero);
                    if (positionOffset.z == 0f) positionOffset.z = 0f; // Reset to zero if slider is at zero
                    Log.Msg($"Live-updated Position Z by {positionOffset.z - prevPositionOffset.z} for selected objects.");
                }
                GUILayout.Space(5);

                // Rotation sliders with live updates
                Vector3 prevRotationOffset = rotationOffset;
                Menu.ModernSlider("Rotation X", ref rotationOffset.x, -180f, 180f);
                if (rotationOffset.x != prevRotationOffset.x)
                {
                    ApplyTransformOffsets(Vector3.zero, new Vector3(rotationOffset.x - prevRotationOffset.x, 0, 0));
                    if (rotationOffset.x == 0f) rotationOffset.x = 0f;
                    Log.Msg($"Live-updated Rotation X by {rotationOffset.x - prevRotationOffset.x} for selected objects.");
                }

                prevRotationOffset = rotationOffset;
                Menu.ModernSlider("Rotation Y", ref rotationOffset.y, -180f, 180f);
                if (rotationOffset.y != prevRotationOffset.y)
                {
                    ApplyTransformOffsets(Vector3.zero, new Vector3(0, rotationOffset.y - prevRotationOffset.y, 0));
                    if (rotationOffset.y == 0f) rotationOffset.y = 0f; // Reset to zero if slider is at zero
                    Log.Msg($"Live-updated Rotation Y by {rotationOffset.y - prevRotationOffset.y} for selected objects.");
                }

                prevRotationOffset = rotationOffset;
                Menu.ModernSlider("Rotation Z", ref rotationOffset.z, -180f, 180f);
                if (rotationOffset.z != prevRotationOffset.z)
                {
                    ApplyTransformOffsets(Vector3.zero, new Vector3(0, 0, rotationOffset.z - prevRotationOffset.z));
                    if (rotationOffset.z == 0f) rotationOffset.z = 0f; // Reset to zero if slider is at zero
                    Log.Msg($"Live-updated Rotation Z by {rotationOffset.z - prevRotationOffset.z} for selected objects.");
                }
                GUILayout.Space(5);
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
            foreach (var obj in spawnedObjects.ToList())
            {
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
            spawnedObjects.Clear();
            Log.Msg($"Deleted {count} spawned objects.");
        }

        // Apply transform offsets to all selected objects
        private static void ApplyTransformOffsets(Vector3 posOffset, Vector3 rotOffset)
        {
            if (selectedObjects.Count == 0)
            {
                return;
            }

            foreach (var obj in selectedObjects)
            {
                if (obj != null)
                {
                    // Apply position offset
                    obj.transform.position += posOffset;

                    // Apply rotation offset (in Euler angles)
                    Vector3 currentEuler = obj.transform.eulerAngles;
                    currentEuler += rotOffset;
                    obj.transform.eulerAngles = currentEuler;
                }
            }
        }
    }
}