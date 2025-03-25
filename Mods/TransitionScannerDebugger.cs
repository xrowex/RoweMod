/*
using UnityEngine;
using MelonLoader;
using Il2CppSystem.Collections.Generic;
using Il2CppMashBox.Core.Physics.Raycasting.SurfaceScanning;
using System.Collections.Generic;
using Il2CppMashBox.Core.Physics.Projectile_Motion;
using static rowemod.Utils.Memory;
using rowemod.Utils;

namespace rowemod.Mods
{
    public static class TransitionScannerDebugger
    {
        public static GameObject debugRayContainer;
        public static System.Collections.Generic.List<GameObject> debugSpheres = new System.Collections.Generic.List<GameObject>();
        public static System.Collections.Generic.List<GameObject> flightPathSpheres = new System.Collections.Generic.List<GameObject>(); 
        public static TransitionScanner transitionScannerInstance;

        public static void OnInitializeMelon()
        {
            if (transitionScannerInstance == null)
            {
                transitionScannerInstance = UnityEngine.Object.FindObjectOfType<TransitionScanner>();

                if (transitionScannerInstance == null)
                {
                    //Log.Msg("[TransitionScannerDebugger] TransitionScanner not found in scene!");
                    return;
                }
            }

            debugRayContainer = new GameObject("DebugVisualizer");
        }

        public static void OnUpdate()
        {
            if (transitionScannerInstance == null)
            {
                transitionScannerInstance = UnityEngine.Object.FindObjectOfType<TransitionScanner>();
                if (transitionScannerInstance == null) return;
            }

            DrawLandingSpotSpheres(); 
        }

        /// <summary>
        /// Draws green spheres at landing spots.
        /// </summary>
        public static void DrawLandingSpotSpheres()
        {
            if (transitionScannerInstance == null)
            {
                //Log.Warning("[DrawLandingSpotSpheres] transitionScannerInstance is null.");
                return;
            }

            var landingSpots = transitionScannerInstance._potentialLandingSpots;
            if (landingSpots == null || landingSpots.Count == 0)
            {
                //Log.Warning("[DrawLandingSpotSpheres] landingSpots list is null or empty.");
                ClearDebugSpheres(debugSpheres);
                return;
            }

            Shader hdrpShader = Shader.Find("HDRP/Lit"); // ✅ HDRP-Compatible Shader
            Material greenMaterial = new Material(hdrpShader)
            {
                color = Color.green
            };

            greenMaterial.EnableKeyword("_EMISSION"); // Enable emission so it glows
            greenMaterial.SetColor("_EmissionColor", Color.green * 2f); // Make it bright

            while (debugSpheres.Count < landingSpots.Count)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                UnityEngine.Object.Destroy(sphere.GetComponent<Collider>());

                // ✅ Apply HDRP material
                sphere.GetComponent<Renderer>().material = greenMaterial;

                debugSpheres.Add(sphere);
            }

            for (int i = 0; i < landingSpots.Count; i++)
            {
                RaycastHit hit = landingSpots[i];
                if (i >= debugSpheres.Count) break;
                if (hit.point == Vector3.zero) continue; // Ignore invalid hits

                debugSpheres[i].transform.position = hit.point;
            }

            while (debugSpheres.Count > landingSpots.Count)
            {
                UnityEngine.Object.Destroy(debugSpheres[debugSpheres.Count - 1]);
                debugSpheres.RemoveAt(debugSpheres.Count - 1);
            }
        }


        public static System.Collections.Generic.List<LineRenderer> flightPathLines = new System.Collections.Generic.List<LineRenderer>();
        private static GameObject flightPathContainer;

        public static void DrawFlightPath()
        {
           
            FlightPrediction flightPrediction = UnityEngine.Object.FindObjectOfType<FlightPrediction>();
            if (flightPrediction == null)
            {
                Log.Error("[FlightPathDebugger] FlightPrediction instance not found.");
                return;
            }

           
            if (vehicleController == null || vehicleController._managedRigidbody == null)
            {
                Log.Error("[FlightPathDebugger] VehicleController or Rigidbody is null.");
                return;
            }

            FlightPathInfo flightPathInfo;
            bool success = flightPrediction.RunFlightPath(vehicleController._managedRigidbody.Rigidbody, vehicleController._managedRigidbody.Rigidbody.velocity, flightPrediction.pathTimeStep, out flightPathInfo);

            if (!success)
            {
                Log.Error("[FlightPathDebugger] Failed to get flight path info.");
                return;
            }

            ClearFlightPathLines(); 

            int numSteps = flightPathInfo.numStepOnHit;
            Vector3 start = flightPathInfo.LaunchPoint;
            Vector3 velocity = flightPathInfo.initialVel;
            float timeStep = flightPathInfo.pathTimeStep;

            if (flightPathContainer == null)
            {
                flightPathContainer = new GameObject("FlightPathLines");
            }

            Vector3 previousPoint = start;
            for (int i = 1; i < numSteps; i++)
            {
                // Compute next point in trajectory
                Vector3 nextPoint = start + velocity * (i * timeStep) + 0.5f * UnityEngine.Physics.gravity * Mathf.Pow(i * timeStep, 2);

                // ✅ Create new LineRenderer segment
                GameObject lineObj = new GameObject($"FlightPathSegment_{i}");
                lineObj.transform.parent = flightPathContainer.transform;

                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default")); 
                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;
                lr.positionCount = 2;
                lr.SetPosition(0, previousPoint);
                lr.SetPosition(1, nextPoint);
                lr.startColor = Color.cyan;
                lr.endColor = Color.cyan;

                flightPathLines.Add(lr);
                previousPoint = nextPoint;
            }
        }

        public static void ClearFlightPathLines()
        {
            foreach (var line in flightPathLines)
            {
                if (line != null)
                {
                    UnityEngine.Object.Destroy(line.gameObject);
                }
            }
            flightPathLines.Clear();
        }



        /// <summary>
        /// Clears all debug spheres of a given list.
        /// </summary>
        public static void ClearDebugSpheres(System.Collections.Generic.List<GameObject> sphereList)
        {
            foreach (var sphere in sphereList)
            {
                if (sphere != null)
                {
                    UnityEngine.Object.Destroy(sphere);
                }
            }
            sphereList.Clear();
        }
    }
}
*/
