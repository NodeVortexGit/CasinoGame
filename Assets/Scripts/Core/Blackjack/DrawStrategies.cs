using System;

namespace Casino.Core.Blackjack
{
    public enum DrawTarget { PlayerInitial, DealerUpcard, DealerHoleCard, PlayerHit, DealerHit }

    public interface IDrawStrategy
    {
        Card Draw(Shoe shoe, DrawTarget target, BlackjackRound round, int handIndex);
    }

    /// <summary>Plain top-of-shoe dealing. This is the normal game.</summary>
    public sealed class FairDraw : IDrawStrategy
    {
        public static readonly FairDraw Instance = new FairDraw();
        public Card Draw(Shoe shoe, DrawTarget target, BlackjackRound round, int handIndex) => shoe.Draw();
    }

    /// <summary>
    /// The visible "Beginner's Luck" boost. With probability <c>luck</c> each card is picked from the remaining shoe
    /// in the player's favour; otherwise it is dealt normally. It can only ever help the player, and luck 0 is
    /// exactly <see cref="FairDraw"/>. The UI must show the boost while it is active.
    /// </summary>
    public sealed class BeginnerLuckDraw : IDrawStrategy
    {
        static readonly int[] StrongFirstCard = { 10, 10, 10, 1, 9 };
        static readonly int[] WeakDealerUpcard = { 4, 5, 6, 5, 6 };

        readonly Func<double> luck;
        readonly IRandom rng;
        readonly int[] scratch = new int[10];

        public BeginnerLuckDraw(Func<double> luck, IRandom rng)
        {
            this.luck = luck ?? throw new ArgumentNullException(nameof(luck));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public Card Draw(Shoe shoe, DrawTarget target, BlackjackRound round, int handIndex)
        {
            double p = luck();
            if (p <= 0 || rng.NextDouble() >= p) return shoe.Draw();
            int value = Choose(shoe, target, round, handIndex);
            return value > 0 ? shoe.DrawValue(value) : shoe.Draw();
        }

        int Choose(Shoe shoe, DrawTarget target, BlackjackRound round, int handIndex)
        {
            switch (target)
            {
                case DrawTarget.PlayerInitial:
                {
                    var hand = round.Hands[handIndex].Hand;
                    if (hand.Count == 0) return PickAvailable(shoe, StrongFirstCard);
                    int n = 0;
                    for (int v = 1; v <= 10; v++)
                    {
                        int total = hand.TotalWith(v, out _);
                        if (total >= 19 && total <= 21 && shoe.CountRemaining(v) > 0) scratch[n++] = v;
                    }
                    return n > 0 ? scratch[rng.Next(n)] : BestPlayerValue(shoe, hand);
                }
                case DrawTarget.DealerUpcard:
                    return PickAvailable(shoe, WeakDealerUpcard);
                case DrawTarget.DealerHoleCard:
                {
                    // Leave the dealer on a stiff hard 12-16 so they must draw and are likely to bust.
                    var dealer = round.Dealer;
                    for (int v = 10; v >= 2; v--)
                    {
                        int total = dealer.TotalWith(v, out bool soft);
                        if (!soft && total >= 12 && total <= 16 && shoe.CountRemaining(v) > 0) return v;
                    }
                    return 0;
                }
                case DrawTarget.PlayerHit:
                    return BestPlayerValue(shoe, round.Hands[handIndex].Hand);
                case DrawTarget.DealerHit:
                    return WorstDealerValue(shoe, round);
                default:
                    return 0;
            }
        }

        int PickAvailable(Shoe shoe, int[] preferred)
        {
            int n = 0;
            foreach (int v in preferred)
                if (shoe.CountRemaining(v) > 0) scratch[n++] = v;
            return n > 0 ? scratch[rng.Next(n)] : 0;
        }

        static int BestPlayerValue(Shoe shoe, Hand hand)
        {
            int best = 0, bestTotal = -1;
            for (int v = 1; v <= 10; v++)
            {
                if (shoe.CountRemaining(v) == 0) continue;
                int total = hand.TotalWith(v, out _);
                if (total <= 21 && total > bestTotal)
                {
                    bestTotal = total;
                    best = v;
                }
            }
            return best;
        }

        int WorstDealerValue(Shoe shoe, BlackjackRound round)
        {
            var dealer = round.Dealer;
            int n = 0;
            for (int v = 1; v <= 10; v++)
                if (shoe.CountRemaining(v) > 0 && dealer.TotalWith(v, out _) > 21) scratch[n++] = v;
            if (n > 0) return scratch[rng.Next(n)];

            // No busting card: keep the dealer drawing from the highest stiff total.
            int keepDrawing = 0, keepTotal = -1;
            for (int v = 1; v <= 10; v++)
            {
                if (shoe.CountRemaining(v) == 0) continue;
                int total = dealer.TotalWith(v, out bool soft);
                if (round.Rules.DealerMustHit(total, soft) && total > keepTotal)
                {
                    keepTotal = total;
                    keepDrawing = v;
                }
            }
            if (keepDrawing > 0) return keepDrawing;

            // The dealer will stand whatever comes: stand them as low as possible.
            int lowest = 0, lowestTotal = int.MaxValue;
            for (int v = 1; v <= 10; v++)
            {
                if (shoe.CountRemaining(v) == 0) continue;
                int total = dealer.TotalWith(v, out _);
                if (total < lowestTotal)
                {
                    lowestTotal = total;
                    lowest = v;
                }
            }
            return lowest;
        }
    }
}
