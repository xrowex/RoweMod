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
        private static Material centerOfMassMaterial;
        private static Vector3 lastCenterOfMass;
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

            if (centerOfMassMarker != null)
            {
                Object.Destroy(centerOfMassMarker);
                centerOfMassMarker = null;
            }

            if (centerOfMassMaterial != null)
            {
                Object.Destroy(centerOfMassMaterial);
                centerOfMassMaterial = null;
            }
        }

        private static void SetCenterOfMassMarkerVisible(bool visible)
        {
            showCenterOfMassMarker = visible;

            if (!visible)
            {
                if (centerOfMassMarker != null)
                    centerOfMassMarker.SetActive(false);

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
                statusText = "Marker follows CenterOfMassBehaviour._worldCenterOfMass.";
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

        private static Material GetCenterOfMassMaterial()
        {
            if (centerOfMassMaterial != null)
                return centerOfMassMaterial;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Log.Warning("[Debug] Could not find HDRP/Lit or Standard shader for center of mass marker.");
                return null;
            }

            centerOfMassMaterial = new Material(shader);
            centerOfMassMaterial.color = new Color(0.05f, 0.9f, 1f, 0.9f);

            if (centerOfMassMaterial.HasProperty("_BaseColor"))
                centerOfMassMaterial.SetColor("_BaseColor", new Color(0.05f, 0.9f, 1f, 0.9f));

            if (centerOfMassMaterial.HasProperty("_EmissionColor"))
                centerOfMassMaterial.SetColor("_EmissionColor", new Color(0.05f, 0.35f, 0.45f, 1f));

            return centerOfMassMaterial;
        }
    }
}
