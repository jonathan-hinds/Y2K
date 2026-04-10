using Unity.Cinemachine;
using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public sealed class PlayerCameraOrbitInput : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader targetInput;
        [SerializeField] private float yawSensitivity = 0.12f;
        [SerializeField] private float pitchSensitivity = 0.08f;

        private CinemachineOrbitalFollow orbitalFollow;

        private void Reset()
        {
            targetInput = FindFirstObjectByType<PlayerInputReader>();
        }

        private void Awake()
        {
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            if (targetInput == null)
            {
                targetInput = FindFirstObjectByType<PlayerInputReader>();
            }
        }

        private void Update()
        {
            if (targetInput == null || orbitalFollow == null)
            {
                return;
            }

            Vector2 lookInput = targetInput.LookInput;
            if (lookInput.sqrMagnitude <= 0f)
            {
                return;
            }

            orbitalFollow.HorizontalAxis.Value = orbitalFollow.HorizontalAxis.ClampValue(
                orbitalFollow.HorizontalAxis.Value + lookInput.x * yawSensitivity);
            orbitalFollow.VerticalAxis.Value = orbitalFollow.VerticalAxis.ClampValue(
                orbitalFollow.VerticalAxis.Value - lookInput.y * pitchSensitivity);
        }

        public void SetInputReader(PlayerInputReader input)
        {
            targetInput = input;
        }
    }
}
