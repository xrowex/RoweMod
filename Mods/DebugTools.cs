using System;
using Il2CppMashBox.Core.Runtime.Physics;
using rowemod.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace rowemod.Mods
{
    public static class DebugTools
    {
        private const float CenterOfMassMarkerScale = 0.35f;

        private static bool showCenterOfMassMarker;
        private static GameObject centerOfMassMarker;
        private static GameObject frontPivotMarker;
        private static GameObject centerProjectionMarker;
        private static Material centerOfMassMaterial;
        private static Material frontPivotMaterial;
        private static Material centerProjectionMaterial;
        private static Vector3 lastCenterOfMass;
        private static Vector3 lastFrontPivot;
        private static Vector3 lastCenterProjection;
        private static float lastHorizontalLeverArm;
        private static string lastPivotError = string.Empty;
        private static string statusText = "Center of mass marker is off.";

        public static void DrawDebugTab()
        {
            Menu.BeginPane("Center Of Mass", "Visualize the current physics center of mass in the world.");
            bool nextShowMarker = showCenterOfMassMarker;
            Menu.ModernToggle("Show _worldCenterOfMass Sphere", ref nextShowMarker, "debug_show_center_of_mass");
            if (nextShowMarker != showCenterOfMassMarker)
            {
                SetCenterOfMassMarkerVisible(nextShowMarker);
            }

            GUILayout.Label($"Scale: {CenterOfMassMarkerScale:0.00}", Menu.UiMutedStyle);
            GUILayout.Label($"Position: X={lastCenterOfMass.x:0.000}, Y={lastCenterOfMass.y:0.000}, Z={lastCenterOfMass.z:0.000}", Menu.UiMutedStyle);
            GUILayout.Label($"Front pivot: X={lastFrontPivot.x:0.000}, Y={lastFrontPivot.y:0.000}, Z={lastFrontPivot.z:0.000}", Menu.UiMutedStyle);
            GUILayout.Label($"Horizontal COM lever arm: {lastHorizontalLeverArm:0.000} m", Menu.UiMutedStyle);
            GUILayout.Label("Cyan = aggregate COM, green = front-wheel pivot, yellow = COM projected to pivot height.", Menu.UiMutedWrappedStyle);
            GUILayout.Label(statusText, Menu.UiMutedWrappedStyle);

            Menu.BeginToolbar();
            if (Menu.SecondaryButton("Refresh Reference", GUILayout.Width(160f), GUILayout.Height(26f)))
            {
                RefreshCenterOfMassReference();
                UpdateCenterOfMassMarker();
            }

            if (Menu.DangerButton("Hide Marker", GUILayout.Width(130f), GUILayout.Height(26f)))
            {
                SetCenterOfMassMarkerVisible(false);
            }
            Menu.EndToolbar();
            Menu.EndPane();
        }

        public static void Update()
        {
            if (!showCenterOfMassMarker)
            {
                if (centerOfMassMarker != null && centerOfMassMarker.activeSelf)
                    centerOfMassMarker.SetActive(false);
                SetAuxiliaryMarkersVisible(false);

                return;
            }

            UpdateCenterOfMassMarker();
        }

        public static void ResetTab()
        {
            SetCenterOfMassMarkerVisible(false);
            statusText = "Debug tab reset.";
        }

        public static void Cleanup()
        {
            showCenterOfMassMarker = false;

            DestroyObject(ref centerOfMassMarker);
            DestroyObject(ref frontPivotMarker);
            DestroyObject(ref centerProjectionMarker);
            DestroyObject(ref centerOfMassMaterial);
            DestroyObject(ref frontPivotMaterial);
            DestroyObject(ref centerProjectionMaterial);
        }

        private static void SetCenterOfMassMarkerVisible(bool visible)
        {
            showCenterOfMassMarker = visible;

            if (!visible)
            {
                if (centerOfMassMarker != null)
                    centerOfMassMarker.SetActive(false);
                SetAuxiliaryMarkersVisible(false);

                statusText = "Center of mass marker is off.";
                return;
            }

            RefreshCenterOfMassReference();
            EnsureCenterOfMassMarker();
            UpdateCenterOfMassMarker();
            Log.Msg("[Debug] Center of mass marker enabled.");
        }

        private static void RefreshCenterOfMassReference()
        {
            if (Memory.rCenterOfMassBehaviour != null)
                return;

            try
            {
                if (Memory.rMbCharacter != null)
                {
                    Memory.rCenterOfMassBehaviour =
                        Memory.rMbCharacter.GetComponentInChildren<CenterOfMassBehaviour>();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[Debug] Could not refresh CenterOfMassBehaviour: {ex.Message}");
            }
        }

        private static void UpdateCenterOfMassMarker()
        {
            RefreshCenterOfMassReference();

            if (Memory.rCenterOfMassBehaviour == null)
            {
                statusText = "CenterOfMassBehaviour not found.";
                if (centerOfMassMarker != null)
                    centerOfMassMarker.SetActive(false);
                return;
            }

            EnsureCenterOfMassMarker();
            if (centerOfMassMarker == null)
                return;

            try
            {
                lastCenterOfMass = Memory.rCenterOfMassBehaviour._worldCenterOfMass;
                centerOfMassMarker.transform.position = lastCenterOfMass;
                centerOfMassMarker.transform.localScale = Vector3.one * CenterOfMassMarkerScale;
                centerOfMassMarker.SetActive(true);

                if (TryGetFrontPivot(out lastFrontPivot))
                {
                    EnsureAuxiliaryMarkers();
                    lastCenterProjection = new Vector3(lastCenterOfMass.x, lastFrontPivot.y, lastCenterOfMass.z);
                    Vector2 horizontalDelta = new Vector2(
                        lastCenterOfMass.x - lastFrontPivot.x,
                        lastCenterOfMass.z - lastFrontPivot.z);
                    lastHorizontalLeverArm = horizontalDelta.magnitude;

                    frontPivotMarker.transform.position = lastFrontPivot;
                    centerProjectionMarker.transform.position = lastCenterProjection;
                    frontPivotMarker.SetActive(true);
                    centerProjectionMarker.SetActive(true);
                    lastPivotError = string.Empty;
                    statusText = "Markers follow aggregate COM and the front-wheel physics pivot.";
                }
                else
                {
                    lastHorizontalLeverArm = 0f;
                    SetAuxiliaryMarkersVisible(false);
                    statusText = "Aggregate COM found; front-wheel pivot is unavailable.";
                }
            }
            catch (Exception ex)
            {
                statusText = $"Could not read _worldCenterOfMass: {ex.Message}";
                Log.Warning($"[Debug] Failed to update center of mass marker: {ex.Message}");
            }
        }

        private static void EnsureCenterOfMassMarker()
        {
            if (centerOfMassMarker != null)
                return;

            centerOfMassMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            centerOfMassMarker.name = "RoweMod Debug _worldCenterOfMass";
            centerOfMassMarker.transform.localScale = Vector3.one * CenterOfMassMarkerScale;

            Collider collider = centerOfMassMarker.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = centerOfMassMarker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = GetCenterOfMassMaterial();
            }

            Object.DontDestroyOnLoad(centerOfMassMarker);
            centerOfMassMarker.SetActive(false);
            Log.Msg("[Debug] Created center of mass marker sphere.");
        }

        private static void EnsureAuxiliaryMarkers()
        {
            if (frontPivotMarker == null)
                frontPivotMarker = CreateMarker("RoweMod Debug Front Pivot", new Color(0.1f, 1f, 0.25f, 0.95f), ref frontPivotMaterial);

            if (centerProjectionMarker == null)
                centerProjectionMarker = CreateMarker("RoweMod Debug COM Projection", new Color(1f, 0.85f, 0.05f, 0.95f), ref centerProjectionMaterial);

            float auxiliaryScale = CenterOfMassMarkerScale * 0.5f;
            if (frontPivotMarker != null)
                frontPivotMarker.transform.localScale = Vector3.one * auxiliaryScale;
            if (centerProjectionMarker != null)
                centerProjectionMarker.transform.localScale = Vector3.one * auxiliaryScale;
        }

        private static GameObject CreateMarker(string markerName, Color color, ref Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = markerName;

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = GetMarkerMaterial(ref material, color, markerName + " Material");

            Object.DontDestroyOnLoad(marker);
            marker.SetActive(false);
            return marker;
        }

        private static bool TryGetFrontPivot(out Vector3 pivot)
        {
            pivot = Vector3.zero;

            try
            {
                var balance = Memory.vehicleController?._balanaceBehaviour;
                var frontWheel = balance?._frontWheelPhysics;
                if (frontWheel != null)
                {
                    Vector3 forcePosition = frontWheel._frictionForcePosition;
                    if (forcePosition.sqrMagnitude > 0.0001f)
                    {
                        pivot = forcePosition;
                        return true;
                    }
                }

                Rigidbody wheelBody = balance?._frontWheelBody;
                if (wheelBody == null)
                    return false;

                float radius = frontWheel == null ? 0f : Mathf.Max(0f, frontWheel._wheelRadius);
                Vector3 up = balance.transform.up;
                pivot = wheelBody.worldCenterOfMass - up * radius;
                return true;
            }
            catch (Exception ex)
            {
                if (!string.Equals(lastPivotError, ex.Message, StringComparison.Ordinal))
                {
                    lastPivotError = ex.Message;
                    Log.Warning($"[Debug] Could not resolve front-wheel pivot: {ex.Message}");
                }
                return false;
            }
        }

        private static void SetAuxiliaryMarkersVisible(bool visible)
        {
            if (frontPivotMarker != null)
                frontPivotMarker.SetActive(visible);
            if (centerProjectionMarker != null)
                centerProjectionMarker.SetActive(visible);
        }

        private static Material GetCenterOfMassMaterial()
        {
            if (centerOfMassMaterial != null)
                return centerOfMassMaterial;

            return GetMarkerMaterial(
                ref centerOfMassMaterial,
                new Color(0.05f, 0.9f, 1f, 0.9f),
                "RoweMod Center Of Mass Material");
        }

        private static Material GetMarkerMaterial(ref Material material, Color color, string materialName)
        {
            if (material != null)
                return material;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Log.Warning("[Debug] Could not find HDRP/Lit or Standard shader for center of mass marker.");
                return null;
            }

            material = new Material(shader) { name = materialName };
            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * 0.4f);

            return material;
        }

        private static void DestroyObject<T>(ref T value) where T : Object
        {
            if (value != null)
                Object.Destroy(value);
            value = null;
        }
    }
}
