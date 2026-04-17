using System.Collections.Generic;
using PaintIn3D;
using Race.Player;
using Race.Tagging;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Race.Editor
{
    public static class TaggingPaintIn3DSetupUtility
    {
        private const string TagTexturePath = "Assets/Tags/000.png";
        private const string GraffitiPrefabPath = "Assets/Resources/Tagging/GraffitiTagInstance.prefab";

        [MenuItem("Race/Tagging/Repair Paint in 3D Setup")]
        public static void Repair()
        {
            ConfigureTagTexture();
            ConfigureGraffitiPrefab();
            ConfigureActiveSceneMeshes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Paint in 3D tagging setup repaired.");
        }

        [MenuItem("Race/Tagging/Run Paint in 3D Smoke Test")]
        public static void RunSmokeTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before running the Paint in 3D smoke test.");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("Paint in 3D smoke test failed: Camera.main is missing.");
                return;
            }

            if (!Physics.Raycast(camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning("Paint in 3D smoke test failed: no center-screen surface was hit.");
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(GraffitiTagInstance.ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("Paint in 3D smoke test failed: graffiti session prefab is missing.");
                return;
            }

            GameObject instance = Object.Instantiate(prefab);
            GraffitiTagInstance tagInstance = instance.GetComponent<GraffitiTagInstance>();
            if (tagInstance == null)
            {
                Debug.LogWarning("Paint in 3D smoke test failed: graffiti session component is missing.");
                Object.Destroy(instance);
                return;
            }

            if (instance.TryGetComponent(out NetworkObject networkObject))
            {
                networkObject.enabled = false;
            }

            Vector3 direction = (hit.point - camera.transform.position).sqrMagnitude > 0.0001f
                ? (hit.point - camera.transform.position).normalized
                : camera.transform.forward;
            Vector3 up = Vector3.ProjectOnPlane(camera.transform.up, direction);
            if (up.sqrMagnitude <= 0.0001f)
            {
                up = Vector3.ProjectOnPlane(camera.transform.right, direction);
            }

            tagInstance.ConfigureTarget(hit.collider.gameObject.scene.name, hit.point, hit.point, camera.transform.position, direction, up.normalized, 1.45f);
            tagInstance.BeginLocalReveal(0.15f);
            Object.Destroy(instance, 1.5f);
            Debug.Log($"Paint in 3D smoke test painted '{hit.collider.name}'.");
        }

        private static void ConfigureTagTexture()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TagTexturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureGraffitiPrefab()
        {
            Texture2D tagTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TagTexturePath);
            if (tagTexture == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(GraffitiPrefabPath);
            if (root == null)
            {
                return;
            }

            GraffitiTagInstance instance = root.GetComponent<GraffitiTagInstance>();
            if (instance != null)
            {
                CwPaintDecal[] decals = root.GetComponents<CwPaintDecal>();
                while (decals.Length < 2)
                {
                    root.AddComponent<CwPaintDecal>();
                    decals = root.GetComponents<CwPaintDecal>();
                }

                SerializedObject serializedObject = new(instance);
                serializedObject.FindProperty("impactCloudParticles").objectReferenceValue = root.GetComponentInChildren<ParticleSystem>(true);
                serializedObject.FindProperty("applyDecal").objectReferenceValue = decals[0];
                serializedObject.FindProperty("eraseDecal").objectReferenceValue = decals[1];
                serializedObject.FindProperty("tagTexture").objectReferenceValue = tagTexture;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, GraffitiPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void ConfigureActiveSceneMeshes()
        {
            HashSet<string> changedPaths = new();
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || renderer.GetComponentInParent<PlayerMotor>() != null)
                {
                    continue;
                }

                Mesh mesh = null;
                if (renderer.TryGetComponent(out MeshFilter meshFilter))
                {
                    mesh = meshFilter.sharedMesh;
                }
                else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    mesh = skinnedMeshRenderer.sharedMesh;
                }

                if (mesh == null || mesh.isReadable)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(assetPath) || !changedPaths.Add(assetPath))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null || importer.isReadable)
                {
                    continue;
                }

                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }
    }
}
