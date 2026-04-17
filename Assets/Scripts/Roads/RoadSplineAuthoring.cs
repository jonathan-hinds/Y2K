using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using Race.Tagging;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Roads
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public sealed class RoadSplineAuthoring : MonoBehaviour
    {
        private readonly struct SegmentBuildContext
        {
            public SegmentBuildContext(Mesh sourceMesh, Spline spline, RoadSplineProfile profile)
            {
                SourceMesh = sourceMesh;
                Spline = spline;
                SegmentLength = Mathf.Max(0.01f, profile.SegmentSpacing);
                SourceVertices = sourceMesh.vertices;
                SourceNormals = sourceMesh.normals;
                SourceTangents = sourceMesh.tangents;
                SourceUvs = sourceMesh.uv;
                SourceSubMeshTriangles = BuildSourceSubMeshTriangleLists(sourceMesh);
                CrossSectionRotation = Quaternion.AngleAxis(profile.CrossSectionRollDegrees, Vector3.forward);
                MinY = sourceMesh.bounds.min.y;
                MeshLength = sourceMesh.bounds.size.y;
                XMin = sourceMesh.bounds.min.x;
                XMax = sourceMesh.bounds.max.x;
                SplineLength = SplineUtility.CalculateLength(spline, float4x4.identity);
                SegmentCount = Mathf.Max(1, Mathf.CeilToInt(SplineLength / SegmentLength));
            }

            public Mesh SourceMesh { get; }
            public Spline Spline { get; }
            public float SegmentLength { get; }
            public float MinY { get; }
            public float MeshLength { get; }
            public float XMin { get; }
            public float XMax { get; }
            public float SplineLength { get; }
            public int SegmentCount { get; }
            public Vector3[] SourceVertices { get; }
            public Vector3[] SourceNormals { get; }
            public Vector4[] SourceTangents { get; }
            public Vector2[] SourceUvs { get; }
            public List<int>[] SourceSubMeshTriangles { get; }
            public Quaternion CrossSectionRotation { get; }
        }

        private const string GeneratedSegmentRootName = "_GeneratedRoadSegments";
        private const string GeneratedSegmentNamePrefix = "RoadSegment_";

        [SerializeField] private RoadSplineProfile profile;
        [SerializeField] private bool autoApplyInEditor = true;
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private SplineInstantiate splineInstantiate;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private MeshCollider meshCollider;
        [SerializeField] private Transform generatedSegmentRoot;

        public RoadSplineProfile Profile => profile;

#if UNITY_EDITOR
        private bool applyQueued;
#endif
        private RoadSplineProfile subscribedProfile;

        private void OnEnable()
        {
            CacheReferences();
            SubscribeToProfile();
        }

        private void OnDisable()
        {
            UnsubscribeFromProfile();
        }

        private void Reset()
        {
            CacheReferences();
            QueueApplyProfile();
        }

        private void OnValidate()
        {
            CacheReferences();
            SubscribeToProfile();

            if (!autoApplyInEditor || Application.isPlaying || !CanApplyInCurrentContext())
            {
                return;
            }

            QueueApplyProfile();
        }

        [ContextMenu("Apply Road Profile")]
        public void ApplyProfile()
        {
            CacheReferences();
            if (!CanApplyInCurrentContext())
            {
                return;
            }

            if (profile == null || profile.SegmentPrefab == null || splineContainer == null || splineContainer.Spline == null)
            {
                ClearGeneratedGeometry();
                DisableRootPresentation();
                return;
            }

#if UNITY_EDITOR
            SyncSplineInstantiatePreview();
#endif

            if (!TryGetSegmentSource(out Mesh sourceMesh, out Material[] sourceMaterials))
            {
                ClearGeneratedGeometry();
                DisableRootPresentation();
                return;
            }

            if (!TryBuildContext(sourceMesh, splineContainer.Spline, out SegmentBuildContext context))
            {
                ClearGeneratedGeometry();
                DisableRootPresentation();
                return;
            }

            RebuildGeneratedSegments(context, sourceMaterials);
            DisableRootPresentation();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        [ContextMenu("Rebuild Road Mesh")]
        public void RebuildRoad()
        {
            ApplyProfile();
        }

        private bool TryGetSegmentSource(out Mesh sourceMesh, out Material[] materials)
        {
            sourceMesh = null;
            materials = null;

            MeshFilter sourceMeshFilter = profile.SegmentPrefab.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = profile.SegmentPrefab.GetComponent<MeshRenderer>();
            if (sourceMeshFilter == null || sourceMeshRenderer == null)
            {
                return false;
            }

            sourceMesh = sourceMeshFilter.sharedMesh;
            materials = sourceMeshRenderer.sharedMaterials;
            return sourceMesh != null;
        }

        private bool TryBuildContext(Mesh sourceMesh, Spline spline, out SegmentBuildContext context)
        {
            context = default;
            if (sourceMesh == null || spline == null)
            {
                return false;
            }

            float meshLength = sourceMesh.bounds.size.y;
            if (meshLength <= Mathf.Epsilon)
            {
                return false;
            }

            context = new SegmentBuildContext(sourceMesh, spline, profile);
            return context.SourceVertices != null && context.SourceVertices.Length > 0;
        }

        private void RebuildGeneratedSegments(in SegmentBuildContext context, Material[] sourceMaterials)
        {
            Transform root = EnsureGeneratedSegmentRoot();
            ClearGeneratedSegments(root);

            for (int segmentIndex = 0; segmentIndex < context.SegmentCount; segmentIndex++)
            {
                Mesh segmentMesh = BuildSegmentMesh(context, segmentIndex);
                if (segmentMesh == null)
                {
                    continue;
                }

                segmentMesh.name = $"{name}_{GeneratedSegmentNamePrefix}{segmentIndex:D2}_Mesh";
                GameObject segmentObject = CreateSegmentObject(root, segmentIndex);
                ConfigureSegmentComponents(segmentObject, segmentMesh, sourceMaterials);
            }

            RemoveLegacyPaintTargetSettings();
        }

        private Mesh BuildSegmentMesh(in SegmentBuildContext context, int segmentIndex)
        {
            int vertexCount = context.SourceVertices.Length;
            if (vertexCount <= 0)
            {
                return null;
            }

            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var tangents = new List<Vector4>(vertexCount);
            var uvs = new List<Vector2>(vertexCount);

            float segmentStartDistance = segmentIndex * context.SegmentLength;
            for (int vertexIndex = 0; vertexIndex < context.SourceVertices.Length; vertexIndex++)
            {
                Vector3 sourceVertex = context.CrossSectionRotation * context.SourceVertices[vertexIndex];
                float localDistance = sourceVertex.y - context.MinY;
                float distance = Mathf.Clamp(segmentStartDistance + localDistance, 0f, context.SplineLength);
                float t = SplineUtility.ConvertIndexUnit(
                    context.Spline,
                    distance,
                    PathIndexUnit.Distance,
                    PathIndexUnit.Normalized);

                SplineUtility.Evaluate(context.Spline, t, out float3 position, out float3 tangent, out float3 upVector);

                Vector3 forward = ((Vector3)tangent).normalized;
                Vector3 up = ((Vector3)upVector).normalized;
                Vector3 right = Vector3.Cross(forward, up).normalized;

                vertices.Add((Vector3)position + (right * sourceVertex.x) + (up * sourceVertex.z));

                Vector3 sourceNormal = context.SourceNormals.Length > 0
                    ? context.CrossSectionRotation * context.SourceNormals[vertexIndex]
                    : Vector3.up;
                Vector3 transformedNormal =
                    (right * sourceNormal.x) +
                    (forward * sourceNormal.y) +
                    (up * sourceNormal.z);
                normals.Add(transformedNormal.normalized);

                Vector4 sourceTangent = context.SourceTangents.Length > 0
                    ? context.SourceTangents[vertexIndex]
                    : new Vector4(1f, 0f, 0f, 1f);
                Vector3 rotatedSourceTangent = context.CrossSectionRotation *
                    new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z);
                Vector3 tangentDirection =
                    (right * rotatedSourceTangent.x) +
                    (forward * rotatedSourceTangent.y) +
                    (up * rotatedSourceTangent.z);
                tangents.Add(new Vector4(tangentDirection.x, tangentDirection.y, tangentDirection.z, sourceTangent.w));

                uvs.Add(BuildSegmentUv(
                    context.SourceUvs,
                    vertexIndex,
                    context.SourceVertices[vertexIndex],
                    context.MinY,
                    context.MeshLength,
                    context.XMin,
                    context.XMax));
            }

            var mesh = new Mesh
            {
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = context.SourceSubMeshTriangles.Length;
            for (int subMeshIndex = 0; subMeshIndex < context.SourceSubMeshTriangles.Length; subMeshIndex++)
            {
                mesh.SetTriangles(context.SourceSubMeshTriangles[subMeshIndex], subMeshIndex, false);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int>[] BuildSourceSubMeshTriangleLists(Mesh sourceMesh)
        {
            int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
            List<int>[] subMeshTriangles = new List<int>[subMeshCount];
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                subMeshTriangles[subMeshIndex] = new List<int>(sourceMesh.GetTriangles(subMeshIndex));
            }

            return subMeshTriangles;
        }

        private static Vector2 BuildSegmentUv(
            Vector2[] sourceUvs,
            int vertexIndex,
            Vector3 sourceVertex,
            float minY,
            float meshLength,
            float xMin,
            float xMax)
        {
            if (sourceUvs.Length > vertexIndex)
            {
                return sourceUvs[vertexIndex];
            }

            float lateral = Mathf.InverseLerp(xMin, xMax, sourceVertex.x);
            float longitudinal = Mathf.Clamp01((sourceVertex.y - minY) / Mathf.Max(Mathf.Epsilon, meshLength));
            return new Vector2(lateral, longitudinal);
        }

        private Transform EnsureGeneratedSegmentRoot()
        {
            if (generatedSegmentRoot != null)
            {
                return generatedSegmentRoot;
            }

            Transform existing = transform.Find(GeneratedSegmentRootName);
            if (existing != null)
            {
                generatedSegmentRoot = existing;
                return generatedSegmentRoot;
            }

            GameObject root = new GameObject(GeneratedSegmentRootName);
            root.transform.SetParent(transform, false);
            root.layer = gameObject.layer;
            generatedSegmentRoot = root.transform;
            return generatedSegmentRoot;
        }

        private GameObject CreateSegmentObject(Transform parent, int segmentIndex)
        {
            GameObject segmentObject = new GameObject($"{GeneratedSegmentNamePrefix}{segmentIndex:D2}");
            segmentObject.layer = gameObject.layer;
            segmentObject.transform.SetParent(parent, false);
            return segmentObject;
        }

        private void ConfigureSegmentComponents(GameObject segmentObject, Mesh segmentMesh, Material[] sourceMaterials)
        {
            MeshFilter segmentMeshFilter = GetOrAddComponent<MeshFilter>(segmentObject);
            MeshRenderer segmentRenderer = GetOrAddComponent<MeshRenderer>(segmentObject);
            MeshCollider segmentCollider = GetOrAddComponent<MeshCollider>(segmentObject);

            segmentMeshFilter.sharedMesh = segmentMesh;
            segmentRenderer.sharedMaterials = sourceMaterials;
            segmentRenderer.enabled = true;
            ApplyRendererSettings(meshRenderer, segmentRenderer);

            segmentCollider.sharedMesh = segmentMesh;
            ApplyColliderSettings(meshCollider, segmentCollider);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(segmentMeshFilter);
                EditorUtility.SetDirty(segmentRenderer);
                EditorUtility.SetDirty(segmentCollider);
            }
#endif
        }

        private void ClearGeneratedGeometry()
        {
            ClearGeneratedSegments(generatedSegmentRoot);
            ReleaseRootGeneratedMesh();
        }

        private void ClearGeneratedSegments(Transform root)
        {
            if (root == null)
            {
                return;
            }

            List<GameObject> children = new(root.childCount);
            for (int index = 0; index < root.childCount; index++)
            {
                children.Add(root.GetChild(index).gameObject);
            }

            for (int index = 0; index < children.Count; index++)
            {
                GameObject child = children[index];
                if (child == null)
                {
                    continue;
                }

                if (child.TryGetComponent(out MeshFilter childMeshFilter))
                {
                    ReleaseGeneratedMesh(childMeshFilter.sharedMesh);
                }

                if (child.TryGetComponent(out MeshCollider childCollider))
                {
                    childCollider.sharedMesh = null;
                }

                DestroyUnityObject(child);
            }
        }

        private void DisableRootPresentation()
        {
            ReleaseRootGeneratedMesh();

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
                meshRenderer.sharedMaterials = System.Array.Empty<Material>();
            }

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }
        }

        private void ReleaseRootGeneratedMesh()
        {
            if (meshFilter == null)
            {
                return;
            }

            ReleaseGeneratedMesh(meshFilter.sharedMesh);
            meshFilter.sharedMesh = null;
        }

        private void RemoveLegacyPaintTargetSettings()
        {
            GraffitiPaintTargetSettings settings = GetComponent<GraffitiPaintTargetSettings>();
            if (settings == null)
            {
                return;
            }

            DestroyUnityObject(settings);
        }

        private void ApplyRendererSettings(MeshRenderer source, MeshRenderer destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.probeAnchor = source.probeAnchor;
            destination.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            destination.renderingLayerMask = source.renderingLayerMask;
            destination.rendererPriority = source.rendererPriority;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
        }

        private static void ApplyColliderSettings(MeshCollider source, MeshCollider destination)
        {
            if (destination == null)
            {
                return;
            }

            if (source == null)
            {
                destination.enabled = true;
                return;
            }

            destination.enabled = source.enabled;
            destination.sharedMaterial = source.sharedMaterial;
            destination.isTrigger = source.isTrigger;
            destination.convex = source.convex;
            destination.cookingOptions = source.cookingOptions;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private void ReleaseGeneratedMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (AssetDatabase.Contains(mesh))
            {
                return;
            }
#endif

            DestroyUnityObject(mesh);
        }

        private void CacheReferences()
        {
            if (splineContainer == null)
            {
                splineContainer = GetComponent<SplineContainer>();
            }

            if (splineInstantiate == null)
            {
                splineInstantiate = GetComponent<SplineInstantiate>();
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (meshCollider == null)
            {
                meshCollider = GetComponent<MeshCollider>();
            }

            if (generatedSegmentRoot == null)
            {
                generatedSegmentRoot = transform.Find(GeneratedSegmentRootName);
            }
        }

        private void SubscribeToProfile()
        {
            if (subscribedProfile == profile)
            {
                return;
            }

            UnsubscribeFromProfile();
            subscribedProfile = profile;

            if (subscribedProfile != null)
            {
                subscribedProfile.Changed += HandleProfileChanged;
            }
        }

        private void UnsubscribeFromProfile()
        {
            if (subscribedProfile == null)
            {
                return;
            }

            subscribedProfile.Changed -= HandleProfileChanged;
            subscribedProfile = null;
        }

        private void HandleProfileChanged()
        {
            ApplyProfile();
        }

        private void QueueApplyProfile()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (applyQueued)
                {
                    return;
                }

                applyQueued = true;
                EditorApplication.delayCall += ApplyProfileDelayed;
                return;
            }
