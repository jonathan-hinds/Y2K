#if UNITY_EDITOR
using System.IO;
using Race.Lighting;
using UnityEditor;
using UnityEngine;

namespace Race.EditorTools
{
    public static class LampPostPrefabFactory
    {
        private const string SourceModelPath = "Assets/LampPost01.fbx";
        private const string PrefabFolderPath = "Assets/Prefabs/Lighting";
        private const string PrefabPath = PrefabFolderPath + "/LampPostLit.prefab";
        private const string ProfileFolderPath = "Assets/Settings/Lighting";
        private const string ProfilePath = ProfileFolderPath + "/LampPostWarmBakedProfile.asset";

        [MenuItem("Tools/Race/Lighting/Create Lamp Post Prefab")]
        public static void CreateLampPostPrefab()
        {
            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            if (sourceModel == null)
            {
                Debug.LogError($"Lamp post source model not found at '{SourceModelPath}'.");
                return;
            }

            EnsureFolders();

            LampPostLightProfile profile = EnsureProfile();
            GameObject root = new("LampPostLit");

            try
            {
                GameObject visualRoot = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
                visualRoot.name = "Visual";
                visualRoot.transform.SetParent(root.transform, false);
                visualRoot.transform.localPosition = Vector3.zero;
                visualRoot.transform.localRotation = Quaternion.identity;
                visualRoot.transform.localScale = Vector3.one;
                PrefabUtility.UnpackPrefabInstance(visualRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                GameObject lightAnchor = new("LightAnchor");
                lightAnchor.transform.SetParent(root.transform, false);
                lightAnchor.transform.localPosition = Vector3.zero;
                lightAnchor.transform.localRotation = Quaternion.identity;
                lightAnchor.transform.localScale = Vector3.one;

                GameObject lightObject = new("Lamp Light");
                lightObject.transform.SetParent(lightAnchor.transform, false);
                lightObject.transform.localPosition = Vector3.zero;
                lightObject.transform.localRotation = Quaternion.identity;
                Light lightComponent = lightObject.AddComponent<Light>();

                LampPostLightAuthoring authoring = root.AddComponent<LampPostLightAuthoring>();
                SerializedObject serializedAuthoring = new(authoring);
                serializedAuthoring.FindProperty("profile").objectReferenceValue = profile;
                serializedAuthoring.FindProperty("visualRoot").objectReferenceValue = visualRoot.transform;
                serializedAuthoring.FindProperty("lightAnchor").objectReferenceValue = lightAnchor.transform;
                serializedAuthoring.FindProperty("lampLight").objectReferenceValue = lightComponent;
                serializedAuthoring.FindProperty("autoApplyInEditor").boolValue = true;
                serializedAuthoring.FindProperty("snapLightToVisualBounds").boolValue = true;
                serializedAuthoring.FindProperty("localLightOffset").vector3Value = new Vector3(0f, -0.35f, 0f);
                serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();

                authoring.ApplyAuthoring();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                GameObject createdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Selection.activeObject = createdPrefab;

                Debug.Log(
                    "Created lamp post lighting prefab at " +
                    PrefabPath +
                    ". Place this prefab around the map and bake lighting for the best performance.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets", "Prefabs");
            CreateFolderIfMissing("Assets/Prefabs", "Lighting");
            CreateFolderIfMissing("Assets", "Settings");
            CreateFolderIfMissing("Assets/Settings", "Lighting");
        }

        private static LampPostLightProfile EnsureProfile()
        {
            LampPostLightProfile profile = AssetDatabase.LoadAssetAtPath<LampPostLightProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<LampPostLightProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static void CreateFolderIfMissing(string parentFolder, string childFolder)
        {
            string combined = Path.Combine(parentFolder, childFolder).Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(combined))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentFolder, childFolder);
        }
    }
}
#endif
