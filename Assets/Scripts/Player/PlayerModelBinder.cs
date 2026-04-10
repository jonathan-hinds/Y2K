using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Player
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class PlayerModelBinder : MonoBehaviour
    {
        private const string GeneratedModelName = "ModelInstance";

        [SerializeField] private Transform modelRoot;
        [SerializeField] private GameObject sourceModelPrefab;
        [SerializeField] private Avatar sourceAvatar;
        [SerializeField, HideInInspector] private string boundSourceAssetPath;

        public Transform ModelRoot => modelRoot;
        public GameObject SourceModelPrefab => sourceModelPrefab;

        private void Reset()
        {
            ResolveModelRoot();
        }

        private void Awake()
        {
            ResolveModelRoot();
        }

        private void OnValidate()
        {
            ResolveModelRoot();

#if UNITY_EDITOR
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string sourceAssetPath = GetSourceAssetPath();
            if (sourceModelPrefab != null && sourceAvatar == null)
            {
                sourceAvatar = FindAvatarAtSourcePath(sourceAssetPath);
            }

            if (sourceAssetPath == boundSourceAssetPath)
            {
                return;
            }

            EditorApplication.delayCall -= HandleDelayedRebuild;
            EditorApplication.delayCall += HandleDelayedRebuild;
#endif
        }

        public Animator GetCurrentAnimator()
        {
            if (modelRoot == null)
            {
                return null;
            }

            return modelRoot.GetComponentInChildren<Animator>(true);
        }

        public bool HasRenderableSourceModel()
        {
#if UNITY_EDITOR
            if (sourceModelPrefab == null)
            {
                return false;
            }

            return sourceModelPrefab.GetComponentsInChildren<Renderer>(true).Length > 0;
#else
            return sourceModelPrefab != null;
#endif
        }

        private void ResolveModelRoot()
        {
            if (modelRoot != null)
            {
                return;
            }

            PlayerRig rig = GetComponent<PlayerRig>();
            modelRoot = rig != null ? rig.ModelRoot : transform;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Model Instance")]
        public void RebuildModelInstance()
        {
            ResolveModelRoot();
            if (modelRoot == null)
            {
                Debug.LogWarning("PlayerModelBinder could not resolve a model root.");
                return;
            }

            if (sourceModelPrefab == null)
            {
                Debug.LogWarning("PlayerModelBinder has no source model prefab assigned.");
                return;
            }

            if (!HasRenderableSourceModel())
            {
                Debug.LogWarning("PlayerModelBinder source model has no renderers. Rebuild skipped so the current model is not destroyed.");
                return;
            }

            RemoveExistingModelChildren();

            GameObject instance = PrefabUtility.InstantiatePrefab(sourceModelPrefab, modelRoot) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(sourceModelPrefab, modelRoot);
            }

            instance.name = GeneratedModelName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            EnsureAnimatorComponent(instance);

            boundSourceAssetPath = GetSourceAssetPath();
            SyncAnimatorReference();
            SyncFootIkRig();
            EditorUtility.SetDirty(this);
        }

        public void SetSourceModelPrefab(GameObject prefab)
        {
            sourceModelPrefab = prefab;
        }

        private void HandleDelayedRebuild()
        {
            EditorApplication.delayCall -= HandleDelayedRebuild;

            if (this == null)
            {
                return;
            }

            RebuildModelInstance();
        }

        private string GetSourceAssetPath()
        {
            return sourceModelPrefab != null ? AssetDatabase.GetAssetPath(sourceModelPrefab) : string.Empty;
        }

        private void RemoveExistingModelChildren()
        {
            for (int i = modelRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = modelRoot.GetChild(i);
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private void EnsureAnimatorComponent(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            if (sourceAvatar == null)
            {
                sourceAvatar = FindAvatarAtSourcePath(GetSourceAssetPath());
            }

            animator.avatar = sourceAvatar;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
        }

        private static Avatar FindAvatarAtSourcePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar)
                {
                    return avatar;
                }
            }

            return null;
        }

        private void SyncAnimatorReference()
        {
            PlayerAnimator playerAnimator = GetComponent<PlayerAnimator>();
            if (playerAnimator == null)
            {
                return;
            }

            SerializedObject serializedAnimator = new SerializedObject(playerAnimator);
            serializedAnimator.FindProperty("animator").objectReferenceValue = GetCurrentAnimator();
            serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
            playerAnimator.ApplyAnimationProfile();
            EditorUtility.SetDirty(playerAnimator);
        }

        private void SyncFootIkRig()
        {
            PlayerFootIkController footIkController = GetComponent<PlayerFootIkController>();
            if (footIkController == null)
            {
                return;
            }

            footIkController.RebuildRig();
            EditorUtility.SetDirty(footIkController);
        }
#endif
    }
}
