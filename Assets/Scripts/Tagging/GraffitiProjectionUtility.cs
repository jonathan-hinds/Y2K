using System.Collections.Generic;
using UnityEngine;

namespace Race.Tagging
{
    public readonly struct GraffitiSurfaceHitSample
    {
        public GraffitiSurfaceHitSample(Renderer renderer, int materialIndex, Vector3 point, Vector3 normal, Vector3 rayDirection, Vector2 uv)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
            Point = point;
            Normal = normal;
            RayDirection = rayDirection;
            Uv = uv;
        }

        public Renderer Renderer { get; }
        public int MaterialIndex { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Vector3 RayDirection { get; }
        public Vector2 Uv { get; }
    }

    public readonly struct GraffitiProjectionVolume
    {
        public GraffitiProjectionVolume(
            string sceneName,
            Vector3 center,
            Vector3 direction,
            Vector3 up,
            Vector3 halfExtents,
            float distance,
            Vector3 surfacePoint,
            Vector3 acquisitionCenter,
            Vector3 acquisitionHalfExtents,
            string targetRendererPath)
        {
            SceneName = sceneName ?? string.Empty;
            Center = center;
            Direction = direction;
            Up = up;
            HalfExtents = halfExtents;
            Distance = distance;
            SurfacePoint = surfacePoint;
            AcquisitionCenter = acquisitionCenter;
            AcquisitionHalfExtents = acquisitionHalfExtents;
            TargetRendererPath = targetRendererPath ?? string.Empty;
        }

        public string SceneName { get; }
        public Vector3 Center { get; }
        public Vector3 Direction { get; }
        public Vector3 Up { get; }
        public Vector3 HalfExtents { get; }
        public float Distance { get; }
        public Vector3 SurfacePoint { get; }
        public Vector3 AcquisitionCenter { get; }
        public Vector3 AcquisitionHalfExtents { get; }
        public string TargetRendererPath { get; }
        public Quaternion Rotation
        {
            get
            {
                if (Direction.sqrMagnitude <= 0.0001f)
                {
                    return Quaternion.identity;
                }

                Vector3 safeDirection = Direction.normalized;
                Vector3 safeUp = Vector3.ProjectOnPlane(Up, safeDirection);
                if (safeUp.sqrMagnitude <= 0.0001f)
                {
                    safeUp = Vector3.ProjectOnPlane(Vector3.up, safeDirection);
                }

                if (safeUp.sqrMagnitude <= 0.0001f)
                {
                    safeUp = Vector3.ProjectOnPlane(Vector3.right, safeDirection);
                }

                return Quaternion.LookRotation(-safeDirection, safeUp.normalized);
            }
        }
    }

    public static class GraffitiProjectionUtility
    {
        private static readonly Collider[] OverlapResults = new Collider[128];
        private static readonly RaycastHit[] BoxCastResults = new RaycastHit[64];
        private static readonly RaycastHit[] AimRaycastResults = new RaycastHit[32];
        private static readonly RaycastHit[] SampleRaycastResults = new RaycastHit[16];

        private const int SampleGridSize = 5;
        private const int MinimumSampleHits = 6;

