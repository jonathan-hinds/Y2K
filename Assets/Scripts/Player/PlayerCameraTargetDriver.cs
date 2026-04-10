using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerCameraTargetDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerRig playerRig;
        [SerializeField] private PlayerMotor targetMotor;

        [Header("Framing")]
        [Tooltip("Base height of the follow target above the player root. Increase this to raise the camera framing.")]
        [SerializeField] private float targetHeight = 1.05f;
        [Tooltip("How far ahead of the player the follow target sits at low speed. Increase this to push the framing forward.")]
        [SerializeField] private float idleForwardOffset = 0.2f;
        [Tooltip("Additional forward offset added at max speed. The total forward framing at max speed is idleForwardOffset + speedLookAheadDistance.")]
        [SerializeField] private float speedLookAheadDistance = 1.2f;
        [Tooltip("How much the follow target can drift sideways while carving or turning at speed.")]
        [SerializeField] private float lateralDriftDistance = 0.45f;
        [Tooltip("How much vertical velocity influences the follow target height.")]
        [SerializeField] private float verticalVelocityLookAhead = 0.05f;
        [Tooltip("Extra lift applied to the follow target while rising in jumps.")]
        [SerializeField] private float jumpLift = 0.08f;
        [Tooltip("Player speed that maps to full forward/drift framing offsets.")]
        [SerializeField] private float speedForMaxOffsets = 90f;

        [Header("Follow Smoothing")]
        [SerializeField] private float groundedPositionSharpness = 16f;
        [SerializeField] private float airbornePositionSharpness = 10f;

        [Header("Drift")]
        [SerializeField] private float minSpeedForVelocityHeading = 8f;
        [SerializeField] private float maxVelocityHeadingAngle = 30f;

        private Transform cameraTarget;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            ResolveCameraTarget();
        }

        private void LateUpdate()
        {
            if (targetMotor == null || cameraTarget == null)
            {
                return;
            }

            Vector3 desiredForward = GetDesiredHeading();
            Vector3 desiredPosition = GetDesiredPosition(desiredForward);
            float sharpness = targetMotor.IsGrounded ? groundedPositionSharpness : airbornePositionSharpness;
            float positionBlend = 1f - Mathf.Exp(-sharpness * Time.deltaTime);

            cameraTarget.position = Vector3.Lerp(cameraTarget.position, desiredPosition, positionBlend);
        }

        private void CacheReferences()
        {
            if (playerRig == null)
            {
                playerRig = GetComponent<PlayerRig>();
            }

            if (targetMotor == null)
            {
                targetMotor = GetComponent<PlayerMotor>();
            }
        }

        private void ResolveCameraTarget()
        {
            cameraTarget = playerRig != null ? playerRig.CameraTarget : null;
        }

        private Vector3 GetDesiredHeading()
        {
            Vector3 facingForward = ProjectPlanar(targetMotor.FacingForward, Vector3.forward);
            Vector3 planarVelocity = ProjectPlanar(targetMotor.WorldVelocity, facingForward);
            float planarSpeed = planarVelocity.magnitude;
            if (planarSpeed < minSpeedForVelocityHeading)
            {
                return facingForward;
            }

            Vector3 velocityForward = planarVelocity / planarSpeed;
            float signedAngle = Vector3.SignedAngle(facingForward, velocityForward, Vector3.up);
            float clampedAngle = Mathf.Clamp(signedAngle, -maxVelocityHeadingAngle, maxVelocityHeadingAngle);
            float speedT = Mathf.Clamp01(planarSpeed / Mathf.Max(0.01f, speedForMaxOffsets));
            return Quaternion.AngleAxis(clampedAngle * speedT, Vector3.up) * facingForward;
        }

        private Vector3 GetDesiredPosition(Vector3 desiredForward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, desiredForward).normalized;
            Vector3 planarVelocity = ProjectPlanar(targetMotor.WorldVelocity, desiredForward);
            float speedT = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, speedForMaxOffsets));
            float lateralDrift = Mathf.Clamp(targetMotor.LocalVelocity.x / Mathf.Max(1f, speedForMaxOffsets), -1f, 1f)
                * lateralDriftDistance
                * speedT;

            float verticalOffset = targetHeight
                + Mathf.Max(0f, targetMotor.ActualVerticalSpeed) * jumpLift
                + targetMotor.VerticalVelocity * verticalVelocityLookAhead;
            Vector3 forwardOffset = desiredForward * (idleForwardOffset + speedLookAheadDistance * speedT);
            Vector3 sideOffset = right * lateralDrift;
            return transform.position + Vector3.up * verticalOffset + forwardOffset + sideOffset;
        }

        private static Vector3 ProjectPlanar(Vector3 vector, Vector3 fallback)
        {
            Vector3 planar = Vector3.ProjectOnPlane(vector, Vector3.up);
            if (planar.sqrMagnitude > 0.0001f)
            {
                return planar;
            }

            Vector3 fallbackPlanar = Vector3.ProjectOnPlane(fallback, Vector3.up);
            return fallbackPlanar.sqrMagnitude > 0.0001f ? fallbackPlanar.normalized : Vector3.forward;
        }
    }
}
