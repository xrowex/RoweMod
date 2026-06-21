using System;
using System.Collections;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace rowemod.Mods
{
    public class RemoteKillSwitched
    {
        private const string SheetUrl =
            "https://docs.google.com/spreadsheets/d/1XEMWSpsjujTKIm3MnduRMTWiQJjoPG34CPySNIKozi4/edit?usp=sharing";

        private static float checkInterval = 60f; // check every minute
        public static bool isModEnabled = true;
        private static bool isCheckingStatus = false;
        private static float lastCheckTime = -999f;
        private static Rect windowRect = new Rect(20, 20, 250, 100);
        private static Rect disabledWindowRect = new Rect(300, 20, 250, 100); // Position for second window
        private static bool showWindow = true;
        private static string statusText = "Checking mod status...";
        private static string disabledText = "The mod is disabled!";

        public static void CheckStatus(bool force = false)
        {
            if (isCheckingStatus)
                return;

            if (!force && Time.unscaledTime - lastCheckTime < checkInterval)
                return;

            MelonCoroutines.Start(CheckStatusRoutine());
        }

        private static IEnumerator CheckStatusRoutine()
        {
            isCheckingStatus = true;
            lastCheckTime = Time.unscaledTime;

            UnityWebRequest www = null;
            try
            {
                www = UnityWebRequest.Get(SheetUrl);
                www.timeout = 5;
            }
            catch (Exception ex)
            {
                Log.Warning("[RemoteKillSwitch] Could not start request: " + ex.Message);
                isCheckingStatus = false;
                yield break;
            }

            yield return www.SendWebRequest();

            try
            {
                // Check for errors
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning("[RemoteKillSwitch] Could not fetch sheet: " + www.error);
                    yield break;
                }

                // Get the response text
                string data = www.downloadHandler.text?.Trim().ToLower();

                if (string.IsNullOrEmpty(data))
                {
                    Log.Warning("[RemoteKillSwitch] Empty or invalid response from server.");
                    yield break;
                }


                // Check the contents of the response
                if (data.Contains("moddisabled"))
                {
                    isModEnabled = false;
                    UpdateStatus("Mod has been remotely disabled!");
                    yield break;
                }

                if (data.Contains("modenabled"))
                {
                    isModEnabled = true;
                    UpdateStatus("Mod is running normally");
                    yield break;
                }

                // If nothing matches, log a warning
                Log.Warning("[RemoteKillSwitch] Unrecognized status from server.");
            }
            catch (Exception ex)
            {
                Log.Warning("[RemoteKillSwitch] Exception occurred during request: " + ex.Message);
            }
            finally
            {
                // Ensure the UnityWebRequest is disposed properly
                www.Dispose();
                isCheckingStatus = false;
            }
        }

        private static void UpdateStatus(string message)
        {
            statusText = message;
            Log.Msg(statusText);
        }


        public static void DrawDisabledWindow(int windowID)
        {
            GUILayout.Label(disabledText);
            if (GUILayout.Button("Recheck Status"))
            {
                // Optionally, trigger the status check manually
                CheckStatus(true);
            }
            GUI.DragWindow();
        }
    }
}
