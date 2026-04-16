using UnityEngine;

namespace Race.Player
{
    [CreateAssetMenu(fileName = "PlayerMovementProfile", menuName = "Race/Player/Movement Profile")]
    public sealed class PlayerMovementProfile : ScriptableObject
    {
        [field: Header("Movement")]
        [field: SerializeField, Min(0f)] public float WalkSpeed { get; private set; } = 25f;
        [field: SerializeField, Min(0f)] public float SprintSpeed { get; private set; } = 75f;
        [field: SerializeField, Min(0f)] public float Acceleration { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float Deceleration { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float AirControlPercent { get; private set; } = 0.9f;
        [field: SerializeField, Min(0f)] public float RotationSharpness { get; private set; } = 18f;
        [field: SerializeField] public float Gravity { get; private set; } = -30f;
        [field: SerializeField] public float GroundedVerticalVelocity { get; private set; } = -2f;

        [field: Header("Jump")]
        [field: SerializeField, Min(0f)] public float JumpHeight { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float WallJumpHeight { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float CoyoteTime { get; private set; } = 0.14f;
        [field: SerializeField, Min(0f)] public float JumpBufferTime { get; private set; } = 0.15f;
        [field: SerializeField, Min(0f)] public float JumpPreparationUngroundedTolerance { get; private set; } = 0.08f;
        [field: SerializeField, Min(0f)] public float AscendingVelocityThreshold { get; private set; } = 0.05f;

        [field: Header("Grounding")]
        [field: SerializeField, Min(0f)] public float GroundProbeDistance { get; private set; } = 0.9f;
        [field: SerializeField] public LayerMask GroundProbeMask { get; private set; } = ~0;
        [field: SerializeField, Min(0f)] public float GroundContactGraceTime { get; private set; } = 0.1f;
        [field: SerializeField, Min(0f)] public float IdlePlanarSnapSpeed { get; private set; } = 1.25f;
        [field: SerializeField, Range(0f, 89f)] public float IdleSnapSlopeAngle { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float SurfaceGravityScale { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 89f)] public float SlideStartAngle { get; private set; } = 3f;
        [field: SerializeField, Min(0f)] public float SlideAcceleration { get; private set; } = 18f;
        [field: SerializeField, Min(0f)] public float SteepSlideAcceleration { get; private set; } = 34f;

        [field: Header("Wall Ride")]
        [field: SerializeField] public LayerMask WallRideMask { get; private set; } = ~0;
        [field: SerializeField, Range(0f, 89f)] public float WallRideMinSurfaceAngle { get; private set; } = 75f;
        [field: SerializeField, Range(90f, 170f)] public float WallRideMaxSurfaceAngle { get; private set; } = 110f;
        [field: SerializeField, Min(0f)] public float WallRideBrakeDeceleration { get; private set; } = 1.5f;
        [field: SerializeField, Min(0f)] public float WallRideVerticalBrakeDeceleration { get; private set; } = 4f;
        [field: SerializeField, Min(0f)] public float WallRideContactOffset { get; private set; } = 0.08f;
        [field: SerializeField, Min(0f)] public float WallRideProbeDistance { get; private set; } = 0.2f;
        [field: SerializeField, Min(0f)] public float WallRideEntryUpwardBoost { get; private set; } = 12f;

        [field: Header("Grinding")]
        [field: SerializeField] public LayerMask GrindMask { get; private set; } = ~0;
        [field: SerializeField, Min(0f)] public float GrindProbeDistance { get; private set; } = 0.2f;
        [field: SerializeField, Min(0f)] public float GrindJumpHeight { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float GrindEntrySpeedBoost { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float GrindGravityScale { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float GrindSpeedDrag { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float GrindMagnetism { get; private set; } = 16f;
        [field: SerializeField, Min(0f)] public float GrindDetachDistance { get; private set; } = 0.45f;
        [field: SerializeField, Min(0f)] public float GrindAirborneReattachTime { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float GrindAirborneReattachDistance { get; private set; } = 0.5f;
        [field: SerializeField, Range(0f, 1f)] public float GrindAirControlDetachThreshold { get; private set; } = 0.35f;

        [field: Header("Grind Balance")]
        [field: SerializeField] public bool GrindBalanceEnabled { get; private set; } = true;
        [field: SerializeField, Range(0.05f, 0.45f)] public float GrindBalanceSafeZone { get; private set; } = 0.2f;
        [field: SerializeField, Min(0.1f)] public float GrindBalanceControlStrength { get; private set; } = 2.4f;
        [field: SerializeField, Min(0f)] public float GrindBalanceBaseDrift { get; private set; } = 0.58f;
        [field: SerializeField, Min(0f)] public float GrindBalanceSpeedDriftMultiplier { get; private set; } = 0.85f;
        [field: SerializeField, Min(0f)] public float GrindBalanceCurvatureInfluence { get; private set; } = 9f;
        [field: SerializeField, Min(0.1f)] public float GrindBalanceDriftSharpness { get; private set; } = 4.5f;
        [field: SerializeField] public Vector2 GrindBalanceDriftRetargetTime { get; private set; } = new(0.45f, 1.05f);
        [field: SerializeField, Min(0.25f)] public float GrindBalanceFailureThreshold { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float GrindBalanceFailureLockoutDuration { get; private set; } = 2.5f;
        [field: SerializeField, Min(0f)] public float GrindBalanceFailureLaunchHorizontalSpeed { get; private set; } = 18f;
        [field: SerializeField, Min(0f)] public float GrindBalanceFailureLaunchVerticalSpeed { get; private set; } = 12f;
        [field: SerializeField, Range(0f, 1f)] public float GrindBalanceFailureLaunchTangentJitter { get; private set; } = 0.3f;

        [field: Header("Traversal Timing")]
        [field: SerializeField, Min(0f)] public float TraversalActivationWindow { get; private set; } = 0.2f;

    }
}
