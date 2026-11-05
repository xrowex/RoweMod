using System;
using System.Collections.Generic;
using NMatrix = System.Numerics.Matrix4x4;
using NQuaternion = System.Numerics.Quaternion;
using NVector = System.Numerics.Vector3;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new(1, 1, 1);
    }
    public struct Quaternion
    {
        internal NQuaternion value;
        public static Quaternion Euler(float x, float y, float z) => new() { value = NQuaternion.CreateFromYawPitchRoll(y * MathF.PI / 180, x * MathF.PI / 180, z * MathF.PI / 180) };
    }
    public struct Matrix4x4
    {
        // System.Numerics uses row vectors; reverse products to emulate Unity's
        // column-vector composition while retaining an independent math engine.
        internal NMatrix value;
        public static Matrix4x4 identity => new() { value = NMatrix.Identity };
        public static Matrix4x4 Scale(Vector3 scale) => new() { value = NMatrix.CreateScale(scale.x, scale.y, scale.z) };
        public static Matrix4x4 TRS(Vector3 position, Quaternion rotation, Vector3 scale) => new() {
            value = NMatrix.CreateScale(scale.x, scale.y, scale.z) * NMatrix.CreateFromQuaternion(rotation.value) * NMatrix.CreateTranslation(position.x, position.y, position.z)
        };
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new() { value = b.value * a.value };
        public Vector3 MultiplyPoint(Vector3 point)
        {
            var p = NVector.Transform(new NVector(point.x, point.y, point.z), value); return new(p.X, p.Y, p.Z);
        }
    }
    public struct Color
    {
        public float r, g, b;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public static class Mathf
    {
        public const float Rad2Deg = 180 / MathF.PI;
        public static float Atan2(float y, float x) => MathF.Atan2(y, x);
    }
    public sealed class Texture2D { public static readonly Texture2D whiteTexture = new(); }
    public static class GUI
    {
        public static Matrix4x4 matrix = Matrix4x4.identity;
        public static Color color;
        public static bool ThrowOnDraw;
        public static readonly List<(Rect rect, Matrix4x4 matrix, Color color)> Draws = new();
        public static void DrawTexture(Rect rect, Texture2D texture)
        {
            if (ThrowOnDraw) throw new InvalidOperationException("Simulated GUI draw failure");
            Draws.Add((rect, matrix, color));
        }
    }
}
