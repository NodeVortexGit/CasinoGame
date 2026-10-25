using System;
using System.Collections.Generic;
using Casino.Core.Blackjack;
using Casino.Core.Economy;
using Casino.Core.Poker;

namespace Casino.Core.VideoPoker
{
    public enum VideoPokerHand { Nothing, JacksOrBetter, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush }

    public enum VideoPokerPhase { Idle, Dealt, Complete }

    /// <summary>
    /// "Jacks or Better", full-pay 9/6: five cards from a fresh 52-card deck, hold any, draw once. Pays (for one, stake
    /// included): royal 800, straight flush 50, four of a kind 25, full house 9, flush 6, straight 4, three of a kind 3,
    /// two pair 2, a pair of jacks or better 1. About 99.5% back with perfect play: skill matters here.
    /// </summary>
    public static class JacksOrBetter
    {
        public static readonly (VideoPokerHand hand, int pays)[] Paytable =
        {
            (VideoPokerHand.RoyalFlush, 800),
            (VideoPokerHand.StraightFlush, 50),
            (VideoPokerHand.FourOfAKind, 25),
            (VideoPokerHand.FullHouse, 9),
            (VideoPokerHand.Flush, 6),
            (VideoPokerHand.Straight, 4),
            (VideoPokerHand.ThreeOfAKind, 3),
            (VideoPokerHand.TwoPair, 2),
            (VideoPokerHand.JacksOrBetter, 1),
        };

        public static int Pays(VideoPokerHand hand)
        {
            foreach (var (h, p) in Paytable)
                if (h == hand) return p;
            return 0;
        }

        public static VideoPokerHand Evaluate(IReadOnlyList<Card> five)
        {
            int score = PokerHand.Score(five);
            switch (PokerHand.Category(score))
            {
                case HandCategory.RoyalFlush: return VideoPokerHand.RoyalFlush;
                case HandCategory.StraightFlush: return VideoPokerHand.StraightFlush;
                case HandCategory.FourOfAKind: return VideoPokerHand.FourOfAKind;
                case HandCategory.FullHouse: return VideoPokerHand.FullHouse;
                case HandCategory.Flush: return VideoPokerHand.Flush;
                case HandCategory.Straight: return VideoPokerHand.Straight;
                case HandCategory.ThreeOfAKind: return VideoPokerHand.ThreeOfAKind;
                case HandCategory.TwoPair: return VideoPokerHand.TwoPair;
                case HandCategory.Pair:
                    int pair = (score >> 16) & 0xF;   // the pair's rank is the first tie-breaker
                    return pair >= 11 ? VideoPokerHand.JacksOrBetter : VideoPokerHand.Nothing;
                default: return VideoPokerHand.Nothing;
            }
        }

        public static string Name(VideoPokerHand hand)
        {
            switch (hand)
            {
                case VideoPokerHand.RoyalFlush: return PokerHand.Name(HandCategory.RoyalFlush);
                case VideoPokerHand.StraightFlush: return PokerHand.Name(HandCategory.StraightFlush);
                case VideoPokerHand.FourOfAKind: return PokerHand.Name(HandCategory.FourOfAKind);
                case VideoPokerHand.FullHouse: return PokerHand.Name(HandCategory.FullHouse);
                case VideoPokerHand.Flush: return PokerHand.Name(HandCategory.Flush);
                case VideoPokerHand.Straight: return PokerHand.Name(HandCategory.Straight);
                case VideoPokerHand.ThreeOfAKind: return PokerHand.Name(HandCategory.ThreeOfAKind);
                case VideoPokerHand.TwoPair: return PokerHand.Name(HandCategory.TwoPair);
                case VideoPokerHand.JacksOrBetter: return Tr.T("Jacks or better");
                default: return "";
            }
        }
    }

    /// <summary>One hand of video poker: deal, hold, draw.</summary>
    public sealed class VideoPokerRound
    {
        readonly IChipBank bank;
        readonly Shoe deck;
        readonly Card[] hand = new Card[5];
        readonly bool[] held = new bool[5];

