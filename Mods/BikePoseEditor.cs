using System;
using System.Collections.Generic;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace rowemod.Mods
{
    public static class BikePoseEditor
    {
        private enum GizmoMode
        {
            Move,
            Rotate
        }

        private const int AxisCount = 3;
        private const int RingSegments = 64;
        private const int TubeSegments = 10;
        private const int ArrowSegments = 16;
        private const float HandlePickRadius = 14f;
        private static readonly string[] FrontEndPoseTransformNames =
        {
            "Headset Visuals",
            "Front Wheel Visuals",
            "Front Wheel Front"
        };
        private static readonly Color[] AxisColors =
        {
            new Color(1f, 0.05f, 0.05f),
            new Color(0.05f, 1f, 0.12f),
            new Color(0.05f, 0.35f, 1f)
        };
        private static readonly Color HoverColor = new Color(1f, 0.78f, 0.05f);

        private static GameObject posedBike;
        private static GameObject placementBike;
        private static GameObject gizmoRoot;
        private static readonly GameObject[] moveHandles = new GameObject[AxisCount];
        private static readonly GameObject[] rotationHandles = new GameObject[AxisCount];
        private static readonly MeshRenderer[] moveRenderers = new MeshRenderer[AxisCount];
        private static readonly MeshRenderer[] rotationRenderers = new MeshRenderer[AxisCount];
        private static readonly Material[] gizmoMaterials = new Material[AxisCount];
        private static Mesh moveHandleMesh;
        private static Mesh rotationHandleMesh;
        private static UnityEngine.Camera activeCamera;

        private static GizmoMode gizmoMode = GizmoMode.Move;
        private static bool useLocalAxes;
        private static bool isPlacing;
        private static int hoveredAxis = -1;
        private static int draggedAxis = -1;
        private static Plane dragPlane;
        private static Vector3 dragAxis;
        private static Vector3 dragStartPosition;
        private static Quaternion dragStartRotation;
        private static float dragStartAxisParameter;
        private static Vector3 dragStartVector;
        private static Vector3 originalPosition;
        private static Quaternion originalRotation = Quaternion.identity;
        private static float gizmoScale = 1f;
        private static float frontEndTurn;
        private static float bottomBracketRotation;
        private static int visualRendererCount;
        private static string statusText = "Place a visual copy of the current bike to begin posing.";
        private static readonly Dictionary<GameObject, BikeVisualRig> visualRigs = new();
        private static float nextErrorLogTime;

        private sealed class BikeVisualRig
        {
            public readonly List<PosePivot> FrontEndPivots = new List<PosePivot>();
            public Transform BottomBracketPivot;
            public Quaternion BottomBracketBaseLocalRotation = Quaternion.identity;
            public string BottomBracketPath;
        }

        private sealed class PosePivot
        {
            public Transform Transform;
            public Quaternion BaseLocalRotation = Quaternion.identity;
            public string Path;
        }

        public static void Update()
        {
            try
            {
                UpdateInternal();
            }
            catch (Exception ex)
            {
                EndDrag();
                statusText = "Bike poser update failed. Check the MelonLoader log.";
                if (Time.unscaledTime >= nextErrorLogTime)
                {
                    nextErrorLogTime = Time.unscaledTime + 2f;
                    Log.Error($"[BikePoser] Update failed: {ex}");
                }
            }
        }

        private static void UpdateInternal()
        {
            bool tabActive = Menu.isOpen && Menu.currentTab == Menu.Tab.BikePoser;
            if (!tabActive)
            {
                SetGizmoVisible(false);
                EndDrag();
                return;
            }

            RefreshCamera();
            if (activeCamera == null)
            {
                if (isPlacing)
                    statusText = "No active camera was found for bike placement.";
                return;
            }

            if (isPlacing)
            {
                UpdatePlacement();
                SetGizmoVisible(false);
                return;
            }

            if (posedBike == null)
            {
                SetGizmoVisible(false);
                return;
            }

            EnsureGizmo();
            UpdateGizmoGeometry();
            HandleGizmoInput();
        }

        public static void DrawTab()
        {
            Menu.BeginPane(
                "Bike Snapshot",
                "Creates a clean visual copy with transforms, meshes, renderers, and current bike materials.");

            Menu.BeginToolbar();
            if (Menu.PrimaryButton(isPlacing ? "Cancel Placement" : "Place Current Bike", GUILayout.Width(180f), GUILayout.Height(26f)))
            {
                if (isPlacing)
                    CancelPlacement();
                else
                    BeginPlacement();
            }

            if (posedBike != null && Menu.SecondaryButton("Refresh Visuals", GUILayout.Width(150f), GUILayout.Height(26f)))
                RefreshVisualClone();

            if (posedBike != null && Menu.DangerButton("Delete Bike", GUILayout.Width(120f), GUILayout.Height(26f)))
                DeleteBike();
            Menu.EndToolbar();

            GUILayout.Space(8f);
            GUILayout.Label(statusText, Menu.UiMutedWrappedStyle);
            Menu.EndPane();

            if (posedBike == null)
                return;

            Menu.BeginPane("Gizmo", "Choose how the placed visual bike should be manipulated.");
            Menu.BeginToolbar();
            if (Menu.PillButton(gizmoMode == GizmoMode.Move ? "Move (Active)" : "Move", gizmoMode == GizmoMode.Move, GUILayout.Width(140f), GUILayout.Height(26f)))
            {
                gizmoMode = GizmoMode.Move;
                EndDrag();
            }

            if (Menu.PillButton(gizmoMode == GizmoMode.Rotate ? "Rotate (Active)" : "Rotate", gizmoMode == GizmoMode.Rotate, GUILayout.Width(140f), GUILayout.Height(26f)))
            {
                gizmoMode = GizmoMode.Rotate;
                EndDrag();
            }
            Menu.EndToolbar();

            Menu.ModernToggle("Local Axes", ref useLocalAxes, "BikePoseLocalAxes");
            Menu.ModernSlider("Gizmo Size", ref gizmoScale, 0.5f, 2f, "BikePoseGizmoScale");
            Menu.EndPane();

            Menu.BeginPane("Transform", "Fine tune the placed bike's world position and rotation.");
            Menu.DrawSectionTitle("Position");
            Vector3 position = posedBike.transform.position;
            float positionX = position.x;
            float positionY = position.y;
            float positionZ = position.z;
            Menu.ModernSlider("Position X", ref positionX, -500f, 500f, "BikePosePositionX");
            Menu.ModernSlider("Position Y", ref positionY, -100f, 500f, "BikePosePositionY");
            Menu.ModernSlider("Position Z", ref positionZ, -500f, 500f, "BikePosePositionZ");
            Vector3 editedPosition = new Vector3(positionX, positionY, positionZ);
            if ((editedPosition - position).sqrMagnitude > 0.000001f)
                posedBike.transform.position = editedPosition;

            GUILayout.Space(8f);
            Menu.DrawSectionTitle("Rotation");
            Vector3 rotation = NormalizeEuler(posedBike.transform.eulerAngles);
            float rotationX = rotation.x;
            float rotationY = rotation.y;
            float rotationZ = rotation.z;
            Menu.ModernSlider("Pitch", ref rotationX, -180f, 180f, "BikePoseRotationX");
            Menu.ModernSlider("Yaw", ref rotationY, -180f, 180f, "BikePoseRotationY");
            Menu.ModernSlider("Roll", ref rotationZ, -180f, 180f, "BikePoseRotationZ");
            Vector3 editedRotation = new Vector3(rotationX, rotationY, rotationZ);
            if ((editedRotation - rotation).sqrMagnitude > 0.000001f)
                posedBike.transform.rotation = Quaternion.Euler(editedRotation);

            Menu.BeginToolbar();
            if (Menu.SecondaryButton("Snap To Ground", GUILayout.Width(150f), GUILayout.Height(26f)))
                SnapToGround();
            if (Menu.SecondaryButton("Reset Transform", GUILayout.Width(150f), GUILayout.Height(26f)))
                ResetTransform();
            Menu.EndToolbar();
            Menu.EndPane();

            Menu.BeginPane("Part Posing", "Adjust simple bike rig pivots on the visual copy.");
            BikeVisualRig rig = GetVisualRig(posedBike);
            if (rig != null && rig.FrontEndPivots.Count > 0)
            {
                float previousFrontEndTurn = frontEndTurn;
                Menu.ModernSlider(
                    "Front End Turn",
                    ref frontEndTurn,
                    -90f,
                    90f,
                    "BikePoseFrontEndTurn");
                if (!Mathf.Approximately(previousFrontEndTurn, frontEndTurn))
                    ApplyPartPose(rig);
                GUILayout.Label(
                    $"Pivots: {string.Join(", ", rig.FrontEndPivots.Select(pivot => pivot.Path))}",
                    Menu.UiMutedWrappedStyle);
            }
            else
            {
                GUILayout.Label("Front-end pivot was not found in this bike hierarchy.", Menu.UiMutedWrappedStyle);
            }

            if (rig?.BottomBracketPivot != null)
            {
                float previousBottomBracketRotation = bottomBracketRotation;
                Menu.ModernSlider(
                    "Bottom Bracket Rotation",
                    ref bottomBracketRotation,
                    -180f,
                    180f,
                    "BikePoseBottomBracketRotation");
                if (!Mathf.Approximately(previousBottomBracketRotation, bottomBracketRotation))
                    ApplyPartPose(rig);
                GUILayout.Label($"Pivot: {rig.BottomBracketPath}", Menu.UiMutedWrappedStyle);
            }
            else
            {
                GUILayout.Label("Bottom-bracket or crank pivot was not found in this bike hierarchy.", Menu.UiMutedWrappedStyle);
            }

            if (Menu.SecondaryButton("Reset Part Pose", GUILayout.Width(150f), GUILayout.Height(26f)))
            {
                frontEndTurn = 0f;
                bottomBracketRotation = 0f;
                ApplyPartPose(rig);
            }
            Menu.EndPane();
        }

        public static void OnTabEntered()
        {
            statusText = posedBike == null
                ? "Place a visual copy of the current bike to begin posing."
                : $"Bike ready with {visualRendererCount} visual renderers.";
        }

        public static void OnTabExited()
        {
            CancelPlacement();
            EndDrag();
            SetGizmoVisible(false);
        }

        public static void ResetTab()
        {
            CancelPlacement();
            DeleteBike();
            gizmoMode = GizmoMode.Move;
            useLocalAxes = false;
            gizmoScale = 1f;
            frontEndTurn = 0f;
            bottomBracketRotation = 0f;
            statusText = "Bike poser reset.";
        }

        private static void BeginPlacement()
        {
            try
            {
                if (Memory.playersBike == null)
                {
                    statusText = "The player's bike is not available in this scene.";
                    return;
                }

                CancelPlacement();
                Log.Msg($"[BikePoser] Creating visual snapshot from {Memory.playersBike.name}.");
                placementBike = CreateVisualBikeClone(Memory.playersBike, out int rendererCount);
                if (placementBike == null)
                {
                    statusText = "Could not find any visible bike meshes to copy.";
                    return;
                }

                visualRendererCount = rendererCount;
                isPlacing = true;
                statusText = "Move the cursor over the world and left-click to place the bike.";
                Log.Msg($"[BikePoser] Visual snapshot ready with {rendererCount} renderers.");
            }
            catch (Exception ex)
            {
                CancelPlacement();
                statusText = "Could not create the bike snapshot. Check the MelonLoader log.";
                Log.Error($"[BikePoser] Begin placement failed: {ex}");
            }
        }

        private static void UpdatePlacement()
        {
            if (placementBike == null)
            {
                isPlacing = false;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            bool hasPlacement = TryGetCursorPlacement(
                mouse.position.ReadValue(),
                out Vector3 point,
                out Quaternion rotation,
                out string placementFailureReason);

            if (hasPlacement)
            {
                placementBike.SetActive(true);
                placementBike.transform.SetPositionAndRotation(point, rotation);
                statusText = "Left-click to place the bike. Right-click cancels.";
            }
            else
            {
                placementBike.SetActive(false);
                statusText = placementFailureReason;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame)
                return;

            if (IsMouseOverMenu())
            {
                statusText = "Move the cursor off the RoweMod menu before placing.";
                return;
            }

            if (!placementBike.activeSelf)
            {
                statusText = placementFailureReason;
                return;
            }

            if (placementBike.activeSelf)
            {
                if (posedBike != null)
                    DestroyVisualBike(posedBike);

                posedBike = placementBike;
                placementBike = null;
                isPlacing = false;
                frontEndTurn = 0f;
                bottomBracketRotation = 0f;
                ApplyPartPose(GetVisualRig(posedBike));
                originalPosition = posedBike.transform.position;
                originalRotation = posedBike.transform.rotation;
                statusText = $"Bike placed with {visualRendererCount} visual renderers. Drag the gizmo handles to pose it.";
                EnsureGizmo();
                Log.Msg($"[BikePoser] Bike placed at {originalPosition}.");
            }
        }

        private static bool TryGetCursorPlacement(
            Vector2 screenPosition,
            out Vector3 point,
            out Quaternion rotation,
            out string failureReason)
        {
            point = Vector3.zero;
            rotation = Quaternion.identity;
            failureReason = null;

            if (activeCamera == null)
            {
                failureReason = "No active camera was found for bike placement.";
                return false;
            }

            Ray ray = activeCamera.ScreenPointToRay(screenPosition);
            if (!TryRaycastWorld(ray, 500f, out RaycastHit hit, out failureReason))
                return false;

            Vector3 normal = hit.normal.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(activeCamera.transform.forward, normal);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(activeCamera.transform.up, normal);

            point = hit.point;
            rotation = Quaternion.LookRotation(forward.normalized, normal);
            failureReason = null;
            return true;
        }

        private static GameObject CreateVisualBikeClone(GameObject source, out int rendererCount)
        {
            rendererCount = 0;
            if (source == null)
                return null;

            GameObject root = new GameObject("Bike Poser Visual");
            GameObject hierarchyRootObject = new GameObject(source.name + " Visuals");
            Transform hierarchyRoot = hierarchyRootObject.transform;
            hierarchyRoot.SetParent(root.transform, false);
            hierarchyRoot.localPosition = Vector3.zero;
            hierarchyRoot.localRotation = Quaternion.identity;
            hierarchyRoot.localScale = source.transform.lossyScale;

            Dictionary<Transform, Transform> transformMap = new Dictionary<Transform, Transform>
            {
                [source.transform] = hierarchyRoot
            };
            BikeVisualRig rig = new BikeVisualRig();
            visualRigs[root] = rig;

            try
            {
                CloneChildTransforms(source.transform, hierarchyRoot, transformMap);

                MeshRenderer[] meshRenderers = source.GetComponentsInChildren<MeshRenderer>(true);
                Log.Msg($"[BikePoser] Found {meshRenderers.Length} MeshRenderer components.");
                foreach (MeshRenderer sourceRenderer in meshRenderers)
                {
                    try
                    {
                        if (!IsVisibleRenderer(sourceRenderer))
                            continue;

                        MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                        if (sourceFilter == null || sourceFilter.sharedMesh == null)
                            continue;
                        if (!transformMap.TryGetValue(sourceRenderer.transform, out Transform cloneTransform))
                            continue;

                        MeshFilter cloneFilter = cloneTransform.gameObject.AddComponent<MeshFilter>();
                        cloneFilter.sharedMesh = sourceFilter.sharedMesh;
                        MeshRenderer cloneRenderer = cloneTransform.gameObject.AddComponent<MeshRenderer>();
                        CopyRendererSettings(sourceRenderer, cloneRenderer);
                        rendererCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[BikePoser] Skipped mesh renderer '{SafeName(sourceRenderer)}': {ex.Message}");
                    }
                }

                SkinnedMeshRenderer[] skinnedRenderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Log.Msg($"[BikePoser] Found {skinnedRenderers.Length} SkinnedMeshRenderer components.");
                foreach (SkinnedMeshRenderer sourceRenderer in skinnedRenderers)
                {
                    try
                    {
                        if (!IsVisibleRenderer(sourceRenderer))
                            continue;
                        if (sourceRenderer.sharedMesh == null ||
                            !transformMap.TryGetValue(sourceRenderer.transform, out Transform cloneTransform))
                            continue;

                        SkinnedMeshRenderer cloneRenderer =
                            cloneTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
                        cloneRenderer.sharedMesh = sourceRenderer.sharedMesh;
                        cloneRenderer.localBounds = sourceRenderer.localBounds;
                        cloneRenderer.updateWhenOffscreen = true;
                        CopyRendererSettings(sourceRenderer, cloneRenderer);

                        Transform[] sourceBones = sourceRenderer.bones;
                        Transform[] cloneBones = new Transform[sourceBones.Length];
                        for (int i = 0; i < sourceBones.Length; i++)
                        {
                            if (sourceBones[i] != null &&
                                transformMap.TryGetValue(sourceBones[i], out Transform cloneBone))
                            {
                                cloneBones[i] = cloneBone;
                            }
                        }

                        cloneRenderer.bones = cloneBones;
                        if (sourceRenderer.rootBone != null &&
                            transformMap.TryGetValue(sourceRenderer.rootBone, out Transform cloneRootBone))
                        {
                            cloneRenderer.rootBone = cloneRootBone;
                        }

                        rendererCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[BikePoser] Skipped skinned renderer '{SafeName(sourceRenderer)}': {ex.Message}");
                    }
                }

                if (rendererCount == 0)
                {
                    DestroyVisualBike(root);
                    return null;
                }

                BindPosePivots(source.transform, transformMap, rig);
                CenterVisualOriginAtBottom(root, hierarchyRoot);
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                Log.Msg(
                    $"[BikePoser] Hierarchy cloned with {transformMap.Count} transforms. " +
                    $"FrontEnd='{DescribePosePivots(rig.FrontEndPivots)}', " +
                    $"BB='{rig.BottomBracketPath ?? "not found"}'.");
                return root;
            }
            catch
            {
                DestroyVisualBike(root);
                throw;
            }
        }

        private static bool IsVisibleRenderer(Renderer renderer)
        {
            return renderer != null &&
                   renderer.enabled &&
                   renderer.gameObject != null &&
                   renderer.gameObject.activeInHierarchy;
        }

        private static void CloneChildTransforms(
            Transform sourceParent,
            Transform cloneParent,
            Dictionary<Transform, Transform> transformMap)
        {
            for (int i = 0; i < sourceParent.childCount; i++)
            {
                Transform sourceChild = sourceParent.GetChild(i);
                GameObject cloneObject = new GameObject(sourceChild.gameObject.name);
                Transform cloneChild = cloneObject.transform;
                cloneChild.SetParent(cloneParent, false);
                cloneChild.localPosition = sourceChild.localPosition;
                cloneChild.localRotation = sourceChild.localRotation;
                cloneChild.localScale = sourceChild.localScale;
                cloneObject.SetActive(sourceChild.gameObject.activeSelf);
                transformMap[sourceChild] = cloneChild;
                CloneChildTransforms(sourceChild, cloneChild, transformMap);
            }
        }

        private static void BindPosePivots(
            Transform sourceRoot,
            Dictionary<Transform, Transform> transformMap,
            BikeVisualRig rig)
        {
            foreach (string transformName in FrontEndPoseTransformNames)
            {
                Transform sourcePivot = FindPosePivotByName(sourceRoot, transformName);
                AddFrontEndPivot(sourceRoot, transformMap, rig, sourcePivot);
            }

            Transform bottomBracketSource = FindBestPosePivot(sourceRoot, GetBottomBracketPivotScore);
            if (bottomBracketSource != null &&
                transformMap.TryGetValue(bottomBracketSource, out Transform bottomBracketPivot))
            {
                rig.BottomBracketPivot = bottomBracketPivot;
                rig.BottomBracketBaseLocalRotation = bottomBracketPivot.localRotation;
                rig.BottomBracketPath = GetTransformPath(bottomBracketSource, sourceRoot);
            }
        }

        private static Transform FindPosePivotByName(Transform sourceRoot, string transformName)
        {
            foreach (Transform candidate in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != null &&
                    candidate != sourceRoot &&
                    candidate.name == transformName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindBestPosePivot(Transform sourceRoot, Func<string, int> getScore)
        {
            Transform best = null;
            int bestScore = 0;

            foreach (Transform candidate in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == null || candidate == sourceRoot)
                    continue;

                int score = getScore(NormalizeTransformName(candidate.name));
                if (score <= bestScore)
                    continue;

                best = candidate;
                bestScore = score;
            }

            return best;
        }

        private static void AddFrontEndPivot(
            Transform sourceRoot,
            Dictionary<Transform, Transform> transformMap,
            BikeVisualRig rig,
            Transform sourcePivot)
        {
            if (sourcePivot == null ||
                !transformMap.TryGetValue(sourcePivot, out Transform clonePivot))
            {
                return;
            }

            rig.FrontEndPivots.Add(new PosePivot
            {
                Transform = clonePivot,
                BaseLocalRotation = clonePivot.localRotation,
                Path = GetTransformPath(sourcePivot, sourceRoot)
            });
        }

        private static int GetBottomBracketPivotScore(string normalizedName)
        {
            return normalizedName switch
            {
                "crankanchor" => 100,
                "crankspivot" => 98,
                "crankpivot" => 98,
                "bottombracketanchor" => 96,
                "bbanchor" => 95,
                "sprocketanchor" => 94,
                "bottombracket" => 90,
                "cranks" => 88,
                "crank" => 86,
                "bb" => 84,
                "sprocket" => 75,
                _ when normalizedName.Contains("bottombracket") => 72,
                _ when normalizedName.Contains("crank") && normalizedName.Contains("anchor") => 70,
                _ => 0
            };
        }

        private static string NormalizeTransformName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == null)
                return string.Empty;

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root)
                    break;

                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string DescribePosePivots(List<PosePivot> pivots)
        {
            return pivots.Count > 0
                ? string.Join(", ", pivots.Select(pivot => pivot.Path))
                : "not found";
        }

        private static BikeVisualRig GetVisualRig(GameObject bike)
        {
            return bike != null && visualRigs.TryGetValue(bike, out BikeVisualRig rig)
                ? rig
                : null;
        }

        private static void ApplyPartPose(BikeVisualRig rig)
        {
            if (rig == null)
                return;

            foreach (PosePivot pivot in rig.FrontEndPivots)
            {
                if (pivot.Transform == null)
                    continue;

                pivot.Transform.localRotation =
                    pivot.BaseLocalRotation * Quaternion.AngleAxis(frontEndTurn, Vector3.up);
            }

            if (rig.BottomBracketPivot != null)
            {
                rig.BottomBracketPivot.localRotation =
                    rig.BottomBracketBaseLocalRotation *
                    Quaternion.AngleAxis(bottomBracketRotation, Vector3.right);
            }
        }

        private static void CopyRendererSettings(Renderer source, Renderer clone)
        {
            clone.sharedMaterials = source.sharedMaterials;
            clone.enabled = source.enabled;
            clone.shadowCastingMode = source.shadowCastingMode;
            clone.receiveShadows = source.receiveShadows;
            clone.lightProbeUsage = source.lightProbeUsage;
            clone.reflectionProbeUsage = source.reflectionProbeUsage;
            clone.sortingLayerID = source.sortingLayerID;
            clone.sortingOrder = source.sortingOrder;
        }

        private static string SafeName(Component component)
        {
            try
            {
                return component != null && component.gameObject != null
                    ? component.gameObject.name
                    : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static void CenterVisualOriginAtBottom(GameObject root, Transform hierarchyRoot)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.bounds.size.sqrMagnitude <= 0.000001f)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!initialized)
                return;

            Vector3 bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 localBottomCenter = root.transform.InverseTransformPoint(bottomCenter);
            hierarchyRoot.localPosition -= localBottomCenter;
        }

        private static void RefreshVisualClone()
        {
            if (posedBike == null || Memory.playersBike == null)
            {
                statusText = "A posed bike and the player's current bike are required.";
                return;
            }

            Vector3 position = posedBike.transform.position;
            Quaternion rotation = posedBike.transform.rotation;
            GameObject replacement = CreateVisualBikeClone(Memory.playersBike, out int rendererCount);
            if (replacement == null)
            {
                statusText = "Could not refresh the bike visuals.";
                return;
            }

            replacement.transform.SetPositionAndRotation(position, rotation);
            ApplyPartPose(GetVisualRig(replacement));
            DestroyVisualBike(posedBike);
            posedBike = replacement;
            visualRendererCount = rendererCount;
            statusText = $"Bike visuals refreshed from the current setup ({rendererCount} renderers).";
        }

        private static void DeleteBike()
        {
            EndDrag();
            if (posedBike != null)
                DestroyVisualBike(posedBike);
            posedBike = null;
            frontEndTurn = 0f;
            bottomBracketRotation = 0f;
            SetGizmoVisible(false);
            statusText = "Posed bike deleted.";
        }

        private static void CancelPlacement()
        {
            isPlacing = false;
            if (placementBike != null)
                DestroyVisualBike(placementBike);
            placementBike = null;
        }

        private static void DestroyVisualBike(GameObject bike)
        {
            if (bike == null)
                return;

            visualRigs.Remove(bike);

            Object.Destroy(bike);
        }

        private static void SnapToGround()
        {
            if (posedBike == null)
                return;

            Vector3 origin = posedBike.transform.position + Vector3.up * 5f;
            if (TryRaycastWorld(new Ray(origin, Vector3.down), 500f, out RaycastHit hit))
            {
                posedBike.transform.position = hit.point;
                statusText = "Bike snapped to the ground.";
            }
            else
            {
                statusText = "No ground was found below the bike.";
            }
        }

        private static bool TryRaycastWorld(Ray ray, float maxDistance, out RaycastHit hit)
        {
            return TryRaycastWorld(ray, maxDistance, out hit, out _);
        }

        private static bool TryRaycastWorld(
            Ray ray,
            float maxDistance,
            out RaycastHit hit,
            out string failureReason)
        {
            int placementMask = UnityEngine.Physics.DefaultRaycastLayers & ~(1 << 31);
            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(
                ray,
                maxDistance,
                placementMask,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            int colliderHits = 0;
            int ignoredHits = 0;
            foreach (RaycastHit candidate in hits)
            {
                if (candidate.collider == null)
                    continue;

                colliderHits++;
                Transform hitTransform = candidate.collider.transform;
                if (ShouldIgnorePlacementHit(hitTransform))
                {
                    ignoredHits++;
                    continue;
                }

                hit = candidate;
                failureReason = null;
                return true;
            }

            hit = default;
            failureReason = colliderHits == 0
                ? "No solid collider was hit under the cursor. Aim at world geometry."
                : ignoredHits == colliderHits
                    ? "Only hit the player/current bike/posed bike. Aim at the ground or another solid surface."
                    : "No valid placement surface was found under the cursor.";
            return false;
        }

        private static bool ShouldIgnorePlacementHit(Transform hitTransform)
        {
            if (hitTransform == null)
                return true;

            return IsPartOfObject(hitTransform, Memory.rMbCharacter) ||
                   IsPartOfObject(hitTransform, Memory.gamePlayer) ||
                   IsPartOfObject(hitTransform, Memory.physicsDrivenCharacter) ||
                   IsPartOfObject(hitTransform, Memory.playersBike) ||
                   IsPartOfObject(hitTransform, placementBike) ||
                   IsPartOfObject(hitTransform, posedBike) ||
                   IsPartOfBody(hitTransform, Memory.springBody) ||
                   IsPartOfBody(hitTransform, Memory.chassisRb);
        }

        private static bool IsPartOfObject(Transform hitTransform, GameObject root)
        {
            return root != null &&
                   root.transform != null &&
                   (hitTransform == root.transform || hitTransform.IsChildOf(root.transform));
        }

        private static bool IsPartOfBody(Transform hitTransform, Rigidbody body)
        {
            return body != null &&
                   body.transform != null &&
                   (hitTransform == body.transform || hitTransform.IsChildOf(body.transform));
        }

        private static void ResetTransform()
        {
            if (posedBike == null)
                return;

            posedBike.transform.SetPositionAndRotation(originalPosition, originalRotation);
            statusText = "Bike transform reset to its placement pose.";
        }

        private static void EnsureGizmo()
        {
            if (gizmoRoot != null)
                return;

            gizmoRoot = new GameObject("Bike Poser Gizmo");

            try
            {
                moveHandleMesh = CreateMoveHandleMesh();
                rotationHandleMesh = CreateRotationHandleMesh();

                for (int axis = 0; axis < AxisCount; axis++)
                {
                    gizmoMaterials[axis] = CreateGizmoMaterial(AxisColors[axis], axis);
                    if (gizmoMaterials[axis] == null)
                        throw new InvalidOperationException("A transform gizmo material could not be created.");

                    moveHandles[axis] = CreateMeshHandle(
                        $"Move Axis {axis}",
                        moveHandleMesh,
                        gizmoMaterials[axis],
                        out moveRenderers[axis]);
                    rotationHandles[axis] = CreateMeshHandle(
                        $"Rotation Ring {axis}",
                        rotationHandleMesh,
                        gizmoMaterials[axis],
                        out rotationRenderers[axis]);
                }

                Log.Msg(
                    $"[BikePoser] Mesh gizmo created with shader " +
                    $"'{gizmoMaterials[0].shader?.name ?? "unknown"}'.");
            }
            catch (Exception ex)
            {
                Object.Destroy(gizmoRoot);
                gizmoRoot = null;
                DestroyGizmoMaterials();
                DestroyGizmoMeshes();
                statusText = "Bike placed, but the transform gizmo could not be created.";
                Log.Error($"[BikePoser] Gizmo creation failed: {ex}");
            }
        }

        private static Material CreateGizmoMaterial(Color color, int axis)
        {
            Shader shader = Shader.Find("HDRP/Unlit") ??
                            Shader.Find("Sprites/Default") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                Log.Error("[BikePoser] No compatible gizmo shader was found.");
                return null;
            }

            Material material = new Material(shader)
            {
                name = $"Bike Poser Gizmo Axis {axis} Material",
                renderQueue = 3100
            };

            material.SetOverrideTag("RenderType", "Opaque");
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)CompareFunction.Always);
            if (material.HasProperty("_SurfaceType"))
                material.SetFloat("_SurfaceType", 0f);
            if (material.HasProperty("_AlphaCutoffEnable"))
                material.SetFloat("_AlphaCutoffEnable", 0f);
            if (material.HasProperty("_DoubleSidedEnable"))
                material.SetFloat("_DoubleSidedEnable", 1f);

            SetGizmoMaterialColor(material, color);
            return material;
        }

        private static GameObject CreateMeshHandle(
            string name,
            Mesh mesh,
            Material material,
            out MeshRenderer renderer)
        {
            GameObject handle = new GameObject(name);
            handle.transform.SetParent(gizmoRoot.transform, false);

            MeshFilter filter = handle.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            renderer = handle.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = short.MaxValue;
            return handle;
        }

        private static Mesh CreateMoveHandleMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            AddCylinder(vertices, triangles, 0f, 0.76f, 0.025f, ArrowSegments);
            AddCone(vertices, triangles, 0.72f, 1f, 0.10f, ArrowSegments);

            Mesh mesh = new Mesh
            {
                name = "Bike Poser Move Handle Mesh"
            };
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRotationHandleMesh()
        {
            List<Vector3> vertices = new List<Vector3>(RingSegments * TubeSegments);
            List<int> triangles = new List<int>(RingSegments * TubeSegments * 6);
            const float ringRadius = 0.78f;
            const float tubeRadius = 0.035f;

            for (int ring = 0; ring < RingSegments; ring++)
            {
                float ringAngle = ring * Mathf.PI * 2f / RingSegments;
                Vector3 radial = new Vector3(Mathf.Cos(ringAngle), Mathf.Sin(ringAngle), 0f);
                Vector3 center = radial * ringRadius;

                for (int tube = 0; tube < TubeSegments; tube++)
                {
                    float tubeAngle = tube * Mathf.PI * 2f / TubeSegments;
                    vertices.Add(
                        center +
                        radial * (Mathf.Cos(tubeAngle) * tubeRadius) +
                        Vector3.forward * (Mathf.Sin(tubeAngle) * tubeRadius));
                }
            }

            for (int ring = 0; ring < RingSegments; ring++)
            {
                int nextRing = (ring + 1) % RingSegments;
                for (int tube = 0; tube < TubeSegments; tube++)
                {
                    int nextTube = (tube + 1) % TubeSegments;
                    int current = ring * TubeSegments + tube;
                    int currentNext = ring * TubeSegments + nextTube;
                    int next = nextRing * TubeSegments + tube;
                    int nextNext = nextRing * TubeSegments + nextTube;

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(currentNext);
                    triangles.Add(currentNext);
                    triangles.Add(next);
                    triangles.Add(nextNext);
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Bike Poser Rotation Handle Mesh"
            };
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCylinder(
            List<Vector3> vertices,
            List<int> triangles,
            float start,
            float end,
            float radius,
            int segments)
        {
            int sideStart = vertices.Count;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, y, start));
                vertices.Add(new Vector3(x, y, end));
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                int bottom = sideStart + segment * 2;
                int top = bottom + 1;
                int nextBottom = sideStart + next * 2;
                int nextTop = nextBottom + 1;

                triangles.Add(bottom);
                triangles.Add(nextBottom);
                triangles.Add(top);
                triangles.Add(nextBottom);
                triangles.Add(nextTop);
                triangles.Add(top);
            }

            AddDisc(vertices, triangles, start, radius, segments, false);
            AddDisc(vertices, triangles, end, radius, segments, true);
        }

        private static void AddCone(
            List<Vector3> vertices,
            List<int> triangles,
            float start,
            float end,
            float radius,
            int segments)
        {
            int ringStart = vertices.Count;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    start));
            }

            int tip = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, end));
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(ringStart + segment);
                triangles.Add(ringStart + next);
                triangles.Add(tip);
            }

            AddDisc(vertices, triangles, start, radius, segments, false);
        }

        private static void AddDisc(
            List<Vector3> vertices,
            List<int> triangles,
            float z,
            float radius,
            int segments,
            bool faceForward)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, z));
            int ringStart = vertices.Count;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    z));
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                if (faceForward)
                {
                    triangles.Add(center);
                    triangles.Add(ringStart + segment);
                    triangles.Add(ringStart + next);
                }
                else
                {
                    triangles.Add(center);
                    triangles.Add(ringStart + next);
                    triangles.Add(ringStart + segment);
                }
            }
        }

        private static void SetGizmoMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_UnlitColor"))
                material.SetColor("_UnlitColor", color);

            Color emissive = color * 3f;
            emissive.a = 1f;
            if (material.HasProperty("_EmissiveColor"))
                material.SetColor("_EmissiveColor", emissive);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emissive);
        }

        private static void DestroyGizmoMaterials()
        {
            for (int axis = 0; axis < gizmoMaterials.Length; axis++)
            {
                if (gizmoMaterials[axis] != null)
                    Object.Destroy(gizmoMaterials[axis]);

                gizmoMaterials[axis] = null;
            }
        }

        private static void DestroyGizmoMeshes()
        {
            if (moveHandleMesh != null)
                Object.Destroy(moveHandleMesh);
            if (rotationHandleMesh != null)
                Object.Destroy(rotationHandleMesh);

            moveHandleMesh = null;
            rotationHandleMesh = null;
        }

        private static void UpdateGizmoGeometry()
        {
            if (posedBike == null || gizmoRoot == null)
                return;

            SetGizmoVisible(true);
            Vector3 center = posedBike.transform.position;
            float distance = Vector3.Distance(activeCamera.transform.position, center);
            float size = Mathf.Clamp(distance * 0.12f, 0.65f, 3f) * gizmoScale;

            for (int axisIndex = 0; axisIndex < AxisCount; axisIndex++)
            {
                Vector3 axis = GetAxis(axisIndex);
                Color color = axisIndex == draggedAxis || axisIndex == hoveredAxis
                    ? HoverColor
                    : AxisColors[axisIndex];
                SetGizmoMaterialColor(gizmoMaterials[axisIndex], color);

                bool moveActive = gizmoMode == GizmoMode.Move;
                moveHandles[axisIndex].SetActive(moveActive);
                rotationHandles[axisIndex].SetActive(!moveActive);

                GameObject activeHandle = moveActive
                    ? moveHandles[axisIndex]
                    : rotationHandles[axisIndex];
                activeHandle.transform.SetPositionAndRotation(
                    center,
                    Quaternion.FromToRotation(Vector3.forward, axis));
                activeHandle.transform.localScale = Vector3.one * size;
            }
        }

        private static void HandleGizmoInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 mousePosition = mouse.position.ReadValue();
            if (draggedAxis < 0)
                hoveredAxis = IsMouseOverMenu() ? -1 : FindHoveredAxis(mousePosition);

            if (mouse.leftButton.wasPressedThisFrame && hoveredAxis >= 0 && !IsMouseOverMenu())
                BeginDrag(hoveredAxis, mousePosition);

            if (draggedAxis >= 0 && mouse.leftButton.isPressed)
                ContinueDrag(mousePosition);

            if (draggedAxis >= 0 && mouse.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        private static int FindHoveredAxis(Vector2 mousePosition)
        {
            Vector3 center = posedBike.transform.position;
            float distance = Vector3.Distance(activeCamera.transform.position, center);
            float size = Mathf.Clamp(distance * 0.12f, 0.65f, 3f) * gizmoScale;
            float bestDistance = HandlePickRadius;
            int bestAxis = -1;

            for (int axisIndex = 0; axisIndex < AxisCount; axisIndex++)
            {
                Vector3 axis = GetAxis(axisIndex);
                float screenDistance;
                if (gizmoMode == GizmoMode.Move)
                {
                    Vector3 start = activeCamera.WorldToScreenPoint(center);
                    Vector3 end = activeCamera.WorldToScreenPoint(center + axis * size);
                    if (start.z <= 0f || end.z <= 0f)
                        continue;
                    screenDistance = DistanceToSegment(mousePosition, start, end);
                }
                else
                {
                    screenDistance = DistanceToRing(mousePosition, center, axis, size * 0.78f);
                }

                if (screenDistance < bestDistance)
                {
                    bestDistance = screenDistance;
                    bestAxis = axisIndex;
                }
            }

            return bestAxis;
        }

        private static float DistanceToRing(Vector2 mousePosition, Vector3 center, Vector3 axis, float radius)
        {
            GetRingBasis(axis, out Vector3 tangent, out Vector3 bitangent);
            float bestDistance = float.MaxValue;
            Vector3 previous = Vector3.zero;

            for (int segment = 0; segment <= RingSegments; segment++)
            {
                float angle = (segment % RingSegments) * Mathf.PI * 2f / RingSegments;
                Vector3 worldPoint = center +
                                     (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
                Vector3 screenPoint = activeCamera.WorldToScreenPoint(worldPoint);
                if (screenPoint.z <= 0f)
                    continue;

                if (segment > 0 && previous.z > 0f)
                {
                    float distance = DistanceToSegment(mousePosition, previous, screenPoint);
                    if (distance < bestDistance)
                        bestDistance = distance;
                }

                previous = screenPoint;
            }

            return bestDistance;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.001f)
                return Vector2.Distance(point, start);

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void BeginDrag(int axisIndex, Vector2 mousePosition)
        {
            draggedAxis = axisIndex;
            dragAxis = GetAxis(axisIndex).normalized;
            dragStartPosition = posedBike.transform.position;
            dragStartRotation = posedBike.transform.rotation;
            Ray ray = activeCamera.ScreenPointToRay(mousePosition);

            if (gizmoMode == GizmoMode.Move)
            {
                Vector3 planeNormal = Vector3.Cross(dragAxis,
                    Vector3.Cross(activeCamera.transform.forward, dragAxis));
                if (planeNormal.sqrMagnitude < 0.001f)
                {
                    planeNormal = Vector3.Cross(dragAxis,
                        Vector3.Cross(activeCamera.transform.up, dragAxis));
                }

                dragPlane = new Plane(planeNormal.normalized, dragStartPosition);
                if (!dragPlane.Raycast(ray, out float enter))
                {
                    EndDrag();
                    return;
                }

                Vector3 hitPoint = ray.GetPoint(enter);
                dragStartAxisParameter = Vector3.Dot(hitPoint - dragStartPosition, dragAxis);
            }
            else
            {
                dragPlane = new Plane(dragAxis, dragStartPosition);
                if (!dragPlane.Raycast(ray, out float enter))
                {
                    EndDrag();
                    return;
                }

                dragStartVector = Vector3.ProjectOnPlane(ray.GetPoint(enter) - dragStartPosition, dragAxis).normalized;
                if (dragStartVector.sqrMagnitude < 0.001f)
                    EndDrag();
            }
        }

        private static void ContinueDrag(Vector2 mousePosition)
        {
            Ray ray = activeCamera.ScreenPointToRay(mousePosition);
            if (!dragPlane.Raycast(ray, out float enter))
                return;

            Vector3 hitPoint = ray.GetPoint(enter);
            if (gizmoMode == GizmoMode.Move)
            {
                float currentParameter = Vector3.Dot(hitPoint - dragStartPosition, dragAxis);
                posedBike.transform.position =
                    dragStartPosition + dragAxis * (currentParameter - dragStartAxisParameter);
            }
            else
            {
                Vector3 currentVector =
                    Vector3.ProjectOnPlane(hitPoint - dragStartPosition, dragAxis).normalized;
                if (currentVector.sqrMagnitude < 0.001f)
                    return;

                float angle = Vector3.SignedAngle(dragStartVector, currentVector, dragAxis);
                posedBike.transform.rotation = Quaternion.AngleAxis(angle, dragAxis) * dragStartRotation;
            }
        }

        private static void EndDrag()
        {
            draggedAxis = -1;
            hoveredAxis = -1;
        }

        private static Vector3 GetAxis(int axisIndex)
        {
            Vector3 axis = axisIndex switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward
            };

            return useLocalAxes && posedBike != null ? posedBike.transform.rotation * axis : axis;
        }

        private static void GetRingBasis(Vector3 axis, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f
                ? Vector3.right
                : Vector3.up;
            tangent = Vector3.Cross(axis, reference).normalized;
            bitangent = Vector3.Cross(axis, tangent).normalized;
        }

        private static void SetGizmoVisible(bool visible)
        {
            if (gizmoRoot != null && gizmoRoot.activeSelf != visible)
                gizmoRoot.SetActive(visible);
        }

        private static void RefreshCamera()
        {
            if (activeCamera != null && activeCamera.isActiveAndEnabled)
                return;

            activeCamera = UnityEngine.Camera.main;
            if (activeCamera != null)
                return;

            Il2CppCinemachine.CinemachineBrain brain =
                GameObject.FindObjectOfType<Il2CppCinemachine.CinemachineBrain>();
            if (brain != null && brain.gameObject.TryGetComponent(out UnityEngine.Camera camera))
                activeCamera = camera;
        }

        private static bool IsMouseOverMenu()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return Menu.windowRect.Contains(guiPosition);
        }

        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            return new Vector3(
                NormalizeAngle(euler.x),
                NormalizeAngle(euler.y),
                NormalizeAngle(euler.z));
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
