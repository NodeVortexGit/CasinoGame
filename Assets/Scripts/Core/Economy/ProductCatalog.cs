using System.Collections.Generic;

namespace Casino.Core.Economy
{
    public enum ProductKind { Chips, Food, Luck }

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

        /// <summary>A luck pack: a number of blackjack hands at a fixed boost (see <see cref="Player.LuckBoost"/>).</summary>
        public ProductDef(string id, string title, int luckHands, int luckPercent, decimal baseUsd, string highlight)
            : this(id, ProductKind.Luck, title, 0, 0, baseUsd, highlight)
        {
            LuckHands = luckHands;
            LuckPercent = luckPercent;
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

        public int LuckHands { get; }
        public int LuckPercent { get; }
    }

    public static class ProductCatalog
    {
        public static readonly IReadOnlyList<ProductDef> All = new[]
        {
            new ProductDef("chips_5k", ProductKind.Chips, Tr.Mark("Handful of Chips"), 5_000, 0, 0.99m, null),
            new ProductDef("chips_30k", ProductKind.Chips, Tr.Mark("Stack of Chips"), 30_000, 0, 4.99m, null),
            new ProductDef("chips_75k", ProductKind.Chips, Tr.Mark("Tray of Chips"), 75_000, 0, 9.99m, null),
            new ProductDef("chips_200k", ProductKind.Chips, Tr.Mark("Rack of Chips"), 200_000, 0, 19.99m, Tr.Mark("MOST POPULAR")),
            new ProductDef("chips_600k", ProductKind.Chips, Tr.Mark("Vault of Chips"), 600_000, 0, 49.99m, null),
            new ProductDef("chips_1500k", ProductKind.Chips, Tr.Mark("High Roller Safe"), 1_500_000, 0, 99.99m, Tr.Mark("BEST VALUE")),

            new ProductDef("food_water", ProductKind.Food, Tr.Mark("Sparkling Water"), 0, 0.10f, 0.49m, null),
            new ProductDef("food_espresso", ProductKind.Food, Tr.Mark("Espresso"), 0, 0.20f, 0.99m, null),
            new ProductDef("food_energy", ProductKind.Food, Tr.Mark("Energy Drink"), 0, 0.35f, 1.49m, null),
            new ProductDef("food_sandwich", ProductKind.Food, Tr.Mark("Club Sandwich"), 0, 0.50f, 1.99m, null),
            new ProductDef("food_burger", ProductKind.Food, Tr.Mark("Burger Meal"), 0, 0.75f, 2.99m, Tr.Mark("MOST POPULAR")),
            new ProductDef("food_steak", ProductKind.Food, Tr.Mark("Steak Dinner"), 0, 1.00f, 3.99m, Tr.Mark("FULL REFILL")),

            // Priced so their expected chips at the bet cap match what the same money buys as chips (see tests).
            new ProductDef("luck_25", Tr.Mark("Lucky Streak"), 25, 20, 1.99m, null),
            new ProductDef("luck_50", Tr.Mark("Hot Streak"), 50, 30, 4.99m, Tr.Mark("MOST LUCK")),
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
