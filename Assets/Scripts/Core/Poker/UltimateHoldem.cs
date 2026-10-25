using System;
using System.Collections.Generic;
using Casino.Core.Blackjack;
using Casino.Core.Economy;

namespace Casino.Core.Poker
{
    public enum HoldemPhase { Idle, Preflop, Flop, River, Complete }

    public enum HoldemOutcome { None, Win, Lose, Push, Fold }

    /// <summary>
    /// Ultimate Texas Hold'em against the dealer. Equal Ante and Blind bets; you and the dealer get two cards each, with five
    /// shared cards. You raise once: 3 or 4 times the ante before the flop, 2 times after it, or 1 time at the river (or
    /// fold). The dealer needs a pair to qualify; when they don't, the Ante comes back. The Blind pays a bonus for a
    /// straight or better when you win. A fresh, shuffled 52-card deck every hand; about a 2% house edge played well.
    /// </summary>
    public sealed class HoldemRound
    {
        /// <summary>Blind pays (to 1) for a winning hand of at least a straight; anything less pushes.</summary>
        public static readonly (HandCategory hand, int numerator, int denominator)[] BlindPays =
        {
            (HandCategory.RoyalFlush, 500, 1),
            (HandCategory.StraightFlush, 50, 1),
            (HandCategory.FourOfAKind, 10, 1),
            (HandCategory.FullHouse, 3, 1),
            (HandCategory.Flush, 3, 2),
            (HandCategory.Straight, 1, 1),
        };

        readonly IChipBank bank;
        readonly Shoe deck;
        readonly Card[] player = new Card[2], dealer = new Card[2], board = new Card[5];

        public HoldemRound(IRandom rng, IChipBank bank)
        {
            this.bank = bank ?? throw new ArgumentNullException(nameof(bank));
            deck = new Shoe(1, 0.95, rng ?? throw new ArgumentNullException(nameof(rng)));
        }

        public HoldemPhase Phase { get; private set; } = HoldemPhase.Idle;
        public long Ante { get; private set; }
        public long Blind => Ante;
        public long Play { get; private set; }

        /// <summary>The raise you made, in antes (0 before you raise or when you fold).</summary>
        public int PlayMultiple { get; private set; }

        public IReadOnlyList<Card> PlayerCards => player;
        public IReadOnlyList<Card> DealerCards => dealer;
        public IReadOnlyList<Card> Board => board;

        /// <summary>How many shared cards are face up: 0 before the flop, 3 after it, 5 at the river.</summary>
        public int BoardShown { get; private set; }

        public bool InProgress => Phase == HoldemPhase.Preflop || Phase == HoldemPhase.Flop || Phase == HoldemPhase.River;
        public HoldemOutcome Outcome { get; private set; }
        public bool DealerQualifies { get; private set; }
        public int PlayerScore { get; private set; }
        public int DealerScore { get; private set; }
        public long AnteReturn { get; private set; }
        public long BlindReturn { get; private set; }
        public long PlayReturn { get; private set; }
        public long TotalWagered => Ante * 2 + Play;
        public long TotalReturned => AnteReturn + BlindReturn + PlayReturn;
        public long Net => TotalReturned - TotalWagered;

        /// <summary>The raises allowed now, in antes.</summary>
        public int[] RaiseOptions
        {
            get
            {
                switch (Phase)
                {
                    case HoldemPhase.Preflop: return new[] { 4, 3 };
                    case HoldemPhase.Flop: return new[] { 2 };
                    case HoldemPhase.River: return new[] { 1 };
                    default: return Array.Empty<int>();
                }
            }
        }

        public bool CanRaise(int multiple) => Array.IndexOf(RaiseOptions, multiple) >= 0 && bank.Balance >= Ante * multiple;
        public bool CanCheck => Phase == HoldemPhase.Preflop || Phase == HoldemPhase.Flop;
        public bool CanFold => Phase == HoldemPhase.River;

