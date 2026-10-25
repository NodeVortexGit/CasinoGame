using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Blackjack;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.Core.Poker;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// Ultimate Texas Hold'em against the dealer (<see cref="HoldemRound"/>): Ante and Blind, then one raise of 4x or 3x
    /// before the flop, 2x after it, or 1x at the river. The dealer qualifies with a pair; the Blind pays for a straight or
    /// better. Played at the hold'em table in the casino, or on a flat felt while the casino is still being built.
    /// </summary>
    public sealed class HoldemScreen : UIScreen
    {
        public const int DealerTheme = 6;
        public static readonly TableDef Table = TableCatalog.Holdem;

        // Layout on the felt (layout units; +y is away from you).
        // Nine cards share the felt, so they're dealt a little smaller than at blackjack and kept clear of the chip tray.
        const float DealerY = 220f, BoardY = 40f, PlayerY = -150f, BetY = -335f, BoardGap = 132f, PairGap = 126f, CardScale = 0.82f;
        const float SeatPitch = 24f, LayerScale = 0.0014f;
        static readonly float[] BetX = { -250f, -80f, 190f };   // ante, blind, play

        HoldemRound round;
        WorldTable seat;
        RectTransform table, fx, bettingControls, actionControls;
        Vector2 deckPos = new Vector2(760, 260);
        Vector3 deckWorld;
        readonly List<CardView> playerCards = new List<CardView>(), dealerCards = new List<CardView>(), boardCards = new List<CardView>();
        readonly ChipStack[] stacks = new ChipStack[3];
        readonly RectTransform[] circles = new RectTransform[3];
        UIButton dealButton, clearButton, rebetButton, checkButton, foldButton, raiseHigh, raiseLow;
        Text playerHand, dealerHand, hint, banner;
        long pendingAnte, lastAnte;
        bool busy;
        StaffPortrait dealer;
        SpeechBubble bubble;
        float idleTimer = 25f;

        public override bool ShowsWorld => seat != null;

        protected override void Build()
        {
            round = new HoldemRound(App.I.Rng, App.I.Wallet);
            lastAnte = App.I.Save.lastHoldemAnte;

            var world = CasinoWorld.Instance;
            if (world != null && world.TryHoldemTable(out var c, out var dealerFigure))
            {
                Vector3 eye = c + new Vector3(0f, 1.55f, -2.0f);
                Vector3 look = eye + new Vector3(0f, -Mathf.Sin(SeatPitch * Mathf.Deg2Rad), Mathf.Cos(SeatPitch * Mathf.Deg2Rad));
                seat = WorldTable.Sit(Content, eye, look, 60f, 80f, new Vector2(c.x, c.z - 2.65f), 0f,
                    c + new Vector3(0f, 0.857f, -0.66f), Quaternion.Euler(90f, 0f, 0f), LayerScale, dealerFigure);
                if (seat != null)
                {
                    deckWorld = c + new Vector3(0.72f, 0.93f, -0.3f);
                    deckPos = seat.OnLayer(deckWorld);
                }
            }

            BuildTable();
            TopBar.Create(Content, Loc.T("HOLD'EM"), Icon.Back, Leave);
            BuildPaytable();
            BuildControls();
            fx = UIKit.NewRect("Fx", Content).Stretch();
            banner = UIKit.Label(fx, "", 120, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display, "Banner");
            ((RectTransform)banner.transform).Center(0, 10, 1600, 200);
            UIKit.TextOutline(banner, Palette.Hex("5A2200"), 4f);
            UIKit.TextShadow(banner, -8f, 0.8f);
            banner.enabled = false;

            App.I.Progress.StreakExtended += OnStreakExtended;
            App.I.Intensity.Baseline = 0.2f;
            EnterBetting();
            Say(Loc.T("Welcome to Hold'em! Ante up and let's see some cards."), 3.5f);
        }

        // ---------------- layout ----------------

        void BuildTable()
        {
            if (seat != null) table = seat.Layer;
            else
            {
                var felt = UIKit.Image(Root, "Felt", ProceduralArt.Felt(1), Color.white);
                ((RectTransform)felt.transform).Stretch(-40, -40, -40, -40);
                var vig = UIKit.Image(Root, "Vignette", ProceduralArt.Vignette, Color.white);
                ((RectTransform)vig.transform).Stretch(-60, -60, -60, -60);
                table = UIKit.NewRect("Table", Content).Stretch();
                for (int i = 0; i < 3; i++)
                {
                    var d = CardView.Create(table);
                    d.Rect.Center(deckPos.x - i * 4, deckPos.y + i * 4, CardView.Width, CardView.Height);
                    d.Rect.localEulerAngles = new Vector3(0, 0, 18);
                }
                dealer = Staff.Portrait(Content, 160, StaffRole.Dealer, DealerTheme);
                dealer.Rect.Center(-760, 300, 160, 160);
                dealer.Prewarm();
                bubble = SpeechBubble.Create(Content, 500, 120);
                ((RectTransform)bubble.transform).Center(-430, 330, 500, 120);
            }

            string[] names = { Loc.T("ANTE"), Loc.T("BLIND"), Loc.T("PLAY") };
            for (int i = 0; i < 3; i++)
            {
                var ring = UIKit.Image(table, names[i], ProceduralArt.DashedRing, Palette.WithAlpha(Palette.GoldLight, 0.7f));
                circles[i] = ((RectTransform)ring.transform).Center(BetX[i], BetY, 140, 140);
                var label = UIKit.Label(circles[i], names[i], 26, Palette.WithAlpha(Palette.GoldLight, 0.75f), TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)label.transform).Stretch();
                stacks[i] = ChipStack.Create(table, Table.ChipValues, 84f);
                stacks[i].Rect.Center(BetX[i], BetY + 8, 84, 84);
            }
            var equals = UIKit.Label(table, "=", 54, Palette.WithAlpha(Palette.GoldLight, 0.6f), TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)equals.transform).Center((BetX[0] + BetX[1]) * 0.5f, BetY, 40, 60);

            playerHand = HandLabel(new Vector2(PairGap + 200f, PlayerY));
            dealerHand = HandLabel(new Vector2(-PairGap - 200f, DealerY));
        }

        Text HandLabel(Vector2 at)
        {
            var bg = UIKit.Panel(table, "HandName", new Color(0, 0, 0, 0.62f), 24f);
            ((RectTransform)bg.transform).Center(at.x, at.y, 300, 56);
            UIKit.Outline(bg.transform, Palette.WithAlpha(Color.white, 0.35f), 24f);
            var t = UIKit.Label(bg.transform, "", 30, Color.white, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)t.transform).Stretch(8, 2, 8, 0);
            bg.gameObject.SetActive(false);
            return t;
        }

        static void ShowHand(Text label, string text, Color color)
        {
            label.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
            label.text = text;
            label.color = color;
        }

        /// <summary>What the Blind pays, always on screen (top left).</summary>
        void BuildPaytable()
        {
            var panel = UIKit.Panel(Content, "Paytable", new Color(0.03f, 0.02f, 0.07f, 0.78f), 24f);
            var rt = ((RectTransform)panel.transform).At(0f, 1f, 30, -TopBar.Height - 16, 440, 330, 0f, 1f);
            UIKit.Outline(rt, Palette.WithAlpha(Palette.Gold, 0.6f), 24f);
            var title = UIKit.Label(rt, Loc.T("BLIND PAYS"), 32, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)title.transform).At(0.5f, 1f, 0, -30, 400, 44);
            for (int i = 0; i < HoldemRound.BlindPays.Length; i++)
            {
                var (hand, num, den) = HoldemRound.BlindPays[i];
                float y = -76 - i * 36;
                var name = UIKit.Label(rt, PokerHand.Name(hand), 25, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
                ((RectTransform)name.transform).At(0f, 1f, 22, y, 290, 34, 0f, 0.5f);
                var pays = UIKit.Label(rt, num + ":" + den, 25, Palette.GoldLight, TextAnchor.MiddleRight, FontKind.Semi);
                ((RectTransform)pays.transform).At(1f, 1f, -22, y, 120, 34, 1f, 0.5f);
            }
            var q = UIKit.Label(rt, Loc.T("Dealer qualifies with a pair or better"), 21, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Body);
            ((RectTransform)q.transform).At(0.5f, 0f, 0, 26, 410, 34);
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
            for (int i = 0; i < Table.ChipValues.Length; i++)
            {
                long value = Table.ChipValues[i];
                int chipIndex = i;
                var chip = UIKit.Image(bettingControls, "Chip " + value, ProceduralArt.Chip(i), Color.white, true);
                var crt = ((RectTransform)chip.transform).Center(-700 + i * 155, 0, 142, 142);
                var label = UIKit.Label(crt, ChipFormat.Short(value), value >= 10_000 ? 30 : 36, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)label.transform).Stretch(0, 2, 0, 0);
                UIKit.TextShadow(label, -2f, 0.8f);
                chip.gameObject.AddComponent<UIButtonLite>().OnClick = () => AddChip(value, chipIndex, crt);
            }
            clearButton = UIKit.Button(bettingControls, Loc.T("CLEAR"), ButtonStyle.Dark, ClearBet, 40);
            clearButton.Rect.Center(150, 0, 220, 130);
            rebetButton = UIKit.Button(bettingControls, Loc.T("REBET"), ButtonStyle.Purple, Rebet, 40);
            rebetButton.Rect.Center(400, 0, 240, 130);
            dealButton = UIKit.Button(bettingControls, Loc.T("DEAL"), ButtonStyle.Gold, Deal, 60);
            dealButton.Rect.Center(705, 0, 310, 136);

            actionControls = UIKit.NewRect("Actions", Content).At(0.5f, 0f, 0, 82, 1920, 150);
            checkButton = UIKit.Button(actionControls, Loc.T("CHECK"), ButtonStyle.Blue, () => Act(Check), 54);
            foldButton = UIKit.Button(actionControls, Loc.T("FOLD"), ButtonStyle.Red, () => Act(Fold), 54);
            raiseLow = UIKit.Button(actionControls, Loc.F("BET {0}×", 3), ButtonStyle.Gold, () => Act(() => Raise(LowRaise)), 54);
            raiseHigh = UIKit.Button(actionControls, Loc.F("BET {0}×", 4), ButtonStyle.Gold, () => Act(() => Raise(4)), 54);

            hint = UIKit.Label(Content, "", 30, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)hint.transform).At(0.5f, 0f, 0, 205, 1500, 44);
            UIKit.TextShadow(hint, -2f, 0.9f);
        }

        int LowRaise => round.Phase == HoldemPhase.Preflop ? 3 : round.Phase == HoldemPhase.Flop ? 2 : 1;

        // ---------------- betting ----------------

        void EnterBetting()
        {
            busy = false;
            bettingControls.gameObject.SetActive(true);
            actionControls.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++) circles[i].gameObject.SetActive(true);
            ShowHand(playerHand, null, Color.white);
            ShowHand(dealerHand, null, Color.white);
            RefreshBetting();
        }

        long MaxAnte => System.Math.Min(Table.MaxBet, App.I.Wallet.Balance / 6);   // room for the ante, blind and a 4x raise

        void RefreshBetting()
        {
            stacks[0].SetAmount(pendingAnte);
            stacks[1].SetAmount(pendingAnte);
            stacks[2].SetAmount(0);
            dealButton.Interactable = pendingAnte >= Table.MinBet && pendingAnte <= MaxAnte;
            dealButton.SetLabel(pendingAnte > 0 ? Loc.T("DEAL") : Loc.F("BET {0}+", ChipFormat.Short(Table.MinBet)));
            clearButton.Interactable = pendingAnte > 0;
            rebetButton.Interactable = RebetAmount() > 0;
            rebetButton.SetLabel(pendingAnte > 0 ? "x2" : Loc.T("REBET"));
            hint.text = pendingAnte > 0
                ? Loc.F("Ante {0} + Blind {0}. Keep {1} more for your raise.", ChipFormat.Full(pendingAnte), ChipFormat.Full(pendingAnte * 4))
                : Loc.T("Tap chips to set your ante. The blind matches it.");
        }

        long RebetAmount()
        {
            if (pendingAnte > 0) return pendingAnte * 2 <= MaxAnte ? pendingAnte * 2 : 0;
            return lastAnte >= Table.MinBet && lastAnte <= MaxAnte ? lastAnte : 0;
        }

        void AddChip(long value, int chipIndex, RectTransform from)
        {
            if (busy || round.InProgress) return;
            long cap = MaxAnte;
            if (pendingAnte + value > cap)
            {
                if (pendingAnte < cap) value = cap - pendingAnte;
                else
                {
                    Sfx.Play(SfxId.Error);
                    if (App.I.Wallet.Balance < Table.MinBet * 6) OfferChips();
                    else App.I.UI.Toast(pendingAnte >= Table.MaxBet ? Loc.T("Table maximum reached") : Loc.T("Not enough chips"), Palette.Red, Icon.Lock);
                    return;
                }
            }
            pendingAnte += value;
            Sfx.Play(SfxId.Chip, 1f, 0.06f);
            StartCoroutine(FlyChip(from.position, OnHud(stacks[0].transform.position), chipIndex));
            RefreshBetting();
        }

        void ClearBet()
        {
            pendingAnte = 0;
            Sfx.Play(SfxId.ChipStack);
            RefreshBetting();
        }

        void Rebet()
        {
            long amount = RebetAmount();
            if (amount <= 0) return;
            pendingAnte = amount;
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
            long before = App.I.Wallet.Balance;
            long ante = pendingAnte;
            if (ante < Table.MinBet || ante > MaxAnte) return;
            if (!round.Start(ante)) return;
            lastAnte = ante;
            App.I.Save.lastHoldemAnte = ante;
            pendingAnte = 0;
            App.I.Intensity.OnBetPlaced(ante * 2, Table.MinBet * 2, Table.MaxBet * 2, before);
            bettingControls.gameObject.SetActive(false);
            hint.text = "";
            StartCoroutine(DealRoutine());
        }

        IEnumerator DealRoutine()
        {
            busy = true;
            stacks[0].SetAmount(round.Ante);
            stacks[1].SetAmount(round.Blind);
            for (int i = 0; i < 2; i++)
            {
                yield return DealCard(playerCards, round.PlayerCards[i], new Vector2((i - 0.5f) * PairGap, PlayerY), true);
                yield return DealCard(dealerCards, round.DealerCards[i], new Vector2((i - 0.5f) * PairGap, DealerY), false);
            }
            for (int i = 0; i < 5; i++)
                yield return DealCard(boardCards, round.Board[i], new Vector2((i - 2) * BoardGap, BoardY), false, 0.16f);
            UpdatePlayerHand();
            busy = false;
            ShowActions();
            Say(Loc.T("Check, or bet three or four times your ante?"), 3f);
        }

        IEnumerator DealCard(List<CardView> into, Card card, Vector2 at, bool faceUp, float duration = 0.24f)
        {
            if (seat != null && seat.Staff != null) seat.Staff.SlideOnTable(deckWorld, table.TransformPoint(at), 0.35f);
            var view = CardView.Create(table);
            view.Set(card, false);
            view.Rect.Center(deckPos.x, deckPos.y, CardView.Width, CardView.Height);
            view.Rect.localEulerAngles = new Vector3(0, 0, 18);
            view.Rect.localScale = Vector3.one * CardScale;
            Sfx.Play(SfxId.CardDeal, 0.9f, 0.1f);
            into.Add(view);
            yield return Tween.All(this, Tween.Move(view.Rect, at, duration), Tween.Rotate(view.Rect, 0f, duration));
            if (faceUp) yield return view.Flip(0.18f);
        }

        // ---------------- decisions ----------------

        void ShowActions()
        {
            actionControls.gameObject.SetActive(true);
            bool river = round.Phase == HoldemPhase.River, pre = round.Phase == HoldemPhase.Preflop;
            checkButton.gameObject.SetActive(!river);
            foldButton.gameObject.SetActive(river);
            raiseHigh.gameObject.SetActive(pre);
            raiseLow.SetLabel(Loc.F("BET {0}×", LowRaise));
            raiseHigh.SetLabel(Loc.F("BET {0}×", 4));
            raiseLow.Interactable = round.CanRaise(LowRaise);
            raiseHigh.Interactable = round.CanRaise(4);
            if (pre)
            {
                checkButton.Rect.Center(-400, 0, 300, 136);
                raiseLow.Rect.Center(0, 0, 300, 136);
                raiseHigh.Rect.Center(400, 0, 300, 136);
            }
            else
            {
                (river ? foldButton : checkButton).Rect.Center(-220, 0, 320, 136);
                raiseLow.Rect.Center(220, 0, 320, 136);
            }
            hint.text = pre ? Loc.T("Raise now for 4× or 3× your ante, or check and see the flop.")
                : river ? Loc.T("Last chance: bet 1× your ante, or fold and lose the ante and blind.")
                : Loc.T("Bet 2× your ante now, or check and see the last two cards.");
        }

        void Act(System.Action action)
        {
            if (busy) return;
            actionControls.gameObject.SetActive(false);
            hint.text = "";
            action();
        }

        void Check()
        {
            int shownBefore = round.BoardShown;
            round.Check();
            Sfx.Play(SfxId.Click);
            StartCoroutine(CheckRoutine(shownBefore));
        }

        IEnumerator CheckRoutine(int shownBefore)
        {
            busy = true;
            Say(round.BoardShown == 3 ? Loc.T("Here's the flop.") : Loc.T("The turn and the river."), 2f);
            yield return RevealBoard(shownBefore, round.BoardShown);
            UpdatePlayerHand();
            busy = false;
            ShowActions();
        }

        void Raise(int multiple)
        {
            int shownBefore = round.BoardShown;
            long before = App.I.Wallet.Balance;
            if (!round.Raise(multiple))
            {
                Sfx.Play(SfxId.Error);
                ShowActions();
                return;
            }
            App.I.Intensity.OnBetPlaced(round.TotalWagered, Table.MinBet * 2, Table.MaxBet * 6, before + round.Ante * 2);
            stacks[2].SetAmount(round.Play);
            Sfx.Play(SfxId.ChipStack);
            Say(multiple >= 3 ? DealerLines.Pick(DealerMoment.BigBet, App.I.Rng) : Loc.T("Let's see what I have."), 2f);
            StartCoroutine(Showdown(shownBefore));
        }

        void Fold()
        {
            round.Fold();
            Sfx.Play(SfxId.Click);
            StartCoroutine(Showdown(round.BoardShown));
        }

        IEnumerator RevealBoard(int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                yield return boardCards[i].Flip(0.2f);
                yield return Tween.Wait(0.08f);
            }
        }

        IEnumerator Showdown(int shownBefore)
        {
            busy = true;
            yield return RevealBoard(shownBefore, 5);
            UpdatePlayerHand();
            yield return Tween.Wait(0.35f);
            foreach (var d in dealerCards) yield return d.Flip(0.22f);
            var dealerCat = PokerHand.Category(round.DealerScore);
            ShowHand(dealerHand, PokerHand.Name(dealerCat), round.DealerQualifies ? Color.white : Palette.TextMuted);
            yield return Tween.Wait(0.5f);
            yield return Settle();
        }

        void UpdatePlayerHand()
        {
            int shown = round.BoardShown;
            if (shown < 3)
            {
                bool pair = PokerHand.High(round.PlayerCards[0]) == PokerHand.High(round.PlayerCards[1]);
                ShowHand(playerHand, pair ? PokerHand.Name(HandCategory.Pair) : PokerHand.Name(HandCategory.HighCard), Palette.GoldLight);
                return;
            }
            var cat = PokerHand.Category(HoldemRound.Best(round.PlayerCards, round.Board, shown));
            ShowHand(playerHand, PokerHand.Name(cat), cat >= HandCategory.Straight ? Palette.Gold : Palette.GoldLight);
        }

        // ---------------- settling ----------------

        IEnumerator Settle()
        {
            long net = round.Net;
            var outcome = round.Outcome;
            if (!round.DealerQualifies && outcome != HoldemOutcome.Fold)
                App.I.UI.Toast(Loc.T("The dealer doesn't qualify: your ante comes back."), Palette.TextMuted, Icon.Check);

            long[] staked = { round.Ante, round.Blind, round.Play };
            long[] back = { round.AnteReturn, round.BlindReturn, round.PlayReturn };
            for (int i = 0; i < 3; i++)
            {
                if (staked[i] <= 0) continue;
                string stamp = back[i] > staked[i] ? Loc.T("WIN") : back[i] == staked[i] ? Loc.T("PUSH") : Loc.T("LOSE");
                Color color = back[i] > staked[i] ? Palette.Green : back[i] == staked[i] ? Color.white : Palette.Red;
                StartCoroutine(Stamp(OnFx(new Vector2(BetX[i], BetY + 90)), stamp, color, 50));
                if (back[i] > 0)
                {
                    stacks[i].SetAmount(back[i]);
                    StartCoroutine(FlyToBalance(OnHud(stacks[i].transform.position), back[i] > staked[i] ? 6 : 3));
                }
                else
                {
                    StartCoroutine(Tween.Move(stacks[i].Rect, new Vector2(0, DealerY), 0.45f, Ease.InQuad));
                    StartCoroutine(Tween.Fade(UIKit.Group(stacks[i]), 0f, 0.45f));
                }
            }
            Sfx.Play(back[0] + back[1] + back[2] > 0 ? SfxId.ChipStack : SfxId.Lose);
            yield return Tween.Wait(0.3f);

            App.I.RecordWager(GameKind.Holdem, round.TotalWagered, net);
            App.I.Intensity.OnRoundResult(net, PokerHand.Category(round.PlayerScore) >= HandCategory.Straight && net > 0);
            if (outcome == HoldemOutcome.Fold)
            {
                Sfx.Play(SfxId.Lose);
                Say(Loc.T("Folded. The next hand is yours."), 2.4f);
            }
            else if (net > 0)
            {
                bool big = PokerHand.Category(round.PlayerScore) >= HandCategory.Straight || net >= round.Ante * 6;
                Sfx.Play(big ? SfxId.BigWin : SfxId.Win);
                ShowBanner("+" + ChipFormat.Full(net), Palette.Gold);
                Confetti.Burst(fx, new Vector2(0, -100), big ? 80 : 40, big);
                if (seat != null && seat.Staff != null) seat.Staff.Wink();
                else if (dealer != null) dealer.Wink(0.8f);
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

            yield return Tween.Wait(2.2f);
            if (streakLine != null)
            {
                Say(streakLine, 3f);
                streakLine = null;
            }
            yield return ClearTable();
            EnterBetting();
            if (App.I.Wallet.Balance < Table.MinBet * 6)
            {
                Say(DealerLines.Pick(DealerMoment.OutOfChips, App.I.Rng), 4f);
                OfferChips();
            }
        }

        IEnumerator ClearTable()
        {
            var all = new List<CardView>(playerCards);
            all.AddRange(dealerCards);
            all.AddRange(boardCards);
            var routines = new List<IEnumerator>();
            foreach (var c in all)
            {
                routines.Add(Tween.Move(c.Rect, new Vector2(-deckPos.x, deckPos.y), 0.35f, Ease.InQuad));
                routines.Add(Tween.Rotate(c.Rect, -14f, 0.35f));
            }
            if (all.Count > 0) Sfx.Play(SfxId.CardDeal, 0.6f);
            ShowHand(playerHand, null, Color.white);
            ShowHand(dealerHand, null, Color.white);
            yield return Tween.All(this, routines.ToArray());
            foreach (var c in all) Destroy(c.gameObject);
            playerCards.Clear();
            dealerCards.Clear();
            boardCards.Clear();
            for (int i = 0; i < 3; i++)
            {
                stacks[i].SetAmount(0);
                stacks[i].Rect.anchoredPosition = new Vector2(BetX[i], BetY + 8);
                UIKit.Group(stacks[i]).alpha = 1f;
            }
        }

        // ---------------- feedback ----------------

        IEnumerator Stamp(Vector2 pos, string text, Color color, int size)
        {
            var label = UIKit.Label(fx, text, size, color, TextAnchor.MiddleCenter, FontKind.Display, "Stamp");
            var rt = ((RectTransform)label.transform).Center(pos.x, pos.y, 400, 100);
            UIKit.TextOutline(label, new Color(0, 0, 0, 0.8f), 3f);
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
            else bubble.Say(line, hold, dealer, Staff.IsFemale(App.I.Staff, StaffRole.Dealer, DealerTheme) ? VoicePitch.High : VoicePitch.Low);
        }

        Vector3 OnHud(Vector3 tableWorld) => seat != null ? seat.ToHudWorld(tableWorld) : tableWorld;
        Vector2 OnFx(Vector2 tablePos) => seat != null ? seat.ToHudLocal(table.TransformPoint(tablePos), fx) : tablePos;

        string streakLine;

        void OnStreakExtended() => streakLine = DealerLines.StreakExtended(App.I.Progress.Streak.Count, App.I.Rng);

        void OfferChips() => ChipOffer.Show(RefreshBetting);

        void Update()
        {
            seat?.Tick();
            if (!busy && !round.InProgress && bettingControls.gameObject.activeSelf)
            {
                dealButton.Interactable = pendingAnte >= Table.MinBet && pendingAnte <= MaxAnte;
                idleTimer -= Time.unscaledDeltaTime;
                if (idleTimer <= 0f) Say(DealerLines.Pick(pendingAnte > 0 ? DealerMoment.Idle : DealerMoment.PlaceBet, App.I.Rng), 2.5f);
            }
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Progress.StreakExtended -= OnStreakExtended;
            if (seat == null) return;
            seat.Dispose();
            var next = App.I != null ? App.I.UI.Current : null;
            if (seat.World != null && next != this && (next == null || !next.ShowsWorld)) seat.World.SetVisible(false);
        }

        void Leave()
        {
            if (busy || round.InProgress)
            {
                App.I.UI.Toast(Loc.T("Finish this hand first."), Palette.TextMuted, Icon.Clock);
                return;
            }
            App.I.UI.Show<LobbyScreen>();
        }

        public override bool OnBack()
        {
            Leave();
            return true;
        }

        // ---------------- automated tour hooks (DevTour) ----------------

        internal bool TourIdle => !busy && !round.InProgress;
        internal bool TourAwaitingPlayer => !busy && round.InProgress;
        internal void TourBet(int chipIndex) => AddChip(Table.ChipValues[chipIndex], chipIndex, (RectTransform)bettingControls.GetChild(chipIndex));
        internal void TourDeal() => Deal();

        internal void TourPlay()
        {
            int raise = HoldemStrategy.Decide(round);
            if (raise > 0 && round.CanRaise(raise)) Act(() => Raise(raise));
            else if (round.CanCheck) Act(Check);
            else Act(Fold);
        }
    }
}
