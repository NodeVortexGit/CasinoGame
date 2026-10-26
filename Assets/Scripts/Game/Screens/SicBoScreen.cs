using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Dice;
using Casino.Core.Player;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// Sic bo (<see cref="SicBoRules"/>): tap the board to place chips, then shake. The board lies on the table in the
    /// casino; the three dice tumble in the shaker at the far end.
    /// </summary>
    public sealed class SicBoScreen : UIScreen
    {
        public const int DealerTheme = 8;
        public static readonly long[] ChipValues = { 10, 50, 100, 500, 1_000, 5_000 };
        public const long MaxTotalBet = 100_000;

        /// <summary>Metres per layout unit on the table, and the table's height.</summary>
        public const float LayerScale = 0.0012f, TableHeight = 0.86f;
        /// <summary>
        /// The table is turned round in its corner: the dealer stands against the south wall and players come to it
        /// from the room (north), so nobody walks up behind the dealer. Offsets below are in the table's own frame.
        /// </summary>
        public static readonly Quaternion Turn = Quaternion.Euler(0f, 180f, 0f);

        public static readonly Vector2 BoardPos = new Vector2(0, -90), BoardSize = new Vector2(1620, 580), ShakerPos = new Vector2(0, 360);

        public sealed class SpotDef
        {
            public string Key, Label, Pays;
            public SicBoBet Bet;
            public Vector2 Center, Size;
            public Color Color;
            public int DiceCount, DiceFace;   // doubles and triples show their dice
        }

        sealed class Spot
        {
            public SicBoBet Bet;
            public RectTransform Rect;
            public ChipStack Stack;
        }

        readonly Dictionary<string, Spot> spots = new Dictionary<string, Spot>();
        readonly List<(string key, long amount)> placed = new List<(string, long)>();
        List<(string key, long amount)> lastRound = new List<(string, long)>();
        readonly List<UIButton> chipButtons = new List<UIButton>();
        readonly DieView[] dice = new DieView[3];
        RectTransform layout, fx, historyRow;
        WorldTable seat;
        Text totalText, resultText;
        UIButton rollButton, undoButton, clearButton, rebetButton;
        long chip = ChipValues[0];
        bool rolling;
        StaffPortrait dealer;
        SpeechBubble bubble;
        readonly List<int[]> history = new List<int[]>();

        public override bool ShowsWorld => seat != null;

        static string Key(SicBoBetKind kind, int number) => kind + "_" + number;

        protected override void Build()
        {
            var world = CasinoWorld.Instance;
            if (world != null && world.TrySicBo(out var c, out var dealerFigure))
            {
                // Leaning over the board: it fills the width (the dealer and shaker sit at the far end).
                Vector3 eye = c + Turn * new Vector3(0f, 1.75f, -1.35f), stand = c + Turn * new Vector3(0f, 0f, -1.7f);
                seat = WorldTable.Sit(Content, eye, c + Turn * new Vector3(0f, 0.8f, 0.15f), 44f, 82f, new Vector2(stand.x, stand.z),
                    Turn.eulerAngles.y, c + Vector3.up * TableHeight, Turn * Quaternion.Euler(90f, 0f, 0f), LayerScale, dealerFigure, true);
            }
            if (seat != null)
            {
                layout = seat.Layer;
                world.ShowSicBoDecor(false);
            }
            else
            {
                var felt = UIKit.Image(Root, "Felt", ProceduralArt.Felt(3), Color.white);
                ((RectTransform)felt.transform).Stretch(-40, -40, -40, -40);
                var vig = UIKit.Image(Root, "Vignette", ProceduralArt.Vignette, Color.white);
                ((RectTransform)vig.transform).Stretch(-60, -60, -60, -60);
                layout = UIKit.NewRect("Layout", Content).Center(0, 40, 1920, 1080);
                layout.localScale = Vector3.one * 0.82f;
                dealer = Staff.Portrait(Content, 150, StaffRole.Dealer, DealerTheme);
                dealer.Rect.Center(-820, 330, 150, 150);
                dealer.Prewarm();
                bubble = SpeechBubble.Create(Content, 460, 110);
                ((RectTransform)bubble.transform).Center(-500, 360, 460, 110);
            }

            TopBar.Create(Content, Loc.T("SIC BO"), Icon.Back, Leave);
            PaintShaker(layout, out dice[0], out dice[1], out dice[2]);
            var board = PaintBoard(layout);
            foreach (var def in Layout())
            {
                var rt = PaintSpot(board, def);
                var stack = ChipStack.Create(rt, ChipValues, 52f);
                stack.Rect.Center(0, -6, 52, 52);
                spots[def.Key] = new Spot { Bet = def.Bet, Rect = rt, Stack = stack };
                string key = def.Key;
                rt.gameObject.AddComponent<Button>().onClick.AddListener(() => Place(key, chip));
            }
            historyRow = UIKit.NewRect("History", Content).At(0f, 1f, 40, -TopBar.Height - 50, 700, 54, 0f, 0.5f);
            BuildControls();
            fx = UIKit.NewRect("Fx", Content).Stretch();
            App.I.Progress.StreakExtended += OnStreakExtended;
            App.I.Intensity.Baseline = 0.2f;
            Refresh();
            Say(Loc.T("Welcome to sic bo! Big, small, or something bolder?"), 3.5f);
        }

        // ------------------------------------------------------------------ board

        /// <summary>Every place to bet, from the long shots at the far end to Small and Big at your end.</summary>
        public static List<SpotDef> Layout()
        {
            var list = new List<SpotDef>();
            float left = -BoardSize.x * 0.5f, top = BoardSize.y * 0.5f;
            Color red = Palette.Hex("A3122E"), blue = Palette.Hex("1E4FA8"), green = Palette.Hex("0B5A33"), gold = Palette.Hex("8A6A12");
            void Add(SicBoBetKind kind, int number, string label, string pays, float x, float y, float w, float h, Color color, int diceCount = 0) =>
                list.Add(new SpotDef
                {
                    Key = Key(kind, number), Bet = new SicBoBet(kind, number, 0), Label = label, Pays = pays,
                    Center = new Vector2(left + x + w * 0.5f, top - y - h * 0.5f), Size = new Vector2(w - 8, h - 8), Color = color,
                    DiceCount = diceCount, DiceFace = number,
                });
            // Totals 4-17
            float tw = BoardSize.x / 14f;
            for (int n = 4; n <= 17; n++)
                Add(SicBoBetKind.Total, n, n.ToString(), SicBoRules.TotalPays(n) + ":1", (n - 4) * tw, 0, tw, 120, n % 2 == 0 ? green : Palette.Hex("0D6E3F"));
            // Doubles, any triple, triples
            float dw = (BoardSize.x - 180f) / 12f;
            for (int f = 1; f <= 6; f++)
                Add(SicBoBetKind.Double, f, "", SicBoRules.DoublePays + ":1", (f - 1) * dw, 120, dw, 130, blue, 2);
            Add(SicBoBetKind.AnyTriple, 0, Tr.T("ANY TRIPLE"), SicBoRules.AnyTriplePays + ":1", 6 * dw, 120, 180, 130, gold);
            for (int f = 1; f <= 6; f++)
                Add(SicBoBetKind.Triple, f, "", SicBoRules.TriplePays + ":1", 6 * dw + 180 + (f - 1) * dw, 120, dw, 130, red, 3);
            // Single numbers
            float sw = BoardSize.x / 6f;
            for (int f = 1; f <= 6; f++)
                Add(SicBoBetKind.Single, f, "", "1:1  2:1  3:1", (f - 1) * sw, 250, sw, 130, green, 1);
            // Small, odd, even, big
            float[] w = { 0.3f, 0.2f, 0.2f, 0.3f };
            SicBoBetKind[] kinds = { SicBoBetKind.Small, SicBoBetKind.Odd, SicBoBetKind.Even, SicBoBetKind.Big };
            string[] sub = { "4-10  •  1:1", "1:1", "1:1", "11-17  •  1:1" };
            float x0 = 0;
            for (int i = 0; i < 4; i++)
            {
                Add(kinds[i], 0, SicBoRules.Label(kinds[i], 0), sub[i], x0, 380, BoardSize.x * w[i], 200, i == 0 || i == 3 ? red : blue);
                x0 += BoardSize.x * w[i];
            }
            return list;
        }

        public static RectTransform PaintBoard(RectTransform parent)
        {
            var b = UIKit.NewRect("Board", parent).Center(BoardPos.x, BoardPos.y, BoardSize.x, BoardSize.y);
            var bg = UIKit.Panel(b, "BoardBg", new Color(0.02f, 0.2f, 0.11f, 0.65f), 18f);
            ((RectTransform)bg.transform).Stretch(-14, -14, -14, -14);
            UIKit.Outline((RectTransform)bg.transform, Palette.WithAlpha(Palette.Gold, 0.75f), 18f);
            return b;
        }

        public static RectTransform PaintSpot(RectTransform board, SpotDef def)
        {
            var bg = UIKit.Panel(board, "Spot " + def.Key, Palette.WithAlpha(def.Color, 0.85f), 10f, true);
            var rt = ((RectTransform)bg.transform).Center(def.Center.x, def.Center.y, def.Size.x, def.Size.y);
            UIKit.Outline(rt, Palette.WithAlpha(Color.white, 0.3f), 10f);
            bool big = def.Size.y > 150;
            if (def.DiceCount > 0)
            {
                float d = Mathf.Min(46f, def.Size.x / (def.DiceCount + 0.6f));
                for (int i = 0; i < def.DiceCount; i++)
                {
                    var die = DieView.Create(rt, d, Palette.Hex("FFF8EC"), Palette.Hex("17141C"));
                    die.Set(def.DiceFace);
                    die.Rect.Center((i - (def.DiceCount - 1) / 2f) * (d + 6), 14, d, d);
                }
            }
            else
            {
                var t = UIKit.Label(rt, def.Label, big ? 54 : def.Label.Length > 3 ? 26 : 40, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)t.transform).Stretch(4, big ? 44 : 30, 4, 0);
            }
            var p = UIKit.Label(rt, def.Pays, big ? 26 : 20, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)p.transform).At(0.5f, 0f, 0, big ? 28 : 18, def.Size.x, 28);
            return rt;
        }

        /// <summary>The shaker at the far end of the table: a gold-rimmed dish with the three dice in it.</summary>
        public static RectTransform PaintShaker(RectTransform parent, out DieView a, out DieView b, out DieView c)
        {
            var rim = UIKit.Image(parent, "Shaker", ProceduralArt.Circle, Palette.Gold);
            var rt = ((RectTransform)rim.transform).Center(ShakerPos.x, ShakerPos.y, 330, 330);
            var dish = UIKit.Image(rt, "Dish", ProceduralArt.Circle, Palette.Hex("2A0F18"));
            ((RectTransform)dish.transform).Center(0, 0, 306, 306);
            a = DieView.Create(parent, 90, Palette.Hex("FFF8EC"), Palette.Hex("C0122F"));
            b = DieView.Create(parent, 90, Palette.Hex("FFF8EC"), Palette.Hex("17141C"));
            c = DieView.Create(parent, 90, Palette.Hex("FFF8EC"), Palette.Hex("17141C"));
            a.Rect.Center(ShakerPos.x - 70, ShakerPos.y + 30, 90, 90);
            b.Rect.Center(ShakerPos.x + 70, ShakerPos.y + 40, 90, 90);
            c.Rect.Center(ShakerPos.x, ShakerPos.y - 70, 90, 90);
            a.Set(4);
            b.Set(2);
            c.Set(6);
            return rt;
        }

        // ------------------------------------------------------------------ betting

        long TotalBet()
        {
            long sum = 0;
            foreach (var s in spots.Values) sum += s.Bet.Amount;
            return sum;
        }

        void Place(string key, long amount, bool quiet = false)
        {
            if (rolling) return;
            long total = TotalBet();
            if (total + amount > App.I.Wallet.Balance)
            {
                Sfx.Play(SfxId.Error);
                if (App.I.Wallet.Balance < ChipValues[0]) ChipOffer.Show(Refresh);
                else App.I.UI.Toast(Loc.T("Not enough chips for that bet."), Palette.TextMuted, Icon.Coin);
                return;
            }
            if (total + amount > MaxTotalBet)
            {
                Sfx.Play(SfxId.Error);
                App.I.UI.Toast(Loc.F("Table limit is {0} per roll.", ChipFormat.Short(MaxTotalBet)), Palette.TextMuted, Icon.Coin);
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
            if (rolling || placed.Count == 0) return;
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
            if (rolling) return;
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
            if (rolling || lastRound.Count == 0) return;
            ClearBets();
            foreach (var (key, amount) in lastRound) Place(key, amount, true);
            Sfx.Play(SfxId.ChipStack);
        }

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
            rollButton = UIKit.Button(Content, Loc.T("SHAKE"), ButtonStyle.Gold, Roll, 46, Icon.Play);
            rollButton.Rect.At(0.5f, 0f, 875, 95, 180, 120);

            totalText = UIKit.Label(Content, "", 30, Palette.GoldLight, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)totalText.transform).At(0f, 0f, 40, 200, 760, 44, 0f, 0.5f);
            UIKit.TextShadow(totalText, -2f, 0.9f);
            resultText = UIKit.Label(Content, "", 84, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)resultText.transform).Center(0, 260, 1200, 110);
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
            rollButton.Interactable = !rolling && total > 0;
            undoButton.Interactable = !rolling && placed.Count > 0;
            clearButton.Interactable = !rolling && placed.Count > 0;
            rebetButton.Interactable = !rolling && lastRound.Count > 0 && placed.Count == 0;
        }

        // ------------------------------------------------------------------ the roll

        void Roll()
        {
            if (rolling) return;
            long total = TotalBet();
            if (total <= 0) return;
            if (App.I.Energy.IsEmpty)
            {
                ExhaustedPrompt.Show();
                return;
            }
            long before = App.I.Wallet.Balance;
            if (!App.I.Wallet.TryDebit(total))
            {
                Sfx.Play(SfxId.Error);
                return;
            }
            App.I.Intensity.OnBetPlaced(total, ChipValues[0], MaxTotalBet, before);
            StartCoroutine(RollRoutine(SicBoRules.Roll(App.I.Rng), total));
        }

        IEnumerator RollRoutine(int[] roll, long total)
        {
            rolling = true;
            Refresh();
            resultText.text = "";
            Say(Loc.T("No more bets!"), 1.6f);
            Sfx.Play(SfxId.Shuffle);
            Vector2[] rest = { ShakerPos + new Vector2(-75, 35), ShakerPos + new Vector2(75, 45), ShakerPos + new Vector2(0, -70) };
            var routines = new IEnumerator[3];
            for (int i = 0; i < 3; i++)
            {
                Vector2 from = dice[i].Rect.anchoredPosition;
                Vector2 to = rest[i] + new Vector2(Random.Range(-18f, 18f), Random.Range(-18f, 18f));
                routines[i] = dice[i].Tumble(from, to, 1.4f + i * 0.15f, roll[i], 900f);
            }
            yield return Tween.All(this, routines);
            Sfx.Play(SfxId.Tick);

            long back = 0;
            var winners = new List<Spot>();
            foreach (var s in spots.Values)
            {
                if (s.Bet.Amount <= 0) continue;
                long r = SicBoRules.Return(s.Bet, roll);
                back += r;
                if (r > 0) winners.Add(s);
            }
            App.I.Wallet.Credit(back);
            long net = back - total;
            App.I.RecordWager(GameKind.SicBo, total, net);
            App.I.Intensity.OnRoundResult(net, false);
            int sum = roll[0] + roll[1] + roll[2];
            bool triple = roll[0] == roll[1] && roll[1] == roll[2];
            resultText.color = triple ? Palette.Gold : Color.white;
            resultText.text = triple ? Loc.F("TRIPLE {0}!", roll[0]) : sum + "  " + (sum <= 10 ? Loc.T("SMALL") : Loc.T("BIG"));
            AddHistory(roll);
            foreach (var w in winners) StartCoroutine(Tween.Punch(w.Rect, 0.2f, 0.5f));
            if (spots.TryGetValue(Key(SicBoBetKind.Total, sum), out var totalSpot)) totalSpot.Rect.gameObject.AddComponent<Pulse>().Amount = 0.1f;

            if (net > 0)
            {
                bool big = net >= total * 10;
                Sfx.Play(big ? SfxId.BigWin : SfxId.Win);
                Confetti.Burst(fx, new Vector2(0, 150), big ? 90 : 40, true);
                Say(big ? Loc.T("What a hit! Congratulations!") : Loc.F("Winner! You win {0}.", ChipFormat.Full(back)));
                totalText.text = Loc.F("WIN  +{0}", ChipFormat.Full(net));   // the profit, as baccarat shows it
            }
            else if (back > 0)
            {
                Sfx.Play(SfxId.Tick);
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
                yield return Tween.Wait(3f);
            }
            if (totalSpot != null)
            {
                var pulse = totalSpot.Rect.GetComponent<Pulse>();
                if (pulse != null) Destroy(pulse);
                totalSpot.Rect.localScale = Vector3.one;
            }
            lastRound = new List<(string, long)>(placed);
            rolling = false;
            ClearBets();
            Say(Loc.T("Place your bets, please."));
            if (App.I.Wallet.Balance < ChipValues[0]) ChipOffer.Show(Refresh);
        }

        void AddHistory(int[] roll)
        {
            history.Insert(0, roll);
            if (history.Count > 6) history.RemoveAt(history.Count - 1);
            for (int i = historyRow.childCount - 1; i >= 0; i--) Destroy(historyRow.GetChild(i).gameObject);
            for (int i = 0; i < history.Count; i++)
            {
                var group = UIKit.Panel(historyRow, "Roll", new Color(0, 0, 0, 0.55f), 14f);
                ((RectTransform)group.transform).At(0f, 0.5f, i * 116, 0, 108, 46, 0f, 0.5f);
                for (int k = 0; k < 3; k++)
                {
                    var d = DieView.Create(group.transform, 30, Palette.Hex("FFF8EC"), Palette.Hex("17141C"));
                    d.Set(history[i][k]);
                    d.Rect.Center(-34 + k * 34, 0, 30, 30);
                }
            }
        }

        void Say(string line, float hold = 2.6f)
        {
            if (seat != null) seat.Say(line, hold);
            else bubble.Say(line, hold, dealer, Staff.IsFemale(App.I.Staff, StaffRole.Dealer, DealerTheme) ? VoicePitch.High : VoicePitch.Low);
        }

        string streakLine;

        void OnStreakExtended() => streakLine = DealerLines.StreakExtended(App.I.Progress.Streak.Count, App.I.Rng);

        void Update() => seat?.Tick();

        void OnDestroy()
        {
            if (App.I != null) App.I.Progress.StreakExtended -= OnStreakExtended;
            if (seat == null) return;
            seat.Dispose();
            if (seat.World != null) seat.World.ShowSicBoDecor(true);
            var next = App.I != null ? App.I.UI.Current : null;
            if (seat.World != null && next != this && (next == null || !next.ShowsWorld)) seat.World.SetVisible(false);
        }

        void Leave()
        {
            if (rolling)
            {
                App.I.UI.Toast(Loc.T("Wait for the dice to land."), Palette.TextMuted, Icon.Clock);
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
            Place(Key(SicBoBetKind.Big, 0), 50);
            Place(Key(SicBoBetKind.Total, 10), 10);
            Place(Key(SicBoBetKind.Single, 6), 10);
        }

        internal void TourRoll() => Roll();
        internal bool TourRolling => rolling;
    }
}
