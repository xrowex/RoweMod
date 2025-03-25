using System.Collections.Generic;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System;
using Il2CppMG_Core.C_R_I_D.Animation_System.Animancer_Test;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace rowemod.Mods
{
    internal class TrickMods
    {
        #region Fields and Properties

        public static TrickSetData[]? rTrickSetData;
        public static TrickSystemBrain? rTrickSystemBrain;
        public static TrickBrainHandler rTrickBrainHandler;
        public static Character? rCharacter;

        public static InputAction? trickSet1InputAction, trickSet2InputAction,
                                  trickSet3InputAction, trickSet4InputAction,
                                  trickSet5InputAction, trickSet6InputAction;

        // We'll store the extracted trick names in a dictionary: TrickSetData.name -> List of strings
        public static Dictionary<string, List<string>> TrickDictionary { get; private set; }
            = new Dictionary<string, List<string>>();

        // For easy display in the GUI, create a pre-built list of "menu entries"
        public static List<TrickMenuEntry> TrickMenuDisplayItems { get; private set; }
            = new List<TrickMenuEntry>();

        #endregion

        #region Helper Class for GUI

        // This struct (or class) holds the pre-processed data
        public class TrickMenuEntry
        {
            public string? TrickSetDisplayName;
            public List<string>? TrickDirections;
        }

        #endregion

        // Example direction labels
        private static readonly string[] DirectionLabels =
        {
            "Up", "UpRight", "Right", "DownRight",
            "Down", "DownLeft", "Left", "UpLeft"
        };

        #region Main Methods

        public static void GrabTrickData()
        {
            Log.Msg("GrabTrickData() called...");

            // Let's find references
            rTrickSetData = Resources.FindObjectsOfTypeAll<TrickSetData>();
            rTrickSystemBrain = GameObject.FindObjectOfType<TrickSystemBrain>();

            // Clear out old data
            TrickDictionary.Clear();
            TrickMenuDisplayItems.Clear();

            if (rTrickSetData != null && rTrickSetData.Length > 0)
            {
                Log.Msg($"TrickSetData Found: {rTrickSetData.Length}");

                // Loop over each TrickSetData
                for (int t = 0; t < rTrickSetData.Length; t++)
                {
                    var trickSetData = rTrickSetData[t];
                    if (trickSetData == null)
                    {
                        Log.Msg($"TrickSetData index {t} is null! Skipping...");
                        continue;
                    }

                    Log.Msg($"TrickSetData index {t}, name: {trickSetData.name}");

                    var trickNames = new List<string>();

                    // Check if _dataList is null
                    if (trickSetData._dataList == null)
                    {
                        Log.Msg($"  _dataList is null in {trickSetData.name}. Skipping...");
                        TrickDictionary[trickSetData.name] = trickNames;
                        continue;
                    }

                    //Log.Msg($"  _dataList has {trickSetData._dataList.Count} items.");

                    // Wrap iteration in try/catch to catch null references
                    try
                    {
                        for (int i = 0; i < trickSetData._dataList.Count; i++)
                        {
                            var item = trickSetData._dataList[(Index)i];
                            if (item == null)
                            {
                                //Log.Msg($"  Item at index {i} is null. Skipping...");
                                continue;
                            }

                            // Cast to Object for name etc.
                            UnityEngine.Object obj = item as UnityEngine.Object;
                            if (obj == null)
                            {
                                Log.Msg($"  Skipping item at index {i} (cannot cast to Object). Type={item.GetType().Name}");
                                continue;
                            }

                            // Log and store the name
                            //Log.Msg($"  Found Object: {obj.name}, runtime type: {obj.GetType().FullName}");
                            trickNames.Add(obj.name);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Msg($"Exception while iterating _dataList of {trickSetData.name}: {ex}");
                    }

                    // Store the list of names in the dictionary
                    TrickDictionary[trickSetData.name] = trickNames;
                }
            }
            else
            {
                Log.Msg("No TrickSetData objects found!");
            }

            // If TrickSystemBrain found, get references
            if (rTrickSystemBrain != null)
            {
                Log.Msg("TrickSystemBrain found. Getting Character + InputActions...");
                rCharacter = rTrickSystemBrain.GetComponent<Character>();

                trickSet1InputAction = rTrickSystemBrain._trickSet1InputAction;
                trickSet2InputAction = rTrickSystemBrain._trickSet2InputAction;
                trickSet3InputAction = rTrickSystemBrain._trickSet3InputAction;
                trickSet4InputAction = rTrickSystemBrain._trickSet4InputAction;
                trickSet5InputAction = rTrickSystemBrain._trickSet5InputAction;
                trickSet6InputAction = rTrickSystemBrain._trickSet6InputAction;
            }
            else
            {
                Log.Msg("TrickSystemBrain NOT found!");
            }

            // -------------------------------------------
            // Build the display data for the menu *once*:
            // -------------------------------------------
            BuildTrickMenuDisplay();
        }


        public static void DrawTrickMenu()
        {
            // Now the GUI is very lightweight; no heavy data lookups or repeated logs.
            GUILayout.Label("=== Trick Menu ===");

            // If we have nothing built, bail out
            if (TrickMenuDisplayItems == null || TrickMenuDisplayItems.Count == 0)
            {
                GUILayout.Label("No Trick Menu Items found...");
                return;
            }

            // Simply iterate over the pre-built menu items and draw
            foreach (var entry in TrickMenuDisplayItems)
            {
                GUILayout.Label($"Trick Set: {entry.TrickSetDisplayName}");
                foreach (var directionString in entry.TrickDirections)
                {
                    GUILayout.Label($"  {directionString}");
                }
            }
        }

        #endregion

        #region Private Helpers

        private static void BuildTrickMenuDisplay()
        {
            // Re-build our TrickMenuDisplayItems based on the dictionary we populated
            TrickMenuDisplayItems.Clear();

            // If rTrickSetData is empty, just return
            if (rTrickSetData == null || rTrickSetData.Length == 0) return;

            foreach (var trickSetData in rTrickSetData)
            {
                if (trickSetData == null) continue;

                // Some nicer display name
                string displaySetName = trickSetData.name.Replace("_TrickSetData_BMX", "");

                var directions = new List<string>();
                if (TrickDictionary.TryGetValue(trickSetData.name, out var trickNames))
                {
                    for (int i = 0; i < trickNames.Count; i++)
                    {
                        string direction = (i < DirectionLabels.Length)
                            ? DirectionLabels[i]
                            : $"Index {i}";

                        // Clean up the trickName
                        string displayTrickName = trickNames[i]
                            .Replace("_SyncAnimatorData_PlayerBMX", "");

                        // E.g. "Up => Backflip"
                        directions.Add($"{direction} => {displayTrickName}");
                    }
                }
                else
                {
                    // No entry or empty
                    directions.Add("(No data found for this TrickSet)");
                }

                // Add a new entry for the UI
                var entry = new TrickMenuEntry
                {
                    TrickSetDisplayName = displaySetName,
                    TrickDirections = directions
                };
                TrickMenuDisplayItems.Add(entry);
            }
        }

        #endregion
    }
}
