using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHeadlightAim : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector3 rotationOffsetEuler;

        private void Awake()
        {
            ResolveCamera();
        }

        private void LateUpdate()
        {
            ResolveCamera();
            if (targetCamera == null)
            {
                return;
            }

            transform.rotation = targetCamera.transform.rotation * Quaternion.Euler(rotationOffsetEuler);
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }
    }
}
