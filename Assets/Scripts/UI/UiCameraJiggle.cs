using UnityEngine;

namespace Race.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiCameraJiggle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private Camera sourceCamera;

        [Header("Translation Response")]
        [SerializeField, Min(0f)] private float yawToHorizontalOffset = 0.045f;
        [SerializeField, Min(0f)] private float pitchToVerticalOffset = 0.022f;
        [SerializeField, Min(0f)] private float lateralVelocityToHorizontalOffset = 0.03f;
        [SerializeField, Min(0f)] private float verticalVelocityToVerticalOffset = 0.014f;
        [SerializeField, Min(0f)] private float maxOffset = 12f;

        [Header("Smoothing")]
        [SerializeField, Min(0.01f)] private float offsetSharpness = 4.5f;
        [SerializeField, Min(0f)] private float bounceStrength = 14f;
        [SerializeField, Min(0f)] private float bounceDamping = 8f;
        [SerializeField, Min(0f)] private float motionImpulseStrength = 0.22f;
        [SerializeField] private bool useUnscaledTime = true;

        private Vector2 baseAnchoredPosition;
        private Vector2 currentOffset;
        private Vector2 currentOffsetVelocity;
        private Vector2 previousTargetOffset;
        private Vector3 previousCameraPosition;
        private Quaternion previousCameraRotation;
        private bool hasPreviousCameraState;

        private void Reset()
        {
            targetRect = GetComponent<RectTransform>();
        }

        private void Awake()
        {
            CacheBaseState();
            ResolveReferences();
        }

        private void OnEnable()
        {
            CacheBaseState();
            ResolveReferences();
            ResetTracking();
            ApplyPose();
        }

        private void OnDisable()
        {
            currentOffset = Vector2.zero;
            currentOffsetVelocity = Vector2.zero;
            hasPreviousCameraState = false;
            ApplyPose();
        }

        private void LateUpdate()
        {
            if (targetRect == null)
            {
                return;
            }

            ResolveReferences();
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (sourceCamera == null || deltaTime <= Mathf.Epsilon)
            {
                UpdatePose(Vector2.zero, Mathf.Max(deltaTime, 0f));
                ApplyPose();
                return;
            }

            if (!hasPreviousCameraState)
            {
                previousCameraPosition = sourceCamera.transform.position;
                previousCameraRotation = sourceCamera.transform.rotation;
                hasPreviousCameraState = true;
                ApplyPose();
                return;
            }

            Vector3 worldVelocity = (sourceCamera.transform.position - previousCameraPosition) / deltaTime;
            Quaternion deltaRotation = sourceCamera.transform.rotation * Quaternion.Inverse(previousCameraRotation);

            previousCameraPosition = sourceCamera.transform.position;
            previousCameraRotation = sourceCamera.transform.rotation;

            Vector3 localVelocity = sourceCamera.transform.InverseTransformDirection(worldVelocity);
            Vector3 angularVelocity = GetAngularVelocity(deltaRotation, deltaTime);

            Vector2 targetOffset = new Vector2(
                (-angularVelocity.y * yawToHorizontalOffset) + (-localVelocity.x * lateralVelocityToHorizontalOffset),
                (angularVelocity.x * pitchToVerticalOffset) + (-localVelocity.y * verticalVelocityToVerticalOffset));
            if (targetOffset.sqrMagnitude > maxOffset * maxOffset)
            {
                targetOffset = targetOffset.normalized * maxOffset;
            }

            UpdatePose(targetOffset, deltaTime);
            ApplyPose();
        }

        private void UpdatePose(Vector2 targetOffset, float deltaTime)
        {
            float followBlend = 1f - Mathf.Exp(-offsetSharpness * deltaTime);
            Vector2 drivenTarget = Vector2.Lerp(targetOffset * 0.65f, targetOffset, followBlend);
            Vector2 targetVelocity = deltaTime > Mathf.Epsilon
                ? (drivenTarget - previousTargetOffset) / deltaTime
                : Vector2.zero;
            Vector2 springForce = (drivenTarget - currentOffset) * bounceStrength;
            Vector2 dampingForce = currentOffsetVelocity * bounceDamping;
            Vector2 motionImpulse = targetVelocity * motionImpulseStrength;

            currentOffsetVelocity += ((springForce - dampingForce) * deltaTime) + (motionImpulse * deltaTime);
            currentOffset += currentOffsetVelocity * deltaTime;
            previousTargetOffset = drivenTarget;

            // Keep tiny sub-pixel velocities from damping the motion into a dead stop too early.
            if (currentOffsetVelocity.sqrMagnitude < 0.0001f && (drivenTarget - currentOffset).sqrMagnitude < 0.0001f)
            {
                currentOffsetVelocity = Vector2.zero;
                currentOffset = drivenTarget;
            }

            if (currentOffset.sqrMagnitude > maxOffset * maxOffset)
            {
                currentOffset = currentOffset.normalized * maxOffset;
                currentOffsetVelocity = Vector2.zero;
            }
        }

        private void CacheBaseState()
        {
            if (targetRect == null)
            {
                targetRect = GetComponent<RectTransform>();
            }

            if (targetRect == null)
            {
                return;
            }

            baseAnchoredPosition = targetRect.anchoredPosition;
        }

        private void ResolveReferences()
        {
            if (targetRect == null)
            {
                targetRect = GetComponent<RectTransform>();
            }

            if (sourceCamera == null)
            {
                sourceCamera = Camera.main;
            }
        }

        private void ResetTracking()
        {
            currentOffset = Vector2.zero;
            currentOffsetVelocity = Vector2.zero;
            previousTargetOffset = Vector2.zero;

            if (sourceCamera != null)
            {
                previousCameraPosition = sourceCamera.transform.position;
                previousCameraRotation = sourceCamera.transform.rotation;
                hasPreviousCameraState = true;
            }
            else
            {
                hasPreviousCameraState = false;
            }
        }

        private void ApplyPose()
        {
            if (targetRect == null)
            {
                return;
            }

            targetRect.anchoredPosition = baseAnchoredPosition + currentOffset;
            targetRect.localRotation = Quaternion.identity;
        }

        private static Vector3 GetAngularVelocity(Quaternion deltaRotation, float deltaTime)
        {
            Vector3 deltaEuler = deltaRotation.eulerAngles;
            return new Vector3(
                NormalizeAngle(deltaEuler.x) / deltaTime,
                NormalizeAngle(deltaEuler.y) / deltaTime,
                NormalizeAngle(deltaEuler.z) / deltaTime);
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
            {
                angle -= 360f;
            }

            return angle;
        }
    }
}
