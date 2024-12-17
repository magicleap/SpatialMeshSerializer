using UnityEngine;

public class FollowTransform : MonoBehaviour
{
    [SerializeField] private Transform _followTransform;
    private Transform _transform;

    private void Awake()
    {
        _transform = transform;
    }

    private void LateUpdate()
    {
        _transform.position = _followTransform.position;
    }
}