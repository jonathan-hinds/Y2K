using System.Collections.Generic;
using Race.Scoring;
using UnityEngine;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerWorldScorePopupPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerTrickScoreController scoreController;
        [SerializeField] private SpeedometerTheme theme;
        [SerializeField] private RectTransform popupLayer;
        [SerializeField] private FloatingScoreText floatingScorePrefab;

        [Header("Styling")]
        [SerializeField, Min(0.1f)] private float popupFontSizeMultiplier = 0.82f;

        [Header("Popup Motion")]
        [SerializeField, Min(0f)] private float popupHorizontalVelocity = 16f;
        [SerializeField, Min(0f)] private float popupUpwardVelocity = 40f;
        [SerializeField] private Vector2 popupLocalOffset = new Vector2(0f, 18f);

        private readonly Stack<FloatingScoreText> popupPool = new Stack<FloatingScoreText>();

        private void Awake()
        {
            if (scoreController == null)
            {
                scoreController = GetComponent<PlayerTrickScoreController>();
            }
        }

        private void OnEnable()
        {
            if (scoreController == null)
            {
                scoreController = GetComponent<PlayerTrickScoreController>();
            }

            if (scoreController != null)
            {
                scoreController.RemotePopupRequested += HandleRemotePopupRequested;
            }
        }

        private void OnDisable()
        {
            if (scoreController != null)
            {
                scoreController.RemotePopupRequested -= HandleRemotePopupRequested;
            }
        }

        private void HandleRemotePopupRequested(PlayerTrickScoreController.ScorePopupRequest popupRequest)
        {
            if (popupLayer == null || floatingScorePrefab == null || theme == null)
            {
                return;
            }

            Vector3 localWorldPosition = popupLayer.InverseTransformPoint(popupRequest.WorldPosition);
            Vector2 anchoredPosition = new Vector2(localWorldPosition.x, localWorldPosition.y) + popupLocalOffset;
            float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
            Vector2 launchVelocity = new Vector2(horizontalDirection * popupHorizontalVelocity, popupUpwardVelocity);

            FloatingScoreText popup = GetOrCreatePopup();
            popup.Play(
                popupRequest.Amount,
                theme,
                anchoredPosition,
                launchVelocity,
                popupFontSizeMultiplier,
                ReturnPopupToPool);
        }

        private FloatingScoreText GetOrCreatePopup()
        {
            FloatingScoreText popup = popupPool.Count > 0 ? popupPool.Pop() : Instantiate(floatingScorePrefab, popupLayer);
            popup.transform.SetParent(popupLayer, false);
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
