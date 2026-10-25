using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Baccarat;
using Casino.Core.Blackjack;
using Casino.Core.Player;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// Punto banco (<see cref="BaccaratTable"/>): pick a chip, tap Player, Banker or Tie, deal. The cards follow the fixed
    /// third-card rules, so there is nothing to decide once the bets are down. The last results show as a bead road.
    /// </summary>
    public sealed class BaccaratScreen : UIScreen
    {
        public const int DealerTheme = 7;

        /// <summary>Multiples of 20, so the banker's 5% commission always comes out in whole chips.</summary>
        public static readonly long[] ChipValues = { 20, 100, 200, 1_000, 5_000 };
        public const long MaxPerSpot = 20_000;

        const float CardsY = 10f, TotalY = 175f, BetY = -300f, HandX = 300f, CardGap = 120f;
        const float SeatPitch = 24f, LayerScale = 0.0014f;
        static readonly BaccaratSide[] Sides = { BaccaratSide.Player, BaccaratSide.Tie, BaccaratSide.Banker };
        static readonly float[] SpotX = { -340f, 0f, 340f };
        static readonly float[] SpotW = { 320f, 250f, 320f };

        BaccaratTable game;
        WorldTable seat;
        RectTransform table, fx, road;
        Vector2 deckPos = new Vector2(760, 260);
        Vector3 deckWorld;
        readonly long[] bets = new long[3];
        long[] lastBets;
        readonly List<int> placed = new List<int>();
        readonly ChipStack[] stacks = new ChipStack[3];
        readonly RectTransform[] spots = new RectTransform[3];
        readonly List<CardView> cards = new List<CardView>();
        readonly List<UIButton> chipButtons = new List<UIButton>();
        readonly List<BaccaratSide> history = new List<BaccaratSide>();
        Text playerTotal, bankerTotal, hint, banner;
        UIButton dealButton, undoButton, clearButton, rebetButton;
        long chip = ChipValues[0];
        bool busy;
        StaffPortrait dealer;
        SpeechBubble bubble;

        public override bool ShowsWorld => seat != null;

        protected override void Build()
        {
            game = new BaccaratTable(App.I.Rng, App.I.Wallet);
            var world = CasinoWorld.Instance;
            if (world != null && world.TryBaccaratTable(out var c, out var dealerFigure))
            {
                Vector3 eye = c + new Vector3(0f, 1.55f, -2.0f);
                Vector3 look = eye + new Vector3(0f, -Mathf.Sin(SeatPitch * Mathf.Deg2Rad), Mathf.Cos(SeatPitch * Mathf.Deg2Rad));
                seat = WorldTable.Sit(Content, eye, look, 60f, 80f, new Vector2(c.x, c.z - 2.65f), 0f,
                    c + new Vector3(0f, 0.857f, -0.66f), Quaternion.Euler(90f, 0f, 0f), LayerScale, dealerFigure, true);
                if (seat != null)
                {
                    deckWorld = c + new Vector3(0.72f, 0.93f, -0.3f);
                    deckPos = seat.OnLayer(deckWorld);
                }
            }

            BuildTable();
            TopBar.Create(Content, Loc.T("BACCARAT"), Icon.Back, Leave);
            BuildRoad();
            BuildControls();
            fx = UIKit.NewRect("Fx", Content).Stretch();
            banner = UIKit.Label(fx, "", 84, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display, "Banner");
            ((RectTransform)banner.transform).Center(0, 330, 1700, 140);   // above the cards, not over them
            UIKit.TextOutline(banner, Palette.Hex("5A2200"), 4f);
            UIKit.TextShadow(banner, -8f, 0.8f);
            banner.enabled = false;

            App.I.Progress.StreakExtended += OnStreakExtended;
            App.I.Intensity.Baseline = 0.2f;
            Refresh();
            Say(Loc.T("Welcome to baccarat! Player, Banker or Tie?"), 3.5f);
        }

        // ---------------- layout ----------------

        void BuildTable()
        {
            if (seat != null) table = seat.Layer;
            else
            {
                var felt = UIKit.Image(Root, "Felt", ProceduralArt.Felt(2), Color.white);
                ((RectTransform)felt.transform).Stretch(-40, -40, -40, -40);
                var vig = UIKit.Image(Root, "Vignette", ProceduralArt.Vignette, Color.white);
                ((RectTransform)vig.transform).Stretch(-60, -60, -60, -60);
                table = UIKit.NewRect("Table", Content).Stretch();
                dealer = Staff.Portrait(Content, 160, StaffRole.Dealer, DealerTheme);
                dealer.Rect.Center(-760, 300, 160, 160);
                dealer.Prewarm();
                bubble = SpeechBubble.Create(Content, 500, 120);
                ((RectTransform)bubble.transform).Center(-430, 330, 500, 120);
            }

            string[] names = { Loc.T("PLAYER"), Loc.T("TIE"), Loc.T("BANKER") };
            string[] pays = { "1:1", "8:1", "0.95:1" };
            Color[] colors = { Palette.Hex("1E4FA8"), Palette.Hex("0D7A45"), Palette.Hex("A3122E") };
            for (int i = 0; i < 3; i++)
            {
                var bg = UIKit.Panel(table, names[i], Palette.WithAlpha(colors[i], 0.75f), 22f, true);
                spots[i] = ((RectTransform)bg.transform).Center(SpotX[i], BetY, SpotW[i], 150);
                UIKit.Outline(spots[i], Palette.WithAlpha(Palette.GoldLight, 0.8f), 22f);
                var label = UIKit.Label(spots[i], names[i], 40, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)label.transform).At(0.5f, 1f, 0, -36, SpotW[i] - 20, 50);
                var pay = UIKit.Label(spots[i], pays[i], 26, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
                ((RectTransform)pay.transform).At(0.5f, 1f, 0, -76, SpotW[i] - 20, 34);
                int index = i;
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => Place(index, chip));
                stacks[i] = ChipStack.Create(table, ChipValues, 70f);
                stacks[i].Rect.Center(SpotX[i], BetY - 40, 70, 70);
            }

            playerTotal = TotalPill(new Vector2(-HandX, TotalY), Loc.T("PLAYER"));
            bankerTotal = TotalPill(new Vector2(HandX, TotalY), Loc.T("BANKER"));
        }

        Text TotalPill(Vector2 at, string side)
        {
            var bg = UIKit.Panel(table, "Total", new Color(0, 0, 0, 0.62f), 26f);
            ((RectTransform)bg.transform).Center(at.x, at.y, 300, 58);
            UIKit.Outline(bg.transform, Palette.WithAlpha(Color.white, 0.35f), 26f);
            var t = UIKit.Label(bg.transform, side, 32, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)t.transform).Stretch(8, 2, 8, 0);
            return t;
        }

        /// <summary>The bead road: the last results, oldest first, in columns of six.</summary>
        void BuildRoad()
        {
            var panel = UIKit.Panel(Content, "Road", new Color(0.03f, 0.02f, 0.07f, 0.78f), 22f);
            var rt = ((RectTransform)panel.transform).At(0f, 1f, 30, -TopBar.Height - 16, 440, 300, 0f, 1f);
            UIKit.Outline(rt, Palette.WithAlpha(Palette.Gold, 0.6f), 22f);
            var title = UIKit.Label(rt, Loc.T("LAST RESULTS"), 28, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)title.transform).At(0.5f, 1f, 0, -26, 400, 40);
            road = UIKit.NewRect("Beads", rt).At(0.5f, 0f, 0, 20, 400, 228, 0.5f, 0f);
        }

        void RedrawRoad()
        {
            for (int i = road.childCount - 1; i >= 0; i--) Destroy(road.GetChild(i).gameObject);
            for (int i = 0; i < history.Count; i++)
            {
                int col = i / 6, row = i % 6;   // eight columns of six fit the panel
                var side = history[i];
                Color color = side == BaccaratSide.Player ? Palette.Hex("2F6FE0") : side == BaccaratSide.Banker ? Palette.Hex("D8213F") : Palette.Hex("16A35A");
                var dot = UIKit.Image(road, "Bead", ProceduralArt.Circle, color);
                ((RectTransform)dot.transform).At(0f, 1f, 10 + col * 48, -4 - row * 38, 36, 36);
                string letter = side == BaccaratSide.Player ? Loc.T("P") : side == BaccaratSide.Banker ? Loc.T("B") : Loc.T("T");
                var t = UIKit.Label(dot.transform, letter, 20, Color.white, TextAnchor.MiddleCenter, FontKind.Bold);
                ((RectTransform)t.transform).Stretch();
            }
        }

        void BuildControls()
        {
            var barBg = UIKit.Panel(Content, "Controls", new Color(0.04f, 0.02f, 0.08f, 0.8f), 30f);
            ((RectTransform)barBg.transform).At(0.5f, 0f, 0, 20, 1700, 150, 0.5f, 0f);
            for (int i = 0; i < ChipValues.Length; i++)
            {
                long value = ChipValues[i];
                var b = UIKit.Button(Content, ChipFormat.Short(value), ButtonStyle.Dark, () => SelectChip(value), 30, null, FontKind.Display, 50f);
                b.Rect.At(0.5f, 0f, -700 + i * 110, 95, 100, 100);
                b.Face.sprite = ProceduralArt.Chip(i);
                b.Face.type = Image.Type.Simple;
                b.Face.color = Color.white;
                chipButtons.Add(b);
            }
            undoButton = UIKit.Button(Content, Loc.T("UNDO"), ButtonStyle.Dark, Undo, 28);
            undoButton.Rect.At(0.5f, 0f, 30, 125, 170, 60);
            clearButton = UIKit.Button(Content, Loc.T("CLEAR"), ButtonStyle.Dark, ClearBets, 28);
            clearButton.Rect.At(0.5f, 0f, 30, 60, 170, 60);
            rebetButton = UIKit.Button(Content, Loc.T("REBET"), ButtonStyle.Blue, Rebet, 32);
            rebetButton.Rect.At(0.5f, 0f, 250, 95, 210, 120);
            dealButton = UIKit.Button(Content, Loc.T("DEAL"), ButtonStyle.Gold, Deal, 56);
            dealButton.Rect.At(0.5f, 0f, 560, 95, 330, 130);

            hint = UIKit.Label(Content, "", 30, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)hint.transform).At(0.5f, 0f, 0, 205, 1500, 44);
            UIKit.TextShadow(hint, -2f, 0.9f);
            SelectChip(chip);
        }

        void SelectChip(long value)
        {
            chip = value;
            for (int i = 0; i < chipButtons.Count; i++)
                chipButtons[i].Rect.localScale = ChipValues[i] == value ? Vector3.one * 1.15f : Vector3.one * 0.92f;
        }

        // ---------------- betting ----------------

        long TotalBet => bets[0] + bets[1] + bets[2];

        void Place(int spot, long amount, bool quiet = false)
        {
            if (busy) return;
            if (TotalBet + amount > App.I.Wallet.Balance)
            {
                Sfx.Play(SfxId.Error);
                if (App.I.Wallet.Balance < ChipValues[0]) ChipOffer.Show(Refresh);
                else App.I.UI.Toast(Loc.T("Not enough chips for that bet."), Palette.TextMuted, Icon.Coin);
                return;
            }
            if (bets[spot] + amount > MaxPerSpot)
            {
                Sfx.Play(SfxId.Error);
                App.I.UI.Toast(Loc.T("Table maximum reached"), Palette.TextMuted, Icon.Lock);
                return;
            }
            bets[spot] += amount;
            placed.Add(spot);
            placedAmounts.Add(amount);
            stacks[spot].SetAmount(bets[spot]);
            if (!quiet) Sfx.Play(SfxId.Chip, 1f, 0.06f);
            Refresh();
        }

        readonly List<long> placedAmounts = new List<long>();

        void Undo()
        {
            if (busy || placed.Count == 0) return;
            int spot = placed[placed.Count - 1];
            long amount = placedAmounts[placedAmounts.Count - 1];
            placed.RemoveAt(placed.Count - 1);
            placedAmounts.RemoveAt(placedAmounts.Count - 1);
            bets[spot] -= amount;
            stacks[spot].SetAmount(bets[spot]);
            Sfx.Play(SfxId.Chip, 0.6f, 0.1f);
            Refresh();
        }

        void ClearBets()
        {
            if (busy) return;
            for (int i = 0; i < 3; i++)
            {
                bets[i] = 0;
                stacks[i].SetAmount(0);
            }
            placed.Clear();
            placedAmounts.Clear();
            Refresh();
        }

        void Rebet()
        {
            if (busy || lastBets == null) return;
            ClearBets();
            for (int i = 0; i < 3; i++)
                if (lastBets[i] > 0) Place(i, lastBets[i], true);
            Sfx.Play(SfxId.ChipStack);
        }

        void Refresh()
        {
            long total = TotalBet;
            dealButton.Interactable = !busy && total > 0;
            undoButton.Interactable = !busy && placed.Count > 0;
            clearButton.Interactable = !busy && placed.Count > 0;
            rebetButton.Interactable = !busy && lastBets != null && placed.Count == 0;
            if (!busy)
                hint.text = total > 0 ? Loc.F("TOTAL BET  {0}", ChipFormat.Full(total)) : Loc.T("Pick a chip, then tap Player, Banker or Tie");
        }

        // ---------------- the coup ----------------

        void Deal()
        {
            if (busy || TotalBet <= 0) return;
            if (App.I.Energy.IsEmpty)
            {
                Say(DealerLines.Pick(DealerMoment.Exhausted, App.I.Rng));
                ExhaustedPrompt.Show();
                return;
            }
            long before = App.I.Wallet.Balance;
            if (!game.Play(bets[0], bets[2], bets[1]))
            {
                Sfx.Play(SfxId.Error);
                return;
            }
            App.I.Intensity.OnBetPlaced(TotalBet, ChipValues[0], MaxPerSpot, before);
            StartCoroutine(CoupRoutine());
        }

        IEnumerator CoupRoutine()
        {
            busy = true;
            Refresh();
            hint.text = "";
            var coup = game.Last;
            if (game.Shuffled)
            {
                Say(DealerLines.Pick(DealerMoment.Shuffle, App.I.Rng), 2f);
                Sfx.Play(SfxId.Shuffle);
                yield return Tween.Wait(0.8f);
            }
            int p = 0, b = 0;
            for (int i = 0; i < coup.ToBanker.Count; i++)
            {
                bool banker = coup.ToBanker[i];
                var card = banker ? coup.Banker[b] : coup.Player[p];
                int index = banker ? b++ : p++;
                if (i == 4) yield return Tween.Wait(0.5f);   // third cards come after a beat
                yield return DealCard(card, banker, index);
                if (i >= 3) UpdateTotals(p, b);
            }
            UpdateTotals(p, b);
            yield return Tween.Wait(0.45f);
            yield return Settle(coup);
        }

        IEnumerator DealCard(Card card, bool banker, int index)
        {
            float side = banker ? HandX : -HandX;
            // The third card lies sideways, outside the first two.
            var at = index < 2 ? new Vector2(side + (index - 0.5f) * CardGap, CardsY)
                : new Vector2(side + (banker ? 1 : -1) * (CardGap + 70f), CardsY - 20f);
            if (seat != null && seat.Staff != null) seat.Staff.SlideOnTable(deckWorld, table.TransformPoint(at), 0.35f);
            var view = CardView.Create(table);
            view.Set(card, false);
            view.Rect.Center(deckPos.x, deckPos.y, CardView.Width, CardView.Height);
            view.Rect.localEulerAngles = new Vector3(0, 0, 18);
            Sfx.Play(SfxId.CardDeal, 0.9f, 0.1f);
            cards.Add(view);
            yield return Tween.All(this, Tween.Move(view.Rect, at, 0.26f), Tween.Rotate(view.Rect, index < 2 ? 0f : 90f, 0.26f));
            yield return view.Flip(0.2f);
        }

        void UpdateTotals(int playerCards, int bankerCards)
        {
            var coup = game.Last;
            playerTotal.text = Loc.T("PLAYER") + "  " + BaccaratRules.Total(coup.Player.GetRange(0, playerCards));
            bankerTotal.text = Loc.T("BANKER") + "  " + BaccaratRules.Total(coup.Banker.GetRange(0, bankerCards));
        }

        IEnumerator Settle(Coup coup)
        {
            var winner = coup.Winner;
            int w = winner == BaccaratSide.Player ? 0 : winner == BaccaratSide.Tie ? 1 : 2;
            StartCoroutine(Tween.Punch(spots[w], 0.15f, 0.5f));
            string text = winner == BaccaratSide.Tie ? Loc.F("TIE  {0}", coup.PlayerTotal)
                : winner == BaccaratSide.Player ? Loc.F("PLAYER WINS  {0} TO {1}", coup.PlayerTotal, coup.BankerTotal)
                : Loc.F("BANKER WINS  {0} TO {1}", coup.BankerTotal, coup.PlayerTotal);
            ShowBanner(text, winner == BaccaratSide.Player ? Palette.Hex("7FB2FF") : winner == BaccaratSide.Banker ? Palette.Hex("FF6A7E") : Palette.Hex("7CFC9A"));
            history.Add(winner);
            if (history.Count > 48) history.RemoveRange(0, 6);
            RedrawRoad();

            long[] staked = (long[])bets.Clone();
            for (int i = 0; i < 3; i++)
            {
                if (staked[i] <= 0) continue;
                long back = BaccaratRules.Return(Sides[i], staked[i], winner);
                if (back > 0)
                {
                    stacks[i].SetAmount(back);
                    StartCoroutine(FlyToBalance(OnHud(stacks[i].transform.position), back > staked[i] ? 6 : 3));
                }
                else
                {
                    StartCoroutine(Tween.Move(stacks[i].Rect, new Vector2(0, CardsY + 200f), 0.45f, Ease.InQuad));
                    StartCoroutine(Tween.Fade(UIKit.Group(stacks[i]), 0f, 0.45f));
                }
            }
            long net = game.Net;
            App.I.RecordWager(GameKind.Baccarat, game.Wagered, net);
            App.I.Intensity.OnRoundResult(net, false);
            if (net > 0)
            {
                bool big = bets[1] > 0 && winner == BaccaratSide.Tie;
                Sfx.Play(big ? SfxId.BigWin : SfxId.Win);
                Confetti.Burst(fx, new Vector2(0, -100), big ? 80 : 40, big);
                if (seat != null && seat.Staff != null) seat.Staff.Wink();
                else if (dealer != null) dealer.Wink(0.8f);
                Say(DealerLines.Pick(big ? DealerMoment.PlayerBigWin : DealerMoment.PlayerWin, App.I.Rng), 2.6f);
                hint.text = Loc.F("WIN  +{0}", ChipFormat.Full(net));
            }
            else if (net == 0)
            {
                Sfx.Play(SfxId.Push);
                Say(DealerLines.Pick(DealerMoment.Push, App.I.Rng), 2.2f);
                hint.text = Loc.F("RETURNED  {0}", ChipFormat.Full(game.Returned));
            }
            else
            {
                Sfx.Play(game.Returned > 0 ? SfxId.Tick : SfxId.Lose);
                Say(DealerLines.Pick(DealerMoment.PlayerLose, App.I.Rng), 2.4f);
                hint.text = game.Returned > 0 ? Loc.F("RETURNED  {0}", ChipFormat.Full(game.Returned)) : Loc.T("No win this time");
            }

            yield return Tween.Wait(2.6f);
            if (streakLine != null)
            {
                Say(streakLine, 3f);
                streakLine = null;
                yield return Tween.Wait(1.5f);
            }
            yield return ClearTable();
            lastBets = staked;
            busy = false;
            ClearBets();
            if (App.I.Wallet.Balance < ChipValues[0])
            {
                Say(DealerLines.Pick(DealerMoment.OutOfChips, App.I.Rng), 4f);
                ChipOffer.Show(Refresh);
            }
        }

        IEnumerator ClearTable()
        {
            var routines = new List<IEnumerator>();
            foreach (var c in cards)
            {
                routines.Add(Tween.Move(c.Rect, new Vector2(-deckPos.x, deckPos.y), 0.35f, Ease.InQuad));
                routines.Add(Tween.Rotate(c.Rect, -14f, 0.35f));
            }
            if (cards.Count > 0) Sfx.Play(SfxId.CardDeal, 0.6f);
            yield return Tween.All(this, routines.ToArray());
            foreach (var c in cards) Destroy(c.gameObject);
            cards.Clear();
            playerTotal.text = Loc.T("PLAYER");
            bankerTotal.text = Loc.T("BANKER");
            for (int i = 0; i < 3; i++)
            {
                stacks[i].SetAmount(0);
                stacks[i].Rect.anchoredPosition = new Vector2(SpotX[i], BetY - 40);
                UIKit.Group(stacks[i]).alpha = 1f;
            }
        }

        // ---------------- feedback ----------------

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
            yield return Tween.Wait(1.6f);
            yield return Tween.Fade(g, 0f, 0.35f);
            banner.enabled = false;
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
            if (seat != null) seat.Say(line, hold);
            else bubble.Say(line, hold, dealer, Staff.IsFemale(App.I.Staff, StaffRole.Dealer, DealerTheme) ? VoicePitch.High : VoicePitch.Low);
        }

        Vector3 OnHud(Vector3 tableWorld) => seat != null ? seat.ToHudWorld(tableWorld) : tableWorld;

        string streakLine;

        void OnStreakExtended() => streakLine = DealerLines.StreakExtended(App.I.Progress.Streak.Count, App.I.Rng);

        void Update() => seat?.Tick();

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
            if (busy)
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

        internal void TourBet(int round)
        {
            Place(round % 2 == 0 ? 2 : 0, ChipValues[1]);
            if (round == 1) Place(1, ChipValues[0]);
        }

        internal void TourDeal() => Deal();
    }
}
