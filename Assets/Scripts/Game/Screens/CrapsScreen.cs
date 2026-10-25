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
    /// Craps (<see cref="CrapsTable"/>): bet the Pass Line on the come-out and roll. A 7 or 11 wins straight away, 2, 3 or
    /// 12 loses; anything else becomes the point, and you keep rolling until you hit it again (win) or a 7 (lose). Odds
    /// behind the Pass Line pay true odds. The table guides you through each step.
    /// </summary>
    public sealed class CrapsScreen : UIScreen
    {
        public const int DealerTheme = 9;
        public static readonly long[] ChipValues = { 10, 50, 100, 500, 1_000 };
        public const long MaxPerSpot = 20_000;
        public const float LayerScale = 0.0016f, TableHeight = 0.9f;
        public static readonly Vector2 BoardPos = new Vector2(0, 40), BoardSize = new Vector2(1940, 700);

        public sealed class SpotDef
        {
            public string Key, Label, Pays;
            public CrapsBetKind Kind;
            public int Number;
            public Vector2 Center, Size;
            public Color Color;
        }

        sealed class Spot
        {
            public SpotDef Def;
            public RectTransform Rect;
            public ChipStack Stack;
            public Image Background;
        }

        CrapsTable game;
        WorldTable seat;
        RectTransform layout, fx, puck;
        Text puckText, hint, resultText, totalText;
        readonly Dictionary<string, Spot> spots = new Dictionary<string, Spot>();
        readonly List<UIButton> chipButtons = new List<UIButton>();
        readonly List<(CrapsBetKind kind, int number, long amount)> placedThisRoll = new List<(CrapsBetKind, int, long)>();
        DieView die1, die2;
        UIButton rollButton, undoButton, clearButton;
        long chip = ChipValues[0];
        bool rolling;
        StaffPortrait dealer;
        SpeechBubble bubble;

        public override bool ShowsWorld => seat != null;

        static string Key(CrapsBetKind kind, int number = 0) => kind == CrapsBetKind.Place ? "place" + number : kind.ToString();

        protected override void Build()
        {
            game = new CrapsTable(App.I.Rng, App.I.Wallet);
            RestoreLayout();
            var world = CasinoWorld.Instance;
            if (world != null && world.TryCraps(out var c, out var dealerFigure))
            {
                // Close to the rail, looking down the layout: the whole board spans the screen, the stickman beyond it.
                Vector3 eye = c + new Vector3(0f, 2.0f, -1.45f);
                seat = WorldTable.Sit(Content, eye, c + new Vector3(0f, 0.9f, 0.4f), 56f, 96f, new Vector2(c.x, c.z - 1.9f), 0f,
                    c + Vector3.up * TableHeight, Quaternion.Euler(90f, 0f, 0f), LayerScale, dealerFigure, true);
            }
            if (seat != null)
            {
                layout = seat.Layer;
                world.ShowCrapsDecor(false);
            }
            else
            {
                var felt = UIKit.Image(Root, "Felt", ProceduralArt.Felt(0), Color.white);
                ((RectTransform)felt.transform).Stretch(-40, -40, -40, -40);
                var vig = UIKit.Image(Root, "Vignette", ProceduralArt.Vignette, Color.white);
                ((RectTransform)vig.transform).Stretch(-60, -60, -60, -60);
                layout = UIKit.NewRect("Layout", Content).Center(0, 10, 1920, 1080);
                layout.localScale = Vector3.one * 0.78f;
                dealer = Staff.Portrait(Content, 150, StaffRole.Dealer, DealerTheme);
                dealer.Rect.Center(-820, 330, 150, 150);
                dealer.Prewarm();
                bubble = SpeechBubble.Create(Content, 460, 110);
                ((RectTransform)bubble.transform).Center(-500, 360, 460, 110);
            }

            TopBar.Create(Content, Loc.T("CRAPS"), Icon.Back, Leave);
            var board = PaintBoard(layout);
            foreach (var def in Layout())
            {
                var rt = PaintSpot(board, def, out var bg);
                var stack = ChipStack.Create(rt, ChipValues, 56f);
                stack.Rect.Center(def.Size.x * 0.5f - 46f, 0, 56, 56);
                spots[def.Key] = new Spot { Def = def, Rect = rt, Stack = stack, Background = bg };
                var d = def;
                rt.gameObject.AddComponent<Button>().onClick.AddListener(() => Place(d.Kind, d.Number, chip));
            }
            puck = PaintPuck(layout, out puckText);
            die1 = DieView.Create(layout, 84, Palette.Hex("E8243C"), Color.white);
            die2 = DieView.Create(layout, 84, Palette.Hex("E8243C"), Color.white);
            die1.Rect.Center(-60, 470, 84, 84);
            die2.Rect.Center(60, 470, 84, 84);
            die1.Set(3);
            die2.Set(4);
            BuildControls();
            fx = UIKit.NewRect("Fx", Content).Stretch();
            App.I.Progress.StreakExtended += OnStreakExtended;
            App.I.Intensity.Baseline = 0.22f;
            Refresh();
            if (game.Point > 0) MovePuck();   // a restored layout may already have the point on
            Say(Loc.T("Welcome to craps! Put a bet on the Pass Line and roll."), 3.5f);
        }

        // ------------------------------------------------------------------ board

        public static List<SpotDef> Layout()
        {
            var list = new List<SpotDef>();
            Color green = Palette.Hex("0B5A33"), dark = Palette.Hex("08452A"), red = Palette.Hex("A3122E"), gold = Palette.Hex("8A6A12");
            void Add(CrapsBetKind kind, int number, string label, string pays, float x, float y, float w, float h, Color color) =>
                list.Add(new SpotDef { Key = Key(kind, number), Kind = kind, Number = number, Label = label, Pays = pays,
                    Center = new Vector2(x, y), Size = new Vector2(w - 10, h - 10), Color = color });
            string[] placePays = { "9:5", "7:5", "7:6", "7:6", "7:5", "9:5" };
            for (int i = 0; i < CrapsTable.PlaceNumbers.Length; i++)
            {
                int n = CrapsTable.PlaceNumbers[i];
                Add(CrapsBetKind.Place, n, n == 6 ? Tr.T("SIX") : n == 9 ? Tr.T("NINE") : n.ToString(), placePays[i], -800 + i * 320, 270, 300, 150, dark);
            }
            Add(CrapsBetKind.Field, 0, Tr.T("FIELD"), Tr.T("2 3 4 9 10 11 12  •  2 and 12 pay double"), -370, 95, 1180, 150, green);
            Add(CrapsBetKind.AnySeven, 0, Tr.T("ANY 7"), "4:1", 340, 95, 230, 150, red);
            Add(CrapsBetKind.AnyCraps, 0, Tr.T("ANY CRAPS"), "7:1", 570, 95, 230, 150, red);
            Add(CrapsBetKind.Eleven, 0, "11", "15:1", 800, 95, 230, 150, red);
            Add(CrapsBetKind.DontPass, 0, Tr.T("DON'T PASS BAR"), Tr.T("1:1  •  12 pushes"), -200, -70, 1520, 120, dark);
            Add(CrapsBetKind.Pass, 0, Tr.T("PASS LINE"), "1:1", -200, -215, 1520, 150, green);
            Add(CrapsBetKind.PassOdds, 0, Tr.T("ODDS"), Tr.T("true odds"), 770, -150, 360, 280, gold);
            return list;
        }

        public static RectTransform PaintBoard(RectTransform parent)
        {
            var b = UIKit.NewRect("Board", parent).Center(BoardPos.x, BoardPos.y, BoardSize.x, BoardSize.y);
            var bg = UIKit.Panel(b, "BoardBg", new Color(0.02f, 0.22f, 0.12f, 0.6f), 22f);
            ((RectTransform)bg.transform).Stretch(-16, -16, -16, -16);
            UIKit.Outline((RectTransform)bg.transform, Palette.WithAlpha(Palette.Gold, 0.75f), 22f);
            return b;
        }

        public static RectTransform PaintSpot(RectTransform board, SpotDef def, out Image bg)
        {
            bg = UIKit.Panel(board, "Spot " + def.Key, Palette.WithAlpha(def.Color, 0.85f), 12f, true);
            var rt = ((RectTransform)bg.transform).Center(def.Center.x, def.Center.y, def.Size.x, def.Size.y);
            UIKit.Outline(rt, Palette.WithAlpha(Color.white, 0.35f), 12f);
            var t = UIKit.Label(rt, def.Label, def.Label.Length > 10 ? 44 : 54, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)t.transform).Stretch(8, 36, 8, 0);
            var p = UIKit.Label(rt, def.Pays, 24, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)p.transform).At(0.5f, 0f, 0, 22, def.Size.x - 20, 30);
            return rt;
        }

        /// <summary>The ON/OFF puck: off at the side on the come-out, on the point number once there is one.</summary>
        public static RectTransform PaintPuck(RectTransform parent, out Text text)
        {
            var disc = UIKit.Image(parent, "Puck", ProceduralArt.Circle, Color.white);
            var rt = ((RectTransform)disc.transform).Center(PuckOff.x, PuckOff.y, 96, 96);
            var inner = UIKit.Image(rt, "Ring", ProceduralArt.Circle, Palette.Hex("17141C"));
            ((RectTransform)inner.transform).Center(0, 0, 84, 84);
            text = UIKit.Label(rt, Loc.T("OFF"), 28, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)text.transform).Stretch();
            return rt;
        }

        static readonly Vector2 PuckOff = new Vector2(-920, 420);

        static Vector2 PuckOn(int point) => new Vector2(-800 + System.Array.IndexOf(CrapsTable.PlaceNumbers, point) * 320, 270 + 40) + BoardPos;

        void MovePuck()
        {
            bool on = game.Point > 0;
            puckText.text = on ? Loc.T("ON") : Loc.T("OFF");
            puckText.color = on ? Palette.TextDark : Color.white;
            ((Image)puck.GetComponent<Image>()).color = on ? Color.white : Palette.Hex("2A2A2A");
            puck.GetChild(0).GetComponent<Image>().color = on ? Color.white : Palette.Hex("17141C");
            StartCoroutine(Tween.Move(puck, on ? PuckOn(game.Point) : PuckOff, 0.35f));
        }

        // ------------------------------------------------------------------ betting

        void Place(CrapsBetKind kind, int number, long amount)
        {
            if (rolling) return;
            if (!game.CanBet(kind, number))
            {
                Sfx.Play(SfxId.Error);
                App.I.UI.Toast(kind == CrapsBetKind.PassOdds
                    ? (game.Point == 0 ? Loc.T("Odds go behind the Pass Line once there's a point.") : game.BetOn(CrapsBetKind.Pass) == 0
                        ? Loc.T("Odds need a Pass Line bet.") : Loc.T("Your odds are at the maximum."))
                    : Loc.T("Line bets go down on the come-out roll."), Palette.TextMuted, Icon.Lock);
                return;
            }
            if (game.BetOn(kind, number) + amount > MaxPerSpot)
            {
                Sfx.Play(SfxId.Error);
                App.I.UI.Toast(Loc.T("Table maximum reached"), Palette.TextMuted, Icon.Lock);
                return;
            }
            long before = game.BetOn(kind, number);
            if (!game.Bet(kind, number, amount))
            {
                Sfx.Play(SfxId.Error);
                if (App.I.Wallet.Balance < ChipValues[0]) ChipOffer.Show(Refresh);
                else App.I.UI.Toast(Loc.T("Not enough chips for that bet."), Palette.TextMuted, Icon.Coin);
                return;
            }
            placedThisRoll.Add((kind, kind == CrapsBetKind.Place ? number : 0, game.BetOn(kind, number) - before));
            Sfx.Play(SfxId.Chip, 1f, 0.06f);
            Refresh();
            SaveLayout();
        }

        void Undo()
        {
            if (rolling || placedThisRoll.Count == 0) return;
            var (kind, number, amount) = placedThisRoll[placedThisRoll.Count - 1];
            placedThisRoll.RemoveAt(placedThisRoll.Count - 1);
            long all = game.TakeDown(kind, number);
            if (all > amount) game.Bet(kind, number, all - amount);   // put back what was there before
            Sfx.Play(SfxId.Chip, 0.6f, 0.1f);
            Refresh();
            SaveLayout();
        }

        /// <summary>Takes down everything that's allowed to come down (not a Pass Line bet once the point is on).</summary>
        void ClearBets()
        {
            if (rolling) return;
            foreach (var s in spots.Values) game.TakeDown(s.Def.Kind, s.Def.Number);
            placedThisRoll.Clear();
            Sfx.Play(SfxId.ChipStack);
            Refresh();
            SaveLayout();
        }

        void Refresh()
        {
            foreach (var s in spots.Values)
            {
                s.Stack.SetAmount(game.BetOn(s.Def.Kind, s.Def.Number));
                bool can = game.CanBet(s.Def.Kind, s.Def.Number) || game.BetOn(s.Def.Kind, s.Def.Number) > 0;
                s.Background.color = Palette.WithAlpha(s.Def.Color, can ? 0.85f : 0.35f);
            }
            long total = game.TotalOnTable;
            totalText.text = total > 0 ? Loc.F("ON THE TABLE  {0}", ChipFormat.Full(total)) : "";
            rollButton.Interactable = !rolling && total > 0;
            undoButton.Interactable = !rolling && placedThisRoll.Count > 0;
            clearButton.Interactable = !rolling && total > 0;
            if (!rolling)
                hint.text = game.Point == 0
                    ? Loc.T("Come-out roll: bet the Pass Line, then roll. 7 or 11 wins, 2, 3 or 12 loses. Place bets start working once there's a point.")
                    : Loc.F("The point is {0}: roll it again before a 7. Odds behind your Pass Line pay true odds.", game.Point);
        }

        /// <summary>The layout survives an app kill: saved after every bet, take-down and roll, restored in Build.
        /// The stakes stay out of the chip balance either way.</summary>
        void SaveLayout()
        {
            var save = App.I.Save;
            save.crapsPoint = game.Point;
            save.crapsBetKinds.Clear();
            save.crapsBetNumbers.Clear();
            save.crapsBetAmounts.Clear();
            foreach (var (kind, number, amount) in game.Bets())
            {
                save.crapsBetKinds.Add((int)kind);
                save.crapsBetNumbers.Add(number);
                save.crapsBetAmounts.Add(amount);
            }
        }

        void RestoreLayout()
        {
            var save = App.I.Save;
            if (save.crapsBetAmounts.Count == 0) return;
            var saved = new System.Collections.Generic.List<(CrapsBetKind Kind, int Number, long Amount)>(save.crapsBetAmounts.Count);
            for (int i = 0; i < save.crapsBetAmounts.Count && i < save.crapsBetKinds.Count; i++)
                saved.Add(((CrapsBetKind)save.crapsBetKinds[i], i < save.crapsBetNumbers.Count ? save.crapsBetNumbers[i] : 0, save.crapsBetAmounts[i]));
            game.Restore(save.crapsPoint, saved);
        }

        void BuildControls()
        {
            var barBg = UIKit.Panel(Content, "Controls", new Color(0.04f, 0.02f, 0.08f, 0.8f), 30f);
            ((RectTransform)barBg.transform).At(0.5f, 0f, 200, 20, 1480, 150, 0.5f, 0f);
            for (int i = 0; i < ChipValues.Length; i++)
            {
                long value = ChipValues[i];
                var b = UIKit.Button(Content, ChipFormat.Short(value), ButtonStyle.Dark, () => SelectChip(value), 30, null, FontKind.Display, 50f);
                b.Rect.At(0.5f, 0f, -440 + i * 108, 95, 100, 100);
                b.Face.sprite = ProceduralArt.Chip(i);
                b.Face.type = Image.Type.Simple;
                b.Face.color = Color.white;
                chipButtons.Add(b);
            }
            undoButton = UIKit.Button(Content, Loc.T("UNDO"), ButtonStyle.Dark, Undo, 28);
            undoButton.Rect.At(0.5f, 0f, 200, 125, 160, 60);
            clearButton = UIKit.Button(Content, Loc.T("CLEAR"), ButtonStyle.Dark, ClearBets, 28);
            clearButton.Rect.At(0.5f, 0f, 200, 60, 160, 60);
            rollButton = UIKit.Button(Content, Loc.T("ROLL"), ButtonStyle.Gold, Roll, 60, Icon.Play);
            rollButton.Rect.At(0.5f, 0f, 750, 95, 300, 128);

            hint = UIKit.Label(Content, "", 28, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)hint.transform).At(0.5f, 0f, 0, 205, 1500, 44);   // between the table's front rail and the chips, clear of the dealer's bubble
            UIKit.TextShadow(hint, -2f, 0.9f);
            totalText = UIKit.Label(Content, "", 28, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)totalText.transform).At(0f, 1f, 40, -TopBar.Height - 90, 700, 40, 0f, 0.5f);
            UIKit.TextShadow(totalText, -2f, 0.9f);
            resultText = UIKit.Label(Content, "", 96, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)resultText.transform).Center(0, 250, 1400, 120);
            UIKit.TextOutline(resultText, Palette.Hex("3A1500"), 3f);
            SelectChip(chip);
        }

        void SelectChip(long value)
        {
            chip = value;
            for (int i = 0; i < chipButtons.Count; i++)
                chipButtons[i].Rect.localScale = ChipValues[i] == value ? Vector3.one * 1.15f : Vector3.one * 0.92f;
        }

        // ------------------------------------------------------------------ the roll

        void Roll()
        {
            if (rolling || game.TotalOnTable <= 0) return;
            if (App.I.Energy.IsEmpty)
            {
                ExhaustedPrompt.Show();
                return;
            }
            App.I.Intensity.OnBetPlaced(game.TotalOnTable, ChipValues[0], MaxPerSpot, App.I.Wallet.Balance + game.TotalOnTable);
            int pointBefore = game.Point;
            var settled = game.Roll();
            SaveLayout();   // the model is settled now; the animation only shows it
            StartCoroutine(RollRoutine(settled, pointBefore));
        }

        IEnumerator RollRoutine(List<CrapsSettlement> settled, int pointBefore)
        {
            rolling = true;
            Refresh();
            hint.text = "";
            resultText.text = "";
            Sfx.Play(SfxId.Shuffle, 0.8f);
            // The dice fly from your end of the table to the far wall and bounce back a little.
            Vector2 from = new Vector2(Random.Range(-120f, 120f), -460f);
            var a = die1.Tumble(from, new Vector2(Random.Range(-260f, -40f), Random.Range(420f, 470f)), 1.1f, game.Die1, 1080f);
            var b = die2.Tumble(from + new Vector2(40, 0), new Vector2(Random.Range(40f, 260f), Random.Range(420f, 470f)), 1.2f, game.Die2, 1260f);
            yield return Tween.All(this, a, b);
            Sfx.Play(SfxId.Tick);
            int t = game.LastTotal;
            resultText.color = t == 7 ? Palette.Hex("FF6A7E") : Palette.Gold;
            resultText.text = t.ToString();

            long wagered = 0, net = 0;
            foreach (var s in settled)
            {
                wagered += s.Staked;
                long n = s.StaysUp ? s.Returned : s.Returned - s.Staked;
                net += n;
                if (!spots.TryGetValue(Key(s.Kind, s.Number), out var spot)) continue;
                if (n > 0 || (n == 0 && s.Returned > 0)) StartCoroutine(Tween.Punch(spot.Rect, 0.15f, 0.4f));
                StartCoroutine(Stamp(spot, n > 0 ? "+" + ChipFormat.Short(n) : n == 0 ? Loc.T("PUSH") : Loc.T("LOSE"), n > 0 ? Palette.Green : n == 0 ? Color.white : Palette.Red));
            }
            if (settled.Count > 0)
            {
                App.I.RecordWager(GameKind.Craps, wagered, net);
                App.I.Intensity.OnRoundResult(net, false);
            }

            // What happened, in the stickman's words.
            if (pointBefore == 0 && game.Point > 0) Say(Loc.F("The point is {0}.", game.Point), 2.4f);
            else if (pointBefore > 0 && game.Point == 0 && t != 7) Say(Loc.F("{0}! The point is made. Winner!", t), 2.6f);
            else if (pointBefore > 0 && t == 7) Say(Loc.T("Seven out. New shooter, new come-out."), 2.6f);
            else if (pointBefore == 0 && (t == 7 || t == 11)) Say(Loc.F("{0}, a natural! Pass Line wins.", t), 2.4f);
            else if (pointBefore == 0 && (t == 2 || t == 3 || t == 12)) Say(Loc.F("{0}, craps. Line away.", t), 2.4f);

            if (net > 0)
            {
                Sfx.Play(net >= wagered * 4 && wagered > 0 ? SfxId.BigWin : SfxId.Win);
                Confetti.Burst(fx, new Vector2(0, 100), 40, false);
            }
            else if (net < 0) Sfx.Play(SfxId.Lose, 0.7f);
            MovePuck();
            yield return Tween.Wait(1.4f);
            if (streakLine != null)
            {
                Say(streakLine);
                streakLine = null;
                yield return Tween.Wait(2.5f);
            }
            placedThisRoll.Clear();
            rolling = false;
            Refresh();
            if (game.TotalOnTable == 0 && App.I.Wallet.Balance < ChipValues[0]) ChipOffer.Show(Refresh);
        }

        IEnumerator Stamp(Spot spot, string text, Color color)
        {
            var label = UIKit.Label(spot.Rect, text, 48, color, TextAnchor.MiddleCenter, FontKind.Display, "Stamp");
            var rt = ((RectTransform)label.transform).Center(0, 0, 400, 80);
            UIKit.TextOutline(label, new Color(0, 0, 0, 0.85f), 3f);
            rt.localScale = Vector3.one * 1.6f;
            yield return Tween.Scale(rt, Vector3.one, 0.25f, Ease.OutBack);
            yield return Tween.Wait(1.2f);
            yield return Tween.Fade(UIKit.Group(label), 0f, 0.3f);
            if (rt != null) Destroy(rt.gameObject);
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
            if (seat.World != null) seat.World.ShowCrapsDecor(true);
            var next = App.I != null ? App.I.UI.Current : null;
            if (seat.World != null && next != this && (next == null || !next.ShowsWorld)) seat.World.SetVisible(false);
        }

        /// <summary>Leaving takes every bet that may come down back to your balance; a Pass Line bet with a point on stays lost.</summary>
        void Leave()
        {
            if (rolling)
            {
                App.I.UI.Toast(Loc.T("Wait for the dice to land."), Palette.TextMuted, Icon.Clock);
                return;
            }
            if (game.Point > 0 && game.BetOn(CrapsBetKind.Pass) + game.BetOn(CrapsBetKind.DontPass) > 0)
            {
                // A line bet can't come down once the point is on: leaving gives it up, so ask first (and never trap anyone).
                App.I.UI.Modal<ConfirmModal>(m => m.Setup(Loc.T("LEAVE THE TABLE?"),
                    Loc.F("Your line bet is still working on the point of {0}. If you leave now, it's lost.", game.Point),
                    Loc.T("STAY"), null, Loc.T("LEAVE"), StandUp));
                return;
            }
            StandUp();
        }

        void StandUp()
        {
            foreach (var s in spots.Values) game.TakeDown(s.Def.Kind, s.Def.Number);
            // Anything left working (a line bet on the point) is given up by leaving - the dialog says so - so the
            // saved layout is wiped rather than restored next time.
            var save = App.I.Save;
            save.crapsPoint = 0;
            save.crapsBetKinds.Clear();
            save.crapsBetNumbers.Clear();
            save.crapsBetAmounts.Clear();
            App.I.UI.Show<LobbyScreen>();
        }

        public override bool OnBack()
        {
            Leave();
            return true;
        }

        internal bool TourRolling => rolling;
        internal int TourPoint => game.Point;

        internal void TourBet()
        {
            if (game.Point == 0 && game.BetOn(CrapsBetKind.Pass) == 0) Place(CrapsBetKind.Pass, 0, 50);
            if (game.Point > 0 && game.BetOn(CrapsBetKind.PassOdds) == 0) Place(CrapsBetKind.PassOdds, 0, 100);
            if (game.BetOn(CrapsBetKind.Field) == 0) Place(CrapsBetKind.Field, 0, 10);
        }

        internal void TourRoll() => Roll();
    }
}
