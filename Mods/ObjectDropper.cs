using Il2CppFusion;
using UnityEngine;
using rowemod.Utils;
using Il2CppMashBox.Addons.ProtoDrone;
using Il2CppMashBox.Addons.CharacterController;
using Il2CppMashBox.Addons.ReplaySystem;
using rowemod;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using Log = rowemod.Utils.Log;
using Object = UnityEngine.Object;

namespace rowemod.Mods
{
    public static class ObjectDropper
    {
        private static readonly List<GameObject> spawnedObjects = new List<GameObject>();

        private static Transform droneTransform;
        private static Transform characterTransform;

        private static string selectedPrefabName = null;

        private static readonly List<GameObject> selectedObjects = new List<GameObject>();
        private static readonly Dictionary<GameObject, List<Material>> originalMaterialsMap = new Dictionary<GameObject, List<Material>>();

        private static readonly int MaxSpawnedObjects = 20;

        private static GUIStyle toggleOnStyle;
        private static GUIStyle toggleOffStyle;
        private static Texture2D toggleOnNormalTexture;
        private static Texture2D toggleOnHoverTexture;
        private static Texture2D toggleOffNormalTexture;
        private static Texture2D toggleOffHoverTexture;
        private static Color lastMenuAccentColor;

        private static Vector3 positionOffset = Vector3.zero;
        private static Vector3 rotationOffset = Vector3.zero;

        private static bool wasMenuOpen = false;

        private static readonly int placementLayerMask = ~((1 << 2) | (1 << 31));
        private static readonly float MinGroundNormalY = 0.0872f;

        private static GameObject previewObject;
        private static Material previewMaterial;
        private static float previewBottomOffset;
        private static float previewYRotation = 0f;
        private static UnityEngine.Camera activeCamera;
        private static bool cachedDroneModeActive;
        private static float nextRuntimeReferenceRefreshTime;
        private static readonly float RuntimeReferenceRefreshInterval = 0.5f;
        private static float nextPreviewUpdateTime;
        private static readonly float PreviewUpdateInterval = 1f / 30f;

        private static bool dropperExpanded = true;
        private static bool transformControlsExpanded = true;

        private static float leftShoulderHoldTime = 0f;
        private static float rightShoulderHoldTime = 0f;
        private static readonly float HoldThreshold = 0.25f;
        private static readonly float FreeRotateSpeed = 90f;
        private static readonly float PreciseClickStep = 5f;

        private static float previewDistanceOffset = 0f;
        private static readonly float DistanceMoveSpeed = 8f;
        private static readonly float MinDistanceOffset = -10f;
        private static readonly float MaxDistanceOffset = 25f;

        private static bool canPlacePreview = false;
        private static GUIStyle notPlaceableWarningStyle;

        private static readonly bool DebugLoggingEnabled = false;
        private static bool lastLoggedDropperActive;
        private static bool lastLoggedPlacementActive;
        private static bool lastLoggedHasCamera;
        private static bool lastLoggedHasGamepad;
        private static bool lastLoggedHasSelection;
        private static bool lastLoggedCanPlacePreview;
        private static bool hasLoggedDropperState;
        private static float nextPlacementFailureLogTime;
        private static float nextNoPrefabsLogTime;
        private static string lastPlacementFailureReason;

        public static void Initialize()
        {
            InitializeToggleStyles();
            RefreshReferences();
            LogDropper("Initialized.");
        }

        public static void ResetTab()
        {
            ClearSelection();

            if (previewObject != null)
            {
                Object.Destroy(previewObject);
                previewObject = null;
            }

            if (previewMaterial != null)
            {
                Object.Destroy(previewMaterial);
                previewMaterial = null;
            }

            selectedPrefabName = null;
            previewYRotation = 0f;
            previewDistanceOffset = 0f;
            previewBottomOffset = 0f;
            canPlacePreview = false;
            LogDropper("Reset tab and cleared preview/selection.");
        }

