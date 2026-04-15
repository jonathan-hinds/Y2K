using System.Collections.Generic;
using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int MoveMagnitudeHash = Animator.StringToHash("MoveMagnitude");
        private static readonly int JumpStartHash = Animator.StringToHash("JumpStart");
        private static readonly int JumpReleaseHash = Animator.StringToHash("JumpRelease");
        private static readonly int JumpHeldHash = Animator.StringToHash("JumpHeld");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int JumpPhaseHash = Animator.StringToHash("JumpPhase");
        private static readonly int LandHash = Animator.StringToHash("Land");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerAnimationProfile animationProfile;

        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new();
        private AnimatorOverrideController runtimeOverrideController;
        private PlayerRig playerRig;

        public Animator Animator => animator;
        public PlayerAnimationProfile AnimationProfile => animationProfile;
        public PlayerAnimationState CurrentState { get; private set; }

        private void Awake()
        {
            playerRig = GetComponent<PlayerRig>();
            ResolveAnimator();
            ApplyAnimationProfile();
        }

        private void OnValidate()
        {
            ResolveAnimator();
        }

        public void ApplyState(PlayerAnimationState state, float animationDampTime, float verticalSpeedDampTime)
        {
            if (animator == null)
            {
                return;
            }

            CurrentState = state;
            animator.SetFloat(MoveXHash, state.MoveX, animationDampTime, Time.deltaTime);
            animator.SetFloat(MoveYHash, state.MoveY, animationDampTime, Time.deltaTime);
            animator.SetFloat(MoveMagnitudeHash, state.MoveMagnitude, animationDampTime, Time.deltaTime);
            animator.SetBool(JumpHeldHash, state.JumpHeld);
            animator.SetBool(IsGroundedHash, state.IsGrounded);
            animator.SetFloat(VerticalSpeedHash, state.VerticalSpeed, verticalSpeedDampTime, Time.deltaTime);
            animator.SetInteger(JumpPhaseHash, state.JumpPhase);
        }

        public void TriggerJumpStart()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(JumpReleaseHash);
            animator.ResetTrigger(LandHash);
            animator.SetTrigger(JumpStartHash);
        }

        public void TriggerJumpRelease()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetTrigger(JumpReleaseHash);
        }

        public void TriggerLand()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetTrigger(LandHash);
        }

        public void ApplyAnimationProfile()
        {
            if (animator == null)
            {
                ResolveAnimator();
            }

            if (animator == null)
            {
                return;
            }

            RuntimeAnimatorController baseController = animationProfile != null && animationProfile.BaseController != null
                ? animationProfile.BaseController
                : animator.runtimeAnimatorController;

            if (baseController == null)
            {
                return;
            }

            if (animationProfile == null)
            {
                if (animator.runtimeAnimatorController != baseController)
                {
                    animator.runtimeAnimatorController = baseController;
                }

                runtimeOverrideController = null;
                overrides.Clear();
                return;
            }

            if (runtimeOverrideController == null || runtimeOverrideController.runtimeAnimatorController != baseController)
            {
                runtimeOverrideController = new AnimatorOverrideController(baseController);
            }

            overrides.Clear();
            runtimeOverrideController.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip sourceClip = overrides[i].Key;
                AnimationClip overrideClip = sourceClip;

                if (sourceClip != null && animationProfile.TryGetOverride(sourceClip.name, out AnimationClip configuredClip) && configuredClip != null)
                {
                    overrideClip = configuredClip;
                }

                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(sourceClip, overrideClip);
            }

            runtimeOverrideController.ApplyOverrides(overrides);

            if (animator.runtimeAnimatorController != runtimeOverrideController)
            {
                animator.runtimeAnimatorController = runtimeOverrideController;
            }
        }

        private void ResolveAnimator()
        {
            if (animator != null)
            {
                return;
            }

            if (playerRig == null)
            {
                playerRig = GetComponent<PlayerRig>();
            }

            animator = playerRig != null && playerRig.ModelRoot != null
                ? playerRig.ModelRoot.GetComponentInChildren<Animator>()
                : GetComponentInChildren<Animator>();
        }
    }
}
