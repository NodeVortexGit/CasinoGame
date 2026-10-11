using System;
using System.Collections.Generic;

namespace Casino.Core.Blackjack
{
    /// <summary>A multi-deck shoe. Cards are only ever reordered, never invented, so the composition is always real.</summary>
    public sealed class Shoe
    {
        readonly List<Card> cards;
        readonly IRandom rng;
        readonly double penetration;
        int next;

        public Shoe(int decks, double penetration, IRandom rng)
        {
            if (decks < 1) throw new ArgumentOutOfRangeException(nameof(decks));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            this.penetration = Math.Max(0.2, Math.Min(0.95, penetration));
            Decks = decks;
            cards = new List<Card>(decks * 52);
            for (int d = 0; d < decks; d++)
                for (int s = 0; s < 4; s++)
                    for (int r = 1; r <= 13; r++)
                        cards.Add(new Card((Rank)r, (Suit)s));
            Shuffle();
        }

        public int Decks { get; }
        public int TotalCards => cards.Count;
        public int Remaining => cards.Count - next;

        /// <summary>True once the cut card has been reached; the round should reshuffle before dealing.</summary>
        public bool NeedsShuffle => next >= (int)(cards.Count * penetration);

        public void Shuffle()
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            next = 0;
        }

        public Card Draw()
        {
            if (next >= cards.Count) Shuffle();
            return cards[next++];
        }

        /// <summary>Number of undealt cards with the given blackjack value (1 = ace, 10 = any ten-value card).</summary>
        public int CountRemaining(int value)
        {
            int n = 0;
            for (int i = next; i < cards.Count; i++)
                if (cards[i].Value == value) n++;
            return n;
        }

        /// <summary>Moves the first undealt card with the given value to the top and deals it. Falls back to the top card.</summary>
        public Card DrawValue(int value)
        {
            if (next >= cards.Count) Shuffle();
            for (int i = next; i < cards.Count; i++)
            {
                if (cards[i].Value != value) continue;
                (cards[i], cards[next]) = (cards[next], cards[i]);
                break;
            }
            return cards[next++];
        }
    }
}
