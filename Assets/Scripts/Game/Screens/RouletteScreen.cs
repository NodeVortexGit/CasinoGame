using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Player;
using Casino.Core.Roulette;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// European roulette (single zero): tap the board to place chips, then spin. Straight-up numbers pay 35 to 1, dozens and
    /// columns 2 to 1, red/black, odd/even and low/high 1 to 1. The house edge is the standard 2.7% on every bet.
    /// </summary>
    public sealed class RouletteScreen : UIScreen
    {
        public const int CroupierTheme = 4;
        static readonly long[] ChipValues = { 10, 50, 100, 500, 1_000, 5_000 };
        const long MaxTotalBet = 100_000;
        const float CellW = 78f, CellH = 104f;
        public const float WheelSize = 560f;

        /// <summary>Metres per layout unit when the wheel and board lie on the table in the casino.</summary>
        public const float LayerScale = 0.0019f;

        /// <summary>Layout positions (like a 1920 x 1080 screen; in the casino +y points away from the player).</summary>
        public static readonly Vector2 WheelPos = new Vector2(-560, -30), BoardPos = new Vector2(370, 70);

        public static Vector2 BoardSize => new Vector2(70 + 13 * CellW, 3 * CellH + 140);

        /// <summary>One place to bet on the board: centre and size relative to the board's centre.</summary>
        public sealed class SpotDef
        {
            public string Key, Label;
            public RouletteBet Bet;
            public Vector2 Center, Size;
            public Color Color;
        }

        sealed class Spot
        {
            public RouletteBet Bet;
            public RectTransform Rect;
            public Image Background;
            public ChipStack Stack;
        }

        readonly Dictionary<string, Spot> spots = new Dictionary<string, Spot>();
        readonly List<(string key, long amount)> placed = new List<(string, long)>();
        List<(string key, long amount)> lastRound = new List<(string, long)>();
        readonly List<int> history = new List<int>();
        RectTransform board, wheel, historyRow, fx, layout;
        WorldTable seat;
        Image ball;
        Text totalText, resultText;
        UIButton spinButton, undoButton, clearButton, rebetButton;
        readonly List<UIButton> chipButtons = new List<UIButton>();
        long chip = ChipValues[0];
        bool spinning;
        StaffPortrait croupier;
        SpeechBubble bubble;
        float wheelAngle;

        static string Key(RouletteBetKind kind, int number = 0) => kind == RouletteBetKind.Straight ? "n" + number : kind.ToString();

        public override bool ShowsWorld => seat != null;

        protected override void Build()
        {
            // At the table in the casino: you sit at the front, the wheel and board lie on the table, the real croupier
            // stands behind the wheel. (Without the casino, e.g. while it is still being built, it is drawn flat.)
            var world = CasinoWorld.Instance;
            if (world != null && world.TryRoulette(out var c, out var croupierFigure))
            {
                Vector3 eye = c + new Vector3(0.1f, 2.0f, -1.95f);
                seat = WorldTable.Sit(Content, eye, c + new Vector3(0.1f, 0.75f, 0.25f), 54f, 78f, new Vector2(c.x + 0.3f, c.z - 1.75f), 0f,
                    c + Vector3.up * TableHeight, Quaternion.Euler(90f, 0f, 0f), LayerScale, croupierFigure, true);
            }
            if (seat != null)
            {
                layout = seat.Layer;
                world.ShowRouletteDecor(false);
            }
            else
            {
                var felt = UIKit.Image(Root, "Felt", ProceduralArt.Felt(0), Color.white);
                ((RectTransform)felt.transform).Stretch(-40, -40, -40, -40);
                var vig = UIKit.Image(Root, "Vignette", ProceduralArt.Vignette, Color.white);
                ((RectTransform)vig.transform).Stretch(-60, -60, -60, -60);
                layout = Content;
            }

            TopBar.Create(Content, Loc.T("ROULETTE"), Icon.Back, Leave);
            BuildWheel();
            BuildBoard();
            BuildControls();

            if (seat == null)
            {
                croupier = Staff.Portrait(Content, 150, StaffRole.Dealer, CroupierTheme);
                croupier.Rect.Center(-860, 300, 150, 150);
                croupier.Prewarm();
                bubble = SpeechBubble.Create(Content, 460, 110);
                ((RectTransform)bubble.transform).Center(-560, 360, 460, 110);
            }

            fx = UIKit.NewRect("Fx", Content).Stretch();
            App.I.Progress.StreakExtended += OnStreakExtended;
            App.I.Intensity.Baseline = 0.2f;
            Refresh();
            Say(Loc.T("Welcome! Place your bets, please."));
        }

        // ------------------------------------------------------------------ wheel

        /// <summary>Height of the wheel and board above the floor when the table stands in the casino.</summary>
        public const float TableHeight = 0.876f;

        void BuildWheel()
        {
            wheel = PaintWheel(layout, seat == null, out ball);
            if (seat == null) historyRow = UIKit.NewRect("History", Content).Center(-560, -420, 620, 54);
            else historyRow = UIKit.NewRect("History", Content).At(0f, 1f, 40, -TopBar.Height - 50, 620, 54, 0f, 0.5f);
        }

        /// <summary>Draws the wheel (numbers and ball) at <see cref="WheelPos"/>; returns the part that turns.</summary>
        public static RectTransform PaintWheel(RectTransform parent, bool shadow, out Image ball)
        {
            var holder = UIKit.NewRect("WheelHolder", parent).Center(WheelPos.x, WheelPos.y, WheelSize, WheelSize);
            if (shadow)
            {
                var sh = UIKit.Image(holder, "Shadow", ProceduralArt.Glow, new Color(0, 0, 0, 0.6f));
                ((RectTransform)sh.transform).Center(10, -14, WheelSize * 1.2f, WheelSize * 1.2f);
            }
            var img = UIKit.Image(holder, "Wheel", ProceduralArt.RouletteWheel, Color.white);
            var wheel = ((RectTransform)img.transform).Center(0, 0, WheelSize, WheelSize);
            UIKit.OwnCanvas(img.gameObject, false);
            for (int i = 0; i < RouletteRules.Pockets; i++)
            {
                float a = GamePainters.PocketAngle(i);
                var t = UIKit.Label(wheel, RouletteRules.WheelOrder[i].ToString(), 20, Color.white, TextAnchor.MiddleCenter, FontKind.Bold);
                var rt = (RectTransform)t.transform;
                float r = WheelSize * 0.395f;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(40, 26);
                rt.anchoredPosition = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * r;
                rt.localEulerAngles = new Vector3(0, 0, -a);
            }
            ball = UIKit.Image(holder, "Ball", ProceduralArt.Ball, Color.white);
            ((RectTransform)ball.transform).Center(0, WheelSize * 0.445f, 24, 24);
            UIKit.OwnCanvas(ball.gameObject, false);
            return wheel;
        }

        static Vector2 Polar(float clockwiseDeg, float radius) =>
            new Vector2(Mathf.Sin(clockwiseDeg * Mathf.Deg2Rad), Mathf.Cos(clockwiseDeg * Mathf.Deg2Rad)) * radius;

        IEnumerator SpinWheel(int number)
        {
            int index = System.Array.IndexOf(RouletteRules.WheelOrder, number);
            float pocket = GamePainters.PocketAngle(index);
            const float duration = 6.2f, drop = 4.4f;
            float startAngle = wheelAngle;
            float wheelTurns = 360f * 2.5f;
            var brt = (RectTransform)ball.transform;
            float outer = WheelSize * 0.445f, inner = WheelSize * 0.37f;
            float relStart = 360f * 6f + Random.Range(0f, 360f);
            int lastTick = 0;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Wheel turns counter-clockwise and slows to a stop.
                wheelAngle = startAngle + wheelTurns * (1f - (1f - u) * (1f - u));
                wheel.localEulerAngles = new Vector3(0, 0, wheelAngle);
                // Ball runs clockwise relative to the wheel and lands in the winning pocket at `drop`.
                float s = Mathf.Clamp01(t / drop);
                float rel = pocket + relStart * (1f - s) * (1f - s);
                float screenAngle = rel - wheelAngle;
                float bounce = s > 0.8f && s < 1f ? Mathf.Abs(Mathf.Sin(s * 60f)) * 10f * (1f - s) * 5f : 0f;
                float radius = Mathf.Lerp(outer, inner, Mathf.SmoothStep(0f, 1f, (s - 0.72f) / 0.28f)) + bounce;
                brt.anchoredPosition = Polar(screenAngle, radius);
                // Clicks as the ball rattles over the pocket dividers.
                int tick = Mathf.FloorToInt(rel / (360f / RouletteRules.Pockets));
                if (s > 0.7f && s < 1f && tick != lastTick)
                {
                    lastTick = tick;
                    Sfx.Play(SfxId.Tick, 0.5f, 0.2f);
                    Casino.Platform.Haptics.Play(Casino.Platform.Haptic.Tick);
                }
                yield return null;
            }
            wheelAngle = Mathf.Repeat(wheelAngle, 360f);
        }

        // ------------------------------------------------------------------ board

        void BuildBoard()
        {
            board = PaintBoard(layout);
            foreach (var def in Layout())
            {
                var rt = PaintSpot(board, def, out var bg);
                var stack = ChipStack.Create(rt, ChipValues, 58f);
                stack.Rect.Center(0, 0, 58, 58);
                spots[def.Key] = new Spot { Bet = def.Bet, Rect = rt, Background = bg, Stack = stack };
                string key = def.Key;
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => Place(key, chip));
            }
        }

        /// <summary>Every place to bet: zero, the 36 numbers, columns, dozens and the even-money bets.</summary>
        public static List<SpotDef> Layout()
        {
            var list = new List<SpotDef>();
            void Add(string key, RouletteBet bet, string label, Vector2 center, Vector2 size, Color color) =>
                list.Add(new SpotDef { Key = key, Bet = bet, Label = label, Center = center, Size = size, Color = color });
            float left = -BoardSize.x * 0.5f, top = BoardSize.y * 0.5f;
            Add(Key(RouletteBetKind.Straight, 0), new RouletteBet(RouletteBetKind.Straight, 0, 0), "0",
                new Vector2(left + 35, top - 1.5f * CellH), new Vector2(68, 3 * CellH - 4), Palette.Hex("0D7A45"));
            for (int c = 0; c < 12; c++)
                for (int r = 0; r < 3; r++)
                {
                    int n = 3 * (c + 1) - r;
                    Add(Key(RouletteBetKind.Straight, n), new RouletteBet(RouletteBetKind.Straight, 0, n), n.ToString(),
                        new Vector2(left + 70 + c * CellW + CellW * 0.5f, top - r * CellH - CellH * 0.5f), new Vector2(CellW - 4, CellH - 4),
                        RouletteRules.IsRed(n) ? Palette.Hex("C0122F") : Palette.Hex("17141C"));
                }
            var columns = new[] { RouletteBetKind.Column3, RouletteBetKind.Column2, RouletteBetKind.Column1 };
            for (int r = 0; r < 3; r++)
                Add(Key(columns[r]), new RouletteBet(columns[r], 0), "2:1",
                    new Vector2(left + 70 + 12 * CellW + CellW * 0.5f, top - r * CellH - CellH * 0.5f), new Vector2(CellW - 4, CellH - 4), Palette.Hex("0B5A33"));
            var dozens = new[] { RouletteBetKind.Dozen1, RouletteBetKind.Dozen2, RouletteBetKind.Dozen3 };
            for (int d = 0; d < 3; d++)
                Add(Key(dozens[d]), new RouletteBet(dozens[d], 0), RouletteRules.Label(dozens[d]),
                    new Vector2(left + 70 + d * 4 * CellW + 2 * CellW, top - 3 * CellH - 35), new Vector2(4 * CellW - 4, 66), Palette.Hex("0B5A33"));
            var outside = new[] { RouletteBetKind.Low, RouletteBetKind.Even, RouletteBetKind.Red, RouletteBetKind.Black, RouletteBetKind.Odd, RouletteBetKind.High };
            for (int k = 0; k < outside.Length; k++)
            {
                var kind = outside[k];
                Color col = kind == RouletteBetKind.Red ? Palette.Hex("C0122F") : kind == RouletteBetKind.Black ? Palette.Hex("17141C") : Palette.Hex("0B5A33");
                Add(Key(kind), new RouletteBet(kind, 0), RouletteRules.Label(kind),
                    new Vector2(left + 70 + k * 2 * CellW + CellW, top - 3 * CellH - 105), new Vector2(2 * CellW - 4, 66), col);
            }
            return list;
        }

        /// <summary>The board's background at <see cref="BoardPos"/>.</summary>
        public static RectTransform PaintBoard(RectTransform parent)
        {
            var b = UIKit.NewRect("Board", parent).Center(BoardPos.x, BoardPos.y, BoardSize.x, BoardSize.y);
            var bg = UIKit.Panel(b, "BoardBg", new Color(0.03f, 0.25f, 0.13f, 0.6f), 18f);
            ((RectTransform)bg.transform).Stretch(-12, -12, -12, -12);
            UIKit.Outline((RectTransform)bg.transform, Palette.WithAlpha(Palette.Gold, 0.7f), 18f);
            return b;
        }

        public static RectTransform PaintSpot(RectTransform board, SpotDef def, out Image bg)
        {
            bg = UIKit.Panel(board, "Spot " + def.Key, def.Color, 8f, true);
            var rt = ((RectTransform)bg.transform).Center(def.Center.x, def.Center.y, def.Size.x, def.Size.y);
            UIKit.Outline(rt, Palette.WithAlpha(Color.white, 0.35f), 8f);
            var t = UIKit.Label(rt, def.Label, def.Label.Length > 3 ? 26 : 34, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)t.transform).Stretch();
            return rt;
        }

        long TotalBet()
        {
            long sum = 0;
            foreach (var s in spots.Values) sum += s.Bet.Amount;
            return sum;
        }

        void Place(string key, long amount, bool quiet = false)
        {
            if (spinning) return;
            long total = TotalBet();
            if (total + amount > App.I.Wallet.Balance)
            {
                Sfx.Play(SfxId.Error);
                App.I.UI.Toast(Loc.T("Not enough chips for that bet."), Palette.TextMuted, Icon.Coin);
                return;
            }
            if (total + amount > MaxTotalBet)
            {
                Sfx.Play(SfxId.Error);
                App.I.UI.Toast(Loc.F("Table limit is {0} per spin.", ChipFormat.Short(MaxTotalBet)), Palette.TextMuted, Icon.Coin);
                return;
            }
            var spot = spots[key];
            spot.Bet.Amount += amount;
            spot.Stack.SetAmount(spot.Bet.Amount);
            placed.Add((key, amount));
            if (!quiet) Sfx.Play(SfxId.Chip, 1f, 0.06f);
            Refresh();
        }

        void Undo()
        {
            if (spinning || placed.Count == 0) return;
            var (key, amount) = placed[placed.Count - 1];
            placed.RemoveAt(placed.Count - 1);
            var spot = spots[key];
            spot.Bet.Amount -= amount;
            spot.Stack.SetAmount(spot.Bet.Amount);
            Sfx.Play(SfxId.Chip, 0.6f, 0.1f);
            Refresh();
        }

        void ClearBets()
        {
            if (spinning) return;
            foreach (var s in spots.Values)
            {
                s.Bet.Amount = 0;
                s.Stack.SetAmount(0);
            }
            placed.Clear();
            Refresh();
        }

        void Rebet()
        {
            if (spinning || lastRound.Count == 0) return;
            ClearBets();
            foreach (var (key, amount) in lastRound) Place(key, amount, true);
            Sfx.Play(SfxId.ChipStack);
        }

        // ------------------------------------------------------------------ controls

        void BuildControls()
        {
            var barBg = UIKit.Panel(Content, "Controls", new Color(0.04f, 0.02f, 0.08f, 0.8f), 30f);
            ((RectTransform)barBg.transform).At(0.5f, 0f, 370, 20, 1180, 150, 0.5f, 0f);
            for (int i = 0; i < ChipValues.Length; i++)
            {
                long value = ChipValues[i];
                var b = UIKit.Button(Content, ChipFormat.Short(value), ButtonStyle.Dark, () => SelectChip(value), 30, null, FontKind.Display, 50f);
                b.Rect.At(0.5f, 0f, -130 + i * 104, 95, 96, 96);
                b.Face.sprite = ProceduralArt.Chip(i);
                b.Face.type = Image.Type.Simple;
                b.Face.color = Color.white;
                chipButtons.Add(b);
            }
            undoButton = UIKit.Button(Content, Loc.T("UNDO"), ButtonStyle.Dark, Undo, 28);
            undoButton.Rect.At(0.5f, 0f, 540, 125, 150, 60);
            clearButton = UIKit.Button(Content, Loc.T("CLEAR"), ButtonStyle.Dark, ClearBets, 28);
            clearButton.Rect.At(0.5f, 0f, 540, 60, 150, 60);
            rebetButton = UIKit.Button(Content, Loc.T("REBET"), ButtonStyle.Blue, Rebet, 28);
            rebetButton.Rect.At(0.5f, 0f, 700, 95, 150, 120);
            spinButton = UIKit.Button(Content, Loc.T("SPIN"), ButtonStyle.Gold, Spin, 50, Icon.Play);
            spinButton.Rect.At(0.5f, 0f, 875, 95, 180, 120);

            totalText = UIKit.Label(Content, "", 30, Palette.GoldLight, TextAnchor.MiddleLeft, FontKind.Semi);
            if (seat != null) ((RectTransform)totalText.transform).At(0f, 0f, 40, 200, 700, 44, 0f, 0.5f);   // clear of the croupier's lines
            else ((RectTransform)totalText.transform).At(0.5f, 1f, -180, -TopBar.Height - 30, 600, 44, 0f, 0.5f);
            if (seat != null) UIKit.TextShadow(totalText, -2f, 0.9f);
            resultText = UIKit.Label(Content, "", seat != null ? 90 : 64, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)resultText.transform).Center(seat != null ? 480 : -560, seat != null ? 300 : -345, 900, 110);
            UIKit.TextOutline(resultText, Palette.Hex("3A1500"), 3f);
            SelectChip(chip);
        }

        void SelectChip(long value)
        {
            chip = value;
            for (int i = 0; i < chipButtons.Count; i++)
                chipButtons[i].Rect.localScale = ChipValues[i] == value ? Vector3.one * 1.15f : Vector3.one * 0.92f;
        }

        void Refresh()
        {
            long total = TotalBet();
            totalText.text = total > 0 ? Loc.F("TOTAL BET  {0}", ChipFormat.Full(total)) : Loc.T("Tap the board to place chips");
            spinButton.Interactable = !spinning && total > 0;
            undoButton.Interactable = !spinning && placed.Count > 0;
            clearButton.Interactable = !spinning && placed.Count > 0;
            rebetButton.Interactable = !spinning && lastRound.Count > 0 && placed.Count == 0;
        }

        void Say(string line)
        {
            if (seat != null) seat.Say(line, 2.6f);
            else bubble.Say(line, 2.6f, croupier, Staff.IsFemale(App.I.Staff, StaffRole.Dealer, CroupierTheme) ? VoicePitch.High : VoicePitch.Low);
        }

        void Update() => seat?.Tick();

        string streakLine;

        void OnStreakExtended() => streakLine = DealerLines.StreakExtended(App.I.Progress.Streak.Count, App.I.Rng);

        void OnDestroy()
        {
            if (App.I != null) App.I.Progress.StreakExtended -= OnStreakExtended;
            if (seat == null) return;
            seat.Dispose();
            if (seat.World != null) seat.World.ShowRouletteDecor(true);
            var next = App.I != null ? App.I.UI.Current : null;
            if (seat.World != null && next != this && (next == null || !next.ShowsWorld)) seat.World.SetVisible(false);
        }

        void Spin()
        {
            if (spinning) return;
            long total = TotalBet();
            if (total <= 0) return;
            long before = App.I.Wallet.Balance;
            if (!App.I.Wallet.TryDebit(total))
            {
                Sfx.Play(SfxId.Error);
                return;
            }
            App.I.Intensity.OnBetPlaced(total, ChipValues[0], MaxTotalBet, before);
            StartCoroutine(SpinRoutine(RouletteRules.Spin(App.I.Rng), total));
        }

        IEnumerator SpinRoutine(int number, long total)
        {
            spinning = true;
            Refresh();
            resultText.text = "";
            Say(Loc.T("No more bets!"));
            Sfx.Play(SfxId.Whoosh);
            if (seat != null && seat.Staff != null)
                seat.Staff.SlideOnTable(seat.Layer.TransformPoint(WheelPos + new Vector2(-120f, 260f)), seat.Layer.TransformPoint(WheelPos + new Vector2(140f, 250f)), 0.5f);
            yield return SpinWheel(number);

            long back = 0;
            var winners = new List<Spot>();
            foreach (var s in spots.Values)
            {
                if (s.Bet.Amount <= 0) continue;
                long r = RouletteRules.Return(s.Bet, number);
                back += r;
                if (r > 0) winners.Add(s);
            }
            App.I.Wallet.Credit(back);
            long net = back - total;
            App.I.RecordWager(GameKind.Roulette, total, net, spots[Key(RouletteBetKind.Straight, number)].Bet.Amount > 0);
            App.I.Intensity.OnRoundResult(net, false);

            string colour = number == 0 ? Loc.T("GREEN") : RouletteRules.IsRed(number) ? Loc.T("RED") : Loc.T("BLACK");
            string said = number == 0 ? Loc.T("green") : RouletteRules.IsRed(number) ? Loc.T("red") : Loc.T("black");
            resultText.color = number == 0 ? Palette.Hex("7CFC9A") : RouletteRules.IsRed(number) ? Palette.Hex("FF6A7E") : Color.white;
            resultText.text = number + "  " + colour;
            AddHistory(number);
            var numberSpot = spots[Key(RouletteBetKind.Straight, number)];
            numberSpot.Rect.gameObject.AddComponent<Pulse>().Amount = 0.12f;
            foreach (var w in winners) StartCoroutine(Tween.Punch(w.Rect, 0.2f, 0.5f));

            if (net > 0)
            {
                bool big = net >= total * 10;
                Sfx.Play(big ? SfxId.BigWin : SfxId.Win);
                Confetti.Burst(fx, seat != null ? new Vector2(0, 150) : new Vector2(-560, 0), big ? 90 : 40, true);
                Say(big ? Loc.T("What a hit! Congratulations!") : Loc.F("Winner! You win {0}.", ChipFormat.Full(back)));
                totalText.text = Loc.F("WIN  +{0}", ChipFormat.Full(net));   // the profit, as baccarat shows it
            }
            else if (back > 0)
            {
                Sfx.Play(SfxId.Tick);
                Say(Loc.F("{0} {1}. {2} comes back to you.", number, said, ChipFormat.Full(back)));
                totalText.text = Loc.F("RETURNED  {0}", ChipFormat.Full(back));
            }
            else
            {
                Sfx.Play(SfxId.Lose);
                Say(DealerLines.Pick(DealerMoment.PlayerLose, App.I.Rng));
                totalText.text = Loc.T("No win this time");
            }

            yield return Tween.Wait(2.4f);
            if (streakLine != null)
            {
                Say(streakLine);
                streakLine = null;
                yield return Tween.Wait(3.5f);
            }
            var pulse = numberSpot.Rect.GetComponent<Pulse>();
            if (pulse != null) Destroy(pulse);
            numberSpot.Rect.localScale = Vector3.one;
            lastRound = new List<(string, long)>(placed);
            spinning = false;
            ClearBets();
            Say(Loc.T("Place your bets, please."));
        }

        void AddHistory(int number)
        {
            history.Insert(0, number);
            if (history.Count > 10) history.RemoveAt(history.Count - 1);
            for (int i = historyRow.childCount - 1; i >= 0; i--) Destroy(historyRow.GetChild(i).gameObject);
            for (int i = 0; i < history.Count; i++)
            {
                int n = history[i];
                var dot = UIKit.Image(historyRow, "H" + i, ProceduralArt.Circle,
                    n == 0 ? Palette.Hex("0D7A45") : RouletteRules.IsRed(n) ? Palette.Hex("C0122F") : Palette.Hex("17141C"));
                ((RectTransform)dot.transform).Center(-270 + i * 60, 0, i == 0 ? 56 : 48, i == 0 ? 56 : 48);
                var t = UIKit.Label(dot.transform, n.ToString(), i == 0 ? 26 : 22, Color.white, TextAnchor.MiddleCenter, FontKind.Bold);
                ((RectTransform)t.transform).Stretch();
            }
        }

        // ------------------------------------------------------------------ leaving / tour

        void Leave()
        {
            if (spinning)
            {
                App.I.UI.Toast(Loc.T("Wait for the ball to land."), Palette.TextMuted, Icon.Clock);
                return;
            }
            App.I.UI.Show<LobbyScreen>();
        }

        public override bool OnBack()
        {
            Leave();
            return true;
        }

        internal void TourBet()
        {
            Place(Key(RouletteBetKind.Red), 50);
            Place(Key(RouletteBetKind.Straight, 17), 10);
            Place(Key(RouletteBetKind.Dozen2), 20);
        }

        internal void TourSpin() => Spin();
        internal bool TourSpinning => spinning;
    }
}
