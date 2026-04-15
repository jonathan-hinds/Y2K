using System;
using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class FloatingScoreText : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation")]
        [SerializeField, Min(0.05f)] private float lifetime = 1f;
        [SerializeField, Min(0f)] private float fadeStartDelay = 0.18f;
        [SerializeField, Min(0f)] private float velocityDamping = 7f;
        [SerializeField, Min(0.01f)] private float spawnScaleMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float settleScaleMultiplier = 1f;

        private Action<FloatingScoreText> completedCallback;
        private Vector2 velocity;
        private float elapsed;
        private float sizeMultiplier = 1f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            completedCallback = null;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition += velocity * deltaTime;
                float dampingBlend = 1f - Mathf.Exp(-velocityDamping * deltaTime);
                velocity = Vector2.Lerp(velocity, Vector2.zero, dampingBlend);
            }

            float normalizedLifetime = lifetime <= Mathf.Epsilon ? 1f : Mathf.Clamp01(elapsed / lifetime);
            float scaleBlend = 1f - Mathf.Exp(-18f * deltaTime);
            float targetScale = Mathf.Lerp(spawnScaleMultiplier, settleScaleMultiplier, normalizedLifetime) * sizeMultiplier;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, scaleBlend);

            if (canvasGroup != null)
            {
                if (elapsed <= fadeStartDelay)
                {
                    canvasGroup.alpha = 1f;
                }
                else
                {
                    float fadeDuration = Mathf.Max(0.01f, lifetime - fadeStartDelay);
                    float fadeT = Mathf.Clamp01((elapsed - fadeStartDelay) / fadeDuration);
                    canvasGroup.alpha = 1f - fadeT;
                }
            }

            if (elapsed < lifetime)
            {
                return;
            }

            completedCallback?.Invoke(this);
        }

        public void Play(int amount, SpeedometerTheme theme, Vector2 anchoredPosition, Vector2 initialVelocity, float fontSizeMultiplier, Action<FloatingScoreText> onComplete)
        {
            ResolveReferences();
            elapsed = 0f;
            velocity = initialVelocity;
            completedCallback = onComplete;
            sizeMultiplier = Mathf.Max(0.1f, fontSizeMultiplier);

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = anchoredPosition;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            transform.localScale = Vector3.one * (spawnScaleMultiplier * sizeMultiplier);

            if (scoreLabel != null)
            {
                SpeedometerTextStyler.Apply(scoreLabel, theme, fontSizeMultiplier, TextAnchor.MiddleCenter);
                scoreLabel.text = amount >= 0 ? $"+{amount}" : amount.ToString();
            }
        }

        private void ResolveReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (scoreLabel == null)
            {
                scoreLabel = GetComponent<Text>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
