using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private PlayerAnimator playerAnimator;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private float animationDampTime = 0.08f;
        [SerializeField] private float normalizedWalkSpeed = 25f;
        [SerializeField] private float idleAnimationSpeedThreshold = 1.5f;
        [SerializeField] private float verticalSpeedDampTime = 0.05f;
        [SerializeField] private bool preferStrafeOnDiagonal = true;
        [SerializeField] private float diagonalDetectionThreshold = 0.2f;
        [SerializeField] private float diagonalStrafeMagnitudeMultiplier = 1f;

        public PlayerAnimationState CurrentState { get; private set; }

        private void Awake()
        {
            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }

            if (playerAnimator == null)
            {
                playerAnimator = GetComponent<PlayerAnimator>();
            }
        }

        private void OnEnable()
        {
            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }

            if (playerAnimator == null)
            {
                playerAnimator = GetComponent<PlayerAnimator>();
            }

            if (playerMotor == null)
            {
                return;
            }

            playerMotor.JumpStarted += HandleJumpStarted;
            playerMotor.JumpReleased += HandleJumpReleased;
            playerMotor.Landed += HandleLanded;
        }

        private void OnDisable()
        {
            if (playerMotor == null)
            {
                return;
            }

            playerMotor.JumpStarted -= HandleJumpStarted;
            playerMotor.JumpReleased -= HandleJumpReleased;
            playerMotor.Landed -= HandleLanded;
        }

        private void Update()
        {
            if (playerAnimator == null || playerMotor == null)
            {
                return;
            }

            bool shouldForceIdle = playerMotor.PlanarSpeed <= idleAnimationSpeedThreshold;
            bool isWallRiding = playerMotor.IsWallRiding;
            Vector2 normalizedInputDirection = isWallRiding
                ? BuildWallRideAnimationDirection()
                : BuildInputAnimationDirection(playerMotor.MoveInput);
            float normalizedSpeed = playerMotor.PlanarSpeed / Mathf.Max(normalizedWalkSpeed, 0.01f);
            normalizedSpeed = Mathf.Clamp(normalizedSpeed, 0f, 1.5f);
            Vector2 normalizedLocalVelocity = normalizedInputDirection * normalizedSpeed;
            if (shouldForceIdle)
            {
                normalizedLocalVelocity = Vector2.zero;
            }

            normalizedLocalVelocity = BuildLocomotionAnimationVector(normalizedLocalVelocity);
            bool animationGrounded = playerMotor.IsGrounded || isWallRiding;
            float animationVerticalSpeed = isWallRiding ? Mathf.Max(0f, playerMotor.ActualVerticalSpeed) : playerMotor.ActualVerticalSpeed;
            int animationJumpPhase = isWallRiding
                ? (int)PlayerMotor.JumpPhase.Ascending
                : (int)playerMotor.CurrentJumpPhase;
            var animationState = new PlayerAnimationState(
                shouldForceIdle ? 0f : normalizedLocalVelocity.x,
                shouldForceIdle ? 0f : normalizedLocalVelocity.y,
                shouldForceIdle ? 0f : normalizedLocalVelocity.magnitude,
                playerMotor.IsJumpHoldActive,
                animationGrounded,
                animationVerticalSpeed,
                animationJumpPhase);

            CurrentState = animationState;
            playerAnimator.ApplyState(animationState, animationDampTime, verticalSpeedDampTime);
        }

        private Vector2 BuildInputAnimationDirection(Vector2 moveInput)
        {
            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
            if (moveInput.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            return BuildLocomotionAnimationVector(moveInput.normalized);
        }

        private Vector2 BuildLocomotionAnimationVector(Vector2 normalizedLocalVelocity)
        {
            normalizedLocalVelocity = Vector2.ClampMagnitude(normalizedLocalVelocity, 1.5f);

            if (!preferStrafeOnDiagonal)
            {
                return normalizedLocalVelocity;
            }

            float absX = Mathf.Abs(normalizedLocalVelocity.x);
            float absY = Mathf.Abs(normalizedLocalVelocity.y);
            bool isDiagonal = absX > diagonalDetectionThreshold && absY > diagonalDetectionThreshold;
            if (!isDiagonal)
            {
                return normalizedLocalVelocity;
            }

            float strafeMagnitude = Mathf.Max(absX, absY) * Mathf.Max(diagonalStrafeMagnitudeMultiplier, 0f);
            return new Vector2(Mathf.Sign(normalizedLocalVelocity.x) * strafeMagnitude, 0f);
        }

        private Vector2 BuildWallRideAnimationDirection()
        {
            Vector3 facingForward = playerMotor.FacingForward;
            if (facingForward.sqrMagnitude <= 0.0001f)
            {
                return Vector2.up;
            }

            Vector3 right = Vector3.Cross(Vector3.up, facingForward).normalized;
            Vector3 wallNormal = playerMotor.WallNormal;
            float sideAlignment = Vector3.Dot(right, wallNormal);
            float forwardAlignment = -Vector3.Dot(facingForward, wallNormal);

            if (Mathf.Abs(sideAlignment) > Mathf.Abs(forwardAlignment))
            {
                return new Vector2(-Mathf.Sign(sideAlignment), 0f);
            }

            return Vector2.up;
        }

        private void HandleJumpStarted()
        {
            if (playerAnimator == null)
            {
                return;
            }

            playerAnimator.TriggerJumpStart();
        }

        private void HandleJumpReleased()
        {
            if (playerAnimator == null)
            {
                return;
            }

            playerAnimator.TriggerJumpRelease();
        }

        private void HandleLanded()
        {
            if (playerAnimator == null)
            {
                return;
            }

            playerAnimator.TriggerLand();
        }
    }
}