        /// <summary>Takes the Ante and the Blind (each <paramref name="ante"/>) and deals. False if the chips aren't there.</summary>
        public bool Start(long ante)
        {
            if (InProgress) throw new InvalidOperationException("A hand is already in progress.");
            if (ante <= 0) throw new ArgumentOutOfRangeException(nameof(ante));
            if (!bank.TryDebit(ante * 2)) return false;
            deck.Shuffle();
            // Dealt in casino order: one card at a time to the player then the dealer, then the board.
            for (int i = 0; i < 2; i++)
            {
                player[i] = deck.Draw();
                dealer[i] = deck.Draw();
            }
            for (int i = 0; i < 5; i++) board[i] = deck.Draw();
            Begin(ante);
            return true;
        }

        /// <summary>For the tests: a hand with chosen cards.</summary>
        internal bool StartWith(long ante, Card[] playerCards, Card[] dealerCards, Card[] boardCards)
        {
            if (!bank.TryDebit(ante * 2)) return false;
            playerCards.CopyTo(player, 0);
            dealerCards.CopyTo(dealer, 0);
            boardCards.CopyTo(board, 0);
            Begin(ante);
            return true;
        }

        void Begin(long ante)
        {
            Ante = ante;
            Play = 0;
            PlayMultiple = 0;
            AnteReturn = BlindReturn = PlayReturn = 0;
            Outcome = HoldemOutcome.None;
            BoardShown = 0;
            Phase = HoldemPhase.Preflop;
        }

        public void Check()
        {
            if (!CanCheck) throw new InvalidOperationException("Can't check now.");
            if (Phase == HoldemPhase.Preflop)
            {
                BoardShown = 3;
                Phase = HoldemPhase.Flop;
            }
            else
            {
                BoardShown = 5;
                Phase = HoldemPhase.River;
            }
        }

        /// <summary>Raises <paramref name="multiple"/> times the ante; the rest of the board comes and the hand is settled.</summary>
        public bool Raise(int multiple)
        {
            if (Array.IndexOf(RaiseOptions, multiple) < 0) throw new InvalidOperationException("That raise isn't allowed now.");
            if (!bank.TryDebit(Ante * multiple)) return false;
            Play = Ante * multiple;
            PlayMultiple = multiple;
            BoardShown = 5;
            Showdown();
            return true;
        }

        /// <summary>Gives up at the river: the Ante and Blind are lost.</summary>
        public void Fold()
        {
            if (!CanFold) throw new InvalidOperationException("You can only fold at the river.");
            BoardShown = 5;
            Score();
            Outcome = HoldemOutcome.Fold;
            Phase = HoldemPhase.Complete;
        }

        public static int Best(IReadOnlyList<Card> hole, IReadOnlyList<Card> board, int boardCount = 5)
        {
            var all = new List<Card>(7);
            all.AddRange(hole);
            for (int i = 0; i < boardCount; i++) all.Add(board[i]);
            return PokerHand.Score(all);
        }

        void Score()
        {
            PlayerScore = Best(player, board);
            DealerScore = Best(dealer, board);
            DealerQualifies = PokerHand.Category(DealerScore) >= HandCategory.Pair;
        }

        void Showdown()
        {
            Score();
            if (PlayerScore > DealerScore)
            {
                Outcome = HoldemOutcome.Win;
                PlayReturn = Play * 2;
                AnteReturn = DealerQualifies ? Ante * 2 : Ante;
                BlindReturn = Ante + BlindWin(Ante, PokerHand.Category(PlayerScore));
            }
            else if (PlayerScore < DealerScore)
            {
                Outcome = HoldemOutcome.Lose;
                AnteReturn = DealerQualifies ? 0 : Ante;
            }
            else
            {
                Outcome = HoldemOutcome.Push;
                PlayReturn = Play;
                AnteReturn = Ante;
                BlindReturn = Ante;
            }
            bank.Credit(TotalReturned);
            Phase = HoldemPhase.Complete;
        }

        /// <summary>What a winning Blind pays on top of the stake (nothing below a straight).</summary>
        public static long BlindWin(long blind, HandCategory hand)
        {
            foreach (var (h, num, den) in BlindPays)
                if (h == hand) return blind * num / den;
            return 0;
        }
    }

