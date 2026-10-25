using System.Collections.Generic;

namespace Casino.Core.Region
{
    public enum RegionSource
    {
        None,
        /// <summary>Device location fix, resolved to a country fully offline. Ignores VPNs.</summary>
        Gps,
        /// <summary>Country of the mobile network the radio is registered on. Physical, permission-free, ignores VPNs.</summary>
        CellNetwork,
        /// <summary>Steam's own country for the account's connection.</summary>
        Steam,
        /// <summary>IP geolocation. Only used on hardware with no location sensor and no cellular radio.</summary>
        IpLookup,
        DeviceLocale,
        /// <summary>Nothing trustworthy was available: charge the base (full) price.</summary>
        Default,
        DevOverride,
    }

    public struct DeviceCapabilities
    {
        public bool HasLocationHardware;
        public bool HasTelephony;
        public bool SteamRunning;
    }

    public static class RegionPolicy
    {
        /// <summary>
        /// Order in which country sources are tried. IP lookup is deliberately excluded whenever the device has location
        /// hardware: a player who has GPS but refuses it pays the base price rather than whatever a VPN says.
        /// </summary>
        public static List<RegionSource> Plan(DeviceCapabilities caps)
        {
            var plan = new List<RegionSource>(4);
            if (caps.HasLocationHardware)
            {
                plan.Add(RegionSource.Gps);
                if (caps.HasTelephony) plan.Add(RegionSource.CellNetwork);
                return plan;
            }
            if (caps.HasTelephony) plan.Add(RegionSource.CellNetwork);
            if (caps.SteamRunning) plan.Add(RegionSource.Steam);
            plan.Add(RegionSource.IpLookup);
            plan.Add(RegionSource.DeviceLocale);
            return plan;
        }

        /// <summary>Sources that a VPN cannot influence.</summary>
        public static bool IsPhysical(RegionSource source) => source == RegionSource.Gps || source == RegionSource.CellNetwork;

        /// <summary>Where the country came from, in the player's language.</summary>
        public static string Describe(RegionSource source)
        {
            switch (source)
            {
                case RegionSource.Gps: return Tr.T("GPS location");
                case RegionSource.CellNetwork: return Tr.T("mobile network");
                case RegionSource.Steam: return "Steam";
                case RegionSource.IpLookup: return Tr.T("internet connection");
                case RegionSource.DeviceLocale: return Tr.T("device settings");
                case RegionSource.DevOverride: return Tr.T("test country");
                case RegionSource.Default: return Tr.T("not verified (standard price)");
                default: return Tr.T("unknown");
            }
        }
    }
}
