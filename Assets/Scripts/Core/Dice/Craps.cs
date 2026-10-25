using System;
using System.Collections.Generic;
using Casino.Core.Economy;

namespace Casino.Core.Dice
{
    public enum CrapsBetKind { Pass, DontPass, PassOdds, Field, Place, AnySeven, AnyCraps, Eleven }

    /// <summary>A bet that was settled (or paid and left up) by a roll.</summary>
    public struct CrapsSettlement
    {
        public CrapsBetKind Kind;
        public int Number;
        public long Staked, Returned;

        /// <summary>The bet is still on the table afterwards (a place bet that won).</summary>
        public bool StaysUp;
    }

    /// <summary>
    /// Craps with the bets that matter: Pass Line and Don't Pass (with a point), free Odds behind the Pass Line at true
    /// odds (3x, 4x, 5x), the Field, Place bets on 4, 5, 6, 8, 9, 10 (working only while a point is on), and the one-roll
    /// Any Seven, Any Craps and Eleven. Chips are staked when placed and paid as each roll settles them.
    /// </summary>
    public sealed class CrapsTable
    {
        public static readonly int[] PlaceNumbers = { 4, 5, 6, 8, 9, 10 };

        readonly IRandom rng;
        readonly IChipBank bank;
        readonly Dictionary<(CrapsBetKind, int), long> bets = new Dictionary<(CrapsBetKind, int), long>();

        public CrapsTable(IRandom rng, IChipBank bank)
        {
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            this.bank = bank ?? throw new ArgumentNullException(nameof(bank));
        }

        /// <summary>0 on the come-out roll, otherwise the point to make.</summary>
        public int Point { get; private set; }
        public int Die1 { get; private set; }
        public int Die2 { get; private set; }
        public int LastTotal => Die1 + Die2;

        public long BetOn(CrapsBetKind kind, int number = 0) => bets.TryGetValue((kind, number), out long a) ? a : 0;

        public long TotalOnTable
        {
            get
            {
                long sum = 0;
                foreach (var a in bets.Values) sum += a;
                return sum;
            }
        }

        /// <summary>Every bet on the layout, for saving.</summary>
        public List<(CrapsBetKind Kind, int Number, long Amount)> Bets()
        {
            var list = new List<(CrapsBetKind, int, long)>(bets.Count);
            foreach (var kv in bets) list.Add((kv.Key.Item1, kv.Key.Item2, kv.Value));
            return list;
        }

        /// <summary>Puts a saved layout and point back. The stakes were debited when the bets were placed, so nothing
        /// is debited again.</summary>
        public void Restore(int point, List<(CrapsBetKind Kind, int Number, long Amount)> saved)
        {
            Point = point;
            bets.Clear();
            if (saved == null) return;
            foreach (var (kind, number, amount) in saved)
                if (amount > 0) bets[(kind, number)] = amount;
        }

        /// <summary>How many times the pass bet the odds may be: 3x on 4 and 10, 4x on 5 and 9, 5x on 6 and 8.</summary>
        public static int OddsMultiple(int point) => point == 4 || point == 10 ? 3 : point == 5 || point == 9 ? 4 : 5;

        public bool CanBet(CrapsBetKind kind, int number = 0)
        {
            switch (kind)
            {
                case CrapsBetKind.Pass:
                case CrapsBetKind.DontPass: return Point == 0;
                case CrapsBetKind.PassOdds: return Point > 0 && BetOn(CrapsBetKind.Pass) > 0 && BetOn(CrapsBetKind.PassOdds) < MaxOdds;
                case CrapsBetKind.Place: return Array.IndexOf(PlaceNumbers, number) >= 0;
                default: return true;
            }
        }

        public long MaxOdds => Point > 0 ? BetOn(CrapsBetKind.Pass) * OddsMultiple(Point) : 0;

        /// <summary>Puts chips on a bet (stacking on what's there). The odds are capped at their maximum.</summary>
        public bool Bet(CrapsBetKind kind, int number, long amount)
        {
            if (amount <= 0 || !CanBet(kind, number)) return false;
            if (kind == CrapsBetKind.PassOdds) amount = Math.Min(amount, MaxOdds - BetOn(kind));
            if (kind != CrapsBetKind.Place) number = 0;
            if (amount <= 0 || !bank.TryDebit(amount)) return false;
            bets[(kind, number)] = BetOn(kind, number) + amount;
            return true;
        }

        /// <summary>Whether a bet may be taken back before the roll: anything but a Pass Line bet once there's a point.</summary>
        public bool CanTakeDown(CrapsBetKind kind, int number = 0) =>
            BetOn(kind, number) > 0 && !(kind == CrapsBetKind.Pass && Point > 0) && !(kind == CrapsBetKind.DontPass && Point > 0);