    /// <summary>
    /// A simple, well-known way to play Ultimate Texas Hold'em close to perfectly (for the automated tests and the tour).
    /// Returns the raise in antes, or 0 to check (or fold at the river).
    /// </summary>
    public static class HoldemStrategy
    {
        public static int Decide(HoldemRound round)
        {
            var hole = round.PlayerCards;
            int a = PokerHand.High(hole[0]), b = PokerHand.High(hole[1]);
            int hi = Math.Max(a, b), lo = Math.Min(a, b);
            bool suited = hole[0].Suit == hole[1].Suit;
            switch (round.Phase)
            {
                case HoldemPhase.Preflop:
                    bool raise = (a == b && a >= 3) || hi == 14
                        || (hi == 13 && (suited || lo >= 5))
                        || (hi == 12 && (lo >= 8 || (suited && lo >= 6)))
                        || (hi == 11 && (lo >= 10 || (suited && lo >= 8)));
                    return raise ? 4 : 0;
                case HoldemPhase.Flop:
                    if (HiddenPairOrBetter(round, 3, 3) || FlushDrawWithHiddenTen(round)) return 2;
                    return 0;
                case HoldemPhase.River:
                    // Exact: betting is worth it whenever, over every hand the dealer could hold, it loses less than the
                    // two antes folding gives up.
                    return RiverBetValue(round) > -2.0 ? 1 : 0;
                default:
                    return 0;
            }
        }

        static readonly Card[] FullDeck = BuildDeck();

        static Card[] BuildDeck()
        {
            var d = new Card[52];
            for (int s = 0; s < 4; s++)
                for (int r = 1; r <= 13; r++) d[s * 13 + r - 1] = new Card((Rank)r, (Suit)s);
            return d;
        }

        /// <summary>The average result, in antes, of betting 1x at the river over all the dealer's possible two cards.</summary>
        public static double RiverBetValue(HoldemRound round)
        {
            var seen = new HashSet<Card>(round.PlayerCards);
            for (int i = 0; i < 5; i++) seen.Add(round.Board[i]);
            var rest = new List<Card>(45);
            foreach (var c in FullDeck)
                if (!seen.Contains(c)) rest.Add(c);
            int mine = HoldemRound.Best(round.PlayerCards, round.Board);
            double bonus = HoldemRound.BlindWin(1_000, PokerHand.Category(mine)) / 1_000.0;
            var theirs = new Card[2];
            double sum = 0;
            int n = 0;
            for (int a = 0; a < rest.Count; a++)
                for (int b = a + 1; b < rest.Count; b++)
                {
                    theirs[0] = rest[a];
                    theirs[1] = rest[b];
                    int score = HoldemRound.Best(theirs, round.Board);
                    bool qualifies = PokerHand.Category(score) >= HandCategory.Pair;
                    if (mine > score) sum += 1 + (qualifies ? 1 : 0) + bonus;
                    else if (mine < score) sum -= 2 + (qualifies ? 1 : 0);
                    n++;
                }
            return sum / n;
        }

        /// <summary>Two pair or better, or a pair made with one of your own cards (pocket pairs above <paramref name="minPocket"/>).</summary>
        static bool HiddenPairOrBetter(HoldemRound round, int boardCount, int minPocket)
        {
            var hole = round.PlayerCards;
            int score = HoldemRound.Best(hole, round.Board, boardCount);
            var cat = PokerHand.Category(score);
            if (cat >= HandCategory.TwoPair)
            {
                // Two pair on the board alone plays the board; it needs one of your cards to count.
                var boardOnly = new List<Card>();
                for (int i = 0; i < boardCount; i++) boardOnly.Add(round.Board[i]);
                if (boardCount < 5 || score > PokerHand.Score(boardOnly)) return true;
            }
            int a = PokerHand.High(hole[0]), b = PokerHand.High(hole[1]);
            if (a == b) return a >= minPocket;
            for (int i = 0; i < boardCount; i++)
            {
                int r = PokerHand.High(round.Board[i]);
                if (r == a || r == b) return true;
            }
            return false;
        }

        static bool FlushDrawWithHiddenTen(HoldemRound round)
        {
            var hole = round.PlayerCards;
            for (int s = 0; s < 4; s++)
            {
                int n = 0;
                bool hiddenTen = false;
                foreach (var c in hole)
                    if ((int)c.Suit == s)
                    {
                        n++;
                        hiddenTen |= PokerHand.High(c) >= 10;
                    }
                for (int i = 0; i < 3; i++)
                    if ((int)round.Board[i].Suit == s) n++;
                if (n >= 4 && hiddenTen) return true;
            }
            return false;
        }
    }
}
