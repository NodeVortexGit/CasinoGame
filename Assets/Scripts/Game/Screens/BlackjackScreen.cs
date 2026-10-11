using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Blackjack;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// The blackjack table. <see cref="BlackjackRound"/> resolves each action instantly and records events; this view
    /// replays them with animations, then enables whichever buttons the round allows next.
    /// </summary>
    public sealed class BlackjackScreen : UIScreen
    {
        public TableDef Table;

        sealed class HandView
        {
            public readonly List<CardView> Cards = new List<CardView>();
            public Text Total;
            public GameObject TotalPill;
            public Image Highlight;
            public ChipStack Bet;
            public float X;
        }

        const float PlayerY = -140f, BetY = -315f, CardFan = 56f;

        // Where cards come from and go to, and the dealer's row (on the table in the casino these follow the real
        // shoe and discard holder as seen from your seat).
        Vector2 shoePos = new Vector2(770, 250), discardPos = new Vector2(-770, 190);
        float dealerY = 108f;
        WorldTable seat;

        /// <summary>
        /// The seat at a casino table: on the middle stool, looking down at the felt and up to the dealer. The cards lie
        /// on the felt about one and a half times real size, so they read well on a phone.
        /// </summary>
        const float SeatPitch = 18f, LayerScale = 0.0015f;

        /// <summary>The dealer's cards lie on the far side of the table; at the table they're drawn larger to read.</summary>
        float dealerCardScale = 1f;

        Vector3 shoeWorld;

        public override bool ShowsWorld => seat != null;

        BlackjackRound round;
        IDrawStrategy luckyDraw;
        readonly List<RoundEvent> events = new List<RoundEvent>();
        readonly List<CardView> dealerCards = new List<CardView>();
        readonly List<HandView> hands = new List<HandView>();

        StaffPortrait dealer;
        SpeechBubble bubble;
        RectTransform table, fx, bettingControls, actionControls, insuranceControls;
        UIButton dealButton, clearButton, rebetButton, hitButton, standButton, doubleButton, splitButton, insuranceYes;
        UIButton[] chipButtons;
        Text dealerTotal, insuranceText, banner;
        GameObject dealerTotalPill;
        ChipStack pendingStack;
        RectTransform pendingSpot;
        long pendingBet, lastBet;
        bool busy;
        Palette.TableTheme theme;
        float idleTimer = 25f;

        protected override void Build()
        {
            Table ??= TableCatalog.Blackjack[0];
            theme = Palette.Tables[Table.Theme];
            var rules = new BlackjackRules();
            round = new BlackjackRound(rules, new Shoe(rules.Decks, rules.Penetration, App.I.Rng), FairDraw.Instance, App.I.Wallet);
            luckyDraw = new BeginnerLuckDraw(() => App.I.Luck.Luck, App.I.Rng);
            lastBet = App.I.Save.lastBet;

            var world = CasinoWorld.Instance;
            if (world != null && world.TryBlackjackTable(Table.Id, out var c, out var dealerFigure))
            {
                Vector3 eye = c + new Vector3(0f, 1.5f, -2.05f);
                Vector3 look = eye + new Vector3(0f, -Mathf.Sin(SeatPitch * Mathf.Deg2Rad), Mathf.Cos(SeatPitch * Mathf.Deg2Rad));
                seat = WorldTable.Sit(Content, eye, look, 58f, 80f, new Vector2(c.x, c.z - 2.65f), 0f,
                    c + new Vector3(0f, 0.857f, -0.7f), Quaternion.Euler(90f, 0f, 0f), LayerScale, dealerFigure);
                if (seat != null)
                {
                    shoeWorld = c + new Vector3(0.72f, 0.97f, -0.3f);
                    shoePos = seat.OnLayer(shoeWorld);
                    discardPos = seat.OnLayer(c + new Vector3(-0.75f, 0.92f, -0.25f));
                    dealerY = 160f;
                    dealerCardScale = 1.3f;
                }
            }

            BuildTable();
            TopBar.Create(Content, Table.Name.ToUpperInvariant(), Icon.Back, Leave);
            var luck = LuckBanner.Create(Content);
            ((RectTransform)luck.transform).At(0f, 1f, 30, -TopBar.Height - 20, 470, 64, 0f, 1f);

            if (seat == null)
            {
                dealer = Staff.Portrait(table, 180, StaffRole.Dealer, Table.Theme);
                dealer.Rect.Center(0, 315, 180, 180);
                dealer.Prewarm();
                bubble = SpeechBubble.Create(table, 500, 120);
                ((RectTransform)bubble.transform).Center(370, 335, 500, 120);
            }

            BuildControls();
            fx = UIKit.NewRect("Fx", Content).Stretch();
            banner = UIKit.Label(fx, "", 130, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display, "Banner");
            ((RectTransform)banner.transform).Center(0, 10, 1600, 200);
            UIKit.TextOutline(banner, Palette.Hex("5A2200"), 4f);
            UIKit.TextShadow(banner, -8f, 0.8f);
            banner.enabled = false;

            App.I.Intensity.Baseline = 0.2f;
            EnterBetting();
            var progress = App.I.Progress;
            progress.StreakExtended += OnStreakExtended;
            bool tease = App.I.Save.handsPlayed > 0 && !progress.Streak.DoneToday(progress.Day) && App.I.Rng.Next(2) == 0;
            Say(App.I.Luck.IsActive && App.I.Save.handsPlayed == 0
                ? "Welcome! Beginner's luck is on your side tonight. Place a bet!"
                : tease ? DealerLines.StreakTease(progress.Streak.Count, false,
                    Mathf.Max(1, progress.Streak.DailyGoal - progress.Streak.RoundsToday(progress.Day)), App.I.Rng)
                : DealerLines.Pick(DealerMoment.Greeting, App.I.Rng), 3.5f);
        }

        // ---------------- layout ----------------

        void BuildTable()
        {
            if (seat != null)
            {
                // The casino's own table is the felt; only the player's betting spot goes on the layer.
                table = seat.Layer;
                BuildBetSpot();
                return;
            }
            var felt = UIKit.Image(Root, "Felt", ProceduralArt.Felt(Table.Theme), Color.white);
            ((RectTransform)felt.transform).Stretch(-40, -40, -40, -40);
            var arc = UIKit.Image(Root, "Arc", ProceduralArt.ThinRing, Palette.WithAlpha(theme.Accent, 0.4f));
            ((RectTransform)arc.transform).Center(0, 1240, 2600, 2600);
            var arc2 = UIKit.Image(Root, "Arc2", ProceduralArt.ThinRing, Palette.WithAlpha(theme.Accent, 0.25f));
            ((RectTransform)arc2.transform).Center(0, 1240, 2900, 2900);
            var vig = UIKit.Image(Root, "Vignette", ProceduralArt.Vignette, Color.white);
            ((RectTransform)vig.transform).Stretch(-60, -60, -60, -60);
            var rail = UIKit.Image(Root, "Rail", ProceduralArt.GradientV, Palette.WithAlpha(Palette.Hex("05030A"), 0.85f));
            var rrt = (RectTransform)rail.transform;
            rrt.anchorMin = new Vector2(0, 1);
            rrt.anchorMax = new Vector2(1, 1);
            rrt.pivot = new Vector2(0.5f, 1);
            rrt.sizeDelta = new Vector2(0, 160);

            table = UIKit.NewRect("Table", Content).Stretch();
            var print = UIKit.Label(table, "BLACKJACK PAYS 3 TO 2   •   DEALER STANDS ON ALL 17s   •   INSURANCE PAYS 2 TO 1",
                26, Palette.WithAlpha(theme.Accent, 0.55f), TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)print.transform).Center(0, -8, 1500, 36);

            for (int i = 0; i < 3; i++)
            {
                var c = CardView.Create(table);
                c.Rect.Center(shoePos.x - i * 4, shoePos.y + i * 4, CardView.Width, CardView.Height);
                c.Rect.localEulerAngles = new Vector3(0, 0, 18);
            }
            var discard = UIKit.Panel(table, "Discard", new Color(0, 0, 0, 0.18f), 16f);
            ((RectTransform)discard.transform).Center(discardPos.x, discardPos.y, CardView.Width + 16, CardView.Height + 16);
            ((RectTransform)discard.transform).localEulerAngles = new Vector3(0, 0, -14);

            BuildBetSpot();
        }

        void BuildBetSpot()
        {
            dealerTotalPill = Pill(table, new Vector2(-330, dealerY), out dealerTotal);
            dealerTotalPill.SetActive(false);

            var spot = UIKit.Image(table, "BetSpot", ProceduralArt.DashedRing, Palette.WithAlpha(theme.Accent, 0.7f));
            pendingSpot = ((RectTransform)spot.transform).Center(0, BetY, 150, 150);
            var spotLabel = UIKit.Label(pendingSpot, "BET", 30, Palette.WithAlpha(theme.Accent, 0.6f), TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)spotLabel.transform).Stretch();
            pendingStack = ChipStack.Create(table, Table.ChipValues, 92f);
            pendingStack.Rect.Center(0, BetY + 8, 92, 92);
        }

        GameObject Pill(Transform parent, Vector2 pos, out Text text)
        {
            var bg = UIKit.Panel(parent, "Total", new Color(0, 0, 0, 0.6f), 26f);
            ((RectTransform)bg.transform).Center(pos.x, pos.y, 100, 56);
            UIKit.Outline(bg.transform, Palette.WithAlpha(Color.white, 0.4f), 26f);
            text = UIKit.Label(bg.transform, "", 40, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)text.transform).Stretch(0, 2, 0, 0);
            return bg.gameObject;
        }

        void BuildControls()
        {
            var bar = UIKit.Image(Content, "ControlsBg", ProceduralArt.GradientV, new Color(0.02f, 0.01f, 0.04f, 0.85f));
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.sizeDelta = new Vector2(0, 170);
            brt.localEulerAngles = new Vector3(0, 0, 180);

            bettingControls = UIKit.NewRect("Betting", Content).At(0.5f, 0f, 0, 82, 1920, 150);
            chipButtons = new UIButton[Table.ChipValues.Length];
            for (int i = 0; i < Table.ChipValues.Length; i++)
            {
                long value = Table.ChipValues[i];
                int chipIndex = i;
                var chip = UIKit.Image(bettingControls, "Chip " + value, ProceduralArt.Chip(i), Color.white, true);
                var crt = ((RectTransform)chip.transform).Center(-700 + i * 155, 0, 142, 142);
                var label = UIKit.Label(crt, ChipFormat.Short(value), value >= 10_000 ? 30 : 36, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)label.transform).Stretch(0, 2, 0, 0);
                UIKit.TextShadow(label, -2f, 0.8f);
                var b = chip.gameObject.AddComponent<UIButtonLite>();
                b.OnClick = () => AddChip(value, chipIndex, crt);
            }
            clearButton = UIKit.Button(bettingControls, "CLEAR", ButtonStyle.Dark, ClearBet, 40);
            clearButton.Rect.Center(150, 0, 220, 130);
            rebetButton = UIKit.Button(bettingControls, "REBET", ButtonStyle.Purple, Rebet, 40);
            rebetButton.Rect.Center(400, 0, 240, 130);
            dealButton = UIKit.Button(bettingControls, "DEAL", ButtonStyle.Gold, Deal, 60);
            dealButton.Rect.Center(705, 0, 310, 136);

            actionControls = UIKit.NewRect("Actions", Content).At(0.5f, 0f, 0, 82, 1920, 150);
            hitButton = UIKit.Button(actionControls, "HIT", ButtonStyle.Green, () => Act(() => round.Hit()), 56);
            hitButton.Rect.Center(-480, 0, 300, 136);
            standButton = UIKit.Button(actionControls, "STAND", ButtonStyle.Red, () => Act(() => round.Stand()), 56);
            standButton.Rect.Center(-160, 0, 300, 136);
            doubleButton = UIKit.Button(actionControls, "DOUBLE", ButtonStyle.Gold, () => Act(DoubleDown), 56);
            doubleButton.Rect.Center(160, 0, 300, 136);
            splitButton = UIKit.Button(actionControls, "SPLIT", ButtonStyle.Blue, () => Act(SplitHand), 56);
            splitButton.Rect.Center(480, 0, 300, 136);

            insuranceControls = UIKit.NewRect("Insurance", Content).At(0.5f, 0f, 0, 82, 1920, 150);
            insuranceText = UIKit.Label(insuranceControls, "", 36, Color.white, TextAnchor.MiddleRight, FontKind.Semi);
            ((RectTransform)insuranceText.transform).Center(-420, 0, 760, 100);
            insuranceYes = UIKit.Button(insuranceControls, "INSURE", ButtonStyle.Gold, () => Act(() => round.ResolveInsurance(true)), 50);
            insuranceYes.Rect.Center(140, 0, 280, 130);
            var no = UIKit.Button(insuranceControls, "NO THANKS", ButtonStyle.Dark, () => Act(() => round.ResolveInsurance(false)), 50);
            no.Rect.Center(460, 0, 320, 130);
        }

        // ---------------- betting ----------------

        void EnterBetting()
        {
            busy = false;
            bettingControls.gameObject.SetActive(true);
            actionControls.gameObject.SetActive(false);
            insuranceControls.gameObject.SetActive(false);
            pendingSpot.gameObject.SetActive(true);
            pendingStack.gameObject.SetActive(true);
            RefreshBetting();
        }

        void RefreshBetting()
        {
            long balance = App.I.Wallet.Balance;
            pendingStack.SetAmount(pendingBet);
            dealButton.Interactable = pendingBet >= Table.MinBet && pendingBet <= balance;
            dealButton.SetLabel(pendingBet > 0 ? "DEAL" : "BET " + ChipFormat.Short(Table.MinBet) + "+");
            clearButton.Interactable = pendingBet > 0;
            long rebet = RebetAmount();
            rebetButton.Interactable = rebet > 0;
            rebetButton.SetLabel(pendingBet > 0 && pendingBet * 2 <= Mathf.Min(balance, Table.MaxBet) ? "x2" : "REBET");
        }

        long RebetAmount()
        {
            long balance = App.I.Wallet.Balance;
            if (pendingBet > 0) return pendingBet * 2 <= System.Math.Min(balance, Table.MaxBet) ? pendingBet * 2 : 0;
            return lastBet >= Table.MinBet && lastBet <= System.Math.Min(balance, Table.MaxBet) ? lastBet : 0;
        }

        void AddChip(long value, int chipIndex, RectTransform from)
        {
            if (busy || round.InProgress) return;
            long cap = System.Math.Min(App.I.Wallet.Balance, Table.MaxBet);
            if (pendingBet + value > cap)
            {
                if (pendingBet < cap) value = cap - pendingBet;
                else
                {
                    Sfx.Play(SfxId.Error);
                    if (App.I.Wallet.Balance < Table.MinBet) OfferChips();
                    else App.I.UI.Toast(pendingBet >= Table.MaxBet ? "Table maximum reached" : "Not enough chips", Palette.Red, Icon.Lock);
                    return;
                }
            }
            pendingBet += value;
            Sfx.Play(SfxId.Chip, 1f, 0.06f);
            StartCoroutine(FlyChip(from.position, OnHud(pendingStack.transform.position), chipIndex));
            RefreshBetting();
        }

        void ClearBet()
        {
            pendingBet = 0;
            Sfx.Play(SfxId.ChipStack);
            RefreshBetting();
        }

        void Rebet()
        {
            long amount = RebetAmount();
            if (amount <= 0) return;
            pendingBet = amount;
            Sfx.Play(SfxId.ChipStack);
            RefreshBetting();
        }

        void Deal()
        {
            if (busy || round.InProgress) return;
            if (App.I.Energy.IsEmpty)
            {
                Say(DealerLines.Pick(DealerMoment.Exhausted, App.I.Rng));
                ExhaustedPrompt.Show();
                return;
            }
            long balanceBefore = App.I.Wallet.Balance;
            long bet = pendingBet;
            if (bet < Table.MinBet || bet > balanceBefore) return;

            round.DrawStrategy = App.I.Luck.IsActive ? luckyDraw : FairDraw.Instance;
            if (!round.Start(bet)) return;
            lastBet = bet;
            App.I.Save.lastBet = bet;
            pendingBet = 0;
            App.I.Intensity.OnBetPlaced(bet, Table.MinBet, Table.MaxBet, balanceBefore);

            if (bet == balanceBefore && balanceBefore >= Table.MinBet * 5) Say(DealerLines.Pick(DealerMoment.AllIn, App.I.Rng));
            else if (bet * 2 >= balanceBefore || bet * 2 >= Table.MaxBet) Say(DealerLines.Pick(DealerMoment.BigBet, App.I.Rng));

            bettingControls.gameObject.SetActive(false);
            pendingSpot.gameObject.SetActive(false);
            pendingStack.gameObject.SetActive(false);
            NewHandView(0);
            hands[0].Bet.SetAmount(bet);
            StartCoroutine(Play());
        }

        // ---------------- actions ----------------

        void Act(System.Action action)
        {
            if (busy) return;
            action();
            StartCoroutine(Play());
        }

        void DoubleDown()
        {
            long before = App.I.Wallet.Balance;
            long add = round.ActiveHand.Bet;
            round.Double();
            App.I.Intensity.OnBetPlaced(add * 2, Table.MinBet, Table.MaxBet, before + add);
            Say(DealerLines.Pick(DealerMoment.Double, App.I.Rng), 2f);
        }

        void SplitHand()
        {
            long before = App.I.Wallet.Balance;
            round.Split();
            App.I.Intensity.OnBetPlaced(round.TotalWagered, Table.MinBet, Table.MaxBet, before + round.TotalWagered / 2);
            Say(DealerLines.Pick(DealerMoment.Split, App.I.Rng), 2f);
        }

        void UpdateActionButtons()
        {
            bool insurance = round.Phase == RoundPhase.Insurance;
            bool turn = round.Phase == RoundPhase.PlayerTurn;
            insuranceControls.gameObject.SetActive(insurance);
            actionControls.gameObject.SetActive(turn);
            if (insurance)
            {
                insuranceText.text = $"Dealer shows an ace. Insure for {ChipFormat.Full(round.InsuranceCost)}?";
                insuranceYes.Interactable = round.CanAffordInsurance;
            }
            if (turn)
            {
                hitButton.Interactable = round.CanHit;
                standButton.Interactable = round.CanStand;
                doubleButton.Interactable = round.CanDouble;
                splitButton.Interactable = round.CanSplit;
            }
        }

        // ---------------- event playback ----------------

        IEnumerator Play()
        {
            busy = true;
            actionControls.gameObject.SetActive(false);
            insuranceControls.gameObject.SetActive(false);
            events.Clear();
            round.DrainEvents(events);
            foreach (var e in events) yield return Animate(e);
            busy = false;
            if (round.Phase == RoundPhase.Complete) yield return FinishRound();
            else UpdateActionButtons();
        }

        IEnumerator Animate(RoundEvent e)
        {
            switch (e.Type)
            {
                case RoundEventType.Shuffled:
                    Say(DealerLines.Pick(DealerMoment.Shuffle, App.I.Rng), 2f);
                    Sfx.Play(SfxId.Shuffle);
                    yield return Tween.Wait(0.75f);
                    break;
                case RoundEventType.DealtToPlayer:
                    yield return DealToPlayer(e.HandIndex, e.Card);
                    break;
                case RoundEventType.DealtToDealer:
                    yield return DealToDealer(e.Card, e.FaceDown);
                    break;
                case RoundEventType.HoleRevealed:
                    if (dealerCards.Count > 1) yield return dealerCards[1].Flip();
                    UpdateDealerTotal();
                    yield return Tween.Wait(0.25f);
                    break;
                case RoundEventType.InsuranceOffered:
                    Say(DealerLines.Pick(DealerMoment.Insurance, App.I.Rng), 3f);
                    break;
                case RoundEventType.InsuranceResolved:
                    App.I.UI.Toast(e.Amount > 0 ? $"Insurance pays {ChipFormat.Full(e.Amount)}" : "No dealer blackjack. Insurance lost.",
                        e.Amount > 0 ? Palette.Green : Palette.TextMuted, Icon.Check);
                    break;
                case RoundEventType.Split:
                    yield return AnimateSplit(e.HandIndex);
                    break;
                case RoundEventType.DoubledDown:
                    hands[e.HandIndex].Bet.SetAmount(e.Amount);
                    Sfx.Play(SfxId.ChipStack);
                    yield return Tween.Wait(0.15f);
                    break;
                case RoundEventType.ActiveHandChanged:
                    SetActiveHand(e.HandIndex);
                    yield return Tween.Wait(0.1f);
                    break;
                case RoundEventType.PlayerBust:
                    Sfx.Play(SfxId.Bust);
                    HandStamp(e.HandIndex, "BUST", Palette.Red);
                    Say(DealerLines.Pick(DealerMoment.PlayerBust, App.I.Rng), 2.2f);
                    yield return Tween.Wait(0.5f);
                    break;
                case RoundEventType.DealerBlackjack:
                    Sfx.Play(SfxId.Lose);
                    Say(DealerLines.Pick(DealerMoment.DealerBlackjack, App.I.Rng));
                    yield return Tween.Wait(0.5f);
                    break;
                case RoundEventType.DealerBust:
                    Say(DealerLines.Pick(DealerMoment.DealerBust, App.I.Rng), 2.2f);
                    StartCoroutine(Stamp(OnFx(new Vector2(0, dealerY)), "DEALER BUST", Palette.Green, 70));
                    yield return Tween.Wait(0.4f);
                    break;
                case RoundEventType.HandSettled:
                    yield return Settle(e);
                    break;
            }
        }

        IEnumerator DealToPlayer(int handIndex, Card card)
        {
            var hv = hands[handIndex];
            DealerSlide(new Vector2(hv.X + hv.Cards.Count * CardFan * 0.5f, PlayerY));
            var view = SpawnCard(card);
            hv.Cards.Add(view);
            yield return LayoutHand(hv, 0.28f, view);
            yield return view.Flip(0.18f);
            UpdateHandTotal(handIndex);
        }

        IEnumerator DealToDealer(Card card, bool faceDown)
        {
            DealerSlide(new Vector2(dealerCards.Count * 48f, dealerY));
            var view = SpawnCard(card);
            view.Rect.localScale = Vector3.one * dealerCardScale;
            dealerCards.Add(view);
            yield return LayoutDealer(0.28f);
            if (!faceDown) yield return view.Flip(0.18f);
            UpdateDealerTotal();
            if (dealerCards.Count > 2) yield return Tween.Wait(0.35f); // let each dealer hit land before the next
        }

        CardView SpawnCard(Card card)
        {
            var view = CardView.Create(table);
            view.Set(card, false);
            view.Rect.Center(shoePos.x, shoePos.y, CardView.Width, CardView.Height);
            view.Rect.localEulerAngles = new Vector3(0, 0, 18);
            Sfx.Play(SfxId.CardDeal, 0.9f, 0.1f);
            return view;
        }

        IEnumerator LayoutHand(HandView hv, float duration, CardView landing = null)
        {
            int n = hv.Cards.Count;
            var routines = new List<IEnumerator>();
            for (int i = 0; i < n; i++)
            {
                var target = new Vector2(hv.X + (i - (n - 1) / 2f) * CardFan, PlayerY + i * 6f);
                routines.Add(Tween.Move(hv.Cards[i].Rect, target, duration));
                routines.Add(Tween.Rotate(hv.Cards[i].Rect, (i - (n - 1) / 2f) * -2f, duration));
                hv.Cards[i].transform.SetAsLastSibling();
            }
            yield return Tween.All(this, routines.ToArray());
        }

        IEnumerator LayoutDealer(float duration)
        {
            int n = dealerCards.Count;
            var routines = new List<IEnumerator>();
            for (int i = 0; i < n; i++)
            {
                routines.Add(Tween.Move(dealerCards[i].Rect, new Vector2((i - (n - 1) / 2f) * 96f * dealerCardScale, dealerY), duration));
                routines.Add(Tween.Rotate(dealerCards[i].Rect, 0f, duration));
            }
            yield return Tween.All(this, routines.ToArray());
        }

        HandView NewHandView(int index)
        {
            var hv = new HandView();
            hv.Highlight = UIKit.Image(table, "Glow", ProceduralArt.Glow, Palette.WithAlpha(Palette.GoldLight, 0f));
            ((RectTransform)hv.Highlight.transform).Center(0, PlayerY, 420, 380);
            hv.Highlight.transform.SetAsFirstSibling();
            hv.Bet = ChipStack.Create(table, Table.ChipValues, 74f);
            hv.TotalPill = Pill(table, Vector2.zero, out hv.Total);
            hv.TotalPill.SetActive(false);
            hands.Insert(index, hv);
            PositionHands(false);
            return hv;
        }

        void PositionHands(bool animate)
        {
            int n = hands.Count;
            for (int i = 0; i < n; i++)
            {
                var hv = hands[i];
                hv.X = (i - (n - 1) / 2f) * (n > 2 ? 420f : 460f);
                ((RectTransform)hv.Highlight.transform).anchoredPosition = new Vector2(hv.X, PlayerY);
                hv.Bet.Rect.anchoredPosition = new Vector2(hv.X, BetY + 10);
                if (animate) StartCoroutine(LayoutHand(hv, 0.3f));
                if (hv.Cards.Count > 0) UpdateHandTotal(i);
            }
        }

        IEnumerator AnimateSplit(int index)
        {
            var source = hands[index];
            long bet = round.Hands[index].Bet;
            var moved = source.Cards[source.Cards.Count - 1];
            source.Cards.RemoveAt(source.Cards.Count - 1);
            var nh = NewHandView(index + 1);
            nh.Cards.Add(moved);
            source.Bet.SetAmount(bet);
            nh.Bet.SetAmount(bet);
            Sfx.Play(SfxId.ChipStack);
            PositionHands(true);
            UpdateHandTotal(index);
            UpdateHandTotal(index + 1);
            yield return Tween.Wait(0.35f);
        }

        void SetActiveHand(int index)
        {
            for (int i = 0; i < hands.Count; i++)
            {
                hands[i].Highlight.color = Palette.WithAlpha(Palette.GoldLight, i == index && hands.Count > 1 ? 0.45f : 0f);
            }
        }

        /// <summary>
        /// Totals come from the cards currently on screen, not the round: the round has already resolved, and reading it
        /// would reveal the dealer's hole card (and hits still in flight) before the animation gets there.
        /// </summary>
        static int VisibleTotal(List<CardView> cards, out bool soft)
        {
            int hard = 0;
            bool ace = false;
            foreach (var c in cards)
            {
                if (!c.FaceUp) continue;
                hard += c.Card.Value;
                ace |= c.Card.IsAce;
            }
            return Hand.Evaluate(hard, ace, out soft);
        }

        static string TotalLabel(int total, bool soft) => soft && total < 21 ? $"{total - 10}/{total}" : total.ToString();

        void UpdateHandTotal(int index)
        {
            var hv = hands[index];
            int total = VisibleTotal(hv.Cards, out bool soft);
            hv.TotalPill.SetActive(hv.Cards.Count > 0);
            hv.Total.text = TotalLabel(total, soft);
            hv.Total.color = total > 21 ? Palette.Red : total == 21 ? Palette.Gold : Color.white;
            var pill = (RectTransform)hv.TotalPill.transform;
            pill.sizeDelta = new Vector2(hv.Total.text.Length > 2 ? 130 : 100, 56);
            float half = CardView.Width / 2f + CardFan / 2f * (hv.Cards.Count - 1);
            pill.anchoredPosition = new Vector2(hv.X - half - pill.sizeDelta.x / 2f - 14f, PlayerY + 50f);
        }

        void UpdateDealerTotal()
        {
            int total = VisibleTotal(dealerCards, out bool soft);
            dealerTotalPill.SetActive(total > 0);
            dealerTotal.text = TotalLabel(total, soft);
            dealerTotal.color = total > 21 ? Palette.Red : Color.white;
            ((RectTransform)dealerTotalPill.transform).anchoredPosition = new Vector2(-(dealerCards.Count - 1) / 2f * 96f * dealerCardScale - 150f * dealerCardScale, dealerY);
        }

        IEnumerator Settle(RoundEvent e)
        {
            var hv = hands[e.HandIndex];
            switch (e.Outcome)
            {
                case HandOutcome.Blackjack:
                    HandStamp(e.HandIndex, "BLACKJACK!", Palette.Gold);
                    break;
                case HandOutcome.Win:
                    HandStamp(e.HandIndex, "WIN", Palette.Green);
                    break;
                case HandOutcome.Push:
                    HandStamp(e.HandIndex, "PUSH", Color.white);
                    break;
                case HandOutcome.Lose:
                    HandStamp(e.HandIndex, "LOSE", Palette.Red);
                    break;
            }
            if (e.Amount > 0)
            {
                if (e.Outcome != HandOutcome.Push)
                {
                    hv.Bet.SetAmount(e.Amount);
                    Sfx.Play(SfxId.ChipStack);
                    yield return Tween.Wait(0.3f);
                }
                StartCoroutine(FlyToBalance(OnHud(hv.Bet.transform.position), e.Outcome == HandOutcome.Push ? 3 : 7));
            }
            else
            {
                StartCoroutine(Tween.Move(hv.Bet.Rect, new Vector2(0, dealerY + 60), 0.45f, Ease.InQuad));
                StartCoroutine(Tween.Fade(UIKit.Group(hv.Bet), 0f, 0.45f));
            }
            yield return Tween.Wait(0.35f);
        }

        IEnumerator FinishRound()
        {
            long net = round.Net;
            bool natural = false;
            int won = 0;
            foreach (var h in round.Hands)
            {
                if (h.Outcome == HandOutcome.Blackjack) natural = true;
                if (h.Outcome == HandOutcome.Win || h.Outcome == HandOutcome.Blackjack) won++;
            }
            bool wasLucky = App.I.Luck.IsActive;
            App.I.RecordHand(round.TotalWagered, net, natural, won);
            App.I.Intensity.OnRoundResult(net, natural);

            if (natural)
            {
                Sfx.Play(SfxId.Blackjack);
                ShowBanner("BLACKJACK!", Palette.Gold);
                Confetti.Burst(fx, Vector2.zero, 80, true);
                Wink(1.6f);
                Say(DealerLines.Pick(DealerMoment.PlayerBlackjack, App.I.Rng));
            }
            else if (net > 0)
            {
                bool big = net >= round.TotalWagered * 2 || net >= Table.MinBet * 20;
                Sfx.Play(big ? SfxId.BigWin : SfxId.Win);
                ShowBanner("+" + ChipFormat.Full(net), Palette.Gold);
                Confetti.Burst(fx, new Vector2(0, -100), big ? 80 : 40, big);
                Wink(0.8f);
                Say(DealerLines.Pick(big ? DealerMoment.PlayerBigWin : DealerMoment.PlayerWin, App.I.Rng), 2.6f);
            }
            else if (net == 0)
            {
                Sfx.Play(SfxId.Push);
                Say(DealerLines.Pick(DealerMoment.Push, App.I.Rng), 2.2f);
            }
            else
            {
                Sfx.Play(SfxId.Lose);
                Say(DealerLines.Pick(DealerMoment.PlayerLose, App.I.Rng), 2.4f);
            }

            if (wasLucky && !App.I.Luck.IsActive)
            {
                App.I.UI.Toast("Beginner's luck is over. From now on it's standard blackjack.", Palette.Hex("7CFC9A"), Icon.Star);
                Say(DealerLines.Pick(DealerMoment.BeginnerLuckOver, App.I.Rng), 3.5f);
            }

            yield return Tween.Wait(1.5f);
            if (streakLine != null)
            {
                Say(streakLine, 3f);
                streakLine = null;
            }
            yield return ClearTable();
            EnterBetting();

            long balance = App.I.Wallet.Balance;
            if (balance < Table.MinBet)
            {
                Say(DealerLines.Pick(DealerMoment.OutOfChips, App.I.Rng), 4f);
                OfferChips();
            }
            else if (balance < Table.MinBet * 5 && App.I.Rng.Next(3) == 0)
            {
                Say(DealerLines.Pick(DealerMoment.LowChips, App.I.Rng), 3f);
            }
            else if (App.I.Energy.IsEmpty)
            {
                Say(DealerLines.Pick(DealerMoment.Exhausted, App.I.Rng), 3f);
            }
        }

        IEnumerator ClearTable()
        {
            var routines = new List<IEnumerator>();
            var all = new List<CardView>(dealerCards);
            foreach (var h in hands) all.AddRange(h.Cards);
            foreach (var c in all)
            {
                routines.Add(Tween.Move(c.Rect, discardPos, 0.35f, Ease.InQuad));
                routines.Add(Tween.Rotate(c.Rect, -14f, 0.35f));
            }
            if (all.Count > 0) Sfx.Play(SfxId.CardDeal, 0.6f);
            dealerTotalPill.SetActive(false);
            foreach (var h in hands)
            {
                h.TotalPill.SetActive(false);
                h.Bet.gameObject.SetActive(false);
                h.Highlight.color = Color.clear;
            }
            yield return Tween.All(this, routines.ToArray());
            foreach (var c in all) Destroy(c.gameObject);
            foreach (var h in hands)
            {
                Destroy(h.Highlight.gameObject);
                Destroy(h.Bet.gameObject);
                Destroy(h.TotalPill);
            }
            dealerCards.Clear();
            hands.Clear();
        }

        // ---------------- feedback helpers ----------------

        void HandStamp(int index, string text, Color color) =>
            StartCoroutine(Stamp(OnFx(new Vector2(hands[index].X, PlayerY)), text, color, hands.Count > 2 ? 54 : 72));

        IEnumerator Stamp(Vector2 pos, string text, Color color, int size)
        {
            var label = UIKit.Label(fx, text, size, color, TextAnchor.MiddleCenter, FontKind.Display, "Stamp");
            var rt = ((RectTransform)label.transform).Center(pos.x, pos.y, 600, 120);
            UIKit.TextOutline(label, new Color(0, 0, 0, 0.8f), 3f);
            UIKit.TextShadow(label, -5f, 0.7f);
            rt.localScale = Vector3.one * 1.8f;
            rt.localEulerAngles = new Vector3(0, 0, -8);
            yield return Tween.Scale(rt, Vector3.one, 0.3f, Ease.OutBack);
            yield return Tween.Wait(1.6f);
            yield return Tween.Fade(UIKit.Group(label), 0f, 0.3f);
            if (rt != null) Destroy(rt.gameObject);
        }

        void ShowBanner(string text, Color color)
        {
            banner.text = text;
            banner.color = color;
            banner.enabled = true;
            StartCoroutine(BannerRoutine());
        }

        IEnumerator BannerRoutine()
        {
            var rt = banner.rectTransform;
            rt.localScale = Vector3.one * 0.3f;
            var g = UIKit.Group(banner);
            g.alpha = 1f;
            yield return Tween.Scale(rt, Vector3.one, 0.45f, Ease.OutElastic);
            yield return Tween.Wait(1.1f);
            yield return Tween.Fade(g, 0f, 0.35f);
            banner.enabled = false;
        }

        IEnumerator FlyChip(Vector3 fromWorld, Vector3 toWorld, int colorIndex)
        {
            var img = UIKit.Image(fx, "FlyingChip", ProceduralArt.Chip(colorIndex), Color.white);
            var rt = (RectTransform)img.transform;
            rt.sizeDelta = new Vector2(80, 80);
            rt.position = fromWorld;
            Vector3 start = rt.position;
            yield return Tween.Run(0.25f, Ease.OutQuad, k =>
            {
                if (rt == null) return;
                Vector3 p = Vector3.Lerp(start, toWorld, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 80f * rt.lossyScale.y;
                rt.position = p;
            });
            if (rt != null) Destroy(rt.gameObject);
        }

        IEnumerator FlyToBalance(Vector3 fromWorld, int count)
        {
            var counter = Content.GetComponentInChildren<ChipCounter>();
            if (counter == null) yield break;
            Vector3 target = counter.transform.position;
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Image(fx, "Coin", ProceduralArt.IconSprite(Icon.Coin), Color.white);
                var rt = (RectTransform)img.transform;
                rt.sizeDelta = new Vector2(56, 56);
                rt.position = fromWorld + new Vector3(Random.Range(-30f, 30f), Random.Range(-20f, 20f), 0) * rt.lossyScale.x;
                StartCoroutine(CoinFlight(rt, target));
                yield return Tween.Wait(0.05f);
            }
        }

        IEnumerator CoinFlight(RectTransform rt, Vector3 target)
        {
            Vector3 start = rt.position;
            Vector3 mid = Vector3.Lerp(start, target, 0.5f) + new Vector3(0, 200f * rt.lossyScale.y, 0);
            yield return Tween.Run(0.55f, Ease.InQuad, k =>
            {
                if (rt == null) return;
                Vector3 a = Vector3.Lerp(start, mid, k), b = Vector3.Lerp(mid, target, k);
                rt.position = Vector3.Lerp(a, b, k);
                rt.localScale = Vector3.one * (1f - 0.4f * k);
            });
            Sfx.Play(SfxId.Chip, 0.35f, 0.15f);
            if (rt != null) Destroy(rt.gameObject);
        }

        void Say(string line, float hold = 3f)
        {
            idleTimer = 25f;
            if (seat != null) seat.Say(line, hold);
            else bubble.Say(line, hold, dealer);
        }

        void Wink(float seconds)
        {
            if (seat != null)
            {
                if (seat.Staff != null) seat.Staff.Wink();
            }
            else dealer.Wink(seconds);
        }

        /// <summary>The dealer's hand slides the card out of the shoe towards where it lands.</summary>
        void DealerSlide(Vector2 to)
        {
            if (seat != null && seat.Staff != null) seat.Staff.SlideOnTable(shoeWorld, table.TransformPoint(to), 0.4f);
        }

        /// <summary>A position on the table as a world position on the HUD (things flying to and from the table).</summary>
        Vector3 OnHud(Vector3 tableWorld) => seat != null ? seat.ToHudWorld(tableWorld) : tableWorld;

        /// <summary>A table layout position as a position in the effects layer (stamps over a hand).</summary>
        Vector2 OnFx(Vector2 tablePos) => seat != null ? seat.ToHudLocal(table.TransformPoint(tablePos), fx) : tablePos;

        /// <summary>Today's streak goal was just met: the dealer says so once this hand's result has been said.</summary>
        string streakLine;

        void OnStreakExtended() => streakLine = DealerLines.StreakExtended(App.I.Progress.Streak.Count, App.I.Rng);

        void OnDestroy()
        {
            if (App.I != null) App.I.Progress.StreakExtended -= OnStreakExtended;
            if (seat == null) return;
            seat.Dispose();
            var next = App.I != null ? App.I.UI.Current : null;
            if (seat.World != null && next != this && (next == null || !next.ShowsWorld)) seat.World.SetVisible(false);
        }

        void OfferChips()
        {
            App.I.UI.Modal<ConfirmModal>(m => m.Setup("OUT OF CHIPS",
                App.I.Purchases.CanWatchAd
                    ? $"Watch a short video for {ChipFormat.Full(GameConfig.AdRewardChips)} free chips, or visit the cashier."
                    : "Visit the cashier to top up your chips.",
                App.I.Purchases.CanWatchAd ? "FREE CHIPS" : "CASHIER",
                () =>
                {
                    if (App.I.Purchases.CanWatchAd) App.I.Purchases.WatchAdForChips(_ => RefreshBetting());
                    else App.I.UI.Modal<ShopModal>(s => s.StartTab = ShopTab.Chips);
                },
                App.I.Purchases.CanWatchAd ? "CASHIER" : "LATER",
                () =>
                {
                    if (App.I.Purchases.CanWatchAd) App.I.UI.Modal<ShopModal>(s => s.StartTab = ShopTab.Chips);
                }, ButtonStyle.Green));
        }

        void Update()
        {
            seat?.Tick();
            if (!busy && !round.InProgress && bettingControls.gameObject.activeSelf)
            {
                dealButton.Interactable = pendingBet >= Table.MinBet && pendingBet <= App.I.Wallet.Balance;
                idleTimer -= Time.unscaledDeltaTime;
                if (idleTimer <= 0f) Say(DealerLines.Pick(pendingBet > 0 ? DealerMoment.Idle : DealerMoment.PlaceBet, App.I.Rng), 2.5f);
            }
        }

        // ---------------- automated tour hooks (DevTour) ----------------

        internal bool TourIdle => !busy && !round.InProgress;
        internal bool TourAwaitingPlayer => !busy && (round.Phase == RoundPhase.PlayerTurn || round.Phase == RoundPhase.Insurance);
        internal void TourBet(int chipIndex) => AddChip(Table.ChipValues[chipIndex], chipIndex, (RectTransform)bettingControls.GetChild(chipIndex));
        internal void TourDeal() => Deal();
        internal void TourLeave() => Leave();

        internal void TourPlayBasic()
        {
            if (round.Phase == RoundPhase.Insurance)
            {
                Act(() => round.ResolveInsurance(false));
                return;
            }
            var h = round.ActiveHand;
            switch (BasicStrategy.Decide(h.Hand, round.DealerUpcard, round.CanDouble, round.CanSplit))
            {
                case PlayerAction.Split when round.CanSplit: Act(SplitHand); break;
                case PlayerAction.Double when round.CanDouble: Act(DoubleDown); break;
                case PlayerAction.Hit when round.CanHit: Act(() => round.Hit()); break;
                default: Act(() => round.Stand()); break;
            }
        }

        // ---------------- leaving ----------------

        void Leave()
        {
            if (busy || round.InProgress)
            {
                App.I.UI.Toast("Finish this hand first.", Palette.TextMuted, Icon.Clock);
                return;
            }
            int attempt = App.I.Persuasion.Attempts;
            App.I.Persuasion.OnLeaveAttempt();
            App.I.UI.Modal<PersuasionModal>(m => m.Setup(Table.Theme, DealerLines.PersuasionLine(attempt), GoToLobby));
        }

        void GoToLobby()
        {
            App.I.Persuasion.Reset();
            App.I.UI.Show<LobbyScreen>();
        }

        public override bool OnBack()
        {
            Leave();
            return true;
        }
    }

    /// <summary>
    /// When you leave a table the dealer pleads for one more hand, more insistently each time, but LEAVE is always right
    /// there and always works: it is also what Back and a controller's first focus do, so nobody gets stuck in a loop.
    /// </summary>
    public sealed class PersuasionModal : UIModal
    {
        int theme;
        string line;
        System.Action leave;

        public void Setup(int tableTheme, string dealerLine, System.Action onLeave)
        {
            theme = tableTheme;
            line = dealerLine;
            leave = onLeave;
        }

        System.Action leaveNow;
        UIButton go;
        internal void TourLeave() => leaveNow?.Invoke();

        public override GameObject DefaultFocus => go != null ? go.gameObject : null;

        public override bool OnBack()
        {
            leaveNow?.Invoke();
            return true;
        }

        protected override void Build()
        {
            MakePanel(1240, 560);
            var p = Staff.Portrait(Panel, 260, StaffRole.Dealer, theme);
            p.Rect.At(0f, 0.5f, 60, 30, 260, 260, 0f, 0.5f);
            p.Wink(10f);
            var t = UIKit.Label(Panel, "“" + line + "”", 46, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)t.transform).Stretch(380, 180, 60, 60);
            var stay = UIKit.Button(Panel, "ONE MORE HAND", ButtonStyle.Gold, Close, 42);
            stay.Rect.At(0.5f, 0f, 160, 80, 400, 100);
            leaveNow = () =>
            {
                Close();
                leave?.Invoke();
            };
            go = UIKit.Button(Panel, "LEAVE", ButtonStyle.Dark, leaveNow, 42);
            go.Rect.At(0.5f, 0f, -230, 80, 300, 100);
        }
    }

    /// <summary>Lightweight tap target for images (the chip buttons) without the full UIButton styling.</summary>
    public sealed class UIButtonLite : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler, UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler
    {
        public System.Action OnClick;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) => OnClick?.Invoke();
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => transform.localScale = Vector3.one * 0.9f;
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => transform.localScale = Vector3.one;
    }
}
