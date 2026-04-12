using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Lighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LampPostLightAuthoring : MonoBehaviour
    {
        [SerializeField] private LampPostLightProfile profile;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform lightAnchor;
        [SerializeField] private Light lampLight;
        [SerializeField] private bool autoApplyInEditor = true;
        [SerializeField] private bool snapLightToVisualBounds = true;
        [SerializeField] private Vector3 localLightOffset = new(0f, -0.2f, 0f);

        public LampPostLightProfile Profile => profile;
        public Light LampLight => lampLight;

        private void OnEnable()
        {
            CacheReferences();
        }

        private void Reset()
        {
            CacheReferences();
            ApplyAuthoring();
        }

        private void OnValidate()
        {
            CacheReferences();

            if (Application.isPlaying || !autoApplyInEditor)
            {
                return;
            }

            ApplyAuthoring();
        }

        [ContextMenu("Apply Lamp Authoring")]
        public void ApplyAuthoring()
        {
            CacheReferences();
            ApplyProfile();

            if (snapLightToVisualBounds)
            {
                SnapLightToVisualBounds();
            }

#if UNITY_EDITOR
            ConfigureStaticFlags();
            EditorUtility.SetDirty(this);
            if (lampLight != null)
            {
                EditorUtility.SetDirty(lampLight);
            }
#endif
        }

        [ContextMenu("Apply Light Profile")]
        public void ApplyProfile()
        {
            profile?.ApplyTo(lampLight);
        }

        [ContextMenu("Snap Light To Visual Bounds")]
        public void SnapLightToVisualBounds()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (!TryGetVisualBounds(out Bounds localBounds))
            {
                return;
            }

            Vector3 snappedPosition = new Vector3(
                localBounds.center.x,
                localBounds.max.y,
                localBounds.center.z) + localLightOffset;

            if (lightAnchor != null)
            {
                lightAnchor.localPosition = snappedPosition;
                lightAnchor.localRotation = Quaternion.identity;
            }

            if (lampLight != null)
            {
                if (lampLight.transform.parent == lightAnchor)
                {
                    lampLight.transform.localPosition = Vector3.zero;
                    lampLight.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    lampLight.transform.localPosition = snappedPosition;
                }
            }
        }

        private void CacheReferences()
        {
            if (lampLight == null)
            {
                lampLight = GetComponentInChildren<Light>(true);
            }

            if (lightAnchor == null)
            {
                Transform anchorCandidate = transform.Find("LightAnchor");
                if (anchorCandidate != null)
                {
                    lightAnchor = anchorCandidate;
                }
            }

            if (visualRoot == null)
            {
                foreach (Transform child in transform)
                {
                    if ((lampLight != null && child == lampLight.transform) || child == lightAnchor)
                    {
                        continue;
                    }

                    visualRoot = child;
                    break;
                }
            }
        }

        private bool TryGetVisualBounds(out Bounds localBounds)
        {
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            bool boundsInitialized = false;
            localBounds = default;

            foreach (Renderer renderer in renderers)
            {
                Bounds rendererBounds = renderer.bounds;
                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;

                Vector3[] corners =
                {
                    new(min.x, min.y, min.z),
                    new(min.x, min.y, max.z),
                    new(min.x, max.y, min.z),
                    new(min.x, max.y, max.z),
                    new(max.x, min.y, min.z),
                    new(max.x, min.y, max.z),
                    new(max.x, max.y, min.z),
                    new(max.x, max.y, max.z)
                };

                foreach (Vector3 corner in corners)
                {
                    Vector3 localCorner = transform.InverseTransformPoint(corner);
                    if (!boundsInitialized)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        boundsInitialized = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }

            return boundsInitialized;
        }

#if UNITY_EDITOR
        private void ConfigureStaticFlags()
        {
            StaticEditorFlags flags =
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic;

            GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
        }
#endif
    }
}
