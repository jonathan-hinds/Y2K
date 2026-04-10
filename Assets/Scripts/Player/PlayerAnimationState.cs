namespace Race.Player
{
    public readonly struct PlayerAnimationState
    {
        public PlayerAnimationState(
            float moveX,
            float moveY,
            float moveMagnitude,
            bool jumpHeld,
            bool isGrounded,
            float verticalSpeed,
            int jumpPhase)
        {
            MoveX = moveX;
            MoveY = moveY;
            MoveMagnitude = moveMagnitude;
            JumpHeld = jumpHeld;
            IsGrounded = isGrounded;
            VerticalSpeed = verticalSpeed;
            JumpPhase = jumpPhase;
        }

        public float MoveX { get; }
        public float MoveY { get; }
        public float MoveMagnitude { get; }
        public bool JumpHeld { get; }
        public bool IsGrounded { get; }
        public float VerticalSpeed { get; }
        public int JumpPhase { get; }
    }
}
