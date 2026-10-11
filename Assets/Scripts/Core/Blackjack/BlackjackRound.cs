using System;
using System.Collections.Generic;
using Casino.Core.Economy;

namespace Casino.Core.Blackjack
{
    public enum RoundPhase { Idle, Insurance, PlayerTurn, DealerTurn, Complete }

    /// <summary>
    /// One round of blackjack against the dealer: US hole-card rules with dealer peek, insurance, double, split.
    /// All money moves through the <see cref="IChipBank"/> at the moment it is staked or paid.
    /// </summary>
    public sealed class BlackjackRound
    {
        readonly List<PlayerHand> hands = new List<PlayerHand>(4);
        readonly List<RoundEvent> events = new List<RoundEvent>(32);
        readonly IChipBank bank;

        public BlackjackRound(BlackjackRules rules, Shoe shoe, IDrawStrategy drawStrategy, IChipBank bank)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Shoe = shoe ?? throw new ArgumentNullException(nameof(shoe));
            DrawStrategy = drawStrategy ?? FairDraw.Instance;
            this.bank = bank ?? throw new ArgumentNullException(nameof(bank));
        }

        public BlackjackRules Rules { get; }
        public Shoe Shoe { get; }
        public IDrawStrategy DrawStrategy { get; set; }
        public Hand Dealer { get; } = new Hand();
        public IReadOnlyList<PlayerHand> Hands => hands;
        public RoundPhase Phase { get; private set; } = RoundPhase.Idle;
        public int ActiveIndex { get; private set; } = -1;
        public bool HoleRevealed { get; private set; }
        public long InsuranceBet { get; private set; }
        public long TotalWagered { get; private set; }
        public long TotalReturned { get; private set; }
        public long Net => TotalReturned - TotalWagered;

        public bool InProgress => Phase == RoundPhase.Insurance || Phase == RoundPhase.PlayerTurn || Phase == RoundPhase.DealerTurn;
        public PlayerHand ActiveHand => ActiveIndex >= 0 && ActiveIndex < hands.Count ? hands[ActiveIndex] : null;
        public Card DealerUpcard => Dealer.Cards[0];
        public long InsuranceCost => hands.Count > 0 ? hands[0].Bet / 2 : 0;
        public bool CanAffordInsurance => InsuranceCost > 0 && bank.Balance >= InsuranceCost;

