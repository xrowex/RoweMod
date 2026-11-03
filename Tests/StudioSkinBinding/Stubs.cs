using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace UnityEngine
{
    public enum HumanBodyBones { Hips }
    public sealed class GameObject
    {
        public Transform transform;
        public int layer;
        public bool activeSelf = true;
        public GameObject(string name) { transform = new Transform(name, this); }
        public void SetActive(bool active) => activeSelf = active;
    }
    public sealed class Transform
    {
        private static int next;
        private readonly int id = ++next;
        private readonly List<Transform> children = new();
        public string name;
        public GameObject gameObject;
        public Transform parent { get; private set; }
        public Vector3 localPosition, localScale = Vector3.One;
        public Quaternion localRotation = Quaternion.Identity;
        public Vector3 position => (parent?.position ?? Vector3.Zero) + localPosition;
        public Transform(string name, GameObject owner) { this.name = name; gameObject = owner; }
        public int GetInstanceID() => id;
        public void SetParent(Transform value, bool worldPositionStays)
        {
            var world = position; parent?.children.Remove(this); parent = value; parent?.children.Add(this);
            if (worldPositionStays) localPosition = world - (parent?.position ?? Vector3.Zero);
        }
        public Transform Find(string name) => children.FirstOrDefault(t => t.name == name);
        public T[] GetComponentsInChildren<T>(bool inactive) where T : class =>
            new[] { this }.Concat(children.SelectMany(c => c.GetComponentsInChildren<Transform>(inactive))).Cast<T>().ToArray();
    }
    public sealed class Animator
    {
        public Transform transform, hips;
        public Transform GetBoneTransform(HumanBodyBones bone) => hips;
    }
}
