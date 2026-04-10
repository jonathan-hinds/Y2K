using Unity.Cinemachine;
using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public sealed class PlayerCameraSpeedEffects : MonoBehaviour
    {
        [SerializeField] private PlayerMotor targetMotor;

        [Header("Distance")]
        [Tooltip("Camera distance at low speed.")]
        [SerializeField] private float minimumCameraDistance = 6.4f;
        [Tooltip("Camera distance at high speed.")]
        [SerializeField] private float maximumCameraDistance = 9f;
        [Tooltip("How quickly the camera slides between min and max distance.")]
        [SerializeField] private float distanceSharpness = 5f;

        [Header("Field Of View")]
        [Tooltip("Camera FOV at low speed.")]
        [SerializeField] private float baseFieldOfView = 60f;
        [Tooltip("Camera FOV at high speed.")]
        [SerializeField] private float maxFieldOfView = 80f;
        [Tooltip("How quickly the FOV shifts between min and max values.")]
        [SerializeField] private float fieldOfViewSharpness = 5f;
        [Tooltip("Player speed that maps to the maximum distance and FOV values.")]
        [SerializeField] private float speedForMaxEffects = 95f;

        private CinemachineCamera virtualCamera;
        private CinemachineOrbitalFollow orbitalFollow;

        private void Reset()
        {
            targetMotor = FindFirstObjectByType<PlayerMotor>();
        }

        private void Awake()
        {
            virtualCamera = GetComponent<CinemachineCamera>();
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            if (targetMotor == null)
            {
                targetMotor = FindFirstObjectByType<PlayerMotor>();
            }
        }

        private void LateUpdate()
        {
            if (targetMotor == null || virtualCamera == null || orbitalFollow == null)
            {
                return;
            }

            float speedT = Mathf.Clamp01(targetMotor.PlanarSpeed / Mathf.Max(0.01f, speedForMaxEffects));
            float distanceBlend = 1f - Mathf.Exp(-distanceSharpness * Time.deltaTime);
            float fovBlend = 1f - Mathf.Exp(-fieldOfViewSharpness * Time.deltaTime);

            orbitalFollow.Radius = Mathf.Lerp(
                orbitalFollow.Radius,
                Mathf.Lerp(minimumCameraDistance, maximumCameraDistance, speedT),
                distanceBlend);

            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(
                lens.FieldOfView,
                Mathf.Lerp(baseFieldOfView, maxFieldOfView, speedT),
                fovBlend);
            virtualCamera.Lens = lens;
        }

        public void SetTargetMotor(PlayerMotor motor)
        {
            targetMotor = motor;
        }

        public void ApplyImmediate()
        {
            if (targetMotor == null || virtualCamera == null || orbitalFollow == null)
            {
                return;
            }

            float speedT = Mathf.Clamp01(targetMotor.PlanarSpeed / Mathf.Max(0.01f, speedForMaxEffects));
            orbitalFollow.Radius = Mathf.Lerp(minimumCameraDistance, maximumCameraDistance, speedT);

            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(baseFieldOfView, maxFieldOfView, speedT);
            virtualCamera.Lens = lens;
        }
    }
}
