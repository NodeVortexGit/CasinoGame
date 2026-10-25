using System.Collections.Generic;

namespace Casino.Core.Roulette
{
    public enum RouletteBetKind { Straight, Red, Black, Odd, Even, Low, High, Dozen1, Dozen2, Dozen3, Column1, Column2, Column3 }

    public struct RouletteBet
    {
        public RouletteBetKind Kind;
        public int Number;   // only for Straight
        public long Amount;

        public RouletteBet(RouletteBetKind kind, long amount, int number = 0)
        {
            Kind = kind;
            Amount = amount;
            Number = number;
        }

        /// <summary>Two bets on the same spot stack into one.</summary>
        public bool SameSpot(RouletteBet other) => Kind == other.Kind && (Kind != RouletteBetKind.Straight || Number == other.Number);
    }

    /// <summary>European single-zero roulette (house edge 1/37 ≈ 2.7% on every bet).</summary>
    public static class RouletteRules
    {
        public const int Pockets = 37;

        /// <summary>Numbers in order around a European wheel, clockwise from zero.</summary>
        public static readonly int[] WheelOrder =
            { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };

        static readonly HashSet<int> Reds = new HashSet<int> { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

        public static bool IsRed(int n) => Reds.Contains(n);
        public static bool IsBlack(int n) => n != 0 && !Reds.Contains(n);

        public static int Spin(IRandom rng) => rng.Next(Pockets);

        /// <summary>Winnings per chip on a win (the stake is returned on top).</summary>
        public static int PayoutOdds(RouletteBetKind kind)
        {
            switch (kind)
            {
                case RouletteBetKind.Straight: return 35;
                case RouletteBetKind.Dozen1:
                case RouletteBetKind.Dozen2:
                case RouletteBetKind.Dozen3:
                case RouletteBetKind.Column1:
                case RouletteBetKind.Column2:
                case RouletteBetKind.Column3: return 2;
                default: return 1;
            }
        }

        public static bool Wins(RouletteBet bet, int n)
        {
            switch (bet.Kind)
            {
                case RouletteBetKind.Straight: return n == bet.Number;
                case RouletteBetKind.Red: return IsRed(n);
                case RouletteBetKind.Black: return IsBlack(n);
                case RouletteBetKind.Odd: return n != 0 && n % 2 == 1;
                case RouletteBetKind.Even: return n != 0 && n % 2 == 0;
                case RouletteBetKind.Low: return n >= 1 && n <= 18;
                case RouletteBetKind.High: return n >= 19;
                case RouletteBetKind.Dozen1: return n >= 1 && n <= 12;
                case RouletteBetKind.Dozen2: return n >= 13 && n <= 24;
                case RouletteBetKind.Dozen3: return n >= 25;
                case RouletteBetKind.Column1: return n != 0 && n % 3 == 1;
                case RouletteBetKind.Column2: return n != 0 && n % 3 == 2;
                case RouletteBetKind.Column3: return n != 0 && n % 3 == 0;
                default: return false;
            }
        }

        /// <summary>What comes back to the player for this bet: stake plus winnings, or nothing.</summary>
        public static long Return(RouletteBet bet, int n) => Wins(bet, n) ? bet.Amount * (PayoutOdds(bet.Kind) + 1) : 0;

        public static string Label(RouletteBetKind kind, int number = 0)
        {
            switch (kind)
            {
                case RouletteBetKind.Straight: return number.ToString();
                case RouletteBetKind.Low: return "1-18";
                case RouletteBetKind.High: return "19-36";
                case RouletteBetKind.Dozen1: return Tr.T("1st 12");
                case RouletteBetKind.Dozen2: return Tr.T("2nd 12");
                case RouletteBetKind.Dozen3: return Tr.T("3rd 12");
                case RouletteBetKind.Column1:
                case RouletteBetKind.Column2:
                case RouletteBetKind.Column3: return Tr.T("2 to 1");
                case RouletteBetKind.Red: return Tr.T("RED");
                case RouletteBetKind.Black: return Tr.T("BLACK");
                case RouletteBetKind.Odd: return Tr.T("ODD");
                case RouletteBetKind.Even: return Tr.T("EVEN");
                default: return kind.ToString().ToUpperInvariant();
            }
        }
    }
}
