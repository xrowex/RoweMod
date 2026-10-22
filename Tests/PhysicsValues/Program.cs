using System.Reflection;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using Il2CppMashBox.Core.Runtime.SmartData;
using rowemod;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;
using PhysicsControl = rowemod.Mods.Physics;

static class Program
{
    static int checks;
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    static NativeSetting Setting(float value, float min, float max, float normal) => new()
    { Definition = new SmartDataFloat { MinValue = min, MaxValue = max, DefaultValue = normal }, Value = value };
    static FakeVehicle Local(bool bound = true, bool proxy = false)
    {
        var vehicle = new FakeVehicle { gameObject = new GameObject { name = "BMXChassis" },
            _userGravitySmartData = Setting(0.2f, 0, 5, 0), _userQuickHopHeightSmartData = Setting(0.7f, 0.25f, 2, 1) };
        vehicle._userGravitySmartData.IsBound = vehicle._userQuickHopHeightSmartData.IsBound = bound;
        vehicle._userGravitySmartData._isProxy = vehicle._userQuickHopHeightSmartData._isProxy = proxy;
        Memory.vehicleController = vehicle;
        Memory.rMbCharacter = new GameObject();
        return vehicle;
    }
    static void Reset()
    {
        PhysicsValueControl.Release();
        GrindFrictionControl.Restore();
        Config.physics = new rowemod.Physics { physicsValueScaleVersion = 1, gravity = 12.5f, smallHopForce = 4.2f, nativeSmallHop = 1, grindFriction = 0.03f };
        Memory.vehicleController = null; Memory.rMbCharacter = null;
        Menu.Edits.Clear(); Menu.Buttons.Clear(); Menu.Toggles.Clear(); Menu.Ranges.Clear(); Config.Saves = 0;
        UnityEngine.Physics.gravity = new Vector3(0, -9.81f, 0);
    }
    static void Main()
    {
        Reset();
        var vehicle = Local();
        var jump = new JumpSettings { JumpVelocity = new[] { 3.2f, 5f } };
        PhysicsValueControl.Apply(); PhysicsValueControl.ApplyLegacyHop(jump);
        Check(UnityEngine.Physics.gravity.y == -9.81f && jump.JumpVelocity.SequenceEqual(new[] { 3.2f, 5f }), "Streets mode leaves global gravity and base hop velocities untouched");
        Check(vehicle._userQuickHopHeightSmartData.Value == 0.7f, "fresh config preserves the player's current Streets setting");
        PhysicsValueControl.DrawControls();
        Check(Menu.Ranges["Small Hop (x)"] == (1f, 0.25f, 2f), "hop range and reset come from the actual native definition");
        Check(Menu.Ranges["Gravity"] == (0f, 0f, 5f), "gravity range and reset come from the actual native definition");
        Menu.Edits["Small Hop (x)"] = 1f;
        PhysicsValueControl.DrawControls();
        Check(vehicle._userQuickHopHeightSmartData.Value == 1 && Config.physics.nativeSmallHopOverride, "native hop editor writes the same multiplier used by Streets");
        Check(Config.physics.smallHopForce == 4.2f && jump.JumpVelocity[0] == 3.2f, "native slider never reinterprets a legacy velocity as a multiplier");
        Menu.Edits["Gravity"] = 2.5f; PhysicsValueControl.DrawControls();
        Check(vehicle._userGravitySmartData.Value == 2.5f && Config.physics.nativeGravity == 2.5f && Config.Saves > 0, "gravity editor persists the value written to the native binding");

        Reset(); vehicle = Local();
        Config.physics.nativeSmallHopOverride = true; Config.physics.nativeSmallHop = 9f;
        Config.physics.nativeGravityOverride = true; Config.physics.nativeGravity = float.NaN;
        PhysicsValueControl.Apply();
        Check(vehicle._userQuickHopHeightSmartData.Value == 2f && vehicle._userGravitySmartData.Value == 0, "saved settings respect native limits and sanitize nonfinite values");
        vehicle._userQuickHopHeightSmartData.Value = 0.8f; PhysicsValueControl.Apply();
        Check(vehicle._userQuickHopHeightSmartData.Value == 0.8f, "later native-menu edits are not overwritten on every menu refresh");
        Check(Config.physics.nativeSmallHop == 0.8f, "native-menu changes also update the persisted override");
        Config.physics.nativeSmallHop = 1.5f; PhysicsValueControl.OnPresetLoaded();
        Check(vehicle._userQuickHopHeightSmartData.Value == 1.5f, "explicit preset load reapplies its saved native values");
        vehicle._userQuickHopHeightSmartData._isProxy = true;
        Config.physics.nativeSmallHop = 1.9f; PhysicsValueControl.OnPresetLoaded();
        Check(vehicle._userQuickHopHeightSmartData.Value == 1.5f, "cached binding cannot write after it becomes a remote proxy");

        foreach (bool proxy in new[] { false, true })
        {
            Reset(); vehicle = Local(bound: proxy, proxy: proxy);
            Config.physics.nativeSmallHopOverride = true; Config.physics.nativeSmallHop = 1.5f;
            PhysicsValueControl.Apply();
            Check(vehicle._userQuickHopHeightSmartData.Value == 0.7f, "unbound prefab and remote proxy are never changed");
        }
        Reset(); vehicle = Local(); Memory.rMbCharacter = null;
        Config.physics.nativeSmallHopOverride = true; Config.physics.nativeSmallHop = 1.5f; PhysicsValueControl.Apply();
        Check(vehicle._userQuickHopHeightSmartData.Value == 0.7f, "no rider context means no settings write");

        Reset(); vehicle = Local(); Config.physics.physicsValueScaleVersion = 0;
        jump = new JumpSettings { JumpVelocity = new[] { 3.2f, 5f } };
        PhysicsValueControl.Apply(); PhysicsValueControl.ApplyLegacyHop(jump); PhysicsValueControl.ApplyLegacyHop(jump);
        Check(UnityEngine.Physics.gravity.y == -12.5f && jump.JumpVelocity.SequenceEqual(new[] { 4.2f, 4.2f }), "legacy tunes retain their existing physical values until the user switches");
        PhysicsValueControl.ApplyLegacyHop(new JumpSettings { JumpVelocity = new[] { 3f } });
        Check(PhysicsValueControl.UseStreetsValues(), "legacy-to-native switch succeeds with a local binding");
        Check(UnityEngine.Physics.gravity.y == -9.81f && jump.JumpVelocity.SequenceEqual(new[] { 3.2f, 5f }), "switch restores distinct original hop velocities and global gravity");
        Check(vehicle._userQuickHopHeightSmartData.Value == 0.7f && !Config.physics.nativeSmallHopOverride, "switch preserves current Streets preferences");
        PhysicsValueControl.ResetToGameDefaults();
        Check(vehicle._userGravitySmartData.Value == 0 && vehicle._userQuickHopHeightSmartData.Value == 1, "reset uses Streets default gravity and hop values");

        TestMaterials(); TestPresets();
        Console.WriteLine($"PASS: {checks} physics-value, preset, and local-peg-material checks. Stubbed Unity; riding behavior remains unverified.");
    }

