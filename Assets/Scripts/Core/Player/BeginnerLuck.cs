namespace Casino.Core.Player
{
    /// <summary>
    /// The new-player boost: the first <see cref="FullHands"/> hands are fully stacked in the player's favour, then the
    /// boost fades out over <see cref="FadeHands"/> hands to standard fair blackjack. Always shown on screen.
    /// </summary>
    public sealed class BeginnerLuck
    {
        public BeginnerLuck(int fullHands, int fadeHands, int handsPlayed)
        {
            FullHands = fullHands < 0 ? 0 : fullHands;
            FadeHands = fadeHands < 0 ? 0 : fadeHands;
            HandsPlayed = handsPlayed;
        }

        public int FullHands { get; }
        public int FadeHands { get; }
        public int HandsPlayed { get; set; }

        public int TotalHands => FullHands + FadeHands;
        public int HandsRemaining => HandsPlayed >= TotalHands ? 0 : TotalHands - HandsPlayed;
        public bool IsActive => Luck > 0;

        /// <summary>0..1 probability that each card is dealt in the player's favour.</summary>
        public double Luck
        {
            get
            {
                if (HandsPlayed < FullHands) return 1.0;
                if (HandsPlayed >= TotalHands) return 0.0;
                return 1.0 - (HandsPlayed - FullHands + 1) / (double)(FadeHands + 1);
            }
        }
    }
}
