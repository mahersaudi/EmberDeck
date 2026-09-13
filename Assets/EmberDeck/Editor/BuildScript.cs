using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    public static class BuildScript
    {
        [MenuItem("EmberDeck/Build macOS Player")]
        public static void BuildMac()
        {
            var scenes = EditorBuildSettings.scenes
                                            .Where(scene => scene.enabled)
                                            .Select(scene => scene.path)
                                            .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[EmberDeck] No enabled scenes. Run EmberDeck > Generate Content and Scene first.");
                EditorApplication.Exit(1);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Build/macOS/EmberDeck.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"[EmberDeck] Build {summary.result}: {summary.totalSize} bytes, {summary.totalTime}");
            if (summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
