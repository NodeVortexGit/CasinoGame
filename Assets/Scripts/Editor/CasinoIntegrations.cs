using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Casino.EditorTools
{
    /// <summary>
    /// Turns the optional store integrations on and off via scripting define symbols. Install the matching package
    /// first (see README), then tick it here. Unticked, the game uses the simulated store in development builds.
    /// </summary>
    public static class CasinoIntegrations
    {
        const string IapMenu = "Casino/Integrations/Unity IAP (Google Play)";
        const string SteamMenu = "Casino/Integrations/Steamworks (Windows + Linux)";

        [MenuItem(IapMenu)] static void ToggleIap() => Toggle(NamedBuildTarget.Android, "CASINO_UNITY_IAP");
        [MenuItem(IapMenu, true)] static bool CheckIap() => Check(IapMenu, NamedBuildTarget.Android, "CASINO_UNITY_IAP");

        [MenuItem(SteamMenu)] static void ToggleSteam() => Toggle(NamedBuildTarget.Standalone, "CASINO_STEAMWORKS");
        [MenuItem(SteamMenu, true)] static bool CheckSteam() => Check(SteamMenu, NamedBuildTarget.Standalone, "CASINO_STEAMWORKS");

        static bool Check(string menu, NamedBuildTarget target, string symbol)
        {
            Menu.SetChecked(menu, Has(target, symbol));
            return true;
        }

        static bool Has(NamedBuildTarget target, string symbol) =>
            new List<string>(PlayerSettings.GetScriptingDefineSymbols(target).Split(';')).Contains(symbol);

        static void Toggle(NamedBuildTarget target, string symbol)
        {
            var list = new List<string>(PlayerSettings.GetScriptingDefineSymbols(target).Split(';'));
            list.RemoveAll(string.IsNullOrWhiteSpace);
            bool enable = !list.Contains(symbol);
            if (enable) list.Add(symbol);
            else list.Remove(symbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            Debug.Log($"Casino: {symbol} {(enable ? "enabled" : "disabled")} for {target.TargetName}.");
        }
    }
}
