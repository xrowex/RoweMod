using System;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation;
using Il2CppMashBox.Character;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Switches only the local rider's foot stance targets. Native character
    /// stance, trick/grind bindings, animation mirroring, and bike physics are
    /// intentionally left untouched.
    /// </summary>
    public static class BikeOnlyStance
    {
        private const float ResolveIntervalSeconds = 1f;

        private sealed class FootStanceBinding
        {
            public FootStancePositioner Positioner;
            public bool OriginalGoofy;
        }

        private static GameObject _root;
        private static int _rootId;
        private static int _vehicleId;
        private static float _nextResolveTime;

        private static CharacterManager _characterManager;
        private static FootStanceBinding[] _feet = Array.Empty<FootStanceBinding>();

        private static bool _applied;
        private static bool _appliedGoofy;
        private static bool _dirty = true;
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
                RestoreOriginalFeet();
                _status = _root == null
                    ? "Waiting for the local rider."
                    : "Disabled; native foot placement restored.";
                return;
            }

            if (!CanApply())
            {
                RestoreOriginalFeet();
                return;
            }

            if (!_dirty && _applied && _appliedGoofy == settings.goofy)
                return;

            ApplyFootStance(settings.goofy);
        }

        public static void NotifySettingsChanged()
        {
            _dirty = true;
            Update();
        }

        public static void OnLocalPlayerSpawned()
        {
            EnsureReferences(true);
            _dirty = true;
            Update();
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            RestoreOriginalFeet();
            ClearReferences();
            _status = gameplayScene
                ? "Waiting for the local rider."
                : "Feet-only stance is available in gameplay.";
        }

        public static void Cleanup()
        {
            RestoreOriginalFeet();
            ClearReferences();
        }

        private static void EnsureReferences(bool force = false)
        {
            GameObject root = Memory.rMbCharacter;
            int rootId = root != null ? root.GetInstanceID() : 0;
            int vehicleId = Memory.vehicleController != null
                ? Memory.vehicleController.GetInstanceID()
                : 0;
            bool changed = rootId != _rootId || vehicleId != _vehicleId;
            bool missing = _characterManager == null || _feet.Length == 0;

            if (!force && !changed && !missing)
                return;
            if (!force && !changed && Time.unscaledTime < _nextResolveTime)
                return;

            _nextResolveTime = Time.unscaledTime + ResolveIntervalSeconds;
            if (changed || force)
            {
                RestoreOriginalFeet();
                ClearReferences();
            }

            if (root == null)
                return;

            _root = root;
            _rootId = rootId;
            _vehicleId = vehicleId;
            _characterManager = root.GetComponentInChildren<CharacterManager>(true);

            FootStancePositioner[] positioners =
                root.GetComponentsInChildren<FootStancePositioner>(true) ??
                Array.Empty<FootStancePositioner>();
            _feet = new FootStanceBinding[positioners.Length];
            for (int i = 0; i < positioners.Length; i++)
            {
                FootStancePositioner positioner = positioners[i];
                _feet[i] = new FootStanceBinding
                {
                    Positioner = positioner,
                    OriginalGoofy = positioner != null && positioner._isGoofy
                };
            }

            _dirty = true;
            _status = positioners.Length > 0
                ? $"Feet-only stance ready: {positioners.Length} foot target(s)."
                : "No FootStancePositioner was found on the local rider.";
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
                    _status = "Mount a bike to switch foot stance.";
                    return false;
                }
            }
            catch
            {
                _status = "Local rider state is temporarily unavailable.";
                return false;
            }

            if (_feet.Length == 0)
            {
                _status = "No FootStancePositioner was found on the local rider.";
                return false;
            }

            return true;
        }

        private static void ApplyFootStance(bool goofy)
        {
            int appliedCount = 0;
            try
            {
                for (int i = 0; i < _feet.Length; i++)
                {
                    FootStancePositioner positioner = _feet[i]?.Positioner;
                    if (positioner == null)
                        continue;

                    positioner.SetGoofy(goofy);
                    appliedCount++;
                    _applied = true;
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
                    $"Feet: {(goofy ? "Goofy" : "Regular")} | " +
                    $"Tricks/grinds: native {native} | " +
                    $"{appliedCount} foot target(s)";
                Log.Msg($"[BikeOnlyStance] {_status}");
            }
            catch (Exception ex)
            {
                _dirty = true;
                _status = $"Foot stance apply failed: {ex.Message}";
                Log.Error($"[BikeOnlyStance] Apply failed: {ex}");
            }
        }

        private static void RestoreOriginalFeet()
        {
            if (!_applied)
                return;

            for (int i = 0; i < _feet.Length; i++)
            {
                FootStanceBinding binding = _feet[i];
                if (binding?.Positioner == null)
                    continue;

                try
                {
                    binding.Positioner.SetGoofy(binding.OriginalGoofy);
                }
                catch
                {
                    // The rider may already have been destroyed during cleanup.
                }
            }

            _applied = false;
            _dirty = true;
        }

        private static void ClearReferences()
        {
            _root = null;
            _rootId = 0;
            _vehicleId = 0;
            _characterManager = null;
            _feet = Array.Empty<FootStanceBinding>();
            _applied = false;
            _dirty = true;
            _nextResolveTime = 0f;
        }
    }
}
