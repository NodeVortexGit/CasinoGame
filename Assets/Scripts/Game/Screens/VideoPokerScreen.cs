using System.Collections;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Player;
using Casino.Core.VideoPoker;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// Jacks or Better video poker (<see cref="VideoPokerRound"/>), full-pay 9/6. Deal five, tap the cards to hold, draw
    /// once. Played on a machine's screen in the casino. HINT holds what the textbook strategy would.
    /// </summary>
    public sealed class VideoPokerScreen : UIScreen
    {
        public static readonly long[] BetOptions = { 10, 20, 50, 100, 250, 500, 1_000, 2_500, 5_000 };
        const float LayerScale = 0.00074f, CardY = -105f, CardGap = 200f, CardScale = 1.2f;

        /// <summary>Which machine on the casino floor was used.</summary>
        public int Machine;

        VideoPokerRound round;
        WorldTable seat;
        RectTransform fx;
        readonly CardView[] cards = new CardView[5];
        readonly Text[] heldTags = new Text[5];
        readonly UIButton[] holdButtons = new UIButton[5];
        readonly Image[] payRows = new Image[JacksOrBetter.Paytable.Length];
        Text betText, resultText, banner;
        UIButton deal, minus, plus, hint;
        int betIndex;
        bool busy;

        public override bool ShowsWorld => seat != null;

        protected override void Build()
        {
            round = new VideoPokerRound(App.I.Rng, App.I.Wallet);
            var world = CasinoWorld.Instance;
            if (world != null && world.TryVideoPokerMachine(Machine, out var screen, out var facing))
            {
                // A little closer than at the slots: five cards and a paytable need to read on a phone.
                Vector3 eye = screen + facing * 0.82f + Vector3.up * 0.12f;
                var standAt = new Vector2(screen.x + facing.x * 1.05f, screen.z + facing.z * 1.05f);
                seat = WorldTable.Sit(Content, eye, screen - Vector3.up * 0.03f, 54f, 70f, standAt, Mathf.Atan2(-facing.x, -facing.z) * Mathf.Rad2Deg,
                    screen + facing * 0.012f - Vector3.up * (40f * LayerScale), Quaternion.LookRotation(-facing), LayerScale, null, true);
            }
            var cab = seat != null ? seat.Layer : Content;
            if (seat == null) Backgrounds.Casino(Root, Palette.Hex("0E2348"), Palette.Hex("05070F"), new Vector2(0, 300));
            TopBar.Create(Content, Loc.T("VIDEO POKER"), Icon.Back, Leave);

            // Cabinet: gold frame, a deep blue screen, the paytable across the top.
            var frame = UIKit.Panel(cab, "Cabinet", Palette.Gold, 40f);
            var frt = ((RectTransform)frame.transform).Center(0, 40, 1180, 660);
            var inner = UIKit.Panel(frt, "Screen", Palette.Hex("0B1D4A"), 32f);
            ((RectTransform)inner.transform).Stretch(10, 10, 10, 10);
            BuildPaytable(frt);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var c = CardView.Create(frt);
                c.Rect.Center((i - 2) * CardGap, CardY, CardView.Width, CardView.Height);
                c.Rect.localScale = Vector3.one * CardScale;
                c.SetFaceUp(false);
                c.gameObject.AddComponent<Button>().onClick.AddListener(() => ToggleHold(index));
                cards[i] = c;
                heldTags[i] = UIKit.Label(frt, Loc.T("HELD"), 40, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)heldTags[i].transform).Center((i - 2) * CardGap, CardY + 148, 190, 44);
                UIKit.TextOutline(heldTags[i], Palette.Hex("3A1500"), 2f);
                heldTags[i].enabled = false;
            }
            resultText = UIKit.Label(frt, "", 48, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)resultText.transform).Center(0, -275, 1000, 60);
            UIKit.TextOutline(resultText, Palette.Hex("05070F"), 2f);
            if (seat == null) Marquee.Create(UIKit.NewRect("Lights", cab).Center(0, 40, 1240, 720), new Vector2(1240, 720), 40f);

            BuildControls();
            fx = UIKit.NewRect("Fx", Content).Stretch();
            banner = UIKit.Label(fx, "", 130, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display, "Banner");
            ((RectTransform)banner.transform).Center(0, 60, 1600, 200);
            UIKit.TextOutline(banner, Palette.Hex("5A2200"), 5f);
            UIKit.TextShadow(banner, -8f, 0.8f);
            banner.enabled = false;

            long saved = App.I.Save.lastBet;
            for (int i = 0; i < BetOptions.Length; i++)
                if (BetOptions[i] <= System.Math.Max(10, saved / 2)) betIndex = i;
            ChangeBet(0);
            resultText.text = Loc.T("Deal, hold the cards you want, draw once.");
            App.I.Intensity.Baseline = 0.2f;
            Refresh();
        }

        void BuildPaytable(RectTransform frt)
        {
            var table = JacksOrBetter.Paytable;
            int half = (table.Length + 1) / 2;
            for (int i = 0; i < table.Length; i++)
            {
                int col = i / half, row = i % half;
                float x = col == 0 ? -280f : 280f, y = 285f - row * 42f;
                var bg = UIKit.Panel(frt, "Pay", new Color(1f, 1f, 1f, 0f), 10f);
                payRows[i] = bg;
                ((RectTransform)bg.transform).Center(x, y, 540, 40);
                var name = UIKit.Label(bg.transform, JacksOrBetter.Name(table[i].hand), 30, Palette.GoldLight, TextAnchor.MiddleLeft, FontKind.Semi);
                ((RectTransform)name.transform).Stretch(16, 0, 120, 0);
                var pays = UIKit.Label(bg.transform, table[i].pays.ToString(), 30, Color.white, TextAnchor.MiddleRight, FontKind.Display);
                ((RectTransform)pays.transform).Stretch(380, 0, 16, 0);
            }
        }

        void BuildControls()
        {
            var barBg = UIKit.Panel(Content, "Controls", new Color(0.04f, 0.03f, 0.1f, 0.85f), 30f);
            ((RectTransform)barBg.transform).At(0.5f, 0f, 0, 20, 1820, 160, 0.5f, 0f);
            minus = UIKit.Button(Content, "–", ButtonStyle.Dark, () => ChangeBet(-1), 80, null, FontKind.Bold, 55f);
            minus.Rect.At(0.5f, 0f, -830, 100, 100, 100);
            plus = UIKit.IconButton(Content, Icon.Plus, ButtonStyle.Dark, () => ChangeBet(1), 100f);
            plus.Rect.At(0.5f, 0f, -560, 100, 100, 100);
            var betLabel = UIKit.Label(Content, Loc.T("BET"), 24, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)betLabel.transform).At(0.5f, 0f, -695, 140, 170, 34);
            betText = UIKit.Label(Content, "", 50, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)betText.transform).At(0.5f, 0f, -695, 90, 170, 64);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                holdButtons[i] = UIKit.Button(Content, Loc.T("HOLD"), ButtonStyle.Blue, () => ToggleHold(index), 34);
                holdButtons[i].Rect.At(0.5f, 0f, -390 + i * 162, 100, 150, 100);
            }
            hint = UIKit.Button(Content, Loc.T("HINT"), ButtonStyle.Purple, Hint, 30);
            hint.Rect.At(0.5f, 0f, 470, 100, 150, 100);
            deal = UIKit.Button(Content, Loc.T("DEAL"), ButtonStyle.Gold, DealOrDraw, 60, Icon.Play);
            deal.Rect.At(0.5f, 0f, 730, 100, 300, 128);
        }

        void ChangeBet(int delta)
        {
            if (busy || round.Phase == VideoPokerPhase.Dealt) return;
            betIndex = Mathf.Clamp(betIndex + delta, 0, BetOptions.Length - 1);
            betText.text = ChipFormat.Short(BetOptions[betIndex]);
            minus.Interactable = betIndex > 0;
            plus.Interactable = betIndex < BetOptions.Length - 1;
            if (delta != 0) Sfx.Play(SfxId.Chip, 0.7f, 0.08f);
        }

        void Refresh()
        {
            bool dealt = round.Phase == VideoPokerPhase.Dealt;
            deal.SetLabel(dealt ? Loc.T("DRAW") : Loc.T("DEAL"));
            deal.Interactable = !busy;
            hint.Interactable = !busy && dealt;
            minus.Interactable = !busy && !dealt && betIndex > 0;
            plus.Interactable = !busy && !dealt && betIndex < BetOptions.Length - 1;
            for (int i = 0; i < 5; i++)
            {
                holdButtons[i].Interactable = !busy && dealt;
                bool held = dealt && round.Held[i];
                holdButtons[i].SetStyle(held ? ButtonStyle.Gold : ButtonStyle.Blue);
                holdButtons[i].SetLabel(held ? Loc.T("HELD") : Loc.T("HOLD"));
                heldTags[i].enabled = held;
            }
        }

        void ToggleHold(int i)
        {
            if (busy || round.Phase != VideoPokerPhase.Dealt) return;
            round.Toggle(i);
            Sfx.Play(SfxId.Click);
            StartCoroutine(Tween.Punch(cards[i].Rect, 0.06f, 0.2f));
            Refresh();
        }

        void Hint()
        {
            if (busy || round.Phase != VideoPokerPhase.Dealt) return;
            round.SetHolds(VideoPokerStrategy.Hold(round.Hand));
            Sfx.Play(SfxId.Ding, 0.6f);
            Refresh();
        }

        void DealOrDraw()
        {
            if (busy) return;
            if (round.Phase == VideoPokerPhase.Dealt) StartCoroutine(DrawRoutine());
            else Deal();
        }

        void Deal()
        {
            if (App.I.Energy.IsEmpty)
            {
                ExhaustedPrompt.Show();
                return;
            }
            long bet = BetOptions[betIndex];
            long before = App.I.Wallet.Balance;
            if (!round.Deal(bet))
            {
                Sfx.Play(SfxId.Error);
                while (betIndex > 0 && BetOptions[betIndex] > App.I.Wallet.Balance) betIndex--;
                ChangeBet(0);
                if (App.I.Wallet.Balance < BetOptions[0]) ChipOffer.Show(Refresh);
                else App.I.UI.Toast(Loc.T("Bet lowered to what you can cover."), Palette.TextMuted, Icon.Coin);
                return;
            }
            App.I.Save.lastBet = bet;
            App.I.Intensity.OnBetPlaced(bet, BetOptions[0], BetOptions[BetOptions.Length - 1], before);
            StartCoroutine(DealRoutine());
        }

        IEnumerator DealRoutine()
        {
            busy = true;
            Refresh();
            HighlightPay(VideoPokerHand.Nothing);
            resultText.text = "";
            banner.enabled = false;
            foreach (var c in cards)
                if (c.FaceUp) StartCoroutine(c.Flip(0.16f));
            yield return Tween.Wait(0.2f);
            Sfx.Play(SfxId.Shuffle, 0.6f);
            for (int i = 0; i < 5; i++)
            {
                cards[i].Set(round.Hand[i], false);
                Sfx.Play(SfxId.CardDeal, 0.8f, 0.1f);
                yield return cards[i].Flip(0.16f);
            }
            var now = round.Current;
            resultText.color = now != VideoPokerHand.Nothing ? Palette.Gold : Palette.TextMuted;
            resultText.text = now != VideoPokerHand.Nothing ? JacksOrBetter.Name(now) : Loc.T("Tap the cards to hold, then draw.");
            HighlightPay(now);
            busy = false;
            Refresh();
        }

        IEnumerator DrawRoutine()
        {
            busy = true;
            Refresh();
            var keep = new bool[5];
            for (int i = 0; i < 5; i++) keep[i] = round.Held[i];
            round.Draw();
            for (int i = 0; i < 5; i++)
            {
                if (keep[i]) continue;
                yield return cards[i].Flip(0.12f);
                cards[i].Set(round.Hand[i], false);
                Sfx.Play(SfxId.CardDeal, 0.8f, 0.1f);
                yield return cards[i].Flip(0.14f);
            }
            for (int i = 0; i < 5; i++) heldTags[i].enabled = false;

            long net = round.Net;
            App.I.RecordWager(GameKind.VideoPoker, round.Bet, net);
            App.I.Intensity.OnRoundResult(net, round.Result >= VideoPokerHand.FullHouse);
            HighlightPay(round.Result);
            if (round.Returned > 0)
            {
                bool big = round.Result >= VideoPokerHand.FullHouse;
                resultText.color = Palette.Gold;
                resultText.text = JacksOrBetter.Name(round.Result) + "   " + (net > 0
                    ? Loc.F("WIN  +{0}", ChipFormat.Full(round.Returned))
                    : Loc.F("RETURNED {0}", ChipFormat.Full(round.Returned)));   // a push is never called a win
                if (net > 0)
                {
                    Sfx.Play(big ? SfxId.BigWin : SfxId.Win);
                    if (big)
                    {
                        banner.text = Loc.Upper(JacksOrBetter.Name(round.Result));
                        banner.enabled = true;
                        StartCoroutine(BannerRoutine());
                        Confetti.Burst(fx, new Vector2(0, 0), 90, true);
                    }
                }
                else Sfx.Play(SfxId.Push);
            }
            else
            {
                resultText.color = Palette.TextMuted;
                resultText.text = Loc.T("No win this time");
                Sfx.Play(SfxId.Lose, 0.6f);
            }
            busy = false;
            Refresh();
            if (App.I.Wallet.Balance < BetOptions[0]) ChipOffer.Show(Refresh);
        }

        void HighlightPay(VideoPokerHand hand)
        {
            for (int i = 0; i < payRows.Length; i++)
                payRows[i].color = JacksOrBetter.Paytable[i].hand == hand ? new Color(1f, 0.8f, 0.2f, 0.35f) : new Color(1f, 1f, 1f, 0f);
        }

        IEnumerator BannerRoutine()
        {
            var rt = banner.rectTransform;
            rt.localScale = Vector3.one * 0.3f;
            var g = UIKit.Group(banner);
            g.alpha = 1f;
            yield return Tween.Scale(rt, Vector3.one, 0.45f, Ease.OutElastic);
            yield return Tween.Wait(1.4f);
            yield return Tween.Fade(g, 0f, 0.35f);
            banner.enabled = false;
        }

        void Update() => seat?.Tick();

        void OnDestroy()
        {
            if (seat == null) return;
            seat.Dispose();
            var next = App.I != null ? App.I.UI.Current : null;
            if (seat.World != null && next != this && (next == null || !next.ShowsWorld)) seat.World.SetVisible(false);
        }

        void Leave()
        {
            if (busy || round.Phase == VideoPokerPhase.Dealt)
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

        internal bool TourBusy => busy;
        internal bool TourDealt => round.Phase == VideoPokerPhase.Dealt;
        internal void TourDealOrDraw() => DealOrDraw();
        internal void TourHint() => Hint();
    }
}
