using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class FollowAimCamera : MonoBehaviour
    {
        private const float HorizontalLookAheadSharpness = 10f;

        [SerializeField] private Transform target;
        [SerializeField] private PlayerInputReader targetInput;
        [SerializeField] private PlayerMotor targetMotor;
        [SerializeField] private float minimumCameraDistance = 6f;
        [SerializeField] private float maximumCameraDistance = 10.5f;
        [SerializeField] private float yawSensitivity = 0.12f;
        [SerializeField] private float pitchSensitivity = 0.08f;
        [SerializeField] private float minPitch = -10f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float followSharpness = 12f;
        [SerializeField] private float baseFieldOfView = 60f;
        [SerializeField] private float maxFieldOfView = 82f;
        [SerializeField] private float speedForMaxFieldOfView = 95f;
        [SerializeField] private float fieldOfViewSharpness = 6f;
        [SerializeField] private float speedLookAhead = 3.5f;
        [SerializeField] private float verticalLookAhead = 0.08f;

        private float yawDegrees;
        private float pitchDegrees = 30f;
        private Vector3 horizontalLookAheadOffset;
        private Camera targetCamera;

        private void Awake()
        {
            if (targetInput == null && target != null)
            {
                targetInput = target.GetComponentInParent<PlayerInputReader>();
            }

            if (targetMotor == null && target != null)
            {
                targetMotor = target.GetComponentInParent<PlayerMotor>();
            }

            targetCamera = GetComponent<Camera>();
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseFieldOfView;
            }

            Vector3 currentEuler = transform.rotation.eulerAngles;
            yawDegrees = currentEuler.y;
            pitchDegrees = NormalizePitch(currentEuler.x);
        }

        private void OnDisable()
        {
            horizontalLookAheadOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (target == null || targetInput == null)
            {
                return;
            }

            Vector2 lookInput = targetInput.LookInput;
            yawDegrees += lookInput.x * yawSensitivity;
            pitchDegrees = Mathf.Clamp(pitchDegrees - lookInput.y * pitchSensitivity, minPitch, maxPitch);

            Quaternion orbitRotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Vector3 focusPoint = GetFocusPoint();
            float speedT = GetSpeedNormalized();
            float currentDistance = Mathf.Lerp(minimumCameraDistance, maximumCameraDistance, speedT);
            Vector3 desiredPosition = focusPoint + orbitRotation * (Vector3.back * currentDistance);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

            Vector3 lookDirection = (focusPoint - transform.position).normalized;
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            transform.rotation = desiredRotation;

            UpdateFieldOfView(speedT);
        }

        private static float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            return pitch;
        }

        private Vector3 GetFocusPoint()
        {
            Vector3 focusPoint = target.position;
            if (targetMotor == null)
            {
                return focusPoint;
            }

            horizontalLookAheadOffset = Vector3.Lerp(
                horizontalLookAheadOffset,
                GetDesiredHorizontalLookAhead(),
                1f - Mathf.Exp(-HorizontalLookAheadSharpness * Time.deltaTime));
            focusPoint += horizontalLookAheadOffset;

            focusPoint += Vector3.up * (targetMotor.VerticalVelocity * verticalLookAhead);
            return focusPoint;
        }

        private float GetSpeedNormalized()
        {
            if (targetMotor == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(targetMotor.PlanarSpeed / Mathf.Max(0.01f, speedForMaxFieldOfView));
        }

        private Vector3 GetDesiredHorizontalLookAhead()
        {
            if (targetInput == null || targetMotor == null)
            {
                return Vector3.zero;
            }

            Vector2 moveInput = Vector2.ClampMagnitude(targetInput.MoveInput, 1f);
            if (moveInput.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 forward = Vector3.ProjectOnPlane(targetMotor.FacingForward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 desiredDirection = right * moveInput.x + forward * moveInput.y;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return desiredDirection.normalized * (speedLookAhead * GetSpeedNormalized());
        }

        private void UpdateFieldOfView(float speedT)
        {
            if (targetCamera == null)
            {
                return;
            }

            float targetFieldOfView = Mathf.Lerp(baseFieldOfView, maxFieldOfView, speedT);
            targetCamera.fieldOfView = Mathf.Lerp(
                targetCamera.fieldOfView,
                targetFieldOfView,
                1f - Mathf.Exp(-fieldOfViewSharpness * Time.deltaTime));
        }

    }
}
