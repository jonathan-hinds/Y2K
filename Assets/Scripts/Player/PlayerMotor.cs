using UnityEngine;
using Race.Grinding;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(MousePlaneAimer))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private struct WallContact
        {
            public bool IsValid;
            public Collider Collider;
            public Vector3 Normal;
            public Vector3 Point;
            public float Score;
        }

        private struct GrindContact
        {
            public bool IsValid;
            public GrindRail Rail;
            public GrindRail.Sample Sample;
            public float Score;
        }

        public enum JumpPhase
        {
            Grounded = 0,
            JumpStart = 1,
            JumpHold = 2,
            Ascending = 3,
            Descending = 4,
            Landing = 5
        }

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 25f;
        [SerializeField] private float sprintSpeed = 75f;
        [SerializeField] private float acceleration = 1f;
        [SerializeField] private float deceleration = 1f;
        [SerializeField, Range(0f, 1f)] private float airControlPercent = 0.9f;
        [SerializeField] private float rotationSharpness = 20f;
        [SerializeField] private float gravity = -30f;
        [SerializeField] private float groundedVerticalVelocity = -2f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 10f;
        [SerializeField] private float wallJumpHeight = 10f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpPreparationUngroundedTolerance = 0.08f;
        [SerializeField] private float ascendingVelocityThreshold = 0.05f;

        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private PlayerMovementProfile movementProfile;
        [SerializeField] private PlayerWallRideProbe wallRideProbe;
        [SerializeField] private PlayerGrindProbe grindProbe;

        [Header("Grounding")]
        [SerializeField] private float groundProbeDistance = 0.75f;
        [SerializeField] private LayerMask groundProbeMask = ~0;
        [SerializeField, Min(0f)] private float groundContactGraceTime = 0.1f;
        [SerializeField] private float idlePlanarSnapSpeed = 1.25f;
        [SerializeField, Range(0f, 89f)] private float idleSnapSlopeAngle = 2f;
        [SerializeField] private float surfaceGravityScale = 1f;
        [SerializeField, Range(0f, 89f)] private float slideStartAngle = 3f;
        [SerializeField] private float slideAcceleration = 18f;
        [SerializeField] private float steepSlideAcceleration = 34f;

        [Header("Wall Ride")]
        [SerializeField] private LayerMask wallRideMask = ~0;
        [SerializeField, Range(0f, 89f)] private float wallRideMinSurfaceAngle = 75f;
        [SerializeField, Range(90f, 170f)] private float wallRideMaxSurfaceAngle = 110f;
        [SerializeField] private float wallRideBrakeDeceleration = 1.5f;
        [SerializeField] private float wallRideVerticalBrakeDeceleration = 4f;
        [SerializeField] private float wallRideContactOffset = 0.08f;
        [SerializeField] private float wallRideProbeDistance = 0.2f;
        [SerializeField, Min(0f)] private float wallRideEntryUpwardBoost = 12f;
        [SerializeField, Min(0f)] private float traversalActivationWindow = 0.2f;

        [Header("Grinding")]
        [SerializeField] private LayerMask grindMask = ~0;
        [SerializeField, Min(0f)] private float grindProbeDistance = 0.2f;
        [SerializeField, Min(0f)] private float grindJumpHeight = 10f;
        [SerializeField, Min(0f)] private float grindEntrySpeedBoost = 10f;
        [SerializeField, Min(0f)] private float grindGravityScale = 1f;
        [SerializeField, Min(0f)] private float grindSpeedDrag = 1f;
        [SerializeField, Min(0f)] private float grindMagnetism = 16f;
        [SerializeField, Min(0f)] private float grindDetachDistance = 0.45f;
        [SerializeField, Min(0f)] private float grindAirborneReattachTime = 0.35f;
        [SerializeField, Min(0f)] private float grindAirborneReattachDistance = 0.5f;
        [SerializeField, Range(0f, 1f)] private float grindAirControlDetachThreshold = 0.35f;

        [Header("Grind Balance")]
        [SerializeField] private bool grindBalanceEnabled = true;
        [SerializeField, Range(0.05f, 0.45f)] private float grindBalanceSafeZone = 0.2f;
        [SerializeField, Min(0.1f)] private float grindBalanceControlAcceleration = 2.1f;
        [SerializeField, Min(0.1f)] private float grindBalanceControlDeceleration = 1.15f;
        [SerializeField, Min(0f)] private float grindBalanceControlTopSpeed = 2.6f;
        [SerializeField, Range(0.1f, 1f)] private float grindBalanceCenterControlMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float grindBalanceBaseDrift = 0.58f;
        [SerializeField, Min(0f)] private float grindBalanceSpeedDriftMultiplier = 0.85f;
        [SerializeField, Min(0f)] private float grindBalanceCurvatureInfluence = 9f;
        [SerializeField, Min(0.1f)] private float grindBalanceDriftSharpness = 4.5f;
        [SerializeField] private Vector2 grindBalanceDriftRetargetTime = new(0.45f, 1.05f);
        [SerializeField, Min(0.25f)] private float grindBalanceFailureThreshold = 1f;
        [SerializeField, Min(0f)] private float grindBalanceFailureLockoutDuration = 2.5f;
        [SerializeField, Min(0f)] private float grindBalanceFailureLaunchHorizontalSpeed = 18f;
        [SerializeField, Min(0f)] private float grindBalanceFailureLaunchVerticalSpeed = 12f;
        [SerializeField, Range(0f, 1f)] private float grindBalanceFailureLaunchTangentJitter = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool enableWallRideDebugLogs;

        private CharacterController characterController;
        private MousePlaneAimer aimer;
        private IPlayerLocomotionInput input;
        private PlayerRig playerRig;
        private PlayerSlopeAlignment slopeAlignment;
        private Vector3 planarVelocity;
        private Vector3 facingForward = Vector3.forward;
        private Vector3 groundNormal = Vector3.up;
        private Vector3 stableGroundNormal = Vector3.up;
        private Vector3 previousPosition;
        private float actualVerticalSpeed;
        private float verticalVelocity;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private float jumpPreparationTimer;
        private float jumpPreparationUngroundedTimer;
        private float lastGroundContactTime = float.NegativeInfinity;
        private bool jumpConsumed;
        private bool jumpPreparing;
        private bool jumpStartedFromWallRide;
        private bool jumpStartedFromGrind;
        private bool isWallRiding;
        private bool isGrinding;
        private bool isGrindingAttached;
        private JumpPhase jumpPhase;
        private Collider activeWallCollider;
        private Vector3 wallNormal = Vector3.zero;
        private WallContact pendingWallContact;
        private GrindContact pendingGrindContact;
        private GrindRail activeGrindRail;
        private float activeGrindT;
        private float grindSignedSpeed;
        private float grindAirborneTimer;
        private Vector3 grindTangent = Vector3.forward;
        private Vector3 grindUp = Vector3.up;
        private Vector3 previousGrindProbeCenter;
        private Vector3 lastGrindProbeTravelDelta;
        private bool hasPreviousGrindProbeCenter;
        private float grindBalanceOffset;
        private float grindBalanceDriftVelocity;
        private float grindBalanceDriftTarget;
        private float grindBalanceControlVelocity;
        private float grindBalanceRetargetTimer;
        private float grindFailureLockoutTimer;
        private bool gameplayMovementLocked;
        private readonly Collider[] grindProbeResults = new Collider[64];
        private readonly TimedTraversalInputGate wallRideInputGate = new();

        public Vector3 WorldVelocity => new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z);
        public float PlanarSpeed => planarVelocity.magnitude;
        public float ConfiguredMaxPlanarSpeed => sprintSpeed;
        public Vector2 MoveInput { get; private set; }
        public Vector2 LocalVelocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsJumpHeld => input != null && input.JumpHeld;
        public bool IsJumpHoldActive => jumpPreparing;
        public float VerticalVelocity => verticalVelocity;
        public float ActualVerticalSpeed => actualVerticalSpeed;
        public JumpPhase CurrentJumpPhase => jumpPhase;
        public Vector3 GroundNormal => groundNormal;
        public Vector3 StableGroundNormal => HasStableGroundContact ? stableGroundNormal : Vector3.up;
        public bool HasStableGroundContact => IsGrounded || (Time.time - lastGroundContactTime) <= groundContactGraceTime;
        public Vector3 FacingForward => facingForward;
        public float CurrentSlopeAngle { get; private set; }
        public bool IsSlopeSliding { get; private set; }
        public bool IsWallRiding => isWallRiding;
        public bool IsGrinding => isGrinding;
        public bool GameplayMovementLocked => gameplayMovementLocked;
        public bool CanBeginGroundedInteraction => IsGrounded && !isWallRiding && !isGrinding && !jumpPreparing && jumpPhase == JumpPhase.Grounded;
        public bool GrindBalanceEnabled => grindBalanceEnabled;
        public float GrindBalanceNormalized => grindBalanceFailureThreshold <= Mathf.Epsilon
            ? 0f
            : Mathf.Clamp(grindBalanceOffset / grindBalanceFailureThreshold, -1f, 1f);
        public float GrindBalanceSafeZoneNormalized => grindBalanceFailureThreshold <= Mathf.Epsilon
            ? 0f
            : Mathf.Clamp01(grindBalanceSafeZone / grindBalanceFailureThreshold);
        public Vector3 WallNormal => wallNormal;

        public event System.Action JumpStarted;
        public event System.Action JumpReleased;
        public event System.Action Landed;

        public void SnapToPose(Vector3 position, Quaternion rotation)
        {
            bool restoreCharacterController = characterController != null && characterController.enabled;
            if (restoreCharacterController)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (restoreCharacterController)
            {
                characterController.enabled = true;
            }

            facingForward = GetPlanarDirectionOrFallback(Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up), GetInitialFacingForward());
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            actualVerticalSpeed = 0f;
            previousPosition = position;
            MoveInput = Vector2.zero;
            LocalVelocity = Vector2.zero;
            IsGrounded = false;
            IsSlopeSliding = false;
            isWallRiding = false;
            jumpConsumed = false;
            jumpPreparing = false;
            jumpStartedFromWallRide = false;
            jumpStartedFromGrind = false;
            jumpBufferTimer = 0f;
            jumpPreparationTimer = 0f;
            jumpPreparationUngroundedTimer = 0f;
            activeWallCollider = null;
            wallNormal = Vector3.zero;
            pendingWallContact = default;
            pendingGrindContact = default;
            activeGrindRail = null;
            activeGrindT = 0f;
            grindSignedSpeed = 0f;
            gameplayMovementLocked = false;
            grindAirborneTimer = 0f;
            isGrinding = false;
            isGrindingAttached = false;
            grindTangent = Vector3.forward;
            grindUp = Vector3.up;
            lastGrindProbeTravelDelta = Vector3.zero;
            hasPreviousGrindProbeCenter = false;
            ResetGrindBalanceState(false);
            wallRideInputGate.Reset();

            if (visualRoot != null && (slopeAlignment == null || !slopeAlignment.IsAlignmentActive))
            {
                visualRoot.rotation = Quaternion.LookRotation(facingForward, Vector3.up);
            }

            TryCaptureCurrentGrindProbeCenter();
        }

        private void Awake()
        {
            ApplyProfile();
            characterController = GetComponent<CharacterController>();
            aimer = GetComponent<MousePlaneAimer>();
            input = GetComponent<IPlayerLocomotionInput>();
            playerRig = GetComponent<PlayerRig>();
            slopeAlignment = GetComponent<PlayerSlopeAlignment>();

            if (visualRoot == null)
            {
                visualRoot = playerRig != null && playerRig.VisualRoot != null ? playerRig.VisualRoot : transform;
            }

            if (wallRideProbe == null)
            {
                wallRideProbe = GetComponentInChildren<PlayerWallRideProbe>();
            }

            if (grindProbe == null)
            {
                grindProbe = GetComponentInChildren<PlayerGrindProbe>();
            }

            facingForward = GetInitialFacingForward();
            previousPosition = transform.position;
            TryCaptureCurrentGrindProbeCenter();
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }

            UpdateTimers();
            UpdateFacing();
            UpdateMovement();
            UpdateJumpPhase();
        }

        public void SetGameplayMovementLocked(bool locked)
        {
            gameplayMovementLocked = locked;
            if (!locked)
            {
                return;
            }

            if (isWallRiding)
            {
                EndWallRide();
            }

            if (isGrinding)
            {
                EndGrinding(false);
            }

            jumpPreparing = false;
            jumpStartedFromWallRide = false;
            jumpStartedFromGrind = false;
            jumpBufferTimer = 0f;
            jumpPreparationTimer = 0f;
            jumpPreparationUngroundedTimer = 0f;
            MoveInput = Vector2.zero;
            LocalVelocity = Vector2.zero;
            planarVelocity = Vector3.zero;

            if (IsGrounded)
            {
                verticalVelocity = groundedVerticalVelocity;
            }
        }

        private void UpdateFacing()
        {
            if (gameplayMovementLocked)
            {
                return;
            }

            Vector3 targetForward = Vector3.ProjectOnPlane(aimer.AimForward, Vector3.up);
            if (targetForward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            targetForward.Normalize();
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            facingForward = Vector3.Slerp(facingForward, targetForward, rotationBlend);
            facingForward = GetPlanarDirectionOrFallback(facingForward, targetForward);

            if (visualRoot == null || (slopeAlignment != null && slopeAlignment.IsAlignmentActive))
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(facingForward, Vector3.up);
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRotation, rotationBlend);
        }

        private void UpdateMovement()
        {
            UpdateGroundingState();
            BeginTraversalContactFrame();
            ReleaseWallRideIfProbeLostContact();
            ReleaseGrindingIfProbeLostContact();
            Vector3 forward = facingForward.sqrMagnitude > 0.0001f ? facingForward : GetInitialFacingForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector2 moveInput = gameplayMovementLocked ? Vector2.zero : Vector2.ClampMagnitude(input.MoveInput, 1f);
            MoveInput = gameplayMovementLocked || (isGrinding && isGrindingAttached) ? Vector2.zero : moveInput;

            if (!isGrinding)
            {
                Vector3 desiredPlanarVelocity = gameplayMovementLocked
                    ? Vector3.zero
                    : (right * moveInput.x + forward * moveInput.y) * GetTargetSpeed();
                if (IsGrounded)
                {
                    desiredPlanarVelocity = Vector3.ProjectOnPlane(desiredPlanarVelocity, groundNormal);
                }
                else if (isWallRiding)
                {
                    desiredPlanarVelocity = Vector3.ProjectOnPlane(desiredPlanarVelocity, wallNormal);
                }

                float controlPercent = IsGrounded ? 1f : airControlPercent;
                float sharpness = desiredPlanarVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
                sharpness *= controlPercent;
                if (gameplayMovementLocked)
                {
                    planarVelocity = Vector3.zero;
                    verticalVelocity = groundedVerticalVelocity;
                }
                else
                {
                    planarVelocity = Vector3.Lerp(planarVelocity, desiredPlanarVelocity, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
                    ApplyWallRideVelocityConstraints();
                    ApplySurfaceGravity();
                    ApplySlopeSliding();
                    ApplyIdlePlanarSnap(moveInput);
                    HandleJumpInput();
                    ApplyVerticalForces();
                }
            }
            else
            {
                HandleGrinding(moveInput);
            }

            bool wasGroundedBeforeMove = IsGrounded;

            Vector3 velocity = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z);
            characterController.Move(velocity * Time.deltaTime);
            UpdateActualVerticalSpeed();
            UpdateGroundingState();
            ProbeForNearbyWallContact();
            ProbeForNearbyGrindContact();
            UpdateWallRideInputWindow();
            UpdateWallRideStateAfterMove();
            UpdateGrindingStateAfterMove();
            ProcessLandingEvents(wasGroundedBeforeMove);

            LocalVelocity = new Vector2(
                Vector3.Dot(planarVelocity, right),
                Vector3.Dot(planarVelocity, forward));
        }

        private void UpdateTimers()
        {
            if (input.JumpPressedThisFrame)
            {
                jumpBufferTimer = jumpBufferTime;
            }
            else
            {
                jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
            }

            grindFailureLockoutTimer = Mathf.Max(0f, grindFailureLockoutTimer - Time.deltaTime);

            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
            }
        }

        private void UpdateGroundingState()
        {
            IsGrounded = characterController.isGrounded;
            groundNormal = Vector3.up;
            CurrentSlopeAngle = 0f;

            Vector3 origin = transform.position + characterController.center;
            float probeRadius = Mathf.Max(0.05f, characterController.radius * 0.9f);
            float probeLength = characterController.height * 0.5f + groundProbeDistance;
            if (Physics.SphereCast(origin, probeRadius, Vector3.down, out RaycastHit hit, probeLength, groundProbeMask, QueryTriggerInteraction.Ignore))
            {
                groundNormal = hit.normal.normalized;
                CurrentSlopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                if (groundNormal.y > 0.0001f)
                {
                    stableGroundNormal = groundNormal;
                    lastGroundContactTime = Time.time;
                }
            }
            else if (IsGrounded)
            {
                stableGroundNormal = Vector3.up;
                lastGroundContactTime = Time.time;
            }
        }

        private void HandleJumpInput()
        {
            bool canUseBufferedJump = jumpBufferTimer > 0f;
            bool canStartJumpPreparation = !jumpConsumed && (IsGrounded || coyoteTimer > 0f || isWallRiding || isGrinding);
            if (canUseBufferedJump && canStartJumpPreparation)
            {
                StartJumpPreparation();
            }

            bool shouldReleaseJump = jumpPreparing && (input.JumpReleasedThisFrame || !input.JumpHeld);
            if (shouldReleaseJump)
            {
                ReleaseJump();
            }
        }

        private void StartJumpPreparation()
        {
            jumpStartedFromWallRide = isWallRiding;
            jumpStartedFromGrind = isGrinding;
            if (!jumpStartedFromWallRide)
            {
                EndWallRide();
            }
            if (!jumpStartedFromGrind)
            {
                EndGrinding(false);
            }

            jumpConsumed = true;
            jumpBufferTimer = 0f;
            jumpPreparationTimer = 0f;
            jumpPreparationUngroundedTimer = 0f;
            jumpPreparing = true;
            verticalVelocity = groundedVerticalVelocity;
            jumpPhase = JumpPhase.JumpStart;

            JumpStarted?.Invoke();
        }

        private void ReleaseJump()
        {
            if (!jumpPreparing)
            {
                return;
            }

            bool releasingFromWallRide = jumpStartedFromWallRide;
            bool releasingFromGrind = jumpStartedFromGrind;
            jumpPreparing = false;
            jumpPreparationTimer = 0f;
            jumpPreparationUngroundedTimer = 0f;
            coyoteTimer = 0f;
            float selectedJumpHeight = releasingFromWallRide
                ? wallJumpHeight
                : jumpHeight;
            if (releasingFromWallRide)
            {
                EndWallRide();
            }
            if (releasingFromGrind)
            {
                EndGrinding(false);
            }

            verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * selectedJumpHeight);
            jumpStartedFromWallRide = false;
            jumpStartedFromGrind = false;
            JumpReleased?.Invoke();
        }

        private void ApplyVerticalForces()
        {
            if (jumpPreparing)
            {
                jumpPreparationTimer += Time.deltaTime;

                if (IsGrounded)
                {
                    jumpPreparationUngroundedTimer = 0f;
                    verticalVelocity = groundedVerticalVelocity;
                }
                else
                {
                    jumpPreparationUngroundedTimer += Time.deltaTime;
                }
            }
            else if (IsGrounded && verticalVelocity < 0f && !jumpConsumed)
            {
                verticalVelocity = groundedVerticalVelocity;
            }

            verticalVelocity += gravity * Time.deltaTime;

            if (isWallRiding && verticalVelocity < 0f)
            {
                verticalVelocity = Mathf.MoveTowards(
                    verticalVelocity,
                    0f,
                    wallRideVerticalBrakeDeceleration * Time.deltaTime);
            }

            if (isGrindingAttached && verticalVelocity < 0f)
            {
                verticalVelocity = Mathf.Max(verticalVelocity, Vector3.Dot(grindTangent * grindSignedSpeed, Vector3.up));
            }
        }

        private void ProcessLandingEvents(bool wasGroundedBeforeMove)
        {
            bool landedThisFrame = !wasGroundedBeforeMove && IsGrounded;
            bool shouldPreservePreparedJump = landedThisFrame
                && jumpPreparing
                && input != null
                && input.JumpHeld
                && jumpPreparationUngroundedTimer <= jumpPreparationUngroundedTolerance;

            if (shouldPreservePreparedJump)
            {
                jumpPreparationUngroundedTimer = 0f;
                verticalVelocity = groundedVerticalVelocity;
                return;
            }

            if (landedThisFrame)
            {
                jumpConsumed = false;
                jumpPreparing = false;
                jumpStartedFromWallRide = false;
                jumpStartedFromGrind = false;
                jumpPreparationTimer = 0f;
                jumpPreparationUngroundedTimer = 0f;
                verticalVelocity = groundedVerticalVelocity;
                Landed?.Invoke();
            }

            if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVerticalVelocity;
            }

            if (IsGrounded)
            {
                EndWallRide();
                EndGrinding(false);
            }

        }

        private void UpdateJumpPhase()
        {
            if (jumpPreparing)
            {
                jumpPhase = jumpPreparationTimer <= Mathf.Epsilon ? JumpPhase.JumpStart : JumpPhase.JumpHold;
                return;
            }

            if (actualVerticalSpeed > ascendingVelocityThreshold)
            {
                jumpPhase = JumpPhase.Ascending;
                return;
            }

            if (actualVerticalSpeed < -ascendingVelocityThreshold)
            {
                jumpPhase = JumpPhase.Descending;
                return;
            }

            if (IsGrounded)
            {
                jumpPhase = JumpPhase.Grounded;
                return;
            }

            if (verticalVelocity > ascendingVelocityThreshold)
            {
                jumpPhase = JumpPhase.Ascending;
                return;
            }

            jumpPhase = JumpPhase.Descending;
        }

        private float GetTargetSpeed()
        {
            return input.SprintHeld ? sprintSpeed : walkSpeed;
        }

        private void ApplySurfaceGravity()
        {
            if (!IsGrounded)
            {
                return;
            }

            Vector3 surfaceGravity = Vector3.ProjectOnPlane(Vector3.down * Mathf.Abs(gravity), groundNormal);
            if (surfaceGravity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            planarVelocity += surfaceGravity * (surfaceGravityScale * Time.deltaTime);
        }

        private void ApplySlopeSliding()
        {
            IsSlopeSliding = false;

            if (!IsGrounded)
            {
                return;
            }

            float slopeAngle = CurrentSlopeAngle;
            if (slopeAngle < slideStartAngle)
            {
                return;
            }

            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
            if (downhillDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            downhillDirection.Normalize();
            float currentSlopeLimit = characterController != null ? characterController.slopeLimit : 45f;
            float downhillStrength = slopeAngle >= currentSlopeLimit ? steepSlideAcceleration : slideAcceleration;
            float slopeFactor = Mathf.InverseLerp(slideStartAngle, Mathf.Max(slideStartAngle + 0.01f, currentSlopeLimit), slopeAngle);
            if (slopeAngle >= currentSlopeLimit)
            {
                slopeFactor = 1f;
            }

            planarVelocity += downhillDirection * (downhillStrength * slopeFactor * Time.deltaTime);
            IsSlopeSliding = true;
        }

        private void ApplyIdlePlanarSnap(Vector2 moveInput)
        {
            if (!IsGrounded)
            {
                return;
            }

            if (moveInput.sqrMagnitude > 0.0001f)
            {
                return;
            }

            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle > idleSnapSlopeAngle)
            {
                return;
            }

            if (planarVelocity.sqrMagnitude > idlePlanarSnapSpeed * idlePlanarSnapSpeed)
            {
                return;
            }

            planarVelocity = Vector3.zero;
        }

        private void ApplyWallRideVelocityConstraints()
        {
            if (!isWallRiding)
            {
                return;
            }

            planarVelocity = Vector3.ProjectOnPlane(planarVelocity, wallNormal);
            if (planarVelocity.sqrMagnitude <= 0.0001f)
            {
                planarVelocity = Vector3.zero;
                return;
            }

            float newSpeed = Mathf.MoveTowards(planarVelocity.magnitude, 0f, wallRideBrakeDeceleration * Time.deltaTime);
            planarVelocity = planarVelocity.normalized * newSpeed;
        }

        private void BeginTraversalContactFrame()
        {
            pendingWallContact = default;
            pendingGrindContact = default;
        }

        private void ProbeForNearbyWallContact()
        {
            if (isGrinding || IsGrounded || wallRideProbeDistance <= Mathf.Epsilon || wallRideProbe == null)
            {
                return;
            }

            ProbeForNearbyWallContactWithSphere();
        }

        private void ProbeForNearbyGrindContact()
        {
            if (IsGrounded || grindProbeDistance <= Mathf.Epsilon || grindProbe == null)
            {
                TryCaptureCurrentGrindProbeCenter();
                return;
            }

            if (!grindProbe.TryGetWorldSphere(out Vector3 probeCenter, out float probeRadius))
            {
                return;
            }

            lastGrindProbeTravelDelta = hasPreviousGrindProbeCenter
                ? probeCenter - previousGrindProbeCenter
                : Vector3.zero;

            float searchRadius = Mathf.Max(0.01f, probeRadius + grindProbeDistance);
            int nearbyColliderCount = Physics.OverlapSphereNonAlloc(
                probeCenter,
                searchRadius,
                grindProbeResults,
                ~0,
                QueryTriggerInteraction.Collide);

            if (hasPreviousGrindProbeCenter)
            {
                nearbyColliderCount = Mathf.Max(
                    nearbyColliderCount,
                    Physics.OverlapCapsuleNonAlloc(
                        previousGrindProbeCenter,
                        probeCenter,
                        searchRadius,
                        grindProbeResults,
                        ~0,
                        QueryTriggerInteraction.Collide));
            }

            for (int i = 0; i < nearbyColliderCount; i++)
            {
                Collider collider = grindProbeResults[i];
                if (collider == null || collider.transform.root == transform.root)
                {
                    continue;
                }

                GrindRail rail = collider.GetComponentInParent<GrindRail>();
                if (rail == null)
                {
                    continue;
                }

                if (grindMask.value != 0 && (grindMask.value & (1 << rail.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (!rail.TryGetNearestSample(probeCenter, out GrindRail.Sample sample))
                {
                    continue;
                }

                float allowedDistance = searchRadius + rail.CollisionRadius;
                if (sample.DistanceToRail > allowedDistance)
                {
                    continue;
                }

                Vector3 projectedVelocity = WorldVelocity;
                Vector3 tangent = ResolveGrindTravelTangent(sample.Tangent, projectedVelocity);
                float alignment = projectedVelocity.sqrMagnitude > 0.0001f
                    ? Mathf.Max(0f, Vector3.Dot(projectedVelocity.normalized, tangent))
                    : 0f;
                float score = (allowedDistance - sample.DistanceToRail) + alignment;
                RegisterGrindContact(rail, sample, score);
            }

            previousGrindProbeCenter = probeCenter;
            hasPreviousGrindProbeCenter = true;
        }

        private void ProbeForNearbyWallContactWithSphere()
        {
            if (wallRideProbe == null || wallRideProbe.ProbeCollider == null)
            {
                return;
            }

            if (!wallRideProbe.TryGetWorldSphere(out Vector3 probeCenter, out float probeRadius))
            {
                return;
            }

            float searchRadius = Mathf.Max(0.01f, probeRadius + wallRideProbeDistance);
            Collider[] nearbyColliders = Physics.OverlapSphere(
                probeCenter,
                searchRadius,
                wallRideMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < nearbyColliders.Length; i++)
            {
                Collider collider = nearbyColliders[i];
                if (collider == null || IsSelfWallRideCollider(collider))
                {
                    continue;
                }

                if (!TryGetProbeWallContact(collider, out Vector3 contactNormal, out Vector3 contactPoint, out float penetrationDepth))
                {
                    continue;
                }

                float distanceScore = penetrationDepth;
                float alignmentScore = Mathf.Max(0f, -Vector3.Dot(WorldVelocity.normalized, contactNormal));
                float score = distanceScore + alignmentScore;
                RegisterWallContact(collider, contactNormal, contactPoint, score);
            }
        }

        private void ReleaseWallRideIfProbeLostContact()
        {
            if (!isWallRiding || activeWallCollider == null || wallRideProbe == null)
            {
                return;
            }

            if (IsProbeTouchingWall(activeWallCollider))
            {
                return;
            }

            CancelAirborneJumpPreparation();
            LogWallRideState("released lost_contact");
            EndWallRide();
        }

        private void ReleaseGrindingIfProbeLostContact()
        {
            if (!isGrindingAttached || activeGrindRail == null || grindProbe == null)
            {
                return;
            }

            if (!grindProbe.TryGetWorldSphere(out Vector3 probeCenter, out float _)
                || !activeGrindRail.TryGetNearestSample(probeCenter, out GrindRail.Sample sample))
            {
                isGrindingAttached = false;
                grindAirborneTimer = 0f;
                return;
            }

            activeGrindT = sample.T;
            grindTangent = GetCanonicalGrindTangent(sample.Tangent);
            grindUp = sample.Up;

            if (sample.DistanceToRail <= grindDetachDistance)
            {
                return;
            }

            isGrindingAttached = false;
            grindAirborneTimer = 0f;
        }

        private void UpdateWallRideStateAfterMove()
        {
            if (isGrinding)
            {
                wallRideInputGate.Reset();
                EndWallRide();
                return;
            }

            if (IsGrounded)
            {
                wallRideInputGate.Reset();
                LogWallRideState("blocked grounded");
                EndWallRide();
                return;
            }

            if (!pendingWallContact.IsValid)
            {
                wallRideInputGate.Reset();
                LogWallRideState("blocked no_contact");
                EndWallRide();
                return;
            }

            bool wasWallRiding = isWallRiding;
            if (!CanWallRide(out string rejectionReason))
            {
                LogWallRideState($"blocked {rejectionReason}");
                EndWallRide();
                return;
            }

            bool shouldRefreshJump = !wasWallRiding
                || activeWallCollider != pendingWallContact.Collider
                || Vector3.Dot(wallNormal, pendingWallContact.Normal) < 0.999f;

            isWallRiding = true;
            activeWallCollider = pendingWallContact.Collider;
            wallNormal = pendingWallContact.Normal;
            planarVelocity = Vector3.ProjectOnPlane(planarVelocity, wallNormal);

            if (shouldRefreshJump)
            {
                ResetJumpForWallRide();
            }

            if (!wasWallRiding)
            {
                ApplyWallRideEntryBoost();
            }

            wallRideInputGate.Reset();
            LogWallRideState(wasWallRiding ? "maintained" : "started");

            if (!wasWallRiding)
            {
                TryConsumeBufferedWallJump();
            }
        }

        private void UpdateGrindingStateAfterMove()
        {
            if (IsGrounded)
            {
                EndGrinding(false);
                return;
            }

            if (isWallRiding)
            {
                EndGrinding(false);
                return;
            }

            if (isGrinding)
            {
                RefreshActiveGrindAfterMove();
                return;
            }

            if (!pendingGrindContact.IsValid || !CanStartGrinding())
            {
                return;
            }

            StartGrinding(pendingGrindContact.Rail, pendingGrindContact.Sample);
        }

        private void ResetJumpForWallRide()
        {
            jumpConsumed = false;
            coyoteTimer = coyoteTime;
        }

        private void ResetJumpForGrinding()
        {
            jumpConsumed = false;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        private void TryConsumeBufferedWallJump()
        {
            bool canUseBufferedJump = jumpBufferTimer > 0f;
            bool canStartJumpPreparation = !jumpConsumed && isWallRiding;
            if (!canUseBufferedJump || !canStartJumpPreparation)
            {
                return;
            }

            StartJumpPreparation();
        }

        private void EndWallRide()
        {
            LogWallRideState("ended");
            isWallRiding = false;
            activeWallCollider = null;
            wallNormal = Vector3.zero;
        }

        private void StartGrinding(GrindRail rail, GrindRail.Sample sample)
        {
            if (rail == null)
            {
                return;
            }

            EndWallRide();
            activeGrindRail = rail;
            activeGrindT = sample.T;
            Vector3 splineTangent = sample.Tangent.sqrMagnitude > 0.0001f ? sample.Tangent.normalized : Vector3.forward;
            float entryDirectionSign = ResolveGrindEntryDirectionSign(rail, sample, splineTangent);
            grindUp = sample.Up;
            float entryVelocity = Vector3.Dot(WorldVelocity, splineTangent);
            float signedEntryVelocity = Mathf.Abs(entryVelocity) > 0.01f
                ? entryVelocity
                : entryDirectionSign;
            grindSignedSpeed = signedEntryVelocity + Mathf.Sign(signedEntryVelocity)
                * (grindEntrySpeedBoost + rail.EntrySpeedBoost);
            if (Mathf.Abs(grindSignedSpeed) <= 0.01f)
            {
                grindSignedSpeed = entryDirectionSign * Mathf.Max(grindEntrySpeedBoost + rail.EntrySpeedBoost, 0.01f);
            }

            grindTangent = splineTangent;
            isGrinding = true;
            isGrindingAttached = true;
            grindAirborneTimer = 0f;
            ResetGrindBalanceState(true);
            ResetJumpForGrinding();
            SnapToGrindMount(sample);
        }

        private void EndGrinding(bool preserveMomentum)
        {
            if (!isGrinding)
            {
                return;
            }

            if (!preserveMomentum)
            {
                Vector3 exitVelocity = grindTangent * grindSignedSpeed;
                planarVelocity = new Vector3(exitVelocity.x, 0f, exitVelocity.z);
                verticalVelocity = exitVelocity.y;
            }

            isGrinding = false;
            isGrindingAttached = false;
            activeGrindRail = null;
            activeGrindT = 0f;
            grindSignedSpeed = 0f;
            grindAirborneTimer = 0f;
            grindTangent = Vector3.forward;
            grindUp = Vector3.up;
            ResetGrindBalanceState(false);
        }

        private void HandleGrinding(Vector2 moveInput)
        {
            if (!isGrinding || activeGrindRail == null)
            {
                return;
            }

            HandleJumpInput();
            UpdateGrindingJumpPreparation();

            if (!isGrindingAttached)
            {
                HandleDetachedGrinding(moveInput);
                return;
            }

            if (!activeGrindRail.TryAdvance(activeGrindT, grindSignedSpeed * Time.deltaTime, out GrindRail.Sample nextSample, out bool reachedEnd))
            {
                EndGrinding(true);
                return;
            }

            if (!UpdateGrindingBalance(moveInput, nextSample))
            {
                FailGrindingBalance();
                return;
            }

            grindTangent = GetCanonicalGrindTangent(nextSample.Tangent);
            grindUp = nextSample.Up;
            float gravityAlongRail = Vector3.Dot(Vector3.down * Mathf.Abs(gravity) * grindGravityScale, grindTangent);
            grindSignedSpeed += gravityAlongRail * Time.deltaTime;
            grindSignedSpeed = Mathf.MoveTowards(grindSignedSpeed, 0f, grindSpeedDrag * Time.deltaTime);
            activeGrindT = nextSample.T;

            if (reachedEnd || activeGrindRail.IsNearEnd(activeGrindT))
            {
                EndGrinding(true);
                return;
            }

            Vector3 targetPosition = activeGrindRail.GetMountPoint(nextSample, GetRootOffsetFromGrindProbe());
            Vector3 magnetVelocity = (targetPosition - transform.position) * grindMagnetism;
            Vector3 totalVelocity = (grindTangent * grindSignedSpeed) + magnetVelocity;
            planarVelocity = new Vector3(totalVelocity.x, 0f, totalVelocity.z);
            verticalVelocity = totalVelocity.y;
        }

        private void HandleDetachedGrinding(Vector2 moveInput)
        {
            if (moveInput.magnitude >= grindAirControlDetachThreshold)
            {
                EndGrinding(true);
                return;
            }

            Vector3 forward = facingForward.sqrMagnitude > 0.0001f ? facingForward : GetInitialFacingForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 desiredPlanarVelocity = (right * moveInput.x + forward * moveInput.y) * GetTargetSpeed();
            float sharpness = desiredPlanarVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            sharpness *= airControlPercent;
            planarVelocity = Vector3.Lerp(planarVelocity, desiredPlanarVelocity, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
            verticalVelocity += gravity * Time.deltaTime;
            grindAirborneTimer += Time.deltaTime;

            if (grindAirborneTimer > grindAirborneReattachTime || grindProbe == null)
            {
                EndGrinding(true);
                return;
            }

            if (!grindProbe.TryGetWorldSphere(out Vector3 probeCenter, out float _)
                || !activeGrindRail.TryGetNearestSample(probeCenter, out GrindRail.Sample sample)
                || sample.DistanceToRail > grindAirborneReattachDistance)
            {
                return;
            }

            isGrindingAttached = true;
            grindAirborneTimer = 0f;
            activeGrindT = sample.T;
            grindTangent = GetCanonicalGrindTangent(sample.Tangent);
            grindUp = sample.Up;
            SnapToGrindMount(sample);
        }

        private bool UpdateGrindingBalance(Vector2 moveInput, GrindRail.Sample nextSample)
        {
            if (!grindBalanceEnabled)
            {
                grindBalanceOffset = 0f;
                return true;
            }

            UpdateGrindingDriftTarget();
            float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(grindSignedSpeed) / Mathf.Max(1f, sprintSpeed));
            float driftMagnitude = grindBalanceBaseDrift * (1f + (normalizedSpeed * grindBalanceSpeedDriftMultiplier));
            float driftBlend = 1f - Mathf.Exp(-grindBalanceDriftSharpness * Time.deltaTime);
            grindBalanceDriftVelocity = Mathf.Lerp(grindBalanceDriftVelocity, grindBalanceDriftTarget * driftMagnitude, driftBlend);

            Vector3 nextTangent = GetCanonicalGrindTangent(nextSample.Tangent);
            float signedCurvature = Vector3.Dot(Vector3.Cross(grindTangent, nextTangent), grindUp) * grindBalanceCurvatureInfluence;
            float controlVelocity = UpdateGrindingBalanceControlVelocity(moveInput.x);
            grindBalanceOffset += (grindBalanceDriftVelocity + signedCurvature + controlVelocity) * Time.deltaTime;

            float failureThreshold = GetGrindBalanceFailureThreshold();
            grindBalanceOffset = Mathf.Clamp(grindBalanceOffset, -failureThreshold * 1.25f, failureThreshold * 1.25f);
            return !HasExceededGrindingBalanceFailureThreshold(failureThreshold);
        }

        private void UpdateGrindingDriftTarget()
        {
            if (grindBalanceRetargetTimer <= 0f)
            {
                float minimumRetargetTime = Mathf.Max(0.05f, grindBalanceDriftRetargetTime.x);
                float maximumRetargetTime = Mathf.Max(minimumRetargetTime, grindBalanceDriftRetargetTime.y);
                grindBalanceRetargetTimer = Random.Range(minimumRetargetTime, maximumRetargetTime);
                grindBalanceDriftTarget = Random.Range(-1f, 1f);
                return;
            }

            grindBalanceRetargetTimer -= Time.deltaTime;
        }

        private float UpdateGrindingBalanceControlVelocity(float horizontalInput)
        {
            float clampedInput = Mathf.Clamp(horizontalInput, -1f, 1f);
            float failureThreshold = GetGrindBalanceFailureThreshold();
            float safeZoneThreshold = Mathf.Min(grindBalanceSafeZone, failureThreshold);
            float currentOffsetMagnitude = Mathf.Abs(grindBalanceOffset);
            float danger = Mathf.InverseLerp(safeZoneThreshold, failureThreshold, currentOffsetMagnitude);
            float controlAuthority = Mathf.Lerp(grindBalanceCenterControlMultiplier, 1f, danger);
            float targetVelocity = clampedInput * grindBalanceControlTopSpeed * controlAuthority;

            float currentVelocity = grindBalanceControlVelocity;
            bool isAcceleratingIntoSameDirection = Mathf.Abs(targetVelocity) > Mathf.Abs(currentVelocity)
                && (Mathf.Approximately(currentVelocity, 0f) || Mathf.Sign(targetVelocity) == Mathf.Sign(currentVelocity));
            float rate = isAcceleratingIntoSameDirection
                ? grindBalanceControlAcceleration
                : grindBalanceControlDeceleration;

            grindBalanceControlVelocity = Mathf.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);
            return grindBalanceControlVelocity;
        }

        private float GetGrindBalanceFailureThreshold()
        {
            return Mathf.Max(0.01f, grindBalanceFailureThreshold);
        }

        private bool HasExceededGrindingBalanceFailureThreshold(float failureThreshold)
        {
            return Mathf.Abs(grindBalanceOffset) >= failureThreshold;
        }

        private void FailGrindingBalance()
        {
            float sideSign = Mathf.Sign(grindBalanceOffset);
            if (Mathf.Approximately(sideSign, 0f))
            {
                sideSign = Random.value < 0.5f ? -1f : 1f;
            }

            Vector3 lateralDirection = Vector3.Cross(grindUp, grindTangent).normalized * sideSign;
            if (lateralDirection.sqrMagnitude <= 0.0001f)
            {
                lateralDirection = transform.right * sideSign;
            }

            float tangentJitter = Random.Range(-grindBalanceFailureLaunchTangentJitter, grindBalanceFailureLaunchTangentJitter);
            Vector3 launchDirection = (lateralDirection + (grindTangent.normalized * tangentJitter)).normalized;
            Vector3 launchVelocity = (launchDirection * grindBalanceFailureLaunchHorizontalSpeed) + (grindUp.normalized * grindBalanceFailureLaunchVerticalSpeed);

            EndGrinding(true);
            pendingGrindContact = default;
            grindFailureLockoutTimer = grindBalanceFailureLockoutDuration;
            planarVelocity = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
            verticalVelocity = launchVelocity.y;
        }

        private void RefreshActiveGrindAfterMove()
        {
            if (!isGrinding || !isGrindingAttached || activeGrindRail == null || grindProbe == null)
            {
                return;
            }

            if (!grindProbe.TryGetWorldSphere(out Vector3 probeCenter, out float _)
                || !activeGrindRail.TryGetNearestSample(probeCenter, out GrindRail.Sample sample))
            {
                isGrindingAttached = false;
                grindAirborneTimer = 0f;
                return;
            }

            activeGrindT = sample.T;
            grindTangent = GetCanonicalGrindTangent(sample.Tangent);
            grindUp = sample.Up;
            if (sample.DistanceToRail > grindDetachDistance)
            {
                isGrindingAttached = false;
                grindAirborneTimer = 0f;
            }
        }

        private void SnapToGrindMount(GrindRail.Sample sample)
        {
            if (characterController == null || grindProbe == null)
            {
                return;
            }

            Vector3 targetPosition = activeGrindRail.GetMountPoint(sample, GetRootOffsetFromGrindProbe());
            Vector3 delta = targetPosition - transform.position;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            characterController.Move(delta);
        }

        private bool CanStartGrinding()
        {
            if (isGrinding || isWallRiding || jumpPreparing || IsGrounded || pendingGrindContact.Rail == null || grindFailureLockoutTimer > 0f)
            {
                return false;
            }

            return verticalVelocity <= ascendingVelocityThreshold;
        }

        private void UpdateGrindingJumpPreparation()
        {
            if (!jumpPreparing || IsGrounded)
            {
                return;
            }

            jumpPreparationTimer += Time.deltaTime;
            jumpPreparationUngroundedTimer += Time.deltaTime;
        }

        private void UpdateWallRideInputWindow()
        {
            wallRideInputGate.Configure(traversalActivationWindow);
        }

        private bool CanWallRide(out string rejectionReason)
        {
            if (isGrinding)
            {
                rejectionReason = "grinding";
                return false;
            }

            if (IsGrounded)
            {
                rejectionReason = "grounded";
                return false;
            }

            if (jumpPreparing)
            {
                if (isWallRiding)
                {
                    rejectionReason = string.Empty;
                    return true;
                }

                rejectionReason = "jump_preparing";
                return false;
            }

            if (isWallRiding)
            {
                rejectionReason = string.Empty;
                return true;
            }

            bool isFalling = verticalVelocity <= ascendingVelocityThreshold;
            rejectionReason = isFalling ? string.Empty : $"not_falling({verticalVelocity:F2})";
            return isFalling;
        }

        private void RegisterGrindContact(GrindRail rail, GrindRail.Sample sample, float score)
        {
            if (rail == null || (pendingGrindContact.IsValid && score <= pendingGrindContact.Score))
            {
                return;
            }

            pendingGrindContact = new GrindContact
            {
                IsValid = true,
                Rail = rail,
                Sample = sample,
                Score = score
            };
        }

        private void ApplyWallRideEntryBoost()
        {
            if (wallRideEntryUpwardBoost <= Mathf.Epsilon)
            {
                return;
            }

            verticalVelocity = Mathf.Max(verticalVelocity, wallRideEntryUpwardBoost);
        }

        private bool TryConsumeWallRideStartInput(out string rejectionReason)
        {
            int opportunityId = GetWallRideOpportunityId(pendingWallContact.Collider);
            if (wallRideInputGate.TryConsume(opportunityId, input != null && input.JumpPressedThisFrame))
            {
                jumpBufferTimer = 0f;
                rejectionReason = string.Empty;
                return true;
            }

            rejectionReason = wallRideInputGate.IsOpenFor(opportunityId)
                ? "awaiting_jump_input"
                : "jump_input_window_expired";
            return false;
        }

        private void OnControllerColliderHit(ControllerColliderHit _)
        {
            // Wall-ride contact selection is owned by the dedicated wall probe.
        }

        private bool IsValidWallRideContact(Collider collider, Vector3 contactNormal, Vector3 contactPoint, string source)
        {
            if (collider == null)
            {
                return false;
            }

            if (IsSelfWallRideCollider(collider))
            {
                if (enableWallRideDebugLogs)
                {
                    Debug.Log($"[WallRideDebug] source={source} collider={collider.name} result=rejected self", collider);
                }

                return false;
            }

            float surfaceAngle = Vector3.Angle(contactNormal, Vector3.up);
            bool rejectedByLayer = (wallRideMask.value & (1 << collider.gameObject.layer)) == 0;
            bool rejectedByAngle = surfaceAngle < wallRideMinSurfaceAngle || surfaceAngle > wallRideMaxSurfaceAngle;
            bool rejectedByHeight = !IsWithinWallRideProbe(contactPoint);

            LogWallRideContact(collider, surfaceAngle, rejectedByLayer, rejectedByAngle, rejectedByHeight, source);

            if (rejectedByLayer || rejectedByAngle || rejectedByHeight)
            {
                return false;
            }

            return true;
        }

        private void RegisterWallContact(Collider collider, Vector3 contactNormal, Vector3 contactPoint, float score)
        {
            if (!IsValidWallRideContact(collider, contactNormal, contactPoint, "Probe/Register"))
            {
                return;
            }

            if (isWallRiding && collider == activeWallCollider)
            {
                score += 1f;
            }

            if (score <= pendingWallContact.Score)
            {
                return;
            }

            pendingWallContact = new WallContact
            {
                IsValid = true,
                Collider = collider,
                Normal = contactNormal.normalized,
                Point = contactPoint,
                Score = score
            };
        }

        private void LogWallRideContact(
            Collider collider,
            float surfaceAngle,
            bool rejectedByLayer,
            bool rejectedByAngle,
            bool rejectedByHeight,
            string source)
        {
            if (!enableWallRideDebugLogs || collider == null)
            {
                return;
            }

            string rejectionSummary = rejectedByLayer || rejectedByAngle || rejectedByHeight
                ? $"candidateRejected layer={rejectedByLayer} angle={rejectedByAngle} height={rejectedByHeight}"
                : "candidateAccepted";

            Debug.Log(
                $"[WallRideDebug] source={source} collider={collider.name} angle={surfaceAngle:F2} layerRejected={rejectedByLayer} angleRejected={rejectedByAngle} heightRejected={rejectedByHeight} result={rejectionSummary}",
                collider);
        }

        private void LogWallRideState(string state)
        {
            if (!enableWallRideDebugLogs)
            {
                return;
            }

            string colliderName = pendingWallContact.Collider != null ? pendingWallContact.Collider.name : "none";
            Debug.Log(
                $"[WallRideState] state={state} grounded={IsGrounded} pending={pendingWallContact.IsValid} wallRiding={isWallRiding} jumpConsumed={jumpConsumed} jumpPreparing={jumpPreparing} verticalVelocity={verticalVelocity:F2} wall={colliderName}",
                this);
        }

        private bool IsWithinWallRideProbe(Vector3 contactPoint)
        {
            return wallRideProbe != null && wallRideProbe.ContainsPoint(contactPoint, wallRideContactOffset);
        }

        private bool IsProbeTouchingWall(Collider collider)
        {
            return TryGetProbeWallContact(collider, out _, out _, out _);
        }

        private void CancelAirborneJumpPreparation()
        {
            if (!jumpPreparing || IsGrounded)
            {
                return;
            }

            jumpPreparing = false;
            jumpStartedFromWallRide = false;
            jumpStartedFromGrind = false;
            jumpPreparationTimer = 0f;
            jumpPreparationUngroundedTimer = 0f;
        }

        private bool TryGetProbeWallContact(Collider collider, out Vector3 contactNormal, out Vector3 contactPoint, out float penetrationDepth)
        {
            contactNormal = Vector3.zero;
            contactPoint = Vector3.zero;
            penetrationDepth = 0f;

            if (collider == null || wallRideProbe == null || wallRideProbe.ProbeCollider == null)
            {
                return false;
            }

            Collider probeCollider = wallRideProbe.ProbeCollider;
            if (!Physics.ComputePenetration(
                    probeCollider,
                    probeCollider.transform.position,
                    probeCollider.transform.rotation,
                    collider,
                    collider.transform.position,
                    collider.transform.rotation,
                    out Vector3 separationDirection,
                    out float separationDistance))
            {
                return false;
            }

            if (separationDistance <= 0f)
            {
                return false;
            }

            contactNormal = separationDirection.normalized;
            contactPoint = collider.ClosestPoint(probeCollider.bounds.center);
            penetrationDepth = separationDistance;
            return true;
        }

        private bool IsSelfWallRideCollider(Collider collider)
        {
            Transform colliderRoot = collider.transform.root;
            return colliderRoot == transform.root;
        }

        private static int GetWallRideOpportunityId(Collider collider)
        {
            return collider != null ? collider.GetInstanceID() : 0;
        }

        private Vector3 GetRootOffsetFromGrindProbe()
        {
            if (grindProbe == null)
            {
                return Vector3.zero;
            }

            return transform.position - grindProbe.WorldCenter;
        }

        private void TryCaptureCurrentGrindProbeCenter()
        {
            if (grindProbe == null)
            {
                lastGrindProbeTravelDelta = Vector3.zero;
                hasPreviousGrindProbeCenter = false;
                return;
            }

            if (!grindProbe.TryGetWorldSphere(out Vector3 center, out float _))
            {
                lastGrindProbeTravelDelta = Vector3.zero;
                hasPreviousGrindProbeCenter = false;
                return;
            }

            lastGrindProbeTravelDelta = hasPreviousGrindProbeCenter
                ? center - previousGrindProbeCenter
                : Vector3.zero;
            previousGrindProbeCenter = center;
            hasPreviousGrindProbeCenter = true;
        }

        private static Vector3 GetCanonicalGrindTangent(Vector3 tangent)
        {
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
        }

        private static Vector3 ResolveGrindTravelTangent(Vector3 tangent, Vector3 velocity)
        {
            float projectedSpeed = Vector3.Dot(velocity, tangent);
            return ResolveGrindTravelTangent(tangent, projectedSpeed);
        }

        private float ResolveGrindEntryDirectionSign(GrindRail rail, GrindRail.Sample sample, Vector3 splineTangent)
        {
            if (rail != null
                && hasPreviousGrindProbeCenter
                && rail.TryGetNearestSample(previousGrindProbeCenter, out GrindRail.Sample previousSample))
            {
                float sampleDelta = sample.T - previousSample.T;
                if (Mathf.Abs(sampleDelta) > 0.0005f)
                {
                    return Mathf.Sign(sampleDelta);
                }
            }

            float probeTravelAlignment = Vector3.Dot(lastGrindProbeTravelDelta, splineTangent);
            if (Mathf.Abs(probeTravelAlignment) > 0.0001f)
            {
                return Mathf.Sign(probeTravelAlignment);
            }

            float planarVelocityAlignment = Vector3.Dot(Vector3.ProjectOnPlane(planarVelocity, Vector3.up), splineTangent);
            if (Mathf.Abs(planarVelocityAlignment) > 0.01f)
            {
                return Mathf.Sign(planarVelocityAlignment);
            }

            float velocityAlignment = Vector3.Dot(WorldVelocity, splineTangent);
            if (Mathf.Abs(velocityAlignment) > 0.01f)
            {
                return Mathf.Sign(velocityAlignment);
            }

            float facingAlignment = Vector3.Dot(facingForward, splineTangent);
            if (Mathf.Abs(facingAlignment) > 0.0001f)
            {
                return Mathf.Sign(facingAlignment);
            }

            return Vector3.Dot(splineTangent, Vector3.down) >= 0f ? 1f : -1f;
        }

        private static Vector3 ResolveGrindTravelTangent(Vector3 tangent, float signedSpeed)
        {
            Vector3 normalizedTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
            if (Mathf.Approximately(signedSpeed, 0f))
            {
                float downhillBias = Vector3.Dot(normalizedTangent, Vector3.down);
                return downhillBias >= 0f ? normalizedTangent : -normalizedTangent;
            }

            return signedSpeed >= 0f ? normalizedTangent : -normalizedTangent;
        }

        private void UpdateActualVerticalSpeed()
        {
            if (Time.deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            actualVerticalSpeed = (transform.position.y - previousPosition.y) / Time.deltaTime;
            previousPosition = transform.position;
        }

        private void ApplyProfile()
        {
            if (movementProfile == null)
            {
                return;
            }

            walkSpeed = movementProfile.WalkSpeed;
            sprintSpeed = movementProfile.SprintSpeed;
            acceleration = movementProfile.Acceleration;
            deceleration = movementProfile.Deceleration;
            airControlPercent = movementProfile.AirControlPercent;
            rotationSharpness = movementProfile.RotationSharpness;
            gravity = movementProfile.Gravity;
            groundedVerticalVelocity = movementProfile.GroundedVerticalVelocity;
            jumpHeight = movementProfile.JumpHeight;
            wallJumpHeight = movementProfile.WallJumpHeight;
            coyoteTime = movementProfile.CoyoteTime;
            jumpBufferTime = movementProfile.JumpBufferTime;
            jumpPreparationUngroundedTolerance = movementProfile.JumpPreparationUngroundedTolerance;
            ascendingVelocityThreshold = movementProfile.AscendingVelocityThreshold;
            groundProbeDistance = movementProfile.GroundProbeDistance;
            groundProbeMask = movementProfile.GroundProbeMask;
            groundContactGraceTime = movementProfile.GroundContactGraceTime;
            idlePlanarSnapSpeed = movementProfile.IdlePlanarSnapSpeed;
            idleSnapSlopeAngle = movementProfile.IdleSnapSlopeAngle;
            surfaceGravityScale = movementProfile.SurfaceGravityScale;
            slideStartAngle = movementProfile.SlideStartAngle;
            slideAcceleration = movementProfile.SlideAcceleration;
            steepSlideAcceleration = movementProfile.SteepSlideAcceleration;
            wallRideMask = movementProfile.WallRideMask;
            wallRideMinSurfaceAngle = movementProfile.WallRideMinSurfaceAngle;
            wallRideMaxSurfaceAngle = movementProfile.WallRideMaxSurfaceAngle;
            wallRideBrakeDeceleration = movementProfile.WallRideBrakeDeceleration;
            wallRideVerticalBrakeDeceleration = movementProfile.WallRideVerticalBrakeDeceleration;
            wallRideContactOffset = movementProfile.WallRideContactOffset;
            wallRideProbeDistance = movementProfile.WallRideProbeDistance;
            wallRideEntryUpwardBoost = movementProfile.WallRideEntryUpwardBoost;
            grindMask = movementProfile.GrindMask;
            grindProbeDistance = movementProfile.GrindProbeDistance;
            grindJumpHeight = movementProfile.GrindJumpHeight;
            grindEntrySpeedBoost = movementProfile.GrindEntrySpeedBoost;
            grindGravityScale = movementProfile.GrindGravityScale;
            grindSpeedDrag = movementProfile.GrindSpeedDrag;
            grindMagnetism = movementProfile.GrindMagnetism;
            grindDetachDistance = movementProfile.GrindDetachDistance;
            grindAirborneReattachTime = movementProfile.GrindAirborneReattachTime;
            grindAirborneReattachDistance = movementProfile.GrindAirborneReattachDistance;
            grindAirControlDetachThreshold = movementProfile.GrindAirControlDetachThreshold;
            grindBalanceEnabled = movementProfile.GrindBalanceEnabled;
            grindBalanceSafeZone = movementProfile.GrindBalanceSafeZone;
            grindBalanceControlAcceleration = movementProfile.GrindBalanceControlAcceleration;
            grindBalanceControlDeceleration = movementProfile.GrindBalanceControlDeceleration;
            grindBalanceControlTopSpeed = movementProfile.GrindBalanceControlTopSpeed;
            grindBalanceCenterControlMultiplier = movementProfile.GrindBalanceCenterControlMultiplier;
            grindBalanceBaseDrift = movementProfile.GrindBalanceBaseDrift;
            grindBalanceSpeedDriftMultiplier = movementProfile.GrindBalanceSpeedDriftMultiplier;
            grindBalanceCurvatureInfluence = movementProfile.GrindBalanceCurvatureInfluence;
            grindBalanceDriftSharpness = movementProfile.GrindBalanceDriftSharpness;
            grindBalanceDriftRetargetTime = movementProfile.GrindBalanceDriftRetargetTime;
            grindBalanceFailureThreshold = movementProfile.GrindBalanceFailureThreshold;
            grindBalanceFailureLockoutDuration = movementProfile.GrindBalanceFailureLockoutDuration;
            grindBalanceFailureLaunchHorizontalSpeed = movementProfile.GrindBalanceFailureLaunchHorizontalSpeed;
            grindBalanceFailureLaunchVerticalSpeed = movementProfile.GrindBalanceFailureLaunchVerticalSpeed;
            grindBalanceFailureLaunchTangentJitter = movementProfile.GrindBalanceFailureLaunchTangentJitter;
            traversalActivationWindow = movementProfile.TraversalActivationWindow;
        }

        private void ResetGrindBalanceState(bool randomizeOffset)
        {
            grindBalanceOffset = randomizeOffset
                ? Random.Range(-grindBalanceSafeZone * 0.35f, grindBalanceSafeZone * 0.35f)
                : 0f;
            grindBalanceDriftVelocity = 0f;
            grindBalanceDriftTarget = 0f;
            grindBalanceControlVelocity = 0f;
            grindBalanceRetargetTimer = 0f;
        }

        private Vector3 GetInitialFacingForward()
        {
            Vector3 initialForward = visualRoot != null ? visualRoot.forward : transform.forward;
            return GetPlanarDirectionOrFallback(initialForward, Vector3.forward);
        }

        private static Vector3 GetPlanarDirectionOrFallback(Vector3 direction, Vector3 fallback)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            fallback = Vector3.ProjectOnPlane(fallback, Vector3.up);
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }
    }

    internal sealed class TimedTraversalInputGate
    {
        private float activationWindowSeconds;
        private int activeOpportunityId;
        private float windowExpiresAt = float.NegativeInfinity;
        private bool hasOpenWindow;

        public void Configure(float activationWindowDuration)
        {
            activationWindowSeconds = Mathf.Max(0f, activationWindowDuration);
        }

        public void Reset()
        {
            hasOpenWindow = false;
            activeOpportunityId = 0;
            windowExpiresAt = float.NegativeInfinity;
        }

        public bool RequiresTimedInput => activationWindowSeconds > Mathf.Epsilon;

        public void RegisterOpportunity(int opportunityId)
        {
            if (!RequiresTimedInput || opportunityId == 0)
            {
                return;
            }

            if (hasOpenWindow && activeOpportunityId == opportunityId && !HasExpired)
            {
                return;
            }

            hasOpenWindow = true;
            activeOpportunityId = opportunityId;
            windowExpiresAt = Time.time + activationWindowSeconds;
        }

        public bool IsOpenFor(int opportunityId)
        {
            if (!RequiresTimedInput)
            {
                return true;
            }

            return hasOpenWindow && activeOpportunityId == opportunityId && !HasExpired;
        }

        public bool TryConsume(int opportunityId, bool pressedThisFrame)
        {
            if (!RequiresTimedInput)
            {
                return true;
            }

            if (!pressedThisFrame || !IsOpenFor(opportunityId))
            {
                return false;
            }

            Reset();
            return true;
        }

        private bool HasExpired => hasOpenWindow && Time.time > windowExpiresAt;
    }
}
