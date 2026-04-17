using System.Collections.Generic;
using UnityEngine;

namespace Race.Tagging
{
    public sealed class GraffitiProjectionDebugView
    {
        private readonly GameObject root;
        private readonly Transform beam;
        private readonly Transform acquisitionBox;
        private readonly Transform volumeBox;
        private readonly Transform centerMarker;
        private readonly List<Transform> hitMarkers = new();
        private readonly Material debugMaterial;

        public GraffitiProjectionDebugView(string name, int maxHitMarkers)
        {
            Shader shader = Shader.Find("Sprites/Default");
            debugMaterial = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"))
            {
                name = $"{name}_Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            root = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            beam = CreatePrimitive("Beam", PrimitiveType.Cube, new Vector3(0.03f, 0.03f, 1f));
            acquisitionBox = CreatePrimitive("Reach", PrimitiveType.Cube, Vector3.one);
            volumeBox = CreatePrimitive("Projection", PrimitiveType.Cube, Vector3.one);
            centerMarker = CreatePrimitive("Center", PrimitiveType.Sphere, Vector3.one * 0.12f);

            int markerCount = Mathf.Clamp(maxHitMarkers, 1, 32);
            for (int index = 0; index < markerCount; index++)
            {
                hitMarkers.Add(CreatePrimitive($"Hit_{index}", PrimitiveType.Sphere, Vector3.one * 0.09f));
            }

            SetVisible(false);
        }

        public void Dispose()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }

            if (debugMaterial != null)
            {
                Object.Destroy(debugMaterial);
            }
        }

        public void Update(
            bool visible,
            Vector3 sprayOrigin,
            in GraffitiProjectionVolume volume,
            bool valid,
            IReadOnlyList<Vector3> hitPoints)
        {
            if (root == null)
            {
                return;
            }

            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            Color beamColor = valid ? new Color(0.18f, 0.85f, 0.35f, 0.45f) : new Color(0.92f, 0.22f, 0.18f, 0.35f);
            Color acquisitionColor = valid ? new Color(0.15f, 0.65f, 1f, 0.08f) : new Color(1f, 0.55f, 0.15f, 0.08f);
            Color boxColor = valid ? new Color(0.15f, 0.95f, 0.35f, 0.16f) : new Color(0.95f, 0.35f, 0.2f, 0.14f);
            Color centerColor = valid ? new Color(1f, 0.95f, 0.2f, 0.9f) : new Color(1f, 0.4f, 0.2f, 0.8f);
            Color hitColor = new(0.2f, 0.75f, 1f, 0.9f);

            UpdateBeam(sprayOrigin, volume.SurfacePoint, beamColor);
            UpdatePrimitive(acquisitionBox, volume.AcquisitionCenter, volume.Rotation, volume.AcquisitionHalfExtents * 2f, acquisitionColor);
            UpdatePrimitive(volumeBox, volume.Center, volume.Rotation, volume.HalfExtents * 2f, boxColor);
            UpdatePrimitive(centerMarker, volume.SurfacePoint, Quaternion.identity, Vector3.one * 0.12f, centerColor);

            for (int index = 0; index < hitMarkers.Count; index++)
            {
                bool markerVisible = hitPoints != null && index < hitPoints.Count;
                hitMarkers[index].gameObject.SetActive(markerVisible);
                if (!markerVisible)
                {
                    continue;
                }

                UpdatePrimitive(hitMarkers[index], hitPoints[index], Quaternion.identity, Vector3.one * 0.09f, hitColor);
            }
        }

        private Transform CreatePrimitive(string name, PrimitiveType type, Vector3 scale)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.hideFlags = HideFlags.HideAndDontSave;

            if (primitive.TryGetComponent(out Collider collider))
            {
                Object.Destroy(collider);
            }

            primitive.transform.SetParent(root.transform, false);
            primitive.transform.localScale = scale;

            if (primitive.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = new Material(debugMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }

            return primitive.transform;
        }

        private void UpdateBeam(Vector3 start, Vector3 end, Color color)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            Vector3 position = length > 0.0001f ? start + direction * 0.5f : start;
            Quaternion rotation = length > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
            UpdatePrimitive(beam, position, rotation, new Vector3(0.03f, 0.03f, Mathf.Max(0.01f, length)), color);
        }

        private void UpdatePrimitive(Transform primitive, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            primitive.position = position;
            primitive.rotation = rotation;
            primitive.localScale = scale;

            if (primitive.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial.color = color;
            }
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }
    }
}
