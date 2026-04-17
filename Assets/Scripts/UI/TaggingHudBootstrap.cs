using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    public static class TaggingHudBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureHudExists()
        {
            if (Object.FindFirstObjectByType<PlayerTaggingBarPresenter>() != null)
            {
                return;
            }

            Canvas canvas = FindTargetCanvas();
            if (canvas == null)
            {
                return;
            }

            GameObject root = new("TaggingBarHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(TaggingBarDisplay), typeof(PlayerTaggingBarPresenter));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, -150f);
            rootRect.sizeDelta = new Vector2(560f, 128f);

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject meter = new("Meter", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform meterRect = meter.GetComponent<RectTransform>();
            meterRect.SetParent(rootRect, false);
            meterRect.anchorMin = Vector2.zero;
            meterRect.anchorMax = Vector2.one;
            meterRect.offsetMin = Vector2.zero;
            meterRect.offsetMax = Vector2.zero;

            RawImage meterImage = meter.GetComponent<RawImage>();
            meterImage.raycastTarget = false;

            root.hideFlags = HideFlags.DontSave;
            meter.hideFlags = HideFlags.DontSave;
        }

        private static Canvas FindTargetCanvas()
        {
            GrindBalanceDisplay grindBalanceDisplay = Object.FindFirstObjectByType<GrindBalanceDisplay>();
            if (grindBalanceDisplay != null)
            {
                Canvas grindCanvas = grindBalanceDisplay.GetComponentInParent<Canvas>();
                if (grindCanvas != null)
                {
                    return grindCanvas;
                }
            }

            PlayerSpeedometerPresenter speedometerPresenter = Object.FindFirstObjectByType<PlayerSpeedometerPresenter>();
            if (speedometerPresenter != null)
            {
                Canvas speedometerCanvas = speedometerPresenter.GetComponentInParent<Canvas>();
                if (speedometerCanvas != null)
                {
                    return speedometerCanvas;
                }
            }

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                if (canvases[index] != null && canvases[index].renderMode != RenderMode.WorldSpace)
                {
                    return canvases[index];
                }
            }

            return null;
        }
    }
}
