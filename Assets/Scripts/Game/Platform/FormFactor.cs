using UnityEngine;

namespace Casino.Platform
{
    public enum DeviceKind { Phone, Tv, Watch, Car }

    /// <summary>
    /// What kind of device the game runs on: phone or tablet, TV (Google TV / Android TV), watch (Wear OS) or car
    /// (Android Automotive OS). Read once from Android's system features; anything else is a phone.
    /// Each port keys its layout, input and limits off this one place.
    /// </summary>
    public static class FormFactor
    {
        static DeviceKind? kind;

        public static DeviceKind Kind => kind ??= Detect();
        public static bool IsPhone => Kind == DeviceKind.Phone;
        public static bool IsTv => Kind == DeviceKind.Tv;
        public static bool IsWatch => Kind == DeviceKind.Watch;
        public static bool IsCar => Kind == DeviceKind.Car;

        /// <summary>No touch screen to rely on: a remote, a game controller or the car's controls drive the UI.</summary>
        public static bool NoTouch => IsTv || (IsCar && !Input.touchSupported);

        /// <summary>For tests and the dev tour: pretend to be another device ("-formFactor=tv" on the command line).</summary>
        public static void Override(DeviceKind k) => kind = k;

        static DeviceKind Detect()
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg.StartsWith("-formFactor=") && System.Enum.TryParse(arg.Substring(12), true, out DeviceKind forced))
                    return forced;
            if (AndroidBridge.HasSystemFeature("android.hardware.type.watch")) return DeviceKind.Watch;
            if (AndroidBridge.HasSystemFeature("android.hardware.type.automotive")) return DeviceKind.Car;
            if (AndroidBridge.HasSystemFeature("android.software.leanback")
                || AndroidBridge.HasSystemFeature("android.hardware.type.television")) return DeviceKind.Tv;
            return DeviceKind.Phone;
        }
    }
}
