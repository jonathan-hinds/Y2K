using Race.Player;
using UnityEngine;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerGrindBalancePresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private GrindBalanceDisplay balanceDisplay;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Binding")]
        [SerializeField] private bool autoFindPlayerMotor = true;

        [Header("Visibility")]
        [SerializeField] private bool hideWhenInactive = true;
        [SerializeField, Min(0f)] private float hiddenAlpha = 0f;
        [SerializeField, Min(0f)] private float visibleAlpha = 1f;

        private void Awake()
        {
            ResolveReferences();
            TryBindPlayerMotor();
            ApplyVisibility(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            TryBindPlayerMotor();
            Refresh();
        }

        private void Update()
        {
            if (autoFindPlayerMotor)
            {
                if (!LocalPlayerMotorResolver.IsLocalPlayerMotorCandidate(playerMotor))
                {
                    playerMotor = null;
                }

                if (playerMotor == null)
                {
                    TryBindPlayerMotor();
                }
            }

            Refresh();
        }

        public void Bind(PlayerMotor motor)
        {
            playerMotor = motor;
            Refresh();
        }

        private void Refresh()
        {
            bool hasFeature = playerMotor != null && playerMotor.GrindBalanceEnabled;
            bool shouldShow = hasFeature && playerMotor != null && playerMotor.IsGrinding;

            if (balanceDisplay != null && hasFeature)
            {
                balanceDisplay.SetState(
                    playerMotor.GrindBalanceNormalized,
                    playerMotor.GrindBalanceSafeZoneNormalized);
            }

            ApplyVisibility(shouldShow);
        }

        private void ApplyVisibility(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            bool shouldHide = hideWhenInactive && !visible;
            canvasGroup.alpha = shouldHide ? hiddenAlpha : visibleAlpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ResolveReferences()
        {
            if (balanceDisplay == null)
            {
                balanceDisplay = GetComponent<GrindBalanceDisplay>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void TryBindPlayerMotor()
        {
            if (LocalPlayerMotorResolver.IsLocalPlayerMotorCandidate(playerMotor))
            {
                return;
            }

            playerMotor = LocalPlayerMotorResolver.FindLocalPlayerMotor();
        }
    }
}