        public long TakeDown(CrapsBetKind kind, int number = 0)
        {
            if (!CanTakeDown(kind, number)) return 0;
            long a = BetOn(kind, number);
            bets.Remove((kind, number));
            bank.Credit(a);
            return a;
        }

        /// <summary>Rolls the dice and settles every bet the roll decides.</summary>
        public List<CrapsSettlement> Roll() => Settle(rng.Next(6) + 1, rng.Next(6) + 1);

        internal List<CrapsSettlement> Settle(int d1, int d2)
        {
            Die1 = d1;
            Die2 = d2;
            int t = d1 + d2;
            var done = new List<CrapsSettlement>();
            void Resolve(CrapsBetKind kind, int number, long returned, bool staysUp = false)
            {
                long staked = BetOn(kind, number);
                if (staked <= 0) return;
                if (!staysUp) bets.Remove((kind, number));
                if (returned > 0) bank.Credit(returned);
                done.Add(new CrapsSettlement { Kind = kind, Number = number, Staked = staked, Returned = returned, StaysUp = staysUp });
            }
            long On(CrapsBetKind k, int n = 0) => BetOn(k, n);

            // One-roll bets.
            bool fieldWins = t == 2 || t == 3 || t == 4 || t == 9 || t == 10 || t == 11 || t == 12;
            Resolve(CrapsBetKind.Field, 0, fieldWins ? On(CrapsBetKind.Field) * (t == 2 || t == 12 ? 3 : 2) : 0);
            Resolve(CrapsBetKind.AnySeven, 0, t == 7 ? On(CrapsBetKind.AnySeven) * 5 : 0);
            Resolve(CrapsBetKind.AnyCraps, 0, t == 2 || t == 3 || t == 12 ? On(CrapsBetKind.AnyCraps) * 8 : 0);
            Resolve(CrapsBetKind.Eleven, 0, t == 11 ? On(CrapsBetKind.Eleven) * 16 : 0);

            // Place bets work only while a point is on: a hit pays and stays up, a seven takes them all.
            if (Point > 0)
            {
                foreach (int n in PlaceNumbers)
                {
                    long a = On(CrapsBetKind.Place, n);
                    if (a <= 0) continue;
                    if (t == n) Resolve(CrapsBetKind.Place, n, PlaceWin(a, n), true);
                    else if (t == 7) Resolve(CrapsBetKind.Place, n, 0);
                }
            }

            // The line bets.
            if (Point == 0)
            {
                if (t == 7 || t == 11)
                {
                    Resolve(CrapsBetKind.Pass, 0, On(CrapsBetKind.Pass) * 2);
                    Resolve(CrapsBetKind.DontPass, 0, 0);
                }
                else if (t == 2 || t == 3 || t == 12)
                {
                    Resolve(CrapsBetKind.Pass, 0, 0);
                    Resolve(CrapsBetKind.DontPass, 0, t == 12 ? On(CrapsBetKind.DontPass) : On(CrapsBetKind.DontPass) * 2);   // 12 is barred: a push
                }
                else Point = t;
            }
            else if (t == Point)
            {
                long odds = On(CrapsBetKind.PassOdds);
                Resolve(CrapsBetKind.Pass, 0, On(CrapsBetKind.Pass) * 2);
                Resolve(CrapsBetKind.PassOdds, 0, odds + OddsWin(odds, Point));
                Resolve(CrapsBetKind.DontPass, 0, 0);
                Point = 0;
            }
            else if (t == 7)
            {
                Resolve(CrapsBetKind.Pass, 0, 0);
                Resolve(CrapsBetKind.PassOdds, 0, 0);
                Resolve(CrapsBetKind.DontPass, 0, On(CrapsBetKind.DontPass) * 2);
                Point = 0;
            }
            return done;
        }

        /// <summary>Winnings (stake not included) on a place bet: 9 to 5 on 4 and 10, 7 to 5 on 5 and 9, 7 to 6 on 6 and 8.</summary>
        public static long PlaceWin(long amount, int number) =>
            number == 4 || number == 10 ? Ratio(amount, 9, 5) : number == 5 || number == 9 ? Ratio(amount, 7, 5) : Ratio(amount, 7, 6);

        /// <summary>True odds: 2 to 1 on 4 and 10, 3 to 2 on 5 and 9, 6 to 5 on 6 and 8.</summary>
        public static long OddsWin(long amount, int point) =>
            point == 4 || point == 10 ? amount * 2 : point == 5 || point == 9 ? Ratio(amount, 3, 2) : Ratio(amount, 6, 5);

        /// <summary>amount × num / den rounded to the nearest chip (bets in tens make every odd come out within half a chip).</summary>
        static long Ratio(long amount, long num, long den) => (amount * num * 2 + den) / (den * 2);
    }
}
