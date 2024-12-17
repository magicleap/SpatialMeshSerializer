using MagicLeap.Android;
using MagicLeap.SpatialMeshSerializer;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

public class MeshingManager : MonoBehaviour
{
    public enum State
    {
        Disabled = 0,
        ReuseMeshQuestion = 1,
        Meshing = 2
    }

    public readonly NotifyingProperty<State> meshingState = new(State.Disabled);

    [SerializeField] private ARMeshManager _meshManager;

    private GameObject[] _loadedMeshes = new GameObject[0];

    private PropertyBinding<AppState.State> _appStateBinding;
    private PropertyBinding<State> _meshingStateBinding;

    private void Awake()
    {
        _meshManager.enabled = false;
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(OpenXRSubsystemsAreLoaded);

        _appStateBinding = new(AppState.state, OnAppStateChanged);
        _meshingStateBinding = new(meshingState, OnMeshingStateChanged);
        
        // We Wait a frame before requesting Spatial Mapping Permission in case other permissions are being requested.
        // Only one Permission request can be made at a time. Otherwise, the request will be ignored.
        yield return null;
        
        if (!Permissions.CheckPermission(Permissions.SpatialMapping))
        {
            Permissions.RequestPermission(Permissions.SpatialMapping,OnPermissionGranted,OnPermissionDenied,OnPermissionDenied);
        }
    }

    private void OnPermissionGranted(string permission)
    {
        MeshLocalizer.onLocalizationStatusChanged += OnLocalized;
        MeshLocalizer.Initialize();
    }
    
    private void OnPermissionDenied(string permission)
    {
       Debug.LogError("Mesh Manager Not Initialized. Permission denied: " + permission);
    }

    private void OnAppStateChanged(AppState.State state, AppState.State prev)
    {
        if (state == AppState.State.Meshing)
            _ = OnEnterMeshingState();
        else
            meshingState.Set(State.Disabled);
    }

    private async Task OnEnterMeshingState()
    {
        var savedMeshes = await MeshSerializer.LoadCurrentSpaceMeshAndInstantiateAsync(_meshManager.meshPrefab.gameObject);

        //If we have no saved meshes, start the meshing process.
        if (!savedMeshes.IsPopulated())
        {
            Debug.Log("No saved meshes for this space yet. Enabling ARMeshManager to mesh space.");
            _meshManager.enabled = true;
            meshingState.Set(State.Meshing);
            return;
        }

        _loadedMeshes = savedMeshes;
        meshingState.Set(State.ReuseMeshQuestion);
    }

    private void OnMeshingStateChanged(State state, State prev)
    {
        _meshManager.enabled = state == State.Meshing;
        InputHandler.input.Controller.Trigger.performed -= OnTrigger;
        InputHandler.input.Controller.Bumper.performed -= OnBumper;

        switch (state)
        {
            case State.ReuseMeshQuestion:
                InputHandler.input.Controller.Trigger.performed += OnTrigger;
                InputHandler.input.Controller.Bumper.performed += OnBumper;
                break;
            case State.Meshing:
                InputHandler.input.Controller.Trigger.performed += OnTrigger;

                // If the user chooses to mesh even though they already have saved meshes, disabled the saved meshes.
                for (int i = 0; i < _loadedMeshes.Length; i++)
                {
                    _loadedMeshes[i].SetActive(false);
                }
                break;
            case State.Disabled:
                break;
        }
    }

    private void OnTrigger(InputAction.CallbackContext obj)
    {
        if (meshingState.value == State.Meshing)
            FinishMeshing();

        AppState.state.Set(AppState.State.Gameplay);
    }

    private void OnBumper(InputAction.CallbackContext obj)
    {
        if (meshingState.value == State.ReuseMeshQuestion)
            meshingState.Set(State.Meshing);
    }

    private void FinishMeshing()
    {
        _ = MeshSerializer.SaveCurrentSpaceMeshAsync(_meshManager.meshes);
    }

    private void OnLocalized()
    {
        AppState.state.Set(AppState.State.Meshing);
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
        _appStateBinding.Dispose();
        _meshingStateBinding.Dispose();
    }
}