#endif

            ApplyProfile();
        }

        private bool CanApplyInCurrentContext()
        {
#if UNITY_EDITOR
            return Application.isPlaying || !PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        private void ApplyProfileDelayed()
        {
            applyQueued = false;

            if (this == null || Application.isPlaying || !CanApplyInCurrentContext())
            {
                return;
            }

            ApplyProfile();
        }

        private void SyncSplineInstantiatePreview()
        {
            if (splineInstantiate == null || profile == null || profile.SegmentPrefab == null)
            {
                return;
            }

            SerializedObject serializedInstantiate = new SerializedObject(splineInstantiate);
            serializedInstantiate.FindProperty("m_Space").intValue = (int)profile.CoordinateSpace;
            serializedInstantiate.FindProperty("m_Spacing").vector2Value =
                new Vector2(profile.SegmentSpacing, profile.SegmentSpacing);

            SerializedProperty itemsProperty = serializedInstantiate.FindProperty("m_ItemsToInstantiate");
            if (itemsProperty.arraySize == 0)
            {
                itemsProperty.InsertArrayElementAtIndex(0);
            }

            SerializedProperty firstItem = itemsProperty.GetArrayElementAtIndex(0);
            firstItem.FindPropertyRelative("Prefab").objectReferenceValue = profile.SegmentPrefab;
            firstItem.FindPropertyRelative("Probability").floatValue = 1f;

            for (int index = itemsProperty.arraySize - 1; index >= 1; index--)
            {
                itemsProperty.DeleteArrayElementAtIndex(index);
            }

            serializedInstantiate.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(splineInstantiate);
        }
#endif
    }
}
