// Minimal managed stand-ins for Unity.Collections, Unity.Jobs and Unity.Burst, so the pure sim compiles and
// its tests run under plain .NET (Linux, CI, cloud sessions). Jobs run sequentially; Burst attributes are ignored.
// Only what Sim/ uses today; extend when the sim needs more.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Unity.Collections
{
    public enum Allocator { Invalid, None, Temp, TempJob, Persistent }
    public enum NativeArrayOptions { UninitializedMemory, ClearMemory }
    [AttributeUsage(AttributeTargets.Field)] public sealed class ReadOnlyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class WriteOnlyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class NativeDisableParallelForRestrictionAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class DeallocateOnJobCompletionAttribute : Attribute { }

    public unsafe struct NativeArray<T> : IEnumerable<T>, IDisposable where T : struct
    {
        internal void* ptr;
        internal int length;
        internal bool owner;
        static int Size => Unsafe.SizeOf<T>();

        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            this.length = length;
            ptr = NativeMemory.AllocZeroed((nuint)Math.Max(1, length * Size));
            owner = true;
        }
        public NativeArray(T[] source, Allocator allocator) : this(source.Length, allocator) { for (int i = 0; i < source.Length; i++) this[i] = source[i]; }
        public int Length => length;
        public bool IsCreated => ptr != null;
        public T this[int index]
        {
            get { Check(index); return Unsafe.Read<T>((byte*)ptr + (long)index * Size); }
            set { Check(index); Unsafe.Write((byte*)ptr + (long)index * Size, value); }
        }
        void Check(int i) { if (ptr == null) throw new ObjectDisposedException("NativeArray"); if ((uint)i >= (uint)length) throw new IndexOutOfRangeException($"{i} of {length}"); }
        public void Dispose() { if (owner && ptr != null) NativeMemory.Free(ptr); ptr = null; }
        public NativeArray<T> GetSubArray(int start, int len)
        {
            if (start < 0 || len < 0 || start + len > length) throw new ArgumentOutOfRangeException();
            return new NativeArray<T> { ptr = (byte*)ptr + (long)start * Size, length = len, owner = false };
        }
        public void CopyFrom(NativeArray<T> src) { if (src.length != length) throw new ArgumentException("length"); Buffer.MemoryCopy(src.ptr, ptr, (long)length * Size, (long)length * Size); }
        public void CopyFrom(T[] src) { if (src.Length != length) throw new ArgumentException("length"); for (int i = 0; i < length; i++) this[i] = src[i]; }
        public void CopyTo(NativeArray<T> dst) => dst.CopyFrom(this);
        public T[] ToArray() { var a = new T[length]; for (int i = 0; i < length; i++) a[i] = this[i]; return a; }
        public ReadOnly AsReadOnly() => new ReadOnly(this);
        public IEnumerator<T> GetEnumerator() { for (int i = 0; i < length; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public static void Copy(NativeArray<T> src, int srcIndex, NativeArray<T> dst, int dstIndex, int len) { for (int i = 0; i < len; i++) dst[dstIndex + i] = src[srcIndex + i]; }

        public struct ReadOnly : IEnumerable<T>
        {
            readonly NativeArray<T> a;
            public ReadOnly(NativeArray<T> a) { this.a = a; }
            public int Length => a.Length;
            public T this[int i] => a[i];
            public IEnumerator<T> GetEnumerator() => a.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public T[] ToArray() => a.ToArray();
        }
    }

    public unsafe struct NativeList<T> : IDisposable where T : unmanaged
    {
        class Box { public NativeArray<T> data; public int length; }
        Box box;
        public NativeList(int capacity, Allocator allocator) { box = new Box { data = new NativeArray<T>(Math.Max(1, capacity), allocator) }; }
        public NativeList(Allocator allocator) : this(8, allocator) { }
        public bool IsCreated => box != null && box.data.IsCreated;
        public int Length { get => box.length; set { Reserve(value); box.length = value; } }
        public int Capacity => box.data.Length;
        void Reserve(int n)
        {
            if (n <= box.data.Length) return;
            var d = new NativeArray<T>(Math.Max(n, box.data.Length * 2), Allocator.Persistent);
            for (int i = 0; i < box.length; i++) d[i] = box.data[i];
            box.data.Dispose(); box.data = d;
        }
        public T this[int i] { get { if ((uint)i >= (uint)box.length) throw new IndexOutOfRangeException(); return box.data[i]; } set { if ((uint)i >= (uint)box.length) throw new IndexOutOfRangeException(); box.data[i] = value; } }
        public void Add(in T v) { Reserve(box.length + 1); box.data[box.length++] = v; }
        public void AddNoResize(T v) => Add(v);
        public void Clear() => box.length = 0;
        public void RemoveAtSwapBack(int i) { this[i] = this[box.length - 1]; box.length--; }
        public void ResizeUninitialized(int n) => Length = n;
        public NativeArray<T> AsArray() => box.data.GetSubArray(0, box.length);
        public void Sort<U>(U comparer) where U : IComparer<T>
        {
            var tmp = AsArray().ToArray();
            // Unity's NativeSortExtension is an unstable introsort; stable sort here is fine because callers use total orders
            var list = new List<T>(tmp); list.Sort((a, b) => comparer.Compare(a, b));
            for (int i = 0; i < list.Count; i++) box.data[i] = list[i];
        }
        public IEnumerator<T> GetEnumerator() { for (int i = 0; i < box.length; i++) yield return box.data[i]; }
        public void Dispose() { if (box != null) { box.data.Dispose(); box = null; } }
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    public static unsafe class NativeArrayUnsafeUtility
    {
        public static void* GetUnsafeReadOnlyPtr<T>(NativeArray<T> a) where T : struct => a.ptr;
        public static void* GetUnsafePtr<T>(NativeArray<T> a) where T : struct => a.ptr;
    }
    public static class UnsafeUtility { public static int SizeOf<T>() where T : struct => System.Runtime.CompilerServices.Unsafe.SizeOf<T>(); }
}

namespace Unity.Jobs
{
    public struct JobHandle { public void Complete() { } }
    public interface IJob { void Execute(); }
    public interface IJobParallelFor { void Execute(int index); }
    public static class JobExtensions
    {
        public static JobHandle Schedule<T>(this T job, JobHandle dep = default) where T : struct, IJob { job.Execute(); return default; }
        public static void Run<T>(this T job) where T : struct, IJob => job.Execute();
    }
    public static class IJobParallelForExtensions
    {
        public static JobHandle Schedule<T>(this T job, int count, int batch, JobHandle dep = default) where T : struct, IJobParallelFor { for (int i = 0; i < count; i++) job.Execute(i); return default; }
        public static void Run<T>(this T job, int count) where T : struct, IJobParallelFor { for (int i = 0; i < count; i++) job.Execute(i); }
    }
}

namespace Unity.Burst
{
    public enum FloatMode { Default, Strict, Deterministic, Fast }
    public enum FloatPrecision { Standard, High, Medium, Low }
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class BurstCompileAttribute : Attribute { public FloatMode FloatMode { get; set; } public FloatPrecision FloatPrecision { get; set; } public bool CompileSynchronously { get; set; } }
}
