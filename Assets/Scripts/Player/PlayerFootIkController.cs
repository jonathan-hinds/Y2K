using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerFootIkController : MonoBehaviour
    {
        [Header("Testing")]
        [SerializeField] private bool enableFootIk;

        [Header("References")]
        [SerializeField] private PlayerAnimator playerAnimator;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private RigBuilder rigBuilder;
        [SerializeField] private Rig footRig;
        [SerializeField] private TwoBoneIKConstraint leftFootConstraint;
        [SerializeField] private TwoBoneIKConstraint rightFootConstraint;
        [SerializeField] private Transform leftFootBone;
        [SerializeField] private Transform rightFootBone;
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;
        [SerializeField] private Transform leftFootHint;
        [SerializeField] private Transform rightFootHint;

        [Header("Grounding")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0f)] private float raycastStartHeight = 0.5f;
        [SerializeField, Min(0.01f)] private float raycastDistance = 1.5f;
        [SerializeField, Min(0f)] private float footHeightOffset = 0.02f;
        [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 75f;
        [SerializeField, Min(0f)] private float weightSharpness = 18f;
        [SerializeField, Min(0.01f)] private float maxHorizontalPlantCorrection = 0.45f;
        [SerializeField, Min(0.01f)] private float maxVerticalPlantCorrection = 0.35f;
        [SerializeField, Min(0f)] private float slideWeightStartSpeed = 3f;
        [SerializeField, Min(0f)] private float slideWeightFullSpeed = 9f;
        [SerializeField, Range(0f, 1f)] private float minSlidePlantWeight = 0.35f;

        [Header("Rig Discovery")]
        [SerializeField] private string leftUpperLegBoneName = "thigh.l";
        [SerializeField] private string leftLowerLegBoneName = "shin.l";
        [SerializeField] private string leftFootBoneName = "foot.l";
        [SerializeField] private string rightUpperLegBoneName = "thigh.r";
        [SerializeField] private string rightLowerLegBoneName = "shin.r";
        [SerializeField] private string rightFootBoneName = "foot.r";
        [SerializeField, Min(0f)] private float hintSideOffset = 0.2f;
        [SerializeField, Min(0f)] private float hintForwardOffset = 0.05f;

        private readonly List<AnimatorClipInfo> currentClipInfos = new();
        private readonly List<AnimatorClipInfo> nextClipInfos = new();

        public bool EnableFootIk
        {
            get => enableFootIk;
            set
            {
                enableFootIk = value;
                NotifyGroundingModeController();
            }
        }

        public bool IsFootIkActive => isActiveAndEnabled && enableFootIk;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Reset()
        {
            ResolveReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            NotifyGroundingModeController();
        }
#endif

        private void LateUpdate()
        {
            ResolveReferences();
            if (!ShouldApplyFootIk())
            {
                BlendConstraint(leftFootConstraint, 0f);
                BlendConstraint(rightFootConstraint, 0f);
                return;
            }

            UpdateFootConstraint(leftFootConstraint, leftFootBone, leftFootTarget, AvatarIKGoal.LeftFoot);
            UpdateFootConstraint(rightFootConstraint, rightFootBone, rightFootTarget, AvatarIKGoal.RightFoot);
        }

        private void UpdateFootConstraint(
            TwoBoneIKConstraint constraint,
            Transform footBone,
            Transform target,
            AvatarIKGoal goal)
        {
            if (constraint == null || footBone == null || target == null)
            {
                return;
            }

            float targetWeight = GetBlendedPlantWeight(goal);
            if (targetWeight > 0.0001f &&
                TryResolveGroundPose(footBone, out Vector3 position))
            {
                target.position = position;
                target.rotation = footBone.rotation;
                targetWeight *= EvaluatePlantCorrectionWeight(footBone, position);
                targetWeight *= EvaluateSlideWeight();
            }

            BlendConstraint(constraint, targetWeight);
        }

        private bool ShouldApplyFootIk()
        {
            if (!IsFootIkActive || playerAnimator == null || playerAnimator.Animator == null || playerAnimator.AnimationProfile == null)
            {
                return false;
            }

            if (playerMotor != null && !playerMotor.HasStableGroundContact)
            {
                return false;
            }

            if (leftFootConstraint == null || rightFootConstraint == null || leftFootTarget == null || rightFootTarget == null)
            {
                return false;
            }

            return playerAnimator.AnimationProfile.FootIkProfile != null;
        }

        private float GetBlendedPlantWeight(AvatarIKGoal goal)
        {
            Animator animator = playerAnimator.Animator;
            PlayerFootIkProfile profile = playerAnimator.AnimationProfile.FootIkProfile;
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

        private bool TryResolveGroundPose(Transform footBone, out Vector3 groundedPosition)
        {
            groundedPosition = default;

            Vector3 origin = footBone.position + Vector3.up * raycastStartHeight;
            float totalDistance = raycastStartHeight + raycastDistance;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, totalDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundAngle)
            {
                return false;
            }

            groundedPosition = hit.point + hit.normal * footHeightOffset;
            return true;
        }

        private float EvaluatePlantCorrectionWeight(Transform footBone, Vector3 groundedPosition)
        {
            if (footBone == null)
            {
                return 0f;
            }

            Vector3 correction = groundedPosition - footBone.position;
            Vector3 horizontalCorrection = Vector3.ProjectOnPlane(correction, Vector3.up);
            float horizontalDistance = horizontalCorrection.magnitude;
            float verticalDistance = Mathf.Abs(correction.y);

            float horizontalWeight = 1f - Mathf.InverseLerp(maxHorizontalPlantCorrection * 0.5f, maxHorizontalPlantCorrection, horizontalDistance);
            float verticalWeight = 1f - Mathf.InverseLerp(maxVerticalPlantCorrection * 0.5f, maxVerticalPlantCorrection, verticalDistance);
            return Mathf.Clamp01(Mathf.Min(horizontalWeight, verticalWeight));
        }

        private float EvaluateSlideWeight()
        {
            if (playerMotor == null || playerMotor.CurrentSlopeAngle <= 0.01f)
            {
                return 1f;
            }

            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, playerMotor.GroundNormal);
            if (downhill.sqrMagnitude <= 0.0001f)
            {
                return 1f;
            }

            downhill.Normalize();
            Vector3 planarVelocity = Vector3.ProjectOnPlane(playerMotor.WorldVelocity, Vector3.up);
            float downhillSpeed = Mathf.Max(0f, Vector3.Dot(planarVelocity, downhill));
            float slideBlend = Mathf.InverseLerp(slideWeightStartSpeed, Mathf.Max(slideWeightStartSpeed + 0.01f, slideWeightFullSpeed), downhillSpeed);
            return Mathf.Lerp(1f, minSlidePlantWeight, slideBlend);
        }

        private void BlendConstraint(TwoBoneIKConstraint constraint, float targetWeight)
        {
            if (constraint == null)
            {
                return;
            }

            float blend = 1f - Mathf.Exp(-weightSharpness * Time.deltaTime);
            constraint.weight = Mathf.Lerp(constraint.weight, targetWeight, blend);
        }

        private void ResolveReferences()
        {
            if (playerAnimator == null)
            {
                playerAnimator = GetComponent<PlayerAnimator>();
            }

            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }

            if (modelRoot == null)
            {
                PlayerRig playerRig = GetComponent<PlayerRig>();
                modelRoot = playerRig != null ? playerRig.ModelRoot : null;
            }

            if (rigBuilder == null && playerAnimator != null && playerAnimator.Animator != null)
            {
                rigBuilder = playerAnimator.Animator.GetComponent<RigBuilder>();
            }

            if (footRig == null && rigBuilder != null && rigBuilder.layers.Count > 0)
            {
                footRig = rigBuilder.layers[0].rig;
            }

            Transform rigRoot = footRig != null ? footRig.transform : (playerAnimator != null && playerAnimator.Animator != null ? playerAnimator.Animator.transform.Find("FootIKRig") : null);
            if (leftFootConstraint == null && rigRoot != null)
            {
                Transform holder = rigRoot.Find("LeftFootConstraint");
                leftFootConstraint = holder != null ? holder.GetComponent<TwoBoneIKConstraint>() : null;
            }

            if (rightFootConstraint == null && rigRoot != null)
            {
                Transform holder = rigRoot.Find("RightFootConstraint");
                rightFootConstraint = holder != null ? holder.GetComponent<TwoBoneIKConstraint>() : null;
            }

            if (leftFootTarget == null && rigRoot != null)
            {
                Transform target = rigRoot.Find("LeftFootTarget");
                leftFootTarget = target;
            }

            if (rightFootTarget == null && rigRoot != null)
            {
                Transform target = rigRoot.Find("RightFootTarget");
                rightFootTarget = target;
            }

            if (leftFootHint == null && rigRoot != null)
            {
                Transform hint = rigRoot.Find("LeftFootHint");
                leftFootHint = hint;
            }

            if (rightFootHint == null && rigRoot != null)
            {
                Transform hint = rigRoot.Find("RightFootHint");
                rightFootHint = hint;
            }

            if (leftFootBone == null)
            {
                leftFootBone = FindDescendantByName(modelRoot, leftFootBoneName);
            }

            if (rightFootBone == null)
            {
                rightFootBone = FindDescendantByName(modelRoot, rightFootBoneName);
            }
        }

