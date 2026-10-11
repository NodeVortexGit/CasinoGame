namespace Casino.Core.Blackjack
{
    public enum HandOutcome { Pending, Win, Blackjack, Push, Lose, Bust }

    public sealed class PlayerHand
    {
        public readonly Hand Hand = new Hand();
        public long Bet;
        public bool Doubled;
        public bool FromSplit;
        public bool SplitAces;
        public bool Finished;
        public HandOutcome Outcome = HandOutcome.Pending;

        /// <summary>Total chips returned to the player for this hand (stake included). Zero for a loss.</summary>
        public long Payout;

        public bool IsNatural => !FromSplit && Hand.IsTwentyOne2Cards;
    }
}
