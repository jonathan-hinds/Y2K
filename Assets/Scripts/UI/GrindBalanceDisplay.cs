using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class GrindBalanceDisplay : MonoBehaviour
    {
        private static readonly Vector2[] MeterPolygon =
        {
            new(0.01f, 0.28f),
            new(0.05f, 0.86f),
            new(0.22f, 0.82f),
            new(0.36f, 0.76f),
            new(0.47f, 0.73f),
            new(0.58f, 0.77f),
            new(0.78f, 0.78f),
            new(0.97f, 0.84f),
            new(1.00f, 0.83f),
            new(0.98f, 0.54f),
            new(0.98f, 0.18f),
            new(0.88f, 0.20f),
            new(0.74f, 0.27f),
            new(0.61f, 0.35f),
            new(0.49f, 0.39f),
            new(0.36f, 0.34f),
            new(0.22f, 0.25f),
            new(0.08f, 0.15f),
            new(0.01f, 0.11f)
        };

        [Header("References")]
        [SerializeField] private RawImage meterImage;
        [SerializeField] private RectTransform meterRect;
        [SerializeField] private SpeedometerTheme theme;

        [Header("Sizing")]
        [SerializeField] private Vector2 speedometerReferenceHudSize = new(420f, 280f);

        [Header("Marker")]
        [SerializeField] private Color markerColor = Color.white;
        [SerializeField] private Color markerOutlineColor = Color.black;
        [SerializeField, Min(1)] private int markerHalfWidthPixels = 1;
        [SerializeField, Min(1)] private int markerOutlinePixels = 3;

        [Header("Response")]
        [SerializeField, Min(0f)] private float responseSharpness = 16f;

        private Texture2D texture;
        private Color32[] pixels;
        private bool[] shapeMask;
        private float[] lateralMap;
        private Vector2Int cachedTextureSize;
        private float safeZoneNormalized = 0.2f;
        private float targetBalance;
        private float displayedBalance = float.MaxValue;
        private float lastRenderedBalance = float.MaxValue;
        private float lastRenderedSafeZone = float.MaxValue;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            RenderIfNeeded(force: true);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Initialize();
            RenderIfNeeded(force: true);
        }

        private void Update()
        {
            if (theme == null)
            {
                return;
            }

            float blend = responseSharpness <= Mathf.Epsilon
                ? 1f
                : 1f - Mathf.Exp(-responseSharpness * Time.unscaledDeltaTime);

            if (displayedBalance == float.MaxValue)
            {
                displayedBalance = targetBalance;
            }
            else
            {
                displayedBalance = Mathf.Lerp(displayedBalance, targetBalance, blend);
            }

            RenderIfNeeded(force: false);
        }

        public void SetState(float normalizedBalance, float normalizedSafeZone)
        {
            targetBalance = Mathf.Clamp(normalizedBalance, -1f, 1f);
            safeZoneNormalized = Mathf.Clamp(normalizedSafeZone, 0.01f, 0.95f);
        }

        private void Initialize()
        {
            if (meterImage == null)
            {
                meterImage = GetComponentInChildren<RawImage>();
            }

            if (meterRect == null && meterImage != null)
            {
                meterRect = meterImage.rectTransform;
            }

            if (theme == null || meterImage == null || meterRect == null)
            {
                return;
            }

            EnsureTexture();
        }

        private void EnsureTexture()
        {
            Vector2 rectSize = meterRect.rect.size;
            if (rectSize.x <= Mathf.Epsilon || rectSize.y <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 referenceHudSize = new(
                Mathf.Max(1f, speedometerReferenceHudSize.x),
                Mathf.Max(1f, speedometerReferenceHudSize.y));
            Vector2Int referenceTextureSize = new(
                Mathf.Max(16, theme.TextureSize.x),
                Mathf.Max(16, theme.TextureSize.y));
            Vector2 normalizedSize = new(
                rectSize.x / referenceHudSize.x,
                rectSize.y / referenceHudSize.y);
            Vector2Int requestedSize = new(
                Mathf.Max(32, Mathf.RoundToInt(referenceTextureSize.x * normalizedSize.x)),
                Mathf.Max(24, Mathf.RoundToInt(referenceTextureSize.y * normalizedSize.y)));

            if (texture != null && cachedTextureSize == requestedSize)
            {
                return;
            }

            cachedTextureSize = requestedSize;

            if (texture != null)
            {
                Destroy(texture);
            }

            texture = new Texture2D(requestedSize.x, requestedSize.y, TextureFormat.RGBA32, false)
            {
                name = "GrindBalance_Runtime",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            pixels = new Color32[requestedSize.x * requestedSize.y];
            shapeMask = new bool[pixels.Length];
            lateralMap = new float[pixels.Length];
            BuildGeometryCache(requestedSize.x, requestedSize.y);
            meterImage.texture = texture;
        }

        private void BuildGeometryCache(int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * width + x;
                    Vector2 uv = new((x + 0.5f) / width, (y + 0.5f) / height);
                    bool insideShape = PointInPolygon(uv, MeterPolygon);
                    shapeMask[pixelIndex] = insideShape;
                    lateralMap[pixelIndex] = insideShape ? Mathf.Lerp(-1f, 1f, uv.x) : 0f;
                }
            }
        }

        private void RenderIfNeeded(bool force)
        {
            if (theme == null || texture == null || pixels == null)
            {
                return;
            }

            if (!force
                && Mathf.Abs(displayedBalance - lastRenderedBalance) < 0.002f
                && Mathf.Abs(safeZoneNormalized - lastRenderedSafeZone) < 0.0001f)
            {
                return;
            }

            lastRenderedBalance = displayedBalance;
            lastRenderedSafeZone = safeZoneNormalized;
            RenderTexture();
        }

        private void RenderTexture()
        {
            int width = cachedTextureSize.x;
            int height = cachedTextureSize.y;
            Vector3[] currentErrors = new Vector3[width + 2];
            Vector3[] nextErrors = new Vector3[width + 2];

            for (int y = 0; y < height; y++)
            {
                System.Array.Clear(nextErrors, 0, nextErrors.Length);

                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * width + x;
                    if (!shapeMask[pixelIndex])
                    {
                        pixels[pixelIndex] = Color.clear;
                        continue;
                    }

                    float normalizedDistance = Mathf.Abs(lateralMap[pixelIndex]);
                    Vector3 desired = ToVector3(EvaluateGradient(normalizedDistance)) + currentErrors[x + 1];
                    desired = Clamp01(desired);
                    Color32 quantized = GetNearestPaletteColor(desired);
                    pixels[pixelIndex] = quantized;

                    Vector3 error = desired - ToVector3(quantized);
                    currentErrors[x + 2] += error * (7f / 16f);
                    nextErrors[x] += error * (3f / 16f);
                    nextErrors[x + 1] += error * (5f / 16f);
                    nextErrors[x + 2] += error * (1f / 16f);
                }

                Vector3[] temp = currentErrors;
                currentErrors = nextErrors;
                nextErrors = temp;
            }

            DrawMarker(width, height);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private Color EvaluateGradient(float normalizedDistance)
        {
            float clampedDistance = Mathf.Clamp01(normalizedDistance);
            if (clampedDistance <= safeZoneNormalized)
            {
                return theme.LowSpeedColor;
            }

            float outerBlend = Mathf.InverseLerp(safeZoneNormalized, 1f, clampedDistance);
            if (outerBlend <= 0.45f)
            {
                float blend = Mathf.InverseLerp(0f, 0.45f, outerBlend);
                return Color.Lerp(theme.LowSpeedColor, theme.MidSpeedColor, blend);
            }

            if (outerBlend <= 0.8f)
            {
                float blend = Mathf.InverseLerp(0.45f, 0.8f, outerBlend);
                return Color.Lerp(theme.MidSpeedColor, theme.HighSpeedColor, blend);
            }

            float maxBlend = Mathf.InverseLerp(0.8f, 1f, outerBlend);
            return Color.Lerp(theme.HighSpeedColor, theme.MaxSpeedColor, maxBlend);
        }

        private void DrawMarker(int width, int height)
        {
            float markerOutlineWidth = (markerOutlinePixels * 2f) / Mathf.Max(1, width);
            float markerWidth = (markerHalfWidthPixels * 2f) / Mathf.Max(1, width);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * width + x;
                    if (!shapeMask[pixelIndex])
                    {
                        continue;
                    }

                    float distanceToMarker = Mathf.Abs(lateralMap[pixelIndex] - displayedBalance);
                    if (distanceToMarker <= markerOutlineWidth)
                    {
                        pixels[pixelIndex] = markerOutlineColor;
                    }

                    if (distanceToMarker <= markerWidth)
                    {
                        pixels[pixelIndex] = markerColor;
                    }
                }
            }
        }

        private Color32 GetNearestPaletteColor(Vector3 color)
        {
            Color32[] palette =
            {
                theme.LowSpeedColor,
                theme.MidSpeedColor,
                theme.HighSpeedColor,
                theme.MaxSpeedColor
            };

            float bestDistance = float.MaxValue;
            Color32 bestColor = palette[0];
            for (int index = 0; index < palette.Length; index++)
            {
                Vector3 candidate = ToVector3(palette[index]);
                float distance = (candidate - color).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestColor = palette[index];
                }
            }

            return bestColor;
        }

        private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int index = 0, previous = polygon.Length - 1; index < polygon.Length; previous = index++)
            {
                Vector2 current = polygon[index];
                Vector2 prior = polygon[previous];
                bool intersects = ((current.y > point.y) != (prior.y > point.y))
                    && (point.x < (prior.x - current.x) * (point.y - current.y) / ((prior.y - current.y) + Mathf.Epsilon) + current.x);
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Vector3 ToVector3(Color color)
        {
            return new Vector3(color.r, color.g, color.b);
        }

        private static Vector3 Clamp01(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp01(value.x),
                Mathf.Clamp01(value.y),
                Mathf.Clamp01(value.z));
        }
    }
}
