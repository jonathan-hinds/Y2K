using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Race.Tagging
{
    public static class GraffitiTargetLocator
    {
        private static readonly Collider[] OverlapResults = new Collider[64];

        public static Renderer FindBestRenderer(Collider collider, Vector3 hitPoint)
        {
            if (collider == null)
            {
                return null;
            }

            if (collider.TryGetComponent(out Renderer directRenderer))
            {
                return directRenderer;
            }

            Renderer parentRenderer = collider.GetComponentInParent<Renderer>();
            if (parentRenderer != null)
            {
                return parentRenderer;
            }

            Renderer[] candidates = collider.transform.root.GetComponentsInChildren<Renderer>(true);
            Renderer bestRenderer = null;
            float bestDistance = float.PositiveInfinity;

            for (int index = 0; index < candidates.Length; index++)
            {
                Renderer candidate = candidates[index];
                Vector3 closestPoint = candidate.bounds.ClosestPoint(hitPoint);
                float distance = (closestPoint - hitPoint).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestRenderer = candidate;
            }

            return bestRenderer;
        }

        public static int FindCandidateRenderers(string sceneName, Vector3 hitPoint, float radius, ICollection<Renderer> results)
        {
            if (results == null)
            {
                return 0;
            }

            results.Clear();

            float safeRadius = Mathf.Max(0.05f, radius);
            int hitCount = Physics.OverlapSphereNonAlloc(hitPoint, safeRadius, OverlapResults, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
            {
                return 0;
            }

            HashSet<int> seenRendererIds = new();
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider collider = OverlapResults[hitIndex];
                if (collider == null)
                {
                    continue;
                }

                CollectRenderersNearPoint(collider.transform.root, sceneName, hitPoint, safeRadius, seenRendererIds, results);
                OverlapResults[hitIndex] = null;
            }

            return results.Count;
        }

        public static bool TryBuildPath(Renderer renderer, out string sceneName, out string hierarchyPath)
        {
            sceneName = string.Empty;
            hierarchyPath = string.Empty;

            if (renderer == null)
            {
                return false;
            }

            Transform target = renderer.transform;
            Scene scene = target.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            sceneName = scene.name;
            hierarchyPath = BuildHierarchyPath(target);
            return !string.IsNullOrEmpty(hierarchyPath);
        }

        public static Renderer ResolveRenderer(string sceneName, string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(hierarchyPath))
            {
                return null;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            Transform target = ResolveTransform(scene, hierarchyPath);
            return target != null ? target.GetComponent<Renderer>() : null;
        }

        private static string BuildHierarchyPath(Transform target)
        {
            List<Transform> chain = new();
            for (Transform current = target; current != null; current = current.parent)
            {
                chain.Add(current);
            }

            chain.Reverse();

            StringBuilder builder = new();
            for (int index = 0; index < chain.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('/');
                }

                Transform current = chain[index];
                builder.Append(current.name);
                builder.Append('#');
                builder.Append(current.GetSiblingIndex());
            }

            return builder.ToString();
        }

        private static void CollectRenderersNearPoint(
            Transform root,
            string sceneName,
            Vector3 hitPoint,
            float radius,
            HashSet<int> seenRendererIds,
            ICollection<Renderer> results)
        {
            if (root == null)
            {
                return;
            }

            float radiusSqr = radius * radius;
            Renderer[] candidates = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < candidates.Length; index++)
            {
                Renderer candidate = candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(sceneName) && candidate.gameObject.scene.name != sceneName)
                {
                    continue;
                }

                Vector3 closestPoint = candidate.bounds.ClosestPoint(hitPoint);
                if ((closestPoint - hitPoint).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                int instanceId = candidate.GetInstanceID();
                if (!seenRendererIds.Add(instanceId))
                {
                    continue;
                }

                results.Add(candidate);
            }
        }

        private static Transform ResolveTransform(Scene scene, string hierarchyPath)
        {
            string[] segments = hierarchyPath.Split('/');
            if (segments.Length == 0)
            {
                return null;
            }

            Transform current = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                if (!TryParseSegment(segments[segmentIndex], out string expectedName, out int expectedSiblingIndex))
                {
                    return null;
                }

                if (segmentIndex == 0)
                {
                    current = FindRoot(roots, expectedName, expectedSiblingIndex)?.transform;
                }
                else if (current != null)
                {
                    current = FindChild(current, expectedName, expectedSiblingIndex);
                }

                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private static GameObject FindRoot(IReadOnlyList<GameObject> roots, string expectedName, int expectedSiblingIndex)
        {
            for (int index = 0; index < roots.Count; index++)
            {
                GameObject root = roots[index];
                if (root.name == expectedName && root.transform.GetSiblingIndex() == expectedSiblingIndex)
                {
                    return root;
                }
            }

            return null;
        }

        private static Transform FindChild(Transform parent, string expectedName, int expectedSiblingIndex)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == expectedName && child.GetSiblingIndex() == expectedSiblingIndex)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool TryParseSegment(string segment, out string expectedName, out int expectedSiblingIndex)
        {
            expectedName = string.Empty;
            expectedSiblingIndex = 0;

            if (string.IsNullOrWhiteSpace(segment))
            {
                return false;
            }

            int separatorIndex = segment.LastIndexOf('#');
            if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
            {
                return false;
            }

            expectedName = segment[..separatorIndex];
            return int.TryParse(segment[(separatorIndex + 1)..], out expectedSiblingIndex);
        }
    }
}
