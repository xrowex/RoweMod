using HarmonyLib;
using Il2CppFusion;
using Il2CppMashBox.Netorking;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Adds a small host-only capacity selector to the native host setup and injects the chosen
    /// value into Fusion's StartGameArgs immediately before the host session is created.
    /// </summary>
    internal static class HostPlayerLimit
    {
        private const int MinimumPlayers = 1;
        private const int MaximumPlayers = 8;
        private static int _lastLoggedCapacity = -1;
        private static bool _loggedHostControlDraw;

        private static int Capacity
        {
            get
            {
                Config.misc.hostPlayerLimit = Mathf.Clamp(Config.misc.hostPlayerLimit, MinimumPlayers, MaximumPlayers);
                return Config.misc.hostPlayerLimit;
            }
        }

        internal static void DrawHostSetupControl()
        {
            if (!_loggedHostControlDraw)
            {
                _loggedHostControlDraw = true;
                rowemod.Utils.Log.Msg("[Host Setup] Player-capacity selector injected into the host map controls.");
            }

            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(38f));
            GUILayout.Label("Players", GUILayout.Width(82f));

            if (GUILayout.Button("<", GUILayout.Width(38f)) && Capacity > MinimumPlayers)
            {
                Config.misc.hostPlayerLimit = Capacity - 1;
                Config.RequestSave();
            }

            GUILayout.Label($"{Capacity} player{(Capacity == 1 ? string.Empty : "s")}",
                GUILayout.Width(92f));

            if (GUILayout.Button(">", GUILayout.Width(38f)) && Capacity < MaximumPlayers)
            {
                Config.misc.hostPlayerLimit = Capacity + 1;
                Config.RequestSave();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Session capacity (1-8)");
            GUILayout.EndHorizontal();
        }

        internal static void ApplyToHostStart(ref StartGameArgs startArgs)
        {
            if (startArgs == null || startArgs.GameMode != GameMode.Host)
                return;

            int capacity = Capacity;
            startArgs.PlayerCount = (Il2CppSystem.Nullable<int>)capacity;

            if (_lastLoggedCapacity != capacity)
            {
                _lastLoggedCapacity = capacity;
                rowemod.Utils.Log.Msg($"[Host Setup] Starting host session with a {capacity}-player capacity.");
            }
        }
    }

    [HarmonyPatch(typeof(Il2CppMashBox.Netorking.FusionBootstrap), "DrawServerBrowserHostMapControls")]
    internal static class HostPlayerLimitHostSetupPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            HostPlayerLimit.DrawHostSetupControl();
        }
    }

    [HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.StartGame))]
    internal static class HostPlayerLimitStartGamePatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref StartGameArgs __0)
        {
            HostPlayerLimit.ApplyToHostStart(ref __0);
        }
    }
}
