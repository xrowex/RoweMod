using System.Linq;
using UnityEngine;
using HarmonyLib;
using Il2Cpp;
using Il2CppCinemachine;
using Il2CppMashBox.Addons.ReplaySystem;
using Il2CppMashBox.Addons.ReplaySystem.Replay_System;
using Il2CppMashBox.Addons.ReplaySystem.Replay_System.Key_Framing;
using Object = Il2CppSystem.Object;

namespace rowemod.Patches
{
    public static class FovTracker
    {
        public static KeyFrameDataFloat fovTrack;
        public static CinemachineVirtualCamera vcam;

        public static void TryInit()
        {
            var replaySystem = Resources.FindObjectsOfTypeAll<ReplaySystem>().FirstOrDefault();
            if (replaySystem == null)
            {
                MelonLoader.MelonLogger.Warning("[FOV Tracker] ReplaySystem not found.");
                return;
            }

            var camObj = replaySystem.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "VirtualCam")?.gameObject;

            if (camObj == null)
            {
                MelonLoader.MelonLogger.Warning("[FOV Tracker] VirtualCam not found.");
                return;
            }

            vcam = camObj.GetComponent<CinemachineVirtualCamera>();
            if (vcam == null)
            {
                MelonLoader.MelonLogger.Warning("[FOV Tracker] CinemachineVirtualCamera not found.");
                return;
            }

            fovTrack = new KeyFrameDataFloat();
            fovTrack.InitForRuntime();

            MelonLoader.MelonLogger.Msg("[FOV Tracker] Initialized successfully.");
        }

        public static float GetCurrentFOV()
        {
            return vcam?.m_Lens.FieldOfView ?? 60f;
        }

        public static void SetFOV(float fov)
        {
            if (vcam?.m_Lens != null)
            {
                var vcamMLens = vcam.m_Lens;
                vcamMLens.FieldOfView = fov;
            }
        }
    }

    [HarmonyPatch]
    public static class FovPatchBundle
    {
        [HarmonyPatch(typeof(ReplaySystem), "StartRecording")]
        [HarmonyPostfix]
        public static void InitFovTrack()
        {
            FovTracker.TryInit();
        }

        [HarmonyPatch(typeof(KeyFrameData), nameof(KeyFrameData.AddKey))]
        [HarmonyPrefix]
        public static void CaptureFov(float time)
        {
            if (FovTracker.vcam == null || FovTracker.fovTrack == null) return;
            float fov = FovTracker.GetCurrentFOV();
            FovTracker.fovTrack.AddKey(time, fov);
        }

        [HarmonyPatch(typeof(KeyFrameData), nameof(KeyFrameData.InterpolateFrameData))]
        [HarmonyPostfix]
        public static void ApplyFov(float time)
        {
            if (FovTracker.vcam == null || FovTracker.fovTrack == null) return;

            // ✅ Only apply FOV if a replay is actively playing back
            if (!ReplayState.IsPlaying) return;


            float fov = FovTracker.fovTrack.InterpolateFrameData(time).Unbox<float>();
            FovTracker.SetFOV(fov);
        }

    }
    public static class ReplayState
    {
        public static bool IsPlaying = false;
    }

    [HarmonyPatch(typeof(ReplaySystem), "StartPlayback")]
    public class ReplayStart
    {
        static void Postfix() => ReplayState.IsPlaying = true;
    }

    [HarmonyPatch(typeof(ReplaySystem), "StopPlayback")]
    public class ReplayStop
    {
        static void Postfix() => ReplayState.IsPlaying = false;
    }
}