        public VideoPokerRound(IRandom rng, IChipBank bank)
        {
            this.bank = bank ?? throw new ArgumentNullException(nameof(bank));
            deck = new Shoe(1, 0.95, rng ?? throw new ArgumentNullException(nameof(rng)));
        }

        public VideoPokerPhase Phase { get; private set; } = VideoPokerPhase.Idle;
        public IReadOnlyList<Card> Hand => hand;
        public IReadOnlyList<bool> Held => held;
        public long Bet { get; private set; }
        public VideoPokerHand Result { get; private set; }
        public long Returned { get; private set; }
        public long Net => Returned - Bet;

        /// <summary>What the cards on show make right now (before the draw, a hint of what you're holding).</summary>
        public VideoPokerHand Current => JacksOrBetter.Evaluate(hand);

        public bool Deal(long bet)
        {
            if (Phase == VideoPokerPhase.Dealt) throw new InvalidOperationException("Draw first.");
            if (bet <= 0) throw new ArgumentOutOfRangeException(nameof(bet));
            if (!bank.TryDebit(bet)) return false;
            Bet = bet;
            Returned = 0;
            Result = VideoPokerHand.Nothing;
            deck.Shuffle();
            for (int i = 0; i < 5; i++)
            {
                hand[i] = deck.Draw();
                held[i] = false;
            }
            Phase = VideoPokerPhase.Dealt;
            return true;
        }

        public void Toggle(int i)
        {
            if (Phase != VideoPokerPhase.Dealt) return;
            held[i] = !held[i];
        }

        public void SetHolds(IReadOnlyList<bool> holds)
        {
            if (Phase != VideoPokerPhase.Dealt) return;
            for (int i = 0; i < 5; i++) held[i] = holds[i];
        }

        /// <summary>Replaces every card not held and pays the final hand.</summary>
        public void Draw()
        {
            if (Phase != VideoPokerPhase.Dealt) throw new InvalidOperationException("Deal first.");
            for (int i = 0; i < 5; i++)
                if (!held[i]) hand[i] = deck.Draw();
            Result = JacksOrBetter.Evaluate(hand);
            Returned = Bet * JacksOrBetter.Pays(Result);
            bank.Credit(Returned);
            Phase = VideoPokerPhase.Complete;
        }
    }

    /// <summary>
    /// The well-known simple strategy for 9/6 Jacks or Better (within a few tenths of a percent of perfect play): take the
    /// first rule that applies.
    /// </summary>
    public static class VideoPokerStrategy
    {
        public static bool[] Hold(IReadOnlyList<Card> hand)
        {
            var made = JacksOrBetter.Evaluate(hand);
            if (made >= VideoPokerHand.FullHouse) return All(true);
            var royal4 = ToStraightFlush(hand, 4, true);
            if (royal4 != null) return royal4;
            if (made >= VideoPokerHand.Straight) return All(true);
            if (made == VideoPokerHand.ThreeOfAKind) return OfAKind(hand, 3);
            var sf4 = ToStraightFlush(hand, 4, false);
            if (sf4 != null) return sf4;
            if (made == VideoPokerHand.TwoPair) return OfAKind(hand, 2);
            if (made == VideoPokerHand.JacksOrBetter) return OfAKind(hand, 2);
            var royal3 = ToStraightFlush(hand, 3, true);
            if (royal3 != null) return royal3;
            var flush4 = ToFlush(hand, 4);
            if (flush4 != null) return flush4;
            var lowPair = OfAKind(hand, 2);
            if (Any(lowPair)) return lowPair;
            var outside = OutsideStraight(hand);
            if (outside != null) return outside;
            var suitedHigh = SuitedHighPair(hand);
            if (suitedHigh != null) return suitedHigh;
            var sf3 = ToStraightFlush(hand, 3, false);
            if (sf3 != null) return sf3;
            var highs = HighCards(hand);
            if (highs.Count >= 2)
            {
                highs.Sort((a, b) => PokerHand.High(hand[a]).CompareTo(PokerHand.High(hand[b])));
                return Pick(highs[0], highs[1]);
            }
            var suitedTen = SuitedTenWithFace(hand);
            if (suitedTen != null) return suitedTen;
            if (highs.Count == 1) return Pick(highs[0]);
            return All(false);
        }

