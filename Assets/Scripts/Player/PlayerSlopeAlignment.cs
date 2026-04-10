using UnityEngine;

namespace Race.Player
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class PlayerSlopeAlignment : MonoBehaviour
    {
        [Header("Testing")]
        [SerializeField] private bool alignToGround = true;

        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerRig playerRig;
        [SerializeField] private Transform visualRoot;

        private Quaternion visualRootBaseLocalRotation = Quaternion.identity;
        private bool hasCachedInitialState;
        private bool wasAlignmentAppliedLastFrame;

        public bool AlignToGround
        {
            get => alignToGround;
            set
            {
                if (alignToGround == value)
                {
                    return;
                }

                alignToGround = value;
                if (!ShouldAlignVisualRoot())
                {
                    RestoreVisualRootRotation();
                }

                wasAlignmentAppliedLastFrame = false;
                NotifyGroundingModeController();
            }
        }

        public bool IsAlignmentActive => isActiveAndEnabled && ShouldAlignVisualRoot();

        private void Awake()
        {
            ResolveReferences();
            CacheInitialState();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (!ShouldAlignVisualRoot())
            {
                if (wasAlignmentAppliedLastFrame)
                {
                    RestoreVisualRootRotation();
                    wasAlignmentAppliedLastFrame = false;
                }

                return;
            }

            Vector3 desiredUp = playerMotor != null && playerMotor.HasStableGroundContact
                ? GetSafeGroundNormal()
                : Vector3.up;

            Vector3 desiredForward = playerMotor != null
                ? Vector3.ProjectOnPlane(playerMotor.FacingForward, desiredUp)
                : Vector3.ProjectOnPlane(visualRoot.forward, desiredUp);
            if (desiredForward.sqrMagnitude <= 0.0001f)
            {
                desiredForward = Vector3.ProjectOnPlane(transform.forward, desiredUp);
            }

            if (desiredForward.sqrMagnitude <= 0.0001f)
            {
                desiredForward = Vector3.forward;
            }

            Quaternion desiredVisualRotation = Quaternion.LookRotation(desiredForward.normalized, desiredUp);
            visualRoot.rotation = desiredVisualRotation;
            wasAlignmentAppliedLastFrame = true;
        }

        private void OnDisable()
        {
            RestoreVisualRootRotation();
            wasAlignmentAppliedLastFrame = false;
        }

        private void Reset()
        {
            ResolveReferences();
            CacheInitialState();
            NotifyGroundingModeController();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            CacheInitialState();
            NotifyGroundingModeController();
        }
#endif

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

            if (visualRoot == null && playerRig != null)
            {
                visualRoot = playerRig.VisualRoot;
            }

        }

        private void CacheInitialState()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            visualRootBaseLocalRotation = visualRoot.localRotation;
            hasCachedInitialState = true;
        }

        private bool HasRequiredReferences()
        {
            return playerMotor != null &&
                   visualRoot != null;
        }

        private bool ShouldAlignVisualRoot()
        {
            return alignToGround && HasRequiredReferences();
        }

        private void RestoreVisualRootRotation()
        {
            if (!hasCachedInitialState || visualRoot == null)
            {
                return;
            }

            visualRoot.localRotation = visualRootBaseLocalRotation;
        }

        private Vector3 GetSafeGroundNormal()
        {
            if (playerMotor == null)
            {
                return Vector3.up;
            }

            Vector3 groundNormal = playerMotor.StableGroundNormal;
            if (groundNormal.sqrMagnitude <= 0.0001f)
            {
                return Vector3.up;
            }

            return groundNormal.normalized;
        }

        private void NotifyGroundingModeController()
        {
            PlayerVisualGroundingModeController modeController = GetComponent<PlayerVisualGroundingModeController>();
            if (modeController != null)
            {
                modeController.SyncModeFromComponents();
            }
        }
    }
}
