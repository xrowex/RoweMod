using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Log = rowemod.Utils.Log;

namespace rowemod.Mods
{
    internal static class RoweGutFaceUnityAudioDiagnostics
    {
        private const string MapSceneName = "RoweGutFace";
        private const string Prefix = "[RoweGutFaceUnityAudio]";
        private static int _scanGeneration;

        public static void OnSceneInitialized(string sceneName)
        {
            if (!string.Equals(sceneName, MapSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            int generation = ++_scanGeneration;
            LogSnapshot("scene callback");
            MelonCoroutines.Start(LogDelayedSnapshots(generation));
        }

        private static IEnumerator LogDelayedSnapshots(int generation)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (!CanContinue(generation))
                yield break;
            LogSnapshot("1 second after load");

            yield return new WaitForSecondsRealtime(4f);
            if (!CanContinue(generation))
                yield break;
            LogSnapshot("5 seconds after load");

            yield return new WaitForSecondsRealtime(10f);
            if (!CanContinue(generation))
                yield break;
            LogSnapshot("15 seconds after load");
        }

        private static bool CanContinue(int generation)
        {
            if (generation != _scanGeneration)
                return false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded &&
                    string.Equals(scene.name, MapSceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void LogSnapshot(string reason)
        {
            try
            {
                AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
                Log.Msg(
                    $"{Prefix} Snapshot '{reason}': activeScene='{SceneManager.GetActiveScene().name}', " +
                    $"listenerVolume={AudioListener.volume:0.###}, listenerPaused={AudioListener.pause}, " +
                    $"dspTime={AudioSettings.dspTime:0.000}, sampleRate={AudioSettings.outputSampleRate}, " +
                    $"dspBuffer={bufferLength}x{bufferCount}, " +
                    $"appFocused={Application.isFocused}.");

                AudioListener[] listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>(true);
                int activeListeners = 0;
                foreach (AudioListener listener in listeners)
                {
                    if (listener == null)
                        continue;

                    GameObject go = listener.gameObject;
                    bool active = go.activeInHierarchy && listener.enabled;
                    if (active)
                        activeListeners++;

                    Log.Msg(
                        $"{Prefix} Listener object='{go.name}', scene='{go.scene.name}', " +
                        $"enabled={listener.enabled}, activeInHierarchy={go.activeInHierarchy}, " +
                        $"activeListener={active}, tag='{go.tag}', position={go.transform.position}.");
                }

                AudioSource[] sources = UnityEngine.Object.FindObjectsOfType<AudioSource>(true);
                int mapSourceCount = 0;
                foreach (AudioSource source in sources)
                {
                    if (source == null || source.gameObject == null)
                        continue;

                    GameObject go = source.gameObject;
                    if (!string.Equals(go.scene.name, MapSceneName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    mapSourceCount++;
                    LogSource(source);
                }

                Log.Msg(
                    $"{Prefix} Snapshot '{reason}' totals: listeners={listeners.Length}, " +
                    $"activeListeners={activeListeners}, allAudioSources={sources.Length}, " +
                    $"mapAudioSources={mapSourceCount}.");

                if (mapSourceCount == 0)
                    Log.Warning($"{Prefix} No Unity AudioSource objects were found in the loaded RoweGutFace scene.");
                if (activeListeners == 0)
                    Log.Warning($"{Prefix} No active Unity AudioListener exists; Unity AudioSources cannot be heard.");
            }
            catch (Exception ex)
            {
                Log.Error($"{Prefix} Snapshot '{reason}' failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogSource(AudioSource source)
        {
            try
            {
                GameObject go = source.gameObject;
                AudioClip clip = source.clip;
                string clipDetails = clip == null
                    ? "clip=<null>"
                    : $"clip='{clip.name}', loadState={clip.loadState}, length={clip.length:0.###}, " +
                      $"channels={clip.channels}, frequency={clip.frequency}, samples={clip.samples}, " +
                      $"preload={clip.preloadAudioData}, backgroundLoad={clip.loadInBackground}";

                string mixer = source.outputAudioMixerGroup == null
                    ? "<none>"
                    : source.outputAudioMixerGroup.name;

                Log.Msg(
                    $"{Prefix} Source object='{go.name}', scene='{go.scene.name}', " +
                    $"enabled={source.enabled}, activeInHierarchy={go.activeInHierarchy}, " +
                    $"isPlaying={source.isPlaying}, time={source.time:0.###}, timeSamples={source.timeSamples}, " +
                    $"playOnAwake={source.playOnAwake}, loop={source.loop}, mute={source.mute}, " +
                    $"volume={source.volume:0.###}, pitch={source.pitch:0.###}, spatialBlend={source.spatialBlend:0.###}, " +
                    $"minDistance={source.minDistance:0.###}, maxDistance={source.maxDistance:0.###}, " +
                    $"priority={source.priority}, mixer='{mixer}', position={go.transform.position}, {clipDetails}.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"{Prefix} Source inspection failed for '{source?.gameObject?.name ?? "<unknown>"}': " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
