using System;

namespace Casino.Core.Dice
{
    public enum SicBoBetKind { Small, Big, Odd, Even, Total, AnyTriple, Triple, Double, Single }

    public struct SicBoBet
    {
        public SicBoBetKind Kind;
        public int Number;   // the total (4-17) or the die face (1-6), where the kind needs one
        public long Amount;

        public SicBoBet(SicBoBetKind kind, int number, long amount)
        {
            Kind = kind;
            Number = number;
            Amount = amount;
        }
    }

    /// <summary>
    /// Sic bo: three dice. Small (4-10) and Big (11-17) pay 1 to 1 and lose to any triple (2.8% house edge, the best
    /// bets on the table); totals, doubles and triples pay more for longer odds. Every outcome of three fair dice is
    /// equally likely, so each bet's edge can be checked exactly over the 216 rolls.
    /// </summary>
    public static class SicBoRules
    {
        public static int[] Roll(IRandom rng) => new[] { rng.Next(6) + 1, rng.Next(6) + 1, rng.Next(6) + 1 };

        /// <summary>What a total pays (to 1).</summary>
        public static int TotalPays(int total)
        {
            switch (total)
            {
                case 4: case 17: return 60;
                case 5: case 16: return 30;
                case 6: case 15: return 17;
                case 7: case 14: return 12;
                case 8: case 13: return 8;
                default: return 6;   // 9, 10, 11, 12
            }
        }

        public const int AnyTriplePays = 30, TriplePays = 180, DoublePays = 10;

        /// <summary>The stake plus winnings for a bet on this roll, or 0.</summary>
        public static long Return(SicBoBet bet, int[] dice)
        {
            if (bet.Amount <= 0) return 0;
            int total = dice[0] + dice[1] + dice[2];
            bool triple = dice[0] == dice[1] && dice[1] == dice[2];
            int count = 0;
            foreach (int d in dice)
                if (d == bet.Number) count++;
            int pays;
            switch (bet.Kind)
            {
                case SicBoBetKind.Small: pays = !triple && total <= 10 ? 1 : -1; break;
                case SicBoBetKind.Big: pays = !triple && total >= 11 ? 1 : -1; break;
                case SicBoBetKind.Odd: pays = !triple && total % 2 == 1 ? 1 : -1; break;
                case SicBoBetKind.Even: pays = !triple && total % 2 == 0 ? 1 : -1; break;
                case SicBoBetKind.Total: pays = total == bet.Number ? TotalPays(total) : -1; break;
                case SicBoBetKind.AnyTriple: pays = triple ? AnyTriplePays : -1; break;
                case SicBoBetKind.Triple: pays = triple && dice[0] == bet.Number ? TriplePays : -1; break;
                case SicBoBetKind.Double: pays = count >= 2 ? DoublePays : -1; break;
                case SicBoBetKind.Single: pays = count > 0 ? count : -1; break;
                default: pays = -1; break;
            }
            return pays < 0 ? 0 : bet.Amount * (pays + 1);
        }

        /// <summary>The house edge of a bet, exactly, over all 216 rolls.</summary>
        public static double Edge(SicBoBetKind kind, int number)
        {
            long back = 0;
            var dice = new int[3];
            for (int a = 1; a <= 6; a++)
                for (int b = 1; b <= 6; b++)
                    for (int c = 1; c <= 6; c++)
                    {
                        dice[0] = a;
                        dice[1] = b;
                        dice[2] = c;
                        back += Return(new SicBoBet(kind, number, 1), dice);
                    }
            return 1.0 - back / 216.0;
        }

        public static string Label(SicBoBetKind kind, int number)
        {
            switch (kind)
            {
                case SicBoBetKind.Small: return Tr.T("SMALL");
                case SicBoBetKind.Big: return Tr.T("BIG");
                case SicBoBetKind.Odd: return Tr.T("ODD");
                case SicBoBetKind.Even: return Tr.T("EVEN");
                case SicBoBetKind.AnyTriple: return Tr.T("ANY TRIPLE");
                default: return number.ToString();
            }
        }
    }
}
