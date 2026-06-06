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
        private static Rect windowRect = new Rect(20, 20, 250, 100);
        private static Rect disabledWindowRect = new Rect(300, 20, 250, 100); // Position for second window
        private static bool showWindow = true;
        private static string statusText = "Checking mod status...";
        private static string disabledText = "The mod is disabled!";

        public static void CheckStatus()
        {
            UnityWebRequest www = UnityWebRequest.Get(SheetUrl);

            try
            {
                // Send the request
                www.SendWebRequest();

                // Wait until the request is done
                while (!www.isDone)
                {
                    // Without blocking the Unity main thread, and to avoid freezing,
                    // you could optionally check other things or include a slight delay here.
                }

                // Check for errors
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning("[RemoteKillSwitch] Could not fetch sheet: " + www.error);
                    return;
                }

                // Get the response text
                string data = www.downloadHandler.text?.Trim().ToLower();

                if (string.IsNullOrEmpty(data))
                {
                    Log.Warning("[RemoteKillSwitch] Empty or invalid response from server.");
                    return;
                }


                // Check the contents of the response
                if (data.Contains("moddisabled"))
                {
                    isModEnabled = false;
                    UpdateStatus("Mod has been remotely disabled!");
                    return;
                }

                if (data.Contains("modenabled"))
                {
                    isModEnabled = true;
                    UpdateStatus("Mod is running normally");
                    return;
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
                CheckStatus();
            }
            GUI.DragWindow();
        }
    }
}