        private static void InitializeToggleStyles()
        {
            Color currentMenuAccentColor = new Color(Config.misc.menuAccentR, Config.misc.menuAccentG, Config.misc.menuAccentB);

            if (toggleOnNormalTexture == null || lastMenuAccentColor != currentMenuAccentColor)
            {
                toggleOnNormalTexture = Menu.MakeRoundedTex(128, 20, Color.red, 10, 1, Color.black);
                toggleOnHoverTexture = Menu.MakeRoundedTex(128, 20, new Color(1f, 0.1f, 0.1f), 10, 1, Color.black);

                toggleOffNormalTexture = Menu.MakeRoundedTex(128, 20, currentMenuAccentColor, 10, 1, Color.black);
                toggleOffHoverTexture = Menu.MakeRoundedTex(128, 20, new Color(
                    Mathf.Min(currentMenuAccentColor.r + 0.1f, 1f),
                    Mathf.Min(currentMenuAccentColor.g + 0.1f, 1f),
                    Mathf.Min(currentMenuAccentColor.b + 0.1f, 1f)), 10, 1, Color.black);

                toggleOnStyle = new GUIStyle(Menu.highQualityButtonStyle)
                {
                    normal = { background = toggleOnNormalTexture, textColor = Color.white },
                    hover = { background = toggleOnHoverTexture, textColor = Color.yellow },
                    active = { background = toggleOnHoverTexture, textColor = Color.green },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    border = new RectOffset(10, 10, 10, 10),
                    contentOffset = new Vector2(0, 0),
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
                    contentOffset = new Vector2(0, 0),
                    stretchWidth = false,
                    stretchHeight = false
                };

                lastMenuAccentColor = currentMenuAccentColor;
            }
        }

