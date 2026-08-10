using Il2CppInterop.Runtime;
using MelonLoader;
using rowemod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace rowemod.Mods
{
    /// <summary>
    /// Discovers the current scene's HDRP Volume components at runtime. Every override has a
    /// captured live value so Game/Reset restores the map instead of relying on guessed defaults.
    /// </summary>
    public static class GraphicsEnvironmentController
    {
        private const string SkyBundleBaseUrl =
            "https://raw.githubusercontent.com/xrowex/RoweMod/master/Bundles/";
        private const long MaximumSkyBundleBytes = 40L * 1024L * 1024L;
        private const int BundleDownloadTimeoutSeconds = 45;
        private const float SceneDiscoveryDelaySeconds = 0.8f;
        private const float ErrorLogCooldownSeconds = 5f;

        private enum EffectKind
        {
            ScreenSpaceReflections,
            AmbientOcclusion,
            GlobalIllumination,
            VolumetricClouds,
            VolumetricFog,
            ContactShadows,
            MotionBlur,
            DepthOfField,
            Bloom
        }

        private sealed class QualitySnapshot
        {
            public bool Captured;
            public float RenderScaleX;
            public float RenderScaleY;
            public ShadowQuality Shadows;
            public float ShadowDistance;
            public float LodBias;
            public int TextureMipmapLimit;
            public float TerrainDetailDensity;
            public float TerrainDetailDistance;
            public bool RealtimeReflectionProbes;
            public HDRenderPipelineAsset PipelineAsset;
            public GlobalDynamicResolutionSettings DynamicResolution;
            public bool DynamicResolutionCaptured;
        }

        private sealed class SkyAssetDefinition
        {
            public readonly string Selection;
            public readonly string DisplayName;
            public readonly string BundleFileName;
            public readonly string AssetFileName;
            public readonly string Sha256;
            public AssetBundle Bundle;
            public Cubemap Cubemap;
            public bool DownloadInProgress;
            public float NextDownloadAttemptTime;

            public SkyAssetDefinition(
                string selection,
                string displayName,
                string bundleFileName,
                string assetFileName,
                string sha256)
            {
                Selection = selection;
                DisplayName = displayName;
                BundleFileName = bundleFileName;
                AssetFileName = assetFileName;
                Sha256 = sha256;
            }
        }

        private sealed class CameraSnapshot
        {
            public HDAdditionalCameraData CameraData;
            public HDAdditionalCameraData.AntialiasingMode Antialiasing;
            public bool AllowDynamicResolution;
        }

        private sealed class LightSnapshot
        {
            public Light Component;
            public LightShadows Shadows;
        }

        private sealed class ShadowDistanceSnapshot
        {
            public HDShadowSettings Component;
            public bool ComponentActive;
            public float Distance;
            public bool DistanceOverride;
        }

        private sealed class SkySnapshot
        {
            public HDRISky Component;
            public string Label;
            public bool ComponentActive;
            public Texture Cubemap;
            public bool CubemapOverride;
            public float Rotation;
            public bool RotationOverride;
            public float Exposure;
            public bool ExposureOverride;
        }

        private sealed class ExposureSnapshot
        {
            public Exposure Component;
            public string Label;
            public string SceneKey;
            public string SceneName;
            public bool ComponentActive;
            public ExposureMode Mode;
            public bool ModeOverride;
            public float FixedExposure;
            public bool FixedExposureOverride;
            public float Compensation;
            public bool CompensationOverride;
        }

        private sealed class ExposureSceneOption
        {
            public string Key;
            public string Name;
            public int ComponentCount;
        }

        private sealed class VisualEnvironmentSnapshot
        {
            public VisualEnvironment Component;
            public bool ComponentActive;
            public int SkyType;
            public bool SkyTypeOverride;
            public SkyAmbientMode AmbientMode;
            public bool AmbientModeOverride;
        }

        private sealed class EffectSnapshot
        {
            public VolumeComponent Component;
            public EffectKind Kind;
            public bool ComponentActive;
            public bool BoolValue;
            public float FloatValue;
            public bool ParameterOverride;
        }

        private static readonly QualitySnapshot OriginalQuality = new QualitySnapshot();
        private static readonly Dictionary<int, CameraSnapshot> OriginalCameras =
            new Dictionary<int, CameraSnapshot>();
        private static readonly Dictionary<int, LightSnapshot> OriginalLights =
            new Dictionary<int, LightSnapshot>();
        private static readonly Dictionary<int, ShadowDistanceSnapshot> OriginalShadowDistances =
            new Dictionary<int, ShadowDistanceSnapshot>();
        private static readonly Dictionary<int, SkySnapshot> OriginalSkies =
            new Dictionary<int, SkySnapshot>();
        private static readonly Dictionary<int, ExposureSnapshot> OriginalExposures =
            new Dictionary<int, ExposureSnapshot>();
        private static readonly Dictionary<int, VisualEnvironmentSnapshot> OriginalVisualEnvironments =
            new Dictionary<int, VisualEnvironmentSnapshot>();
        private static readonly Dictionary<string, EffectSnapshot> OriginalEffects =
            new Dictionary<string, EffectSnapshot>();
        private static readonly SkyAssetDefinition[] CustomSkyAssets =
        {
            new SkyAssetDefinition(
                "Clear",
                "Kloofendal Clear Day",
                "rowemod_hdri_clear",
                "kloofendal_43d_clear_puresky_4k.hdr",
                "076F41EF72C05AA5E02E06DB241A55E6ED4402A004776A3FB98973EC53166E30"),
            new SkyAssetDefinition(
                "Sunset",
                "Industrial Sunset",
                "rowemod_hdri_sunset",
                "industrial_sunset_02_puresky_4k.exr",
                "A944EAF701FAC9CAEA5E30E494F409FEA308AB087A7CAEA06090FA1C04237753"),
            new SkyAssetDefinition(
                "Night",
                "Qwantani Night",
                "rowemod_hdri_skies",
                "qwantani_night_puresky_4k.exr",
                "2C3662878D2E5778EEF8C7AA2A0A4B6CD33FF2448ED5B12CA846370BC65A4098")
        };
        private static bool _sceneDiscoveryPending = true;
        private static bool _performanceValuesInitialized;
        private static float _nextDiscoveryTime;
        private static float _nextErrorLogTime;
        private static bool _renderScaleFallbackWarningLogged;
        private static bool _renderScaleRestoreWarningLogged;
        private static bool _exposureSceneDropdownOpen;
        private static bool _performanceAdvancedExpanded;
        private static string _selectedExposureSceneKey = string.Empty;
        private static string _status = "Waiting to scan the current HDRP scene.";

        public static string Status => _status;
        public static int SkyCount => OriginalSkies.Values.Count(IsAlive);
        public static int ExposureCount => OriginalExposures.Values.Count(IsAlive);
        public static int PerformanceEffectCount => OriginalEffects.Values.Count(IsAlive);
        public static bool RequiresUpdate => _sceneDiscoveryPending;

        public static void OnSceneInitialized()
        {
            try
            {
                RestoreSceneObjects();
            }
            catch (Exception ex)
            {
                ReportRuntimeFailure("restoring the previous scene", ex);
            }
            finally
            {
                OriginalCameras.Clear();
                OriginalLights.Clear();
                OriginalShadowDistances.Clear();
                OriginalSkies.Clear();
                OriginalExposures.Clear();
                OriginalVisualEnvironments.Clear();
                OriginalEffects.Clear();
            }
            _performanceValuesInitialized = false;
            _exposureSceneDropdownOpen = false;
            _sceneDiscoveryPending = true;
            _nextDiscoveryTime = Time.realtimeSinceStartup + SceneDiscoveryDelaySeconds;
            _status = "Waiting for the map's HDRP volumes.";
        }

        public static void Update()
        {
            if (!_sceneDiscoveryPending || Time.realtimeSinceStartup < _nextDiscoveryTime)
                return;

            // Scene initialization is the only automatic application point. Menu controls call
            // ApplyConfiguredSettings directly when the user changes a value, so no periodic
            // quality, light, Volume, or render-pipeline writes are needed during gameplay.
            RefreshSceneBindingsSafely(true);
        }

        public static void DrawControls()
        {
            if (_sceneDiscoveryPending)
                RefreshSceneBindingsSafely(false);

            DrawPerformanceControls();
            DrawEnvironmentControls();
        }

        public static void DrawPerformancePage()
        {
            if (_sceneDiscoveryPending)
                RefreshSceneBindingsSafely(false);

            DrawPerformanceControls();
        }

        public static void DrawEnvironmentPage()
        {
            if (_sceneDiscoveryPending)
                RefreshSceneBindingsSafely(false);

            DrawEnvironmentControls();
        }

        public static void ResetToGameDefaults()
        {
            RestoreQuality();
            RestoreAllEffects();
            RestoreEnvironment();
            Config.ResetGraphicsSettings();
            SyncPerformanceSettingsFromGame(Config.graphicsSettings);
            ReleaseUnusedSkyAssets(null);
            _status = "Restored the captured game and map graphics values.";
            Log.Msg("[Graphics] Restored captured game/map values.");
        }

        private static void DrawPerformanceControls()
        {
            GraphicsSettings settings = Config.graphicsSettings ??= new GraphicsSettings();
            Config.NormalizeGraphicsSettings(settings);

            Menu.BeginPane(
                "Performance Presets",
                "Reversible HDRP presets. Game restores the exact values captured before RoweMod changed them.");
            Menu.BeginToolbar();
            if (Menu.PillButton("Game", settings.performancePreset == 0, GUILayout.ExpandWidth(true)))
                SetPerformancePreset(0);
            if (Menu.PillButton("Balanced", settings.performancePreset == 1, GUILayout.ExpandWidth(true)))
                SetPerformancePreset(1);
            if (Menu.PillButton("Low", settings.performancePreset == 2, GUILayout.ExpandWidth(true)))
                SetPerformancePreset(2);
            if (Menu.PillButton("Potato", settings.performancePreset == 3, GUILayout.ExpandWidth(true)))
                SetPerformancePreset(3);
            Menu.EndToolbar();

            string presetName = GetPresetName(settings.performancePreset);
            Menu.DrawStatusBadge(
                $"{presetName}  •  Internal render scale " +
                $"{(settings.performancePreset == 0 ? OriginalQuality.RenderScaleX : settings.renderScale) * 100f:0}%  •  " +
                $"{OriginalCameras.Values.Count(IsAlive)} HDRP camera(s)");
            GUILayout.Label(
                "Potato is intentionally aggressive: 60% internal resolution, no realtime shadows, quarter-resolution textures, " +
                "lower LOD/terrain detail, no antialiasing, and costly HDRP Volume effects forced off.",
                Menu.UiMutedWrappedStyle);

            _performanceAdvancedExpanded = Menu.ModernFoldout(
                settings.performancePreset == 4 ? "Custom Quality Controls (Active)" : "Customize Quality (Advanced)",
                _performanceAdvancedExpanded);
            if (!_performanceAdvancedExpanded)
            {
                Menu.EndPane();
                return;
            }

            float renderScale = settings.renderScale * 100f;
            Menu.ModernSlider("Internal Render Scale (%)", ref renderScale, 50f, 100f, "graphics_render_scale");
            bool changed = !Mathf.Approximately(renderScale / 100f, settings.renderScale);
            settings.renderScale = renderScale / 100f;

            bool realtimeShadows = settings.realtimeShadows;
            Menu.ModernToggle("Allow Realtime Shadows", ref realtimeShadows, "graphics_realtime_shadows");
            changed |= realtimeShadows != settings.realtimeShadows;
            settings.realtimeShadows = realtimeShadows;

            float shadowDistance = settings.shadowDistance;
            Menu.ModernSlider("Shadow Distance", ref shadowDistance, 0f, 500f, "graphics_shadow_distance");
            changed |= !Mathf.Approximately(shadowDistance, settings.shadowDistance);
            settings.shadowDistance = shadowDistance;

            float lodBias = settings.lodBias;
            Menu.ModernSlider("World Detail / LOD", ref lodBias, 0.25f, 2f, "graphics_lod_bias");
            changed |= !Mathf.Approximately(lodBias, settings.lodBias);
            settings.lodBias = lodBias;

            float terrainDensity = settings.terrainDetailDensity * 100f;
            Menu.ModernSlider("Terrain Detail Density (%)", ref terrainDensity, 0f, 100f, "graphics_terrain_density");
            changed |= !Mathf.Approximately(terrainDensity / 100f, settings.terrainDetailDensity);
            settings.terrainDetailDensity = terrainDensity / 100f;

            float terrainDistance = settings.terrainDetailDistance;
            Menu.ModernSlider("Terrain Detail Distance", ref terrainDistance, 0f, 250f, "graphics_terrain_distance");
            changed |= !Mathf.Approximately(terrainDistance, settings.terrainDetailDistance);
            settings.terrainDetailDistance = terrainDistance;

            Menu.DrawSectionTitle("Texture Resolution", "Higher mip limits use less VRAM but look softer.");
            int textureMipmapLimit = settings.textureMipmapLimit;
            Menu.BeginToolbar();
            changed |= DrawIntegerChoice("Full", 0, ref textureMipmapLimit);
            changed |= DrawIntegerChoice("Half", 1, ref textureMipmapLimit);
            changed |= DrawIntegerChoice("Quarter", 2, ref textureMipmapLimit);
            changed |= DrawIntegerChoice("Eighth", 3, ref textureMipmapLimit);
            Menu.EndToolbar();
            settings.textureMipmapLimit = textureMipmapLimit;

            Menu.DrawSectionTitle("Antialiasing", "FXAA is the inexpensive option; TAA and SMAA cost more.");
            int antiAliasingMode = settings.antiAliasingMode;
            Menu.BeginToolbar();
            changed |= DrawIntegerChoice("Off", 0, ref antiAliasingMode);
            changed |= DrawIntegerChoice("FXAA", 1, ref antiAliasingMode);
            changed |= DrawIntegerChoice("TAA", 2, ref antiAliasingMode);
            changed |= DrawIntegerChoice("SMAA", 3, ref antiAliasingMode);
            Menu.EndToolbar();
            settings.antiAliasingMode = antiAliasingMode;

            Menu.DrawSectionTitle(
                "Expensive HDRP Effects",
                $"Dynamically found {PerformanceEffectCount} supported effect component(s). Turn an option off to force that effect off in every map Volume.");
            bool allowScreenSpaceReflections = settings.allowScreenSpaceReflections;
            bool allowAmbientOcclusion = settings.allowAmbientOcclusion;
            bool allowGlobalIllumination = settings.allowGlobalIllumination;
            bool allowVolumetricClouds = settings.allowVolumetricClouds;
            bool allowVolumetricFog = settings.allowVolumetricFog;
            bool allowContactShadows = settings.allowContactShadows;
            bool allowMotionBlur = settings.allowMotionBlur;
            bool allowDepthOfField = settings.allowDepthOfField;
            bool allowBloom = settings.allowBloom;
            bool allowRealtimeReflectionProbes = settings.allowRealtimeReflectionProbes;
            changed |= DrawAllowToggle("Screen-space Reflections", ref allowScreenSpaceReflections, "graphics_ssr");
            changed |= DrawAllowToggle("Ambient Occlusion", ref allowAmbientOcclusion, "graphics_ao");
            changed |= DrawAllowToggle("Screen-space Global Illumination", ref allowGlobalIllumination, "graphics_ssgi");
            changed |= DrawAllowToggle("Volumetric Clouds", ref allowVolumetricClouds, "graphics_clouds");
            changed |= DrawAllowToggle("Volumetric Fog", ref allowVolumetricFog, "graphics_fog");
            changed |= DrawAllowToggle("Contact Shadows", ref allowContactShadows, "graphics_contact_shadows");
            changed |= DrawAllowToggle("Motion Blur", ref allowMotionBlur, "graphics_motion_blur");
            changed |= DrawAllowToggle("Depth of Field", ref allowDepthOfField, "graphics_dof");
            changed |= DrawAllowToggle("Bloom", ref allowBloom, "graphics_bloom");
            changed |= DrawAllowToggle("Realtime Reflection Probes", ref allowRealtimeReflectionProbes, "graphics_reflection_probes");
            settings.allowScreenSpaceReflections = allowScreenSpaceReflections;
            settings.allowAmbientOcclusion = allowAmbientOcclusion;
            settings.allowGlobalIllumination = allowGlobalIllumination;
            settings.allowVolumetricClouds = allowVolumetricClouds;
            settings.allowVolumetricFog = allowVolumetricFog;
            settings.allowContactShadows = allowContactShadows;
            settings.allowMotionBlur = allowMotionBlur;
            settings.allowDepthOfField = allowDepthOfField;
            settings.allowBloom = allowBloom;
            settings.allowRealtimeReflectionProbes = allowRealtimeReflectionProbes;

            if (changed)
            {
                settings.performancePreset = 4;
                Config.NormalizeGraphicsSettings(settings);
                ApplyConfiguredSettings(true);
                Config.RequestSave();
            }
            Menu.EndPane();
        }

        private static void DrawEnvironmentControls()
        {
            GraphicsSettings settings = Config.graphicsSettings ??= new GraphicsSettings();
            Menu.BeginAltPane(
                "HDRI Sky & Exposure",
                "RoweMod discovers live HDRP Volume profiles and saves camera exposure independently for each Unity scene.");

            Menu.BeginToolbar();
            if (Menu.PillButton("Map Sky", IsMapSky(settings.skySelection), GUILayout.ExpandWidth(true)))
                SelectSky("Map");
            if (Menu.PillButton("Clear Day", IsSkySelection(settings.skySelection, "Clear"), GUILayout.ExpandWidth(true)))
                SelectSky("Clear");
            if (Menu.PillButton("Sunset", IsSkySelection(settings.skySelection, "Sunset"), GUILayout.ExpandWidth(true)))
                SelectSky("Sunset");
            if (Menu.PillButton("Night", IsSkySelection(settings.skySelection, "Night"), GUILayout.ExpandWidth(true)))
                SelectSky("Night");
            Menu.EndToolbar();
            if (Menu.SecondaryButton("Rescan HDRP Volumes", GUILayout.ExpandWidth(true), GUILayout.Height(28f)))
                RefreshSceneBindingsSafely(true);

            Menu.DrawStatusBadge(
                $"Found {SkyCount} HDRI sky component(s)  •  {ExposureCount} exposure component(s) across {GetExposureSceneOptions().Count} scene(s)");
            GUILayout.Label(Status, Menu.UiMutedWrappedStyle);
            GUILayout.Label(
                "Custom skies replace the realtime sky and dynamic environment lighting. A map's baked lightmaps and baked reflection probes cannot be changed at runtime.",
                Menu.UiMutedWrappedStyle);

            bool changed = false;
            bool skyTuning = settings.skyTuningOverride;
            Menu.ModernToggle("Override Sky Rotation / Brightness", ref skyTuning, "graphics_sky_tuning");
            if (skyTuning != settings.skyTuningOverride)
            {
                if (skyTuning)
                    SeedSkyTuningFromScene(settings);
                settings.skyTuningOverride = skyTuning;
                changed = true;
            }

            if (settings.skyTuningOverride)
            {
                float rotation = settings.skyRotation;
                Menu.ModernSlider("Sky Rotation", ref rotation, 0f, 360f, "graphics_sky_rotation");
                changed |= !Mathf.Approximately(rotation, settings.skyRotation);
                settings.skyRotation = rotation;

                float skyExposure = settings.skyExposure;
                Menu.ModernSlider("Sky Exposure", ref skyExposure, -10f, 10f, "graphics_sky_exposure");
                changed |= !Mathf.Approximately(skyExposure, settings.skyExposure);
                settings.skyExposure = skyExposure;
            }

            GUILayout.Space(8f);
            changed |= DrawSceneExposureControls(settings);

            if (changed)
            {
                Config.NormalizeGraphicsSettings(settings);
                ApplyConfiguredSettings(true);
                Config.RequestSave();
            }
            Menu.EndPane();
        }

        private static bool DrawSceneExposureControls(GraphicsSettings settings)
        {
            GUILayout.Label("Camera Exposure by Scene", Menu.UiHeaderStyle);
            List<ExposureSceneOption> scenes = GetExposureSceneOptions();
            ExposureSceneOption selected = ResolveSelectedExposureScene(scenes);
            if (selected == null)
            {
                _exposureSceneDropdownOpen = false;
                GUILayout.Label(
                    "No loaded Unity scene currently exposes an HDRP Exposure component.",
                    Menu.UiMutedWrappedStyle);
                return false;
            }

            string selectorLabel = $"Scene: {selected.Name} ({selected.ComponentCount})  v";
            if (Menu.SecondaryButton(selectorLabel, GUILayout.ExpandWidth(true), GUILayout.Height(28f)))
                _exposureSceneDropdownOpen = !_exposureSceneDropdownOpen;

            if (_exposureSceneDropdownOpen)
            {
                Menu.BeginAltPanel(GUILayout.ExpandWidth(true));
                foreach (ExposureSceneOption option in scenes)
                {
                    string label = $"{option.Name}  ({option.ComponentCount} exposure component(s))";
                    if (!Menu.PillButton(
                            label,
                            string.Equals(option.Key, selected.Key, StringComparison.OrdinalIgnoreCase),
                            GUILayout.ExpandWidth(true),
                            GUILayout.Height(26f)))
                        continue;

                    _selectedExposureSceneKey = option.Key;
                    selected = option;
                    _exposureSceneDropdownOpen = false;
                }
                Menu.EndPanel();
            }

            SceneExposureSettings sceneSettings = GetOrCreateSceneExposureSettings(settings, selected);
            GUILayout.Label(
                $"Changes below affect only {selected.Name}; its values are saved separately from every other scene.",
                Menu.UiMutedWrappedStyle);

            bool changed = false;
            bool exposureOverride = sceneSettings.exposureOverride;
            Menu.ModernToggle(
                "Override Camera Exposure",
                ref exposureOverride,
                "graphics_exposure_override_" + selected.Key);
            if (exposureOverride != sceneSettings.exposureOverride)
            {
                if (exposureOverride)
                    SeedExposureFromScene(sceneSettings, selected.Key);
                sceneSettings.exposureOverride = exposureOverride;
                changed = true;
            }

            if (!sceneSettings.exposureOverride)
            {
                GUILayout.Label("Map Default: this scene's captured exposure is untouched.", Menu.UiMutedStyle);
                return changed;
            }

            float compensation = sceneSettings.exposureCompensation;
            Menu.ModernSlider(
                "Exposure Compensation",
                ref compensation,
                -10f,
                10f,
                "graphics_exposure_compensation_" + selected.Key);
            changed |= !Mathf.Approximately(compensation, sceneSettings.exposureCompensation);
            sceneSettings.exposureCompensation = compensation;

            bool fixedExposure = sceneSettings.forceFixedExposure;
            Menu.ModernToggle(
                "Force Fixed Exposure",
                ref fixedExposure,
                "graphics_fixed_exposure_toggle_" + selected.Key);
            changed |= fixedExposure != sceneSettings.forceFixedExposure;
            sceneSettings.forceFixedExposure = fixedExposure;

            if (sceneSettings.forceFixedExposure)
            {
                float fixedValue = sceneSettings.fixedExposure;
                Menu.ModernSlider(
                    "Fixed Exposure (EV)",
                    ref fixedValue,
                    -10f,
                    10f,
                    "graphics_fixed_exposure_" + selected.Key);
                changed |= !Mathf.Approximately(fixedValue, sceneSettings.fixedExposure);
                sceneSettings.fixedExposure = fixedValue;
            }

            return changed;
        }

        private static bool DrawIntegerChoice(string label, int value, ref int target)
        {
            if (!Menu.PillButton(label, target == value, GUILayout.ExpandWidth(true)))
                return false;
            if (target == value)
                return false;
            target = value;
            return true;
        }

        private static bool DrawAllowToggle(string label, ref bool value, string id)
        {
            bool before = value;
            Menu.ModernToggle("Allow " + label, ref value, id);
            return before != value;
        }

        private static void SetPerformancePreset(int preset)
        {
            GraphicsSettings settings = Config.graphicsSettings ??= new GraphicsSettings();
            if (preset == 0)
            {
                settings.performancePreset = 0;
                RestoreQuality();
                RestoreAllEffects();
                SyncPerformanceSettingsFromGame(settings);
            }
            else
            {
                settings.performancePreset = preset;
                ApplyPresetValues(settings, preset);
                ApplyConfiguredSettings(true);
            }

            Config.NormalizeGraphicsSettings(settings);
            Config.RequestSave();
            _status = preset == 0
                ? "Performance overrides restored to captured game values."
                : $"{GetPresetName(preset)} performance preset applied.";
            Log.Msg("[Graphics] " + _status);
        }

        private static void ApplyPresetValues(GraphicsSettings settings, int preset)
        {
            // Reset allow flags first so moving from Potato to a higher preset restores effects.
            settings.allowScreenSpaceReflections = preset == 1;
            settings.allowAmbientOcclusion = preset == 1;
            settings.allowGlobalIllumination = false;
            settings.allowVolumetricClouds = false;
            settings.allowVolumetricFog = preset == 1;
            settings.allowContactShadows = preset == 1;
            settings.allowMotionBlur = false;
            settings.allowDepthOfField = false;
            settings.allowBloom = preset != 3;
            settings.allowRealtimeReflectionProbes = preset == 1;

            switch (preset)
            {
                case 1: // Balanced
                    settings.renderScale = 0.9f;
                    settings.realtimeShadows = true;
                    settings.shadowDistance = 70f;
                    settings.lodBias = 0.85f;
                    settings.textureMipmapLimit = 0;
                    settings.antiAliasingMode = 1;
                    settings.terrainDetailDensity = 0.75f;
                    settings.terrainDetailDistance = 90f;
                    break;
                case 2: // Low
                    settings.renderScale = 0.75f;
                    settings.realtimeShadows = true;
                    settings.shadowDistance = 40f;
                    settings.lodBias = 0.65f;
                    settings.textureMipmapLimit = 1;
                    settings.antiAliasingMode = 1;
                    settings.terrainDetailDensity = 0.4f;
                    settings.terrainDetailDistance = 50f;
                    break;
                default: // Potato
                    settings.renderScale = 0.6f;
                    settings.realtimeShadows = false;
                    settings.shadowDistance = 20f;
                    settings.lodBias = 0.45f;
                    settings.textureMipmapLimit = 2;
                    settings.antiAliasingMode = 0;
                    settings.terrainDetailDensity = 0.15f;
                    settings.terrainDetailDistance = 25f;
                    break;
            }
        }

        private static string GetPresetName(int preset)
        {
            return preset switch
            {
                1 => "Balanced",
                2 => "Low",
                3 => "Potato",
                4 => "Custom",
                _ => "Game"
            };
        }

        private static void SelectSky(string selection)
        {
            GraphicsSettings settings = Config.graphicsSettings ??= new GraphicsSettings();
            settings.skySelection = selection;
            SkyAssetDefinition definition = FindSkyDefinition(selection);
            if (definition != null)
                EnsureSkyAsset(definition);
            ApplyEnvironment(true);
            Config.RequestSave();
        }

        private static void RefreshSceneBindings(bool logResult)
        {
            CaptureOriginalQuality();
            CaptureCameras();
            CaptureLights();

            HashSet<int> liveSkyIds = new HashSet<int>();
            HashSet<int> liveExposureIds = new HashSet<int>();
            HashSet<int> liveVisualEnvironmentIds = new HashSet<int>();
            HashSet<int> liveShadowDistanceIds = new HashSet<int>();
            HashSet<string> liveEffectKeys = new HashSet<string>();
            Volume[] volumes = Resources.FindObjectsOfTypeAll<Volume>();
            if (volumes != null)
            {
                foreach (Volume volume in volumes)
                {
                    if (volume == null || !IsLoadedSceneObject(volume))
                        continue;

                    VolumeProfile profile;
                    try
                    {
                        profile = volume.profileRef;
                    }
                    catch
                    {
                        continue;
                    }

                    if (profile?.components == null || IsRoweModVolume(volume, profile))
                        continue;

                    string label = BuildVolumeLabel(volume, profile);
                    string sceneKey = BuildSceneKey(volume.gameObject.scene);
                    string sceneName = BuildSceneName(volume.gameObject.scene);
                    for (int i = 0; i < profile.components.Count; i++)
                    {
                        VolumeComponent component = profile.components[i];
                        if (component == null)
                            continue;

                        HDRISky sky = TryCast<HDRISky>(component);
                        if (sky != null)
                        {
                            int id = component.GetInstanceID();
                            liveSkyIds.Add(id);
                            if (!OriginalSkies.ContainsKey(id))
                                OriginalSkies[id] = CaptureSky(sky, label);
                            continue;
                        }

                        Exposure exposure = TryCast<Exposure>(component);
                        if (exposure != null)
                        {
                            int id = component.GetInstanceID();
                            liveExposureIds.Add(id);
                            if (!OriginalExposures.ContainsKey(id))
                                OriginalExposures[id] = CaptureExposure(
                                    exposure,
                                    label,
                                    sceneKey,
                                    sceneName);
                            continue;
                        }

                        VisualEnvironment visualEnvironment = TryCast<VisualEnvironment>(component);
                        if (visualEnvironment != null)
                        {
                            int id = component.GetInstanceID();
                            liveVisualEnvironmentIds.Add(id);
                            if (!OriginalVisualEnvironments.ContainsKey(id))
                            {
                                OriginalVisualEnvironments[id] = new VisualEnvironmentSnapshot
                                {
                                    Component = visualEnvironment,
                                    ComponentActive = visualEnvironment.active,
                                    SkyType = visualEnvironment.skyType.value,
                                    SkyTypeOverride = visualEnvironment.skyType.overrideState,
                                    AmbientMode = visualEnvironment.skyAmbientMode.value,
                                    AmbientModeOverride = visualEnvironment.skyAmbientMode.overrideState
                                };
                            }
                            continue;
                        }

                        HDShadowSettings shadowSettings = TryCast<HDShadowSettings>(component);
                        if (shadowSettings != null)
                        {
                            int id = component.GetInstanceID();
                            liveShadowDistanceIds.Add(id);
                            if (!OriginalShadowDistances.ContainsKey(id))
                            {
                                OriginalShadowDistances[id] = new ShadowDistanceSnapshot
                                {
                                    Component = shadowSettings,
                                    ComponentActive = shadowSettings.active,
                                    Distance = shadowSettings.maxShadowDistance.value,
                                    DistanceOverride = shadowSettings.maxShadowDistance.overrideState
                                };
                            }
                            continue;
                        }

                        CaptureEffect(component, EffectKind.ScreenSpaceReflections, TryCast<ScreenSpaceReflection>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.AmbientOcclusion, TryCast<ScreenSpaceAmbientOcclusion>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.GlobalIllumination, TryCast<GlobalIllumination>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.VolumetricClouds, TryCast<VolumetricClouds>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.VolumetricFog, TryCast<Fog>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.ContactShadows, TryCast<ContactShadows>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.MotionBlur, TryCast<MotionBlur>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.DepthOfField, TryCast<DepthOfField>(component), liveEffectKeys);
                        CaptureEffect(component, EffectKind.Bloom, TryCast<Bloom>(component), liveEffectKeys);
                    }
                }
            }

            RemoveMissing(OriginalSkies, liveSkyIds);
            RemoveMissing(OriginalExposures, liveExposureIds);
            RemoveMissing(OriginalVisualEnvironments, liveVisualEnvironmentIds);
            RemoveMissing(OriginalShadowDistances, liveShadowDistanceIds);
            RemoveMissing(OriginalEffects, liveEffectKeys);
            _sceneDiscoveryPending = false;

            GraphicsSettings settings = Config.graphicsSettings ??= new GraphicsSettings();
            if (!_performanceValuesInitialized && settings.performancePreset == 0)
                SyncPerformanceSettingsFromGame(settings);
            _performanceValuesInitialized = true;
            MigrateLegacyExposureSettings(settings);
            ResolveSelectedExposureScene(GetExposureSceneOptions());

            SkyAssetDefinition selectedSky = FindSkyDefinition(settings.skySelection);
            if (selectedSky != null)
                EnsureSkyAsset(selectedSky);
            ApplyConfiguredSettings(false);

            _status = $"Live scan: {SkyCount} HDRI sky, {ExposureCount} exposure across " +
                      $"{GetExposureSceneOptions().Count} scene(s), " +
                      $"{OriginalVisualEnvironments.Values.Count(IsAlive)} visual environment, " +
                      $"{OriginalShadowDistances.Values.Count(IsAlive)} HDRP shadow setting, " +
                      $"{OriginalLights.Values.Count(IsAlive)} scene light(s), and " +
                      $"{PerformanceEffectCount} supported performance-effect component(s).";
            if (logResult)
                Log.Msg("[Graphics] " + _status);
        }

        private static void RefreshSceneBindingsSafely(bool logResult)
        {
            try
            {
                RefreshSceneBindings(logResult);
            }
            catch (Exception ex)
            {
                _sceneDiscoveryPending = false;
                ReportRuntimeFailure("scanning HDRP Volumes", ex);
            }
        }

        private static void CaptureOriginalQuality()
        {
            if (OriginalQuality.Captured)
                return;

            OriginalQuality.RenderScaleX = ScalableBufferManager.widthScaleFactor;
            OriginalQuality.RenderScaleY = ScalableBufferManager.heightScaleFactor;
            OriginalQuality.Shadows = QualitySettings.shadows;
            OriginalQuality.ShadowDistance = QualitySettings.shadowDistance;
            OriginalQuality.LodBias = QualitySettings.lodBias;
            OriginalQuality.TextureMipmapLimit = QualitySettings.globalTextureMipmapLimit;
            OriginalQuality.TerrainDetailDensity = QualitySettings.terrainDetailDensityScale;
            OriginalQuality.TerrainDetailDistance = QualitySettings.terrainDetailDistance;
            OriginalQuality.RealtimeReflectionProbes = QualitySettings.realtimeReflectionProbes;
            try
            {
                RenderPipelineAsset pipelineAsset = QualitySettings.renderPipeline;
                if (pipelineAsset == null)
                    pipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                HDRenderPipelineAsset hdAsset = pipelineAsset?.TryCast<HDRenderPipelineAsset>();
                if (hdAsset != null)
                {
                    RenderPipelineSettings pipelineSettings = hdAsset.currentPlatformRenderPipelineSettings;
                    OriginalQuality.PipelineAsset = hdAsset;
                    OriginalQuality.DynamicResolution = pipelineSettings.dynamicResolutionSettings;
                    OriginalQuality.DynamicResolutionCaptured = true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[Graphics] Could not capture HDRP dynamic-resolution settings: " + ex.Message);
            }
            OriginalQuality.Captured = true;
            Log.Msg(
                $"[Graphics] Captured game quality: render={OriginalQuality.RenderScaleX:0.##}, " +
                $"shadows={OriginalQuality.Shadows}/{OriginalQuality.ShadowDistance:0.#}m, " +
                $"lod={OriginalQuality.LodBias:0.##}, mip={OriginalQuality.TextureMipmapLimit}, " +
                $"hdrpDynamicResolution={OriginalQuality.DynamicResolutionCaptured}.");
        }

        private static void CaptureCameras()
        {
            HDAdditionalCameraData[] cameras = Resources.FindObjectsOfTypeAll<HDAdditionalCameraData>();
            if (cameras == null)
                return;

            HashSet<int> liveIds = new HashSet<int>();
            foreach (HDAdditionalCameraData camera in cameras)
            {
                if (camera == null || !IsLoadedSceneObject(camera))
                    continue;
                int id = camera.GetInstanceID();
                liveIds.Add(id);
                if (OriginalCameras.ContainsKey(id))
                    continue;
                OriginalCameras[id] = new CameraSnapshot
                {
                    CameraData = camera,
                    Antialiasing = camera.antialiasing,
                    AllowDynamicResolution = camera.allowDynamicResolution
                };
            }
            RemoveMissing(OriginalCameras, liveIds);
        }

        private static void CaptureLights()
        {
            Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
            if (lights == null)
                return;

            HashSet<int> liveIds = new HashSet<int>();
            foreach (Light light in lights)
            {
                if (light == null || !IsLoadedSceneObject(light))
                    continue;
                int id = light.GetInstanceID();
                liveIds.Add(id);
                if (OriginalLights.ContainsKey(id))
                    continue;
                OriginalLights[id] = new LightSnapshot
                {
                    Component = light,
                    Shadows = light.shadows
                };
            }
            RemoveMissing(OriginalLights, liveIds);
        }

        private static SkySnapshot CaptureSky(HDRISky sky, string label)
        {
            return new SkySnapshot
            {
                Component = sky,
                Label = label,
                ComponentActive = sky.active,
                Cubemap = sky.hdriSky.value,
                CubemapOverride = sky.hdriSky.overrideState,
                Rotation = sky.rotation.value,
                RotationOverride = sky.rotation.overrideState,
                Exposure = sky.exposure.value,
                ExposureOverride = sky.exposure.overrideState
            };
        }

        private static ExposureSnapshot CaptureExposure(
            Exposure exposure,
            string label,
            string sceneKey,
            string sceneName)
        {
            return new ExposureSnapshot
            {
                Component = exposure,
                Label = label,
                SceneKey = sceneKey,
                SceneName = sceneName,
                ComponentActive = exposure.active,
                Mode = exposure.mode.value,
                ModeOverride = exposure.mode.overrideState,
                FixedExposure = exposure.fixedExposure.value,
                FixedExposureOverride = exposure.fixedExposure.overrideState,
                Compensation = exposure.compensation.value,
                CompensationOverride = exposure.compensation.overrideState
            };
        }

        private static void CaptureEffect<T>(
            VolumeComponent raw,
            EffectKind kind,
            T typed,
            HashSet<string> liveKeys) where T : VolumeComponent
        {
            if (typed == null)
                return;

            string key = BuildEffectKey(raw.GetInstanceID(), kind);
            liveKeys.Add(key);
            if (OriginalEffects.ContainsKey(key))
                return;

            EffectSnapshot snapshot = new EffectSnapshot
            {
                Component = raw,
                Kind = kind,
                ComponentActive = raw.active
            };
            switch (kind)
            {
                case EffectKind.ScreenSpaceReflections:
                    ScreenSpaceReflection ssr = typed.TryCast<ScreenSpaceReflection>();
                    snapshot.BoolValue = ssr.enabled.value;
                    snapshot.ParameterOverride = ssr.enabled.overrideState;
                    break;
                case EffectKind.AmbientOcclusion:
                    ScreenSpaceAmbientOcclusion ao = typed.TryCast<ScreenSpaceAmbientOcclusion>();
                    snapshot.FloatValue = ao.intensity.value;
                    snapshot.ParameterOverride = ao.intensity.overrideState;
                    break;
                case EffectKind.GlobalIllumination:
                    GlobalIllumination gi = typed.TryCast<GlobalIllumination>();
                    snapshot.BoolValue = gi.enable.value;
                    snapshot.ParameterOverride = gi.enable.overrideState;
                    break;
                case EffectKind.VolumetricClouds:
                    VolumetricClouds clouds = typed.TryCast<VolumetricClouds>();
                    snapshot.BoolValue = clouds.enable.value;
                    snapshot.ParameterOverride = clouds.enable.overrideState;
                    break;
                case EffectKind.VolumetricFog:
                    Fog fog = typed.TryCast<Fog>();
                    snapshot.BoolValue = fog.enableVolumetricFog.value;
                    snapshot.ParameterOverride = fog.enableVolumetricFog.overrideState;
                    break;
                case EffectKind.ContactShadows:
                    ContactShadows contact = typed.TryCast<ContactShadows>();
                    snapshot.BoolValue = contact.enable.value;
                    snapshot.ParameterOverride = contact.enable.overrideState;
                    break;
                case EffectKind.MotionBlur:
                    MotionBlur motion = typed.TryCast<MotionBlur>();
                    snapshot.FloatValue = motion.intensity.value;
                    snapshot.ParameterOverride = motion.intensity.overrideState;
                    break;
                case EffectKind.Bloom:
                    Bloom bloom = typed.TryCast<Bloom>();
                    snapshot.FloatValue = bloom.intensity.value;
                    snapshot.ParameterOverride = bloom.intensity.overrideState;
                    break;
            }
            OriginalEffects[key] = snapshot;
        }

        private static void ApplyConfiguredSettings(bool restoreAllowedValues)
        {
            GraphicsSettings settings = Config.graphicsSettings;
            if (settings == null)
                return;
            Config.NormalizeGraphicsSettings(settings);

            if (settings.performancePreset == 0)
            {
                if (restoreAllowedValues)
                {
                    RestoreQuality();
                    RestoreAllEffects();
                }
            }
            else
            {
                ApplyQuality(settings, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.ScreenSpaceReflections, settings.allowScreenSpaceReflections, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.AmbientOcclusion, settings.allowAmbientOcclusion, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.GlobalIllumination, settings.allowGlobalIllumination, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.VolumetricClouds, settings.allowVolumetricClouds, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.VolumetricFog, settings.allowVolumetricFog, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.ContactShadows, settings.allowContactShadows, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.MotionBlur, settings.allowMotionBlur, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.DepthOfField, settings.allowDepthOfField, restoreAllowedValues);
                ApplyEffectPermission(EffectKind.Bloom, settings.allowBloom, restoreAllowedValues);
            }

            if (restoreAllowedValues || !IsMapSky(settings.skySelection) ||
                settings.skyTuningOverride || settings.exposureOverride ||
                HasSceneExposureOverride(settings))
                ApplyEnvironment(restoreAllowedValues);
        }

        private static void ApplyQuality(GraphicsSettings settings, bool restoreAllowedValues)
        {
            CaptureOriginalQuality();
            CaptureLights();
            float scale = Mathf.Clamp(settings.renderScale, 0.5f, 1f);
            ApplyRenderScale(scale);
            QualitySettings.shadows = settings.realtimeShadows
                ? OriginalQuality.Shadows
                : ShadowQuality.Disable;
            QualitySettings.shadowDistance = Mathf.Max(0f, settings.shadowDistance);
            QualitySettings.lodBias = Mathf.Max(0.25f, settings.lodBias);
            QualitySettings.globalTextureMipmapLimit = settings.textureMipmapLimit;
            QualitySettings.terrainDetailDensityScale = Mathf.Clamp01(settings.terrainDetailDensity);
            QualitySettings.terrainDetailDistance = Mathf.Max(0f, settings.terrainDetailDistance);
            QualitySettings.realtimeReflectionProbes = settings.allowRealtimeReflectionProbes &&
                                                       OriginalQuality.RealtimeReflectionProbes;
            ApplyRealtimeShadowPermission(settings.realtimeShadows, restoreAllowedValues);
            ApplyHDRPShadowDistance(settings.realtimeShadows ? settings.shadowDistance : 0f);

            HDAdditionalCameraData.AntialiasingMode mode =
                (HDAdditionalCameraData.AntialiasingMode)Mathf.Clamp(settings.antiAliasingMode, 0, 3);
            foreach (CameraSnapshot snapshot in OriginalCameras.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                snapshot.CameraData.antialiasing = mode;
                snapshot.CameraData.allowDynamicResolution = scale < 0.999f || snapshot.AllowDynamicResolution;
            }
        }

        private static void RestoreQuality()
        {
            if (!OriginalQuality.Captured)
                return;
            RestoreRenderScale();
            QualitySettings.shadows = OriginalQuality.Shadows;
            QualitySettings.shadowDistance = OriginalQuality.ShadowDistance;
            QualitySettings.lodBias = OriginalQuality.LodBias;
            QualitySettings.globalTextureMipmapLimit = OriginalQuality.TextureMipmapLimit;
            QualitySettings.terrainDetailDensityScale = OriginalQuality.TerrainDetailDensity;
            QualitySettings.terrainDetailDistance = OriginalQuality.TerrainDetailDistance;
            QualitySettings.realtimeReflectionProbes = OriginalQuality.RealtimeReflectionProbes;
            RestoreRealtimeLights();
            RestoreHDRPShadowDistances();
            foreach (CameraSnapshot snapshot in OriginalCameras.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                snapshot.CameraData.antialiasing = snapshot.Antialiasing;
                snapshot.CameraData.allowDynamicResolution = snapshot.AllowDynamicResolution;
            }
        }

        private static void ApplyRealtimeShadowPermission(bool allow, bool restoreAllowed)
        {
            foreach (LightSnapshot snapshot in OriginalLights.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                if (!allow)
                    snapshot.Component.shadows = LightShadows.None;
                else if (restoreAllowed)
                    snapshot.Component.shadows = snapshot.Shadows;
            }
        }

        private static void RestoreRealtimeLights()
        {
            foreach (LightSnapshot snapshot in OriginalLights.Values)
                if (IsAlive(snapshot))
                    snapshot.Component.shadows = snapshot.Shadows;
        }

        private static void ApplyHDRPShadowDistance(float distance)
        {
            foreach (ShadowDistanceSnapshot snapshot in OriginalShadowDistances.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                snapshot.Component.active = true;
                snapshot.Component.maxShadowDistance.overrideState = true;
                snapshot.Component.maxShadowDistance.value = Mathf.Max(0f, distance);
            }
        }

        private static void RestoreHDRPShadowDistances()
        {
            foreach (ShadowDistanceSnapshot snapshot in OriginalShadowDistances.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                snapshot.Component.active = snapshot.ComponentActive;
                snapshot.Component.maxShadowDistance.value = snapshot.Distance;
                snapshot.Component.maxShadowDistance.overrideState = snapshot.DistanceOverride;
            }
        }

        private static void ApplyEffectPermission(EffectKind kind, bool allow, bool restoreAllowed)
        {
            foreach (EffectSnapshot snapshot in OriginalEffects.Values)
            {
                if (snapshot.Kind != kind || !IsAlive(snapshot))
                    continue;
                if (allow)
                {
                    if (restoreAllowed)
                        RestoreEffect(snapshot);
                }
                else
                {
                    ForceEffectOff(snapshot);
                }
            }
        }

        private static void ForceEffectOff(EffectSnapshot snapshot)
        {
            snapshot.Component.active = true;
            switch (snapshot.Kind)
            {
                case EffectKind.ScreenSpaceReflections:
                    SetBool(TryCast<ScreenSpaceReflection>(snapshot.Component)?.enabled, false);
                    break;
                case EffectKind.AmbientOcclusion:
                    SetFloat(TryCast<ScreenSpaceAmbientOcclusion>(snapshot.Component)?.intensity, 0f);
                    break;
                case EffectKind.GlobalIllumination:
                    SetBool(TryCast<GlobalIllumination>(snapshot.Component)?.enable, false);
                    break;
                case EffectKind.VolumetricClouds:
                    SetBool(TryCast<VolumetricClouds>(snapshot.Component)?.enable, false);
                    break;
                case EffectKind.VolumetricFog:
                    SetBool(TryCast<Fog>(snapshot.Component)?.enableVolumetricFog, false);
                    break;
                case EffectKind.ContactShadows:
                    SetBool(TryCast<ContactShadows>(snapshot.Component)?.enable, false);
                    break;
                case EffectKind.MotionBlur:
                    SetFloat(TryCast<MotionBlur>(snapshot.Component)?.intensity, 0f);
                    break;
                case EffectKind.DepthOfField:
                    snapshot.Component.active = false;
                    break;
                case EffectKind.Bloom:
                    SetFloat(TryCast<Bloom>(snapshot.Component)?.intensity, 0f);
                    break;
            }
        }

        private static void RestoreEffect(EffectSnapshot snapshot)
        {
            snapshot.Component.active = snapshot.ComponentActive;
            switch (snapshot.Kind)
            {
                case EffectKind.ScreenSpaceReflections:
                    RestoreBool(TryCast<ScreenSpaceReflection>(snapshot.Component)?.enabled, snapshot);
                    break;
                case EffectKind.AmbientOcclusion:
                    RestoreFloat(TryCast<ScreenSpaceAmbientOcclusion>(snapshot.Component)?.intensity, snapshot);
                    break;
                case EffectKind.GlobalIllumination:
                    RestoreBool(TryCast<GlobalIllumination>(snapshot.Component)?.enable, snapshot);
                    break;
                case EffectKind.VolumetricClouds:
                    RestoreBool(TryCast<VolumetricClouds>(snapshot.Component)?.enable, snapshot);
                    break;
                case EffectKind.VolumetricFog:
                    RestoreBool(TryCast<Fog>(snapshot.Component)?.enableVolumetricFog, snapshot);
                    break;
                case EffectKind.ContactShadows:
                    RestoreBool(TryCast<ContactShadows>(snapshot.Component)?.enable, snapshot);
                    break;
                case EffectKind.MotionBlur:
                    RestoreFloat(TryCast<MotionBlur>(snapshot.Component)?.intensity, snapshot);
                    break;
                case EffectKind.Bloom:
                    RestoreFloat(TryCast<Bloom>(snapshot.Component)?.intensity, snapshot);
                    break;
            }
        }

        private static void RestoreAllEffects()
        {
            foreach (EffectSnapshot snapshot in OriginalEffects.Values)
                if (IsAlive(snapshot))
                    RestoreEffect(snapshot);
        }

        private static void ApplyRenderScale(float scale)
        {
            if (OriginalQuality.DynamicResolutionCaptured && OriginalQuality.PipelineAsset != null)
            {
                try
                {
                    HDRenderPipelineAsset asset = OriginalQuality.PipelineAsset;
                    RenderPipelineSettings pipelineSettings = asset.currentPlatformRenderPipelineSettings;
                    GlobalDynamicResolutionSettings dynamicSettings = pipelineSettings.dynamicResolutionSettings;
                    float percentage = Mathf.Clamp(scale * 100f, 50f, 100f);
                    dynamicSettings.enabled = true;
                    dynamicSettings.forceResolution = true;
                    dynamicSettings.forcedPercentage = percentage;
                    dynamicSettings.minPercentage = 50f;
                    dynamicSettings.maxPercentage = 100f;
                    dynamicSettings.dynResType = DynamicResolutionType.Software;
                    dynamicSettings.upsampleFilter = percentage <= 65f
                        ? DynamicResUpscaleFilter.Bilinear
                        : DynamicResUpscaleFilter.CatmullRom;
                    pipelineSettings.dynamicResolutionSettings = dynamicSettings;
                    asset.currentPlatformRenderPipelineSettings = pipelineSettings;
                    return;
                }
                catch (Exception ex)
                {
                    if (!_renderScaleFallbackWarningLogged)
                    {
                        _renderScaleFallbackWarningLogged = true;
                        Log.Warning(
                            "[Graphics] HDRP render-scale override fell back to ScalableBufferManager: " +
                            ex.Message);
                    }
                }
            }

            ScalableBufferManager.ResizeBuffers(scale, scale);
        }

        private static void RestoreRenderScale()
        {
            if (OriginalQuality.DynamicResolutionCaptured && OriginalQuality.PipelineAsset != null)
            {
                try
                {
                    HDRenderPipelineAsset asset = OriginalQuality.PipelineAsset;
                    RenderPipelineSettings pipelineSettings = asset.currentPlatformRenderPipelineSettings;
                    pipelineSettings.dynamicResolutionSettings = OriginalQuality.DynamicResolution;
                    asset.currentPlatformRenderPipelineSettings = pipelineSettings;
                }
                catch (Exception ex)
                {
                    if (!_renderScaleRestoreWarningLogged)
                    {
                        _renderScaleRestoreWarningLogged = true;
                        Log.Warning(
                            "[Graphics] Could not restore HDRP dynamic-resolution settings: " +
                            ex.Message);
                    }
                }
            }
            ScalableBufferManager.ResizeBuffers(OriginalQuality.RenderScaleX, OriginalQuality.RenderScaleY);
        }

        private static void ApplyEnvironment(bool restoreUncontrolledValues)
        {
            GraphicsSettings settings = Config.graphicsSettings;
            if (settings == null)
                return;

            Cubemap selectedSky = null;
            bool mapSky = IsMapSky(settings.skySelection);
            SkyAssetDefinition selectedDefinition = FindSkyDefinition(settings.skySelection);
            if (selectedDefinition != null && EnsureSkyAsset(selectedDefinition))
                selectedSky = selectedDefinition.Cubemap;

            if (selectedSky != null)
            {
                foreach (VisualEnvironmentSnapshot snapshot in OriginalVisualEnvironments.Values)
                {
                    if (!IsAlive(snapshot))
                        continue;
                    snapshot.Component.active = true;
                    snapshot.Component.skyType.overrideState = true;
                    snapshot.Component.skyType.value = (int)SkyType.HDRI;
                    snapshot.Component.skyAmbientMode.overrideState = true;
                    snapshot.Component.skyAmbientMode.value = SkyAmbientMode.Dynamic;
                }
            }
            else if (mapSky && restoreUncontrolledValues)
            {
                RestoreVisualEnvironments();
            }

            foreach (SkySnapshot snapshot in OriginalSkies.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                HDRISky sky = snapshot.Component;
                if (mapSky)
                {
                    sky.hdriSky.value = snapshot.Cubemap;
                    sky.hdriSky.overrideState = snapshot.CubemapOverride;
                }
                else if (selectedSky != null)
                {
                    sky.active = true;
                    sky.hdriSky.overrideState = true;
                    sky.hdriSky.value = selectedSky;
                }

                if (settings.skyTuningOverride)
                {
                    sky.active = true;
                    sky.rotation.overrideState = true;
                    sky.rotation.value = settings.skyRotation;
                    sky.exposure.overrideState = true;
                    sky.exposure.value = settings.skyExposure;
                }
                else if (restoreUncontrolledValues)
                {
                    sky.rotation.value = snapshot.Rotation;
                    sky.rotation.overrideState = snapshot.RotationOverride;
                    sky.exposure.value = snapshot.Exposure;
                    sky.exposure.overrideState = snapshot.ExposureOverride;
                    if (mapSky)
                        sky.active = snapshot.ComponentActive;
                }
            }

            foreach (ExposureSnapshot snapshot in OriginalExposures.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                Exposure exposure = snapshot.Component;
                SceneExposureSettings sceneSettings =
                    FindSceneExposureSettings(settings, snapshot.SceneKey);
                if (sceneSettings?.exposureOverride == true)
                {
                    exposure.active = true;
                    exposure.compensation.overrideState = true;
                    exposure.compensation.value = sceneSettings.exposureCompensation;
                    if (sceneSettings.forceFixedExposure)
                    {
                        exposure.mode.overrideState = true;
                        exposure.mode.value = ExposureMode.Fixed;
                        exposure.fixedExposure.overrideState = true;
                        exposure.fixedExposure.value = sceneSettings.fixedExposure;
                    }
                    else if (restoreUncontrolledValues)
                    {
                        exposure.mode.value = snapshot.Mode;
                        exposure.mode.overrideState = snapshot.ModeOverride;
                        exposure.fixedExposure.value = snapshot.FixedExposure;
                        exposure.fixedExposure.overrideState = snapshot.FixedExposureOverride;
                    }
                }
                else if (restoreUncontrolledValues)
                {
                    RestoreExposure(snapshot);
                }
            }
            ReleaseUnusedSkyAssets(selectedDefinition);
        }

        private static void RestoreEnvironment()
        {
            RestoreVisualEnvironments();
            foreach (SkySnapshot snapshot in OriginalSkies.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                HDRISky sky = snapshot.Component;
                sky.active = snapshot.ComponentActive;
                sky.hdriSky.value = snapshot.Cubemap;
                sky.hdriSky.overrideState = snapshot.CubemapOverride;
                sky.rotation.value = snapshot.Rotation;
                sky.rotation.overrideState = snapshot.RotationOverride;
                sky.exposure.value = snapshot.Exposure;
                sky.exposure.overrideState = snapshot.ExposureOverride;
            }
            foreach (ExposureSnapshot snapshot in OriginalExposures.Values)
                if (IsAlive(snapshot))
                    RestoreExposure(snapshot);
        }

        private static void RestoreVisualEnvironments()
        {
            foreach (VisualEnvironmentSnapshot snapshot in OriginalVisualEnvironments.Values)
            {
                if (!IsAlive(snapshot))
                    continue;
                snapshot.Component.active = snapshot.ComponentActive;
                snapshot.Component.skyType.value = snapshot.SkyType;
                snapshot.Component.skyType.overrideState = snapshot.SkyTypeOverride;
                snapshot.Component.skyAmbientMode.value = snapshot.AmbientMode;
                snapshot.Component.skyAmbientMode.overrideState = snapshot.AmbientModeOverride;
            }
        }

        private static void RestoreExposure(ExposureSnapshot snapshot)
        {
            Exposure exposure = snapshot.Component;
            exposure.active = snapshot.ComponentActive;
            exposure.mode.value = snapshot.Mode;
            exposure.mode.overrideState = snapshot.ModeOverride;
            exposure.fixedExposure.value = snapshot.FixedExposure;
            exposure.fixedExposure.overrideState = snapshot.FixedExposureOverride;
            exposure.compensation.value = snapshot.Compensation;
            exposure.compensation.overrideState = snapshot.CompensationOverride;
        }

        private static void RestoreSceneObjects()
        {
            RestoreQuality();
            RestoreAllEffects();
            RestoreEnvironment();
        }

        private static void SyncPerformanceSettingsFromGame(GraphicsSettings settings)
        {
            CaptureOriginalQuality();
            settings.renderScale = OriginalQuality.RenderScaleX;
            settings.realtimeShadows = OriginalQuality.Shadows != ShadowQuality.Disable;
            settings.shadowDistance = OriginalQuality.ShadowDistance;
            ShadowDistanceSnapshot firstShadowDistance = OriginalShadowDistances.Values.FirstOrDefault(IsAlive);
            if (firstShadowDistance != null)
                settings.shadowDistance = firstShadowDistance.Distance;
            settings.lodBias = OriginalQuality.LodBias;
            settings.textureMipmapLimit = OriginalQuality.TextureMipmapLimit;
            settings.terrainDetailDensity = OriginalQuality.TerrainDetailDensity;
            settings.terrainDetailDistance = OriginalQuality.TerrainDetailDistance;
            CameraSnapshot firstCamera = OriginalCameras.Values.FirstOrDefault(IsAlive);
            if (firstCamera != null)
                settings.antiAliasingMode = (int)firstCamera.Antialiasing;
            settings.allowScreenSpaceReflections = true;
            settings.allowAmbientOcclusion = true;
            settings.allowGlobalIllumination = true;
            settings.allowVolumetricClouds = true;
            settings.allowVolumetricFog = true;
            settings.allowContactShadows = true;
            settings.allowMotionBlur = true;
            settings.allowDepthOfField = true;
            settings.allowBloom = true;
            settings.allowRealtimeReflectionProbes = true;
            Config.NormalizeGraphicsSettings(settings);
        }

        private static void ReportRuntimeFailure(string operation, Exception ex)
        {
            _status = $"Graphics operation failed while {operation}; see Latest.log.";
            float now = Time.realtimeSinceStartup;
            if (now < _nextErrorLogTime)
                return;
            _nextErrorLogTime = now + ErrorLogCooldownSeconds;
            Log.Error($"[Graphics] Failed while {operation}: {ex}");
        }

        private static void SeedSkyTuningFromScene(GraphicsSettings settings)
        {
            SkySnapshot first = OriginalSkies.Values.FirstOrDefault(IsAlive);
            if (first == null)
                return;
            settings.skyRotation = first.Component.rotation.value;
            settings.skyExposure = first.Component.exposure.value;
        }

        private static List<ExposureSceneOption> GetExposureSceneOptions()
        {
            Dictionary<string, ExposureSceneOption> options =
                new Dictionary<string, ExposureSceneOption>(StringComparer.OrdinalIgnoreCase);
            foreach (ExposureSnapshot snapshot in OriginalExposures.Values)
            {
                if (!IsAlive(snapshot) || string.IsNullOrWhiteSpace(snapshot.SceneKey))
                    continue;

                if (!options.TryGetValue(snapshot.SceneKey, out ExposureSceneOption option))
                {
                    option = new ExposureSceneOption
                    {
                        Key = snapshot.SceneKey,
                        Name = string.IsNullOrWhiteSpace(snapshot.SceneName)
                            ? snapshot.SceneKey
                            : snapshot.SceneName,
                        ComponentCount = 0
                    };
                    options[snapshot.SceneKey] = option;
                }
                option.ComponentCount++;
            }

            return options.Values
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(option => option.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ExposureSceneOption ResolveSelectedExposureScene(
            List<ExposureSceneOption> scenes)
        {
            if (scenes == null || scenes.Count == 0)
            {
                _selectedExposureSceneKey = string.Empty;
                return null;
            }

            ExposureSceneOption selected = scenes.FirstOrDefault(
                option => string.Equals(
                    option.Key,
                    _selectedExposureSceneKey,
                    StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                string activeSceneKey = BuildSceneKey(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                selected = scenes.FirstOrDefault(
                    option => string.Equals(
                        option.Key,
                        activeSceneKey,
                        StringComparison.OrdinalIgnoreCase));
            }

            selected ??= scenes[0];
            _selectedExposureSceneKey = selected.Key;
            return selected;
        }

        private static SceneExposureSettings GetOrCreateSceneExposureSettings(
            GraphicsSettings settings,
            ExposureSceneOption scene)
        {
            settings.sceneExposureSettings ??= new Dictionary<string, SceneExposureSettings>();
            SceneExposureSettings sceneSettings = FindSceneExposureSettings(settings, scene.Key);
            if (sceneSettings == null)
            {
                ExposureSnapshot first = OriginalExposures.Values.FirstOrDefault(
                    snapshot => IsAlive(snapshot) &&
                                string.Equals(
                                    snapshot.SceneKey,
                                    scene.Key,
                                    StringComparison.OrdinalIgnoreCase));
                sceneSettings = new SceneExposureSettings
                {
                    sceneName = scene.Name,
                    exposureOverride = false,
                    forceFixedExposure = first?.Mode == ExposureMode.Fixed,
                    exposureCompensation = first?.Compensation ?? 0f,
                    fixedExposure = first?.FixedExposure ?? 0f
                };
                settings.sceneExposureSettings[scene.Key] = sceneSettings;
            }
            else
            {
                sceneSettings.sceneName = scene.Name;
            }
            return sceneSettings;
        }

        private static SceneExposureSettings FindSceneExposureSettings(
            GraphicsSettings settings,
            string sceneKey)
        {
            if (settings?.sceneExposureSettings == null || string.IsNullOrWhiteSpace(sceneKey))
                return null;
            if (settings.sceneExposureSettings.TryGetValue(sceneKey, out SceneExposureSettings exact))
                return exact;

            foreach (KeyValuePair<string, SceneExposureSettings> pair in settings.sceneExposureSettings)
            {
                if (string.Equals(pair.Key, sceneKey, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }

        private static void MigrateLegacyExposureSettings(GraphicsSettings settings)
        {
            if (settings == null || !settings.exposureOverride)
                return;

            ExposureSceneOption scene = ResolveSelectedExposureScene(GetExposureSceneOptions());
            if (scene == null)
                return;

            SceneExposureSettings sceneSettings = GetOrCreateSceneExposureSettings(settings, scene);
            sceneSettings.exposureOverride = true;
            sceneSettings.forceFixedExposure = settings.forceFixedExposure;
            sceneSettings.exposureCompensation = settings.exposureCompensation;
            sceneSettings.fixedExposure = settings.fixedExposure;
            settings.exposureOverride = false;
            Config.RequestSave();
            Log.Msg($"[Graphics] Migrated the legacy camera exposure override to scene '{scene.Name}'.");
        }

        private static void SeedExposureFromScene(
            SceneExposureSettings settings,
            string sceneKey)
        {
            ExposureSnapshot first = OriginalExposures.Values.FirstOrDefault(
                snapshot => IsAlive(snapshot) &&
                            string.Equals(
                                snapshot.SceneKey,
                                sceneKey,
                                StringComparison.OrdinalIgnoreCase));
            if (first == null)
                return;
            settings.exposureCompensation = first.Compensation;
            settings.fixedExposure = first.FixedExposure;
            settings.forceFixedExposure = first.Mode == ExposureMode.Fixed;
        }

        private static bool EnsureSkyAsset(SkyAssetDefinition definition)
        {
            if (definition == null)
                return false;
            if (definition.Cubemap != null)
                return true;

            if (definition.Bundle != null)
            {
                if (LoadSkyCubemapFromBundle(definition))
                    return true;
                definition.Bundle.Unload(false);
                definition.Bundle = null;
            }

            string path = Path.Combine(Memory.bundlesFolderPath, definition.BundleFileName);
            if (File.Exists(path))
            {
                string actualHash = ComputeFileSha256(path);
                if (string.Equals(actualHash, definition.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    definition.Bundle = AssetBundle.LoadFromFile(path);
                    if (definition.Bundle != null && LoadSkyCubemapFromBundle(definition))
                    {
                        definition.NextDownloadAttemptTime = 0f;
                        _status = $"Verified 2048 BC6H {definition.DisplayName} HDRI loaded.";
                        Log.Msg(
                            $"[Graphics] {definition.DisplayName} HDRI bundle loaded and verified " +
                            $"(bundle={definition.BundleFileName}, sha256={actualHash}).");
                        return true;
                    }
                }
                else
                {
                    Log.Warning(
                        $"[Graphics] {definition.DisplayName} HDRI bundle hash mismatch. " +
                        $"expected={definition.Sha256}, actual={actualHash}");
                }
            }

            BeginSkyBundleDownload(definition, path);
            return false;
        }

        private static bool LoadSkyCubemapFromBundle(SkyAssetDefinition definition)
        {
            if (definition?.Bundle == null)
                return false;
            foreach (string assetName in definition.Bundle.GetAllAssetNames())
            {
                if (!assetName.EndsWith(definition.AssetFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                definition.Cubemap = definition.Bundle.LoadAsset<Cubemap>(assetName);
                break;
            }
            return definition.Cubemap != null;
        }

        private static void ReleaseUnusedSkyAssets(SkyAssetDefinition retainedDefinition)
        {
            foreach (SkyAssetDefinition definition in CustomSkyAssets)
            {
                if (ReferenceEquals(definition, retainedDefinition) || definition.Bundle == null)
                    continue;
                definition.Bundle.Unload(true);
                definition.Bundle = null;
                definition.Cubemap = null;
            }
        }

        private static void BeginSkyBundleDownload(SkyAssetDefinition definition, string destinationPath)
        {
            if (definition == null || definition.DownloadInProgress ||
                Time.realtimeSinceStartup < definition.NextDownloadAttemptTime)
                return;
            if (CustomSkyAssets.Any(item => item.DownloadInProgress))
            {
                _status = "Finishing another HDRI download first.";
                return;
            }

            definition.DownloadInProgress = true;
            _status = $"Downloading the verified {definition.DisplayName} HDRI bundle...";
            MelonCoroutines.Start(DownloadSkyBundle(definition, destinationPath));
        }

        private static IEnumerator DownloadSkyBundle(SkyAssetDefinition definition, string destinationPath)
        {
            string url = SkyBundleBaseUrl + definition.BundleFileName;
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = BundleDownloadTimeoutSeconds;
            yield return request.SendWebRequest();
            bool installed = false;
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _status = $"{definition.DisplayName} HDRI download failed; the current map sky remains active.";
                    Log.Warning($"[Graphics] {definition.DisplayName} HDRI download failed: {request.error}");
                    yield break;
                }

                byte[] data = request.downloadHandler.data;
                if (data == null || data.Length == 0 || data.Length > MaximumSkyBundleBytes)
                {
                    _status = $"{definition.DisplayName} HDRI download was empty or exceeded the safe size limit.";
                    yield break;
                }

                string actualHash;
                using (SHA256 sha256 = SHA256.Create())
                    actualHash = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", string.Empty);
                if (!string.Equals(actualHash, definition.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _status = $"{definition.DisplayName} HDRI download failed verification; the map sky remains active.";
                    Log.Warning(
                        $"[Graphics] Downloaded {definition.DisplayName} HDRI hash mismatch. " +
                        $"expected={definition.Sha256}, actual={actualHash}");
                    yield break;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                string stagingPath = destinationPath + ".download";
                File.WriteAllBytes(stagingPath, data);
                File.Copy(stagingPath, destinationPath, true);
                File.Delete(stagingPath);
                installed = true;
            }
            catch (Exception ex)
            {
                _status = $"{definition.DisplayName} HDRI bundle could not be installed.";
                Log.Warning($"[Graphics] {definition.DisplayName} HDRI install failed: {ex.Message}");
            }
            finally
            {
                definition.DownloadInProgress = false;
                if (!installed)
                    definition.NextDownloadAttemptTime = Time.realtimeSinceStartup + 30f;
                request.Dispose();
            }

            if (!installed)
                yield break;
            definition.Bundle = AssetBundle.LoadFromFile(destinationPath);
            if (definition.Bundle != null && LoadSkyCubemapFromBundle(definition))
            {
                definition.NextDownloadAttemptTime = 0f;
                _status = $"Verified 2048 BC6H {definition.DisplayName} HDRI installed and loaded.";
                ApplyEnvironment(false);
                Log.Msg(
                    $"[Graphics] {definition.DisplayName} HDRI bundle downloaded, verified, and loaded " +
                    $"(bundle={definition.BundleFileName}).");
            }
        }

        private static string ComputeFileSha256(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                using SHA256 sha256 = SHA256.Create();
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                Log.Warning("[Graphics] Could not hash an HDRI bundle: " + ex.Message);
                return string.Empty;
            }
        }

        private static bool HasSceneExposureOverride(GraphicsSettings settings)
        {
            return settings?.sceneExposureSettings?.Values.Any(
                scene => scene?.exposureOverride == true) ?? false;
        }

        private static bool IsMapSky(string selection)
        {
            return string.IsNullOrWhiteSpace(selection) ||
                   string.Equals(selection, "Map", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSkySelection(string selection, string expected)
        {
            return string.Equals(selection, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static SkyAssetDefinition FindSkyDefinition(string selection)
        {
            return CustomSkyAssets.FirstOrDefault(
                item => IsSkySelection(selection, item.Selection));
        }

        private static bool IsRoweModVolume(Volume volume, VolumeProfile profile)
        {
            string name = (volume.name ?? string.Empty) + " " + (profile.name ?? string.Empty);
            return name.IndexOf("RoweMod", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLoadedSceneObject(Component component)
        {
            try
            {
                return component != null && component.gameObject != null &&
                       component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildSceneKey(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid())
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(scene.path))
                return scene.path.Trim();
            if (!string.IsNullOrWhiteSpace(scene.name))
                return scene.name.Trim();
            return "Scene " + scene.buildIndex;
        }

        private static string BuildSceneName(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.IsValid() && !string.IsNullOrWhiteSpace(scene.name))
                return scene.name.Trim();
            return "Unnamed Scene";
        }

        private static string BuildVolumeLabel(Volume volume, VolumeProfile profile)
        {
            string volumeName = string.IsNullOrWhiteSpace(volume.name) ? "Volume" : volume.name;
            string profileName = string.IsNullOrWhiteSpace(profile.name) ? "Profile" : profile.name;
            return volumeName + " / " + profileName;
        }

        private static string BuildEffectKey(int componentId, EffectKind kind)
        {
            return componentId + ":" + (int)kind;
        }

        private static T TryCast<T>(VolumeComponent component) where T : VolumeComponent
        {
            if (component == null)
                return null;
            try
            {
                return component.TryCast<T>();
            }
            catch
            {
                return null;
            }
        }

        private static void SetBool(BoolParameter parameter, bool value)
        {
            if (parameter == null)
                return;
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void SetFloat(FloatParameter parameter, float value)
        {
            if (parameter == null)
                return;
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void RestoreBool(BoolParameter parameter, EffectSnapshot snapshot)
        {
            if (parameter == null)
                return;
            parameter.value = snapshot.BoolValue;
            parameter.overrideState = snapshot.ParameterOverride;
        }

        private static void RestoreFloat(FloatParameter parameter, EffectSnapshot snapshot)
        {
            if (parameter == null)
                return;
            parameter.value = snapshot.FloatValue;
            parameter.overrideState = snapshot.ParameterOverride;
        }

        private static void RemoveMissing<T>(Dictionary<int, T> dictionary, HashSet<int> liveIds)
        {
            foreach (int id in dictionary.Keys.Where(id => !liveIds.Contains(id)).ToArray())
                dictionary.Remove(id);
        }

        private static void RemoveMissing<T>(Dictionary<string, T> dictionary, HashSet<string> liveKeys)
        {
            foreach (string key in dictionary.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
                dictionary.Remove(key);
        }

        private static bool IsAlive(CameraSnapshot snapshot)
        {
            return snapshot?.CameraData != null;
        }

        private static bool IsAlive(LightSnapshot snapshot)
        {
            return snapshot?.Component != null;
        }

        private static bool IsAlive(ShadowDistanceSnapshot snapshot)
        {
            return snapshot?.Component != null;
        }

        private static bool IsAlive(SkySnapshot snapshot)
        {
            return snapshot?.Component != null;
        }

        private static bool IsAlive(ExposureSnapshot snapshot)
        {
            return snapshot?.Component != null;
        }

        private static bool IsAlive(VisualEnvironmentSnapshot snapshot)
        {
            return snapshot?.Component != null;
        }

        private static bool IsAlive(EffectSnapshot snapshot)
        {
            return snapshot?.Component != null;
        }
    }
}
