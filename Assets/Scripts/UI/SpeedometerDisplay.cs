using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class SpeedometerDisplay : MonoBehaviour
    {
        private static readonly Vector2[] OuterPolygon =
        {
            new(0.47f, 0.12f),
            new(0.27f, 0.11f),
            new(0.12f, 0.15f),
            new(0.05f, 0.25f),
            new(0.02f, 0.40f),
            new(0.03f, 0.57f),
            new(0.10f, 0.74f),
            new(0.23f, 0.86f),
            new(0.42f, 0.93f),
            new(0.63f, 0.96f),
            new(0.84f, 0.97f),
            new(0.97f, 0.97f),
            new(0.96f, 0.84f),
            new(0.92f, 0.70f),
            new(0.85f, 0.57f),
            new(0.78f, 0.42f),
            new(0.73f, 0.33f),
            new(0.66f, 0.40f),
            new(0.54f, 0.47f),
            new(0.49f, 0.47f)
        };

        private static readonly Vector2[] InnerCutout =
        {
            new(0.49f, 0.28f),
            new(0.34f, 0.29f),
            new(0.23f, 0.33f),
            new(0.17f, 0.40f),
            new(0.17f, 0.51f),
            new(0.22f, 0.60f),
            new(0.35f, 0.67f),
            new(0.53f, 0.69f),
            new(0.66f, 0.64f),
            new(0.73f, 0.56f),
            new(0.71f, 0.48f),
            new(0.61f, 0.38f)
        };

        private static readonly Vector2[] Centerline =
        {
            new(0.46f, 0.18f),
            new(0.30f, 0.17f),
            new(0.17f, 0.22f),
            new(0.08f, 0.36f),
            new(0.08f, 0.57f),
            new(0.16f, 0.74f),
            new(0.31f, 0.84f),
            new(0.51f, 0.89f),
            new(0.71f, 0.91f),
            new(0.88f, 0.92f),
            new(0.93f, 0.78f),
            new(0.90f, 0.58f),
            new(0.80f, 0.40f),
            new(0.69f, 0.33f)
        };

        [Header("References")]
        [SerializeField] private RawImage meterImage;
        [SerializeField] private Text speedLabel;
        [SerializeField] private SpeedometerTheme theme;

        private Texture2D texture;
        private Color32[] pixels;
        private bool[] shapeMask;
        private float[] progressMap;
        private float[] segmentLengths;
        private float totalCenterlineLength;
        private float targetSpeedMph;
        private float targetMaxSpeedMph = 1f;
        private float displayedSpeedMph;
        private float displayedProgress = -1f;
        private int displayedSpeedInteger = int.MinValue;
        private float lastRenderedProgress = -1f;
        private Vector2Int cachedTextureSize;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            RenderIfNeeded(force: true);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyThemeToLabel();
            }
        }

        private void Update()
        {
            if (theme == null)
            {
                return;
            }

            float blend = theme.ResponseSharpness <= Mathf.Epsilon
                ? 1f
                : 1f - Mathf.Exp(-theme.ResponseSharpness * Time.unscaledDeltaTime);

            displayedSpeedMph = Mathf.Lerp(displayedSpeedMph, targetSpeedMph, blend);
            float targetProgress = Mathf.Clamp01(targetSpeedMph / Mathf.Max(1f, targetMaxSpeedMph));
            float progress = Mathf.Lerp(Mathf.Max(0f, displayedProgress), targetProgress, blend);
            displayedProgress = Mathf.Clamp01(progress);

            RenderIfNeeded(force: false);
        }

        public void SetTargetSpeed(float currentSpeedMph, float maxSpeedMph)
        {
            targetSpeedMph = Mathf.Max(0f, currentSpeedMph);
            targetMaxSpeedMph = Mathf.Max(1f, maxSpeedMph);
        }

        private void Initialize()
        {
            if (meterImage == null)
            {
                meterImage = GetComponentInChildren<RawImage>();
            }

            if (speedLabel == null)
            {
                speedLabel = GetComponentInChildren<Text>();
            }

            if (theme == null || meterImage == null)
            {
                return;
            }

            ApplyThemeToLabel();
            EnsureTexture();
        }

        private void ApplyThemeToLabel()
        {
            if (theme == null || speedLabel == null)
            {
                return;
            }

            speedLabel.font = theme.SpeedFont;
            speedLabel.color = theme.NumberColor;
            speedLabel.fontSize = Mathf.RoundToInt(theme.NumberFontSize);
            speedLabel.alignment = TextAnchor.MiddleCenter;
            speedLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            speedLabel.verticalOverflow = VerticalWrapMode.Overflow;
            speedLabel.resizeTextForBestFit = false;

            Outline outline = speedLabel.GetComponent<Outline>();
            if (outline == null)
            {
                outline = speedLabel.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = theme.NumberOutlineColor;
            float outlineSize = Mathf.Lerp(1f, 8f, theme.NumberOutlineWidth);
            outline.effectDistance = new Vector2(outlineSize, -outlineSize);

            if (speedLabel.rectTransform != null)
            {
                speedLabel.rectTransform.anchoredPosition = theme.NumberOffset;
            }
        }

        private void EnsureTexture()
        {
            Vector2Int requestedSize = new(
                Mathf.Max(64, theme.TextureSize.x),
                Mathf.Max(64, theme.TextureSize.y));
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
                name = "Speedometer_Runtime",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            pixels = new Color32[requestedSize.x * requestedSize.y];
            shapeMask = new bool[pixels.Length];
            progressMap = new float[pixels.Length];
            BuildCenterlineLengths();
            BuildGeometryCache(requestedSize.x, requestedSize.y);

            meterImage.texture = texture;
        }

        private void BuildCenterlineLengths()
        {
            segmentLengths = new float[Centerline.Length];
            totalCenterlineLength = 0f;

            for (int index = 1; index < Centerline.Length; index++)
            {
                totalCenterlineLength += Vector2.Distance(Centerline[index - 1], Centerline[index]);
                segmentLengths[index] = totalCenterlineLength;
            }
        }

        private void BuildGeometryCache(int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * width + x;
                    Vector2 uv = new((x + 0.5f) / width, (y + 0.5f) / height);
                    bool insideOuter = PointInPolygon(uv, OuterPolygon);
                    bool insideInner = PointInPolygon(uv, InnerCutout);
                    bool insideShape = insideOuter && !insideInner;
                    shapeMask[pixelIndex] = insideShape;
                    progressMap[pixelIndex] = insideShape ? SampleCenterlineProgress(uv) : 0f;
                }
            }
        }

        private void RenderIfNeeded(bool force)
        {
            if (theme == null || texture == null || pixels == null)
            {
                return;
            }

            int speedValue = Mathf.RoundToInt(displayedSpeedMph);
            if (!force
                && Mathf.Abs(displayedProgress - lastRenderedProgress) < 0.0025f
                && speedValue == displayedSpeedInteger)
            {
                return;
            }

            displayedSpeedInteger = speedValue;
            lastRenderedProgress = displayedProgress;
            RenderTexture(displayedProgress);
            UpdateLabel(speedValue);
        }

        private void RenderTexture(float progress)
        {
            int width = cachedTextureSize.x;
            int height = cachedTextureSize.y;
            Vector3[] currentErrors = new Vector3[width + 2];
            Vector3[] nextErrors = new Vector3[width + 2];
            Color32 emptyColor = theme.EmptyColor;

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

                    float pathProgress = progressMap[pixelIndex];
                    if (pathProgress > progress)
                    {
                        pixels[pixelIndex] = emptyColor;
                        continue;
                    }

                    Vector3 desired = ToVector3(EvaluateGradient(pathProgress)) + currentErrors[x + 1];
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

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private void UpdateLabel(int speedValue)
        {
            if (speedLabel == null)
            {
                return;
            }

            speedLabel.text = string.Format("{0} MPH", Mathf.Max(0, speedValue));
        }

        private Color EvaluateGradient(float progress)
        {
            float yellowThreshold = Mathf.Clamp01(theme.YellowThreshold);
            float redThreshold = Mathf.Clamp(theme.RedThreshold, yellowThreshold + 0.01f, 1f);

            if (progress <= yellowThreshold)
            {
                float blend = Mathf.InverseLerp(0f, yellowThreshold, progress);
                return Color.Lerp(theme.LowSpeedColor, theme.MidSpeedColor, blend);
            }

            if (progress <= redThreshold)
            {
                float blend = Mathf.InverseLerp(yellowThreshold, redThreshold, progress);
                return Color.Lerp(theme.MidSpeedColor, theme.HighSpeedColor, blend);
            }

            float maxBlend = Mathf.InverseLerp(redThreshold, 1f, progress);
            return Color.Lerp(theme.HighSpeedColor, theme.MaxSpeedColor, maxBlend);
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

        private float SampleCenterlineProgress(Vector2 point)
        {
            float bestDistance = float.MaxValue;
            float distanceAlongSpline = 0f;

            for (int segmentIndex = 1; segmentIndex < Centerline.Length; segmentIndex++)
            {
                Vector2 start = Centerline[segmentIndex - 1];
                Vector2 end = Centerline[segmentIndex];
                Vector2 segment = end - start;
                float segmentLengthSqr = segment.sqrMagnitude;
                float projection = segmentLengthSqr > Mathf.Epsilon
                    ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSqr)
                    : 0f;
                Vector2 closestPoint = start + segment * projection;
                float distance = (closestPoint - point).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                float distanceToSegmentStart = segmentLengths[segmentIndex - 1];
                distanceAlongSpline = distanceToSegmentStart + Vector2.Distance(start, closestPoint);
            }

            return totalCenterlineLength <= Mathf.Epsilon ? 0f : distanceAlongSpline / totalCenterlineLength;
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