        public static void Update()
        {
            var mouse = Mouse.current;
            var gamepad = Gamepad.current;

            RefreshRuntimeReferences();

            bool dropperMenuActive = IsDropperTabActive;
            bool placementActive = dropperMenuActive && cachedDroneModeActive;
            LogDropperState(dropperMenuActive, placementActive, activeCamera != null, gamepad != null);

            if (placementActive && gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                if (activeCamera != null)
                {
                    Config.misc.disableDroneCollider = true;
                    LogDropper("Place input detected from drone buttonSouth.");
                    TrySpawnSelectedPrefab(GetCameraCenterRay(), "drone");
                }
                else
                {
                    LogDropper("buttonSouth was pressed, but activeCamera is null.");
                }
            }

            if (placementActive && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (activeCamera == null)
                {
                    LogDropper("Mouse placement click ignored because activeCamera is null.");
                }
                else if (IsMouseOverUI())
                {
                    LogDropper("Mouse placement click ignored because cursor is over the menu UI.");
                }
                else
                {
                    Config.misc.disableDroneCollider = true;
                    LogDropper("Place input detected from mouse left click.");
                    TrySpawnSelectedPrefab(activeCamera.ScreenPointToRay(mouse.position.ReadValue()), "mouse");
                }
            }

            bool isMenuOpen = Menu.isOpen;

            if (isMenuOpen != wasMenuOpen)
            {
                if (selectedObjects.Count > 0)
                {
                    ClearSelection();

                    if (previewObject != null)
                    {
                        Object.Destroy(previewObject);
                        previewObject = null;
                        LogDropper("Menu open state changed; destroyed preview while clearing selection.");
                    }
                }

                wasMenuOpen = isMenuOpen;
            }

            spawnedObjects.RemoveAll(o => o == null);
            selectedObjects.RemoveAll(o => o == null);

            UpdatePreviewRotation(mouse, gamepad, placementActive);

            bool shouldShowPreview = previewObject != null && placementActive;

            if (!shouldShowPreview)
            {
                if (previewObject != null && previewObject.activeSelf)
                {
                    previewObject.SetActive(false);
                    LogDropper($"Preview hidden. dropperActive={dropperMenuActive}, placementActive={placementActive}.");
                }
            }
            else
            {
                if (!previewObject.activeSelf)
                {
                    previewObject.SetActive(true);
                    LogDropper("Preview shown.");
                }

                if (activeCamera != null)
                {
                    bool forcePreviewUpdate = (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                                              || (mouse != null && mouse.leftButton.wasPressedThisFrame);

                    if (forcePreviewUpdate || Time.unscaledTime >= nextPreviewUpdateTime)
                    {
                        nextPreviewUpdateTime = Time.unscaledTime + PreviewUpdateInterval;
                        Ray previewRay = GetCameraCenterRay();
                        UpdatePreviewTransform(previewRay);
                    }
                }
            }
        }

        public static bool IsDropperTabActive => Menu.isOpen && Menu.currentTab == Menu.Tab.Dropper;

        public static bool ShouldBlockDroneShot()
        {
            return IsDropperTabActive
                   && !string.IsNullOrEmpty(selectedPrefabName);
        }

        private static void RefreshRuntimeReferences(bool force = false)
        {
            if (!force && Time.unscaledTime < nextRuntimeReferenceRefreshTime)
                return;

            nextRuntimeReferenceRefreshTime = Time.unscaledTime + RuntimeReferenceRefreshInterval;
            RefreshActiveCamera();
            cachedDroneModeActive = IsDroneModeActive();
        }

        private static bool IsDroneModeActive()
        {
            var droneManager = GameObject.FindObjectOfType<DroneManager>(true);

            if (droneManager != null && droneManager.IsDeployed)
                return true;

            var droneController = GameObject.FindObjectOfType<DroneController>(true);

            if (droneController == null)
                return false;

            return droneController.enabled && droneController.gameObject.activeInHierarchy;
        }

        private static void RefreshActiveCamera()
        {
            var brain = GameObject.FindObjectOfType<Il2CppCinemachine.CinemachineBrain>();

            if (brain != null && brain.gameObject.TryGetComponent(out UnityEngine.Camera cam))
            {
                if (activeCamera != cam)
                {
                    activeCamera = cam;
                }
            }
            else
            {
                activeCamera = null;
            }
        }

        private static Ray GetCameraCenterRay()
        {
            return activeCamera.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private static void UpdatePreviewRotation(Mouse mouse, Gamepad gamepad, bool placementActive)
        {
            if (!placementActive || string.IsNullOrEmpty(selectedPrefabName))
                return;

            float rotationDelta = 0f;

            if (gamepad != null)
            {
                if (gamepad.leftStickButton.wasPressedThisFrame)
                {
                    previewYRotation = 0f;
                    rotationDelta = 0f;
                }

                Vector2 rightStick = gamepad.rightStick.ReadValue();

                if (Mathf.Abs(rightStick.y) > 0.15f)
                {
                    previewDistanceOffset += rightStick.y * DistanceMoveSpeed * Time.deltaTime;
                    previewDistanceOffset = Mathf.Clamp(previewDistanceOffset, MinDistanceOffset, MaxDistanceOffset);
                }

                if (gamepad.leftShoulder.isPressed)
                {
                    leftShoulderHoldTime += Time.deltaTime;

                    if (leftShoulderHoldTime > HoldThreshold)
                    {
                        rotationDelta -= FreeRotateSpeed * Time.deltaTime;
                    }
                }
                else
                {
                    if (gamepad.leftShoulder.wasReleasedThisFrame && leftShoulderHoldTime <= HoldThreshold)
                    {
                        rotationDelta -= PreciseClickStep;
                    }

                    leftShoulderHoldTime = 0f;
                }

                if (gamepad.rightShoulder.isPressed)
                {
                    rightShoulderHoldTime += Time.deltaTime;

                    if (rightShoulderHoldTime > HoldThreshold)
                    {
                        rotationDelta += FreeRotateSpeed * Time.deltaTime;
                    }
                }
                else
                {
                    if (gamepad.rightShoulder.wasReleasedThisFrame && rightShoulderHoldTime <= HoldThreshold)
                    {
                        rotationDelta += PreciseClickStep;
                    }

                    rightShoulderHoldTime = 0f;
                }
            }

            if (rotationDelta == 0f)
                return;

            previewYRotation = Mathf.Repeat(previewYRotation + rotationDelta, 360f);
        }

        private static bool TryGetSelectedPrefab(out GameObject prefabToSpawn)
        {
            prefabToSpawn = null;

            if (Memory.dropperPrefabs == null || Memory.dropperPrefabs.Count == 0)
            {
                Log.Warning("No prefabs available to spawn in ObjectDropper.");
                return false;
            }

            if (string.IsNullOrEmpty(selectedPrefabName))
            {
                Log.Warning("No prefab selected. Please select a prefab in the Object Dropper menu.");
                return false;
            }

            if (!Memory.dropperPrefabNames.Contains(selectedPrefabName))
            {
                Log.Warning($"Selected prefab '{selectedPrefabName}' is no longer valid. Clearing selection.");
                LogDropper($"Known prefab names: {string.Join(", ", Memory.dropperPrefabNames)}");
                selectedPrefabName = null;
                return false;
            }

            int prefabIndex = Memory.dropperPrefabNames.IndexOf(selectedPrefabName);

            if (prefabIndex < 0 || prefabIndex >= Memory.dropperPrefabs.Count)
            {
                Log.Error($"Selected prefab '{selectedPrefabName}' not found or invalid.");
                return false;
            }

            prefabToSpawn = Memory.dropperPrefabs[prefabIndex];

            if (prefabToSpawn == null)
            {
                LogDropper($"Selected prefab '{selectedPrefabName}' resolved to null at index {prefabIndex}.");
                return false;
            }

            return prefabToSpawn != null;
        }

        private static void TrySpawnSelectedPrefab(Ray ray, string source)
        {
            LogDropper($"Spawn attempt. source={source}, selected='{selectedPrefabName ?? "None"}', previewActive={(previewObject != null && previewObject.activeSelf)}, canPlace={canPlacePreview}.");

            if (!TryGetSelectedPrefab(out GameObject prefabToSpawn))
                return;

            if (previewObject != null && previewObject.activeSelf && !canPlacePreview)
            {
                Log.Warning("Cannot place object here.");
                LogDropper($"Blocked spawn because preview is not placeable. lastFailure='{lastPlacementFailureReason ?? "unknown"}'.");
                return;
            }

            if (spawnedObjects.Count >= MaxSpawnedObjects)
            {
                GameObject oldestObject = spawnedObjects[0];

                if (oldestObject != null)
                {
                    LogDropper($"Max spawned objects reached ({MaxSpawnedObjects}); destroying oldest '{oldestObject.name}'.");
                    Object.Destroy(oldestObject);
                }

                spawnedObjects.RemoveAt(0);
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (previewObject != null && previewObject.activeSelf)
            {
                spawnPosition = previewObject.transform.position;
                spawnRotation = previewObject.transform.rotation;
                LogDropper($"Using preview transform. position={FormatVector(spawnPosition)}, rotationY={spawnRotation.eulerAngles.y:F1}.");
            }
            else
            {
                if (!TryRaycastIgnoringSpawned(ray, out RaycastHit hit, out string reason))
                {
                    Log.Warning($"Raycast from {source} did not hit ground and no preview was active. Reason: {reason}");
                    return;
                }

                spawnRotation = GetPlacementRotation(hit.normal.normalized);
                spawnPosition = hit.point;

                GameObject temp = Object.Instantiate(prefabToSpawn, Vector3.zero, spawnRotation);
                spawnPosition.y += GetBottomOffset(temp);
                Object.Destroy(temp);
                LogDropper($"Using raycast hit '{hit.collider?.name ?? "unknown"}'. position={FormatVector(spawnPosition)}, normal={FormatVector(hit.normal)}.");
            }

            GameObject spawned = Object.Instantiate(prefabToSpawn, spawnPosition, spawnRotation);

            if (spawned != null && spawned.GetComponent<Rigidbody>() != null)
            {
                spawned.AddComponent<RecordableBody>();
            }

            ApplyAimConstraints(spawned);
            spawnedObjects.Add(spawned);
            LogDropper($"Spawned '{spawned?.name ?? "null"}'. totalSpawned={spawnedObjects.Count}.");
        }

        private static Quaternion GetPlacementRotation(Vector3 groundNormal)
        {
            if (groundNormal.y <= 0.0872f)
            {
                return Quaternion.Euler(0f, previewYRotation, 0f);
            }

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

            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            return obj.transform.position.y - bounds.min.y;
        }

        private static void ApplyAimConstraints(GameObject spawned)
        {
            if (spawned == null || spawned.GetComponentInChildren<AimConstraint>() == null)
                return;

            if (Memory.chassisRb == null)
            {
                Log.Warning("chassisRb is null; skipping AimConstraint setup.");
                return;
            }

            var aimConstraints = spawned.GetComponentsInChildren<AimConstraint>();

            foreach (var aimConstraint in aimConstraints)
            {
                aimConstraint.SetSources(new Il2CppSystem.Collections.Generic.List<ConstraintSource>());

                ConstraintSource source = new ConstraintSource
                {
                    sourceTransform = Memory.chassisRb.transform,
                    weight = 1.0f
                };

                aimConstraint.AddSource(source);
                aimConstraint.constraintActive = true;
                aimConstraint.locked = true;

                aimConstraint.enabled = false;
                aimConstraint.enabled = true;
            }
        }

        private static void UpdatePreviewTransform(Ray ray)
        {
            if (!TryGetPlacementHit(ray, out RaycastHit hit, out string reason))
            {
                canPlacePreview = false;
                SetPreviewColor(false);
                LogPlacementFailure(reason);
                return;
            }

            canPlacePreview = true;
            LogPlacementRecovered();
            SetPreviewColor(true);

            Quaternion rotation = GetPlacementRotation(hit.normal.normalized);
            previewObject.transform.rotation = rotation;

            Vector3 adjustedPosition = hit.point;
            adjustedPosition.y += previewBottomOffset;
            previewObject.transform.position = adjustedPosition;
        }

        private static bool TryGetPlacementHit(Ray cameraRay, out RaycastHit hit, out string reason)
        {
            hit = default;
            reason = string.Empty;

            if (!TryRaycastIgnoringSpawned(cameraRay, out RaycastHit baseHit, out reason))
                return false;

            if (Mathf.Approximately(previewDistanceOffset, 0f))
            {
                hit = baseHit;
                return true;
            }

            Vector3 groundForward = Vector3.ProjectOnPlane(cameraRay.direction, Vector3.up);

            if (groundForward.sqrMagnitude < 0.001f)
            {
                hit = baseHit;
                return true;
            }

            groundForward.Normalize();

            Vector3 targetPoint = baseHit.point + groundForward * previewDistanceOffset;
            Ray groundProbeRay = new Ray(targetPoint + Vector3.up * 30f, Vector3.down);

            if (!TryRaycastIgnoringSpawned(groundProbeRay, out RaycastHit offsetHit, out reason))
            {
                reason = $"Offset ground probe failed after base hit. offset={previewDistanceOffset:F2}. {reason}";
                return false;
            }

            hit = offsetHit;
            return true;
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
            return TryRaycastIgnoringSpawned(ray, out validHit, out _);
        }

        private static bool TryRaycastIgnoringSpawned(Ray ray, out RaycastHit validHit, out string reason)
        {
            validHit = default;
            reason = string.Empty;

            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(ray, 100f, placementLayerMask);

            if (hits.Length == 0)
            {
                reason = $"Raycast found 0 hits. origin={FormatVector(ray.origin)}, direction={FormatVector(ray.direction)}, mask={placementLayerMask}.";
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            int skippedPreview = 0;
            int skippedSpawned = 0;
            int skippedNormal = 0;

            foreach (var hit in hits)
            {
                GameObject root = hit.collider.transform.root.gameObject;

                if (previewObject != null && root == previewObject)
                {
                    skippedPreview++;
                    continue;
                }

                bool isSpawned = false;

                foreach (var spawned in spawnedObjects)
                {
                    if (spawned != null && root == spawned)
                    {
                        isSpawned = true;
                        break;
                    }
                }

                if (isSpawned)
                {
                    skippedSpawned++;
                    continue;
                }

                if (hit.normal.y < MinGroundNormalY)
                {
                    skippedNormal++;
                    continue;
                }

                validHit = hit;
                return true;
            }

            reason = $"Raycast had {hits.Length} hits but no valid placement hit. skippedPreview={skippedPreview}, skippedSpawned={skippedSpawned}, skippedSteepNormal={skippedNormal}, minNormalY={MinGroundNormalY:F4}.";
            return false;
        }

        private static bool IsMouseOverUI()
        {
            Rect screenRect = new Rect(Menu.windowRect.x, Menu.windowRect.y, Menu.windowRect.width, Menu.windowRect.height);
            var mouse = Mouse.current;

            if (mouse == null)
                return false;

            float flippedY = Screen.height - mouse.position.y.ReadValue();
            Vector2 mousePos = new Vector2(mouse.position.x.ReadValue(), flippedY);

            return screenRect.Contains(mousePos);
        }

        private static void RefreshReferences()
        {
            var droneController = GameObject.FindObjectOfType<DroneController>(true);

            if (droneController != null && droneController.gameObject.activeInHierarchy)
            {
                droneTransform = droneController.transform;
            }
            else
            {
                droneTransform = null;
                LogDropper("No active drone found while refreshing references.");
            }

            if (Memory.rMbCharacter != null)
            {
                var characterController = Memory.rMbCharacter.GetComponentInChildren<MGCharacterController>();

                if (characterController != null)
                {
                    characterTransform = characterController.transform;
                }
                else
                {
                    characterTransform = Memory.rMbCharacter.transform;
                    LogDropper("Character controller not found in rMbCharacter; using rMbCharacter transform.");
                }
            }
            else
            {
                characterTransform = null;
                Log.Error("rMbCharacter is null, cannot find character transform.");
            }
        }

        private static void ToggleObjectSelection(GameObject obj)
        {
            if (obj == null || !spawnedObjects.Contains(obj))
            {
                Log.Warning("Toggled object is null or not a spawned object.");
                return;
            }

            if (selectedObjects.Contains(obj))
            {
                selectedObjects.Remove(obj);
                RestoreObjectMaterials(obj);
                LogDropper($"Deselected spawned object '{obj.name}'. selectedCount={selectedObjects.Count}.");
            }
            else
            {
                selectedObjects.Add(obj);
                HighlightObject(obj);
                LogDropper($"Selected spawned object '{obj.name}'. selectedCount={selectedObjects.Count}.");
            }
        }

        private static void HighlightObject(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            List<Material> originalMaterials = new List<Material>();

            foreach (Renderer renderer in renderers)
            {
                Material[] mats = renderer.materials;
                originalMaterials.AddRange(mats);

                Material[] highlightMats = new Material[mats.Length];

                for (int i = 0; i < mats.Length; i++)
                {
                    Material highlightMat = new Material(mats[i]);
                    highlightMat.color = Color.green;
                    highlightMats[i] = highlightMat;
                }

                renderer.materials = highlightMats;
            }

            originalMaterialsMap[obj] = originalMaterials;
        }

        private static void RestoreObjectMaterials(GameObject obj)
        {
            if (!originalMaterialsMap.TryGetValue(obj, out var originalMaterials))
                return;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            int materialIndex = 0;

            foreach (Renderer renderer in renderers)
            {
                int matCount = renderer.sharedMaterials.Length;
                Material[] mats = new Material[matCount];

                for (int i = 0; i < matCount && materialIndex < originalMaterials.Count; i++, materialIndex++)
                {
                    mats[i] = originalMaterials[materialIndex];
                }

                renderer.sharedMaterials = mats;
            }

            originalMaterialsMap.Remove(obj);
        }

        private static void ClearSelection()
        {
            foreach (var obj in selectedObjects.ToList())
            {
                if (obj != null)
                    RestoreObjectMaterials(obj);
            }

            selectedObjects.Clear();
            originalMaterialsMap.Clear();

            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
            LogDropper("Selection cleared.");
        }

        private static void DeleteSelectedObjects()
        {
            if (selectedObjects.Count == 0)
            {
                Log.Warning("No objects selected to delete.");
                return;
            }

            foreach (var obj in selectedObjects.ToList())
            {
                if (obj != null && spawnedObjects.Contains(obj))
                {
                    Object.Destroy(obj);
                    spawnedObjects.Remove(obj);
                    originalMaterialsMap.Remove(obj);
                }
            }

            selectedObjects.Clear();
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
            LogDropper($"Deleted selected objects. remainingSpawned={spawnedObjects.Count}.");
        }

        private static void DeleteAllSpawnedObjects()
        {
            int deletedCount = spawnedObjects.Count;
            ClearSelection();

            foreach (var obj in spawnedObjects)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }

            spawnedObjects.Clear();
            LogDropper($"Deleted all spawned objects. deletedCount={deletedCount}.");
        }

        private static void ApplyTransformOffsets(Vector3 posDelta, Vector3 rotDelta)
        {
            foreach (var obj in selectedObjects)
            {
                if (obj == null)
                    continue;

                obj.transform.position += posDelta;
                obj.transform.Rotate(rotDelta, Space.World);
            }
        }

        private static void CreatePreviewMaterial()
        {
            if (previewMaterial != null)
                return;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Log.Warning("[Dropper] Could not find HDRP/Lit or Standard shader for preview.");
                return;
            }

            previewMaterial = new Material(shader);
            ConfigurePreviewMaterial(previewMaterial);
            SetPreviewColor(true);
            LogDropper($"Created preview material. shader={(previewMaterial.shader != null ? previewMaterial.shader.name : "null")}.");
        }

        private static void ConfigurePreviewMaterial(Material material)
        {
            if (material == null)
                return;

            try
            {
                if (material.HasProperty("_SurfaceType"))
                    material.SetFloat("_SurfaceType", 1f);

                if (material.HasProperty("_BlendMode"))
                    material.SetFloat("_BlendMode", 0f);

                if (material.HasProperty("_ZWrite"))
                    material.SetFloat("_ZWrite", 0f);

                if (material.HasProperty("_AlphaCutoffEnable"))
                    material.SetFloat("_AlphaCutoffEnable", 0f);

                if (material.HasProperty("_CullMode"))
                    material.SetFloat("_CullMode", 2f);

                if (material.HasProperty("_EmissiveExposureWeight"))
                    material.SetFloat("_EmissiveExposureWeight", 0f);

                if (material.HasProperty("_SrcBlend"))
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

                if (material.HasProperty("_DstBlend"))
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_EMISSION");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            catch (Exception ex)
            {
                Log.Warning($"[Dropper] Failed to configure preview material: {ex.Message}");
            }
        }

        private static void BuildPreviewObject(GameObject prefab)
        {
            if (previewObject != null)
                Object.Destroy(previewObject);

            previewObject = Object.Instantiate(prefab);
            previewObject.name = "Placement_Preview";
            previewBottomOffset = GetBottomOffset(previewObject);
            canPlacePreview = false;

            if (previewObject.TryGetComponent<Rigidbody>(out var rb))
                Object.Destroy(rb);

            var colliders = previewObject.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
                Object.Destroy(col);

            var renderers = previewObject.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var mats = renderer.materials;

                for (int i = 0; i < mats.Length; i++)
                    mats[i] = previewMaterial;

                renderer.materials = mats;
            }

            LogDropper($"Built preview for '{prefab?.name ?? "null"}'. renderers={renderers.Length}, removedColliders={colliders.Length}.");
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                Transform child = obj.transform.GetChild(i);
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public static void DrawNotPlaceableWarning()
        {
            if (!IsDropperTabActive || previewObject == null || !previewObject.activeSelf || canPlacePreview)
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

        public static void DrawDropperTab()
        {
            InitializeToggleStyles();

            dropperExpanded = Menu.ModernFoldout("Object Dropper", dropperExpanded);

            if (!dropperExpanded)
                return;

            if (Memory.dropperPrefabs == null || Memory.dropperPrefabNames == null || Memory.dropperPrefabNames.Count == 0)
            {
                GUILayout.Label("No objects available to spawn.", Menu.labelStyle);
                LogNoPrefabsAvailable();
                return;
            }

            GUILayout.Label("Available Prefabs:", Menu.labelStyle);

            foreach (string prefabName in Memory.dropperPrefabNames)
            {
                if (GUILayout.Button(prefabName, Menu.highQualityButtonStyle, GUILayout.Width(500f), GUILayout.Height(20f)))
                {
                    selectedPrefabName = prefabName;
                    previewYRotation = 0f;
                    previewDistanceOffset = 0f;
                    canPlacePreview = false;

                    int index = Memory.dropperPrefabNames.IndexOf(prefabName);
                    LogDropper($"Selected prefab button '{prefabName}'. index={index}, prefabs={Memory.dropperPrefabs?.Count ?? 0}, names={Memory.dropperPrefabNames?.Count ?? 0}.");

                    if (index >= 0 && index < Memory.dropperPrefabs.Count)
                    {
                        var prefab = Memory.dropperPrefabs[index];
                        CreatePreviewMaterial();

                        if (previewMaterial != null)
                        {
                            BuildPreviewObject(prefab);
                            RefreshRuntimeReferences(true);
                            nextPreviewUpdateTime = 0f;
                        }
                    }
                    else
                    {
                        Log.Warning($"[Dropper] Selected prefab index was invalid. name='{prefabName}', index={index}.");
                    }
                }
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

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

                    if (obj == null)
                        continue;

                    string displayName = Memory.dropperPrefabNames?.FirstOrDefault(name => obj.name.Contains(name));

                    if (string.IsNullOrEmpty(displayName))
                        displayName = obj.name;

                    string label = $"Object {i + 1}: {displayName}";
                    bool isSelected = selectedObjects.Contains(obj);
                    GUIStyle toggleStyle = (isSelected ? toggleOnStyle : toggleOffStyle) ?? Menu.highQualityButtonStyle;

                    if (GUILayout.Button(label, toggleStyle, GUILayout.Width(500f), GUILayout.Height(20f)))
                    {
                        ToggleObjectSelection(obj);
                    }
                }
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Space(30);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();

            if (GUILayout.Button("<b>Delete All Spawned Objects</b>", Menu.highQualityButtonStyle, GUILayout.Width(200f), GUILayout.Height(30f)))
            {
                DeleteAllSpawnedObjects();
            }

            if (GUILayout.Button("<b>Delete Selected Objects</b>", Menu.highQualityButtonStyle, GUILayout.Width(200f), GUILayout.Height(30f)))
            {
                DeleteSelectedObjects();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            if (selectedObjects.Count > 0)
            {
                GUILayout.Space(10);
                transformControlsExpanded = Menu.ModernFoldout("Transform Controls", transformControlsExpanded);

                if (transformControlsExpanded)
                {
                    Vector3 prev = positionOffset;

                    Menu.ModernSlider("Position X", ref positionOffset.x, -5f, 5f);

                    if (positionOffset.x != prev.x)
                        ApplyTransformOffsets(new Vector3(positionOffset.x - prev.x, 0, 0), Vector3.zero);

                    prev = positionOffset;

                    Menu.ModernSlider("Position Up/Down", ref positionOffset.y, -5f, 5f);

                    if (positionOffset.y != prev.y)
                        ApplyTransformOffsets(new Vector3(0, positionOffset.y - prev.y, 0), Vector3.zero);

                    prev = positionOffset;

                    Menu.ModernSlider("Position Z", ref positionOffset.z, -5f, 5f);

                    if (positionOffset.z != prev.z)
                        ApplyTransformOffsets(new Vector3(0, 0, positionOffset.z - prev.z), Vector3.zero);

                    GUILayout.Space(5);

                    Vector3 prevRot = rotationOffset;

                    Menu.ModernSlider("Rotation X", ref rotationOffset.x, -180f, 180f);

                    if (rotationOffset.x != prevRot.x)
                        ApplyTransformOffsets(Vector3.zero, new Vector3(rotationOffset.x - prevRot.x, 0, 0));

                    prevRot = rotationOffset;

                    Menu.ModernSlider("Rotation Y", ref rotationOffset.y, -180f, 180f);

                    if (rotationOffset.y != prevRot.y)
                        ApplyTransformOffsets(Vector3.zero, new Vector3(0, rotationOffset.y - prevRot.y, 0));

                    prevRot = rotationOffset;

                    Menu.ModernSlider("Rotation Z", ref rotationOffset.z, -180f, 180f);

                    if (rotationOffset.z != prevRot.z)
                        ApplyTransformOffsets(Vector3.zero, new Vector3(0, 0, rotationOffset.z - prevRot.z));
                }
            }
        }

        private static void LogDropper(string message)
        {
            if (!DebugLoggingEnabled)
                return;

            Log.Msg($"[Dropper] {message}");
        }

        private static void LogDropperState(bool dropperActive, bool placementActive, bool hasCamera, bool hasGamepad)
        {
            bool hasSelection = !string.IsNullOrEmpty(selectedPrefabName);

            if (hasLoggedDropperState
                && lastLoggedDropperActive == dropperActive
                && lastLoggedPlacementActive == placementActive
                && lastLoggedHasCamera == hasCamera
                && lastLoggedHasGamepad == hasGamepad
                && lastLoggedHasSelection == hasSelection
                && lastLoggedCanPlacePreview == canPlacePreview)
            {
                return;
            }

            hasLoggedDropperState = true;
            lastLoggedDropperActive = dropperActive;
            lastLoggedPlacementActive = placementActive;
            lastLoggedHasCamera = hasCamera;
            lastLoggedHasGamepad = hasGamepad;
            lastLoggedHasSelection = hasSelection;
            lastLoggedCanPlacePreview = canPlacePreview;

            LogDropper($"State changed: tabActive={dropperActive}, placementActive={placementActive}, camera={hasCamera}, gamepad={hasGamepad}, selected='{selectedPrefabName ?? "None"}', preview={(previewObject != null)}, canPlace={canPlacePreview}, spawned={spawnedObjects.Count}.");
        }

        private static void LogPlacementFailure(string reason)
        {
            string previousReason = lastPlacementFailureReason;
            lastPlacementFailureReason = reason;

            if (!DebugLoggingEnabled)
                return;

            if (Time.unscaledTime < nextPlacementFailureLogTime && reason == previousReason)
                return;

            nextPlacementFailureLogTime = Time.unscaledTime + 1f;
            LogDropper($"Preview not placeable: {reason}");
        }

        private static void LogPlacementRecovered()
        {
            if (string.IsNullOrEmpty(lastPlacementFailureReason))
                return;

            LogDropper("Preview placement recovered.");
            lastPlacementFailureReason = null;
        }

        private static void LogNoPrefabsAvailable()
        {
            if (Time.unscaledTime < nextNoPrefabsLogTime)
                return;

            nextNoPrefabsLogTime = Time.unscaledTime + 5f;
            LogDropper($"No dropper prefabs available. prefabs={Memory.dropperPrefabs?.Count ?? 0}, names={Memory.dropperPrefabNames?.Count ?? 0}, loadedBundles={Memory.loadedBundles?.Count ?? 0}.");
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }
    }
}
