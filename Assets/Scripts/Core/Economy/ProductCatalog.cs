using System.Collections.Generic;

namespace Casino.Core.Economy
{
    public enum ProductKind { Chips, Food }

    public sealed class ProductDef
    {
        public ProductDef(string id, ProductKind kind, string title, long chips, float energy, decimal baseUsd, string highlight)
        {
            Id = id;
            Kind = kind;
            Title = title;
            Chips = chips;
            Energy = energy;
            BaseUsd = baseUsd;
            Highlight = highlight;
        }

        /// <summary>Store product id (Google Play / Steam item mapping). Location-tier mode appends "_t1".."_t5".</summary>
        public string Id { get; }
        public ProductKind Kind { get; }
        public string Title { get; }
        public long Chips { get; }

        /// <summary>Fraction of the energy bar restored, 0..1.</summary>
        public float Energy { get; }

        /// <summary>Price in the highest-income countries; everyone else gets a regional multiplier.</summary>
        public decimal BaseUsd { get; }
        public string Highlight { get; }
    }

    public static class ProductCatalog
    {
        public static readonly IReadOnlyList<ProductDef> All = new[]
        {
            new ProductDef("chips_5k", ProductKind.Chips, "Handful of Chips", 5_000, 0, 0.99m, null),
            new ProductDef("chips_30k", ProductKind.Chips, "Stack of Chips", 30_000, 0, 4.99m, null),
            new ProductDef("chips_75k", ProductKind.Chips, "Tray of Chips", 75_000, 0, 9.99m, null),
            new ProductDef("chips_200k", ProductKind.Chips, "Rack of Chips", 200_000, 0, 19.99m, "MOST POPULAR"),
            new ProductDef("chips_600k", ProductKind.Chips, "Vault of Chips", 600_000, 0, 49.99m, null),
            new ProductDef("chips_1500k", ProductKind.Chips, "High Roller Safe", 1_500_000, 0, 99.99m, "BEST VALUE"),

            new ProductDef("food_water", ProductKind.Food, "Sparkling Water", 0, 0.10f, 0.49m, null),
            new ProductDef("food_espresso", ProductKind.Food, "Espresso", 0, 0.20f, 0.99m, null),
            new ProductDef("food_energy", ProductKind.Food, "Energy Drink", 0, 0.35f, 1.49m, null),
            new ProductDef("food_sandwich", ProductKind.Food, "Club Sandwich", 0, 0.50f, 1.99m, null),
            new ProductDef("food_burger", ProductKind.Food, "Burger Meal", 0, 0.75f, 2.99m, "MOST POPULAR"),
            new ProductDef("food_steak", ProductKind.Food, "Steak Dinner", 0, 1.00f, 3.99m, "FULL REFILL"),
        };

        public static ProductDef Find(string id)
        {
            foreach (var p in All)
                if (p.Id == id) return p;
            return null;
        }

        public static List<ProductDef> OfKind(ProductKind kind)
        {
            var list = new List<ProductDef>();
            foreach (var p in All)
                if (p.Kind == kind) list.Add(p);
            return list;
        }

        /// <summary>Extra chips per unit of money compared to the smallest pack, as a whole percentage.</summary>
        public static int BonusPercent(ProductDef p)
        {
            if (p.Kind != ProductKind.Chips) return 0;
            var smallest = All[0];
            decimal baseRate = smallest.Chips / smallest.BaseUsd;
            decimal rate = p.Chips / p.BaseUsd;
            int pct = (int)decimal.Round((rate / baseRate - 1m) * 100m);
            return pct < 5 ? 0 : (pct + 5) / 10 * 10;
        }
    }
}
