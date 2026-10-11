using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Casino.EditorTools
{
    /// <summary>
    /// The whole game is built from code at runtime, so the project only needs one empty scene. This creates it,
    /// registers it for builds and applies sensible player settings the first time the project is opened.
    /// </summary>
    [InitializeOnLoad]
    public static class CasinoProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string AppliedMarker = "ProjectSettings/CasinoSetupApplied.txt";

        static CasinoProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("casino.setup.checked", false)) return;
                SessionState.SetBool("casino.setup.checked", true);
                EnsureScene();
                if (!File.Exists(AppliedMarker))
                {
                    ApplyPlayerSettings();
                    File.WriteAllText(AppliedMarker, "Player settings applied by Casino > Setup. Delete this file to re-apply on next open.\n");
                }
            };
        }

        [MenuItem("Casino/Setup/Create Main Scene", false, 100)]
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
                Debug.Log("Casino: created " + ScenePath);
            }
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Called by builds, which may run headless where delayCall never fires.</summary>
        public static void EnsureReadyForBuild()
        {
            EnsureScene();
            StaffImporter.EnsureController();
            if (File.Exists(AppliedMarker)) return;
            ApplyPlayerSettings();
            File.WriteAllText(AppliedMarker, "Player settings applied by Casino > Setup. Delete this file to re-apply on next open.\n");
        }

        [MenuItem("Casino/Setup/Apply Player Settings", false, 101)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Lucky Ace Games";
            PlayerSettings.productName = "Lucky Ace Casino";

            // Landscape only, both directions.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Potato-friendly rendering: gamma space is cheaper and the UI looks the same.
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            // Desktop window.
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

            // Android: IL2CPP + ARM64 are required by Google Play; ARMv7 keeps old 32-bit phones working.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.luckyace.casino");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            EnsureLegacyInput();
            Debug.Log("Casino: player settings applied (Casino > Setup > Apply Player Settings).");
        }

        /// <summary>
        /// The UI uses the built-in input manager. If the project was set to "Input System Package (New)" only,
        /// switch it to "Both" so legacy input keeps working (takes effect after an editor restart).
        /// </summary>
        static void EnsureLegacyInput()
        {
            var settings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue != 1) return;
            prop.intValue = 2;
            so.ApplyModifiedProperties();
            Debug.LogWarning("Casino: Active Input Handling set to 'Both'. Restart the editor for it to take effect.");
        }
    }
}