        public static bool TryResolveProjection(
            Ray aimRay,
            Vector3 sprayOrigin,
            Transform ignoredRoot,
            LayerMask surfaceMask,
            float targetingDistance,
            float maxTagDistance,
            float acquisitionRadius,
            float projectionHeight,
            float projectionAspect,
            float projectionDepth,
            float forwardOffset,
            Vector3 preferredUp,
            out GraffitiProjectionVolume volume)
        {
            volume = default;
            Vector3 safeDirection = aimRay.direction.sqrMagnitude > 0.0001f ? aimRay.direction.normalized : Vector3.forward;
            Vector3 halfExtents = BuildHalfExtents(projectionHeight, projectionAspect, projectionDepth);
            Vector3 projectionUp = BuildUp(safeDirection, preferredUp);

            RaycastHit hit = default;
            if (!TryFindClosestHit(
                    aimRay.origin,
                    safeDirection,
                    targetingDistance,
                    ignoredRoot,
                    surfaceMask,
                    null,
                    AimRaycastResults,
                    out hit))
            {
                Quaternion rotation = Quaternion.LookRotation(-safeDirection, projectionUp);
                Vector3 castOrigin = sprayOrigin + safeDirection * halfExtents.z;
                int hitCount = Physics.BoxCastNonAlloc(
                    castOrigin,
                    new Vector3(
                        Mathf.Max(halfExtents.x, acquisitionRadius),
                        Mathf.Max(halfExtents.y, acquisitionRadius),
                        halfExtents.z),
                    safeDirection,
                    BoxCastResults,
                    rotation,
                    targetingDistance,
                    surfaceMask,
                    QueryTriggerInteraction.Ignore);
                if (hitCount <= 0)
                {
                    return false;
                }

                float nearestDistance = float.PositiveInfinity;
                for (int index = 0; index < hitCount; index++)
                {
                    RaycastHit candidate = BoxCastResults[index];
                    BoxCastResults[index] = default;
                    if (candidate.collider == null)
                    {
                        continue;
                    }

                    if (ignoredRoot != null && candidate.collider.transform.root == ignoredRoot)
                    {
                        continue;
                    }

                    if (candidate.distance >= nearestDistance)
                    {
                        continue;
                    }

                    nearestDistance = candidate.distance;
                    hit = candidate;
                }

                if (hit.collider == null)
                {
                    return false;
                }
            }

            float distanceFromSpray = Vector3.Dot(hit.point - sprayOrigin, safeDirection);
            if (distanceFromSpray <= 0f)
            {
                distanceFromSpray = Vector3.Distance(sprayOrigin, hit.point);
            }

            if (distanceFromSpray > maxTagDistance)
            {
                return false;
            }

            Vector3 projectionDirection = ResolveProjectionDirection(safeDirection, hit.normal);
            projectionUp = BuildUp(projectionDirection, preferredUp);
            Vector3 center = sprayOrigin + safeDirection * distanceFromSpray + safeDirection * forwardOffset;
            Vector3 acquisitionCenter = sprayOrigin + safeDirection * (distanceFromSpray * 0.5f);
            Vector3 acquisitionHalfExtents = new(
                Mathf.Max(halfExtents.x, acquisitionRadius),
                Mathf.Max(halfExtents.y, acquisitionRadius),
                Mathf.Max(0.05f, distanceFromSpray * 0.5f));
            Renderer targetRenderer = GraffitiTargetLocator.FindBestRenderer(hit.collider, hit.point);
            string targetRendererPath = string.Empty;
            if (targetRenderer != null && GraffitiTargetLocator.TryBuildPath(targetRenderer, out _, out string hierarchyPath))
            {
                targetRendererPath = hierarchyPath;
            }

            volume = new GraffitiProjectionVolume(
                hit.collider.gameObject.scene.name,
                center,
                projectionDirection,
                projectionUp,
                halfExtents,
                distanceFromSpray,
                hit.point,
                acquisitionCenter,
                acquisitionHalfExtents,
                targetRendererPath);
            return true;
        }

        public static GraffitiProjectionVolume BuildPreviewVolume(
            string sceneName,
            Vector3 sprayOrigin,
            Vector3 direction,
            float previewDistance,
            float projectionHeight,
            float projectionAspect,
            float projectionDepth,
            float forwardOffset,
            Vector3 preferredUp)
        {
            Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            Vector3 up = BuildUp(safeDirection, preferredUp);
            Vector3 center = sprayOrigin + safeDirection * previewDistance + safeDirection * forwardOffset;
            Vector3 halfExtents = BuildHalfExtents(projectionHeight, projectionAspect, projectionDepth);
            Vector3 acquisitionCenter = sprayOrigin + safeDirection * (previewDistance * 0.5f);
            Vector3 acquisitionHalfExtents = new(
                Mathf.Max(halfExtents.x, 0.05f),
                Mathf.Max(halfExtents.y, 0.05f),
                Mathf.Max(0.05f, previewDistance * 0.5f));
            return new GraffitiProjectionVolume(sceneName, center, safeDirection, up, halfExtents, previewDistance, center, acquisitionCenter, acquisitionHalfExtents, string.Empty);
        }

