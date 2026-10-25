using System;
using System.Collections.Generic;
using Casino.Core.Blackjack;
using Casino.Core.Economy;

namespace Casino.Core.Baccarat
{
    public enum BaccaratSide { Player, Banker, Tie }

    /// <summary>One coup: the cards in the order they were dealt, who got each, and who won.</summary>
    public sealed class Coup
    {
        public readonly List<Card> Player = new List<Card>(3), Banker = new List<Card>(3);

        /// <summary>Deal order: true for a card to the banker's hand.</summary>
        public readonly List<bool> ToBanker = new List<bool>(6);

        public int PlayerTotal => BaccaratRules.Total(Player);
        public int BankerTotal => BaccaratRules.Total(Banker);
        public bool Natural => (Player.Count == 2 && PlayerTotal >= 8) || (Banker.Count == 2 && BankerTotal >= 8);

        public BaccaratSide Winner =>
            PlayerTotal > BankerTotal ? BaccaratSide.Player : BankerTotal > PlayerTotal ? BaccaratSide.Banker : BaccaratSide.Tie;
    }

    /// <summary>
    /// Punto banco from an eight-deck shoe: tens and pictures count 0, a hand counts its last digit, and the third-card
    /// rules decide everything (no choices). Player pays 1 to 1, Banker 1 to 1 less 5% commission, Tie 8 to 1; a tie
    /// returns Player and Banker bets. House edges: Banker 1.06%, Player 1.23%, Tie 14.1%.
    /// </summary>
    public static class BaccaratRules
    {
        public const int Decks = 8;
        public const int TiePays = 8;

        public static int Value(Card c) => c.Rank >= Rank.Ten ? 0 : (int)c.Rank;

        public static int Total(IReadOnlyList<Card> hand)
        {
            int t = 0;
            foreach (var c in hand) t += Value(c);
            return t % 10;
        }

        /// <summary>Deals one coup from the shoe by the standard third-card rules.</summary>
        public static Coup Deal(Func<Card> draw)
        {
            var coup = new Coup();
            void To(bool banker)
            {
                (banker ? coup.Banker : coup.Player).Add(draw());
                coup.ToBanker.Add(banker);
            }
            To(false);
            To(true);
            To(false);
            To(true);
            if (coup.Natural) return coup;
            int p = coup.PlayerTotal, b = coup.BankerTotal;
            int third = -1;
            if (p <= 5)
            {
                To(false);
                third = Value(coup.Player[2]);
            }
            if (BankerDraws(b, third)) To(true);
            return coup;
        }

        /// <summary>The banker's third-card rule; <paramref name="playerThird"/> is -1 when the player stood.</summary>
        public static bool BankerDraws(int banker, int playerThird)
        {
            if (playerThird < 0) return banker <= 5;
            switch (banker)
            {
                case 0: case 1: case 2: return true;
                case 3: return playerThird != 8;
                case 4: return playerThird >= 2 && playerThird <= 7;
                case 5: return playerThird >= 4 && playerThird <= 7;
                case 6: return playerThird == 6 || playerThird == 7;
                default: return false;
            }
        }

        /// <summary>What comes back for a bet on <paramref name="side"/> (stake included), or 0.</summary>
        public static long Return(BaccaratSide side, long amount, BaccaratSide winner)
        {
            if (amount <= 0) return 0;
            if (winner == BaccaratSide.Tie)
                return side == BaccaratSide.Tie ? amount * (TiePays + 1) : amount;   // ties push Player and Banker
            if (side != winner) return 0;
            return side == BaccaratSide.Banker ? amount + amount * 19 / 20 : amount * 2;
        }
    }

    /// <summary>A baccarat table's shoe and bets for one coup at a time.</summary>
    public sealed class BaccaratTable
    {
        readonly IChipBank bank;

        public BaccaratTable(IRandom rng, IChipBank bank)
        {
            this.bank = bank ?? throw new ArgumentNullException(nameof(bank));
            Shoe = new Shoe(BaccaratRules.Decks, 0.85, rng ?? throw new ArgumentNullException(nameof(rng)));
        }

        public Shoe Shoe { get; }
        public bool Shuffled { get; private set; }
        public Coup Last { get; private set; }
        public long Wagered { get; private set; }
        public long Returned { get; private set; }
        public long Net => Returned - Wagered;

        /// <summary>Stakes the bets (Player, Banker, Tie), deals a coup and pays it. False if the chips aren't there.</summary>
        public bool Play(long player, long banker, long tie)
        {
            long total = player + banker + tie;
            if (total <= 0 || player < 0 || banker < 0 || tie < 0) throw new ArgumentOutOfRangeException(nameof(total));
            if (!bank.TryDebit(total)) return false;
            Shuffled = Shoe.NeedsShuffle;
            if (Shuffled) Shoe.Shuffle();
            Last = BaccaratRules.Deal(Shoe.Draw);
            var w = Last.Winner;
            Wagered = total;
            Returned = BaccaratRules.Return(BaccaratSide.Player, player, w) + BaccaratRules.Return(BaccaratSide.Banker, banker, w)
                + BaccaratRules.Return(BaccaratSide.Tie, tie, w);
            bank.Credit(Returned);
            return true;
        }
    }
}
