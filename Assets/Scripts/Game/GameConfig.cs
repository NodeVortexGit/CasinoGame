using Casino.Core.Economy;

namespace Casino
{
    /// <summary>All tunable numbers and integration ids in one place.</summary>
    public static class GameConfig
    {
        public const string CasinoName = "LUCKY ACE";

        /// <summary>
        /// Demo / sideload builds (scripting define CASINO_DEMO, set by Casino > Build > Android APK (test)): the test store
        /// and test ads are active so the whole loop can be tried without real money. Never ship a store build with it.
        /// </summary>
#if CASINO_DEMO
        public const bool DemoMode = true;
#else
        public const bool DemoMode = false;
#endif

        /// <summary>Editor, development and demo builds use the simulated store and ads and show developer settings.</summary>
        public static bool TestServices => UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild || DemoMode;
        public const string CasinoSubtitle = "CASINO";

        // Economy
        public const long StartingChips = 1_000;
        public const long AdRewardChips = 250;
        public const int AdsPerDay = 12;
        public const int AdCooldownSeconds = 90;

        // Energy: drains while the player is in the casino, recovers slowly while the app is closed.
        public const double EnergyMinutesToEmpty = 45;
        public const double EnergyHoursToFullWhileAway = 8;

        // Beginner's Luck: full boost for the first hands, then fades to fair blackjack. Always visible on screen.
        public const int BeginnerLuckFullHands = 3;
        public const int BeginnerLuckFadeHands = 12;

        // Age gate
        public const int MinimumAge = Casino.Core.Player.AgeGate.MinimumAge;

        // Pricing. StoreNative = one SKU per product, per-country prices set in Play Console / Steamworks (recommended).
        // LocationTiers = five SKUs per product (id_t1..id_t5), tier picked from the verified device location.
        public static readonly PricingMode Pricing = PricingMode.StoreNative;

        // How long a resolved country is trusted before re-checking.
        public const double CountryRecheckHours = 24;

        // IP fallback (only used on hardware with no GPS and no cellular radio). Returns "loc=XX".
        public const string IpCountryUrl = "https://www.cloudflare.com/cdn-cgi/trace";

        // Ads (AdMob). Replace the test ids with your own before release.
        public const string AdMobAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string AdMobRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";

        // Steam
        public const uint SteamAppId = 480; // Spacewar test app - replace with your own app id.

        /// <summary>
        /// Steam Inventory item definition ids for each product (create matching itemdefs with price categories in
        /// Steamworks; Steam handles regional pricing for them).
        /// </summary>
        public static int SteamItemDef(string productId)
        {
            switch (productId)
            {
                case "chips_5k": return 1001;
                case "chips_30k": return 1002;
                case "chips_75k": return 1003;
                case "chips_200k": return 1004;
                case "chips_600k": return 1005;
                case "chips_1500k": return 1006;
                case "food_water": return 2001;
                case "food_espresso": return 2002;
                case "food_energy": return 2003;
                case "food_sandwich": return 2004;
                case "food_burger": return 2005;
                case "food_steak": return 2006;
                default: return 0;
            }
        }

        public static string ProductForSteamItemDef(int itemDef)
        {
            foreach (var p in ProductCatalog.All)
                if (SteamItemDef(p.Id) == itemDef) return p.Id;
            return null;
        }
    }
}
