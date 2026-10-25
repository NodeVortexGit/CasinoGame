using System.Collections.Generic;
using Casino.Core.Blackjack;

namespace Casino.Core.Poker
{
    public enum HandCategory { HighCard, Pair, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush }

    /// <summary>
    /// Standard poker hand ranking: the best five cards out of any number (five for video poker, seven for hold'em).
    /// <see cref="Score"/> orders hands completely: a higher score is a better hand and equal scores split.
    /// </summary>
    public static class PokerHand
    {
        /// <summary>Ace high (2..14).</summary>
        public static int High(Card c) => c.Rank == Rank.Ace ? 14 : (int)c.Rank;

        /// <summary>The category of a score.</summary>
        public static HandCategory Category(int score) => (HandCategory)(score >> 20);

        /// <summary>
        /// The best five-card hand among <paramref name="cards"/> (at least five) as one number: the category in the top
        /// bits, then up to five tie-breaking ranks, most important first.
        /// </summary>
        public static int Score(IReadOnlyList<Card> cards)
        {
            var count = new int[15];
            var suitCount = new int[4];
            foreach (var c in cards)
            {
                count[High(c)]++;
                suitCount[(int)c.Suit]++;
            }

            // Straight flush and flush first: they beat everything a pair-based hand can make except quads/full house,
            // which are checked in order below.
            int flushSuit = -1;
            for (int s = 0; s < 4; s++)
                if (suitCount[s] >= 5) flushSuit = s;
            if (flushSuit >= 0)
            {
                var inSuit = new bool[15];
                foreach (var c in cards)
                    if ((int)c.Suit == flushSuit) inSuit[High(c)] = true;
                int sf = StraightTop(inSuit);
                if (sf > 0) return Make(sf == 14 ? HandCategory.RoyalFlush : HandCategory.StraightFlush, sf);
            }

            int quad = 0, trip = 0, trip2 = 0, pair = 0, pair2 = 0;
            for (int r = 14; r >= 2; r--)
            {
                if (count[r] == 4) quad = quad == 0 ? r : quad;
                else if (count[r] == 3)
                {
                    if (trip == 0) trip = r;
                    else if (trip2 == 0) trip2 = r;
                }
                else if (count[r] == 2)
                {
                    if (pair == 0) pair = r;
                    else if (pair2 == 0) pair2 = r;
                }
            }

            if (quad > 0) return Make(HandCategory.FourOfAKind, quad, Kickers(count, 1, quad));
            if (trip > 0 && (trip2 > 0 || pair > 0)) return Make(HandCategory.FullHouse, trip, System.Math.Max(trip2, pair));
            if (flushSuit >= 0)
            {
                var top = new List<int>(5);
                for (int r = 14; r >= 2 && top.Count < 5; r--)
                    foreach (var c in cards)
                        if ((int)c.Suit == flushSuit && High(c) == r)
                        {
                            top.Add(r);
                            break;
                        }
                return Make(HandCategory.Flush, top.ToArray());
            }
            var any = new bool[15];
            for (int r = 2; r <= 14; r++) any[r] = count[r] > 0;
            int straight = StraightTop(any);
            if (straight > 0) return Make(HandCategory.Straight, straight);
            if (trip > 0) return Make(HandCategory.ThreeOfAKind, trip, Kickers(count, 2, trip));
            if (pair2 > 0) return Make(HandCategory.TwoPair, pair, pair2, Kickers(count, 1, pair, pair2));
            if (pair > 0) return Make(HandCategory.Pair, pair, Kickers(count, 3, pair));
            return Make(HandCategory.HighCard, Kickers(count, 5));
        }

        /// <summary>Top card of the highest straight among the ranks present (5 for the wheel A-2-3-4-5), or 0.</summary>
        static int StraightTop(bool[] has)
        {
            for (int top = 14; top >= 5; top--)
            {
                bool run = true;
                for (int r = top; r > top - 5 && run; r--)
                    run = r == 1 ? has[14] : has[r];   // the ace also plays low, under the 2
                if (run) return top;
            }
            return 0;
        }

        static int[] Kickers(int[] count, int how, params int[] skip)
        {
            var k = new List<int>(how);
            for (int r = 14; r >= 2 && k.Count < how; r--)
            {
                if (count[r] == 0 || System.Array.IndexOf(skip, r) >= 0) continue;
                k.Add(r);
            }
            return k.ToArray();
        }

        static int Make(HandCategory category, int first, params int[] rest)
        {
            var all = new int[rest.Length + 1];
            all[0] = first;
            rest.CopyTo(all, 1);
            return Make(category, all);
        }

        static int Make(HandCategory category, int first, int second, int[] rest)
        {
            var all = new int[rest.Length + 2];
            all[0] = first;
            all[1] = second;
            rest.CopyTo(all, 2);
            return Make(category, all);
        }

        static int Make(HandCategory category, int[] ranks)
        {
            int score = (int)category;
            for (int i = 0; i < 5; i++) score = (score << 4) | (i < ranks.Length ? ranks[i] : 0);
            return score;
        }

        /// <summary>The name players know a category by.</summary>
        public static string Name(HandCategory category)
        {
            switch (category)
            {
                case HandCategory.RoyalFlush: return Tr.T("Royal flush");
                case HandCategory.StraightFlush: return Tr.T("Straight flush");
                case HandCategory.FourOfAKind: return Tr.T("Four of a kind");
                case HandCategory.FullHouse: return Tr.T("Full house");
                case HandCategory.Flush: return Tr.T("Flush");
                case HandCategory.Straight: return Tr.T("Straight");
                case HandCategory.ThreeOfAKind: return Tr.T("Three of a kind");
                case HandCategory.TwoPair: return Tr.T("Two pair");
                case HandCategory.Pair: return Tr.T("Pair");
                default: return Tr.T("High card");
            }
        }
    }
}
