# Spatial Mesh Serializer

## Overview

This project contains both the `com.magicleap.solutions.spatialmeshserializer` and a pre-configured Unity Project which demonstrate how to capture, save, and reload the runtime mesh generated by the Magic Leap 2. Saving and loading the mesh allows developers to:

- Scan an environment once and save the resulting spatial meshes.
- Reload saved meshes in future sessions without rescanning.
- Use Magic Leap Spaces to recall the saved mesh when localized into a known environment.

- `./Packages/com.magicleap.solutions.spatialmeshserializer/`: Contains runtime scripts for serialization, deserialization, and localization.
- `Spatial Mesh Serializer Sample`: The project includes a sample that can be imported into existing projects from the package manager. The sample includes sample scripts and scene (`MeshSample.unity`).

## Requirements

- Unity 2022.3.49 or later
- A fully scanned and saved environment using Magic Leap Spaces
- A Magic Leap 2 device that can localize to the environment
- [Magic Leap Unity SDK](https://developer.magicleap.com/)

## Getting Started

Developers can use the package be either using the pre-configured project or by importing the `com.magicleap.solutions.spatialmeshserializer` package into their existing project.

**Note:** If you are importing the package into an existing project, ensure that the `SpaceManager` and `SpaceImportExport` permissions are declared in your project's manifest.

### Pre-configured Project

1. Clone the repository:
   ```bash
   git clone https://github.com/magicleap/SpatialMeshSerializer.git
   ```
2. Open Unity.
3. In the Unity Hub, select **Open Project**, then choose the cloned repository folder.
4. In the Unity Editor, open `Assets/Spatial Mesh Serializer Sample/Scenes/MeshSample.unity`.
5. Build and run the scene on your Magic Leap 2 device.
6. After the device localizes into a Space, move around to scan the environment.
7. Press the controller’s Trigger to save the mesh.
8. Restart the app and press the Trigger again to load the previously saved mesh.

### Importing the Package into an Existing Project

1. Open your existing Unity project.
2. In Unity, select **Window > Package Manager**.
3. Select the plus (**+**) icon and choose **Add package from git URL**.
4. Paste:
   ```
   https://github.com/magicleap/SpatialMeshSerializer.git?path=/Packages/com.magicleap.solutions.spatialmeshserializer
   ```
5. Select **Add**.
6. In the Package Manager, select **Magic Leap Spatial Mesh Serializer**.
7. Under **Samples**, select **Import** next to **Spatial Mesh Serializer Sample**.
8. Open the sample scene located in:
   ```
   Assets/Samples/Magic Leap Spatial Mesh Serializer/<version>/Spatial Mesh Serializer Sample/
   ```
9. Open `MeshSample.unity`, build, and run the scene on your Magic Leap 2 device.


## API Overview

### MeshLocalizer.cs

`MeshLocalizer` handles Magic Leap Space localization. It ensures that saved and loaded meshes are associated with the correct Space and that they appear accurately in the environment.

#### Key Features

- Initialization: Call `MeshLocalizer.Initialize()` to set up the localization system.
- Event handling: Subscribe to `MeshLocalizer.onLocalizationStatusChanged` to respond to localization changes.
- Space identification: Use `MeshLocalizer.TryGetSpaceUUID()` to retrieve the current Space UUID.

#### Example

```csharp
using UnityEngine;
using MagicLeap.SpatialMeshSerializer;
using UnityEngine.XR.Management;
using UnityEngine.XR;

public class LocalizationExample : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(SetUpLocalization());
    }

    private IEnumerator SetUpLocalization()
    {
        // Wait until all OpenXR subsystems are loaded before initializing localization.
        yield return new WaitUntil(OpenXRSubsystemsAreLoaded);

        // Subscribe to localization status changes.
        MeshLocalizer.onLocalizationStatusChanged += OnLocalizationChanged;

        // Initialize the localizer.
        // This sets up events and attempts to localize into a known Space.
        // After localization, you can associate saved meshes with a unique Space ID.
        MeshLocalizer.Initialize();
    }

    private bool OpenXRSubsystemsAreLoaded()
    {
        if (XRGeneralSettings.Instance == null ||
            XRGeneralSettings.Instance.Manager == null ||
            XRGeneralSettings.Instance.Manager.activeLoader == null)
        {
            return false;
        }
        return XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRAnchorSubsystem>() != null;
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks.
        MeshLocalizer.onLocalizationStatusChanged -= OnLocalizationChanged;
    }

    private void OnLocalizationChanged()
    {
        // Check if we have a known space ID now that the device is localized.
        if (MeshLocalizer.TryGetSpaceUUID(out string spaceId))
        {
            Debug.Log($"Localized into space: {spaceId}. Ready to load or save meshes!");
        }
        else
        {
            Debug.Log("Not currently localized into a known space.");
        }
    }
}
```

### MeshSerializer.cs

`MeshSerializer` provides methods to save and load meshes tied to a localized Space.

#### Key Features

- Saving: Use `SaveCurrentSpaceMeshAsync()` to save all meshes from the `ARMeshManager` to the current Space.
- Loading: Use `LoadCurrentSpaceMeshAndInstantiateAsync()` to load and instantiate saved meshes from the current Space.
- Advanced control: Use methods like `SerializeMeshesAsync()` to access intermediate byte arrays for networking or custom workflows.

#### Examples

##### Save and Load

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using MagicLeap.SpatialMeshSerializer;

public class MeshExample : MonoBehaviour
{
    [SerializeField] private ARMeshManager meshManager;
    [SerializeField] private GameObject meshPrefab;

    private async void SaveMeshes()
    {
        // Save current space meshes to device storage.
        await MeshSerializer.SaveCurrentSpaceMeshAsync(meshManager.meshes);
        Debug.Log("Meshes saved successfully.");
    }

    private async void LoadMeshes()
    {
        // Load saved meshes for the current Space and instantiate them.
        GameObject[] loadedMeshes = await MeshSerializer.LoadCurrentSpaceMeshAndInstantiateAsync(meshPrefab);

        if (loadedMeshes != null && loadedMeshes.Length > 0)
        {
            Debug.Log("Meshes loaded successfully.");
        }
        else
        {
            Debug.Log("No saved meshes found for this space.");
        }
    }
}
```

##### Advanced API

For networking or custom workflows, you can work directly with intermediate byte arrays.

```csharp
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.Collections;
using MagicLeap.SpatialMeshSerializer;

public class AdvancedMeshUsage : MonoBehaviour
{
    public async Task SaveMeshesWithIntermediateArrays(ARMeshManager meshManager)
    {
        if (!MeshLocalizer.TryGetSpaceUUID(out string id))
        {
            Debug.Log("Not localized into a Space.");
            return;
        }

        // Serialize meshes into raw byte arrays.
        NativeArray<byte>[] serializedData = await MeshSerializer.SerializeMeshesAsync(meshManager.meshes);

        // Save serialized data to disk.
        await MeshSerializer.SaveMeshesToFileAsync(id, serializedData);

        // Dispose native arrays to free memory.
        serializedData.DisposeAll();
    }

    public async Task<GameObject[]> LoadMeshesWithIntermediateArrays(ARMeshManager meshManager)
    {
        if (!MeshLocalizer.TryGetSpaceUUID(out string id))
        {
            Debug.Log("Not localized into a Space.");
            return null;
        }

        // Load serialized mesh data from disk.
        MeshSerializer.LoadedMesh[] loadedMeshes = await MeshSerializer.LoadMeshesAsync(id);

        if (!loadedMeshes.IsPopulated())
        {
            Debug.Log("No meshes found for this Space.");
            return null;
        }

        // Instantiate meshes in the scene.
        GameObject[] meshObjects = await MeshSerializer.InstantiateLoadedMeshesAsync(loadedMeshes, meshManager.meshPrefab.gameObject);

        // Dispose of native arrays after use.
        loadedMeshes.DisposeAll();

        return meshObjects;
    }
}
```

## License

This repository is licensed under the Magic Leap Open Source License. See the [LICENSE](LICENSE) file for more information.
