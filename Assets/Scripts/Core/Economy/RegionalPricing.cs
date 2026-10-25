using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Casino.Core.Economy
{
    public enum PricingMode
    {
        /// <summary>One SKU per product; per-country prices live in Play Console / Steamworks. Recommended.</summary>
        StoreNative,

        /// <summary>Five SKUs per product (suffix _t1.._t5); the device's verified location picks the tier.</summary>
        LocationTiers,
    }

    /// <summary>A country's money: ISO code, how many to the US dollar, decimals and the sign people there know it by.</summary>
    public sealed class CurrencyInfo
    {
        public string Code, Symbol;
        public decimal PerUsd;
        public int Decimals;

        /// <summary>Big-unit currencies are priced in round numbers (¥480, ₩6,500); the rest end in 9 (₹449, 2.69 €).</summary>
        public bool RoundPrices;

        /// <summary>Priced in whole units even under 100, though the currency has cents (₹29, 49 ₽, 29 kr).</summary>
        public bool WholePrices;
    }

    /// <summary>How the player's language writes a price: the sign before or after the number, with a space, and its separators.</summary>
    public struct PriceStyle
    {
        public bool SymbolFirst, Space;
        public string Decimal, Group;
        public int PrimaryGroup, SecondaryGroup;

        public static PriceStyle English => new PriceStyle { SymbolFirst = true, Decimal = ".", Group = ",", PrimaryGroup = 3, SecondaryGroup = 3 };
    }

    /// <summary>
    /// Purchasing-power based prices: poorer countries pay proportionally less, in their own currency, rounded to the
    /// kind of price people there are used to. Every ISO 3166-1 country (plus Kosovo) is in the table.
    /// </summary>
    public static class RegionalPricing
    {
        public const int TierCount = 5;
        static readonly float[] TierMultipliers = { 1.00f, 0.80f, 0.60f, 0.45f, 0.30f };
        static readonly float[] TierThresholds = { 0.85f, 0.70f, 0.55f, 0.38f };
        static Dictionary<string, (float multiplier, string currency)> table;
        static Dictionary<string, CurrencyInfo> currencies;

        public static string DataSource => RegionalPriceTable.DataSource;

        static Dictionary<string, (float multiplier, string currency)> Table
        {
            get
            {
                if (table != null) return table;
                var t = new Dictionary<string, (float, string)>(256, StringComparer.OrdinalIgnoreCase);
                foreach (var entry in RegionalPriceTable.Countries.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = entry.Split(';');
                    if (f.Length < 3 || f[0].Length != 2) continue;
                    t[f[0]] = (float.Parse(f[1], CultureInfo.InvariantCulture), f[2]);
                }
                return table = t;
            }
        }

        static Dictionary<string, CurrencyInfo> Currencies
        {
            get
            {
                if (currencies != null) return currencies;
                var c = new Dictionary<string, CurrencyInfo>(160, StringComparer.OrdinalIgnoreCase);
                foreach (var entry in RegionalPriceTable.Currencies.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = entry.Split(';');
                    if (f.Length < 5) continue;
                    c[f[0]] = new CurrencyInfo
                    {
                        Code = f[0],
                        PerUsd = decimal.Parse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture),
                        Decimals = int.Parse(f[2], CultureInfo.InvariantCulture),
                        Symbol = f[3],
                        RoundPrices = f[4] == "r",
                        WholePrices = f[4] == "w",
                    };
                }
                return currencies = c;
            }
        }

        static readonly CurrencyInfo Usd = new CurrencyInfo { Code = "USD", Symbol = "$", PerUsd = 1m, Decimals = 2 };

        /// <summary>Every country in the table (ISO 3166-1 alpha-2).</summary>
        public static IEnumerable<string> Countries => Table.Keys;

        public static IEnumerable<KeyValuePair<string, float>> AllCountries
        {
            get
            {
                foreach (var e in Table) yield return new KeyValuePair<string, float>(e.Key, e.Value.multiplier);
            }
        }

        public static bool IsKnownCountry(string iso2) => !string.IsNullOrEmpty(iso2) && Table.ContainsKey(iso2);

        /// <summary>Continuous multiplier for a country; unknown countries pay the base price.</summary>
        public static float Multiplier(string iso2) =>
            !string.IsNullOrEmpty(iso2) && Table.TryGetValue(iso2, out var m) ? m.multiplier : 1f;

        /// <summary>The country's currency code; US dollars where the country isn't known.</summary>
        public static string CurrencyOf(string iso2) => Currency(iso2).Code;

        public static CurrencyInfo Currency(string iso2) =>
            !string.IsNullOrEmpty(iso2) && Table.TryGetValue(iso2, out var m) && Currencies.TryGetValue(m.currency, out var c) ? c : Usd;

        public static int Tier(string iso2)
        {
            float m = Multiplier(iso2);
            for (int i = 0; i < TierThresholds.Length; i++)
                if (m >= TierThresholds[i]) return i + 1;
            return TierCount;
        }

        public static float TierMultiplier(int tier) => TierMultipliers[Math.Max(1, Math.Min(TierCount, tier)) - 1];

        public static float EffectiveMultiplier(string iso2, PricingMode mode) =>
            mode == PricingMode.LocationTiers ? TierMultiplier(Tier(iso2)) : Multiplier(iso2);

        /// <summary>Regional USD price rounded down to a .x9 price point (minimum 0.19).</summary>
        public static decimal PriceUsd(decimal baseUsd, float multiplier)
        {
            long cents = (long)decimal.Round(baseUsd * (decimal)multiplier * 100m);
            long charm = (cents + 1) / 10 * 10 - 1;
            if (charm < 19) charm = 19;
            if (charm > (long)(baseUsd * 100m)) charm = (long)(baseUsd * 100m);
            return charm / 100m;
        }

        public static decimal PriceUsd(ProductDef product, string iso2, PricingMode mode) =>
            PriceUsd(product.BaseUsd, EffectiveMultiplier(iso2, mode));

        /// <summary>The product's regional price in the country's own currency (unknown countries: the base price in US dollars).</summary>
        public static decimal LocalPrice(ProductDef product, string iso2, PricingMode mode)
        {
            var c = Currency(iso2);
            decimal regional = PricePoint(product.BaseUsd * (decimal)EffectiveMultiplier(iso2, mode) * c.PerUsd, c);
            decimal full = PricePoint(product.BaseUsd * c.PerUsd, c);
            return Math.Min(regional, full);
        }

        /// <summary>
        /// An amount as a price people in that currency are used to: under 100 (in currencies with cents) it ends in 9
        /// cents (2.69, 12.49); otherwise whole numbers, ending in 9 (129, 1,199) or round for big-unit currencies (¥480).
        /// </summary>
        public static decimal PricePoint(decimal amount, CurrencyInfo c)
        {
            if (amount <= 0m) return 0m;
            if (c.Decimals > 0 && amount < 100m && !c.WholePrices)
            {
                decimal step = amount < 10m ? 0.10m : 0.50m;
                decimal p = decimal.Round(amount / step, MidpointRounding.AwayFromZero) * step - 0.01m;
                return Math.Max(p, 0.09m);
            }
            if (amount < 10m) return Math.Max(1m, decimal.Round(amount, MidpointRounding.AwayFromZero));
            decimal unit = amount < 1000m ? (amount < 100m ? 1m : 10m) : Pow10((int)Math.Floor(Math.Log10((double)amount)) - 1);
            if (c.RoundPrices)
            {
                if (amount < 100m) unit = 5m;
                return Math.Max(unit, decimal.Round(amount / unit, MidpointRounding.AwayFromZero) * unit);
            }
            if (amount < 30m) return decimal.Round(amount, MidpointRounding.AwayFromZero);   // ₹27, 19 kr
            if (amount < 100m) unit = 5m;                                                    // 34, 39, 44, 49
            decimal nine = decimal.Round((amount + 1m) / unit, MidpointRounding.AwayFromZero) * unit - 1m;
            return Math.Max(9m, nine);
        }

        static decimal Pow10(int n)
        {
            decimal p = 1m;
            for (int i = 0; i < n; i++) p *= 10m;
            return p;
        }

        /// <summary>A price written the way the player's language writes it, with the currency's own sign.</summary>
        public static string Format(decimal amount, CurrencyInfo c, PriceStyle style)
        {
            bool cents = amount != decimal.Truncate(amount) && c.Decimals > 0;
            string digits = decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder(24);
            Group(sb, digits, style);
            if (cents)
            {
                decimal fraction = amount - decimal.Truncate(amount);
                string f = fraction.ToString("F" + c.Decimals, CultureInfo.InvariantCulture);   // "0.69"
                sb.Append(style.Decimal ?? ".").Append(f.Substring(2));
            }
            string number = sb.ToString();
            string sign = c.Symbol;
            bool code = sign.Length >= 3 && char.IsLetter(sign[0]) && char.IsLetter(sign[sign.Length - 1]) && sign.ToUpperInvariant() == sign;
            string gap = style.Space || code ? " " : "";
            return style.SymbolFirst ? sign + gap + number : number + gap + sign;
        }

        static void Group(StringBuilder sb, string digits, PriceStyle style)
        {
            int primary = style.PrimaryGroup > 0 ? style.PrimaryGroup : 3, secondary = style.SecondaryGroup > 0 ? style.SecondaryGroup : 3;
            string sep = style.Group ?? ",";
            if (digits.Length <= primary || (digits.Length == 4 && sep == " "))
            {
                sb.Append(digits);   // 4-digit numbers aren't grouped with a space (1990 Kč, not 1 990 Kč)
                return;
            }
            int first = digits.Length - primary;
            int lead = first % secondary;
            if (lead == 0) lead = secondary;
            sb.Append(digits, 0, lead);
            for (int i = lead; i < first; i += secondary) sb.Append(sep).Append(digits, i, secondary);
            sb.Append(sep).Append(digits, first, primary);
        }

        public static string StoreSku(ProductDef product, string iso2, PricingMode mode) =>
            mode == PricingMode.LocationTiers ? product.Id + "_t" + Tier(iso2) : product.Id;

        /// <summary>Maps any SKU (tiered or not) back to its catalog product id.</summary>
        public static string ProductIdFromSku(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return sku;
            int n = sku.Length;
            if (n > 3 && sku[n - 3] == '_' && sku[n - 2] == 't' && sku[n - 1] >= '1' && sku[n - 1] <= '9')
                return sku.Substring(0, n - 3);
            return sku;
        }

        public static string FormatUsd(decimal amount) => "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
