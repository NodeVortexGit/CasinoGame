using System.Collections.Generic;

namespace Casino.Core.Blackjack
{
    public sealed class Hand
    {
        readonly List<Card> cards = new List<Card>(6);

        public IReadOnlyList<Card> Cards => cards;
        public int Count => cards.Count;

        public void Add(Card card) => cards.Add(card);
        public void Clear() => cards.Clear();

        public Card RemoveLast()
        {
            var c = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            return c;
        }

        public int HardSum
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < cards.Count; i++) sum += cards[i].Value;
                return sum;
            }
        }

        public bool HasAce
        {
            get
            {
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i].IsAce) return true;
                return false;
            }
        }

        public int Total => Evaluate(HardSum, HasAce, out _);
        public bool IsSoft { get { Evaluate(HardSum, HasAce, out bool soft); return soft; } }
        public bool IsBust => Total > 21;

        /// <summary>Two-card 21. Callers decide whether it counts as a natural (split hands do not).</summary>
        public bool IsTwentyOne2Cards => cards.Count == 2 && Total == 21;

        public static int Evaluate(int hardSum, bool hasAce, out bool soft)
        {
            soft = hasAce && hardSum + 10 <= 21;
            return soft ? hardSum + 10 : hardSum;
        }

        /// <summary>Total this hand would have after receiving a card of the given value, without mutating it.</summary>
        public int TotalWith(int value, out bool soft) => Evaluate(HardSum + value, HasAce || value == 1, out soft);

        public override string ToString() => string.Join(" ", cards) + " (" + Total + ")";
    }
}
