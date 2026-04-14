using UnityEngine;

namespace Race.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private int spawnIndex;
        [SerializeField] private Color gizmoColor = new(0.2f, 0.75f, 1f, 0.95f);
        [SerializeField] private float gizmoRadius = 0.65f;
        [SerializeField] private float gizmoArrowLength = 1.4f;

        public int SpawnIndex => spawnIndex;

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);

            Vector3 forwardStart = transform.position + Vector3.up * 0.15f;
            Vector3 forwardEnd = forwardStart + transform.forward * gizmoArrowLength;
            Gizmos.DrawLine(forwardStart, forwardEnd);

            Vector3 arrowLeft = Quaternion.Euler(0f, 150f, 0f) * transform.forward * 0.35f;
            Vector3 arrowRight = Quaternion.Euler(0f, -150f, 0f) * transform.forward * 0.35f;
            Gizmos.DrawLine(forwardEnd, forwardEnd + arrowLeft);
            Gizmos.DrawLine(forwardEnd, forwardEnd + arrowRight);
        }
    }
}
