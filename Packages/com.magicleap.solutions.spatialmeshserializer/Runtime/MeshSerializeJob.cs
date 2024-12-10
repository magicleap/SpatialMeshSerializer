using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace MagicLeap.SpatialMeshSerializer
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    internal struct MeshSerializeJob : IJob
    {
        public const int HEADER_SIZE_BYTES = sizeof(int) * 4;
        public const int POSE_SIZE_BYTES = 7 * sizeof(float); // One Vector3 and one Quaternion use 3 + 4 floats.

        [ReadOnly] public int vertexCount;
        [ReadOnly] public int indexCount;
        [ReadOnly] public NativeArray<byte> vertexBuffer;
        [ReadOnly] public NativeArray<byte> indexBuffer;
        [ReadOnly] public Pose spaceOffset;

        [WriteOnly] public NativeArray<byte> serializedData;

        public void Execute()
        {
            var offset = 0;

            CopyBytes(vertexCount, serializedData, 0 * sizeof(int));
            CopyBytes(vertexBuffer.Length, serializedData, 1 * sizeof(int));
            CopyBytes(indexCount, serializedData, 2 * sizeof(int));
            CopyBytes(indexBuffer.Length, serializedData, 3 * sizeof(int));

            offset += HEADER_SIZE_BYTES;

            CopyBytes(spaceOffset, serializedData, offset);

            offset += POSE_SIZE_BYTES;

            var vertexSlice = serializedData.Slice(offset, vertexBuffer.Length);

            CopyData(vertexBuffer, vertexSlice);

            offset += vertexBuffer.Length;

            var indexSlice = serializedData.Slice(offset, indexBuffer.Length);

            CopyData(indexBuffer, indexSlice);
        }

        public static unsafe void CopyData<T>(ReadOnlySpan<T> from, NativeSlice<T> to) where T : unmanaged
        {
            fixed (T* srcPtr = from)
            {
                var dstPtr = to.GetUnsafePtr();
                UnsafeUtility.MemCpy(dstPtr, srcPtr, sizeof(T) * from.Length);
            }
        }

        private static unsafe void CopyBytes<T>(T value, Span<byte> buffer, int startIndex) where T : unmanaged
        {
            fixed (byte* numPtr = &buffer[startIndex])
                *(T*)numPtr = value;
        }
    }
}