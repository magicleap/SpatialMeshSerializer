using TMPro;
using UnityEngine;

public class MeshingMessage : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private MeshingManager _meshingManager;

    private PropertyBinding<MeshingManager.State> _stateBinding;

    private void Start()
    {
        _stateBinding = new(_meshingManager.meshingState, OnStateChanged, OnStateChanged);
    }

    private void OnStateChanged(MeshingManager.State state, MeshingManager.State prev)
    {
        switch (state)
        {
            case MeshingManager.State.Disabled:
                _messageText.text = "";
                break;
            case MeshingManager.State.ReuseMeshQuestion:
                _messageText.text = "Existing mesh discovered for your Space.\nPress trigger to use it or bumper to remesh.";
                break;
            case MeshingManager.State.Meshing:
                _messageText.text = "Move around your space to scan the play area.\nPress controller trigger when finished.";
                break;
        }
    }

    private void OnDestroy()
    {
        _stateBinding.Dispose();
    }
}