using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Casino.Core.Economy;
using UnityEditor;
using UnityEngine;

namespace Casino.EditorTools
{
    /// <summary>
    /// Writes the regional price sheet you copy into Play Console / Steamworks: one row per country with the
    /// multiplier, tier and the USD-equivalent price of every product.
    /// </summary>
    public static class PriceExport
    {
        [MenuItem("Casino/Pricing/Export Regional Price Sheet (CSV)", false, 200)]
        public static void Export()
        {
            string path = EditorUtility.SaveFilePanel("Regional price sheet", "Builds", "regional_prices.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BuildCsv());
            Debug.Log("Casino: price sheet written to " + path + " (" + RegionalPricing.DataSource + ")");
            EditorUtility.RevealInFinder(path);
        }

        public static string BuildCsv()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var products = ProductCatalog.All;
            sb.Append("country,multiplier,tier,tier_multiplier");
            foreach (var p in products) sb.Append(',').Append(p.Id).Append("_usd");
            sb.AppendLine();
            foreach (var entry in RegionalPricing.AllCountries.OrderBy(e => e.Key))
            {
                int tier = RegionalPricing.Tier(entry.Key);
                sb.Append(entry.Key).Append(',').Append(entry.Value.ToString("0.00", inv)).Append(',').Append(tier).Append(',')
                    .Append(RegionalPricing.TierMultiplier(tier).ToString("0.00", inv));
                foreach (var p in products)
                    sb.Append(',').Append(RegionalPricing.PriceUsd(p, entry.Key, PricingMode.StoreNative).ToString("0.00", inv));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
