#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Race.EditorTools
{
    public static class WindowsBuildTool
    {
        private const string ProductNameFallback = "Race";
        private const string BuildRoot = "Builds/Windows";

        [MenuItem("Tools/Race/Build/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                Debug.LogError("No enabled scenes were found in Build Settings.");
                return;
            }

            string productName = string.IsNullOrWhiteSpace(PlayerSettings.productName)
                ? ProductNameFallback
                : SanitizeFileName(PlayerSettings.productName);

            string version = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
                ? "dev"
                : SanitizeFileName(PlayerSettings.bundleVersion);

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string buildFolder = Path.Combine(BuildRoot, $"{productName}_{version}_{timestamp}");
            string executablePath = Path.Combine(buildFolder, $"{productName}.exe");

            Directory.CreateDirectory(buildFolder);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"Windows build failed with result {summary.result}. See the Unity Console and Build Report for details.");
                return;
            }

            string fullBuildFolderPath = Path.GetFullPath(buildFolder);
            EditorUtility.RevealInFinder(fullBuildFolderPath);

            Debug.Log(
                $"Windows build completed successfully at '{fullBuildFolderPath}'. " +
                $"Zip the entire folder before sending it to friends.");
        }

        [MenuItem("Tools/Race/Build/Open Windows Build Folder")]
        public static void OpenWindowsBuildFolder()
        {
            string fullBuildRoot = Path.GetFullPath(BuildRoot);
            Directory.CreateDirectory(fullBuildRoot);
            EditorUtility.RevealInFinder(fullBuildRoot);
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        }
    }
}
#endif
