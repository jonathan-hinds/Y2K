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
        [SerializeField] private string trickModifierActionName = "Click";
        [SerializeField] private string interactActionName = "Interact";

        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private InputAction trickModifierAction;
        private InputAction interactAction;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool JumpReleasedThisFrame { get; private set; }
        public bool TrickModifierHeld { get; private set; }
        public bool InteractHeld { get; private set; }
        public bool InteractPressedThisFrame { get; private set; }
        public bool InteractReleasedThisFrame { get; private set; }
        public Vector2 PointerScreenPosition => Mouse.current?.position.ReadValue() ?? Vector2.zero;
        public bool InputBlocked { get; set; }
        public bool GameplayMovementBlocked { get; set; }
        public bool GameplayLookBlocked { get; set; }

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            moveAction = playerInput.actions[moveActionName];
            lookAction = playerInput.actions[lookActionName];
            sprintAction = playerInput.actions[sprintActionName];
            jumpAction = playerInput.actions[jumpActionName];
            trickModifierAction = playerInput.actions[trickModifierActionName];
            interactAction = playerInput.actions[interactActionName];
        }

        private void Update()
        {
            JumpPressedThisFrame = false;
            JumpReleasedThisFrame = false;
            InteractPressedThisFrame = false;
            InteractReleasedThisFrame = false;

            if (InputBlocked)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                SprintHeld = false;
                JumpHeld = false;
                TrickModifierHeld = false;
                InteractHeld = false;
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

            TrickModifierHeld = trickModifierAction != null && trickModifierAction.IsPressed();
            InteractHeld = IsActionHeld(interactAction);
            InteractPressedThisFrame = interactAction != null && interactAction.WasPressedThisFrame();
            InteractReleasedThisFrame = interactAction != null && interactAction.WasReleasedThisFrame();

            if (GameplayMovementBlocked)
            {
                MoveInput = Vector2.zero;
                SprintHeld = false;
                JumpHeld = false;
                JumpPressedThisFrame = false;
                JumpReleasedThisFrame = false;
                TrickModifierHeld = false;
            }

            if (GameplayLookBlocked)
            {
                LookInput = Vector2.zero;
            }
        }

        private static bool IsActionHeld(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (action.IsPressed())
            {
                return true;
            }

            return action.ReadValue<float>() > 0.5f;
        }
    }
}