    static Collider ColliderFor(GameObject root, string name, PhysicMaterial material, bool trigger = false)
    {
        var peg = new GameObject { name = name }; peg.transform.parent = root.transform;
        var capsule = new GameObject { name = "Capsule" }; capsule.transform.parent = peg.transform;
        return new Collider { transform = capsule.transform, sharedMaterial = material, isTrigger = trigger };
    }
    static void TestMaterials()
    {
        Reset(); var vehicle = Local();
        var original = new PhysicMaterial { name = "Pegs", staticFriction = 0.03f, dynamicFriction = 0.02f };
        var peg = ColliderFor(vehicle.gameObject, "PegsFrontRight", original);
        var wheel = ColliderFor(vehicle.gameObject, "FrontWheel", original);
        var trigger = ColliderFor(vehicle.gameObject, "PegTrigger", original, true);
        vehicle.gameObject.Colliders = new[] { peg, wheel, trigger };
        GrindFrictionControl.Apply();
        Check(peg.sharedMaterial == original, "disabled friction keeps native materials untouched");
        Config.physics.grindFrictionEnabled = true; Config.physics.grindFriction = 0.2f; GrindFrictionControl.Apply();
        var clone = peg.sharedMaterial;
        Check(clone != original && clone.staticFriction == 0.2f && clone.dynamicFriction == 0.2f, "peg receives a local clone with both friction values");
        Check(original.staticFriction == 0.03f && original.dynamicFriction == 0.02f && wheel.sharedMaterial == original && trigger.sharedMaterial == original, "shared/remote material, wheels and trigger colliders are preserved");
        GrindFrictionControl.Apply(); Check(peg.sharedMaterial == clone, "repeat apply reuses the existing material copy");
        var replacement = new PhysicMaterial { name = "Replacement", staticFriction = 0.1f, dynamicFriction = 0.1f };
        peg.sharedMaterial = replacement; GrindFrictionControl.Apply();
        Check(clone.Destroyed && peg.sharedMaterial != replacement, "native part/material replacement retires the old clone");
        clone = peg.sharedMaterial; Config.physics.grindFrictionEnabled = false; GrindFrictionControl.Apply();
        Check(peg.sharedMaterial == replacement && clone.Destroyed, "disable restores the latest native material and frees the clone");
        Config.physics.grindFrictionEnabled = true; Config.physics.grindFriction = 0; GrindFrictionControl.Apply();
        Check(peg.sharedMaterial.dynamicFriction == 0, "zero-friction tune remains valid");
        Config.physics.grindFriction = 4f; GrindFrictionControl.Apply();
        Check(peg.sharedMaterial.dynamicFriction == 4f, "restored control retains the original zero-to-five friction range");
        clone = peg.sharedMaterial;
        peg.transform.parent.parent = new GameObject { name = "RemoteBike" }.transform;
        GrindFrictionControl.Apply();
        Check(peg.sharedMaterial == replacement && clone.Destroyed, "detaching a peg from the local bike restores its material");
        GrindFrictionControl.Restore();
    }

