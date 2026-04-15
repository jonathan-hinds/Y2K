using System.Collections.Generic;
using System.IO;
using Race.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Race.Editor
{
    public static class PlayerAnimationAssetBuilder
    {
        private const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";
        private const string ProfilePath = "Assets/Animations/PlayerAnimationProfile.asset";
        private const string FootIkProfilePath = "Assets/Animations/PlayerFootIkProfile.asset";
        private const string GeneratedDirectory = "Assets/Animations/Generated";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string IdleClipName = "Armature|Idle";
        private const string ForwardClipName = "Armature|Walk";
        private const string StrafeLeftClipName = "Armature|Straf Left";
        private const string StrafeRightClipName = "Armature|Straf Right";
        private const string JumpStartClipName = "Armature|JumpStart";
        private const string JumpHoldClipName = "Armature|JumpStartHold";
        private const string JumpReleaseClipName = "Armature|JumpRelease";
        private const string JumpAscendingClipName = "Armature|JumpingUpwardAirHold";
        private const string JumpDescendingClipName = "Armature|JumpingDownwardAirHold";
        private const string WallRideIdleClipName = "Armature|WallRideIdle";
        private const string WallRideLeftClipName = "Armature|WallRideLeft";
        private const string WallRideRightClipName = "Armature|WallRideRight";
        private const string WallRideJumpStartClipName = "Armature|WallRideJumpStart";
        private const string WallRideJumpHoldClipName = "Armature|WallRideJumpHold";

        [MenuItem("Tools/Race/Animations/Build Modular Player Animation Setup")]
        public static void BuildModularPlayerAnimationSetup()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("Player animation controller was not found.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(GeneratedDirectory))
            {
                Directory.CreateDirectory(GeneratedDirectory);
                AssetDatabase.Refresh();
            }

            Dictionary<string, AnimationClip> generatedClips = BuildStandaloneClips(controller);
            RewireController(controller, generatedClips);
            EnsureControllerSupportsIk(controller);
            PlayerFootIkProfile footIkProfile = CreateOrUpdateFootIkProfile(generatedClips);
            PlayerAnimationProfile profile = CreateOrUpdateProfile(controller, generatedClips, footIkProfile);
            WirePlayerPrefab(profile);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(footIkProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Player animation setup has been modularized. Assign future imported clips on PlayerAnimationProfile.");
        }

        [MenuItem("Tools/Race/Animations/Auto Assign Profile From Bound Model")]
        public static void AutoAssignProfileFromBoundModel()
        {
            PlayerAnimationProfile profile = AssetDatabase.LoadAssetAtPath<PlayerAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("Player animation profile was not found.");
                return;
            }

            if (!TryGetBoundModelAssetPath(out string modelAssetPath))
            {
                Debug.LogError("Could not resolve the bound source model from the player prefab.");
                return;
            }

            Dictionary<string, AnimationClip> clipsByName = LoadAnimationClipsByName(modelAssetPath);
            var serializedProfile = new SerializedObject(profile);

            AssignProfileClip(serializedProfile, "idleClip", clipsByName, IdleClipName);
            AssignProfileClip(serializedProfile, "moveForwardClip", clipsByName, ForwardClipName);
            AssignProfileClip(serializedProfile, "moveRightClip", clipsByName, StrafeRightClipName);
            AssignProfileClip(serializedProfile, "moveBackwardClip", clipsByName, ForwardClipName);
            AssignProfileClip(serializedProfile, "moveLeftClip", clipsByName, StrafeLeftClipName);
            AssignProfileClip(serializedProfile, "jumpStartClip", clipsByName, JumpStartClipName);
            AssignProfileClip(serializedProfile, "jumpHoldClip", clipsByName, JumpHoldClipName);
            AssignProfileClip(serializedProfile, "jumpReleaseClip", clipsByName, JumpReleaseClipName);
            AssignProfileClip(serializedProfile, "jumpAscendingClip", clipsByName, JumpAscendingClipName);
            AssignProfileClip(serializedProfile, "jumpDescendingClip", clipsByName, JumpDescendingClipName);
            AssignProfileClip(serializedProfile, "wallRideIdleClip", clipsByName, WallRideIdleClipName);
            AssignProfileClip(serializedProfile, "wallRideLeftClip", clipsByName, WallRideLeftClipName);
            AssignProfileClip(serializedProfile, "wallRideRightClip", clipsByName, WallRideRightClipName);
            AssignProfileClip(serializedProfile, "wallRideJumpStartClip", clipsByName, WallRideJumpStartClipName);
            AssignProfileClip(serializedProfile, "wallRideJumpHoldClip", clipsByName, WallRideJumpHoldClipName);
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            PlayerFootIkProfile footIkProfile = AssetDatabase.LoadAssetAtPath<PlayerFootIkProfile>(FootIkProfilePath);
            if (footIkProfile == null)
            {
                footIkProfile = CreateOrUpdateFootIkProfile(new Dictionary<string, AnimationClip>());
            }

            AutoAssignFootIkProfile(footIkProfile, clipsByName);
            profile.SetFootIkProfile(footIkProfile);

            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(footIkProfile);
            AssetDatabase.SaveAssets();
            Debug.Log("Player animation profile was updated from the bound model clips.");
        }

        private static Dictionary<string, AnimationClip> BuildStandaloneClips(AnimatorController controller)
        {
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = FindState(rootStateMachine, "Locomotion");
            BlendTree locomotionBlendTree = locomotionState != null ? locomotionState.motion as BlendTree : null;

            var generatedClips = new Dictionary<string, AnimationClip>
            {
                [PlayerAnimationProfile.IdleSourceClipName] = CopyOrCreateClip(
                    GetBlendTreeMotion(locomotionBlendTree, 0),
                    PlayerAnimationProfile.IdleSourceClipName),
                [PlayerAnimationProfile.MoveForwardSourceClipName] = CopyOrCreateClip(
                    GetBlendTreeMotion(locomotionBlendTree, 1),
                    PlayerAnimationProfile.MoveForwardSourceClipName),
                [PlayerAnimationProfile.MoveRightSourceClipName] = CopyOrCreateClip(
                    GetBlendTreeMotion(locomotionBlendTree, 2),
                    PlayerAnimationProfile.MoveRightSourceClipName),
                [PlayerAnimationProfile.MoveBackwardSourceClipName] = CopyOrCreateClip(
                    GetBlendTreeMotion(locomotionBlendTree, 3),
                    PlayerAnimationProfile.MoveBackwardSourceClipName),
                [PlayerAnimationProfile.MoveLeftSourceClipName] = CopyOrCreateClip(
                    GetBlendTreeMotion(locomotionBlendTree, 4),
                    PlayerAnimationProfile.MoveLeftSourceClipName),
                [PlayerAnimationProfile.JumpStartSourceClipName] = CopyOrCreateClip(
                    GetStateMotion(rootStateMachine, "JumpStart"),
                    PlayerAnimationProfile.JumpStartSourceClipName),
                [PlayerAnimationProfile.JumpHoldSourceClipName] = CopyOrCreateClip(
                    GetStateMotion(rootStateMachine, "JumpHold"),
                    PlayerAnimationProfile.JumpHoldSourceClipName),
                [PlayerAnimationProfile.JumpReleaseSourceClipName] = CopyOrCreateClip(
                    GetStateMotion(rootStateMachine, "JumpRelease"),
                    PlayerAnimationProfile.JumpReleaseSourceClipName),
                [PlayerAnimationProfile.JumpAscendingSourceClipName] = CopyOrCreateClip(
                    GetStateMotion(rootStateMachine, "JumpingUpwardAirHold"),
                    PlayerAnimationProfile.JumpAscendingSourceClipName),
                [PlayerAnimationProfile.JumpDescendingSourceClipName] = CopyOrCreateClip(
                    GetStateMotion(rootStateMachine, "JumpingDownwardAirHold"),
                    PlayerAnimationProfile.JumpDescendingSourceClipName),
                [PlayerAnimationProfile.LandingSourceClipName] = CopyOrCreateClip(
                    GetStateMotion(rootStateMachine, "JumpingLanding"),
                    PlayerAnimationProfile.LandingSourceClipName)
            };

            return generatedClips;
        }

        private static void RewireController(AnimatorController controller, IReadOnlyDictionary<string, AnimationClip> generatedClips)
        {
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = FindState(rootStateMachine, "Locomotion");
            BlendTree locomotionBlendTree = locomotionState != null ? locomotionState.motion as BlendTree : null;

            SetBlendTreeMotion(locomotionBlendTree, 0, generatedClips[PlayerAnimationProfile.IdleSourceClipName]);
            SetBlendTreeMotion(locomotionBlendTree, 1, generatedClips[PlayerAnimationProfile.MoveForwardSourceClipName]);
            SetBlendTreeMotion(locomotionBlendTree, 2, generatedClips[PlayerAnimationProfile.MoveRightSourceClipName]);
            SetBlendTreeMotion(locomotionBlendTree, 3, generatedClips[PlayerAnimationProfile.MoveBackwardSourceClipName]);
            SetBlendTreeMotion(locomotionBlendTree, 4, generatedClips[PlayerAnimationProfile.MoveLeftSourceClipName]);

            SetStateMotion(rootStateMachine, "JumpStart", generatedClips[PlayerAnimationProfile.JumpStartSourceClipName]);
            SetStateMotion(rootStateMachine, "JumpHold", generatedClips[PlayerAnimationProfile.JumpHoldSourceClipName]);
            SetStateMotion(rootStateMachine, "JumpRelease", generatedClips[PlayerAnimationProfile.JumpReleaseSourceClipName]);
            SetStateMotion(rootStateMachine, "JumpingUpwardAirHold", generatedClips[PlayerAnimationProfile.JumpAscendingSourceClipName]);
            SetStateMotion(rootStateMachine, "JumpingDownwardAirHold", generatedClips[PlayerAnimationProfile.JumpDescendingSourceClipName]);
            SetStateMotion(rootStateMachine, "JumpingLanding", generatedClips[PlayerAnimationProfile.LandingSourceClipName]);
        }

        private static PlayerAnimationProfile CreateOrUpdateProfile(
            RuntimeAnimatorController controller,
            IReadOnlyDictionary<string, AnimationClip> generatedClips,
            PlayerFootIkProfile footIkProfile)
        {
            PlayerAnimationProfile profile = AssetDatabase.LoadAssetAtPath<PlayerAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlayerAnimationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("baseController").objectReferenceValue = controller;
            AssignDefaultProfileClip(serializedProfile, "idleClip", generatedClips[PlayerAnimationProfile.IdleSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "moveForwardClip", generatedClips[PlayerAnimationProfile.MoveForwardSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "moveRightClip", generatedClips[PlayerAnimationProfile.MoveRightSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "moveBackwardClip", generatedClips[PlayerAnimationProfile.MoveBackwardSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "moveLeftClip", generatedClips[PlayerAnimationProfile.MoveLeftSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "jumpStartClip", generatedClips[PlayerAnimationProfile.JumpStartSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "jumpHoldClip", generatedClips[PlayerAnimationProfile.JumpHoldSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "jumpReleaseClip", generatedClips[PlayerAnimationProfile.JumpReleaseSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "jumpAscendingClip", generatedClips[PlayerAnimationProfile.JumpAscendingSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "jumpDescendingClip", generatedClips[PlayerAnimationProfile.JumpDescendingSourceClipName]);
            AssignDefaultProfileClip(serializedProfile, "landingClip", generatedClips[PlayerAnimationProfile.LandingSourceClipName]);
            serializedProfile.FindProperty("footIkProfile").objectReferenceValue = footIkProfile;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            return profile;
        }

        private static PlayerFootIkProfile CreateOrUpdateFootIkProfile(IReadOnlyDictionary<string, AnimationClip> clipsBySourceName)
        {
            PlayerFootIkProfile profile = AssetDatabase.LoadAssetAtPath<PlayerFootIkProfile>(FootIkProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlayerFootIkProfile>();
                AssetDatabase.CreateAsset(profile, FootIkProfilePath);
            }

            SyncFootIkEntry(profile, PlayerAnimationProfile.IdleSourceClipName, clipsBySourceName);
            SyncFootIkEntry(profile, PlayerAnimationProfile.MoveForwardSourceClipName, clipsBySourceName);
            SyncFootIkEntry(profile, PlayerAnimationProfile.MoveBackwardSourceClipName, clipsBySourceName);
            SyncFootIkEntry(profile, PlayerAnimationProfile.MoveLeftSourceClipName, clipsBySourceName);
            SyncFootIkEntry(profile, PlayerAnimationProfile.MoveRightSourceClipName, clipsBySourceName);
            return profile;
        }

        private static void AutoAssignFootIkProfile(PlayerFootIkProfile profile, IReadOnlyDictionary<string, AnimationClip> clipsByName)
        {
            AssignFootIkClip(profile, PlayerAnimationProfile.IdleSourceClipName, clipsByName, IdleClipName);
            AssignFootIkClip(profile, PlayerAnimationProfile.MoveForwardSourceClipName, clipsByName, ForwardClipName);
            AssignFootIkClip(profile, PlayerAnimationProfile.MoveBackwardSourceClipName, clipsByName, ForwardClipName);
            AssignFootIkClip(profile, PlayerAnimationProfile.MoveLeftSourceClipName, clipsByName, StrafeLeftClipName);
            AssignFootIkClip(profile, PlayerAnimationProfile.MoveRightSourceClipName, clipsByName, StrafeRightClipName);
        }

        private static void WirePlayerPrefab(PlayerAnimationProfile profile)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogWarning("Player prefab was not found. Animation assets were built, but prefab wiring was skipped.");
                return;
            }

            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerRig rig = prefabInstance.GetComponent<PlayerRig>();
                PlayerAnimationDriver driver = prefabInstance.GetComponent<PlayerAnimationDriver>();
                PlayerAnimator playerAnimator = prefabInstance.GetComponent<PlayerAnimator>();
                PlayerFootIkController footIkController = prefabInstance.GetComponent<PlayerFootIkController>();
                PlayerVisualGroundingModeController groundingModeController = prefabInstance.GetComponent<PlayerVisualGroundingModeController>();

                if (driver == null)
                {
                    Debug.LogWarning("PlayerAnimationDriver was not found on the player prefab.");
                    return;
                }

                if (playerAnimator == null)
                {
                    playerAnimator = prefabInstance.AddComponent<PlayerAnimator>();
                }

                if (footIkController == null)
                {
                    footIkController = prefabInstance.AddComponent<PlayerFootIkController>();
                }

                if (groundingModeController == null)
                {
                    groundingModeController = prefabInstance.AddComponent<PlayerVisualGroundingModeController>();
                }

                Animator animator = rig != null && rig.ModelRoot != null
                    ? rig.ModelRoot.GetComponentInChildren<Animator>(true)
                    : prefabInstance.GetComponentInChildren<Animator>(true);

                var animatorSo = new SerializedObject(playerAnimator);
                animatorSo.FindProperty("animator").objectReferenceValue = animator;
                animatorSo.FindProperty("animationProfile").objectReferenceValue = profile;
                animatorSo.ApplyModifiedPropertiesWithoutUndo();
                playerAnimator.ApplyAnimationProfile();

                var driverSo = new SerializedObject(driver);
                driverSo.FindProperty("playerAnimator").objectReferenceValue = playerAnimator;
                driverSo.ApplyModifiedPropertiesWithoutUndo();

                var footIkSo = new SerializedObject(footIkController);
                footIkSo.FindProperty("playerAnimator").objectReferenceValue = playerAnimator;
                footIkSo.FindProperty("playerMotor").objectReferenceValue = prefabInstance.GetComponent<PlayerMotor>();
                footIkSo.FindProperty("modelRoot").objectReferenceValue = rig != null ? rig.ModelRoot : null;
                footIkSo.ApplyModifiedPropertiesWithoutUndo();
                footIkController.RebuildRig();

                PrefabUtility.SaveAsPrefabAsset(prefabInstance, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }
        }

        private static AnimationClip CopyOrCreateClip(AnimationClip sourceClip, string clipName)
        {
            string assetPath = Path.Combine(GeneratedDirectory, clipName + ".anim").Replace("\\", "/");
            AnimationClip targetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (targetClip == null)
            {
                targetClip = new AnimationClip();
                AssetDatabase.CreateAsset(targetClip, assetPath);
            }

            if (sourceClip != null)
            {
                EditorUtility.CopySerialized(sourceClip, targetClip);
            }
            else
            {
                targetClip.ClearCurves();
                targetClip.frameRate = 60f;
            }

            targetClip.name = clipName;
            EditorUtility.SetDirty(targetClip);
            return targetClip;
        }

        private static AnimationClip GetStateMotion(AnimatorStateMachine rootStateMachine, string stateName)
        {
            AnimatorState state = FindState(rootStateMachine, stateName);
            return state != null ? state.motion as AnimationClip : null;
        }

        private static AnimationClip GetBlendTreeMotion(BlendTree blendTree, int index)
        {
            if (blendTree == null || index < 0 || index >= blendTree.children.Length)
            {
                return null;
            }

            return blendTree.children[index].motion as AnimationClip;
        }

        private static void SetStateMotion(AnimatorStateMachine rootStateMachine, string stateName, Motion motion)
        {
            AnimatorState state = FindState(rootStateMachine, stateName);
            if (state != null)
            {
                state.motion = motion;
                EditorUtility.SetDirty(state);
            }
        }

        private static void SetBlendTreeMotion(BlendTree blendTree, int index, Motion motion)
        {
            if (blendTree == null || index < 0 || index >= blendTree.children.Length)
            {
                return;
            }

            ChildMotion[] children = blendTree.children;
            children[index].motion = motion;
            blendTree.children = children;
            EditorUtility.SetDirty(blendTree);
        }

        private static AnimatorState FindState(AnimatorStateMachine rootStateMachine, string stateName)
        {
            var stack = new Stack<AnimatorStateMachine>();
            stack.Push(rootStateMachine);

            while (stack.Count > 0)
            {
                AnimatorStateMachine stateMachine = stack.Pop();

                foreach (ChildAnimatorState childState in stateMachine.states)
                {
                    if (childState.state != null && childState.state.name == stateName)
                    {
                        return childState.state;
                    }
                }

                foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
                {
                    if (childStateMachine.stateMachine != null)
                    {
                        stack.Push(childStateMachine.stateMachine);
                    }
                }
            }

            return null;
        }

        private static void AssignDefaultProfileClip(SerializedObject serializedProfile, string propertyName, AnimationClip fallbackClip)
        {
            SerializedProperty property = serializedProfile.FindProperty(propertyName);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = fallbackClip;
            }
        }

        private static void AssignProfileClip(SerializedObject serializedProfile, string propertyName, IReadOnlyDictionary<string, AnimationClip> clipsByName, string clipName)
        {
            if (clipsByName.TryGetValue(clipName, out AnimationClip clip))
            {
                serializedProfile.FindProperty(propertyName).objectReferenceValue = clip;
            }
        }

        private static void SyncFootIkEntry(PlayerFootIkProfile profile, string sourceClipName, IReadOnlyDictionary<string, AnimationClip> clipsBySourceName)
        {
            FootIkClipSettings settings = profile.GetOrCreateSettings(sourceClipName);
            settings.SetClip(clipsBySourceName.TryGetValue(sourceClipName, out AnimationClip clip) ? clip : null);
            settings.SeedDefaultsIfEmpty();
            EditorUtility.SetDirty(profile);
        }

        private static void AssignFootIkClip(PlayerFootIkProfile profile, string sourceClipName, IReadOnlyDictionary<string, AnimationClip> clipsByName, string clipName)
        {
            FootIkClipSettings settings = profile.GetOrCreateSettings(sourceClipName);
            if (clipsByName.TryGetValue(clipName, out AnimationClip clip))
            {
                settings.SetClip(clip);
                settings.SeedDefaultsIfEmpty();
            }
        }

        private static void EnsureControllerSupportsIk(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            bool changed = false;
            for (int i = 0; i < layers.Length; i++)
            {
                if (!layers[i].iKPass)
                {
                    layers[i].iKPass = true;
                    changed = true;
                }
            }

            if (changed)
            {
                controller.layers = layers;
            }
        }

        private static bool TryGetBoundModelAssetPath(out string assetPath)
        {
            assetPath = null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                return false;
            }

            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerModelBinder binder = prefabInstance.GetComponent<PlayerModelBinder>();
                if (binder == null)
                {
                    return false;
                }

                var serializedBinder = new SerializedObject(binder);
                GameObject sourceModel = serializedBinder.FindProperty("sourceModelPrefab").objectReferenceValue as GameObject;
                if (sourceModel == null)
                {
                    return false;
                }

                assetPath = AssetDatabase.GetAssetPath(sourceModel);
                return !string.IsNullOrEmpty(assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }
        }

        private static Dictionary<string, AnimationClip> LoadAnimationClipsByName(string assetPath)
        {
            var clips = new Dictionary<string, AnimationClip>();
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clips[clip.name] = clip;
                }
            }

            return clips;
        }
    }
}
