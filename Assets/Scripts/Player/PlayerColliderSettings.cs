using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerColliderSettings : MonoBehaviour
    {
        [SerializeField] private float height = 1.8f;
        [SerializeField] private float radius = 0.35f;
        [SerializeField] private Vector3 center = new Vector3(0f, 0.9f, 0f);
        [SerializeField] private float stepOffset = 0.35f;
        [SerializeField] private float skinWidth = 0.03f;
        [SerializeField] private float minMoveDistance = 0f;
        [SerializeField] private float slopeLimit = 45f;

        private CharacterController characterController;

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        [ContextMenu("Apply Collider Settings")]
        public void Apply()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (characterController == null)
            {
                return;
            }

            characterController.height = height;
            characterController.radius = radius;
            characterController.center = center;
            characterController.stepOffset = stepOffset;
            characterController.skinWidth = skinWidth;
            characterController.minMoveDistance = minMoveDistance;
            characterController.slopeLimit = slopeLimit;
        }
    }
}
