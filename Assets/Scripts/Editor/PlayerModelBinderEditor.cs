using Race.Player;
using UnityEditor;
using UnityEngine;

namespace Race.Editor
{
    [CustomEditor(typeof(PlayerModelBinder))]
    public sealed class PlayerModelBinderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Assign the current FBX model here. When the FBX changes, reassign it in this one field and rebuild the model instance.",
                MessageType.Info);

            PlayerModelBinder binder = (PlayerModelBinder)target;
            if (binder.SourceModelPrefab != null && !binder.HasRenderableSourceModel())
            {
                EditorGUILayout.HelpBox(
                    "The assigned FBX currently has no renderers in Unity. Rebuilding would create an invisible skeleton only. Check the Blender export or FBX import contents first.",
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("modelRoot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceModelPrefab"), new GUIContent("Source Model FBX"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceAvatar"), new GUIContent("Source Avatar"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(binder.SourceModelPrefab == null))
            {
                if (GUILayout.Button("Rebuild Model Instance"))
                {
                    binder.RebuildModelInstance();
                }
            }
        }
    }
}
