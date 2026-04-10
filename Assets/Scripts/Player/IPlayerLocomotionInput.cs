using UnityEngine;

namespace Race.Player
{
    public interface IPlayerLocomotionInput
    {
        Vector2 MoveInput { get; }
        Vector2 LookInput { get; }
        bool SprintHeld { get; }
        bool JumpHeld { get; }
        bool JumpPressedThisFrame { get; }
        bool JumpReleasedThisFrame { get; }
        Vector2 PointerScreenPosition { get; }
    }
}
