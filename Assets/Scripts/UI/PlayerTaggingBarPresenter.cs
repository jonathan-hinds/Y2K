using Race.Player;
using Race.Tagging;
using UnityEngine;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerTaggingBarPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerTaggingController taggingController;
        [SerializeField] private TaggingBarDisplay taggingBarDisplay;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Binding")]
        [SerializeField] private bool autoFindLocalPlayer = true;

        [Header("Visibility")]
        [SerializeField] private bool hideWhenInactive = true;
        [SerializeField, Min(0f)] private float hiddenAlpha = 0f;
        [SerializeField, Min(0f)] private float visibleAlpha = 1f;

        private void Awake()
        {
            ResolveReferences();
            TryBindLocalPlayer();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveReferences();
            TryBindLocalPlayer();
            Refresh();
        }

        private void Update()
        {
            if (autoFindLocalPlayer && (taggingController == null || !taggingController.isActiveAndEnabled))
            {
                TryBindLocalPlayer();
            }

            Refresh();
        }

        private void ResolveReferences()
        {
            if (taggingBarDisplay == null)
            {
                taggingBarDisplay = GetComponent<TaggingBarDisplay>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void TryBindLocalPlayer()
        {
            PlayerMotor localPlayerMotor = LocalPlayerMotorResolver.FindLocalPlayerMotor();
            taggingController = localPlayerMotor != null
                ? localPlayerMotor.GetComponent<PlayerTaggingController>()
                : null;
        }

        private void Refresh()
        {
            bool shouldShow = taggingController != null && taggingController.IsTagging;
            if (taggingBarDisplay != null)
            {
                taggingBarDisplay.SetProgress(shouldShow ? taggingController.TagProgressNormalized : 0f);
            }

            if (canvasGroup == null)
            {
                return;
            }

            bool shouldHide = hideWhenInactive && !shouldShow;
            canvasGroup.alpha = shouldHide ? hiddenAlpha : visibleAlpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