        /// <summary>Moves recorded events into <paramref name="into"/> and clears the internal buffer.</summary>
        public void DrainEvents(List<RoundEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        public bool Start(long bet)
        {
            if (InProgress) throw new InvalidOperationException("A round is already in progress.");
            if (bet <= 0) throw new ArgumentOutOfRangeException(nameof(bet));
            if (!bank.TryDebit(bet)) return false;

            hands.Clear();
            Dealer.Clear();
            events.Clear();
            HoleRevealed = false;
            InsuranceBet = 0;
            TotalWagered = bet;
            TotalReturned = 0;
            ActiveIndex = -1;

            if (Shoe.NeedsShuffle)
            {
                Shoe.Shuffle();
                Emit(new RoundEvent(RoundEventType.Shuffled));
            }

            hands.Add(new PlayerHand { Bet = bet });
            DealPlayer(0, DrawTarget.PlayerInitial);
            DealDealer(DrawTarget.DealerUpcard, false);
            DealPlayer(0, DrawTarget.PlayerInitial);
            DealDealer(DrawTarget.DealerHoleCard, true);

            if (Rules.OfferInsurance && DealerUpcard.IsAce && !hands[0].IsNatural)
            {
                Phase = RoundPhase.Insurance;
                Emit(new RoundEvent(RoundEventType.InsuranceOffered, amount: InsuranceCost));
                return true;
            }

            AfterInsurance();
            return true;
        }

        public void ResolveInsurance(bool take)
        {
            if (Phase != RoundPhase.Insurance) throw new InvalidOperationException("Insurance is not on offer.");
            if (take && CanAffordInsurance && bank.TryDebit(InsuranceCost))
            {
                InsuranceBet = InsuranceCost;
                TotalWagered += InsuranceBet;
            }
            AfterInsurance();
        }

        void AfterInsurance()
        {
            var first = hands[0];
            bool dealerCanHaveBlackjack = DealerUpcard.IsAce || DealerUpcard.Value == 10;
            if (dealerCanHaveBlackjack && Dealer.IsTwentyOne2Cards)
            {
                RevealHole();
                Emit(new RoundEvent(RoundEventType.DealerBlackjack));
                if (InsuranceBet > 0)
                {
                    long insurancePayout = InsuranceBet * 3;
                    bank.Credit(insurancePayout);
                    TotalReturned += insurancePayout;
                    Emit(new RoundEvent(RoundEventType.InsuranceResolved, amount: insurancePayout));
                }
                first.Finished = true;
                SettleHand(0, first.IsNatural ? HandOutcome.Push : HandOutcome.Lose);
                Complete();
                return;
            }

            if (InsuranceBet > 0) Emit(new RoundEvent(RoundEventType.InsuranceResolved, amount: 0));

            if (first.IsNatural)
            {
                first.Finished = true;
                RevealHole();
                SettleHand(0, HandOutcome.Blackjack);
                Complete();
                return;
            }

            Phase = RoundPhase.PlayerTurn;
            ActiveIndex = 0;
            Emit(new RoundEvent(RoundEventType.ActiveHandChanged, 0));
        }

        bool PlayerCanAct => Phase == RoundPhase.PlayerTurn && ActiveHand != null && !ActiveHand.Finished;

        public bool CanStand => PlayerCanAct;

        public bool CanHit => PlayerCanAct && ActiveHand.Hand.Total < 21 && !(ActiveHand.SplitAces && !Rules.HitSplitAces);

        public bool CanDouble => PlayerCanAct
            && ActiveHand.Hand.Count == 2
            && (!ActiveHand.FromSplit || Rules.DoubleAfterSplit)
            && !ActiveHand.SplitAces
            && bank.Balance >= ActiveHand.Bet;

        public bool CanSplit => PlayerCanAct && IsSplittablePair(ActiveHand) && bank.Balance >= ActiveHand.Bet;

        bool IsSplittablePair(PlayerHand h) =>
            h.Hand.Count == 2
            && h.Hand.Cards[0].Value == h.Hand.Cards[1].Value
            && hands.Count < Rules.MaxHands
            && (!h.SplitAces || Rules.ResplitAces);

        public void Hit()
        {
            if (!CanHit) throw new InvalidOperationException("Cannot hit now.");
            var h = ActiveHand;
            DealPlayer(ActiveIndex, DrawTarget.PlayerHit);
            if (h.Hand.IsBust)
            {
                MarkBust(ActiveIndex);
                Advance();
            }
            else if (h.Hand.Total == 21)
            {
                h.Finished = true;
                Advance();
            }
        }

        public void Stand()
        {
            if (!CanStand) throw new InvalidOperationException("Cannot stand now.");
            ActiveHand.Finished = true;
            Advance();
        }

        public void Double()
        {
            if (!CanDouble) throw new InvalidOperationException("Cannot double now.");
            var h = ActiveHand;
            if (!bank.TryDebit(h.Bet)) throw new InvalidOperationException("Insufficient chips to double.");
            TotalWagered += h.Bet;
            h.Bet *= 2;
            h.Doubled = true;
            Emit(new RoundEvent(RoundEventType.DoubledDown, ActiveIndex, amount: h.Bet));
            DealPlayer(ActiveIndex, DrawTarget.PlayerHit);
            if (h.Hand.IsBust) MarkBust(ActiveIndex);
            h.Finished = true;
            Advance();
        }

        public void Split()
        {
            if (!CanSplit) throw new InvalidOperationException("Cannot split now.");
            var h = ActiveHand;
            if (!bank.TryDebit(h.Bet)) throw new InvalidOperationException("Insufficient chips to split.");
            TotalWagered += h.Bet;

            var moved = h.Hand.RemoveLast();
            bool aces = moved.IsAce;
            h.FromSplit = true;
            h.SplitAces = aces;
            var newHand = new PlayerHand { Bet = h.Bet, FromSplit = true, SplitAces = aces };
            newHand.Hand.Add(moved);
            hands.Insert(ActiveIndex + 1, newHand);
            Emit(new RoundEvent(RoundEventType.Split, ActiveIndex, moved, amount: h.Bet));

            DealPlayer(ActiveIndex, DrawTarget.PlayerHit);
            if (AutoFinishes(h))
            {
                h.Finished = true;
                Advance();
            }
        }

        bool AutoFinishes(PlayerHand h)
        {
            if (h.Hand.Total == 21) return true;
            return h.SplitAces && !Rules.HitSplitAces && !IsSplittablePair(h);
        }

        void MarkBust(int index)
        {
            var h = hands[index];
            h.Outcome = HandOutcome.Bust;
            h.Finished = true;
            Emit(new RoundEvent(RoundEventType.PlayerBust, index));
        }

        void Advance()
        {
            for (int i = ActiveIndex + 1; i < hands.Count; i++)
            {
                var h = hands[i];
                ActiveIndex = i;
                Emit(new RoundEvent(RoundEventType.ActiveHandChanged, i));
                if (h.Hand.Count == 1) DealPlayer(i, DrawTarget.PlayerHit);
                if (AutoFinishes(h))
                {
                    h.Finished = true;
                    continue;
                }
                return;
            }
            PlayDealer();
        }

        void PlayDealer()
        {
            Phase = RoundPhase.DealerTurn;
            ActiveIndex = -1;
            RevealHole();

            bool anyLive = false;
            foreach (var h in hands)
                if (h.Outcome != HandOutcome.Bust) anyLive = true;

            if (anyLive)
            {
                while (Rules.DealerMustHit(Dealer.Total, Dealer.IsSoft))
                    DealDealer(DrawTarget.DealerHit, false);
                if (Dealer.IsBust) Emit(new RoundEvent(RoundEventType.DealerBust));
            }

            int dealerTotal = Dealer.Total;
            for (int i = 0; i < hands.Count; i++)
            {
                var h = hands[i];
                HandOutcome outcome;
                if (h.Outcome == HandOutcome.Bust) outcome = HandOutcome.Bust;
                else if (Dealer.IsBust || h.Hand.Total > dealerTotal) outcome = HandOutcome.Win;
                else if (h.Hand.Total == dealerTotal) outcome = HandOutcome.Push;
                else outcome = HandOutcome.Lose;
                SettleHand(i, outcome);
            }
            Complete();
        }

        void SettleHand(int index, HandOutcome outcome)
        {
            var h = hands[index];
            h.Outcome = outcome;
            switch (outcome)
            {
                case HandOutcome.Blackjack: h.Payout = h.Bet + (long)Math.Floor(h.Bet * Rules.BlackjackPayout); break;
                case HandOutcome.Win: h.Payout = h.Bet * 2; break;
                case HandOutcome.Push: h.Payout = h.Bet; break;
                default: h.Payout = 0; break;
            }
            if (h.Payout > 0) bank.Credit(h.Payout);
            TotalReturned += h.Payout;
            Emit(new RoundEvent(RoundEventType.HandSettled, index, outcome: outcome, amount: h.Payout));
        }

        void Complete()
        {
            Phase = RoundPhase.Complete;
            ActiveIndex = -1;
            Emit(new RoundEvent(RoundEventType.RoundComplete, amount: Net));
        }

        void RevealHole()
        {
            if (HoleRevealed) return;
            HoleRevealed = true;
            Emit(new RoundEvent(RoundEventType.HoleRevealed, card: Dealer.Cards[1]));
        }

        void DealPlayer(int index, DrawTarget target)
        {
            var card = DrawStrategy.Draw(Shoe, target, this, index);
            hands[index].Hand.Add(card);
            Emit(new RoundEvent(RoundEventType.DealtToPlayer, index, card));
        }

        void DealDealer(DrawTarget target, bool faceDown)
        {
            var card = DrawStrategy.Draw(Shoe, target, this, -1);
            Dealer.Add(card);
            Emit(new RoundEvent(RoundEventType.DealtToDealer, card: card, faceDown: faceDown));
        }

        void Emit(RoundEvent e) => events.Add(e);
    }
}
