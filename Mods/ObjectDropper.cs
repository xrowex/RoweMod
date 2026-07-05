using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppFusion;
using UnityEngine;
using rowemod.Utils;
using Il2CppMashBox.Addons.ProtoDrone;
using Il2CppMashBox.Addons.CharacterController;
using Il2CppMashBox.Addons.ReplaySystem;
using rowemod;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using Log = rowemod.Utils.Log;
using Object = UnityEngine.Object;


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

        private static readonly int placementLayerMask = ~((1 << 2) | (1 << 31));
        private static readonly float MinGroundNormalY = 0.0872f;

        // Preview
        private static GameObject previewObject;
        private static Material previewMaterial;
        private static float previewBottomOffset;
        private static float previewYRotation = 0f;
        private static UnityEngine.Camera activeCamera;
        private static bool canPlacePreview = false;
        private static GUIStyle notPlaceableWarningStyle;
        private static Vector2 prefabListScroll = Vector2.zero;
        private static Vector2 spawnedListScroll = Vector2.zero;
        private static string prefabSearch = string.Empty;
        private static string dropperStatus = "Select an object, then click in the world to place it.";
        private static float nextAssetLoadAttemptTime = 0f;

        // Initialize the dropper by finding references
        public static void Initialize()
        {
            // Logging initialization start
            Log.Msg("Initializing ObjectDropper...");

            // Initialize cached GUI styles and textures
            
            InitializeToggleStyles();

            RefreshReferences();
        }

        // Initialize or update cached GUI styles and textures for toggles
        private static void InitializeToggleStyles()
        {
            // Create or update textures if menu accent color has changed
            Color currentMenuAccentColor = new Color(Config.misc.menuAccentR, Config.misc.menuAccentG, Config.misc.menuAccentB);
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
            var mouse = Mouse.current;
            if (mouse == null) return;
            RefreshActiveCamera();

            // Handle object spawning
            if (Menu.isOpen && Menu.currentTab == Menu.Tab.Dropper && mouse.leftButton.wasPressedThisFrame)
            {
                Config.misc.disableDroneCollider = true;
                if (IsMouseOverUI()) return;

                if (Memory.dropperPrefabs == null || Memory.dropperPrefabs.Count == 0)
                {
                    SetDropperStatus("No objects are loaded for the dropper.", true);
                    return;
                }

                if (string.IsNullOrEmpty(selectedPrefabName))
                {
                    SetDropperStatus("Select an object before placing.", true);
                    return;
                }

                if (!Memory.dropperPrefabNames.Contains(selectedPrefabName))
                {
                    SetDropperStatus($"Selected object '{selectedPrefabName}' is no longer available.", true);
                    selectedPrefabName = null;
                    DestroyPreviewObject();
                    return;
                }

                int prefabIndex = Memory.dropperPrefabNames.IndexOf(selectedPrefabName);
                if (prefabIndex < 0 || prefabIndex >= Memory.dropperPrefabs.Count)
                {
                    Log.Error($"Selected prefab '{selectedPrefabName}' not found or invalid.");
                    return;
                }

                GameObject prefabToSpawn = Memory.dropperPrefabs[prefabIndex];

                if (activeCamera == null)
                {
                    SetDropperStatus("Cannot place object: no active camera found.", true);
                    return;
                }

                if (previewObject != null && previewObject.activeSelf && !canPlacePreview)
                {
                    SetDropperStatus("Cannot place object here.", true);
                    return;
                }

                // Check if spawned objects exceed the limit
                if (spawnedObjects.Count >= MaxSpawnedObjects)
                {
                    // Remove the oldest object
                    GameObject oldestObject = spawnedObjects[0];
                    if (oldestObject != null)
                    {
                        UnityEngine.Object.Destroy(oldestObject);
                        Log.Msg($"Destroyed oldest spawned object: {oldestObject.name} to stay within {MaxSpawnedObjects} limit.");
                    }
                    spawnedObjects.RemoveAt(0);
                }

                if (previewObject != null && previewObject.activeSelf && canPlacePreview)
                {
                    GameObject previewSpawned = Object.Instantiate(prefabToSpawn, previewObject.transform.position, previewObject.transform.rotation);
                    ConfigureSpawnedObject(previewSpawned);
                    spawnedObjects.Add(previewSpawned);
                    SetDropperStatus($"Placed {selectedPrefabName}.", false);
                    Log.Msg($"Spawned object from preview at {previewObject.transform.position}, rotation: {previewObject.transform.rotation.eulerAngles}");
                    return;
                }

                // Use mouse raycast for placement
                Ray ray = activeCamera.ScreenPointToRay(mouse.position.ReadValue());
                if (TryRaycastIgnoringSpawned(ray, out RaycastHit hit))
                {
                    Vector3 groundNormal = hit.normal.normalized;
                    Quaternion rotation;

                    // Check if slope is within 85 degrees (cos(85°) ≈ 0.0872)
                    if (groundNormal.y > MinGroundNormalY)
                    {
                        // Align with normal, using previewYRotation for forward direction
                        Vector3 forward = Quaternion.Euler(0f, previewYRotation, 0f) * Vector3.forward;
                        forward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;
                        rotation = Quaternion.LookRotation(forward, groundNormal);
                        Log.Msg($"Mouse raycast hit ground with normal: {groundNormal}, rotation: {rotation.eulerAngles}");
                    }
                    else
                    {
                        // Fallback to flat rotation with previewYRotation
                        rotation = Quaternion.Euler(0f, previewYRotation, 0f);
                        Log.Warning($"Mouse raycast normal too steep: {groundNormal} (y={groundNormal.y}). Using flat rotation.");
                    }

                    Vector3 position = hit.point;

                    float bottomOffset = 0f;
                    GameObject temp = Object.Instantiate(prefabToSpawn, Vector3.zero, rotation);
                    var renderers = temp.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                        bottomOffset = -bounds.min.y;
                    }
                    Object.Destroy(temp);
                    position.y += bottomOffset;
                    
                    
                    
                    GameObject spawned = Object.Instantiate(prefabToSpawn, position, rotation);
                    

                    ConfigureSpawnedObject(spawned);
                    spawnedObjects.Add(spawned);
                    SetDropperStatus($"Placed {selectedPrefabName}.", false);
                    Log.Msg($"Spawned object under mouse at {position}, rotation: {rotation.eulerAngles}");
                }
                else
                {
                    SetDropperStatus("Cannot place object: no valid surface under cursor.", true);
                }
            }

            // Deselect objects on menu toggle
            bool isMenuOpen = Menu.isOpen;
            if (isMenuOpen != wasMenuOpen)
            {
                if (selectedObjects.Count > 0)
                {
                    ClearSelection();
                    UnityEngine.Object.Destroy(previewObject);
                    previewObject = null;
                    Log.Msg("Deselected all objects due to menu toggle.");
                }
                wasMenuOpen = isMenuOpen;
            }
            
            // Scroll to rotate preview
            float scrollDelta = 0f;

            if (Mouse.current != null)
            {
                // Normalize OS delta (e.g., 120 on Windows) into “notches” (~1 per step)
                scrollDelta = Mouse.current.scroll.ReadValue().y / 120f;
            }

            if (scrollDelta != 0f)
            {
                previewYRotation += scrollDelta * 5f; // same multiplier you had
                previewYRotation %= 360f;
            }

            RefreshActiveCamera();

            // Update preview position/rotation
            // Disable preview object if not on Dropper tab
            if (!Menu.isOpen || Menu.currentTab != Menu.Tab.Dropper)
            {
                if (previewObject != null && previewObject.activeSelf)
                    previewObject.SetActive(false);
            }
            else
            {
                if (string.IsNullOrEmpty(selectedPrefabName))
                {
                    DestroyPreviewObject();
                    return;
                }

                if (previewObject != null && !previewObject.activeSelf)
                    previewObject.SetActive(true);

                if (activeCamera != null && previewObject != null)
                {
                    Ray ray = activeCamera.ScreenPointToRay(mouse.position.ReadValue());
                    UpdatePreviewTransform(ray);
                }
            }
        }

        private static void RefreshActiveCamera()
        {
            if (activeCamera != null)
                return;

            var brain = GameObject.FindObjectOfType<Il2CppCinemachine.CinemachineBrain>();
            if (brain != null && brain.gameObject.TryGetComponent(out UnityEngine.Camera cam))
            {
                activeCamera = cam;
                Log.Msg($"Active camera assigned: {cam.name}");
                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera != null)
            {
                activeCamera = mainCamera;
                Log.Msg($"Active camera assigned from Camera.main: {mainCamera.name}");
            }
        }

        private static void UpdatePreviewTransform(Ray ray)
        {
            if (!TryRaycastIgnoringSpawned(ray, out RaycastHit hit))
            {
                canPlacePreview = false;
                SetPreviewColor(false);
                return;
            }

            canPlacePreview = true;
            SetPreviewColor(true);

            previewObject.transform.rotation = GetPlacementRotation(hit.normal.normalized);

            Vector3 adjustedPosition = hit.point;
            adjustedPosition.y += previewBottomOffset;
            previewObject.transform.position = adjustedPosition;
        }

        private static Quaternion GetPlacementRotation(Vector3 groundNormal)
        {
            if (groundNormal.y <= MinGroundNormalY)
                return Quaternion.Euler(0f, previewYRotation, 0f);

            Vector3 forward = Quaternion.Euler(0f, previewYRotation, 0f) * Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;

            return Quaternion.LookRotation(forward, groundNormal);
        }

        private static float GetBottomOffset(GameObject obj)
        {
            if (obj == null)
                return 0f;

            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 0f;

            Bounds bounds = renderers[0].bounds;

            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            return obj.transform.position.y - bounds.min.y;
        }

        private static void SetPreviewColor(bool canPlace)
        {
            if (previewMaterial == null)
                return;

            Color color = canPlace
                ? new Color(0f, 1f, 0f, 0.45f)
                : new Color(1f, 0f, 0f, 0.45f);

            try
            {
                previewMaterial.color = color;

                if (previewMaterial.HasProperty("_BaseColor"))
                    previewMaterial.SetColor("_BaseColor", color);

                if (previewMaterial.HasProperty("_EmissiveColor"))
                    previewMaterial.SetColor("_EmissiveColor", color);

                if (previewMaterial.HasProperty("_EmissionColor"))
                    previewMaterial.SetColor("_EmissionColor", color);
            }
            catch (Exception ex)
            {
                Log.Warning($"[Dropper] Failed to update preview color: {ex.Message}");
            }
        }

        private static bool TryRaycastIgnoringSpawned(Ray ray, out RaycastHit validHit)
        {
            validHit = default;

            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(ray, 100f, placementLayerMask);
            if (hits.Length == 0)
                return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                GameObject root = hit.collider.transform.root.gameObject;

                if (previewObject != null && root == previewObject)
                    continue;

                bool isSpawned = false;
                foreach (var spawned in spawnedObjects)
                {
                    if (spawned != null && root == spawned)
                    {
                        isSpawned = true;
                        break;
                    }
                }

                if (isSpawned || hit.normal.y < MinGroundNormalY)
                    continue;

                validHit = hit;
                return true;
            }

            return false;
        }

        private static void ConfigureSpawnedObject(GameObject spawned)
        {
            if (spawned == null)
                return;

            if (spawned.GetComponent<Rigidbody>() != null)
            {
                spawned.AddComponent<RecordableBody>();
                Log.Msg($"Added RecordableBody to {spawned.name} as it has a Rigidbody component");
            }

            if (spawned.GetComponentInChildren<AimConstraint>() == null || Memory.chassisRb == null)
                return;

            Log.Msg("Object has an AimConstraint component");
            var aimConstraints = spawned.GetComponentsInChildren<AimConstraint>();

            foreach (var aimConstraint in aimConstraints)
            {
                aimConstraint.SetSources(new Il2CppSystem.Collections.Generic.List<ConstraintSource>());

                ConstraintSource source = new ConstraintSource
                {
                    sourceTransform = Memory.chassisRb.transform,
                    weight = 1.0f
                };

                Log.Msg("AimConstraint sourceTransform set to " + Memory.chassisRb.transform.name);

                aimConstraint.AddSource(source);
                aimConstraint.constraintActive = true;
                aimConstraint.locked = true;
                aimConstraint.enabled = false;
                aimConstraint.enabled = true;
            }
        }

        private static bool IsMouseOverUI()
        {
            // Convert GUI space to screen space for your windowRect
            Rect screenRect = new Rect(
                Menu.windowRect.x,
                Menu.windowRect.y,
                Menu.windowRect.width,
                Menu.windowRect.height
            );
            
            var mouse = Mouse.current;
            if (mouse == null) return false;
            
            // Flip Y since GUI and Screen have different origins
            float flippedY = Screen.height - mouse.position.y.ReadValue();
            Vector2 mousePos = new Vector2(mouse.position.x.ReadValue(), flippedY);

            return screenRect.Contains(mousePos);
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
                SetDropperStatus("No spawned objects are selected.", true);
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
            SetDropperStatus($"Deleted {deletedCount} selected object(s).", false);
            Log.Msg($"Deleted {deletedCount} selected objects.");

            // Reset transform offsets after deletion
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
        }


        // Draw the UI for the Dropper tab
        public static void DrawDropperTab()
        {
            InitializeToggleStyles();
            EnsureDropperAssetsLoaded(false);

            if (Memory.dropperPrefabs == null || Memory.dropperPrefabNames.Count == 0)
            {
                DestroyPreviewObject();
                Menu.BeginPane("Object Dropper", "Dropper objects load from bundled prefabs named dropper_*.");
                GUILayout.Label("No objects available to spawn.", Menu.UiHeaderStyle);
                GUILayout.Label("Load a bundle with dropper prefabs, then refresh this tab.", Menu.UiMutedWrappedStyle);
                GUILayout.Space(10f);
                if (Menu.PrimaryButton("Refresh Objects", GUILayout.Width(150f), GUILayout.Height(26f)))
                {
                    EnsureDropperAssetsLoaded(true);
                }
                Menu.EndPane();
                return;
            }

            spawnedObjects.RemoveAll(o => o == null);
            selectedObjects.RemoveAll(o => o == null);

            float paneHeight = Menu.GetContentPaneHeight(100f);
            float leftWidth = Mathf.Max(260f, Menu.windowRect.width * 0.34f);

            Menu.BeginTwoPane(paneHeight);

            Menu.BeginPane("Object Library", "Pick an object to preview, then click outside the menu to place it.", GUILayout.Width(leftWidth), GUILayout.Height(paneHeight));
            Menu.BeginToolbar();
            Menu.SearchRow(ref prefabSearch, Mathf.Max(160f, leftWidth - 115f), "Search");
            Menu.EndToolbar();

            string normalizedSearch = prefabSearch?.Trim() ?? string.Empty;
            List<string> filteredPrefabs = Memory.dropperPrefabNames
                .Where(name => string.IsNullOrEmpty(normalizedSearch) || name.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (filteredPrefabs.Count == 0)
            {
                Menu.DrawEmptyState("No matching objects.", "Clear the search filter to show all dropper objects.");
            }
            else
            {
                prefabListScroll = GUILayout.BeginScrollView(prefabListScroll, false, true, GUILayout.ExpandHeight(true));
                foreach (string prefabName in filteredPrefabs)
                {
                    bool isSelectedPrefab = string.Equals(selectedPrefabName, prefabName, StringComparison.Ordinal);
                    if (GUILayout.Button(prefabName, isSelectedPrefab ? Menu.UiRowButtonSelectedStyle : Menu.UiRowButtonStyle, GUILayout.Height(26f)))
                    {
                        SelectPrefab(prefabName);
                    }
                }
                GUILayout.EndScrollView();
            }

            Menu.EndPane();

            Menu.BeginPane("Placement", "Preview follows the cursor. Mouse wheel rotates the selected object.", GUILayout.ExpandWidth(true), GUILayout.Height(paneHeight));

            GUILayout.Label($"Selected: {(selectedPrefabName ?? "None")}", Menu.UiHeaderStyle);
            GUILayout.Label(dropperStatus, Menu.UiMutedWrappedStyle);

            Menu.BeginToolbar();
            if (Menu.SecondaryButton("Clear Selection", GUILayout.Width(130f), GUILayout.Height(26f)))
            {
                selectedPrefabName = null;
                DestroyPreviewObject();
                SetDropperStatus("Selection cleared.", false);
            }

            if (Menu.DangerButton("Delete Selected", GUILayout.Width(130f), GUILayout.Height(26f)))
            {
                DeleteSelectedObjects();
            }

            if (Menu.DangerButton("Delete All", GUILayout.Width(105f), GUILayout.Height(26f)))
            {
                DeleteAllSpawnedObjects();
            }
            Menu.EndToolbar();

            GUILayout.Space(8f);
            Menu.DrawSectionTitle($"Spawned Objects ({spawnedObjects.Count}/{MaxSpawnedObjects})", $"{selectedObjects.Count} selected.");
            if (spawnedObjects.Count == 0)
            {
                Menu.DrawEmptyState("No objects spawned.", "Select an object, then click in the world to place it.");
            }
            else
            {
                spawnedListScroll = GUILayout.BeginScrollView(spawnedListScroll, false, true, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
                for (int i = 0; i < spawnedObjects.Count; i++)
                {
                    GameObject obj = spawnedObjects[i];
                    if (obj == null) continue;

                    string displayName = "(Unknown)";

                    if (Memory.dropperPrefabNames != null)
                    {
                        displayName = Memory.dropperPrefabNames
                            .FirstOrDefault(name => obj.name.Contains(name));
                    }

                    if (string.IsNullOrEmpty(displayName))
                        displayName = obj.name;

                    string label = $"Object {i + 1}: {displayName}";
                    bool isSelected = selectedObjects.Contains(obj);
                    if (GUILayout.Button(label, isSelected ? Menu.UiRowButtonSelectedStyle : Menu.UiRowButtonStyle, GUILayout.Height(24f)))
                    {
                        ToggleObjectSelection(obj);
                    }
                }
                GUILayout.EndScrollView();
            }

            if (selectedObjects.Count > 0)
            {
                GUILayout.Space(8f);
                Menu.BeginAltPane("Transform Controls", "Offsets apply live to the selected spawned objects.");

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
                        if (rotationOffset.x == 0f) rotationOffset.x = 0f; // Reset to zero if slider is at zero
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
                Menu.EndPane();
            }

            Menu.EndPane();
            Menu.EndTwoPane();
        }

        private static void EnsureDropperAssetsLoaded(bool forceReload)
        {
            if (!forceReload && Memory.dropperPrefabs != null && Memory.dropperPrefabs.Count > 0)
                return;

            float now = Time.unscaledTime;
            if (!forceReload && now < nextAssetLoadAttemptTime)
                return;

            nextAssetLoadAttemptTime = now + 5f;

            try
            {
                Memory.LoadAllAssetBundles(forceReload);
                if (Memory.dropperPrefabs != null && Memory.dropperPrefabs.Count > 0)
                    SetDropperStatus($"Loaded {Memory.dropperPrefabs.Count} dropper object(s).", false);
            }
            catch (Exception ex)
            {
                SetDropperStatus("Could not load dropper bundles: " + ex.Message, true);
            }
        }

        private static void SelectPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return;

            int index = Memory.dropperPrefabNames.IndexOf(prefabName);
            if (index < 0 || index >= Memory.dropperPrefabs.Count)
            {
                SetDropperStatus($"Could not find object '{prefabName}'.", true);
                selectedPrefabName = null;
                DestroyPreviewObject();
                return;
            }

            selectedPrefabName = prefabName;
            previewYRotation = 0f;
            Log.Msg($"Selected prefab: {selectedPrefabName}");

            CreatePreviewMaterial();
            if (previewMaterial == null)
            {
                SetDropperStatus("Could not create preview material.", true);
                DestroyPreviewObject();
                return;
            }

            BuildPreviewObject(Memory.dropperPrefabs[index]);
            SetDropperStatus($"Selected {selectedPrefabName}. Click outside the menu to place it.", false);
        }

        private static void SetDropperStatus(string message, bool warning)
        {
            dropperStatus = message;
            if (warning)
                Log.Warning("[Dropper] " + message);
        }

        private static void DestroyPreviewObject()
        {
            if (previewObject != null)
            {
                UnityEngine.Object.Destroy(previewObject);
                previewObject = null;
            }

            canPlacePreview = false;
            previewBottomOffset = 0f;
        }

        // Reset the Dropper tab to default state
        public static void ResetTab()
        {
            // Resetting selected prefab and clear selections
            selectedPrefabName = null;
            ClearSelection();
            DestroyPreviewObject();
            Config.misc.disableDroneCollider = false;
            dropperStatus = "Select an object, then click in the world to place it.";
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
            SetDropperStatus($"Deleted {count} spawned object(s).", false);
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

        private static void CreatePreviewMaterial()
        {
            if (previewMaterial != null) return;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Log.Error("Could not find HDRP/Lit or Standard shader. Cannot create preview material.");
                return;
            }

            previewMaterial = new Material(shader);

            if (previewMaterial.HasProperty("_SurfaceType"))
                previewMaterial.SetFloat("_SurfaceType", 1f);

            if (previewMaterial.HasProperty("_BlendMode"))
                previewMaterial.SetFloat("_BlendMode", 0f);

            if (previewMaterial.HasProperty("_ZWrite"))
                previewMaterial.SetFloat("_ZWrite", 0f);

            if (previewMaterial.HasProperty("_AlphaCutoffEnable"))
                previewMaterial.SetFloat("_AlphaCutoffEnable", 0f);

            if (previewMaterial.HasProperty("_CullMode"))
                previewMaterial.SetFloat("_CullMode", 2f);

            if (previewMaterial.HasProperty("_SrcBlend"))
                previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (previewMaterial.HasProperty("_DstBlend"))
                previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            previewMaterial.DisableKeyword("_ALPHATEST_ON");
            previewMaterial.EnableKeyword("_ALPHABLEND_ON");
            previewMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            SetPreviewColor(true);
        }

        private static void BuildPreviewObject(GameObject prefab)
        {
            DestroyPreviewObject();

            if (prefab == null)
            {
                SetDropperStatus("Selected object prefab is missing.", true);
                return;
            }

            if (previewMaterial == null)
            {
                SetDropperStatus("Preview material is missing.", true);
                return;
            }

            previewObject = UnityEngine.Object.Instantiate(prefab);
            previewObject.name = "[Preview] " + prefab.name;
            previewBottomOffset = GetBottomOffset(previewObject);
            canPlacePreview = false;

            foreach (var renderer in previewObject.GetComponentsInChildren<Renderer>())
            {
                var mats = renderer.materials;

                for (int i = 0; i < mats.Length; i++)
                    mats[i] = previewMaterial;

                renderer.materials = mats;
            }

            // Disable physics so preview doesn't move or collide
            foreach (var rb in previewObject.GetComponentsInChildren<Rigidbody>())
            {
                UnityEngine.Object.Destroy(rb);
            }

            // Disable all colliders to prevent raycast interference
            foreach (var collider in previewObject.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.Destroy(collider);
            }

            int previewLayer = 0;
            previewObject.layer = previewLayer;
            foreach (Transform child in previewObject.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = previewLayer;
            }

            SetPreviewColor(true);
        }

        public static void DrawNotPlaceableWarning()
        {
            if (!Menu.isOpen || Menu.currentTab != Menu.Tab.Dropper || previewObject == null || !previewObject.activeSelf || canPlacePreview)
                return;

            if (notPlaceableWarningStyle == null)
            {
                notPlaceableWarningStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                notPlaceableWarningStyle.normal.textColor = Color.red;
            }

            GUI.Label(
                new Rect(Screen.width * 0.5f - 200f, Screen.height - 130f, 400f, 50f),
                "NOT PLACEABLE",
                notPlaceableWarningStyle
            );
        }

        public static bool ShouldBlockDroneShot()
        {
            return Menu.isOpen
                   && Menu.currentTab == Menu.Tab.Dropper
                   && !string.IsNullOrEmpty(selectedPrefabName);
        }
    }
}
