using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

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
        [SerializeField] private RoadSplineProfile profile;
        [SerializeField] private bool autoApplyInEditor = true;
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private MeshCollider meshCollider;

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

            if (!autoApplyInEditor || Application.isPlaying)
            {
                return;
            }

            QueueApplyProfile();
        }

        [ContextMenu("Apply Road Profile")]
        public void ApplyProfile()
        {
            CacheReferences();

            if (profile == null || profile.SegmentPrefab == null || splineContainer == null)
            {
                return;
            }

            if (!TryGetSegmentSource(out Mesh sourceMesh, out Material[] materials))
            {
                return;
            }

            Mesh generatedMesh = BuildRoadMesh(sourceMesh, splineContainer.Spline);
            if (generatedMesh == null)
            {
                return;
            }

            generatedMesh.name = $"{name}_RoadMesh";
            meshFilter.sharedMesh = generatedMesh;
            meshRenderer.sharedMaterials = materials;
            meshCollider.sharedMesh = generatedMesh;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(meshFilter);
                EditorUtility.SetDirty(meshRenderer);
                EditorUtility.SetDirty(meshCollider);
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

        private Mesh BuildRoadMesh(Mesh sourceMesh, Spline spline)
        {
            float segmentLength = Mathf.Max(0.01f, profile.SegmentSpacing);
            float minY = sourceMesh.bounds.min.y;
            float meshLength = sourceMesh.bounds.size.y;
            if (meshLength <= Mathf.Epsilon)
            {
                return null;
            }

            float splineLength = SplineUtility.CalculateLength(spline, float4x4.identity);
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(splineLength / segmentLength));

            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;
            Vector4[] sourceTangents = sourceMesh.tangents;
            Vector2[] sourceUvs = sourceMesh.uv;
            int[] sourceTriangles = sourceMesh.triangles;
            Quaternion crossSectionRotation = Quaternion.AngleAxis(profile.CrossSectionRollDegrees, Vector3.forward);

            int vertexCount = sourceVertices.Length * segmentCount;
            int triangleIndexCount = sourceTriangles.Length * segmentCount;

            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var tangents = new List<Vector4>(vertexCount);
            var uvs = new List<Vector2>(vertexCount);
            var triangles = new List<int>(triangleIndexCount);

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                int vertexOffset = vertices.Count;
                float segmentStartDistance = segmentIndex * segmentLength;

                for (int vertexIndex = 0; vertexIndex < sourceVertices.Length; vertexIndex++)
                {
                    Vector3 sourceVertex = crossSectionRotation * sourceVertices[vertexIndex];
                    float localDistance = sourceVertex.y - minY;
                    float distance = Mathf.Clamp(segmentStartDistance + localDistance, 0f, splineLength);
                    float t = SplineUtility.ConvertIndexUnit(
                        spline,
                        distance,
                        PathIndexUnit.Distance,
                        PathIndexUnit.Normalized);

                    SplineUtility.Evaluate(spline, t, out float3 position, out float3 tangent, out float3 upVector);

                    Vector3 forward = ((Vector3)tangent).normalized;
                    Vector3 up = ((Vector3)upVector).normalized;
                    Vector3 right = Vector3.Cross(forward, up).normalized;

                    vertices.Add((Vector3)position + (right * sourceVertex.x) + (up * sourceVertex.z));

                    Vector3 sourceNormal = sourceNormals.Length > 0
                        ? crossSectionRotation * sourceNormals[vertexIndex]
                        : Vector3.up;
                    Vector3 transformedNormal =
                        (right * sourceNormal.x) +
                        (forward * sourceNormal.y) +
                        (up * sourceNormal.z);
                    normals.Add(transformedNormal.normalized);

                    Vector4 sourceTangent = sourceTangents.Length > 0
                        ? sourceTangents[vertexIndex]
                        : new Vector4(1f, 0f, 0f, 1f);
                    Vector3 rotatedSourceTangent = crossSectionRotation *
                        new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z);
                    Vector3 tangentDirection =
                        (right * rotatedSourceTangent.x) +
                        (forward * rotatedSourceTangent.y) +
                        (up * rotatedSourceTangent.z);
                    tangents.Add(new Vector4(tangentDirection.x, tangentDirection.y, tangentDirection.z, sourceTangent.w));

                    uvs.Add(sourceUvs.Length > 0 ? sourceUvs[vertexIndex] : Vector2.zero);
                }

                for (int triangleIndex = 0; triangleIndex < sourceTriangles.Length; triangleIndex++)
                {
                    triangles.Add(vertexOffset + sourceTriangles[triangleIndex]);
                }
            }

            var mesh = new Mesh
            {
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CacheReferences()
        {
            if (splineContainer == null)
            {
                splineContainer = GetComponent<SplineContainer>();
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

#if UNITY_EDITOR
        private void ApplyProfileDelayed()
        {
            applyQueued = false;

            if (this == null || Application.isPlaying)
            {
                return;
            }

            ApplyProfile();
        }
#endif
    }
}
