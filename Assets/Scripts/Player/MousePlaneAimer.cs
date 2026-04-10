using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class MousePlaneAimer : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Vector3 planeNormal = Vector3.up;
        [SerializeField] private float lookAheadDistance = 2f;

        public Vector3 AimPoint { get; private set; }
        public Vector3 AimForward { get; private set; } = Vector3.forward;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (aimOrigin == null)
            {
                aimOrigin = transform;
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                return;
            }

            Vector3 flattenedDirection = Vector3.ProjectOnPlane(targetCamera.transform.forward, planeNormal);
            if (flattenedDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            AimForward = flattenedDirection.normalized;
            AimPoint = aimOrigin.position + AimForward * lookAheadDistance;
        }
    }
}
