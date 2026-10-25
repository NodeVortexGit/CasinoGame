using System;
using System.Collections.Generic;

namespace Casino.Core.Slots
{
    public enum SlotSymbol { Cherry, Lemon, Orange, Plum, Bell, Bar, Seven, Diamond, Wild, Scatter }

    public sealed class LineWin
    {
        public int Line;
        public SlotSymbol Symbol;
        public int Count;
        public long Payout;
    }

    public sealed class SpinResult
    {
        public long Bet;
        public int[] Stops;
        /// <summary>[reel, row], row 0 = top.</summary>
        public SlotSymbol[,] Grid;
        public readonly List<LineWin> Lines = new List<LineWin>();
        public int Scatters;
        public long ScatterPayout;
        public long TotalPayout;
    }

    /// <summary>
    /// "Lucky Ace 7s": 5 reels, 3 rows, 10 fixed paylines. The Ace is wild (substitutes for everything but the star);
    /// three or more stars anywhere pay a multiple of the total bet. Every stop on every reel is equally likely, so the
    /// payback below is exact, not an estimate, and it is shown to the player in the paytable.
    /// </summary>
    public static class SlotMachine
    {
        public const int Reels = 5, Rows = 3, LineCount = 10;

        // C=Cherry L=Lemon O=Orange P=Plum B=Bell X=Bar S=Seven D=Diamond W=Wild T=scatTer (star)
        static readonly string[] StripText =
        {
            "CSOLCOCPCPWBCTOXLWBXODLPSB",
            "BWPDXLOCTCDPBLPCXLCOCSLBC",
            "ODXOLDSXDLCWOPDOLPBDLBTSPCPLC",
            "BCLPCPOWPLCBXWLBPCDWTO",
            "COLSDLOPOBSPOLCSCPLTWPX",
        };

        public static readonly SlotSymbol[][] Strips = BuildStrips();

        /// <summary>Row per reel for each payline (0 = top row).</summary>
        public static readonly int[][] Paylines =
        {
            new[] { 1, 1, 1, 1, 1 },
            new[] { 0, 0, 0, 0, 0 },
            new[] { 2, 2, 2, 2, 2 },
            new[] { 0, 1, 2, 1, 0 },
            new[] { 2, 1, 0, 1, 2 },
            new[] { 0, 0, 1, 2, 2 },
            new[] { 2, 2, 1, 0, 0 },
            new[] { 1, 0, 0, 0, 1 },
            new[] { 1, 2, 2, 2, 1 },
            new[] { 1, 0, 1, 2, 1 },
        };

        /// <summary>
        /// Line pays, in line bets, for 3, 4 and 5 in a row from the left reel. Nothing pays for two in a row: small pays
        /// on half the spins felt like winning all the time while losing, so about 3 spins in 10 pay, and they pay more.
        /// </summary>
        public static readonly int[][] Pays =
        {
            new[] { 8, 20, 50 },      // Cherry
            new[] { 8, 20, 50 },      // Lemon
            new[] { 10, 25, 75 },     // Orange
            new[] { 10, 30, 90 },     // Plum
            new[] { 15, 45, 150 },    // Bell
            new[] { 22, 70, 220 },    // Bar
            new[] { 40, 120, 500 },   // Seven
            new[] { 60, 200, 1000 },  // Diamond
            new[] { 100, 500, 2500 }, // Wild (Ace)
        };

        /// <summary>Scatter pays in total bets for 3, 4 and 5 stars anywhere.</summary>
        public static readonly int[] ScatterPays = { 2, 10, 50 };

        /// <summary>Total bet choices (10 lines, so each is a multiple of 10).</summary>
        public static readonly long[] BetOptions = { 10, 20, 50, 100, 200, 500, 1_000, 2_500, 5_000, 10_000, 25_000, 50_000 };

        static SlotSymbol[][] BuildStrips()
        {
            const string code = "CLOPBXSDWT";
            var strips = new SlotSymbol[StripText.Length][];
            for (int r = 0; r < StripText.Length; r++)
            {
                strips[r] = new SlotSymbol[StripText[r].Length];
                for (int i = 0; i < StripText[r].Length; i++) strips[r][i] = (SlotSymbol)code.IndexOf(StripText[r][i]);
            }
            return strips;
        }

        public static SlotSymbol At(int reel, int stop, int row)
        {
            var strip = Strips[reel];
            return strip[(stop + row) % strip.Length];
        }