        static bool[] All(bool v) => new[] { v, v, v, v, v };

        static bool Any(bool[] h) => Array.IndexOf(h, true) >= 0;

        static bool[] Pick(params int[] indices)
        {
            var h = All(false);
            foreach (int i in indices) h[i] = true;
            return h;
        }

        /// <summary>Every card that's part of a set of exactly <paramref name="n"/> or more of a rank (n = 2: all pairs).</summary>
        static bool[] OfAKind(IReadOnlyList<Card> hand, int n)
        {
            var count = new int[15];
            foreach (var c in hand) count[PokerHand.High(c)]++;
            var h = All(false);
            for (int i = 0; i < 5; i++) h[i] = count[PokerHand.High(hand[i])] >= n;
            return h;
        }

        static List<int> HighCards(IReadOnlyList<Card> hand)
        {
            var list = new List<int>();
            for (int i = 0; i < 5; i++)
                if (PokerHand.High(hand[i]) >= 11) list.Add(i);
            return list;
        }

        /// <summary><paramref name="n"/> cards of one suit within a span of five ranks (only 10-A when <paramref name="royal"/>).</summary>
        static bool[] ToStraightFlush(IReadOnlyList<Card> hand, int n, bool royal)
        {
            for (int s = 0; s < 4; s++)
            {
                for (int low = royal ? 10 : 1; low <= 10; low++)
                {
                    var h = All(false);
                    int k = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        if ((int)hand[i].Suit != s) continue;
                        int r = PokerHand.High(hand[i]);
                        if ((r >= low && r <= low + 4) || (low == 1 && r == 14))
                        {
                            h[i] = true;
                            k++;
                        }
                    }
                    if (k >= n) return h;
                }
            }
            return null;
        }

        static bool[] ToFlush(IReadOnlyList<Card> hand, int n)
        {
            for (int s = 0; s < 4; s++)
            {
                var h = All(false);
                int k = 0;
                for (int i = 0; i < 5; i++)
                    if ((int)hand[i].Suit == s)
                    {
                        h[i] = true;
                        k++;
                    }
                if (k >= n) return h;
            }
            return null;
        }

        /// <summary>Four ranks in a row that a card at either end completes (2-3-4-5 up to 10-J-Q-K).</summary>
        static bool[] OutsideStraight(IReadOnlyList<Card> hand)
        {
            for (int low = 2; low <= 10; low++)
            {
                var h = All(false);
                var seen = new bool[15];
                for (int i = 0; i < 5; i++)
                {
                    int r = PokerHand.High(hand[i]);
                    if (r >= low && r <= low + 3 && !seen[r])
                    {
                        seen[r] = true;
                        h[i] = true;
                    }
                }
                int k = 0;
                foreach (bool b in h)
                    if (b) k++;
                if (k == 4) return h;
            }
            return null;
        }

        static bool[] SuitedHighPair(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < 5; i++)
                for (int j = i + 1; j < 5; j++)
                    if (hand[i].Suit == hand[j].Suit && PokerHand.High(hand[i]) >= 11 && PokerHand.High(hand[j]) >= 11)
                        return Pick(i, j);
            return null;
        }

        static bool[] SuitedTenWithFace(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    if (i == j || hand[i].Suit != hand[j].Suit) continue;
                    int a = PokerHand.High(hand[i]), b = PokerHand.High(hand[j]);
                    if (a == 10 && b >= 11 && b <= 13) return Pick(i, j);
                }
            return null;
        }
    }
}
