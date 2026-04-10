using UnityEngine;

namespace Race.Player
{
    [CreateAssetMenu(fileName = "PlayerAnimationProfile", menuName = "Race/Player/Animation Profile")]
    public sealed class PlayerAnimationProfile : ScriptableObject
    {
        public const string IdleSourceClipName = "PLA_Idle";
        public const string MoveForwardSourceClipName = "PLA_MoveForward";
        public const string MoveRightSourceClipName = "PLA_MoveRight";
        public const string MoveBackwardSourceClipName = "PLA_MoveBackward";
        public const string MoveLeftSourceClipName = "PLA_MoveLeft";
        public const string JumpStartSourceClipName = "PLA_JumpStart";
        public const string JumpHoldSourceClipName = "PLA_JumpHold";
        public const string JumpReleaseSourceClipName = "PLA_JumpRelease";
        public const string JumpAscendingSourceClipName = "PLA_JumpAscending";
        public const string JumpDescendingSourceClipName = "PLA_JumpDescending";
        public const string LandingSourceClipName = "PLA_Landing";

        [SerializeField] private RuntimeAnimatorController baseController;

        [Header("Locomotion")]
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip moveForwardClip;
        [SerializeField] private AnimationClip moveRightClip;
        [SerializeField] private AnimationClip moveBackwardClip;
        [SerializeField] private AnimationClip moveLeftClip;

        [Header("Jump")]
        [SerializeField] private AnimationClip jumpStartClip;
        [SerializeField] private AnimationClip jumpHoldClip;
        [SerializeField] private AnimationClip jumpReleaseClip;
        [SerializeField] private AnimationClip jumpAscendingClip;
        [SerializeField] private AnimationClip jumpDescendingClip;
        [SerializeField] private AnimationClip landingClip;

        [Header("Foot IK")]
        [SerializeField] private PlayerFootIkProfile footIkProfile;

        public RuntimeAnimatorController BaseController => baseController;
        public PlayerFootIkProfile FootIkProfile => footIkProfile;

        public bool TryGetOverride(string sourceClipName, out AnimationClip clip)
        {
            clip = sourceClipName switch
            {
                IdleSourceClipName => idleClip,
                MoveForwardSourceClipName => moveForwardClip,
                MoveRightSourceClipName => moveRightClip,
                MoveBackwardSourceClipName => moveBackwardClip,
                MoveLeftSourceClipName => moveLeftClip,
                JumpStartSourceClipName => jumpStartClip,
                JumpHoldSourceClipName => jumpHoldClip,
                JumpReleaseSourceClipName => jumpReleaseClip,
                JumpAscendingSourceClipName => jumpAscendingClip,
                JumpDescendingSourceClipName => jumpDescendingClip,
                LandingSourceClipName => landingClip,
                _ => null
            };

            return clip != null;
        }

#if UNITY_EDITOR
        public void SetBaseController(RuntimeAnimatorController controller)
        {
            baseController = controller;
        }

        public void SetDefaultClip(string sourceClipName, AnimationClip clip)
        {
            switch (sourceClipName)
            {
                case IdleSourceClipName:
                    idleClip = clip;
                    break;
                case MoveForwardSourceClipName:
                    moveForwardClip = clip;
                    break;
                case MoveRightSourceClipName:
                    moveRightClip = clip;
                    break;
                case MoveBackwardSourceClipName:
                    moveBackwardClip = clip;
                    break;
                case MoveLeftSourceClipName:
                    moveLeftClip = clip;
                    break;
                case JumpStartSourceClipName:
                    jumpStartClip = clip;
                    break;
                case JumpHoldSourceClipName:
                    jumpHoldClip = clip;
                    break;
                case JumpReleaseSourceClipName:
                    jumpReleaseClip = clip;
                    break;
                case JumpAscendingSourceClipName:
                    jumpAscendingClip = clip;
                    break;
                case JumpDescendingSourceClipName:
                    jumpDescendingClip = clip;
                    break;
                case LandingSourceClipName:
                    landingClip = clip;
                    break;
            }
        }

        public void SetFootIkProfile(PlayerFootIkProfile profile)
        {
            footIkProfile = profile;
        }
#endif
    }
}
