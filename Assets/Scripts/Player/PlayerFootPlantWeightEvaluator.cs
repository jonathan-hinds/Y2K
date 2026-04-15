using System.Collections.Generic;
using UnityEngine;

namespace Race.Player
{
    public static class PlayerFootPlantWeightEvaluator
    {
        public static float Evaluate(
            PlayerAnimator playerAnimator,
            AvatarIKGoal goal,
            List<AnimatorClipInfo> currentClipInfos,
            List<AnimatorClipInfo> nextClipInfos)
        {
            if (playerAnimator == null)
            {
                return 0f;
            }

            Animator animator = playerAnimator.Animator;
            PlayerFootIkProfile profile = playerAnimator.AnimationProfile != null
                ? playerAnimator.AnimationProfile.FootIkProfile
                : null;

            if (animator == null || profile == null)
            {
                return 0f;
            }

            float weightedPlant = 0f;
            float totalWeight = 0f;

            currentClipInfos.Clear();
            animator.GetCurrentAnimatorClipInfo(0, currentClipInfos);
            float currentNormalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            AccumulatePlantWeights(profile, currentClipInfos, currentNormalizedTime, goal, ref weightedPlant, ref totalWeight);

            if (animator.IsInTransition(0))
            {
                nextClipInfos.Clear();
                animator.GetNextAnimatorClipInfo(0, nextClipInfos);
                float nextNormalizedTime = animator.GetNextAnimatorStateInfo(0).normalizedTime;
                AccumulatePlantWeights(profile, nextClipInfos, nextNormalizedTime, goal, ref weightedPlant, ref totalWeight);
            }

            if (totalWeight <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(weightedPlant / totalWeight);
        }

        private static void AccumulatePlantWeights(
            PlayerFootIkProfile profile,
            List<AnimatorClipInfo> clipInfos,
            float normalizedTime,
            AvatarIKGoal goal,
            ref float weightedPlant,
            ref float totalWeight)
        {
            for (int i = 0; i < clipInfos.Count; i++)
            {
                AnimatorClipInfo clipInfo = clipInfos[i];
                if (clipInfo.clip == null || clipInfo.weight <= 0.0001f)
                {
                    continue;
                }

                if (!profile.TryGetSettings(clipInfo.clip, out FootIkClipSettings settings))
                {
                    continue;
                }

                float plantWeight = 0f;
                if (settings.TryEvaluate(goal, normalizedTime, out FootPlantWindow window))
                {
                    plantWeight = window.PositionWeight;
                }

                weightedPlant += plantWeight * clipInfo.weight;
                totalWeight += clipInfo.weight;
            }
        }
    }
}
