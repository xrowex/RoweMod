using System;
using NVector = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        internal NVector N => new(x, y, z);
        internal static Vector3 From(NVector n) => new(n.X, n.Y, n.Z);
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 forward => new(0, 0, 1);
        public Vector3 normalized => From(NVector.Normalize(N));
        public float magnitude => N.Length();
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.N + b.N);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.N - b.N);
        public static Vector3 operator *(Vector3 a, float f) => From(a.N * f);
        public static Vector3 operator /(Vector3 a, float f) => From(a.N / f);
        public static float Dot(Vector3 a, Vector3 b) => NVector.Dot(a.N, b.N);
    }
    public struct Quaternion
    {
        internal NQuaternion N;
        public static Quaternion identity => new() { N = NQuaternion.Identity };
        public static Quaternion Euler(float x, float y, float z) => new() { N =
            NQuaternion.CreateFromAxisAngle(NVector.UnitY, y * MathF.PI / 180) *
            NQuaternion.CreateFromAxisAngle(NVector.UnitX, x * MathF.PI / 180) *
            NQuaternion.CreateFromAxisAngle(NVector.UnitZ, z * MathF.PI / 180) };
        public static Quaternion operator *(Quaternion a, Quaternion b) => new() { N = a.N * b.N };
        public static Vector3 operator *(Quaternion a, Vector3 b) => Vector3.From(NVector.Transform(b.N, a.N));
        public static Quaternion Inverse(Quaternion q) => new() { N = NQuaternion.Inverse(q.N) };
        public static Quaternion Slerp(Quaternion a, Quaternion b, float f) => new() { N = NQuaternion.Slerp(a.N, b.N, Math.Clamp(f, 0, 1)) };
        public static Quaternion FromToRotation(Vector3 a, Vector3 b)
        {
            var first = NVector.Normalize(a.N); var second = NVector.Normalize(b.N); float dot = NVector.Dot(first, second);
            if (dot > .999999f) return identity;
            if (dot < -.999999f) { var axis = NVector.Cross(first, NVector.UnitY); if (axis.LengthSquared() < .001f) axis = NVector.Cross(first, NVector.UnitZ); return new() { N = NQuaternion.CreateFromAxisAngle(NVector.Normalize(axis), MathF.PI) }; }
            return new() { N = NQuaternion.Normalize(new NQuaternion(NVector.Cross(first, second), 1 + dot)) };
        }
    }
    public sealed class Transform
    {
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public float scale = 1;
        public Quaternion rotation { get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value; }
        public Vector3 position { get => parent == null ? localPosition : parent.TransformPoint(localPosition); set => localPosition = parent == null ? value : parent.InverseTransformPoint(value); }
        public Vector3 TransformPoint(Vector3 point)
        {
            var local = localRotation * (point * scale) + localPosition;
            return parent == null ? local : parent.TransformPoint(local);
        }
        public Vector3 InverseTransformPoint(Vector3 point)
        {
            var local = parent == null ? point : parent.InverseTransformPoint(point);
            return (Quaternion.Inverse(localRotation) * (local - localPosition)) / scale;
        }
    }
}
