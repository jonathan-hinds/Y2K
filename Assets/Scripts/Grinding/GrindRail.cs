using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Grinding
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Race/Grinding/Grind Rail")]
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(SplineExtrude))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GrindRail : MonoBehaviour
    {
        public readonly struct Sample
        {
            public Sample(Vector3 position, Vector3 tangent, Vector3 up, float t, float distanceToRail)
            {
                Position = position;
                Tangent = tangent;
                Up = up;
                T = t;
                DistanceToRail = distanceToRail;
            }

            public Vector3 Position { get; }
            public Vector3 Tangent { get; }
            public Vector3 Up { get; }
            public float T { get; }
            public float DistanceToRail { get; }
        }

        [Header("References")]
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private SplineExtrude splineExtrude;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Transform generatedColliderRoot;
        [SerializeField] private Material railMaterial;

        [Header("Visuals")]
        [SerializeField, Min(0.01f)] private float cableRadius = 0.08f;
        [SerializeField, Range(3, 12)] private int cableSides = 6;
        [SerializeField, Min(0.5f)] private float visualSegmentsPerUnit = 6f;
        [SerializeField] private bool cappedEnds = true;

        [Header("Collision")]
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.18f;
        [SerializeField, Min(0.25f)] private float collisionSegmentLength = 1.25f;
        [SerializeField] private bool autoRebuildInEditor = true;

        [Header("Traversal")]
        [SerializeField, Min(0f)] private float entrySpeedBoost = 10f;
        [SerializeField, Min(0f)] private float rideHeightOffset;
        [SerializeField, Min(1)] private int nearestPointResolution = 8;
        [SerializeField, Min(1)] private int nearestPointIterations = 3;

        public float EntrySpeedBoost => entrySpeedBoost;
        public float RideHeightOffset => rideHeightOffset;
        public float CollisionRadius => collisionRadius;
        public float Length { get; private set; }

#if UNITY_EDITOR
        private bool rebuildQueued;
#endif

        private void Awake()
        {
            CacheReferences();
            RefreshCachedLength();
            if (CanRunEditorAuthoring())
            {
                EnsureGeneratedColliderRoot();
            }
        }

        private void OnEnable()
        {
            CacheReferences();
            RefreshCachedLength();
        }

        private void Reset()
        {
            CacheReferences();
            AssignDefaultLayer();
            if (!CanRunEditorAuthoring())
            {
                return;
            }

            EnsureGeneratedColliderRoot();
            ApplyAuthoring();
        }

        private void OnValidate()
        {
            CacheReferences();
            RefreshCachedLength();
            if (!CanRunEditorAuthoring())
            {
                return;
            }

            EnsureGeneratedColliderRoot();

            if (!autoRebuildInEditor || Application.isPlaying)
            {
                return;
            }

            QueueApplyAuthoring();
        }

        [ContextMenu("Apply Grind Rail")]
        public void ApplyAuthoring()
        {
            if (!CanRunEditorAuthoring())
            {
                return;
            }

            CacheReferences();
            RefreshCachedLength();
            ConfigureExtrude();
            EnsureGeneratedColliderRoot();
            RebuildGeneratedColliders();
        }

        public bool TryGetNearestSample(Vector3 worldPoint, out Sample sample)
        {
            sample = default;
            if (splineContainer == null || splineContainer.Spline == null)
            {
                return false;
            }

            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            float distance = SplineUtility.GetNearestPoint(
                splineContainer.Spline,
                (float3)localPoint,
                out float3 nearest,
                out float t,
                nearestPointResolution,
                nearestPointIterations);

            if (!TryEvaluateSample(t, out Sample evaluated))
            {
                return false;
            }

            sample = new Sample(
                transform.TransformPoint((Vector3)nearest),
                evaluated.Tangent,
                evaluated.Up,
                t,
                distance);

            return true;
        }

        public bool TryEvaluateSample(float t, out Sample sample)
        {
            sample = default;
            if (splineContainer == null)
            {
                return false;
            }

            float normalizedT = Mathf.Clamp01(t);
            if (!splineContainer.Evaluate(normalizedT, out float3 position, out float3 tangent, out float3 upVector))
            {
                return false;
            }

            Vector3 tangentDirection = ((Vector3)tangent).normalized;
            if (tangentDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 upDirection = ((Vector3)upVector).normalized;
            if (upDirection.sqrMagnitude <= 0.0001f)
            {
                upDirection = Vector3.up;
            }

            sample = new Sample((Vector3)position, tangentDirection, upDirection, normalizedT, 0f);
            return true;
        }

        public bool TryAdvance(float fromT, float signedDistance, out Sample sample, out bool reachedEnd)
        {
            sample = default;
            reachedEnd = false;
            if (splineContainer == null)
            {
                return false;
            }

            float clampedFromT = Mathf.Clamp01(fromT);
            float3 nextPoint = SplineUtility.GetPointAtLinearDistance(
                splineContainer.Spline,
                clampedFromT,
                signedDistance,
                out float nextT);

            reachedEnd = Mathf.Abs(nextT - clampedFromT) <= 0.0001f && Mathf.Abs(signedDistance) > 0.001f;
            if (!TryEvaluateSample(nextT, out Sample evaluated))
            {
                return false;
            }

            sample = new Sample(transform.TransformPoint((Vector3)nextPoint), evaluated.Tangent, evaluated.Up, nextT, 0f);
            return true;
        }

        public Vector3 GetMountPoint(Sample sample, Vector3 rootOffsetFromProbe)
        {
            return sample.Position + (sample.Up * rideHeightOffset) + rootOffsetFromProbe;
        }

        public bool IsNearEnd(float t, float normalizedThreshold = 0.0025f)
        {
            float threshold = Mathf.Clamp01(normalizedThreshold);
            return t <= threshold || t >= 1f - threshold;
        }

        private void CacheReferences()
        {
            if (splineContainer == null)
            {
                splineContainer = GetComponent<SplineContainer>();
            }

            if (splineExtrude == null)
            {
                splineExtrude = GetComponent<SplineExtrude>();
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private void RefreshCachedLength()
        {
            Length = splineContainer != null ? Mathf.Max(0f, splineContainer.CalculateLength()) : 0f;
        }

        private void ConfigureExtrude()
        {
            if (splineExtrude == null)
            {
                return;
            }

            splineExtrude.Container = splineContainer;
            splineExtrude.Radius = cableRadius;
            splineExtrude.Sides = cableSides;
            splineExtrude.SegmentsPerUnit = visualSegmentsPerUnit;
            splineExtrude.Capped = cappedEnds;
            splineExtrude.RebuildOnSplineChange = true;
            splineExtrude.RebuildFrequency = 0;
            ApplyVisualDefaults();
            splineExtrude.Rebuild();
        }

        private void ApplyVisualDefaults()
        {
            if (meshRenderer == null)
            {
                return;
            }

            if (railMaterial != null)
            {
                meshRenderer.sharedMaterial = railMaterial;
                return;
            }

            if (meshRenderer.sharedMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return;
            }

            var fallbackMaterial = new Material(shader)
            {
                name = "GrindRail_Auto"
            };

            if (fallbackMaterial.HasProperty("_BaseColor"))
            {
                fallbackMaterial.SetColor("_BaseColor", new Color(0.08f, 0.09f, 0.11f, 1f));
            }

            if (fallbackMaterial.HasProperty("_Smoothness"))
            {
                fallbackMaterial.SetFloat("_Smoothness", 0.85f);
            }

            if (fallbackMaterial.HasProperty("_Metallic"))
            {
                fallbackMaterial.SetFloat("_Metallic", 0.9f);
            }

            meshRenderer.sharedMaterial = fallbackMaterial;
        }

        private void EnsureGeneratedColliderRoot()
        {
            if (!CanRunEditorAuthoring())
            {
                return;
            }

            if (generatedColliderRoot != null)
            {
                return;
            }

            Transform existingChild = transform.Find("_GeneratedRailColliders");
            if (existingChild != null)
            {
                generatedColliderRoot = existingChild;
                return;
            }

            var root = new GameObject("_GeneratedRailColliders");
            root.transform.SetParent(transform, false);
            root.layer = gameObject.layer;
            generatedColliderRoot = root.transform;
        }

        private void RebuildGeneratedColliders()
        {
            if (!CanRunEditorAuthoring())
            {
                return;
            }

            if (generatedColliderRoot == null || splineContainer == null)
            {
                return;
            }

            ClearGeneratedColliders();

            float splineLength = Mathf.Max(Length, splineContainer.CalculateLength());
            if (splineLength <= 0.001f)
            {
                return;
            }

            float segmentLength = Mathf.Max(0.05f, collisionSegmentLength);
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(splineLength / segmentLength));

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                float startDistance = splineLength * (segmentIndex / (float)segmentCount);
                float endDistance = splineLength * ((segmentIndex + 1) / (float)segmentCount);
                float startT = SplineUtility.ConvertIndexUnit(
                    splineContainer.Spline,
                    startDistance,
                    PathIndexUnit.Distance,
                    PathIndexUnit.Normalized);
                float endT = SplineUtility.ConvertIndexUnit(
                    splineContainer.Spline,
                    endDistance,
                    PathIndexUnit.Distance,
                    PathIndexUnit.Normalized);

                if (!TryEvaluateSample(startT, out Sample startSample) || !TryEvaluateSample(endT, out Sample endSample))
                {
                    continue;
                }

                Vector3 delta = endSample.Position - startSample.Position;
                float distance = delta.magnitude;
                if (distance <= 0.001f)
                {
                    continue;
                }

                var segmentObject = new GameObject($"RailCollider_{segmentIndex:D2}");
                Transform segmentTransform = segmentObject.transform;
                segmentTransform.SetParent(generatedColliderRoot, false);
                segmentTransform.position = startSample.Position + (delta * 0.5f);
                segmentTransform.rotation = Quaternion.LookRotation(delta.normalized, startSample.Up);
                segmentObject.layer = gameObject.layer;

                CapsuleCollider capsuleCollider = segmentObject.AddComponent<CapsuleCollider>();
                capsuleCollider.direction = 2;
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.radius = collisionRadius;
                capsuleCollider.height = distance + (collisionRadius * 2f);
                capsuleCollider.isTrigger = true;
            }
        }

        private void ClearGeneratedColliders()
        {
            if (!CanRunEditorAuthoring())
            {
                return;
            }

            if (generatedColliderRoot == null)
            {
                return;
            }

            var children = new List<GameObject>();
            for (int i = 0; i < generatedColliderRoot.childCount; i++)
            {
                children.Add(generatedColliderRoot.GetChild(i).gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(children[i]);
                    continue;
                }
#endif
                Destroy(children[i]);
            }
        }

        private void AssignDefaultLayer()
        {
            int layer = LayerMask.NameToLayer("GrindRail");
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }
        }

        private void QueueApplyAuthoring()
        {
#if UNITY_EDITOR
            if (!CanRunEditorAuthoring())
            {
                return;
            }

            if (Application.isPlaying)
            {
                ApplyAuthoring();
                return;
            }

            if (rebuildQueued)
            {
                return;
            }

            rebuildQueued = true;
            EditorApplication.delayCall += ApplyAuthoringDelayed;
#else
            ApplyAuthoring();
#endif
        }

#if UNITY_EDITOR
        private bool CanRunEditorAuthoring()
        {
            return Application.isPlaying || !PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }

        private void ApplyAuthoringDelayed()
        {
            rebuildQueued = false;
            if (this == null || !CanRunEditorAuthoring())
            {
                return;
            }

            ApplyAuthoring();
            EditorUtility.SetDirty(this);
        }
#else
        private bool CanRunEditorAuthoring()
        {
            return true;
        }
#endif
    }
}
