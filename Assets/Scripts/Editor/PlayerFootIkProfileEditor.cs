using Race.Player;
using UnityEditor;
using UnityEngine;

namespace Race.Editor
{
    [CustomEditor(typeof(PlayerFootIkProfile))]
    public sealed class PlayerFootIkProfileEditor : UnityEditor.Editor
    {
        private static readonly GUIContent ClipContent = new("Clip");
        private static readonly GUIContent FrameCountContent = new("Frame Count");
        private static readonly GUIContent EnabledContent = new("Enabled");
        private static readonly GUIContent WindowsContent = new("Plant Windows");

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Author foot plants per animation clip. For each clip, define the frame windows where the left or right foot should stay locked to the ground. Frames outside those windows will release the foot so the passing and lift phases can play normally.",
                MessageType.Info);

            serializedObject.Update();

            SerializedProperty clipSettingsProperty = serializedObject.FindProperty("clipSettings");
            if (clipSettingsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No clip settings found. Run the animation setup tools to seed the profile from the bound model.",
                    MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            for (int i = 0; i < clipSettingsProperty.arraySize; i++)
            {
                SerializedProperty clipEntry = clipSettingsProperty.GetArrayElementAtIndex(i);
                DrawClipEntry(clipEntry, i);
                EditorGUILayout.Space(10f);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawClipEntry(SerializedProperty clipEntry, int index)
        {
            SerializedProperty sourceClipName = clipEntry.FindPropertyRelative("sourceClipName");
            SerializedProperty clip = clipEntry.FindPropertyRelative("clip");
            SerializedProperty frameCount = clipEntry.FindPropertyRelative("frameCount");
            SerializedProperty leftFoot = clipEntry.FindPropertyRelative("leftFoot");
            SerializedProperty rightFoot = clipEntry.FindPropertyRelative("rightFoot");

            string header = string.IsNullOrWhiteSpace(sourceClipName.stringValue)
                ? $"Clip {index + 1}"
                : sourceClipName.stringValue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(clip, ClipContent);
                    EditorGUILayout.PropertyField(frameCount, FrameCountContent);
                }

                EditorGUILayout.Space(4f);
                DrawFootSection("Left Foot", leftFoot, frameCount.intValue);
                EditorGUILayout.Space(4f);
                DrawFootSection("Right Foot", rightFoot, frameCount.intValue);
            }
        }

        private static void DrawFootSection(string label, SerializedProperty footProperty, int frameCount)
        {
            SerializedProperty enabledProperty = footProperty.FindPropertyRelative("enabled");
            SerializedProperty windowsProperty = footProperty.FindPropertyRelative("windows");

            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(enabledProperty, EnabledContent);

            if (!enabledProperty.boolValue)
            {
                EditorGUILayout.HelpBox($"{label} IK is disabled for this clip.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField($"{WindowsContent.text} ({windowsProperty.arraySize})", EditorStyles.miniLabel);

            EditorGUI.indentLevel++;
            for (int i = 0; i < windowsProperty.arraySize; i++)
            {
                DrawWindowEntry(windowsProperty.GetArrayElementAtIndex(i), i, frameCount);
            }

            if (windowsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No plant windows are set for {label}. Add one to define the exact planted frame range.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Window"))
                {
                    windowsProperty.InsertArrayElementAtIndex(windowsProperty.arraySize);
                    SerializedProperty newWindow = windowsProperty.GetArrayElementAtIndex(windowsProperty.arraySize - 1);
                    newWindow.FindPropertyRelative("startFrame").intValue = 0;
                    newWindow.FindPropertyRelative("endFrame").intValue = Mathf.Max(0, frameCount - 1);
                    newWindow.FindPropertyRelative("positionWeight").floatValue = 1f;
                    newWindow.FindPropertyRelative("rotationWeight").floatValue = 1f;
                }

                using (new EditorGUI.DisabledScope(windowsProperty.arraySize == 0))
                {
                    if (GUILayout.Button("Remove Last"))
                    {
                        windowsProperty.DeleteArrayElementAtIndex(windowsProperty.arraySize - 1);
                    }
                }
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawWindowEntry(SerializedProperty windowProperty, int index, int frameCount)
        {
            SerializedProperty startFrame = windowProperty.FindPropertyRelative("startFrame");
            SerializedProperty endFrame = windowProperty.FindPropertyRelative("endFrame");
            SerializedProperty positionWeight = windowProperty.FindPropertyRelative("positionWeight");
            SerializedProperty rotationWeight = windowProperty.FindPropertyRelative("rotationWeight");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Window {index + 1}", EditorStyles.miniBoldLabel);
                EditorGUILayout.IntSlider(startFrame, 0, Mathf.Max(0, frameCount - 1), new GUIContent("Start Frame"));
                EditorGUILayout.IntSlider(endFrame, 0, Mathf.Max(0, frameCount - 1), new GUIContent("End Frame"));
                EditorGUILayout.Slider(positionWeight, 0f, 1f, new GUIContent("Position Weight"));
                EditorGUILayout.Slider(rotationWeight, 0f, 1f, new GUIContent("Rotation Weight"));
            }
        }
    }
}
