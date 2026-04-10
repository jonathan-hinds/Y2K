using Race.Player;
using UnityEditor;
using UnityEngine;

namespace Race.Editor
{
    public static class PlayerModelBindingSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Tools/Race/Animations/Convert Player To Model Binder")]
        public static void ConvertPlayerToModelBinder()
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("Player prefab not found.");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerRig rig = prefabContents.GetComponent<PlayerRig>();
                if (rig == null || rig.ModelRoot == null)
                {
                    Debug.LogError("PlayerRig or ModelRoot is missing on the player prefab.");
                    return;
                }

                PlayerModelBinder binder = prefabContents.GetComponent<PlayerModelBinder>();
                if (binder == null)
                {
                    binder = prefabContents.AddComponent<PlayerModelBinder>();
                }

                GameObject sourcePrefab = null;
                if (rig.ModelRoot.childCount > 0)
                {
                    GameObject currentModelInstance = rig.ModelRoot.GetChild(0).gameObject;
                    sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(currentModelInstance);
                }

                SerializedObject serializedBinder = new SerializedObject(binder);
                serializedBinder.FindProperty("modelRoot").objectReferenceValue = rig.ModelRoot;
                if (sourcePrefab != null)
                {
                    serializedBinder.FindProperty("sourceModelPrefab").objectReferenceValue = sourcePrefab;
                }
                serializedBinder.ApplyModifiedPropertiesWithoutUndo();

                binder.RebuildModelInstance();

                PrefabUtility.SaveAsPrefabAsset(prefabContents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Player prefab now uses PlayerModelBinder. Reassign future FBX updates in one place on the binder.");
        }
    }
}
