using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicLeap.SpatialMeshSerializer
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    internal struct MeshDeserializeJob : IJob, IDisposable
    {
        private const int VERTEX_DIMENSION = 3;
        private const int NUM_VERTEX_ATTRIBUTES = 2;
        public const int VERTEX_SIZE = VERTEX_DIMENSION * NUM_VERTEX_ATTRIBUTES * sizeof(float);

        [ReadOnly] public NativeArray<byte> serializedData;

        public Mesh.MeshDataArray meshData;
        [WriteOnly] public NativeArray<Pose> pose;

        public void Execute()
        {
            var mesh = meshData[0];

            var offset = 0;

            var header = serializedData.Slice(0, MeshSerializeJob.HEADER_SIZE_BYTES).SliceConvert<int>();

            var vertexCount = header[0];
            var vertexBufferLength = header[1];
            var indexCount = header[2];
            var indexBufferLength = header[3];

            offset += MeshSerializeJob.HEADER_SIZE_BYTES;

            pose[0] = serializedData.Slice(offset, MeshSerializeJob.POSE_SIZE_BYTES).SliceConvert<Pose>()[0];

            offset += MeshSerializeJob.POSE_SIZE_BYTES;

            var layout = new NativeArray<VertexAttributeDescriptor>(NUM_VERTEX_ATTRIBUTES, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, VERTEX_DIMENSION);
            layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, VERTEX_DIMENSION);
            mesh.SetVertexBufferParams(vertexCount, layout);
            var vertexBuffer = mesh.GetVertexData<byte>();

            serializedData.Slice(offset, vertexBufferLength).CopyTo(vertexBuffer);

            offset += vertexBufferLength;

            var indices = serializedData.Slice(offset, indexBufferLength).SliceConvert<ushort>();

            mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
            var indexBuffer = mesh.GetIndexData<ushort>();

            indices.CopyTo(indexBuffer);

            var desc = new SubMeshDescriptor
            {
                topology = MeshTopology.Triangles,
                firstVertex = 0,
                baseVertex = 0,
                indexStart = 0,
                vertexCount = vertexCount,
                indexCount = indices.Length
            };

            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, desc, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds);
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            pose.Dispose();

            _disposed = true;
        }
    }
}