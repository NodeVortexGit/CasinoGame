using System;
using System.Collections.Generic;

namespace Casino.Core.Player
{
    /// <summary>
    /// Paid luck for after Beginner's Luck: a pack gives a number of blackjack hands at a fixed boost, using the same
    /// visible draw as Beginner's Luck (each card has that chance of being dealt in the player's favour). It only
    /// applies to bets up to <see cref="MaxBet"/>; bigger bets are plain fair blackjack and don't use up hands. Several
    /// packs queue up, strongest first. The boost never goes below fair, and the UI shows it while it lasts.
    /// </summary>
    public sealed class LuckBoost
    {
        // Win rate of basic-strategy play at each boost level (simulated, 200,000 rounds each; the tests re-check them),
        // and the player's average result per round as a share of the bet.
        static readonly int[] Levels = { 0, 5, 10, 15, 20, 25, 30, 40, 50, 75, 100 };
        static readonly double[] WinRates = { 0.435, 0.487, 0.532, 0.577, 0.621, 0.661, 0.699, 0.769, 0.826, 0.931, 1.0 };
        static readonly double[] Edges = { -0.005, 0.110, 0.206, 0.309, 0.407, 0.499, 0.586, 0.748, 0.886, 1.112, 1.178 };

        /// <summary>The bet cap the packs are priced for (see the tests).</summary>
        public const long DefaultMaxBet = 1_000;

        readonly SaveData s;

        public LuckBoost(SaveData save, long maxBet = DefaultMaxBet)
        {
            s = save;
            MaxBet = maxBet;
            Clean();
        }

        public long MaxBet { get; }

        public int HandsLeft
        {
            get
            {
                int n = 0;
                foreach (int h in s.luckPackHands) n += h;
                return n;
            }
        }

        public bool IsActive => HandsLeft > 0;

        /// <summary>The boost the next hand gets (the strongest pack still held), in percent.</summary>
        public int Percent => s.luckPackPercent.Count > 0 ? s.luckPackPercent[0] : 0;

        /// <summary>Hands left at the current (strongest) boost.</summary>
        public int HandsAtPercent => s.luckPackHands.Count > 0 ? s.luckPackHands[0] : 0;

        public bool Applies(long bet) => IsActive && bet > 0 && bet <= MaxBet;

        /// <summary>0..1 chance that each card of a hand with this bet is dealt in the player's favour.</summary>
        public double LuckFor(long bet) => Applies(bet) ? Percent / 100.0 : 0.0;

        public void Add(int hands, int percent)
        {
            if (hands <= 0 || percent <= 0) return;
            int i = s.luckPackPercent.IndexOf(percent);
            if (i >= 0) s.luckPackHands[i] += hands;
            else
            {
                // Keep the strongest boost first.
                int at = 0;
                while (at < s.luckPackPercent.Count && s.luckPackPercent[at] > percent) at++;
                s.luckPackPercent.Insert(at, percent);
                s.luckPackHands.Insert(at, hands);
            }
        }

        /// <summary>A boosted hand was played: uses up one hand of the strongest pack.</summary>
        public void UseHand()
        {
            if (s.luckPackHands.Count == 0) return;
            s.luckPackHands[0]--;
            Clean();
        }

        void Clean()
        {
            if (s.luckPackHands.Count != s.luckPackPercent.Count)
            {
                s.luckPackHands.Clear();
                s.luckPackPercent.Clear();
            }
            for (int i = s.luckPackHands.Count - 1; i >= 0; i--)
                if (s.luckPackHands[i] <= 0)
                {
                    s.luckPackHands.RemoveAt(i);
                    s.luckPackPercent.RemoveAt(i);
                }
        }

        /// <summary>Expected share of hands won at a boost level (for telling the player plainly what they get).</summary>
        public static double WinRate(int percent) => Lerp(WinRates, percent);

        /// <summary>Average result per round at a boost level, as a share of the bet (fair blackjack is about -0.5%).</summary>
        public static double Edge(int percent) => Lerp(Edges, percent);

        static double Lerp(double[] values, int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            for (int i = 1; i < Levels.Length; i++)
                if (percent <= Levels[i])
                {
                    double t = (percent - Levels[i - 1]) / (double)(Levels[i] - Levels[i - 1]);
                    return values[i - 1] + (values[i] - values[i - 1]) * t;
                }
            return values[values.Length - 1];
        }
    }
}
