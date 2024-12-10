using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace MagicLeap.SpatialMeshSerializer
{
    public static class MeshSerializer
    {
        public static readonly string DIRECTORY = $"{Application.persistentDataPath}/meshes";

        public struct LoadedMesh
        {
            public Mesh mesh;
            public NativeArray<byte> nativeData;
            public Pose offsetFromSpaceOrigin;
            public string name;
        }

        #region Public API

        /// <summary>
        /// Serializes all <see cref="Mesh"/> data from the given <paramref name="meshFilters"/> into an array of native byte arrays.
        /// </summary>
        /// <remarks>When using this method you are responsible for disposing of the returned native arrays.</remarks>
        /// <param name="meshFilters">Meshes to be saved.</param>
        /// <returns>An array of unmanaged byte arrays where each byte array contains the serialized bytes of a saved mesh.</returns>
        public static async Task<NativeArray<byte>[]> SerializeMeshesAsync(IList<MeshFilter> meshFilters)
        {
            Pose offset = default;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!MeshLocalizer.TryGetOffsetPose(meshFilters[0].transform, out offset))
                throw new InvalidOperationException("Cannot save mesh while not localized into a space!");
#endif

            return await SerializeMeshesAsync(meshFilters, offset);
        }

        /// <param name="offset">Local position/rotation offset from the mesh to the current space origin.</param>
        /// <inheritdoc cref="SerializeMeshesAsync"/>
        public static async Task<NativeArray<byte>[]> SerializeMeshesAsync(IList<MeshFilter> meshFilters, Pose offset)
        {
            var validMeshes = ListPool<MeshFilter>.Get();

            for (int i = 0; i < meshFilters.Count; i++)
            {
                if (meshFilters[i].sharedMesh.vertexCount > 0)
                    validMeshes.Add(meshFilters[i]);
            }

            var dataTasks = new Task<NativeArray<byte>>[validMeshes.Count];

            for (int i = 0; i < validMeshes.Count; i++)
            {
                dataTasks[i] = RunSerializeJobAsync(validMeshes[i].sharedMesh, offset);
            }

            ListPool<MeshFilter>.Release(validMeshes);

            return await Task.WhenAll(dataTasks);
        }

        /// <summary>
        /// Saves all <see cref="Mesh"/> data from the given <paramref name="meshFilters"/> into a folder labeled <paramref name="id"/>.
        /// </summary>
        /// <param name="meshFilters">Meshes to be saved.</param>
        public static async Task SaveCurrentSpaceMeshAsync(IList<MeshFilter> meshFilters)
        {
            MeshLocalizer.TryGetSpaceUUID(out var id);

            var nativeArrays = await SerializeMeshesAsync(meshFilters);

            await SaveMeshesToFileAsync(id, nativeArrays);

            nativeArrays.DisposeAll();
        }

        /// <summary>
        /// Saves all <see cref="Mesh"/> data from the given <paramref name="data"/> into a folder labeled <paramref name="id"/>.
        /// </summary>
        /// <param name="data">Mesh data to be saved.</param>
        /// <param name="id">Unique ID to save these meshes under. Recommended that you use the Space ID.</param>
        public static async Task SaveMeshesToFileAsync(string id, NativeArray<byte>[] data)
        {
            DeleteMeshes(id); // Gotta clear out the old meshes before saving a new one.

            var saveTasks = new Task<bool>[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                saveTasks[i] = TrySaveMeshToFileAsync(id, $"Mesh Chunk {i}", data[i]);
            }

            await Task.WhenAll(saveTasks);
        }

        /// <summary>
        /// Loads all meshes under the given <paramref name="id"/> and instantiates them into <see cref="GameObject"/>s.
        /// </summary>
        /// <param name="meshPrefab">Prefab to instantiate for each portion of the mesh and apply the mesh to. Prefab must contain a MeshFilter component.</param>
        /// <returns>An array of meshes, their names, and offset from the previously provided space origin transform.</returns>
        public static async Task<GameObject[]> LoadCurrentSpaceMeshAndInstantiateAsync(GameObject meshPrefab)
        {
            MeshLocalizer.TryGetSpaceUUID(out var id);

            var savedMeshes = await LoadMeshesAsync(id);

            var meshObjects = await InstantiateLoadedMeshesAsync(savedMeshes, meshPrefab);

            savedMeshes?.DisposeAll();

            return meshObjects;
        }

        /// <summary>
        /// Instantiates all the given <paramref name="savedMeshes"/> into <see cref="GameObject"/>s within the scene.
        /// </summary>
        /// <param name="savedMeshes">Loaded mesh data to instantiate.</param>
        /// <param name="meshPrefab">Prefab to instantiate and place the processed <see cref="Mesh"/> objects into. Must contain a <see cref="MeshFilter"/> component.</param>
        /// <param name="disposeNative">If the native arrays in the <paramref name="savedMeshes"/> should be disposed of after instantiation.</param>
        /// <returns>An array of <see cref="GameObject"/>s containing all the meshes that have been instantiated.</returns>
        public static async Task<GameObject[]> InstantiateLoadedMeshesAsync(LoadedMesh[] savedMeshes, GameObject meshPrefab)
        {
            if (savedMeshes.IsNullOrEmpty())
            {
                Debug.LogError("No meshes were loaded!");
                return null;
            }

            if (!meshPrefab.GetComponent<MeshFilter>())
            {
                Debug.LogError("No MeshFilter component on the provided Mesh Prefab!");
                return null;
            }

            var anyFailedToLocalize = false;

            var handle = GameObject.InstantiateAsync(meshPrefab, savedMeshes.Length);

            while (!handle.isDone)
                await Task.Yield();

            var gameObjects = handle.Result;

            for (int i = 0; i < gameObjects.Length; i++)
            {
                var go = gameObjects[i];
                var mf = go.GetComponent<MeshFilter>();
                mf.sharedMesh = savedMeshes[i].mesh;

#if UNITY_ANDROID && !UNITY_EDITOR
                if (!MeshLocalizer.TryLocalizeTransformToSpace(go.transform, savedMeshes[i].offsetFromSpaceOrigin))
                    anyFailedToLocalize = true;
#else
                var offset = savedMeshes[i].offsetFromSpaceOrigin;
                go.transform.SetPositionAndRotation(offset.position, offset.rotation);
#endif
                go.AddComponent<MeshCollider>().sharedMesh = savedMeshes[i].mesh;
            }

            if (anyFailedToLocalize)
                Debug.LogError("LoadMeshesAndInstantiateAsync called while not localized into a space! Meshes will not be localized as a result.");

            return gameObjects;
        }

        /// <summary>
        /// Loads all meshes from files on the device storage with the given <paramref name="id"/>.
        /// </summary>
        /// <param name="id">Unique ID to load these meshes from. Recommended that you use the Space ID.</param>
        /// <returns>An array of meshes, their names, and offset from the previously provided space origin transform.</returns>
        public static async Task<LoadedMesh[]> LoadMeshesAsync(string id)
        {
            var dir = GetDirectory(id);

            if (!Directory.Exists(dir))
                return null;

            var files = Directory.GetFiles(dir);

            var dataTasks = new Task<NativeArray<byte>>[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                dataTasks[i] = TryLoadMeshFromFileAsync(files[i]);
            }

            var dataArrays = await Task.WhenAll(dataTasks);

            return await LoadMeshesAsync(dataArrays);
        }

        /// <summary>
        /// Loads all meshes from the raw data in the given <paramref name="dataArrays"/>.
        /// </summary>
        /// <param name="dataArrays"></param>
        /// <returns>An array of meshes, their names, and offset from the previously provided space origin transform.</returns>
        public static async Task<LoadedMesh[]> LoadMeshesAsync(NativeArray<byte>[] dataArrays)
        {
            var meshTasks = new Task<LoadedMesh>[dataArrays.Length];

            for (int i = 0; i < dataArrays.Length; i++)
            {
                meshTasks[i] = DeserializeAsync(dataArrays[i], i.ToString());
            }

            return await Task.WhenAll(meshTasks);
        }

        /// <summary>
        /// Deletes all files on this device associated with the given <paramref name="id"/>.
        /// </summary>
        /// <param name="id">Unique ID of this set of meshes. Recommended that you use the Space ID.</param>
        public static void DeleteMeshes(string id)
        {
            var dir = GetDirectory(id);

            if (!Directory.Exists(dir))
                return;

            Directory.Delete(dir, true);
        }

        #endregion

        #region Private Methods

        private static async Task<NativeArray<byte>> RunSerializeJobAsync(Mesh mesh, Pose spaceOffset)
        {
            var meshData = Mesh.AcquireReadOnlyMeshData(mesh)[0];

            var vertexBuffer = meshData.GetVertexData<byte>();
            var indexBuffer = meshData.GetIndexData<byte>();

            var serializedData = new NativeArray<byte>(MeshSerializeJob.HEADER_SIZE_BYTES + MeshSerializeJob.POSE_SIZE_BYTES + vertexBuffer.Length + indexBuffer.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            var job = new MeshSerializeJob()
            {
                vertexBuffer = vertexBuffer,
                indexBuffer = indexBuffer,
                spaceOffset = spaceOffset,
                serializedData = serializedData,
                vertexCount = mesh.vertexCount,
                indexCount = (int)mesh.GetIndexCount(0)
            };

            var handle = job.Schedule();

            while (!handle.IsCompleted)
                await Task.Yield();

            handle.Complete();

            return job.serializedData;
        }

        private static async Task<LoadedMesh> DeserializeAsync(NativeArray<byte> nativeData, string meshName)
        {
            var mesh = new Mesh();
            mesh.name = meshName;
            var meshData = Mesh.AllocateWritableMeshData(1);

            var job = new MeshDeserializeJob()
            {
                serializedData = nativeData,
                meshData = meshData,
                pose = new NativeArray<Pose>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
            };

            var handle = job.Schedule();

            while (!handle.IsCompleted)
                await Task.Yield();

            handle.Complete();

            Mesh.ApplyAndDisposeWritableMeshData(job.meshData, mesh, MeshUpdateFlags.DontValidateIndices |
                MeshUpdateFlags.DontResetBoneBounds |
                MeshUpdateFlags.DontNotifyMeshUsers |
                MeshUpdateFlags.DontRecalculateBounds);

            mesh.RecalculateBounds();

            var loadedMesh = new LoadedMesh()
            {
                mesh = mesh,
                offsetFromSpaceOrigin = job.pose[0],
                name = meshName,
                nativeData = job.serializedData
            };

            job.Dispose();

            return loadedMesh;
        }

        private static async Task<bool> TrySaveMeshToFileAsync(string folderName, string fileName, NativeArray<byte> data)
        {
#if !UNITY_WEBGL
            return await Task.Run(() => TrySaveMeshToFile(folderName, fileName, data));
#else
            return TrySaveMeshToFile(folderName, fileName, data);
#endif
        }

        private static bool TrySaveMeshToFile(string folderName, string fileName, NativeArray<byte> data)
        {
            try
            {
                var dir = GetDirectory(folderName);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var path = GetFullPath(folderName, fileName);

                using (var fileStream = File.Create(path))
                {
                    fileStream.Write(data);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        private static async Task<NativeArray<byte>> TryLoadMeshFromFileAsync(string path)
        {
#if !UNITY_WEBGL
            return await Task.Run(() => TryLoadMeshFromFile(path));
#else
            return TryLoadMeshFromFile(path);
#endif
        }

        private static NativeArray<byte> TryLoadMeshFromFile(string path)
        {
            try
            {
                using (var fileStream = File.OpenRead(path))
                {
                    var data = new NativeArray<byte>((int)fileStream.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    fileStream.Read(data);
                    return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return default;
            }
        }

        private static string GetFullPath(string folderName, string fileName)
        {
            return $"{DIRECTORY}/{folderName}/{fileName}.bin";
        }

        private static string GetDirectory(string folderName)
        {
            return $"{DIRECTORY}/{folderName}";
        }

        #endregion
    }
}