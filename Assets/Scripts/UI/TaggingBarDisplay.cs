using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class TaggingBarDisplay : MonoBehaviour
    {
        private static readonly Vector2[] MeterPolygon =
        {
            new(0.01f, 0.47f),
            new(0.27f, 0.64f),
            new(0.43f, 0.54f),
            new(0.71f, 0.69f),
            new(0.98f, 0.66f),
            new(0.98f, 0.18f),
            new(0.72f, 0.36f),
            new(0.53f, 0.27f),
            new(0.41f, 0.18f),
            new(0.17f, 0.30f)
        };

        private static readonly Vector2[] Centerline =
        {
            new(0.04f, 0.45f),
            new(0.23f, 0.50f),
            new(0.42f, 0.37f),
            new(0.71f, 0.51f),
            new(0.95f, 0.42f)
        };

        [Header("References")]
        [SerializeField] private RawImage meterImage;
        [SerializeField] private RectTransform meterRect;

        [Header("Sizing")]
        [SerializeField] private Vector2 speedometerReferenceHudSize = new(420f, 280f);
        [SerializeField] private Vector2Int referenceTextureSize = new(128, 88);

        [Header("Palette")]
        [SerializeField] private Color emptyColor = new(0.03f, 0.03f, 0.03f, 0.55f);
        [SerializeField] private Color filledBlackColor = new(0.03f, 0.03f, 0.03f, 1f);
        [SerializeField] private Color redColor = new(0.93f, 0.18f, 0.10f, 1f);
        [SerializeField] private Color yellowColor = new(0.95f, 0.88f, 0.20f, 1f);
        [SerializeField] private Color greenColor = new(0.18f, 0.86f, 0.30f, 1f);
        [SerializeField, Min(0f)] private float responseSharpness = 18f;

        private Texture2D texture;
        private Color32[] pixels;
        private bool[] shapeMask;
        private float[] progressMap;
        private float[] segmentLengths;
        private float totalCenterlineLength;
        private Vector2Int cachedTextureSize;
        private float targetProgress;
        private float displayedProgress = -1f;
        private float lastRenderedProgress = -1f;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            RenderIfNeeded(true);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Initialize();
            RenderIfNeeded(true);
        }

        private void Update()
        {
            if (displayedProgress < 0f)
            {
                displayedProgress = targetProgress;
            }
            else
            {
                float blend = responseSharpness <= Mathf.Epsilon
                    ? 1f
                    : 1f - Mathf.Exp(-responseSharpness * Time.unscaledDeltaTime);
                displayedProgress = Mathf.Lerp(displayedProgress, targetProgress, blend);
            }

            RenderIfNeeded(false);
        }

        public void SetProgress(float normalizedProgress)
        {
            targetProgress = Mathf.Clamp01(normalizedProgress);
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

            if (meterImage == null || meterRect == null)
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

            Vector2 normalizedSize = new(
                rectSize.x / Mathf.Max(1f, speedometerReferenceHudSize.x),
                rectSize.y / Mathf.Max(1f, speedometerReferenceHudSize.y));
            Vector2Int requestedSize = new(
                Mathf.Max(24, Mathf.RoundToInt(referenceTextureSize.x * normalizedSize.x)),
                Mathf.Max(12, Mathf.RoundToInt(referenceTextureSize.y * normalizedSize.y)));

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
                name = "TaggingBar_Runtime",
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
                    bool insideShape = PointInPolygon(uv, MeterPolygon);
                    shapeMask[pixelIndex] = insideShape;
                    progressMap[pixelIndex] = insideShape ? SampleCenterlineProgress(uv) : 0f;
                }
            }
        }

        private void RenderIfNeeded(bool force)
        {
            if (texture == null || pixels == null)
            {
                return;
            }

            if (!force && Mathf.Abs(displayedProgress - lastRenderedProgress) < 0.002f)
            {
                return;
            }

            lastRenderedProgress = displayedProgress;
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

                    float pathProgress = progressMap[pixelIndex];
                    if (pathProgress > displayedProgress)
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

        private Color EvaluateGradient(float progress)
        {
            if (progress <= 0.33f)
            {
                return Color.Lerp(filledBlackColor, redColor, Mathf.InverseLerp(0f, 0.33f, progress));
            }

            if (progress <= 0.66f)
            {
                return Color.Lerp(redColor, yellowColor, Mathf.InverseLerp(0.33f, 0.66f, progress));
            }

            return Color.Lerp(yellowColor, greenColor, Mathf.InverseLerp(0.66f, 1f, progress));
        }

        private Color32 GetNearestPaletteColor(Vector3 color)
        {
            Color32[] palette =
            {
                filledBlackColor,
                redColor,
                yellowColor,
                greenColor
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