#if UNITY_EDITOR
        public void RebuildRig()
        {
            ResolveReferences();
            Animator animator = playerAnimator != null ? playerAnimator.Animator : null;
            if (animator == null || modelRoot == null)
            {
                return;
            }

            Transform leftRoot = FindDescendantByName(modelRoot, leftUpperLegBoneName);
            Transform leftMid = FindDescendantByName(modelRoot, leftLowerLegBoneName);
            Transform leftTip = FindDescendantByName(modelRoot, leftFootBoneName);
            Transform rightRoot = FindDescendantByName(modelRoot, rightUpperLegBoneName);
            Transform rightMid = FindDescendantByName(modelRoot, rightLowerLegBoneName);
            Transform rightTip = FindDescendantByName(modelRoot, rightFootBoneName);
            if (leftRoot == null || leftMid == null || leftTip == null || rightRoot == null || rightMid == null || rightTip == null)
            {
                Debug.LogWarning("PlayerFootIkController could not resolve one or more foot IK bones. Update the bone name fields and rebuild.");
                return;
            }

            rigBuilder = animator.GetComponent<RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = animator.gameObject.AddComponent<RigBuilder>();
            }

            Transform rigRoot = FindOrCreateChild(animator.transform, "FootIKRig");
            footRig = rigRoot.GetComponent<Rig>();
            if (footRig == null)
            {
                footRig = rigRoot.gameObject.AddComponent<Rig>();
            }

            footRig.weight = 1f;
            leftFootBone = leftTip;
            rightFootBone = rightTip;

            leftFootTarget = FindOrCreateChild(rigRoot, "LeftFootTarget");
            rightFootTarget = FindOrCreateChild(rigRoot, "RightFootTarget");
            leftFootHint = FindOrCreateChild(rigRoot, "LeftFootHint");
            rightFootHint = FindOrCreateChild(rigRoot, "RightFootHint");

            AlignTargetToFoot(leftFootTarget, leftTip);
            AlignTargetToFoot(rightFootTarget, rightTip);
            PositionHint(leftFootHint, leftRoot, leftMid, leftTip, -1f);
            PositionHint(rightFootHint, rightRoot, rightMid, rightTip, 1f);

            leftFootConstraint = ConfigureConstraint(rigRoot, "LeftFootConstraint", leftRoot, leftMid, leftTip, leftFootTarget, leftFootHint);
            rightFootConstraint = ConfigureConstraint(rigRoot, "RightFootConstraint", rightRoot, rightMid, rightTip, rightFootTarget, rightFootHint);

            rigBuilder.layers = new List<RigLayer> { new RigLayer(footRig, true) };
            rigBuilder.Build();

            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(rigBuilder);
            EditorUtility.SetDirty(footRig);
            EditorUtility.SetDirty(leftFootConstraint);
            EditorUtility.SetDirty(rightFootConstraint);
        }

        private TwoBoneIKConstraint ConfigureConstraint(
            Transform rigRoot,
            string constraintName,
            Transform root,
            Transform mid,
            Transform tip,
            Transform target,
            Transform hint)
        {
            Transform holder = FindOrCreateChild(rigRoot, constraintName);
            TwoBoneIKConstraint constraint = holder.GetComponent<TwoBoneIKConstraint>();
            if (constraint == null)
            {
                constraint = holder.gameObject.AddComponent<TwoBoneIKConstraint>();
            }

            SerializedObject serializedConstraint = new SerializedObject(constraint);
            serializedConstraint.FindProperty("m_Weight").floatValue = 0f;
            serializedConstraint.FindProperty("m_Data.m_Root").objectReferenceValue = root;
            serializedConstraint.FindProperty("m_Data.m_Mid").objectReferenceValue = mid;
            serializedConstraint.FindProperty("m_Data.m_Tip").objectReferenceValue = tip;
            serializedConstraint.FindProperty("m_Data.m_Target").objectReferenceValue = target;
            serializedConstraint.FindProperty("m_Data.m_Hint").objectReferenceValue = hint;
            serializedConstraint.FindProperty("m_Data.m_TargetPositionWeight").floatValue = 1f;
            serializedConstraint.FindProperty("m_Data.m_TargetRotationWeight").floatValue = 0f;
            serializedConstraint.FindProperty("m_Data.m_HintWeight").floatValue = 1f;
            serializedConstraint.FindProperty("m_Data.m_MaintainTargetPositionOffset").boolValue = false;
            serializedConstraint.FindProperty("m_Data.m_MaintainTargetRotationOffset").boolValue = false;
            serializedConstraint.ApplyModifiedPropertiesWithoutUndo();
            return constraint;
        }

        private void AlignTargetToFoot(Transform target, Transform foot)
        {
            target.position = foot.position;
            target.rotation = foot.rotation;
        }

        private void PositionHint(Transform hint, Transform root, Transform mid, Transform tip, float sideSign)
        {
            Vector3 legDirection = (tip.position - root.position).normalized;
            Vector3 sideDirection = modelRoot != null ? modelRoot.right * sideSign : Vector3.right * sideSign;
            hint.position = mid.position + sideDirection * hintSideOffset + legDirection * hintForwardOffset;
            hint.rotation = Quaternion.identity;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

#endif

        private void NotifyGroundingModeController()
        {
            PlayerVisualGroundingModeController modeController = GetComponent<PlayerVisualGroundingModeController>();
            if (modeController != null)
            {
                modeController.SyncModeFromComponents();
            }
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendantByName(root.GetChild(i), targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
