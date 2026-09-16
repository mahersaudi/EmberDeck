using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Builds the players: macOS and Windows, each as a development or a release build.
    ///
    ///   unity run . -- -executeMethod EmberDeck.EditorTools.BuildScript.BuildWindowsRelease
    ///
    /// Development builds go to Build/&lt;platform&gt;/ and carry the capture harness's debug hooks.
    /// Release builds go to Build/Release/&lt;platform&gt;/, which is exactly what the SteamPipe depot
    /// scripts in steam/ upload — so what was tested is what ships.
    ///
    /// Only the generated scene is built. The build list also held SampleScene, left over from the
    /// Unity template, and it would have shipped inside the game.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>The one version number: player settings, the macOS bundle, and the Steam build description.</summary>
        public const string Version = "0.1.0";

        const string Scene = "Assets/EmberDeck/Scenes/Combat.unity";

        enum Platform { MacOS, Windows, Android }

        // BuildMac keeps its name: the capture and test commands already call it.
        [MenuItem("EmberDeck/Build/macOS (Development)")]
        public static void BuildMac() => Build(Platform.MacOS, development: true);

        [MenuItem("EmberDeck/Build/macOS (Release)")]
        public static void BuildMacRelease() => Build(Platform.MacOS, development: false);

        [MenuItem("EmberDeck/Build/Windows (Development)")]
        public static void BuildWindows() => Build(Platform.Windows, development: true);

        [MenuItem("EmberDeck/Build/Windows (Release)")]
        public static void BuildWindowsRelease() => Build(Platform.Windows, development: false);

        /// <summary>
        /// A release build with the capture harness compiled in (EMBERDECK_CAPTURE), for store
        /// screenshots. A development build stamps "Development Build" across the corner of every
        /// frame, and a release build has no harness to drive — this is the only way to photograph
        /// the game as players will see it.
        /// </summary>
        [MenuItem("EmberDeck/Build/Screenshots (release with the harness)")]
        public static void BuildShots() => Build(Platform.MacOS, development: false, harness: true);

        [MenuItem("EmberDeck/Build/Android (Development)")]
        public static void BuildAndroid() => Build(Platform.Android, development: true);

        [MenuItem("EmberDeck/Build/Android (Release)")]
        public static void BuildAndroidRelease() => Build(Platform.Android, development: false);

        [MenuItem("EmberDeck/Build/All Release Builds")]
        public static void BuildAllRelease()
        {
            if (Build(Platform.MacOS, development: false))
                Build(Platform.Windows, development: false);
        }

        static bool Build(Platform platform, bool development, bool harness = false)
        {
            var target = platform switch
            {
                Platform.Windows => BuildTarget.StandaloneWindows64,
                Platform.Android => BuildTarget.Android,
                _                => BuildTarget.StandaloneOSX,
            };
            var group = platform == Platform.Android ? BuildTargetGroup.Android : BuildTargetGroup.Standalone;

            if (!File.Exists(Scene))
                return Fail($"{Scene} does not exist. Run EmberDeck > Generate Content and Scene first.");

            // A missing platform module does not fail loudly on its own: the build is refused with a
            // generic message. Say which module is missing instead.
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                return Fail(platform switch
                {
                    Platform.Windows => "Windows Build Support (Mono) is not installed for this editor. Install the module, then build again.",
                    Platform.Android => "Android Build Support (with its SDK and NDK) is not installed for this editor.",
                    _                => "Mac Build Support is not installed for this editor.",
                });

            // Mono for both desktop players. IL2CPP for Windows can only be built on Windows, and one backend
            // for both platforms means one set of behaviour to test. Android uses IL2CPP (ApplyAndroid).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.bundleVersion = Version;
            // Applied on every build, so no build can ship with Unity's default icon.
            ProjectSetup.ApplyIcon();
            ProjectSetup.ApplyInput();
            ProjectSetup.ApplyFonts();
            PlayerSettings.macOS.buildNumber = Version;
            if (platform == Platform.Android) ProjectSetup.ApplyAndroid();

            string folder = platform switch
            {
                Platform.Windows => "Windows",
                Platform.Android => "Android",
                _                => "macOS",
            };
            string file = platform switch
            {
                Platform.Windows => "EmberDeck.exe",
                Platform.Android => "EmberDeck.apk",
                _                => "EmberDeck.app",
            };
            string path = harness      ? $"Build/Shots/{folder}/{file}"
                        : development  ? $"Build/{folder}/{file}"
                                       : $"Build/Release/{folder}/{file}";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None,
                // The harness is compiled out of a release build by its #if. This define puts it back
                // without turning on the development flag, whose watermark is the thing being avoided.
                extraScriptingDefines = harness ? new[] { "EMBERDECK_CAPTURE" } : null,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            string kind = harness ? "release+harness" : development ? "development" : "release";

            Debug.Log($"[EmberDeck] Build {summary.result}: {platform} {kind} {Version} -> {path}, " +
                      $"{summary.totalSize} bytes, {summary.totalTime}");

            if (summary.result == BuildResult.Succeeded) return true;
            return Fail($"{platform} {kind} build failed: {summary.totalErrors} errors.");
        }

        /// <summary>
        /// Logs the failure, and exits with an error code only in batch mode. Exiting unconditionally —
        /// as this script used to — would close the editor on anyone who built from the menu.
        /// </summary>
        static bool Fail(string message)
        {
            Debug.LogError($"[EmberDeck] {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }
    }
}
