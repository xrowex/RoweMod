using System;
using System.Collections;
using HarmonyLib;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Animator_Motion_Systems;
using Il2CppMashBox.Character;
using Il2CppMashBox.Core.Runtime.InverseKinematics;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Mirrors only the rider/bike presentation and bike-facing helpers.
    /// It intentionally never changes CharacterManager stance or the trick
    /// animation system, so native trick and grind interpretation remains intact.
    /// </summary>
    public static class BikeOnlyStance
    {
        private const float ResolveIntervalSeconds = 1f;
        private const float IntegrityCheckIntervalSeconds = 1f;

        private static GameObject _root;
        private static int _rootId;
        private static int _vehicleId;
        private static float _nextResolveTime;
        private static float _nextIntegrityCheckTime;

        private static CharacterManager _characterManager;
        private static CharacterSportsStance _sportsStance;
        private static TuckUpTest _tuckUp;
        private static MGSteerableWheel _steerableWheel;
        private static BMXMotionComponent _bmxMotion;
        private static FullBodyMotionComponent _fullBodyMotion;
        private static UnityIKLimb[] _ikLimbs = Array.Empty<UnityIKLimb>();

        private static bool _applied;
        private static bool _appliedGoofy;
        private static bool _dirty = true;
        private static bool _isApplying;
        private static int _monitorGeneration;
        private static string _status = "Waiting for the local rider.";

        public static string Status => _status;

        public static bool? NativeGoofy
        {
            get
            {
                try
                {
                    return _characterManager != null
                        ? _characterManager.IsGoofyStance
                        : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool CanSwitchFromInput()
        {
            EnsureReferences();
            return Config.bikeOnlyStanceSettings?.enabled == true &&
                   CanApply();
        }

        public static void Update()
        {
            EnsureReferences();

            BikeOnlyStanceSettings settings = Config.bikeOnlyStanceSettings;
            if (settings == null || !settings.enabled)
            {
                RestoreNativeStance();
                _status = _root == null
                    ? "Waiting for the local rider."
                    : "Disabled; the game controls the complete stance.";
                return;
            }

            if (!CanApply())
            {
                RestoreNativeStance();
                return;
            }

            if (!_dirty &&
                _applied &&
                _appliedGoofy == settings.goofy &&
                Time.unscaledTime < _nextIntegrityCheckTime)
            {
                return;
            }

            _nextIntegrityCheckTime =
                Time.unscaledTime + IntegrityCheckIntervalSeconds;

            if (!_dirty && _applied && _appliedGoofy == settings.goofy &&
                PrimaryComponentsMatch(settings.goofy))
            {
                return;
            }

            ApplyBikeMirror(settings.goofy);
        }

        public static void NotifySettingsChanged()
        {
            _dirty = true;
            Update();
            RestartIntegrityMonitor();
        }

        public static void OnLocalPlayerSpawned()
        {
            EnsureReferences(true);
            _dirty = true;
            Update();
            RestartIntegrityMonitor();
        }

        public static void NotifyNativeStanceUpdated(CharacterSportsStance instance)
        {
            if (instance == null || _sportsStance == null)
                return;

            try
            {
                if (instance.GetInstanceID() == _sportsStance.GetInstanceID())
                {
                    _dirty = true;
                    if (!_isApplying)
                        Update();
                }
            }
            catch
            {
                _dirty = true;
            }
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            _monitorGeneration++;
            RestoreNativeStance();
            ClearReferences();
            _status = gameplayScene
                ? "Waiting for the local rider."
                : "Bike-only stance is available in gameplay.";
        }

        public static void Cleanup()
        {
            _monitorGeneration++;
            RestoreNativeStance();
            ClearReferences();
        }

        private static void RestartIntegrityMonitor()
        {
            _monitorGeneration++;
            BikeOnlyStanceSettings settings = Config.bikeOnlyStanceSettings;
            if (settings?.enabled != true)
                return;

            int generation = _monitorGeneration;
            MelonCoroutines.Start(IntegrityMonitor(generation));
        }

        private static IEnumerator IntegrityMonitor(int generation)
        {
            while (generation == _monitorGeneration &&
                   Config.bikeOnlyStanceSettings?.enabled == true)
            {
                yield return new WaitForSecondsRealtime(IntegrityCheckIntervalSeconds);

                if (generation != _monitorGeneration ||
                    Config.bikeOnlyStanceSettings?.enabled != true)
                {
                    yield break;
                }

                Update();
            }
        }

        private static void EnsureReferences(bool force = false)
        {
            GameObject root = Memory.rMbCharacter;
            int rootId = root != null ? root.GetInstanceID() : 0;
            int vehicleId = Memory.vehicleController != null
                ? Memory.vehicleController.GetInstanceID()
                : 0;
            bool changed = rootId != _rootId || vehicleId != _vehicleId;
            bool missing = _characterManager == null || _sportsStance == null;

            if (!force && !changed && !missing)
                return;
            if (!force && !changed && Time.unscaledTime < _nextResolveTime)
                return;

            _nextResolveTime = Time.unscaledTime + ResolveIntervalSeconds;
            if (changed)
            {
                RestoreNativeStance();
                ClearReferences();
            }

            if (root == null)
                return;

            _root = root;
            _rootId = rootId;
            _vehicleId = vehicleId;
            _characterManager = root.GetComponentInChildren<CharacterManager>(true);
            _sportsStance =
                root.GetComponentInChildren<CharacterSportsStance>(true);

            if (_sportsStance != null)
            {
                _tuckUp = _sportsStance._tuckUpTest;
                _steerableWheel = _sportsStance._mgSteerableWheel;
                _bmxMotion = _sportsStance._bmxMotionComponent;
                _fullBodyMotion = _sportsStance._fullBodyMotionComponent;
            }

            _tuckUp ??= root.GetComponentInChildren<TuckUpTest>(true);
            _steerableWheel ??=
                root.GetComponentInChildren<MGSteerableWheel>(true);
            _bmxMotion ??=
                root.GetComponentInChildren<BMXMotionComponent>(true);
            _fullBodyMotion ??=
                root.GetComponentInChildren<FullBodyMotionComponent>(true);
            _ikLimbs = root.GetComponentsInChildren<UnityIKLimb>(true) ??
                       Array.Empty<UnityIKLimb>();

            _dirty = true;
            _status = BuildResolvedStatus();
        }

        private static bool CanApply()
        {
            if (!Main.playableSceneLoaded || _root == null ||
                _characterManager == null)
            {
                _status = "Waiting for the local rider.";
                return false;
            }

            try
            {
                if (!_characterManager.IsLocalPlayer)
                {
                    _status = "The resolved rider is not local; no changes applied.";
                    return false;
                }

                if (!_characterManager.IsAlive)
                {
                    _status = "Waiting for the local rider to respawn.";
                    return false;
                }

                if (!_characterManager.IsDrivingVehicle)
                {
                    _status = "Mount a bike to apply bike-only stance.";
                    return false;
                }
            }
            catch
            {
                _status = "Local rider state is temporarily unavailable.";
                return false;
            }

            if (_bmxMotion == null && _fullBodyMotion == null &&
                _tuckUp == null && _steerableWheel == null &&
                _ikLimbs.Length == 0)
            {
                _status = "No bike stance components were found.";
                return false;
            }

            return true;
        }

        private static bool PrimaryComponentsMatch(bool goofy)
        {
            try
            {
                if (_bmxMotion != null && _bmxMotion.Mirrored != goofy)
                    return false;
                if (_fullBodyMotion != null &&
                    _fullBodyMotion.Mirrored != goofy)
                {
                    return false;
                }
                if (_tuckUp != null && _tuckUp._isMirrored != goofy)
                    return false;
                if (_steerableWheel != null &&
                    _steerableWheel._isMirrored != goofy)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static void ApplyBikeMirror(bool goofy)
        {
            if (_isApplying)
                return;

            int appliedCount = 0;
            _isApplying = true;
            try
            {
                if (_tuckUp != null)
                {
                    _tuckUp.SetMirrored(goofy);
                    appliedCount++;
                }

                if (_steerableWheel != null)
                {
                    _steerableWheel.Mirror(goofy);
                    appliedCount++;
                }

                if (_bmxMotion != null)
                {
                    _bmxMotion.SetMirrored(goofy);
                    appliedCount++;
                }

                if (_fullBodyMotion != null)
                {
                    _fullBodyMotion.SetMirrored(goofy);
                    appliedCount++;
                }

                for (int i = 0; i < _ikLimbs.Length; i++)
                {
                    UnityIKLimb limb = _ikLimbs[i];
                    if (limb == null)
                        continue;

                    limb.SetMirrored(goofy);
                    appliedCount++;
                }

                _applied = appliedCount > 0;
                if (_applied)
                    Main.NotifyRuntimeContributionApplied();
                _appliedGoofy = goofy;
                _dirty = false;
                bool? nativeGoofy = NativeGoofy;
                string native = nativeGoofy.HasValue
                    ? (nativeGoofy.Value ? "Goofy" : "Regular")
                    : "Unknown";
                _status =
                    $"Bike: {(goofy ? "Goofy" : "Regular")} | " +
                    $"Trick/grind binds: native {native} | " +
                    $"{appliedCount} components";
                Log.Msg($"[BikeOnlyStance] {_status}");
            }
            catch (Exception ex)
            {
                _dirty = true;
                _status = $"Bike stance apply failed: {ex.Message}";
                Log.Error($"[BikeOnlyStance] Apply failed: {ex}");
            }
            finally
            {
                _isApplying = false;
            }
        }

        private static void RestoreNativeStance()
        {
            if (!_applied)
                return;

            bool nativeGoofy;
            try
            {
                nativeGoofy =
                    _characterManager != null &&
                    _characterManager.IsGoofyStance;
            }
            catch
            {
                _applied = false;
                _dirty = true;
                return;
            }

            ApplyBikeMirror(nativeGoofy);
            _applied = false;
            _dirty = true;
        }

        private static string BuildResolvedStatus()
        {
            int componentCount = 0;
            if (_tuckUp != null) componentCount++;
            if (_steerableWheel != null) componentCount++;
            if (_bmxMotion != null) componentCount++;
            if (_fullBodyMotion != null) componentCount++;
            componentCount += _ikLimbs.Length;

            return _sportsStance == null
                ? $"Partial bike stance support: {componentCount} components."
                : $"Bike stance ready: {componentCount} components.";
        }

        private static void ClearReferences()
        {
            _root = null;
            _rootId = 0;
            _vehicleId = 0;
            _characterManager = null;
            _sportsStance = null;
            _tuckUp = null;
            _steerableWheel = null;
            _bmxMotion = null;
            _fullBodyMotion = null;
            _ikLimbs = Array.Empty<UnityIKLimb>();
            _applied = false;
            _dirty = true;
            _nextResolveTime = 0f;
            _nextIntegrityCheckTime = 0f;
        }
    }

    [HarmonyPatch(
        typeof(CharacterSportsStance),
        nameof(CharacterSportsStance.UpdateThings))]
    internal static class BikeOnlyStanceNativeUpdatePatch
    {
        private static void Postfix(CharacterSportsStance __instance)
        {
            BikeOnlyStance.NotifyNativeStanceUpdated(__instance);
        }
    }
}
