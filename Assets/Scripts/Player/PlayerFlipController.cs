using System;
using Race.Scoring;
using Unity.Netcode;
using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerRig))]
    [RequireComponent(typeof(PlayerTrickScoreController))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerFlipController : NetworkBehaviour
    {
        public const string ForwardFlipTrickId = "flip_forward";
        public const string BackwardFlipTrickId = "flip_backward";

        private enum FlipDirection : sbyte
        {
            None = 0,
            Forward = 1,
            Backward = -1
        }

        private struct CrashEventState : INetworkSerializable, IEquatable<CrashEventState>
        {
            public ushort Sequence;
            public Vector3 CrashPosition;
            public Quaternion CrashRotation;
            public Vector3 CrashVelocity;
            public Vector3 RespawnPosition;
            public Quaternion RespawnRotation;
            public sbyte DirectionSign;

            public bool Equals(CrashEventState other)
            {
                return Sequence == other.Sequence
                    && CrashPosition.Equals(other.CrashPosition)
                    && CrashRotation.Equals(other.CrashRotation)
                    && CrashVelocity.Equals(other.CrashVelocity)
                    && RespawnPosition.Equals(other.RespawnPosition)
                    && RespawnRotation.Equals(other.RespawnRotation)
                    && DirectionSign == other.DirectionSign;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Sequence);
                serializer.SerializeValue(ref CrashPosition);
                serializer.SerializeValue(ref CrashRotation);
                serializer.SerializeValue(ref CrashVelocity);
                serializer.SerializeValue(ref RespawnPosition);
                serializer.SerializeValue(ref RespawnRotation);
                serializer.SerializeValue(ref DirectionSign);
            }
        }

        [Header("References")]
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerRig playerRig;
        [SerializeField] private PlayerTrickScoreController scoreController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerAnimationDriver animationDriver;
        [SerializeField] private PlayerAnimator playerAnimator;
        [SerializeField] private PlayerFootIkController footIkController;
        [SerializeField] private PlayerSlopeAlignment slopeAlignment;
        [SerializeField] private PlayerVisualGroundingModeController groundingModeController;
        [SerializeField] private PlayerCameraTargetDriver cameraTargetDriver;

        [Header("Flip Tuning")]
        [SerializeField, Min(45f)] private float flipSpeedDegreesPerSecond = 540f;
        [SerializeField, Range(0.1f, 1f)] private float verticalInputThreshold = 0.5f;
        [SerializeField, Range(1f, 45f)] private float landedAngleTolerance = 15f;

        [Header("Crash")]
        [SerializeField, Min(0.1f)] private float crashDurationSeconds = 1.35f;
        [SerializeField, Min(0f)] private float crashImpulse = 5.5f;
        [SerializeField, Min(0.1f)] private float respawnHeightOffset = 0.2f;
        [SerializeField, Min(0.5f)] private float respawnProbeHeight = 3f;
        [SerializeField, Min(0.5f)] private float respawnProbeDistance = 8f;
        [SerializeField] private LayerMask respawnGroundMask = ~0;

        private readonly NetworkVariable<short> visualPitchCentiDegrees = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<CrashEventState> lastCrashEvent = new(writePerm: NetworkVariableWritePermission.Owner);

        private GameObject crashRagdollInstance;
        private RuntimeHumanoidRagdoll crashRagdoll;
        private Transform flipPivot;
        private Quaternion flipPivotBaseLocalRotation = Quaternion.identity;
        private bool hasFlipPivotBaseLocalRotation;
        private bool attemptedFlip;
        private FlipDirection activeDirection;
        private float accumulatedRotationDegrees;
        private float visualPitchDegrees;
        private bool wasGrounded = true;
        private bool crashActive;
        private float crashTimer;
        private Vector3 crashRespawnPosition;
        private Quaternion crashRespawnRotation;
        private bool cachedInputBlocked;
        private bool cachedMotorEnabled;
        private bool cachedCharacterControllerEnabled;
        private bool cachedAnimationDriverEnabled;
        private bool cachedAnimatorEnabled;
        private bool cachedFootIkEnabled;
        private bool cachedSlopeAlignmentEnabled;
        private bool cachedGroundingModeEnabled;

        private void Awake()
        {
            ResolveReferences();
            EnsureFlipPivot();
            CacheFlipPivotBaseLocalRotation();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureFlipPivot();
            CacheFlipPivotBaseLocalRotation();
            ApplyVisualPitch(visualPitchDegrees);
            wasGrounded = playerMotor == null || playerMotor.IsGrounded;
        }

        public override void OnNetworkSpawn()
        {
            visualPitchCentiDegrees.OnValueChanged += HandleVisualPitchChanged;
            lastCrashEvent.OnValueChanged += HandleCrashEventChanged;
            ApplyRemotePitchIfNeeded();
        }

        public override void OnNetworkDespawn()
        {
            visualPitchCentiDegrees.OnValueChanged -= HandleVisualPitchChanged;
            lastCrashEvent.OnValueChanged -= HandleCrashEventChanged;
        }

        private void Update()
        {
            if (crashActive)
            {
                UpdateCrashTimer();
                return;
            }

            if (ShouldSimulateLocally())
            {
                UpdateLocalFlipState();
            }
            else
            {
                ApplyRemotePitchIfNeeded();
            }
        }

        private void LateUpdate()
        {
            if (!crashActive)
            {
                ApplyVisualPitch(visualPitchDegrees);
            }
        }

        private void ResolveReferences()
        {
            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }

            if (playerRig == null)
            {
                playerRig = GetComponent<PlayerRig>();
            }

            if (scoreController == null)
            {
                scoreController = GetComponent<PlayerTrickScoreController>();
            }

            if (inputReader == null)
            {
                inputReader = GetComponent<PlayerInputReader>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (animationDriver == null)
            {
                animationDriver = GetComponent<PlayerAnimationDriver>();
            }

            if (playerAnimator == null)
            {
                playerAnimator = GetComponent<PlayerAnimator>();
            }

            if (footIkController == null)
            {
                footIkController = GetComponent<PlayerFootIkController>();
            }

            if (slopeAlignment == null)
            {
                slopeAlignment = GetComponent<PlayerSlopeAlignment>();
            }

            if (groundingModeController == null)
            {
                groundingModeController = GetComponent<PlayerVisualGroundingModeController>();
            }

            if (cameraTargetDriver == null)
            {
                cameraTargetDriver = GetComponent<PlayerCameraTargetDriver>();
            }
        }

        private void UpdateLocalFlipState()
        {
            if (playerMotor == null || inputReader == null)
            {
                return;
            }

            bool grounded = playerMotor.IsGrounded;
            if (grounded)
            {
                if (!wasGrounded)
                {
                    ResolveLanding();
                }
                else if (!attemptedFlip)
                {
                    ResetVisualRotation();
                }
            }
            else
            {
                UpdateAirborneFlip();
            }

            PublishVisualPitch();
            wasGrounded = grounded;
        }

        private void UpdateAirborneFlip()
        {
            FlipDirection requestedDirection = ResolveRequestedDirection();
            if (activeDirection == FlipDirection.None && requestedDirection != FlipDirection.None)
            {
                activeDirection = requestedDirection;
                attemptedFlip = true;
            }

            if (activeDirection == FlipDirection.None || requestedDirection != activeDirection)
            {
                return;
            }

            float rotationDelta = flipSpeedDegreesPerSecond * Time.deltaTime;
            accumulatedRotationDegrees += rotationDelta;
            visualPitchDegrees = Mathf.DeltaAngle(0f, visualPitchDegrees + rotationDelta * (int)activeDirection);
        }

        private void ResolveLanding()
        {
            if (!attemptedFlip)
            {
                ResetVisualRotation();
                return;
            }

            int landedFlipCount = Mathf.Max(0, Mathf.RoundToInt(accumulatedRotationDegrees / 360f));
            bool landedUpright = Mathf.Abs(Mathf.DeltaAngle(visualPitchDegrees, 0f)) <= landedAngleTolerance;
            bool landedCleanly = landedUpright && landedFlipCount > 0;
            if (landedCleanly)
            {
                AwardCompletedFlips(landedFlipCount);
                ResetAttemptState();
                return;
            }

            CrashEventState crashEvent = BuildCrashEventState();
            StartCrashPresentation(crashEvent);
            if (IsSpawned && IsOwner)
            {
                lastCrashEvent.Value = crashEvent;
            }
        }

        private void AwardCompletedFlips(int flipCount)
        {
            if (scoreController == null || flipCount <= 0)
            {
                return;
            }

            string trickId = activeDirection == FlipDirection.Backward
                ? BackwardFlipTrickId
                : ForwardFlipTrickId;

            for (int index = 0; index < flipCount; index++)
            {
                scoreController.TryAwardTrick(trickId);
            }
        }

        private CrashEventState BuildCrashEventState()
        {
            Vector3 crashPosition = transform.position;
            Quaternion crashRotation = transform.rotation;
            Vector3 respawnPosition = ResolveRespawnPosition(crashPosition);
            Quaternion respawnRotation = ResolveRespawnRotation();
            Vector3 crashVelocity = playerMotor != null ? playerMotor.WorldVelocity : Vector3.zero;

            return new CrashEventState
            {
                Sequence = (ushort)(lastCrashEvent.Value.Sequence + 1),
                CrashPosition = crashPosition,
                CrashRotation = crashRotation,
                CrashVelocity = crashVelocity,
                RespawnPosition = respawnPosition,
                RespawnRotation = respawnRotation,
                DirectionSign = (sbyte)activeDirection
            };
        }

        private Vector3 ResolveRespawnPosition(Vector3 crashPosition)
        {
            Vector3 probeOrigin = crashPosition + Vector3.up * respawnProbeHeight;
            if (Physics.Raycast(
                    probeOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    respawnProbeDistance,
                    respawnGroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * respawnHeightOffset;
            }

            return crashPosition + Vector3.up * respawnHeightOffset;
        }

        private Quaternion ResolveRespawnRotation()
        {
            Vector3 facingForward = playerMotor != null ? playerMotor.FacingForward : transform.forward;
            facingForward = Vector3.ProjectOnPlane(facingForward, Vector3.up);
            if (facingForward.sqrMagnitude <= 0.0001f)
            {
                facingForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            if (facingForward.sqrMagnitude <= 0.0001f)
            {
                facingForward = Vector3.forward;
            }

            return Quaternion.LookRotation(facingForward.normalized, Vector3.up);
        }

        private void StartCrashPresentation(CrashEventState crashEvent)
        {
            crashActive = true;
            crashTimer = crashDurationSeconds;
            crashRespawnPosition = crashEvent.RespawnPosition;
            crashRespawnRotation = crashEvent.RespawnRotation;

            ResetAttemptState();
            SetGameplayPresentationEnabled(false);
            transform.SetPositionAndRotation(crashEvent.CrashPosition, crashEvent.CrashRotation);
            SpawnCrashRagdoll(crashEvent);
            SetLiveVisualVisible(false);
        }

        private void UpdateCrashTimer()
        {
            crashTimer -= Time.deltaTime;
            if (crashTimer > 0f)
            {
                return;
            }

            FinishCrashPresentation();
        }

        private void FinishCrashPresentation()
        {
            crashActive = false;
            DestroyCrashRagdoll();

            if (playerMotor != null)
            {
                playerMotor.SnapToPose(crashRespawnPosition, crashRespawnRotation);
            }
            else
            {
                transform.SetPositionAndRotation(crashRespawnPosition, crashRespawnRotation);
            }

            ResetVisualRotation();
            SetLiveVisualVisible(true);
            SetGameplayPresentationEnabled(true);
            cameraTargetDriver?.SnapImmediately();
            RefreshLocalCameraRig();
            PublishVisualPitch(forceZero: true);
            wasGrounded = playerMotor == null || playerMotor.IsGrounded;
        }

        private void SetGameplayPresentationEnabled(bool enabled)
        {
            if (!enabled)
            {
                cachedInputBlocked = inputReader != null && inputReader.InputBlocked;
                cachedMotorEnabled = playerMotor != null && playerMotor.enabled;
                cachedCharacterControllerEnabled = characterController != null && characterController.enabled;
                cachedAnimationDriverEnabled = animationDriver != null && animationDriver.enabled;
                cachedAnimatorEnabled = playerAnimator != null && playerAnimator.Animator != null && playerAnimator.Animator.enabled;
                cachedFootIkEnabled = footIkController != null && footIkController.enabled;
                cachedSlopeAlignmentEnabled = slopeAlignment != null && slopeAlignment.enabled;
                cachedGroundingModeEnabled = groundingModeController != null && groundingModeController.enabled;
            }

            if (inputReader != null)
            {
                inputReader.InputBlocked = enabled ? cachedInputBlocked : true;
            }

            if (playerMotor != null)
            {
                playerMotor.enabled = enabled && cachedMotorEnabled;
            }

            if (characterController != null)
            {
                characterController.enabled = enabled && cachedCharacterControllerEnabled;
            }

            if (animationDriver != null)
            {
                animationDriver.enabled = enabled && cachedAnimationDriverEnabled;
            }

            if (footIkController != null)
            {
                footIkController.enabled = enabled && cachedFootIkEnabled;
            }

            if (slopeAlignment != null)
            {
                slopeAlignment.enabled = enabled && cachedSlopeAlignmentEnabled;
            }

            if (groundingModeController != null)
            {
                groundingModeController.enabled = enabled && cachedGroundingModeEnabled;
            }

            if (playerAnimator != null && playerAnimator.Animator != null)
            {
                playerAnimator.Animator.enabled = enabled && cachedAnimatorEnabled;
                if (enabled)
                {
                    playerAnimator.Animator.Rebind();
                    playerAnimator.Animator.Update(0f);
                }
            }
        }

        private FlipDirection ResolveRequestedDirection()
        {
            if (inputReader == null || !inputReader.TrickModifierHeld)
            {
                return FlipDirection.None;
            }

            float verticalInput = inputReader.MoveInput.y;
            if (verticalInput >= verticalInputThreshold)
            {
                return FlipDirection.Forward;
            }

            if (verticalInput <= -verticalInputThreshold)
            {
                return FlipDirection.Backward;
            }

            return FlipDirection.None;
        }

        private void PublishVisualPitch(bool forceZero = false)
        {
            if (IsSpawned && IsOwner)
            {
                visualPitchCentiDegrees.Value = QuantizePitch(forceZero ? 0f : visualPitchDegrees);
            }
        }

        private void ApplyRemotePitchIfNeeded()
        {
            if (ShouldSimulateLocally())
            {
                return;
            }

            visualPitchDegrees = DequantizePitch(visualPitchCentiDegrees.Value);
        }

        private void HandleVisualPitchChanged(short previousValue, short newValue)
        {
            if (ShouldSimulateLocally())
            {
                return;
            }

            visualPitchDegrees = DequantizePitch(newValue);
        }

        private void HandleCrashEventChanged(CrashEventState previousValue, CrashEventState newValue)
        {
            if (previousValue.Sequence == newValue.Sequence || ShouldSimulateLocally())
            {
                return;
            }

            StartCrashPresentation(newValue);
        }

        private void ResetAttemptState()
        {
            attemptedFlip = false;
            activeDirection = FlipDirection.None;
            accumulatedRotationDegrees = 0f;
            ResetVisualRotation();
        }

        private void ResetVisualRotation()
        {
            visualPitchDegrees = 0f;
            ApplyVisualPitch(0f);
        }

        private void EnsureFlipPivot()
        {
            Transform modelRoot = playerRig != null ? playerRig.ModelRoot : null;
            if (modelRoot == null)
            {
                return;
            }

            if (flipPivot != null)
            {
                return;
            }

            Transform pivotParent = playerRig != null && playerRig.VisualRoot != null
                ? playerRig.VisualRoot
                : modelRoot.parent;
            if (pivotParent == null)
            {
                return;
            }

            Vector3 pivotWorldPosition = ResolveFlipPivotWorldPosition(modelRoot);
            var pivotObject = new GameObject("FlipPivot");
            flipPivot = pivotObject.transform;
            flipPivot.SetParent(pivotParent, false);
            flipPivot.position = pivotWorldPosition;
            flipPivot.rotation = pivotParent.rotation;
            flipPivot.localScale = Vector3.one;

            modelRoot.SetParent(flipPivot, true);
        }

        private void CacheFlipPivotBaseLocalRotation()
        {
            if (flipPivot == null)
            {
                return;
            }

            flipPivotBaseLocalRotation = flipPivot.localRotation;
            hasFlipPivotBaseLocalRotation = true;
        }

        private void ApplyVisualPitch(float pitchDegrees)
        {
            if (flipPivot == null)
            {
                return;
            }

            if (!hasFlipPivotBaseLocalRotation)
            {
                CacheFlipPivotBaseLocalRotation();
            }

            flipPivot.localRotation = flipPivotBaseLocalRotation * Quaternion.Euler(pitchDegrees, 0f, 0f);
        }

        private Vector3 ResolveFlipPivotWorldPosition(Transform modelRoot)
        {
            if (playerAnimator != null && playerAnimator.Animator != null && playerAnimator.Animator.isHuman)
            {
                Transform hips = playerAnimator.Animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    return hips.position;
                }
            }

            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int index = 1; index < renderers.Length; index++)
                {
                    combinedBounds.Encapsulate(renderers[index].bounds);
                }

                return combinedBounds.center;
            }

            return modelRoot.position;
        }

        private void SetLiveVisualVisible(bool visible)
        {
            Transform modelRoot = playerRig != null ? playerRig.ModelRoot : null;
            if (modelRoot != null)
            {
                modelRoot.gameObject.SetActive(visible);
            }
        }

        private void SpawnCrashRagdoll(CrashEventState crashEvent)
        {
            DestroyCrashRagdoll();

            Transform sourceModelRoot = playerRig != null ? playerRig.ModelRoot : null;
            if (sourceModelRoot == null)
            {
                return;
            }

            crashRagdollInstance = Instantiate(sourceModelRoot.gameObject, sourceModelRoot.position, sourceModelRoot.rotation);
            crashRagdollInstance.name = sourceModelRoot.gameObject.name + "_CrashRagdoll";
            crashRagdollInstance.SetActive(true);

            Animator ragdollAnimator = crashRagdollInstance.GetComponentInChildren<Animator>(true);
            if (ragdollAnimator != null)
            {
                ragdollAnimator.enabled = false;
            }

            crashRagdoll = new RuntimeHumanoidRagdoll(ragdollAnimator, crashRagdollInstance.transform);
            if (!crashRagdoll.IsAvailable)
            {
                DestroyCrashRagdoll();
                return;
            }

            Vector3 impulseDirection = new Vector3(0f, 0.35f, -crashEvent.DirectionSign);
            crashRagdoll.Enable(crashEvent.CrashVelocity, impulseDirection, crashImpulse);
        }

        private void DestroyCrashRagdoll()
        {
            crashRagdoll?.Disable();
            crashRagdoll = null;

            if (crashRagdollInstance != null)
            {
                Destroy(crashRagdollInstance);
                crashRagdollInstance = null;
            }
        }

        private void RefreshLocalCameraRig()
        {
            if (!ShouldSimulateLocally())
            {
                return;
            }

            PlayerCinemachineCameraRig cameraRig = FindFirstObjectByType<PlayerCinemachineCameraRig>();
            if (cameraRig != null)
            {
                cameraRig.SetTarget(playerRig, inputReader, playerMotor);
            }
        }

        private bool ShouldSimulateLocally()
        {
            if (IsSpawned)
            {
                return IsOwner;
            }

            return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        }

        private static short QuantizePitch(float pitchDegrees)
        {
            return (short)Mathf.RoundToInt(Mathf.DeltaAngle(0f, pitchDegrees) * 100f);
        }

        private static float DequantizePitch(short serializedPitch)
        {
            return serializedPitch / 100f;
        }
    }
}
