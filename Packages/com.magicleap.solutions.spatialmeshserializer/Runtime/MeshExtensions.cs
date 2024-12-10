using System;
using Unity.Collections;

namespace MagicLeap.SpatialMeshSerializer
{
    public static class MeshExtensions
    {
        public static bool IsNullOrEmpty<T>(this T[] arr)
        {
            return arr == null || arr.Length <= 0;
        }

        public static bool IsPopulated<T>(this T[] arr)
        {
            return arr != null && arr.Length > 0;
        }

        public static void DisposeAll(this NativeArray<byte>[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i].Dispose();
            }
        }

        public static void DisposeAll(this ReadOnlySpan<MeshSerializer.LoadedMesh> arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i].nativeData.Dispose();
            }
        }

        public static void DisposeAll(this MeshSerializer.LoadedMesh[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i].nativeData.Dispose();
            }
        }
    }
}