        public static int CollectSurfaceHitSamples(
            in GraffitiProjectionVolume volume,
            Vector3 sprayOrigin,
            Transform ignoredRoot,
            LayerMask surfaceMask,
            int sampleColumns,
            int sampleRows,
            ICollection<GraffitiSurfaceHitSample> samples,
            ICollection<Renderer> renderers = null,
            ICollection<Vector3> hitPoints = null,
            Renderer requiredRenderer = null)
        {
            if (sampleColumns <= 0 || sampleRows <= 0)
            {
                samples?.Clear();
                renderers?.Clear();
                hitPoints?.Clear();
                return 0;
            }

            samples?.Clear();
            renderers?.Clear();
            hitPoints?.Clear();

            Quaternion rotation = volume.Rotation;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            float maxDistance = Mathf.Max(
                volume.Distance + volume.HalfExtents.z * 4f,
                Vector3.Distance(sprayOrigin, volume.Center) + volume.HalfExtents.z * 4f);

            HashSet<int> seenRendererIds = renderers != null ? new HashSet<int>() : null;
            int hitCount = 0;

            for (int row = 0; row < sampleRows; row++)
            {
                float v01 = (row + 0.5f) / sampleRows;
                float v = v01 * 2f - 1f;
                for (int column = 0; column < sampleColumns; column++)
                {
                    float u01 = (column + 0.5f) / sampleColumns;
                    float u = u01 * 2f - 1f;
                    Vector3 projectorPoint = volume.Center
                        + right * (u * volume.HalfExtents.x)
                        + up * (v * volume.HalfExtents.y);
                    Vector3 rayDirection = projectorPoint - sprayOrigin;
                    float rayLength = rayDirection.magnitude;
                    if (rayLength <= 0.001f)
                    {
                        continue;
                    }

                    rayDirection /= rayLength;
                    if (!TryFindClosestHit(
                            sprayOrigin,
                            rayDirection,
                            maxDistance,
                            ignoredRoot,
                            surfaceMask,
                            volume.SceneName,
                            SampleRaycastResults,
                            out RaycastHit bestHit))
                    {
                        continue;
                    }

                    Renderer renderer = GraffitiTargetLocator.FindBestRenderer(bestHit.collider, bestHit.point);
                    if (renderer == null || (requiredRenderer != null && renderer != requiredRenderer))
                    {
                        continue;
                    }

                    int materialIndex = GraffitiTargetLocator.ResolveMaterialIndex(bestHit, renderer);
                    samples?.Add(new GraffitiSurfaceHitSample(renderer, materialIndex, bestHit.point, bestHit.normal, rayDirection, new Vector2(u01, v01)));
                    if (seenRendererIds != null && seenRendererIds.Add(renderer.GetInstanceID()))
                    {
                        renderers.Add(renderer);
                    }

                    hitPoints?.Add(bestHit.point);
                    hitCount++;
                }
            }

            return hitCount;
        }

        public static int CollectCandidateRenderers(
            in GraffitiProjectionVolume volume,
            Transform ignoredRoot,
            LayerMask surfaceMask,
            ICollection<Renderer> renderers,
            ICollection<Vector3> hitPoints = null)
        {
            if (renderers == null)
            {
                return 0;
            }

            renderers.Clear();
            hitPoints?.Clear();

            int colliderCount = Physics.OverlapBoxNonAlloc(
                volume.Center,
                volume.HalfExtents,
                OverlapResults,
                volume.Rotation,
                surfaceMask,
                QueryTriggerInteraction.Ignore);
            if (colliderCount <= 0)
            {
                return 0;
            }

            HashSet<int> seenRendererIds = new();
            for (int index = 0; index < colliderCount; index++)
            {
                Collider collider = OverlapResults[index];
                OverlapResults[index] = null;
                if (collider == null)
                {
                    continue;
                }

                if (ignoredRoot != null && collider.transform.root == ignoredRoot)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(volume.SceneName) && collider.gameObject.scene.name != volume.SceneName)
                {
                    continue;
                }

                Renderer renderer = GraffitiTargetLocator.FindBestRenderer(collider, volume.SurfacePoint);
                if (renderer == null || !seenRendererIds.Add(renderer.GetInstanceID()))
                {
                    continue;
                }

                renderers.Add(renderer);
                hitPoints?.Add(collider.ClosestPoint(volume.Center));
            }

            return renderers.Count;
        }

