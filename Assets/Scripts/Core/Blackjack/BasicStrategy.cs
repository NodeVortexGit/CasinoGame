namespace Casino.Core.Blackjack
{
    public enum PlayerAction { Hit, Stand, Double, Split }

    /// <summary>
    /// Textbook basic strategy for 4-8 decks, dealer stands on soft 17, double after split allowed.
    /// Used by the simulation tests and available for an optional in-game hint.
    /// </summary>
    public static class BasicStrategy
    {
        public static PlayerAction Decide(Hand hand, Card dealerUpcard, bool canDouble, bool canSplit)
        {
            int up = dealerUpcard.IsAce ? 11 : dealerUpcard.Value;

            if (canSplit && hand.Count == 2 && hand.Cards[0].Value == hand.Cards[1].Value)
            {
                int pair = hand.Cards[0].IsAce ? 11 : hand.Cards[0].Value;
                if (ShouldSplit(pair, up)) return PlayerAction.Split;
            }

            int total = hand.Total;
            if (hand.IsSoft && hand.Count >= 2)
            {
                switch (total)
                {
                    case 13:
                    case 14: return up >= 5 && up <= 6 ? DoubleOr(canDouble, PlayerAction.Hit) : PlayerAction.Hit;
                    case 15:
                    case 16: return up >= 4 && up <= 6 ? DoubleOr(canDouble, PlayerAction.Hit) : PlayerAction.Hit;
                    case 17: return up >= 3 && up <= 6 ? DoubleOr(canDouble, PlayerAction.Hit) : PlayerAction.Hit;
                    case 18:
                        if (up >= 3 && up <= 6) return DoubleOr(canDouble, PlayerAction.Stand);
                        return up <= 8 ? PlayerAction.Stand : PlayerAction.Hit;
                    default: return total >= 19 ? PlayerAction.Stand : PlayerAction.Hit;
                }
            }

            if (total <= 8) return PlayerAction.Hit;
            if (total == 9) return up >= 3 && up <= 6 ? DoubleOr(canDouble, PlayerAction.Hit) : PlayerAction.Hit;
            if (total == 10) return up <= 9 ? DoubleOr(canDouble, PlayerAction.Hit) : PlayerAction.Hit;
            if (total == 11) return up <= 10 ? DoubleOr(canDouble, PlayerAction.Hit) : PlayerAction.Hit;
            if (total == 12) return up >= 4 && up <= 6 ? PlayerAction.Stand : PlayerAction.Hit;
            if (total <= 16) return up <= 6 ? PlayerAction.Stand : PlayerAction.Hit;
            return PlayerAction.Stand;
        }

        static PlayerAction DoubleOr(bool canDouble, PlayerAction fallback) => canDouble ? PlayerAction.Double : fallback;

        static bool ShouldSplit(int pair, int up)
        {
            switch (pair)
            {
                case 11:
                case 8: return true;
                case 2:
                case 3:
                case 7: return up <= 7;
                case 4: return up == 5 || up == 6;
                case 6: return up <= 6;
                case 9: return up <= 9 && up != 7;
                default: return false;
            }
        }
    }
}
