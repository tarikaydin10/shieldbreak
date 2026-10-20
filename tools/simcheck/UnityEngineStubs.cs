// Minimal managed stand-ins for the UnityEngine API the combat and formation definitions (Scripts/Data) use, so
// the ScriptableObject-based test setups (TestCombat) and the combat tests run under plain .NET. Plain objects
// instead of assets: CreateInstance is `new T()` (field initializers run as in Unity), Destroy does nothing.
// Only what Data/ and the tests use today; extend when they need more.
using System;
using Unity.Mathematics;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; } = "";
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static implicit operator float2(Vector2 v) => new float2(v.x, v.y);
        public static implicit operator Vector2(float2 v) => new Vector2(v.x, v.y);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static implicit operator float3(Vector3 v) => new float3(v.x, v.y, v.z);
        public static implicit operator Vector3(float3 v) => new Vector3(v.x, v.y, v.z);
    }

    /// <summary>Same formulas as UnityEngine.Mathf (double-precision System.Math, cast to float).</summary>
    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
    }

    public static class Debug
    {
        public static void LogWarning(object message) => Console.WriteLine("warning: " + message);
    }

    public static class Application
    {
        public static bool isPlaying => false;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName { get; set; }
        public string fileName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
}

namespace UnityEditor
{
    /// <summary>Tests that load real project assets need Unity; here they are reported as skipped.</summary>
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T : class
        {
            NUnit.Framework.Assert.Ignore($"Needs the Unity asset {path} (run .\\sb.ps1 test).");
            return null;
        }
    }
}
