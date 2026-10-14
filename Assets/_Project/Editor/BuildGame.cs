using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PG.EditorTools
{
    public static class BuildGame
    {
        [MenuItem("PixelGenesis/Build Windows Development")]
        public static void Windows()
        {
            int code = 1;
            try
            {
                DataSync.Sync();
                Directory.CreateDirectory("Builds/Windows");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/GameBootstrap.unity" },
                    locationPathName = "Builds/Windows/PixelGenesis.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
                Debug.Log($"[Build] {report.summary.result}; errors {report.summary.totalErrors}; bytes {report.summary.totalSize}");
            }
            catch (Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("PixelGenesis/Build Android APK")]
        public static void Android()
        {
            int code = 1;
            try
            {
                DataSync.Sync();
                ConfigureAndroid();
                Directory.CreateDirectory("Builds/Android");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/GameBootstrap.unity" },
                    locationPathName = "Builds/Android/PixelGenesis.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                });
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
                Debug.Log($"[Build] Android {report.summary.result}; errors {report.summary.totalErrors}; bytes {report.summary.totalSize}");
            }
            catch (Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("PixelGenesis/Build Android Release AAB")]
        public static void AndroidRelease()
        {
            int code = 1;
            bool previousBundleMode = EditorUserBuildSettings.buildAppBundle;
            try
            {
                string appId = Environment.GetEnvironmentVariable("PG_ANDROID_APP_ID");
                string keystore = Environment.GetEnvironmentVariable("PG_ANDROID_KEYSTORE");
                string keystorePass = Environment.GetEnvironmentVariable("PG_ANDROID_KEYSTORE_PASS");
                string alias = Environment.GetEnvironmentVariable("PG_ANDROID_KEYALIAS");
                string aliasPass = Environment.GetEnvironmentVariable("PG_ANDROID_KEYALIAS_PASS");
                if (string.IsNullOrWhiteSpace(appId) || appId.StartsWith("com.DefaultCompany.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Set PG_ANDROID_APP_ID to the final reverse-domain package ID before a release build.");
                if (string.IsNullOrWhiteSpace(keystore) || !File.Exists(keystore) ||
                    string.IsNullOrWhiteSpace(keystorePass) || string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(aliasPass))
                    throw new InvalidOperationException("Set PG_ANDROID_KEYSTORE, PG_ANDROID_KEYSTORE_PASS, PG_ANDROID_KEYALIAS and PG_ANDROID_KEYALIAS_PASS in the build environment.");
                if (PlayerSettings.Android.bundleVersionCode < 1 || string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
                    throw new InvalidOperationException("Set a release version and positive Android version code in Player Settings.");

                DataSync.Sync();
                ConfigureAndroid(appId);
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = aliasPass;
                EditorUserBuildSettings.buildAppBundle = true;
                Directory.CreateDirectory("Builds/Android");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/GameBootstrap.unity" },
                    locationPathName = "Builds/Android/PixelGenesis.aab",
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
                Debug.Log($"[Build] Android release {report.summary.result}; errors {report.summary.totalErrors}; bytes {report.summary.totalSize}");
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                // Keep signing secrets out of project settings and subsequent editor builds.
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                EditorUserBuildSettings.buildAppBundle = previousBundleMode;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("PixelGenesis/Build WebGL")]
        public static void Web()
        {
            int code = 1;
            try
            {
                DataSync.Sync();
                ConfigureWeb();
                string outputPath = ResolveWebBuildPath();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/GameBootstrap.unity" },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    // Release by default (a development wasm is ~140 MB); PG_WEB_DEVELOPMENT=1 opts back in.
                    options = Environment.GetEnvironmentVariable("PG_WEB_DEVELOPMENT") == "1" ? BuildOptions.Development : BuildOptions.None,
                });
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
                Debug.Log($"[Build] WebGL {report.summary.result}; errors {report.summary.totalErrors}; bytes {report.summary.totalSize}");
            }
            catch (Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string ResolveWebBuildPath()
        {
            string root = Path.GetFullPath(Directory.GetCurrentDirectory());
            string requested = Environment.GetEnvironmentVariable("PG_WEB_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(requested)) requested = Path.Combine("Builds", "Web", "PixelGenesis");
            string path = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(root, requested));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing to write WebGL output outside the project: {path}");
            return path;
        }

        // Deterministic player settings so batch builds need no editor GUI state.
        static void ConfigureWeb()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "com.DefaultCompany.PixelGenesis");
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // raw file names match oyun.html; the host compresses
            PlayerSettings.WebGL.decompressionFallback = false;
            // Governor remains opt-in; the browser client uses the user-configured endpoint and requires provider CORS.
        }

        // Deterministic player settings so batch builds need no editor GUI state.
        static void ConfigureAndroid()
        {
            ConfigureAndroid(Environment.GetEnvironmentVariable("PG_ANDROID_APP_ID") ?? "com.DefaultCompany.PixelGenesis");
        }

        static void ConfigureAndroid(string appId)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, appId);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = true; // Governor LLM client
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        }
    }
}
