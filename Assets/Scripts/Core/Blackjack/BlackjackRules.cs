namespace Casino.Core.Blackjack
{
    /// <summary>Standard Las Vegas Strip style rules. House edge with basic strategy is roughly 0.4-0.6%.</summary>
    public sealed class BlackjackRules
    {
        public int Decks = 6;
        public double Penetration = 0.75;
        public bool DealerHitsSoft17;
        public double BlackjackPayout = 1.5;
        public bool DoubleAfterSplit = true;
        public int MaxHands = 4;
        public bool ResplitAces;
        public bool HitSplitAces;
        public bool OfferInsurance = true;

        public bool DealerMustHit(int total, bool soft) => total < 17 || (total == 17 && soft && DealerHitsSoft17);
    }
}