        public static SpinResult Spin(IRandom rng, long totalBet)
        {
            var stops = new int[Reels];
            for (int r = 0; r < Reels; r++) stops[r] = rng.Next(Strips[r].Length);
            return Evaluate(stops, totalBet);
        }

        public static SpinResult Evaluate(int[] stops, long totalBet)
        {
            var result = new SpinResult { Bet = totalBet, Stops = stops, Grid = new SlotSymbol[Reels, Rows] };
            for (int r = 0; r < Reels; r++)
                for (int row = 0; row < Rows; row++)
                {
                    var s = At(r, stops[r], row);
                    result.Grid[r, row] = s;
                    if (s == SlotSymbol.Scatter) result.Scatters++;
                }

            long lineBet = totalBet / LineCount;
            var seq = new SlotSymbol[Reels];
            for (int l = 0; l < Paylines.Length; l++)
            {
                for (int r = 0; r < Reels; r++) seq[r] = result.Grid[r, Paylines[l][r]];
                int multiplier = LinePay(seq, out var symbol, out int count);
                if (multiplier <= 0) continue;
                var win = new LineWin { Line = l, Symbol = symbol, Count = count, Payout = multiplier * lineBet };
                result.Lines.Add(win);
                result.TotalPayout += win.Payout;
            }
            if (result.Scatters >= 3)
            {
                result.ScatterPayout = ScatterPays[Math.Min(result.Scatters, 5) - 3] * totalBet;
                result.TotalPayout += result.ScatterPayout;
            }
            return result;
        }

        /// <summary>Pay for one line, in line bets: the best of a run of a symbol (with wilds) or a run of wilds.</summary>
        public static int LinePay(SlotSymbol[] seq, out SlotSymbol symbol, out int count)
        {
            int wilds = 0;
            while (wilds < Reels && seq[wilds] == SlotSymbol.Wild) wilds++;
            int wildPay = wilds >= 3 ? Pays[(int)SlotSymbol.Wild][wilds - 3] : 0;
            symbol = SlotSymbol.Wild;
            count = wilds;
            if (wilds == Reels) return wildPay;
            var s = seq[wilds];
            if (s == SlotSymbol.Scatter) return wildPay;
            int run = wilds;
            while (run < Reels && (seq[run] == s || seq[run] == SlotSymbol.Wild)) run++;
            int pay = run >= 3 ? Pays[(int)s][run - 3] : 0;
            if (pay > wildPay)
            {
                symbol = s;
                count = run;
                return pay;
            }
            return wildPay;
        }

        static double rtp = -1;

        /// <summary>
        /// Exact long-run payback (0..1). Each reel stop is equally likely and reels are independent, so every line sees
        /// the same symbol odds per reel: it is enough to weigh every possible line and the scatter counts.
        /// </summary>
        public static double ExactRtp()
        {
            if (rtp >= 0) return rtp;
            const int symbols = 10;
            var p = new double[Reels, symbols];
            for (int r = 0; r < Reels; r++)
            {
                foreach (var s in Strips[r]) p[r, (int)s] += 1.0 / Strips[r].Length;
            }
            double line = 0;
            var seq = new SlotSymbol[Reels];
            var idx = new int[Reels];
            int total = 1;
            for (int r = 0; r < Reels; r++) total *= symbols;
            for (int n = 0; n < total; n++)
            {
                int k = n;
                double prob = 1;
                for (int r = 0; r < Reels; r++)
                {
                    idx[r] = k % symbols;
                    k /= symbols;
                    prob *= p[r, idx[r]];
                    seq[r] = (SlotSymbol)idx[r];
                }
                if (prob <= 0) continue;
                line += prob * LinePay(seq, out _, out _);
            }

            // Scatters: each reel shows a star with probability 3 * stars / length (no strip has two stars within 3 stops).
            var dist = new double[Reels + 1];
            dist[0] = 1;
            for (int r = 0; r < Reels; r++)
            {
                int stars = 0;
                foreach (var s in Strips[r]) if (s == SlotSymbol.Scatter) stars++;
                double q = 3.0 * stars / Strips[r].Length;
                for (int c = r + 1; c >= 0; c--) dist[c] = dist[c] * (1 - q) + (c > 0 ? dist[c - 1] * q : 0);
            }
            double scatter = 0;
            for (int c = 3; c <= Reels; c++) scatter += dist[c] * ScatterPays[c - 3];
            // 10 lines at a tenth of the bet each: line payback equals one line's expected pay in line bets.
            rtp = line + scatter;
            return rtp;
        }
    }
}