        public static Vector3[] GetBoxCorners(in GraffitiProjectionVolume volume)
        {
            Quaternion rotation = volume.Rotation;
            Vector3 extents = volume.HalfExtents;

            Vector3[] corners = new Vector3[8];
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 local = new(extents.x * x, extents.y * y, extents.z * z);
                        corners[index++] = volume.Center + rotation * local;
                    }
                }
            }

            return corners;
        }

        private static Vector3 BuildHalfExtents(float projectionHeight, float projectionAspect, float projectionDepth)
        {
            float safeHeight = Mathf.Max(0.1f, projectionHeight);
            float safeAspect = Mathf.Max(0.1f, projectionAspect);
            float safeDepth = Mathf.Max(0.05f, projectionDepth);
            return new Vector3(safeHeight * safeAspect * 0.5f, safeHeight * 0.5f, safeDepth * 0.5f);
        }

        private static int CollectSurfaceSamples(
            Vector3 sprayOrigin,
            Transform ignoredRoot,
            LayerMask surfaceMask,
            float targetingDistance,
            float maxTagDistance,
            Vector3 surfacePoint,
            Vector3 halfExtents,
            Vector3 right,
            Vector3 up,
            ref Vector3 averagedPoint,
            ref Vector3 averagedNormal)
        {
            Vector3 pointAccumulator = Vector3.zero;
            Vector3 normalAccumulator = Vector3.zero;
            int hitCount = 0;
            float maxDistance = Mathf.Max(targetingDistance, maxTagDistance + halfExtents.z * 2f);

            for (int y = 0; y < SampleGridSize; y++)
            {
                float v = SampleGridSize == 1 ? 0f : (y / (float)(SampleGridSize - 1)) * 2f - 1f;
                for (int x = 0; x < SampleGridSize; x++)
                {
                    float u = SampleGridSize == 1 ? 0f : (x / (float)(SampleGridSize - 1)) * 2f - 1f;
                    Vector3 samplePoint = surfacePoint + right * (u * halfExtents.x) + up * (v * halfExtents.y);
                    Vector3 rayDirection = samplePoint - sprayOrigin;
                    float rayLength = rayDirection.magnitude;
                    if (rayLength <= 0.001f)
                    {
                        continue;
                    }

                    rayDirection /= rayLength;
                    int sampleResultCount = Physics.RaycastNonAlloc(
                        sprayOrigin,
                        rayDirection,
                        SampleRaycastResults,
                        maxDistance,
                        surfaceMask,
                        QueryTriggerInteraction.Ignore);
                    if (sampleResultCount <= 0)
                    {
                        continue;
                    }

                    RaycastHit bestHit = default;
                    float bestDistance = float.PositiveInfinity;
                    for (int resultIndex = 0; resultIndex < sampleResultCount; resultIndex++)
                    {
                        RaycastHit candidate = SampleRaycastResults[resultIndex];
                        SampleRaycastResults[resultIndex] = default;
                        if (candidate.collider == null)
                        {
                            continue;
                        }

                        if (ignoredRoot != null && candidate.collider.transform.root == ignoredRoot)
                        {
                            continue;
                        }

                        if (candidate.distance >= bestDistance || candidate.distance > maxTagDistance + halfExtents.z)
                        {
                            continue;
                        }

                        bestDistance = candidate.distance;
                        bestHit = candidate;
                    }

                    if (bestHit.collider == null)
                    {
                        continue;
                    }

                    pointAccumulator += bestHit.point;
                    normalAccumulator += bestHit.normal;
                    hitCount++;
                }
            }

            if (hitCount > 0)
            {
                averagedPoint = pointAccumulator / hitCount;
                averagedNormal = normalAccumulator / hitCount;
            }

            return hitCount;
        }

        private static bool TryFindClosestHit(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            Transform ignoredRoot,
            LayerMask surfaceMask,
            string sceneName,
            RaycastHit[] hitBuffer,
            out RaycastHit bestHit)
        {
            bestHit = default;
            int sampleResultCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                maxDistance,
                surfaceMask,
                QueryTriggerInteraction.Ignore);
            if (sampleResultCount <= 0)
            {
                return false;
            }

            float bestDistance = float.PositiveInfinity;
            for (int resultIndex = 0; resultIndex < sampleResultCount; resultIndex++)
            {
                RaycastHit candidate = hitBuffer[resultIndex];
                hitBuffer[resultIndex] = default;
                if (candidate.collider == null)
                {
                    continue;
                }

                if (ignoredRoot != null && candidate.collider.transform.root == ignoredRoot)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(sceneName) && candidate.collider.gameObject.scene.name != sceneName)
                {
                    continue;
                }

                if (candidate.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = candidate.distance;
                bestHit = candidate;
            }

            return bestHit.collider != null;
        }

        private static Vector3 BuildUp(Vector3 direction, Vector3 preferredUp)
        {
            Vector3 up = Vector3.ProjectOnPlane(preferredUp, direction);
            if (up.sqrMagnitude <= 0.0001f)
            {
                up = Vector3.ProjectOnPlane(Vector3.up, direction);
            }

            if (up.sqrMagnitude <= 0.0001f)
            {
                up = Vector3.ProjectOnPlane(Vector3.right, direction);
            }

            return up.normalized;
        }

        private static Vector3 ResolveProjectionDirection(Vector3 fallbackDirection, Vector3 surfaceNormal)
        {
            Vector3 safeFallbackDirection = fallbackDirection.sqrMagnitude > 0.0001f
                ? fallbackDirection.normalized
                : Vector3.forward;
            if (surfaceNormal.sqrMagnitude <= 0.0001f)
            {
                return safeFallbackDirection;
            }

            Vector3 safeSurfaceNormal = surfaceNormal.normalized;
            return Vector3.Dot(safeSurfaceNormal, safeFallbackDirection) <= 0f
                ? -safeSurfaceNormal
                : safeSurfaceNormal;
        }
    }
}
