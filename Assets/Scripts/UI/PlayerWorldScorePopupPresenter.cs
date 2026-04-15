using System.Collections.Generic;
using Race.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerWorldScorePopupPresenter : MonoBehaviour
    {
        private sealed class WorldPopupInstance
        {
            public RectTransform Root;
            public FloatingScoreText Popup;
        }

        [Header("References")]
        [SerializeField] private PlayerTrickScoreController scoreController;
        [SerializeField] private SpeedometerTheme theme;
        [SerializeField] private RectTransform popupLayer;
        [SerializeField] private FloatingScoreText floatingScorePrefab;

        [Header("Styling")]
        [SerializeField, Min(0.1f)] private float popupFontSizeMultiplier = 3.28f;

        [Header("Popup Motion")]
        [SerializeField, Min(0f)] private float popupHorizontalVelocity = 16f;
        [SerializeField, Min(0f)] private float popupUpwardVelocity = 40f;
        [SerializeField] private Vector3 popupWorldOffset = new Vector3(0f, 1.8f, 0f);

        private readonly Stack<WorldPopupInstance> popupPool = new Stack<WorldPopupInstance>();
        private readonly Dictionary<FloatingScoreText, WorldPopupInstance> popupLookup = new Dictionary<FloatingScoreText, WorldPopupInstance>();

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
            if (floatingScorePrefab == null || theme == null)
            {
                return;
            }

            float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
            Vector2 launchVelocity = new Vector2(horizontalDirection * popupHorizontalVelocity, popupUpwardVelocity);

            WorldPopupInstance popupInstance = GetOrCreatePopup();
            popupInstance.Root.position = popupRequest.WorldPosition + popupWorldOffset;
            popupInstance.Root.rotation = Quaternion.identity;

            popupInstance.Popup.Play(
                popupRequest.Amount,
                theme,
                Vector2.zero,
                launchVelocity,
                popupFontSizeMultiplier,
                ReturnPopupToPool);
        }

        private WorldPopupInstance GetOrCreatePopup()
        {
            WorldPopupInstance popupInstance = popupPool.Count > 0 ? popupPool.Pop() : CreatePopupInstance();
            popupInstance.Root.gameObject.SetActive(true);
            return popupInstance;
        }

        private void ReturnPopupToPool(FloatingScoreText popup)
        {
            if (popup == null || !popupLookup.TryGetValue(popup, out WorldPopupInstance popupInstance))
            {
                return;
            }

            popupInstance.Root.gameObject.SetActive(false);
            popupPool.Push(popupInstance);
        }

        private WorldPopupInstance CreatePopupInstance()
        {
            var rootObject = new GameObject("WorldScorePopup", typeof(RectTransform), typeof(Canvas), typeof(CameraFacingBillboard));
            rootObject.transform.SetParent(null, false);

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.sizeDelta = ResolvePopupCanvasSize();
            rootRect.localScale = ResolvePopupCanvasScale();

            Canvas canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;

            FloatingScoreText popup = Instantiate(floatingScorePrefab, rootRect);
            popup.transform.SetParent(rootRect, false);

            var popupInstance = new WorldPopupInstance
            {
                Root = rootRect,
                Popup = popup
            };

            popupLookup[popup] = popupInstance;
            rootObject.SetActive(false);
            return popupInstance;
        }

        private Vector2 ResolvePopupCanvasSize()
        {
            if (popupLayer != null)
            {
                return popupLayer.rect.size;
            }

            RectTransform popupRect = floatingScorePrefab != null ? floatingScorePrefab.transform as RectTransform : null;
            return popupRect != null ? popupRect.rect.size : new Vector2(180f, 64f);
        }

        private Vector3 ResolvePopupCanvasScale()
        {
            if (popupLayer != null)
            {
                Canvas parentCanvas = popupLayer.GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    return parentCanvas.transform.localScale;
                }
            }

            return Vector3.one * 0.01f;
        }
    }
}
