using UnityEngine;

namespace Casino.Platform
{
    /// <summary>Small JNI helpers. Every method is safe to call on any platform (returns a neutral value off Android).</summary>
    public static class AndroidBridge
    {
        public static bool HasSystemFeature(string feature)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                    return pm.Call<bool>("hasSystemFeature", feature);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("hasSystemFeature failed: " + e.Message);
            }
#endif
            return false;
        }

        /// <summary>Location sensor present (GPS or network-based location).</summary>
        public static bool HasLocationHardware =>
            HasSystemFeature("android.hardware.location.gps") || HasSystemFeature("android.hardware.location.network");

        public static bool HasTelephony => HasSystemFeature("android.hardware.telephony");

        /// <summary>ISO country of the mobile network the radio is currently registered on ("" if none). No permission needed.</summary>
        public static string NetworkCountryIso()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var tm = activity.Call<AndroidJavaObject>("getSystemService", "phone"))
                    return tm != null ? (tm.Call<string>("getNetworkCountryIso") ?? "") : "";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("getNetworkCountryIso failed: " + e.Message);
            }
#endif
            return "";
        }

        /// <summary>The phone's language as a BCP 47 tag, e.g. "bg-BG", "pt-BR", "zh-Hant-TW" ("" off Android).</summary>
        public static string LocaleTag()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var current = locale.CallStatic<AndroidJavaObject>("getDefault"))
                    return current.Call<string>("toLanguageTag") ?? "";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Locale tag lookup failed: " + e.Message);
            }
#endif
            return "";
        }

        public static string LocaleCountry()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var current = locale.CallStatic<AndroidJavaObject>("getDefault"))
                    return current.Call<string>("getCountry") ?? "";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Locale lookup failed: " + e.Message);
            }
#endif
            return "";
        }
    }
}
