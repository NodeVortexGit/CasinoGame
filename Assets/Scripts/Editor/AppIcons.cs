using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Casino.EditorTools
{
    /// <summary>
    /// Puts the app icon (drawn by Tools/make_icon.py into Assets/Art/Icon) on every build: Android's adaptive icon as its
    /// two layers, the round and legacy launcher icons, and the default icon for the other platforms.
    /// </summary>
    static class AppIcons
    {
        const string Dir = "Assets/Art/Icon/";

        public static void Apply()
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIconBackground.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIconForeground.png");
            if (full == null || background == null || foreground == null)
            {
                Debug.LogWarning("App icon images are missing: run Tools/make_icon.py");
                return;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
            Set(AndroidPlatformIconKind.Adaptive, background, foreground);
            Set(AndroidPlatformIconKind.Round, full);
            Set(AndroidPlatformIconKind.Legacy, full);
        }

        static void Set(PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
    }
}
