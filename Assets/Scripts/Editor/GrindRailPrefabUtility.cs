using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Race.Editor
{
    public static class GrindRailPrefabUtility
    {
        private const string PrefabPath = "Assets/Prefabs/Roads/GrindRail.prefab";

        [MenuItem("GameObject/Race/Grind Rail", false, 20)]
        public static void CreateGrindRail(MenuCommand command)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Missing Grind Rail Prefab",
                    $"Could not find {PrefabPath}. Reimport or recreate the grind rail prefab first.",
                    "OK");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
            if (instance == null)
            {
                return;
            }

            GameObject parent = command.context as GameObject;
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(instance, parent);
            }
            else
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    instance.transform.position = sceneView.pivot;
                }
            }

            Undo.RegisterCreatedObjectUndo(instance, "Create Grind Rail");
            Selection.activeGameObject = instance;
        }
    }
}
