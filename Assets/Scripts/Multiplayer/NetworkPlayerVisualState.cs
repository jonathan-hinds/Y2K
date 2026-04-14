using System;
using Unity.Netcode;
using UnityEngine;

namespace Race.Multiplayer
{
    public struct NetworkPlayerVisualState : INetworkSerializable, IEquatable<NetworkPlayerVisualState>
    {
        private const float AxisScale = 1000f;
        private const float MagnitudeScale = 1000f;
        private const float VerticalSpeedScale = 100f;
        private const float VerticalSpeedRange = 512f;

        public short MoveX;
        public short MoveY;
        public ushort MoveMagnitude;
        public short VerticalSpeed;
        public ushort FacingYaw;
        public byte JumpPhase;
        public byte Flags;

        public bool JumpHeld
        {
            readonly get => (Flags & 1) != 0;
            set => Flags = value ? (byte)(Flags | 1) : (byte)(Flags & ~1);
        }

        public bool IsGrounded
        {
            readonly get => (Flags & 2) != 0;
            set => Flags = value ? (byte)(Flags | 2) : (byte)(Flags & ~2);
        }

        public static NetworkPlayerVisualState From(Race.Player.PlayerAnimationState state, Vector3 facingForward)
        {
            float yaw = Mathf.Atan2(facingForward.x, facingForward.z) * Mathf.Rad2Deg;
            if (yaw < 0f)
            {
                yaw += 360f;
            }

            var networkState = new NetworkPlayerVisualState
            {
                MoveX = QuantizeSigned(state.MoveX, AxisScale),
                MoveY = QuantizeSigned(state.MoveY, AxisScale),
                MoveMagnitude = QuantizeUnsigned(state.MoveMagnitude, MagnitudeScale),
                VerticalSpeed = QuantizeVerticalSpeed(state.VerticalSpeed),
                FacingYaw = (ushort)Mathf.RoundToInt(Mathf.Clamp(yaw, 0f, 359.99f) / 360f * ushort.MaxValue),
                JumpPhase = (byte)Mathf.Clamp(state.JumpPhase, byte.MinValue, byte.MaxValue),
                Flags = 0
            };

            networkState.JumpHeld = state.JumpHeld;
            networkState.IsGrounded = state.IsGrounded;
            return networkState;
        }

        public readonly Race.Player.PlayerAnimationState ToAnimationState()
        {
            return new Race.Player.PlayerAnimationState(
                MoveX / AxisScale,
                MoveY / AxisScale,
                MoveMagnitude / MagnitudeScale,
                JumpHeld,
                IsGrounded,
                VerticalSpeed / VerticalSpeedScale,
                JumpPhase);
        }

        public readonly Quaternion ToFacingRotation()
        {
            float yaw = FacingYaw / (float)ushort.MaxValue * 360f;
            return Quaternion.Euler(0f, yaw, 0f);
        }

        public readonly bool Equals(NetworkPlayerVisualState other)
        {
            return MoveX == other.MoveX
                && MoveY == other.MoveY
                && MoveMagnitude == other.MoveMagnitude
                && VerticalSpeed == other.VerticalSpeed
                && FacingYaw == other.FacingYaw
                && JumpPhase == other.JumpPhase
                && Flags == other.Flags;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is NetworkPlayerVisualState other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(MoveX, MoveY, MoveMagnitude, VerticalSpeed, FacingYaw, JumpPhase, Flags);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MoveX);
            serializer.SerializeValue(ref MoveY);
            serializer.SerializeValue(ref MoveMagnitude);
            serializer.SerializeValue(ref VerticalSpeed);
            serializer.SerializeValue(ref FacingYaw);
            serializer.SerializeValue(ref JumpPhase);
            serializer.SerializeValue(ref Flags);
        }

        private static short QuantizeSigned(float value, float scale)
        {
            return (short)Mathf.Clamp(Mathf.RoundToInt(value * scale), short.MinValue, short.MaxValue);
        }

        private static ushort QuantizeUnsigned(float value, float scale)
        {
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(value * scale), ushort.MinValue, ushort.MaxValue);
        }

        private static short QuantizeVerticalSpeed(float value)
        {
            float clamped = Mathf.Clamp(value, -VerticalSpeedRange, VerticalSpeedRange);
            return QuantizeSigned(clamped, VerticalSpeedScale);
        }
    }
}
