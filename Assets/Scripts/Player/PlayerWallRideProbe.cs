using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class PlayerWallRideProbe : MonoBehaviour
    {
        [SerializeField] private SphereCollider probeCollider;

        public SphereCollider ProbeCollider => probeCollider;

        public Vector3 WorldCenter
        {
            get
            {
                if (probeCollider == null)
                {
                    return transform.position;
                }

                return probeCollider.transform.TransformPoint(probeCollider.center);
            }
        }

        public float WorldRadius
        {
            get
            {
                if (probeCollider == null)
                {
                    return 0f;
                }

                Vector3 lossyScale = probeCollider.transform.lossyScale;
                float maxAxisScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
                return probeCollider.radius * maxAxisScale;
            }
        }

        public bool ContainsPoint(Vector3 point, float padding = 0f)
        {
            if (probeCollider == null)
            {
                return false;
            }

            Vector3 closestPoint = probeCollider.ClosestPoint(point);
            float allowedDistance = Mathf.Max(0f, padding);
            return (closestPoint - point).sqrMagnitude <= allowedDistance * allowedDistance;
        }

        public bool TryGetWorldSphere(out Vector3 center, out float radius)
        {
            center = WorldCenter;
            radius = WorldRadius;
            return probeCollider != null && radius > Mathf.Epsilon;
        }

        private void Reset()
        {
            ResolveProbeCollider();
            ApplyDefaults();
        }

        private void OnValidate()
        {
            ResolveProbeCollider();
            ApplyDefaults();
        }

        private void ResolveProbeCollider()
        {
            if (probeCollider == null)
            {
                probeCollider = GetComponent<SphereCollider>();
            }
        }

        private void ApplyDefaults()
        {
            if (probeCollider == null)
            {
                return;
            }

            probeCollider.isTrigger = true;
        }
    }
}
