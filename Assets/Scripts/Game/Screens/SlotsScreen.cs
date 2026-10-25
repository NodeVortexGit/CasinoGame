using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Slots;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// "Lucky Ace 7s": 5 reels, 10 lines. Honest by design: the exact payback is in the paytable, reel stops are uniformly
    /// random, there are no staged near-misses, and a return smaller than the bet is shown plainly ("returned"), never
    /// celebrated like a win.
    /// </summary>
    public sealed class SlotsScreen : UIScreen
    {
        const float CellW = 206f, CellH = 172f, ReelGap = 14f;

        /// <summary>Metres per layout unit when the reels play on a machine's screen in the casino.</summary>
        const float LayerScale = 0.00074f;

        WorldTable seat;

        public override bool ShowsWorld => seat != null;

        /// <summary>Which cabinet on the casino floor was used (only changes the colour scheme).</summary>
        public int Machine;

        sealed class Reel
        {
            public RectTransform Root;
            public Image[] Cells;
            public float Position;   // strip index shown in the top row (fractional while spinning)
            public int Index;
        }

        readonly Reel[] reels = new Reel[SlotMachine.Reels];
        RectTransform lineLayer, fx;
        Text betText, winText, banner;
        UIButton spin, minus, plus, auto;
        // Auto spin stays on from machine to machine, but only for this run of the app: it always starts off.
        static bool autoSpin;
        bool leaveAfterSpin;
        int betIndex;
        bool spinning;
        Coroutine showWins;
        static readonly Color[] LineColors =
        {
            Palette.Hex("FF4FA3"), Palette.Hex("35E0F0"), Palette.Hex("7CFC9A"), Palette.Hex("F5C542"), Palette.Hex("FF9A2E"),
            Palette.Hex("9B4DFF"), Palette.Hex("FF5E5E"), Palette.Hex("4AA8FF"), Palette.Hex("FFE9A3"), Palette.Hex("E0218A"),
        };

        float ReelX(int r) => (r - 2) * (CellW + ReelGap);
        static float RowY(int row) => (1 - row) * CellH;

        protected override void Build()
        {
            Color[] cabinets = { Palette.Hex("8A1730"), Palette.Hex("43207A"), Palette.Hex("124F86"), Palette.Hex("0D5E3A"), Palette.Hex("8A3A12") };
            Color cabinet = cabinets[Mathf.Abs(Machine) % cabinets.Length];

            // In the casino you sit at the machine you picked and the reels spin on its screen.
            var world = CasinoWorld.Instance;
            if (world != null && world.TrySlotMachine(Machine, out var screen, out var facing))
            {
                Vector3 eye = screen + facing * 1.0f + Vector3.up * 0.16f;
                var standAt = new Vector2(screen.x + facing.x * 1.05f, screen.z + facing.z * 1.05f);
                seat = WorldTable.Sit(Content, eye, screen - Vector3.up * 0.03f, 54f, 70f, standAt, Mathf.Atan2(-facing.x, -facing.z) * Mathf.Rad2Deg,
                    screen + facing * 0.012f - Vector3.up * (40f * LayerScale), Quaternion.LookRotation(-facing), LayerScale, null);
            }
            var cab = seat != null ? seat.Layer : Content;
            if (seat == null) Backgrounds.Casino(Root, Palette.Shade(cabinet, 0.9f), Palette.Hex("0B0612"), new Vector2(0, 300));

            TopBar.Create(Content, Loc.T("LUCKY ACE 7s"), Icon.Back, Leave);

            // Cabinet: gold frame, dark reel window, marquee lights on top.
            var frame = UIKit.Panel(cab, "Cabinet", Palette.Gold, 40f);
            var frt = ((RectTransform)frame.transform).Center(0, 40, 1180, 640);
            var inner = UIKit.Panel(frt, "Inner", Palette.Shade(cabinet, 0.55f), 34f);
            ((RectTransform)inner.transform).Stretch(10, 10, 10, 10);
            var window = UIKit.Panel(frt, "Window", Palette.Hex("FFF8EC"), 20f);
            var wrt = ((RectTransform)window.transform).Center(0, 0, 5 * CellW + 4 * ReelGap + 24, 3 * CellH + 24);
            Marquee.Create(UIKit.NewRect("Lights", cab).Center(0, 40, 1240, 700), new Vector2(1240, 700), 40f);

            for (int r = 0; r < SlotMachine.Reels; r++)
            {
                var reelRoot = UIKit.NewRect("Reel" + r, wrt).Center(ReelX(r), 0, CellW, 3 * CellH);
                reelRoot.gameObject.AddComponent<RectMask2D>();
                var shade = UIKit.Image(reelRoot, "Shade", ProceduralArt.GradientV, new Color(0, 0, 0, 0.06f));
                ((RectTransform)shade.transform).Stretch();
                var reel = new Reel { Root = reelRoot, Cells = new Image[5], Index = r };
                for (int k = 0; k < 5; k++)
                {
                    var img = UIKit.Image(reelRoot, "Cell" + k, null, Color.white);
                    img.preserveAspect = true;
                    ((RectTransform)img.transform).Center(0, 0, CellW - 26, CellH - 20);
                    reel.Cells[k] = img;
                }
                reel.Position = App.I.Rng.Next(SlotMachine.Strips[r].Length);
                reels[r] = reel;
                SetReel(reel, reel.Position);
                if (r > 0)
                {
                    var sep = UIKit.Image(wrt, "Sep" + r, null, Palette.WithAlpha(Palette.GoldDark, 0.6f));
                    ((RectTransform)sep.transform).Center(ReelX(r) - (CellW + ReelGap) * 0.5f, 0, 4, 3 * CellH);
                }
            }
            lineLayer = UIKit.NewRect("Lines", wrt).Stretch(12, 12, 12, 12);
            // Payline numbers down both sides
            for (int l = 0; l < SlotMachine.LineCount; l++)
            {
                int row0 = SlotMachine.Paylines[l][0], row4 = SlotMachine.Paylines[l][4];
                LineTag(frt, l, -frt.sizeDelta.x * 0.5f + 4f, RowY(row0) + (l % 3 - 1) * 26f);
                LineTag(frt, l, frt.sizeDelta.x * 0.5f - 4f, RowY(row4) + (l % 3 - 1) * 26f);
            }

            // Bottom controls
            var barBg = UIKit.Panel(Content, "Controls", new Color(0.06f, 0.03f, 0.1f, 0.85f), 30f);
            ((RectTransform)barBg.transform).At(0.5f, 0f, 0, 20, 1500, 160, 0.5f, 0f);
            minus = UIKit.Button(Content, "–", ButtonStyle.Dark, () => ChangeBet(-1), 80, null, FontKind.Bold, 55f);
            minus.Rect.At(0.5f, 0f, -640, 100, 110, 110);
            plus = UIKit.IconButton(Content, Icon.Plus, ButtonStyle.Dark, () => ChangeBet(1), 110f);
            plus.Rect.At(0.5f, 0f, -330, 100, 110, 110);
            var betLabel = UIKit.Label(Content, Loc.T("TOTAL BET"), 24, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)betLabel.transform).At(0.5f, 0f, -485, 140, 220, 34);
            betText = UIKit.Label(Content, "", 52, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)betText.transform).At(0.5f, 0f, -485, 90, 220, 64);

            var winLabel = UIKit.Label(Content, Loc.T("WIN"), 24, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)winLabel.transform).At(0.5f, 0f, 30, 140, 400, 34);
            winText = UIKit.Label(Content, "", 52, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)winText.transform).At(0.5f, 0f, 30, 90, 520, 64);

            var table = UIKit.Button(Content, Loc.T("PAYTABLE"), ButtonStyle.Purple, () => App.I.UI.Modal<SlotPaytableModal>(), 30, Icon.Star);
            table.Rect.At(0.5f, 0f, 320, 100, 230, 96);
            spin = UIKit.Button(Content, Loc.T("SPIN"), ButtonStyle.Gold, Spin, 64, Icon.Play);
            spin.Rect.At(0.5f, 0f, 620, 100, 300, 128);
            spin.gameObject.AddComponent<Pulse>();
            auto = UIKit.Button(Content, Loc.T("AUTO SPIN"), ButtonStyle.Dark, ToggleAuto, 26, Icon.Check);
            auto.Rect.At(0.5f, 0f, 620, 222, 300, 64);
            ShowAuto();

            fx = UIKit.NewRect("Fx", Content).Stretch();
            banner = UIKit.Label(fx, "", 150, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display, "Banner");
            ((RectTransform)banner.transform).Center(0, 60, 1600, 220);
            UIKit.TextOutline(banner, Palette.Hex("5A2200"), 5f);
            UIKit.TextShadow(banner, -8f, 0.8f);
            banner.enabled = false;

            long saved = App.I.Save.lastBet;
            betIndex = 0;
            for (int i = 0; i < SlotMachine.BetOptions.Length; i++)
                if (SlotMachine.BetOptions[i] <= System.Math.Max(10, saved / 2)) betIndex = i;
            ChangeBet(0);
            winText.text = Loc.T("GOOD LUCK!");
            App.I.Intensity.Baseline = 0.22f;
            if (autoSpin) StartCoroutine(AutoNext(1.2f));   // carried over from the last machine
        }

        void ToggleAuto()
        {
            autoSpin = !autoSpin;
            ShowAuto();
            if (autoSpin && !spinning) StartCoroutine(AutoNext(0.2f));
        }

        void ShowAuto()
        {
            auto.SetStyle(autoSpin ? ButtonStyle.Green : ButtonStyle.Dark);
            auto.SetIconVisible(autoSpin);
        }

        /// <summary>The next automatic spin, once nothing is in the way (a modal, a spin still running).</summary>
        IEnumerator AutoNext(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            while (autoSpin && (spinning || App.I.UI.HasModal || App.I.UI.Current != this)) yield return null;
            if (autoSpin) Spin();
        }

        void LineTag(RectTransform parent, int line, float x, float y)
        {
            var tag = UIKit.Panel(parent, "Line" + line, LineColors[line], 14f);
            ((RectTransform)tag.transform).Center(x, y, 40, 34);
            var t = UIKit.Label(tag.transform, (line + 1).ToString(), 22, Palette.TextDark, TextAnchor.MiddleCenter, FontKind.Bold);
            ((RectTransform)t.transform).Stretch();
        }

        void SetReel(Reel reel, float position)
        {
            reel.Position = position;
            var strip = SlotMachine.Strips[reel.Index];
            int top = Mathf.FloorToInt(position);
            float f = position - top;
            for (int k = 0; k < reel.Cells.Length; k++)
            {
                int i = ((top + k - 1) % strip.Length + strip.Length) % strip.Length;
                reel.Cells[k].sprite = ProceduralArt.SlotSymbol(strip[i]);
                ((RectTransform)reel.Cells[k].transform).anchoredPosition = new Vector2(0, CellH * (2 - k) + f * CellH);
            }
        }

        void ChangeBet(int delta)
        {
            if (spinning) return;
            betIndex = Mathf.Clamp(betIndex + delta, 0, SlotMachine.BetOptions.Length - 1);
            betText.text = ChipFormat.Short(Bet);
            minus.Interactable = betIndex > 0;
            plus.Interactable = betIndex < SlotMachine.BetOptions.Length - 1;
            if (delta != 0) Sfx.Play(SfxId.Chip, 0.7f, 0.08f);
        }

        long Bet => SlotMachine.BetOptions[betIndex];

        void Spin()
        {
            if (spinning) return;
            long bet = Bet;
            long before = App.I.Wallet.Balance;
            if (!App.I.Wallet.TryDebit(bet))
            {
                Sfx.Play(SfxId.Error);
                if (autoSpin)
                {
                    autoSpin = false;
                    ShowAuto();
                }
                // Step down to a bet the player can afford, or point them to the cashier.
                while (betIndex > 0 && SlotMachine.BetOptions[betIndex] > App.I.Wallet.Balance) betIndex--;
                ChangeBet(0);
                if (App.I.Wallet.Balance < SlotMachine.BetOptions[0])
                    App.I.UI.Modal<ConfirmModal>(m => m.Setup(Loc.T("OUT OF CHIPS"), Loc.T("You need at least 10 chips to spin."), Loc.T("GET CHIPS"),
                        () => App.I.UI.Modal<ShopModal>(s => s.StartTab = ShopTab.Chips), Loc.T("LATER")));
                else App.I.UI.Toast(Loc.T("Bet lowered to what you can cover."), Palette.TextMuted, Icon.Coin);
                return;
            }
            App.I.Save.lastBet = bet;
            App.I.Intensity.OnBetPlaced(bet, SlotMachine.BetOptions[0], SlotMachine.BetOptions[SlotMachine.BetOptions.Length - 1], before);
            var result = SlotMachine.Spin(App.I.Rng, bet);
            StartCoroutine(SpinRoutine(result));
        }

        IEnumerator SpinRoutine(SpinResult result)
        {
            spinning = true;
            spin.Interactable = false;
            minus.Interactable = plus.Interactable = false;
            if (showWins != null) StopCoroutine(showWins);
            ClearLines();
            banner.enabled = false;
            winText.text = "";
            winText.color = Color.white;
            Sfx.Play(SfxId.Whoosh);

            var routines = new IEnumerator[reels.Length];
            for (int r = 0; r < reels.Length; r++) routines[r] = SpinReel(reels[r], result.Stops[r], 1.1f + r * 0.32f, 3 + r);
            yield return Tween.All(this, routines);

            long net = result.TotalPayout - result.Bet;
            App.I.Wallet.Credit(result.TotalPayout);
            App.I.RecordWager(Casino.Core.Player.GameKind.Slots, result.Bet, net);
            App.I.Intensity.OnRoundResult(net, false);

            if (result.TotalPayout > 0)
            {
                showWins = StartCoroutine(ShowWins(result));
                bool realWin = result.TotalPayout > result.Bet;
                if (realWin)
                {
                    float multiple = result.TotalPayout / (float)result.Bet;
                    yield return CountUp(result.TotalPayout, multiple >= 15f ? 1.6f : 0.7f);
                    if (multiple >= 15f)
                    {
                        Sfx.Play(SfxId.BigWin);
                        Confetti.Burst(fx, new Vector2(0, 100), multiple >= 50f ? 120 : 80, true);
                        yield return Banner(multiple >= 50f ? Loc.T("MEGA WIN!") : Loc.T("BIG WIN!"));
                    }
                    else
                    {
                        Sfx.Play(SfxId.Win);
                        Confetti.Burst(fx, new Vector2(0, -60), 30, true);
                    }
                }
                else
                {
                    // Less than (or equal to) the bet: part of the stake back. Said plainly, no fanfare.
                    Sfx.Play(SfxId.Tick);
                    winText.color = Palette.TextMuted;
                    winText.text = Loc.F("RETURNED {0}", ChipFormat.Full(result.TotalPayout));
                }
            }
            else
            {
                winText.color = Palette.TextMuted;
                winText.text = Loc.T("NO WIN");
            }

            spinning = false;
            spin.Interactable = true;
            ChangeBet(0);
            if (leaveAfterSpin) App.I.UI.Show<LobbyScreen>();
            else if (autoSpin) StartCoroutine(AutoNext(result.TotalPayout > result.Bet ? 1.2f : 0.6f));
        }

        IEnumerator SpinReel(Reel reel, int stop, float duration, int turns)
        {
            int len = SlotMachine.Strips[reel.Index].Length;
            float start = Mathf.Round(reel.Position);
            int startI = (int)start;
            int distance = ((startI - stop) % len + len) % len;
            float end = start - turns * len - distance;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - u, 3f);
                SetReel(reel, Mathf.Lerp(start, end, eased) - Mathf.Sin(u * Mathf.PI) * 0.15f * u);
                yield return null;
            }
            // A small bounce as the reel locks in.
            for (float b = 0f; b < 0.16f; b += Time.unscaledDeltaTime)
            {
                SetReel(reel, end - Mathf.Sin(b / 0.16f * Mathf.PI) * 0.12f);
                yield return null;
            }
            SetReel(reel, ((int)end % len + len) % len);
            Sfx.Play(SfxId.Chip, 0.9f, 0.05f);
        }

        IEnumerator CountUp(long amount, float seconds)
        {
            winText.color = Palette.Gold;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                winText.text = ChipFormat.Full((long)(amount * Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            winText.text = "+" + ChipFormat.Full(amount);
        }

        IEnumerator Banner(string text)
        {
            banner.text = text;
            banner.enabled = true;
            banner.transform.localScale = Vector3.one * 0.3f;
            yield return Tween.Scale(banner.transform, Vector3.one, 0.5f);
            yield return Tween.Wait(1.4f);
            banner.enabled = false;
        }

        // ------------------------------------------------------------------ paylines

        void ClearLines()
        {
            for (int i = lineLayer.childCount - 1; i >= 0; i--) Destroy(lineLayer.GetChild(i).gameObject);
        }

        IEnumerator ShowWins(SpinResult result)
        {
            // First all winning lines together, then each one in turn.
            DrawLines(result, -1);
            yield return Tween.Wait(1.6f);
            while (true)
            {
                foreach (var w in result.Lines)
                {
                    DrawLines(result, w.Line);
                    yield return Tween.Wait(1.2f);
                }
                if (result.Scatters >= 3)
                {
                    DrawLines(result, -2);
                    yield return Tween.Wait(1.2f);
                }
                if (result.Lines.Count == 0 && result.Scatters < 3) yield break;
            }
        }

        void DrawLines(SpinResult result, int only)
        {
            ClearLines();
            foreach (var w in result.Lines)
            {
                if (only >= 0 && w.Line != only) continue;
                if (only == -2) continue;
                Color col = LineColors[w.Line];
                var rows = SlotMachine.Paylines[w.Line];
                for (int r = 0; r < SlotMachine.Reels - 1; r++)
                    Segment(new Vector2(ReelX(r), RowY(rows[r])), new Vector2(ReelX(r + 1), RowY(rows[r + 1])), col);
                for (int r = 0; r < w.Count; r++) Frame(new Vector2(ReelX(r), RowY(rows[r])), col);
                if (only >= 0)
                    winText.text = Loc.F("LINE {0}: {1} × {2} = {3}", w.Line + 1, w.Count, Loc.Upper(SlotSymbolNames.Of(w.Symbol)), ChipFormat.Full(w.Payout));
            }
            if (only == -2 || (only == -1 && result.Scatters >= 3))
            {
                for (int r = 0; r < SlotMachine.Reels; r++)
                    for (int row = 0; row < SlotMachine.Rows; row++)
                        if (result.Grid[r, row] == SlotSymbol.Scatter) Frame(new Vector2(ReelX(r), RowY(row)), Palette.Gold);
                if (only == -2) winText.text = Loc.F("{0} STARS = {1}", result.Scatters, ChipFormat.Full(result.ScatterPayout));
            }
        }

        void Segment(Vector2 a, Vector2 b, Color col)
        {
            var img = UIKit.Image(lineLayer, "Seg", null, col);
            var rt = (RectTransform)img.transform;
            Vector2 d = b - a;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(d.magnitude + 8f, 9f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            UIKit.TextShadow(img, -2f, 0.5f);
        }

        void Frame(Vector2 at, Color col)
        {
            var frame = UIKit.Image(lineLayer, "Frame", ProceduralArt.RoundOutline, col);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = ProceduralArt.RoundRadiusPx / 18f;
            ((RectTransform)frame.transform).Center(at.x, at.y, CellW - 6, CellH - 6);
            frame.gameObject.AddComponent<Pulse>().Amount = 0.05f;
        }

        // ------------------------------------------------------------------ leaving / tour

        void Leave()
        {
            // Mid-spin (auto spin leaves little time between spins), the reels finish and then we go.
            if (spinning) leaveAfterSpin = true;
            else App.I.UI.Show<LobbyScreen>();
        }

        public override bool OnBack()
        {
            Leave();
            return true;
        }

        void OnDestroy()
        {
            if (seat == null) return;
            seat.Dispose();
            var next = App.I != null ? App.I.UI.Current : null;
            if (seat.World != null && next != this && (next == null || !next.ShowsWorld)) seat.World.SetVisible(false);
        }

        void Update()
        {
            seat?.Tick();
#if UNITY_STANDALONE || UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Space) && !App.I.UI.HasModal) Spin();
#endif
        }

        internal void TourSpin() => Spin();
        internal bool TourSpinning => spinning;
    }

    /// <summary>What every symbol pays, and the machine's exact payback.</summary>
    /// <summary>The slot symbols' names, in the player's language.</summary>
    public static class SlotSymbolNames
    {
        static readonly string[] English =
        {
            Loc.N("Cherry"), Loc.N("Lemon"), Loc.N("Orange"), Loc.N("Plum"), Loc.N("Bell"), Loc.N("Bar"), Loc.N("Seven"),
            Loc.N("Diamond"), Loc.N("Ace"), Loc.N("Star"),
        };

        public static string Of(SlotSymbol s) => Loc.T(English[(int)s]);
    }

    public sealed class SlotPaytableModal : UIModal
    {
        protected override void Build()
        {
            MakePanel(1500, 900, Loc.T("PAYTABLE"));
            AddCloseButton();
            var symbols = new[] { SlotSymbol.Wild, SlotSymbol.Diamond, SlotSymbol.Seven, SlotSymbol.Bar, SlotSymbol.Bell, SlotSymbol.Plum, SlotSymbol.Orange, SlotSymbol.Lemon, SlotSymbol.Cherry };
            for (int i = 0; i < symbols.Length; i++)
            {
                int col = i % 3, row = i / 3;
                float x = -470 + col * 470, y = 230 - row * 150;
                var img = UIKit.Image(Panel, "Sym", ProceduralArt.SlotSymbol(symbols[i]), Color.white);
                img.preserveAspect = true;
                ((RectTransform)img.transform).Center(x - 140, y, 120, 120);
                var pays = SlotMachine.Pays[(int)symbols[i]];
                string text = $"5×  {pays[2]}\n4×  {pays[1]}\n3×  {pays[0]}";
                var t = UIKit.Label(Panel, text, 26, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
                ((RectTransform)t.transform).Center(x + 60, y, 260, 120);
            }
            var star = UIKit.Image(Panel, "Star", ProceduralArt.SlotSymbol(SlotSymbol.Scatter), Color.white);
            star.preserveAspect = true;
            ((RectTransform)star.transform).Center(-610, -230, 110, 110);
            var st = UIKit.Label(Panel, Loc.F("STARS pay anywhere: 5 = {0}× bet, 4 = {1}× bet, 3 = {2}× bet.",
                    SlotMachine.ScatterPays[2], SlotMachine.ScatterPays[1], SlotMachine.ScatterPays[0]) + "\n" +
                Loc.T("The ACE is wild: it stands in for every symbol except the star.") + "\n" +
                Loc.T("Line pays are multiples of the line bet (total bet ÷ 10), left to right from the first reel."),
                24, Palette.TextMuted, TextAnchor.MiddleLeft, FontKind.Body);
            ((RectTransform)st.transform).Center(90, -230, 1240, 130);
            var rtp = UIKit.Label(Panel, Loc.F("PAYBACK: {0}% over the long run. Every spin is independent and random.",
                    ChipFormat.Number(SlotMachine.ExactRtp() * 100, "0.0")),
                28, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)rtp.transform).Center(0, -350, 1400, 50);
        }
    }
}
