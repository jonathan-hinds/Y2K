using System.Collections.Generic;
using Race.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerScoreHudPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpeedometerTheme theme;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private RectTransform floatingScoreLayer;
        [SerializeField] private FloatingScoreText floatingScorePrefab;

        [Header("Binding")]
        [SerializeField] private bool autoBindLocalPlayer = true;

        [Header("Styling")]
        [SerializeField] private string scorePrefix = "SCORE";
        [SerializeField, Min(0.1f)] private float scoreFontSizeMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float popupFontSizeMultiplier = 0.92f;

        [Header("Score Animation")]
        [SerializeField, Min(1f)] private float minimumCountUpRate = 120f;
        [SerializeField, Min(0f)] private float countUpSharpness = 8f;

        [Header("Popup Motion")]
        [SerializeField, Min(0f)] private float popupHorizontalVelocity = 110f;
        [SerializeField, Min(0f)] private float popupUpwardVelocity = 145f;
        [SerializeField] private Vector2 popupScreenOffset = new Vector2(0f, 42f);

        private readonly Stack<FloatingScoreText> popupPool = new Stack<FloatingScoreText>();
        private PlayerTrickScoreController boundController;
        private float displayedScore;
        private int targetScore;
        private int lastRenderedScore = int.MinValue;

        private void Awake()
        {
            ApplyLabelStyle();
            TryBindController();
        }

        private void OnEnable()
        {
            ApplyLabelStyle();
            TryBindController();
            RefreshScoreLabel(force: true);
        }

        private void OnDisable()
        {
            BindController(null);
        }

        private void Update()
        {
            if (autoBindLocalPlayer)
            {
                if (!HasValidBinding())
                {
                    BindController(null);
                }

                if (boundController == null)
                {
                    TryBindController();
                }
            }

            UpdateDisplayedScore();
        }

        private void ApplyLabelStyle()
        {
            if (scoreLabel == null)
            {
                return;
            }

            SpeedometerTextStyler.Apply(scoreLabel, theme, scoreFontSizeMultiplier, TextAnchor.UpperLeft);
        }

        private void TryBindController()
        {
            if (!autoBindLocalPlayer || boundController != null)
            {
                return;
            }

            PlayerTrickScoreController[] controllers = FindObjectsByType<PlayerTrickScoreController>(FindObjectsSortMode.None);
            for (int index = 0; index < controllers.Length; index++)
            {
                PlayerTrickScoreController candidate = controllers[index];
                if (!IsLocalControllerCandidate(candidate))
                {
                    continue;
                }

                BindController(candidate);
                return;
            }
        }

        private bool HasValidBinding()
        {
            return IsLocalControllerCandidate(boundController);
        }

        private static bool IsLocalControllerCandidate(PlayerTrickScoreController candidate)
        {
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                return false;
            }

            bool isOfflineLocal = !candidate.IsSpawned
                && candidate.PlayerMotor != null
                && candidate.PlayerMotor.enabled
                && candidate.PlayerMotor.gameObject.activeInHierarchy;

            return candidate.IsOwner || isOfflineLocal;
        }

        private void BindController(PlayerTrickScoreController controller)
        {
            if (boundController == controller)
            {
                return;
            }

            if (boundController != null)
            {
                boundController.ScoreChanged -= HandleScoreChanged;
                boundController.LocalPopupRequested -= HandleLocalPopupRequested;
            }

            boundController = controller;

            if (boundController != null)
            {
                boundController.ScoreChanged += HandleScoreChanged;
                boundController.LocalPopupRequested += HandleLocalPopupRequested;
            }

            targetScore = boundController != null ? boundController.CurrentScore : 0;
            displayedScore = targetScore;
            RefreshScoreLabel(force: true);
        }

        private void HandleScoreChanged(int score)
        {
            targetScore = Mathf.Max(0, score);
        }

        private void UpdateDisplayedScore()
        {
            float clampedTargetScore = Mathf.Max(0, targetScore);
            if (displayedScore >= clampedTargetScore)
            {
                displayedScore = clampedTargetScore;
                RefreshScoreLabel(force: false);
                return;
            }

            float difference = clampedTargetScore - displayedScore;
            float scaledRate = Mathf.Max(minimumCountUpRate, difference * countUpSharpness);
            displayedScore = Mathf.Min(clampedTargetScore, displayedScore + (scaledRate * Time.unscaledDeltaTime));
            RefreshScoreLabel(force: false);
        }

        private void RefreshScoreLabel(bool force)
        {
            if (scoreLabel == null)
            {
                return;
            }

            int renderedScore = Mathf.RoundToInt(displayedScore);
            if (!force && renderedScore == lastRenderedScore)
            {
                return;
            }

            lastRenderedScore = renderedScore;
            scoreLabel.text = string.Format("{0} {1}", scorePrefix, renderedScore);
        }

        private void HandleLocalPopupRequested(PlayerTrickScoreController.ScorePopupRequest popupRequest)
        {
            if (floatingScoreLayer == null || floatingScorePrefab == null || theme == null)
            {
                return;
            }

            Camera currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return;
            }

            Vector3 screenPosition = currentCamera.WorldToScreenPoint(popupRequest.WorldPosition);
            if (screenPosition.z <= 0f)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    floatingScoreLayer,
                    screenPosition,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
            Vector2 launchVelocity = new Vector2(horizontalDirection * popupHorizontalVelocity, popupUpwardVelocity);
            FloatingScoreText popup = GetOrCreatePopup(floatingScoreLayer);
            popup.Play(
                popupRequest.Amount,
                theme,
                localPoint + popupScreenOffset,
                launchVelocity,
                popupFontSizeMultiplier,
                ReturnPopupToPool);
        }

        private FloatingScoreText GetOrCreatePopup(RectTransform parent)
        {
            FloatingScoreText popup = popupPool.Count > 0 ? popupPool.Pop() : Instantiate(floatingScorePrefab, parent);
            popup.transform.SetParent(parent, false);
            popup.gameObject.SetActive(true);
            return popup;
        }

        private void ReturnPopupToPool(FloatingScoreText popup)
        {
            if (popup == null)
            {
                return;
            }

            popup.gameObject.SetActive(false);
            popupPool.Push(popup);
        }
    }
}
