using UnityEngine;

namespace Race.Player
{
    public enum PlayerVisualGroundingMode
    {
        None = 0,
        SlopeAlignment = 1,
        FootIk = 2
    }

    [DisallowMultipleComponent]
    public sealed class PlayerVisualGroundingModeController : MonoBehaviour
    {
        [SerializeField] private PlayerVisualGroundingMode mode = PlayerVisualGroundingMode.SlopeAlignment;
        [SerializeField] private PlayerSlopeAlignment slopeAlignment;
        [SerializeField] private PlayerFootIkController footIkController;

        private bool isApplyingMode;

        public PlayerVisualGroundingMode Mode
        {
            get => mode;
            set
            {
                mode = value;
                ApplyMode();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyMode();
        }

        private void Reset()
        {
            ResolveReferences();
            ApplyMode();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyMode();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            ApplyMode();
        }
#endif

        private void ResolveReferences()
        {
            if (slopeAlignment == null)
            {
                slopeAlignment = GetComponent<PlayerSlopeAlignment>();
            }

            if (footIkController == null)
            {
                footIkController = GetComponent<PlayerFootIkController>();
            }
        }

        private void ApplyMode()
        {
            if (isApplyingMode)
            {
                return;
            }

            isApplyingMode = true;
            if (slopeAlignment != null)
            {
                slopeAlignment.AlignToGround = mode == PlayerVisualGroundingMode.SlopeAlignment;
            }

            if (footIkController != null)
            {
                footIkController.EnableFootIk = mode == PlayerVisualGroundingMode.FootIk;
            }
            isApplyingMode = false;
        }

        public void SyncModeFromComponents()
        {
            ResolveReferences();

            bool slopeEnabled = slopeAlignment != null && slopeAlignment.AlignToGround;
            bool footIkEnabled = footIkController != null && footIkController.EnableFootIk;

            if (slopeEnabled && !footIkEnabled)
            {
                mode = PlayerVisualGroundingMode.SlopeAlignment;
                return;
            }

            if (footIkEnabled && !slopeEnabled)
            {
                mode = PlayerVisualGroundingMode.FootIk;
                return;
            }

            if (!slopeEnabled && !footIkEnabled)
            {
                mode = PlayerVisualGroundingMode.None;
            }
        }
    }
}
