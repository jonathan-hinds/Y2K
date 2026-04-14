using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Lighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BuiltInFogController : MonoBehaviour
    {
        [SerializeField] private BuiltInFogProfile profile;
        [SerializeField] private bool autoApplyInEditor = true;
        [SerializeField] private bool autoApplyAtRuntime = true;

        public BuiltInFogProfile Profile => profile;

        private void OnEnable()
        {
            if (!Application.isPlaying || autoApplyAtRuntime)
            {
                ApplyFog();
            }
        }

        private void Reset()
        {
            ApplyFog();
        }

        private void OnValidate()
        {
            if (Application.isPlaying || !autoApplyInEditor)
            {
                return;
            }

            ApplyFog();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !autoApplyAtRuntime)
            {
                return;
            }

            ApplyFog();
        }

        [ContextMenu("Apply Built-In Fog")]
        public void ApplyFog()
        {
            profile?.Apply();

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public void SetProfile(BuiltInFogProfile newProfile, bool applyImmediately = true)
        {
            profile = newProfile;
            if (applyImmediately)
            {
                ApplyFog();
            }
        }
    }
}
