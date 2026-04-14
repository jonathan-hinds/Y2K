using Race.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Race.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(OwnerAnticipatedNetworkTransform))]
    public sealed class NetworkPlayerAvatar : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerRig playerRig;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerAnimationDriver animationDriver;
        [SerializeField] private PlayerAnimator playerAnimator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private MousePlaneAimer mousePlaneAimer;
        [SerializeField] private PlayerCameraTargetDriver cameraTargetDriver;
        [SerializeField] private PlayerFootIkController footIkController;
        [SerializeField] private PlayerSlopeAlignment slopeAlignment;
        [SerializeField] private PlayerVisualGroundingModeController groundingModeController;
        [SerializeField] private PlayerHeadlightAim headlightAim;
        [SerializeField] private Light headlight;
        [SerializeField] private OwnerAnticipatedNetworkTransform anticipatedNetworkTransform;

        [Header("Remote Presentation")]
        [SerializeField] private float remoteFacingSharpness = 18f;
        [SerializeField] private float remoteHeadlightPitch = 12f;

        private readonly NetworkVariable<NetworkPlayerVisualState> visualState = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector3> spawnPosition = new(writePerm: NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Quaternion> spawnRotation = new(writePerm: NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ushort> jumpStartedSequence = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ushort> jumpReleasedSequence = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ushort> landedSequence = new(writePerm: NetworkVariableWritePermission.Owner);

        private MultiplayerSessionController sessionController;
        private Transform visualRoot;
        private Transform headlightTransform;

        public PlayerInputReader InputReader => inputReader;
        public PlayerRig PlayerRig => playerRig;
        public PlayerMotor PlayerMotor => playerMotor;

        private void Awake()
        {
            CacheReferences();
        }

        public override void OnNetworkSpawn()
        {
            CacheReferences();
            sessionController = FindFirstObjectByType<MultiplayerSessionController>();
            ApplySpawnPose();

            jumpStartedSequence.OnValueChanged += HandleJumpStartedSequenceChanged;
            jumpReleasedSequence.OnValueChanged += HandleJumpReleasedSequenceChanged;
            landedSequence.OnValueChanged += HandleLandedSequenceChanged;

            if (IsOwner)
            {
                ConfigureAsLocallyControlled();
                SubscribeMotorEvents(true);
                sessionController?.RegisterLocalNetworkPlayer(this);
                PushVisualState(force: true);
            }
            else
            {
                ConfigureAsRemoteReplica();
                ApplyRemoteVisualState(visualState.Value, true);
            }
        }

        public override void OnNetworkDespawn()
        {
            jumpStartedSequence.OnValueChanged -= HandleJumpStartedSequenceChanged;
            jumpReleasedSequence.OnValueChanged -= HandleJumpReleasedSequenceChanged;
            landedSequence.OnValueChanged -= HandleLandedSequenceChanged;

            if (IsOwner)
            {
                SubscribeMotorEvents(false);
                sessionController?.UnregisterLocalNetworkPlayer(this);
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                PushVisualState(force: false);
            }
            else
            {
                ApplyRemoteVisualState(visualState.Value, false);
            }
        }

        public void SetGameplayInputBlocked(bool blocked)
        {
            if (inputReader != null)
            {
                inputReader.InputBlocked = blocked;
            }
        }

        public void SetServerSpawnPose(Vector3 position, Quaternion rotation)
        {
            spawnPosition.Value = position;
            spawnRotation.Value = rotation;
        }

        private void CacheReferences()
        {
            if (playerRig == null)
            {
                playerRig = GetComponent<PlayerRig>();
            }

            if (inputReader == null)
            {
                inputReader = GetComponent<PlayerInputReader>();
            }

            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }

            if (animationDriver == null)
            {
                animationDriver = GetComponent<PlayerAnimationDriver>();
            }

            if (playerAnimator == null)
            {
                playerAnimator = GetComponent<PlayerAnimator>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (mousePlaneAimer == null)
            {
                mousePlaneAimer = GetComponent<MousePlaneAimer>();
            }

            if (cameraTargetDriver == null)
            {
                cameraTargetDriver = GetComponent<PlayerCameraTargetDriver>();
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

            if (anticipatedNetworkTransform == null)
            {
                anticipatedNetworkTransform = GetComponent<OwnerAnticipatedNetworkTransform>();
            }

            if (playerRig != null)
            {
                visualRoot = playerRig.VisualRoot != null ? playerRig.VisualRoot : transform;
            }
            else
            {
                visualRoot = transform;
            }

            if (headlightAim == null && playerRig != null && playerRig.CameraTarget != null)
            {
                headlightAim = playerRig.CameraTarget.GetComponentInChildren<PlayerHeadlightAim>(true);
            }

            if (headlight == null && playerRig != null && playerRig.CameraTarget != null)
            {
                headlight = playerRig.CameraTarget.GetComponentInChildren<Light>(true);
            }

            headlightTransform = headlightAim != null ? headlightAim.transform : (headlight != null ? headlight.transform : null);
        }

        private void ApplySpawnPose()
        {
            Vector3 position = spawnPosition.Value;
            Quaternion rotation = spawnRotation.Value == default ? transform.rotation : spawnRotation.Value;

            if (playerMotor != null)
            {
                playerMotor.SnapToPose(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            if (anticipatedNetworkTransform != null)
            {
                anticipatedNetworkTransform.AnticipateMove(position);
                anticipatedNetworkTransform.AnticipateRotate(rotation);
            }
        }

        private void ConfigureAsLocallyControlled()
        {
            SetBehaviourEnabled(playerInput, true);
            SetBehaviourEnabled(inputReader, true);
            SetBehaviourEnabled(playerMotor, true);
            SetBehaviourEnabled(animationDriver, true);
            SetBehaviourEnabled(mousePlaneAimer, true);
            SetBehaviourEnabled(cameraTargetDriver, true);
            SetBehaviourEnabled(footIkController, true);
            SetBehaviourEnabled(slopeAlignment, true);
            SetBehaviourEnabled(groundingModeController, true);
            SetBehaviourEnabled(headlightAim, true);

            if (characterController != null && !characterController.enabled)
            {
                characterController.enabled = true;
            }

            if (inputReader != null)
            {
                inputReader.InputBlocked = false;
            }
        }

        private void ConfigureAsRemoteReplica()
        {
            SetBehaviourEnabled(playerInput, false);
            SetBehaviourEnabled(inputReader, false);
            SetBehaviourEnabled(playerMotor, false);
            SetBehaviourEnabled(animationDriver, false);
            SetBehaviourEnabled(mousePlaneAimer, false);
            SetBehaviourEnabled(cameraTargetDriver, false);
            SetBehaviourEnabled(footIkController, false);
            SetBehaviourEnabled(slopeAlignment, false);
            SetBehaviourEnabled(groundingModeController, false);
            SetBehaviourEnabled(headlightAim, false);

            if (characterController != null && characterController.enabled)
            {
                characterController.enabled = false;
            }
        }

        private void SubscribeMotorEvents(bool subscribe)
        {
            if (playerMotor == null)
            {
                return;
            }

            if (subscribe)
            {
                playerMotor.JumpStarted += HandleOwnerJumpStarted;
                playerMotor.JumpReleased += HandleOwnerJumpReleased;
                playerMotor.Landed += HandleOwnerLanded;
            }
            else
            {
                playerMotor.JumpStarted -= HandleOwnerJumpStarted;
                playerMotor.JumpReleased -= HandleOwnerJumpReleased;
                playerMotor.Landed -= HandleOwnerLanded;
            }
        }

        private void HandleOwnerJumpStarted()
        {
            if (IsOwner)
            {
                jumpStartedSequence.Value++;
            }
        }

        private void HandleOwnerJumpReleased()
        {
            if (IsOwner)
            {
                jumpReleasedSequence.Value++;
            }
        }

        private void HandleOwnerLanded()
        {
            if (IsOwner)
            {
                landedSequence.Value++;
            }
        }

        private void HandleJumpStartedSequenceChanged(ushort previousValue, ushort newValue)
        {
            if (IsOwner || previousValue == newValue || playerAnimator == null)
            {
                return;
            }

            playerAnimator.TriggerJumpStart();
        }

        private void HandleJumpReleasedSequenceChanged(ushort previousValue, ushort newValue)
        {
            if (IsOwner || previousValue == newValue || playerAnimator == null)
            {
                return;
            }

            playerAnimator.TriggerJumpRelease();
        }

        private void HandleLandedSequenceChanged(ushort previousValue, ushort newValue)
        {
            if (IsOwner || previousValue == newValue || playerAnimator == null)
            {
                return;
            }

            playerAnimator.TriggerLand();
        }

        private void PushVisualState(bool force)
        {
            if (!IsOwner || playerMotor == null || animationDriver == null)
            {
                return;
            }

            NetworkPlayerVisualState nextState = NetworkPlayerVisualState.From(animationDriver.CurrentState, playerMotor.FacingForward);
            if (!force && visualState.Value.Equals(nextState))
            {
                return;
            }

            visualState.Value = nextState;
        }

        private void ApplyRemoteVisualState(NetworkPlayerVisualState state, bool immediate)
        {
            if (playerAnimator != null)
            {
                playerAnimator.ApplyState(state.ToAnimationState(), 0.08f, 0.05f);
            }

            if (visualRoot != null)
            {
                Quaternion targetRotation = state.ToFacingRotation();
                if (immediate)
                {
                    visualRoot.rotation = targetRotation;
                }
                else
                {
                    float blend = 1f - Mathf.Exp(-remoteFacingSharpness * Time.deltaTime);
                    visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRotation, blend);
                }
            }

            if (headlightTransform != null)
            {
                Quaternion targetRotation = state.ToFacingRotation() * Quaternion.Euler(remoteHeadlightPitch, 0f, 0f);
                if (immediate)
                {
                    headlightTransform.rotation = targetRotation;
                }
                else
                {
                    float blend = 1f - Mathf.Exp(-remoteFacingSharpness * Time.deltaTime);
                    headlightTransform.rotation = Quaternion.Slerp(headlightTransform.rotation, targetRotation, blend);
                }
            }
        }

        private static void SetBehaviourEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
    }
}
