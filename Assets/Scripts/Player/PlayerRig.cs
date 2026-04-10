using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerRig : MonoBehaviour
    {
        [field: SerializeField] public Transform VisualRoot { get; private set; }
        [field: SerializeField] public Transform ModelRoot { get; private set; }
        [field: SerializeField] public Transform CameraTarget { get; private set; }

        private void Reset()
        {
            VisualRoot = transform;
            ModelRoot = transform;
            CameraTarget = transform;
        }
    }
}
