using System;

namespace Casino.Core.Blackjack
{
    public enum Suit : byte { Spades, Hearts, Diamonds, Clubs }

    public enum Rank : byte { Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

    public readonly struct Card : IEquatable<Card>
    {
        public readonly Rank Rank;
        public readonly Suit Suit;

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        /// <summary>Blackjack value with aces counted as 1 (soft totals are handled by <see cref="Hand"/>).</summary>
        public int Value => Rank >= Rank.Ten ? 10 : (int)Rank;

        public bool IsAce => Rank == Rank.Ace;
        public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;
        public bool IsFace => Rank >= Rank.Jack;

        public string RankLabel
        {
            get
            {
                switch (Rank)
                {
                    case Rank.Ace: return "A";
                    case Rank.Jack: return "J";
                    case Rank.Queen: return "Q";
                    case Rank.King: return "K";
                    default: return ((int)Rank).ToString();
                }
            }
        }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
        public override bool Equals(object obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => ((int)Rank << 2) | (int)Suit;
        public override string ToString() => RankLabel + "SHDC"[(int)Suit];
    }
}
