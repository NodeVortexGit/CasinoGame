namespace Casino.Core.Blackjack
{
    public enum RoundEventType
    {
        Shuffled,
        DealtToPlayer,
        DealtToDealer,
        HoleRevealed,
        InsuranceOffered,
        InsuranceResolved,
        Split,
        DoubledDown,
        ActiveHandChanged,
        PlayerBust,
        DealerBlackjack,
        DealerBust,
        HandSettled,
        RoundComplete,
    }

    /// <summary>
    /// Everything the round does is recorded as events so the view can replay it with animations at its own pace.
    /// </summary>
    public readonly struct RoundEvent
    {
        public readonly RoundEventType Type;
        public readonly int HandIndex;
        public readonly Card Card;
        public readonly bool FaceDown;
        public readonly HandOutcome Outcome;
        public readonly long Amount;

        public RoundEvent(RoundEventType type, int handIndex = -1, Card card = default, bool faceDown = false,
            HandOutcome outcome = HandOutcome.Pending, long amount = 0)
        {
            Type = type;
            HandIndex = handIndex;
            Card = card;
            FaceDown = faceDown;
            Outcome = outcome;
            Amount = amount;
        }

        public override string ToString() => $"{Type} h{HandIndex} {Card} {(FaceDown ? "down" : "")} {Outcome} {Amount}";
    }
}