    static void TestPresets()
    {
        Reset(); var vehicle = Local();
        string root = Path.Combine(Path.GetTempPath(), "rowemod-physics-test-" + Guid.NewGuid().ToString("N"));
        MelonLoader.Utils.MelonEnvironment.ModsDirectory = root;
        var save = typeof(PhysicsPreset).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Static);
        var load = typeof(PhysicsPreset).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            Config.physics.lastVehicle = 4;
            save.Invoke(null, new object[] { "Native" });
            string directory = Path.Combine(root, "RoweMod", "PhysicsPresets");
            string json = File.ReadAllText(Path.Combine(directory, "Native.json"));
            Check(json.Contains("\"nativeSmallHopOverride\": true") && !Config.physics.nativeSmallHopOverride, "saving captures native values in the preset without enabling live overrides");
            vehicle._userQuickHopHeightSmartData.Value = 1.8f; Config.physics.lastVehicle = 2;
            load.Invoke(null, new object[] { "Native" });
            Check(vehicle._userQuickHopHeightSmartData.Value == 0.7f && Config.physics.lastVehicle == 2, "native preset restores the multiplier and retains the active vehicle selection");
            File.WriteAllText(Path.Combine(directory, "Legacy.json"), "{\"settings\":{\"gravity\":15.41,\"smallHopForce\":3.7,\"grindFriction\":0}}");
            load.Invoke(null, new object[] { "Legacy" });
            Check(Config.physics.physicsValueScaleVersion == 0 && Config.physics.smallHopForce == 3.7f && UnityEngine.Physics.gravity.y == -15.41f, "old presets retain their raw scale and tune");
            Check(Config.physics.grindFrictionEnabled && Config.physics.grindFriction == 0, "older friction presets preserve intentional zero friction");
            File.WriteAllText(Path.Combine(directory, "Recent.json"), "{\"settings\":{\"gravity\":12.5,\"smallHopForce\":4.2}}");
            load.Invoke(null, new object[] { "Recent" });
            Check(!Config.physics.grindFrictionEnabled && Config.physics.grindFriction == 0.03f, "presets from versions without friction keep it disabled with a usable edit default");
        }
        finally { PhysicsValueControl.Release(); GrindFrictionControl.Restore(); Directory.Delete(root, true); }
    }
}
