using UnityEngine;

public class MeshVisualizer : MonoBehaviour
{
    public enum MaterialType
    {
        Occlusion = 0,
        Visible = 1
    }

    [Tooltip("Mesh Renderer to apply the materials to.")]
    [SerializeField] private MeshRenderer _meshRenderer;
    [Tooltip("Materials to apply to this MeshRenderer depending on the AppState (Index 0 should be Occlusion, 1 is Visible).")]
    [SerializeField] private Material[] _materials;
    [Tooltip("Which App States the mesh should be visible in.")]
    [SerializeField] private AppState.State _statesToBeVisibleIn = AppState.State.Meshing | AppState.State.Gameplay;

    private PropertyBinding<AppState.State> _appStateBinding;

    private void Start()
    {
        _appStateBinding = new(AppState.state, OnGameStateChanged, (state) => SetMaterialByState(state));
    }

    private void OnGameStateChanged(AppState.State state, AppState.State prev)
    {
        SetMaterialByState(state);
    }

    private void SetMaterialByState(AppState.State state)
    {
        var materialType = (state & _statesToBeVisibleIn) != 0 ? MaterialType.Visible : MaterialType.Occlusion;

        _meshRenderer.sharedMaterial = _materials[(int)materialType];
    }

    private void OnDestroy()
    {
        _appStateBinding.Dispose();
    }
}