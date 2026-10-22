using System.Reflection;

namespace rowemod
{
    public struct Physics
    {
        public float gravity, smallHopForce, nativeGravity, nativeSmallHop, grindFriction, physicsStepRate, grindPoseLerpSpeed;
        public int physicsValueScaleVersion, lastVehicle;
        public bool nativeGravityOverride, nativeSmallHopOverride, grindFrictionEnabled;
    }
    public static class Config
    {
        public static Physics physics;
        public static int Saves;
        public static void RequestSave() => Saves++;
    }
    public static class Menu
    {
        public static UnityEngine.GUIStyle UiMutedWrappedStyle, UiSearchFieldStyle, UiRowButtonSelectedStyle, UiRowButtonStyle, redButtonStyle;
        public static float EffectiveUiScale = 1;
        public static Dictionary<string, float> Edits = new();
        public static HashSet<string> Buttons = new();
        public static HashSet<string> Toggles = new();
        public static Dictionary<string, (float Default, float Min, float Max)> Ranges = new();
        public static void Slider(string label, ref float value, float defaultValue, float min, float max)
        {
            Ranges[label] = (defaultValue, min, max);
            if (Edits.Remove(label, out float edit)) value = edit;
        }
        public static bool SecondaryButton(string label, params object[] options) => Buttons.Remove(label);
        public static bool PrimaryButton(string label, params object[] options) => Buttons.Remove(label);
        public static bool ControllerButton(string id, string label, object style, params object[] options) => Buttons.Remove(label);
        public static void ModernToggle(string label, ref bool value, string id) { if (Toggles.Remove(label)) value = !value; }
        public static void BeginAltPane(string title, string description) { }
        public static void EndPane() { }
    }
}
namespace rowemod.Utils
{
    public static class Memory
    {
        public static UnityEngine.GameObject rMbCharacter;
        public static FakeVehicle vehicleController;
    }
    public static class Log
    {
        public static void Msg(string message) { }
        public static void Warning(string message) { }
        public static void Error(string message) { }
    }
    public sealed class FakeVehicle : UnityEngine.Object
    {
        public UnityEngine.GameObject gameObject;
        public NativeSetting _userGravitySmartData { get; set; }
        public NativeSetting _userQuickHopHeightSmartData { get; set; }
    }
    public sealed class NativeSetting
    {
        public Il2CppMashBox.Core.Runtime.SmartData.SmartDataFloat Definition { get; set; }
        public bool IsBound { get; set; } = true;
        public bool _isProxy { get; set; }
        float current;
        public int Writes;
        public float Value { get => current; set { current = value; Writes++; } }
    }
}
namespace Il2CppMashBox.Core.Runtime.SmartData
{
    public sealed class SmartDataFloat : UnityEngine.Object { public float MinValue, MaxValue, DefaultValue; }
}
namespace Il2CppMashBox.Core.Runtime.Physics.Vehicle
{
    public sealed class JumpSettings : UnityEngine.Object { public float[] JumpVelocity; }
}
namespace MelonLoader.Utils
{
    public static class MelonEnvironment { public static string ModsDirectory; }
}
namespace rowemod.Mods { public static class Physics { public static void Update() { PhysicsValueControl.Apply(); GrindFrictionControl.Apply(); } } }
namespace UnityEngine
{
    public class Object
    {
        static long next;
        public IntPtr Pointer { get; } = (IntPtr)Interlocked.Increment(ref next);
        public string name;
        public HideFlags hideFlags;
        public bool Destroyed;
        public int GetInstanceID() => (int)Pointer;
        public static T Instantiate<T>(T original) where T : Object
        {
            if (original is PhysicMaterial material) return new PhysicMaterial { name = material.name, staticFriction = material.staticFriction, dynamicFriction = material.dynamicFriction } as T;
            throw new NotSupportedException();
        }
        public static void Destroy(Object obj) => obj.Destroyed = true;
    }
    public enum HideFlags { HideAndDontSave }
    public class GameObject : Object
    {
        public Transform transform;
        public Collider[] Colliders = Array.Empty<Collider>();
        public GameObject() { transform = new Transform { gameObject = this }; }
        public T[] GetComponentsInChildren<T>(bool inactive) => Colliders.Cast<T>().ToArray();
    }
    public sealed class Transform
    {
        public GameObject gameObject;
        public Transform parent;
        public string name => gameObject.name;
    }
    public sealed class Collider : Object
    {
        public Transform transform;
        public bool isTrigger;
        public PhysicMaterial sharedMaterial;
    }
    public sealed class PhysicMaterial : Object { public float staticFriction, dynamicFriction; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public struct Vector2 { }
    public static class Physics { public static Vector3 gravity = new(0, -9.81f, 0); }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        public static int Max(int a, int b) => Math.Max(a, b);
    }
    public static class GUI { public static void FocusControl(string name) { } }
    public class GUIStyle { }
    public static class GUILayout
    {
        public static void Label(string text, object style) { }
        public static object Height(float value) => null;
        public static object Width(float value) => null;
        public static object MinWidth(float value) => null;
        public static object ExpandWidth(bool value) => null;
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static Vector2 BeginScrollView(Vector2 position, params object[] options) => position;
        public static void EndScrollView() { }
        public static string TextField(string value, int length, object style, params object[] options) => value;
    }
}
