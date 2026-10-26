using System;
using System.Collections;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Player;
using Casino.Core.Slots;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// The daily screen: the streak (today's goal, a week of flames, streak freezes), the daily bonus wheel with its
    /// chances printed underneath, and today's three missions.
    /// </summary>
    public sealed class DailyModal : UIModal
    {
        const float ColumnW = 560f, ColumnH = 760f, ColumnY = -50f, WheelSize = 400f;

        Text streakNumber, streakBest, goalText, freezeText, wheelInfo;
        Image bigFlame;
        RectTransform goalFill, wheel;
        UIButton freezeButton, freezeAdButton, spinButton;

        readonly Image[] dayIcons = new Image[7];
        readonly Image[] dayRings = new Image[7];
        readonly Text[] missionCounts = new Text[Missions.PerDay];
        readonly RectTransform[] missionFills = new RectTransform[Missions.PerDay];
        readonly Image[] missionChecks = new Image[Missions.PerDay];
        readonly Text[] missionRewards = new Text[Missions.PerDay];
        Text bonusText;
        bool spinning, wheelShowsPrize;
        float refresh;

        static Progress P => App.I.Progress;

        public override GameObject DefaultFocus => spinButton != null && spinButton.Interactable ? spinButton.gameObject : null;

        protected override void Build()
        {
            MakePanel(1800, 900, Loc.T("DAILY"));
            AddCloseButton();
            BuildStreak(Column(-600, StreakPill.Fire));
            BuildWheel(Column(0, Palette.Gold));
            BuildMissions(Column(600, Palette.Cyan));
            P.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (App.I != null) P.Changed -= Refresh;
        }

        RectTransform Column(float x, Color accent)
        {
            var bg = UIKit.Panel(Panel, "Column", new Color(0, 0, 0, 0.28f), 28f);
            var rt = ((RectTransform)bg.transform).Center(x, ColumnY, ColumnW, ColumnH);
            UIKit.Outline(rt, Palette.WithAlpha(accent, 0.35f), 28f);
            return rt;
        }

        static Text Header(RectTransform col, string text, Color color)
        {
            var t = UIKit.Label(col, text, 40, color, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)t.transform).At(0.5f, 1f, 0, -44, ColumnW - 40, 56);
            UIKit.TextShadow(t, -3f, 0.6f);
            return t;
        }

        static RectTransform Bar(RectTransform parent, float x, float y, float w, float h, Color color, out RectTransform fill)
        {
            var track = UIKit.Panel(parent, "Track", new Color(1, 1, 1, 0.12f), h / 2f);
            var trt = ((RectTransform)track.transform).Center(x, y, w, h);
            var f = UIKit.Panel(trt, "Fill", color, h / 2f);
            fill = (RectTransform)f.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.05f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            return trt;
        }

        // ------------------------------------------------------------------ streak

        void BuildStreak(RectTransform col)
        {
            Header(col, Loc.T("DAILY STREAK"), StreakPill.Fire);
            var glow = UIKit.Image(col, "Glow", ProceduralArt.Glow, Palette.WithAlpha(StreakPill.Fire, 0.35f));
            ((RectTransform)glow.transform).Center(-100, 200, 300, 300);
            bigFlame = UIKit.Image(col, "Flame", ProceduralArt.IconSprite(Icon.Flame), StreakPill.Fire);
            ((RectTransform)bigFlame.transform).Center(-100, 200, 170, 170);
            streakNumber = UIKit.Label(col, "0", 130, Color.white, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)streakNumber.transform).Center(110, 210, 260, 150);
            UIKit.TextShadow(streakNumber, -4f, 0.6f);
            var days = UIKit.Label(col, Loc.T("DAY STREAK"), 26, StreakPill.Fire, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)days.transform).Center(110, 130, 260, 36);
            streakBest = UIKit.Label(col, "", 22, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)streakBest.transform).Center(0, 86, ColumnW - 40, 30);

            goalText = UIKit.Label(col, "", 26, Color.white, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)goalText.transform).Center(0, 34, ColumnW - 30, 40);
            Bar(col, 0, -14, 440, 22, StreakPill.Fire, out goalFill);

            // The last seven days, today on the right.
            for (int i = 0; i < 7; i++)
            {
                float x = -195 + i * 65;
                dayRings[i] = UIKit.Image(col, "Day", ProceduralArt.Circle, new Color(1, 1, 1, 0.1f));
                ((RectTransform)dayRings[i].transform).Center(x, -100, 58, 58);
                dayIcons[i] = UIKit.Image(dayRings[i].transform, "Mark", ProceduralArt.IconSprite(Icon.Flame), StreakPill.Fire);
                ((RectTransform)dayIcons[i].transform).Stretch(8, 8, 8, 8);
                var day = DateTime.Today.AddDays(i - 6);
                var letter = UIKit.Label(col, i == 6 ? Loc.T("TODAY") : Loc.Upper(Loc.Weekday((int)day.DayOfWeek)), 18,
                    i == 6 ? Palette.Gold : Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
                ((RectTransform)letter.transform).Center(x, -146, 70, 26);
            }

            var ice = UIKit.Image(col, "Freeze", ProceduralArt.IconSprite(Icon.Freeze), Palette.Hex("8FD8FF"));
            ((RectTransform)ice.transform).Center(-205, -215, 56, 56);
            freezeText = UIKit.Label(col, "", 24, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)freezeText.transform).Center(30, -212, 400, 92);   // header plus a two-line note, without shrinking
            // A freeze for a video, or for chips.
            freezeAdButton = UIKit.Button(col, Loc.T("WATCH AD"), ButtonStyle.Green, () => App.I.Purchases.WatchAdForFreeze(), 28, Icon.Video);
            freezeAdButton.Rect.Center(-118, -310, 228, 90);
            freezeButton = UIKit.Button(col, Loc.T("BUY"), ButtonStyle.Blue, AskBuyFreeze, 28, Icon.Coin);
            freezeButton.Rect.Center(118, -310, 228, 90);
        }

        void AskBuyFreeze()
        {
            if (!P.CanBuyFreeze) return;
            App.I.UI.Modal<ConfirmModal>(m => m.Setup(Loc.T("STREAK FREEZE"),
                Loc.F("A streak freeze keeps your streak alive on a day you don't play. You can hold {0}.\n\nBuy one for {1} chips?",
                    Streak.MaxFreezes, ChipFormat.Full(GameConfig.StreakFreezeChips)),
                Loc.T("BUY"), () =>
                {
                    if (P.BuyFreeze())
                    {
                        Sfx.Play(SfxId.ChipStack);
                        App.I.UI.Toast(Loc.T("Streak freeze ready."), Palette.Hex("8FD8FF"), Icon.Freeze);
                    }
                    else App.I.UI.Toast(Loc.T("Not enough chips for a freeze."), Palette.TextMuted, Icon.Coin);
                }, Loc.T("CANCEL"), null, ButtonStyle.Blue));
        }

        // ------------------------------------------------------------------ wheel

        void BuildWheel(RectTransform col)
        {
            Header(col, Loc.T("DAILY SPIN"), Palette.Gold);
            var holder = UIKit.NewRect("WheelHolder", col).Center(0, 60, WheelSize, WheelSize);
            var shadow = UIKit.Image(holder, "Shadow", ProceduralArt.Glow, new Color(0, 0, 0, 0.6f));
            ((RectTransform)shadow.transform).Center(0, -10, WheelSize * 1.25f, WheelSize * 1.25f);
            var img = UIKit.Image(holder, "Wheel", ProceduralArt.PrizeWheel, Color.white);
            wheel = ((RectTransform)img.transform).Center(0, 0, WheelSize, WheelSize);
            int n = DailyWheel.Prizes.Length;
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) * 360f / n;
                var t = UIKit.Label(wheel, ChipFormat.Short(DailyWheel.Prizes[i]), 34, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                var rt = (RectTransform)t.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(120, 44);
                rt.anchoredPosition = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * WheelSize * 0.31f;
                // Along the radius, reading outwards like on a real prize wheel, so they look right at any angle it stops at.
                rt.localEulerAngles = new Vector3(0, 0, 90f - a);
                UIKit.TextShadow(t, -2f, 0.8f);
            }
            // Pointer at the top.
            var pointer = UIKit.Image(holder, "Pointer", ProceduralArt.IconSprite(Icon.Play), Palette.GoldLight);
            var prt = ((RectTransform)pointer.transform).Center(0, WheelSize * 0.5f + 8f, 70, 70);
            prt.localEulerAngles = new Vector3(0, 0, -90);
            UIKit.TextShadow(pointer, -3f, 0.7f);

            wheelInfo = UIKit.Label(col, "", 30, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)wheelInfo.transform).Center(0, -184, ColumnW - 40, 44);
            spinButton = UIKit.Button(col, Loc.T("SPIN"), ButtonStyle.Gold, () => StartCoroutine(Spin()), 50, Icon.Play);
            spinButton.Rect.Center(0, -262, 340, 100);

            var odds = new System.Text.StringBuilder(Loc.T("Chances:")).Append("  ");
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (x, y) => DailyWheel.Prizes[x].CompareTo(DailyWheel.Prizes[y]));
            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                odds.Append(ChipFormat.Short(DailyWheel.Prizes[i])).Append(' ').Append(Loc.F("{0}%", DailyWheel.Weights[i]));
                if (k < n - 1) odds.Append(k == 3 ? "\n" : "  •  ");
            }
            var oddsText = UIKit.Label(col, odds.ToString(), 19, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Body);
            ((RectTransform)oddsText.transform).Center(0, -340, ColumnW - 30, 56);
        }

        IEnumerator Spin()
        {
            if (spinning || !P.WheelReady) yield break;
            spinning = true;
            spinButton.Interactable = false;
            int segment = P.SpinWheel(out long prize);
            if (segment < 0)
            {
                spinning = false;
                yield break;
            }
            float step = 360f / DailyWheel.Prizes.Length;
            float start = wheel.localEulerAngles.z;
            float target = start - Mathf.Repeat(start, 360f) + 360f * 6f + (segment + 0.5f) * step + UnityEngine.Random.Range(-step * 0.3f, step * 0.3f);
            int lastTick = Mathf.FloorToInt(start / step);
            wheelInfo.text = "";
            wheelShowsPrize = true;
            yield return Tween.Run(4.2f, Ease.OutCubic, k =>
            {
                float z = Mathf.LerpUnclamped(start, target, k);
                wheel.localEulerAngles = new Vector3(0, 0, z);
                int tick = Mathf.FloorToInt(z / step);
                if (tick != lastTick)
                {
                    lastTick = tick;
                    Sfx.Play(SfxId.Tick, 0.6f, 0.1f);
                    Casino.Platform.Haptics.Play(Casino.Platform.Haptic.Tick);
                }
            });
            Sfx.Play(SfxId.Coins);
            Confetti.Burst(Panel, new Vector2(0, 40), 70, true);
            wheelInfo.text = Loc.F("You won {0} chips!", ChipFormat.Full(prize));
            spinning = false;
            Refresh();
        }

        // ------------------------------------------------------------------ missions

        void BuildMissions(RectTransform col)
        {
            Header(col, Loc.T("TODAY'S MISSIONS"), Palette.Cyan);
            for (int i = 0; i < Missions.PerDay; i++)
            {
                var m = P.Mission(i);
                float y = 200 - i * 160;
                var card = UIKit.Panel(col, "Mission", new Color(1, 1, 1, 0.06f), 22f);
                var crt = ((RectTransform)card.transform).Center(0, y, ColumnW - 50, 140);
                var art = UIKit.Image(crt, "Game", GameArt(m.Game), GameTint(m.Game));
                art.preserveAspect = true;
                ((RectTransform)art.transform).At(0f, 0.5f, 18, 0, 84, 84, 0f, 0.5f);
                var text = UIKit.Label(crt, Loc.T(m.Text), 26, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
                ((RectTransform)text.transform).Stretch(118, 64, 120, 14);
                Bar(crt, -20, -30, 260, 18, Palette.Cyan, out missionFills[i]);
                missionCounts[i] = UIKit.Label(crt, "", 22, Palette.TextMuted, TextAnchor.MiddleLeft, FontKind.Semi);
                ((RectTransform)missionCounts[i].transform).Center(160, -30, 120, 30);
                missionRewards[i] = UIKit.Label(crt, $"+{ChipFormat.Short(m.RewardChips)}", 30, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)missionRewards[i].transform).At(1f, 0.5f, -16, 10, 100, 44, 1f, 0.5f);
                missionChecks[i] = UIKit.Image(crt, "Done", ProceduralArt.IconSprite(Icon.Check), Palette.Hex("7CFC9A"));
                ((RectTransform)missionChecks[i].transform).At(1f, 0.5f, -30, 0, 70, 70, 1f, 0.5f);
            }
            bonusText = UIKit.Label(col, "", 26, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)bonusText.transform).Center(0, -300, ColumnW - 40, 80);
        }

        static Sprite GameArt(GameKind? game)
        {
            switch (game)
            {
                case GameKind.Slots: return ProceduralArt.SlotSymbol(SlotSymbol.Seven);
                case GameKind.Roulette: return ProceduralArt.RouletteWheel;
                default: return ProceduralArt.IconSprite(Icon.Cards);
            }
        }

        static Color GameTint(GameKind? game)
        {
            switch (game)
            {
                case GameKind.Blackjack: return Palette.Hex("7CFC9A");
                case GameKind.Holdem: return Palette.Hex("39E6FF");
                case GameKind.Baccarat: return Palette.Hex("FF6A7E");
                case GameKind.VideoPoker: return Palette.Hex("7FB2FF");
                case GameKind.SicBo: return Palette.Hex("FF4FA3");
                case GameKind.Craps: return Palette.Hex("F5C542");
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ state

        void Refresh()
        {
            var p = P;
            var streak = p.Streak;
            bool done = streak.DoneToday(p.Day);
            streakNumber.text = streak.Count.ToString();
            bigFlame.color = done ? StreakPill.Fire : new Color(0.5f, 0.47f, 0.55f);
            streakBest.text = streak.Best > 0 ? Loc.F("Best: {0} days", streak.Best) : Loc.T("Play every day to build a streak");
            int rounds = Mathf.Min(streak.RoundsToday(p.Day), streak.DailyGoal);
            goalText.text = done ? Loc.T("Today's goal done! See you tomorrow.") : Loc.F("Today: {0} / {1} rounds (any game)", rounds, streak.DailyGoal);
            goalFill.anchorMax = new Vector2(Mathf.Max(0.05f, done ? 1f : (float)rounds / streak.DailyGoal), 1f);
            for (int i = 0; i < 7; i++)
            {
                int day = p.Day - 6 + i;
                var mark = streak.Mark(day);
                dayIcons[i].enabled = mark != DayMark.None;
                dayIcons[i].sprite = ProceduralArt.IconSprite(mark == DayMark.Frozen ? Icon.Freeze : Icon.Flame);
                dayIcons[i].color = mark == DayMark.Frozen ? Palette.Hex("8FD8FF") : StreakPill.Fire;
                dayRings[i].color = i == 6 ? Palette.WithAlpha(Palette.Gold, done ? 0.35f : 0.2f) : new Color(1, 1, 1, mark == DayMark.None ? 0.08f : 0.16f);
            }
            freezeText.text = Loc.F("Streak freezes: {0} / {1}", streak.Freezes, Streak.MaxFreezes) +
                "\n<size=19><color=#C9BFDB>" + Loc.T("Save your streak on a day you miss. +1 every 7 days.") + "</color></size>";
            freezeButton.SetLabel(p.CanBuyFreeze ? ChipFormat.Short(GameConfig.StreakFreezeChips) : Loc.T("FULL"));
            freezeButton.Interactable = p.CanBuyFreeze;
            var purchases = App.I.Purchases;
            freezeAdButton.SetLabel(!p.CanBuyFreeze ? Loc.T("FULL") : purchases.AdReady ? Loc.T("WATCH AD")
                : App.I.Ads.IsBlocked ? Loc.T("NO VIDEO") : Loc.T("AD SOON"));
            freezeAdButton.Interactable = p.CanBuyFreeze && purchases.AdReady;

            if (!spinning)
            {
                spinButton.Interactable = p.WheelReady;
                if (p.WheelReady)
                {
                    spinButton.SetLabel(Loc.T("SPIN"));
                    double mult = DailyWheel.StreakMultiplier(streak.Count);
                    if (!wheelShowsPrize)
                        wheelInfo.text = mult > 1.0 ? Loc.F("Streak bonus: prizes ×{0}", ChipFormat.Number(mult, "0.0")) : Loc.T("One free spin every day");
                }
                else
                {
                    var left = DateTime.Today.AddDays(1) - DateTime.Now;
                    spinButton.SetLabel(Loc.F("NEXT  {0}", $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}"));
                }
            }

            for (int i = 0; i < Missions.PerDay; i++)
            {
                var m = p.Mission(i);
                bool finished = p.MissionDone(i);
                int progress = p.MissionProgress(i);
                missionFills[i].anchorMax = new Vector2(Mathf.Max(0.05f, (float)progress / m.Target), 1f);
                missionCounts[i].text = m.Goal == MissionGoal.ChipsWon ? $"{ChipFormat.Short(progress)}/{ChipFormat.Short(m.Target)}" : $"{progress}/{m.Target}";
                missionChecks[i].enabled = finished;
                missionRewards[i].enabled = !finished;
            }
            bonusText.text = p.MissionsDone == Missions.PerDay
                ? Loc.T("All done! Bonus collected.") + "\n<size=20><color=#C9BFDB>" + Loc.T("New missions at midnight.") + "</color></size>"
                : Loc.F("Finish all three: +{0} bonus chips", ChipFormat.Full(Missions.AllDoneBonus));
        }

        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f) return;
            refresh = 0.5f;
            if (!spinning) Refresh();
        }

        // ------------------------------------------------------------------ tour

        internal bool TourSpinning => spinning;
        internal void TourSpin() => StartCoroutine(Spin());
    }
}
