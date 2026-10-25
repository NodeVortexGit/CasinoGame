using System.Collections.Generic;

namespace Casino.Core.Economy
{
    public sealed class TableDef
    {
        public TableDef(string id, string name, long minBet, long maxBet, int theme, int level = 1)
        {
            Id = id;
            Level = level;
            Name = name;
            MinBet = minBet;
            MaxBet = maxBet;
            Theme = theme;
            ChipValues = new[] { minBet, minBet * 5 / 2, minBet * 5, minBet * 10, minBet * 50 };
        }

        public string Id { get; }

        /// <summary>Player level needed to sit down here.</summary>
        public int Level { get; }
        public string Name { get; }
        public long MinBet { get; }
        public long MaxBet { get; }

        /// <summary>Index into the view's table themes (felt colour, accent, dealer).</summary>
        public int Theme { get; }
        public long[] ChipValues { get; }
    }

    public static class TableCatalog
    {
        public static readonly IReadOnlyList<TableDef> Blackjack = new[]
        {
            new TableDef("bj_bronze", Tr.Mark("Bronze Table"), 10, 500, 0),
            new TableDef("bj_silver", Tr.Mark("Silver Table"), 100, 5_000, 1, 3),
            new TableDef("bj_gold", Tr.Mark("Gold Table"), 1_000, 50_000, 2, 6),
            new TableDef("bj_diamond", Tr.Mark("Diamond Table"), 10_000, 500_000, 3, 10),
        };

        /// <summary>Ultimate Texas Hold'em: the ante's limits (the blind matches it, and a raise can be four antes more).</summary>
        public static readonly TableDef Holdem = new TableDef("holdem", Tr.Mark("Hold'em Table"), 10, 5_000, 6);

        public static TableDef Find(string id)
        {
            foreach (var t in Blackjack)
                if (t.Id == id) return t;
            return null;
        }
    }
}
