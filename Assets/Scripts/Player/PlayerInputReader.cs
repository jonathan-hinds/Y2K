using UnityEngine;
using UnityEngine.InputSystem;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerLocomotionInput
    {
        [Header("Actions")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string jumpActionName = "Jump";

        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool JumpReleasedThisFrame { get; private set; }
        public Vector2 PointerScreenPosition => Mouse.current?.position.ReadValue() ?? Vector2.zero;
        public bool InputBlocked { get; set; }

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            moveAction = playerInput.actions[moveActionName];
            lookAction = playerInput.actions[lookActionName];
            sprintAction = playerInput.actions[sprintActionName];
            jumpAction = playerInput.actions[jumpActionName];
        }

        private void Update()
        {
            JumpPressedThisFrame = false;
            JumpReleasedThisFrame = false;

            if (InputBlocked)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                SprintHeld = false;
                JumpHeld = false;
                return;
            }

            if (moveAction != null)
            {
                MoveInput = moveAction.ReadValue<Vector2>();
            }

            if (lookAction != null)
            {
                LookInput = lookAction.ReadValue<Vector2>();
            }

            if (sprintAction != null)
            {
                SprintHeld = sprintAction.IsPressed();
            }

            if (jumpAction != null)
            {
                JumpPressedThisFrame = jumpAction.WasPressedThisFrame();
                JumpReleasedThisFrame = jumpAction.WasReleasedThisFrame();
                JumpHeld = jumpAction.IsPressed();
            }
            else
            {
                JumpHeld = false;
            }
        }
    }
}
