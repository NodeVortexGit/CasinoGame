using System;
using System.Collections;
using System.Globalization;
using Casino.Core.Economy;
using Casino.Core.Region;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Casino.Platform
{
    /// <summary>
    /// Works out which country's prices apply. Physical signals first: a GPS fix resolved to a country fully offline
    /// against bundled borders, then the cellular network's country. IP geolocation is only used on hardware with no
    /// location sensor and no cellular radio (desktops), so a VPN does not change prices on phones.
    /// </summary>
    public sealed class RegionService
    {
        CountryLookup lookup;

        public string Country { get; private set; }
        public RegionSource Source { get; private set; } = RegionSource.None;
        public bool Resolving { get; private set; }

        /// <summary>The device has a location sensor but the player declined or disabled it.</summary>
        public bool LocationDeclined { get; private set; }

        public event Action Changed;

        /// <summary>
        /// Shown before the system location prompt (Google Play's "prominent disclosure"); invoke the callback when the
        /// player has read it. Left null, the system prompt appears directly.
        /// </summary>
        public Action<Action> ExplainLocation;

        /// <summary>Country used for pricing; null means base price.</summary>
        public string PricingCountry => Source == RegionSource.Default ? null : Country;

        public void RestoreCached(string country, RegionSource source)
        {
            if (string.IsNullOrEmpty(country) && source != RegionSource.Default) return;
            Country = string.IsNullOrEmpty(country) ? null : country;
            Source = source;
        }

        public void SetDevOverride(string country)
        {
            Country = country;
            Source = RegionSource.DevOverride;
            Changed?.Invoke();
        }

        public IEnumerator Resolve()
        {
            if (Resolving) yield break;
            Resolving = true;
            LocationDeclined = false;

            var caps = new DeviceCapabilities
            {
                HasLocationHardware = AndroidBridge.HasLocationHardware,
                HasTelephony = AndroidBridge.HasTelephony,
                SteamRunning = SteamBootstrap.Running,
            };
            string found = null;
            RegionSource foundBy = RegionSource.Default;
            foreach (var source in RegionPolicy.Plan(caps))
            {
                string result = null;
                switch (source)
                {
                    case RegionSource.Gps:
                    {
                        var box = new string[1];
                        yield return GpsCountry(box);
                        result = box[0];
                        break;
                    }
                    case RegionSource.CellNetwork:
                        result = AndroidBridge.NetworkCountryIso();
                        break;
                    case RegionSource.Steam:
                        result = SteamBootstrap.IpCountry();
                        break;
                    case RegionSource.IpLookup:
                    {
                        var box = new string[1];
                        yield return IpCountry(box);
                        result = box[0];
                        break;
                    }
                    case RegionSource.DeviceLocale:
                        result = LocaleCountry();
                        break;
                }
                result = Normalize(result);
                if (result != null)
                {
                    found = result;
                    foundBy = source;
                    break;
                }
            }

            Country = found;
            Source = found != null ? foundBy : RegionSource.Default;
            Resolving = false;
            Debug.Log($"Region: {Country ?? "-"} via {Source}");
            Changed?.Invoke();
        }

        static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            code = code.Trim().ToUpperInvariant();
            if (code.Length != 2 || code == "XX" || code == "T1") return null; // T1 = Tor exit in Cloudflare's trace
            return code;
        }

        IEnumerator GpsCountry(string[] result)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
            {
                if (ExplainLocation != null)
                {
                    bool read = false;
                    ExplainLocation(() => read = true);
                    while (!read) yield return null;
                }
                bool? granted = null;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => granted = true;
                callbacks.PermissionDenied += _ => granted = false;
                Permission.RequestUserPermission(Permission.CoarseLocation, callbacks);
                float waited = 0f;
                while (granted == null && waited < 30f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (granted != true)
                {
                    LocationDeclined = true;
                    yield break;
                }
            }
            if (!Input.location.isEnabledByUser)
            {
                LocationDeclined = true;
                yield break;
            }
            Input.location.Start(5000f, 5000f);
            float t = 0f;
            while (Input.location.status == LocationServiceStatus.Initializing && t < 20f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Input.location.status == LocationServiceStatus.Running)
            {
                var fix = Input.location.lastData;
                result[0] = Lookup.Find(fix.latitude, fix.longitude);
            }
            Input.location.Stop();
#else
            yield break;
#endif
        }

        CountryLookup Lookup
        {
            get
            {
                if (lookup == null)
                {
                    var asset = Resources.Load<TextAsset>("Geo/countries");
                    lookup = CountryLookup.Load(asset.bytes);
                    Resources.UnloadAsset(asset);
                }
                return lookup;
            }
        }

        static IEnumerator IpCountry(string[] result)
        {
            using (var req = UnityWebRequest.Get(GameConfig.IpCountryUrl))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                foreach (var line in req.downloadHandler.text.Split('\n'))
                    if (line.StartsWith("loc=", StringComparison.Ordinal)) result[0] = line.Substring(4);
            }
        }

        static string LocaleCountry()
        {
            string android = AndroidBridge.LocaleCountry();
            if (!string.IsNullOrEmpty(android)) return android;
            try
            {
                string name = CultureInfo.CurrentCulture.Name;
                if (!string.IsNullOrEmpty(name) && name.Contains("-")) return new RegionInfo(name).TwoLetterISORegionName;
                string lang = Environment.GetEnvironmentVariable("LANG"); // e.g. bg_BG.UTF-8 on Linux
                if (!string.IsNullOrEmpty(lang) && lang.Length >= 5 && lang[2] == '_') return lang.Substring(3, 2);
            }
            catch (Exception)
            {
                // ignore: falls through to base price
            }
            return null;
        }

        /// <summary>The pricing country, where it came from and its currency, in the player's language.</summary>
        public string Describe()
        {
            if (Resolving) return Loc.T("Checking your region...");
            if (PricingCountry == null) return Loc.F("Not verified ({0}): standard prices", RegionPolicy.Describe(Source));
            return Loc.F("{0} ({1}): prices in {2}", Loc.Country(PricingCountry), RegionPolicy.Describe(Source),
                RegionalPricing.CurrencyOf(PricingCountry));
        }
    }
}
