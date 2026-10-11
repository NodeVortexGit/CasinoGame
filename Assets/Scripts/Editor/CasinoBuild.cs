using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Casino.EditorTools
{
    /// <summary>
    /// One-click builds, also usable headless:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Casino.EditorTools.CasinoBuild.Linux
    /// </summary>
    public static class CasinoBuild
    {
        const string Root = "Builds";

        /// <summary>Sideload test build: test store and test ads switched on (CASINO_DEMO), no real money.</summary>
        [MenuItem("Casino/Build/Android APK (test, sideload)", false, 1)]
        public static void AndroidApk() => Android(false, true);

        [MenuItem("Casino/Build/Android App Bundle (Google Play)", false, 2)]
        public static void AndroidBundle() => Android(true, false);

        /// <summary>
        /// Exports the Android project for Gradle (Builds/Android/Gradle). Tools/android_build.sh then builds it with Gradle:
        /// APKs to sideload and the app bundle, checks the bundle with bundletool and installs on a connected phone with
        /// adb. Test build (test store and test ads) unless -release is passed.
        /// </summary>
        [MenuItem("Casino/Build/Android Gradle project", false, 3)]
        public static void AndroidGradleProject()
        {
            bool release = Array.IndexOf(Environment.GetCommandLineArgs(), "-release") >= 0;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
            PlayerSettings.Android.buildApkPerCpuArchitecture = true;
            SyncAdMobAppId();
            try
            {
                Build(BuildTarget.Android, Path.Combine(Root, "Android", "Gradle"), !release);
            }
            finally
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            }
        }

        [MenuItem("Casino/Build/Linux", false, 20)]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, Path.Combine(Root, "Linux", "LuckyAceCasino.x86_64"));

        [MenuItem("Casino/Build/Windows (Steam)", false, 21)]
        public static void Windows()
        {
            string exe = Path.Combine(Root, "Windows", "LuckyAceCasino.exe");
            if (Build(BuildTarget.StandaloneWindows64, exe)) WriteSteamAppId(Path.GetDirectoryName(exe));
        }

        static void Android(bool bundle, bool demo)
        {
            EditorUserBuildSettings.buildAppBundle = bundle;
            // Sideload builds: one APK per CPU type (~23 MB each instead of ~39 MB). Google Play app bundles split
            // per device automatically, so this only matters for APKs.
            PlayerSettings.Android.buildApkPerCpuArchitecture = !bundle;
            SyncAdMobAppId();
            Build(BuildTarget.Android, Path.Combine(Root, "Android", bundle ? "LuckyAceCasino.aab" : "LuckyAceCasino.apk"), demo);
        }

        static bool Build(BuildTarget target, string path, bool demo = false)
        {
            CasinoProjectSetup.EnsureReadyForBuild();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var args = Environment.GetCommandLineArgs();
            bool development = Array.IndexOf(args, "-development") >= 0;
            demo |= Array.IndexOf(args, "-demo") >= 0;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { CasinoProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = demo ? new[] { "CASINO_DEMO" } : null,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;
            Debug.Log($"Casino build {target}: {summary.result}, {summary.totalSize / (1024 * 1024f):0.0} MB, {summary.totalErrors} errors -> {path}");
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
            return ok;
        }

        /// <summary>
        /// The Google Mobile Ads plugin writes the AdMob app id into the Android manifest from its own settings asset (and
        /// the app crashes at launch without one). Keep that asset in step with GameConfig.AdMobAndroidAppId.
        /// </summary>
        static void SyncAdMobAppId()
        {
            var type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            if (type == null) return; // plugin not installed
            var load = type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var prop = type.GetProperty("GoogleMobileAdsAndroidAppId");
            if (!(load?.Invoke(null, null) is ScriptableObject settings) || prop == null) return;
            string wanted = GameConfigBridge.AdMobAndroidAppId;
            if (string.IsNullOrEmpty(wanted) || (string)prop.GetValue(settings) == wanted) return;
            prop.SetValue(settings, wanted);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"Casino: AdMob Android app id set to {wanted}.");
        }

        /// <summary>steam_appid.txt lets a development build talk to Steam when launched outside the Steam client.</summary>
        static void WriteSteamAppId(string folder)
        {
            File.WriteAllText(Path.Combine(folder, "steam_appid.txt"), GameConfigBridge.SteamAppId.ToString());
        }
    }

    /// <summary>Editor scripts can't reference Assembly-CSharp, so GameConfig values are read here via reflection.</summary>
    static class GameConfigBridge
    {
        public static string AdMobAndroidAppId =>
            Type.GetType("Casino.GameConfig, Assembly-CSharp")?.GetField("AdMobAndroidAppId")?.GetValue(null) as string;

        public static uint SteamAppId
        {
            get
            {
                var type = Type.GetType("Casino.GameConfig, Assembly-CSharp");
                var field = type?.GetField("SteamAppId");
                return field != null ? (uint)field.GetValue(null) : 480u;
            }
        }
    }
}
