using UnityEngine;

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
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpPreparationUngroundedTolerance = 0.08f;
        [SerializeField] private float ascendingVelocityThreshold = 0.05f;

        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private PlayerMovementProfile movementProfile;
        [SerializeField] private PlayerWallRideProbe wallRideProbe;

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
        private bool isWallRiding;
        private JumpPhase jumpPhase;
        private Collider activeWallCollider;
        private Vector3 wallNormal = Vector3.zero;
        private WallContact pendingWallContact;

        public Vector3 WorldVelocity => new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z);
        public float PlanarSpeed => planarVelocity.magnitude;
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
            jumpBufferTimer = 0f;
            jumpPreparationTimer = 0f;
            jumpPreparationUngroundedTimer = 0f;
            activeWallCollider = null;
            wallNormal = Vector3.zero;
            pendingWallContact = default;

            if (visualRoot != null && (slopeAlignment == null || !slopeAlignment.IsAlignmentActive))
            {
                visualRoot.rotation = Quaternion.LookRotation(facingForward, Vector3.up);
            }
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

            facingForward = GetInitialFacingForward();
            previousPosition = transform.position;
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

        private void UpdateFacing()
        {
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
            BeginWallContactFrame();
            ReleaseWallRideIfProbeLostContact();
            Vector3 forward = facingForward.sqrMagnitude > 0.0001f ? facingForward : GetInitialFacingForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector2 moveInput = Vector2.ClampMagnitude(input.MoveInput, 1f);
            MoveInput = moveInput;

            Vector3 desiredPlanarVelocity = (right * moveInput.x + forward * moveInput.y) * GetTargetSpeed();
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
            planarVelocity = Vector3.Lerp(planarVelocity, desiredPlanarVelocity, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
            ApplyWallRideVelocityConstraints();
            ApplySurfaceGravity();
            ApplySlopeSliding();
            ApplyIdlePlanarSnap(moveInput);

            HandleJumpInput();
            ApplyVerticalForces();

            bool wasGroundedBeforeMove = IsGrounded;

            Vector3 velocity = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z);
            characterController.Move(velocity * Time.deltaTime);
            UpdateActualVerticalSpeed();
            UpdateGroundingState();
            ProbeForNearbyWallContact();
            UpdateWallRideStateAfterMove();
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
            bool canStartJumpPreparation = !jumpConsumed && (IsGrounded || coyoteTimer > 0f || isWallRiding);
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
            EndWallRide();
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

            jumpPreparing = false;
            jumpPreparationTimer = 0f;
            jumpPreparationUngroundedTimer = 0f;
            coyoteTimer = 0f;
            verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
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

        private void BeginWallContactFrame()
        {
            pendingWallContact = default;
        }

        private void ProbeForNearbyWallContact()
        {
            if (IsGrounded || wallRideProbeDistance <= Mathf.Epsilon || wallRideProbe == null)
            {
                return;
            }

            ProbeForNearbyWallContactWithSphere();
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

        private void UpdateWallRideStateAfterMove()
        {
            if (IsGrounded)
            {
                LogWallRideState("blocked grounded");
                EndWallRide();
                return;
            }

            if (!pendingWallContact.IsValid)
            {
                LogWallRideState("blocked no_contact");
                EndWallRide();
                return;
            }

            if (!CanWallRide(out string rejectionReason))
            {
                LogWallRideState($"blocked {rejectionReason}");
                EndWallRide();
                return;
            }

            bool wasWallRiding = isWallRiding;
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

            LogWallRideState(wasWallRiding ? "maintained" : "started");

            if (!wasWallRiding)
            {
                TryConsumeBufferedWallJump();
            }
        }

        private void ResetJumpForWallRide()
        {
            jumpConsumed = false;
            coyoteTimer = coyoteTime;
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

        private bool CanWallRide(out string rejectionReason)
        {
            if (IsGrounded)
            {
                rejectionReason = "grounded";
                return false;
            }

            if (jumpPreparing)
            {
                rejectionReason = "jump_preparing";
                return false;
            }

            bool isFalling = verticalVelocity <= ascendingVelocityThreshold;
            rejectionReason = isFalling ? string.Empty : $"not_falling({verticalVelocity:F2})";
            return isFalling;
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
}
