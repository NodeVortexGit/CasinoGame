using System;
using System.Collections.Generic;
using Casino.Art;
using Casino.Core;
using Casino.Core.Economy;
using Casino.Core.Player;

namespace Casino
{
    /// <summary>A moment worth a banner: level up, streak extended, mission done.</summary>
    public struct Celebration
    {
        public Icon Icon;
        public UnityEngine.Color Color;
        public string Title, Subtitle;
    }

    /// <summary>
    /// The reasons to come back: levels and XP, the daily streak, the daily bonus wheel, free chips on a timer and three
    /// daily missions (rules in Core/Player/Progression.cs). Games report each finished round here; this pays the
    /// rewards, keeps the save up to date and tells the UI what to celebrate.
    /// </summary>
    public sealed class Progress
    {
        readonly App app;
        int today;

        public Progress(App app)
        {
            this.app = app;
            Streak = new Streak(app.Save, GameConfig.StreakDailyGoal);
            today = Today;
            RollDay();
        }

        public Streak Streak { get; }

        /// <summary>Anything the HUD shows (level, XP, streak, missions) changed.</summary>
        public event Action Changed;

        public event Action<Celebration> Celebrate;

        /// <summary>News from a day change (a streak freeze used, a streak ended), for the lobby to tell the player once.</summary>
        public string Notice { get; set; }

        public static int Today => CalendarDay.Of(DateTime.Now);
        public int Day => today;
        SaveData Save => app.Save;

        // ---------------- levels ----------------

        public int Level => Save.level;
        public long Xp => Save.xp;

        public float LevelProgress
        {
            get
            {
                long from = Levels.XpToReach(Level), to = Levels.XpToReach(Level + 1);
                return to > from ? UnityEngine.Mathf.Clamp01((float)(Save.xp - from) / (to - from)) : 0f;
            }
        }

        public bool Unlocked(TableDef table) => Level >= table.Level;

        static string UnlockAt(int level)
        {
            foreach (var t in TableCatalog.Blackjack)
                if (t.Level == level && level > 1) return t.Name + " unlocked";
            return null;
        }

        // ---------------- day changes ----------------

        /// <summary>Called on start, on resume and now and then: a new day brings new missions and settles the streak.</summary>
        public void CheckDay()
        {
            int now = Today;
            if (now == today) return;
            today = now;
            RollDay();
            Changed?.Invoke();
        }

        void RollDay()
        {
            if (Save.missionDay != today || Save.missionIds.Count != Missions.PerDay || Save.missionProgress.Count != Missions.PerDay ||
                Save.missionDone.Count != Missions.PerDay)
            {
                Save.missionDay = today;
                Save.missionIds = new List<int>(Missions.ForDay(today));
                Save.missionProgress = new List<int> { 0, 0, 0 };
                Save.missionDone = new List<bool> { false, false, false };
                Save.missionBonusPaid = false;
            }
            var change = Streak.Settle(today, out int days);
            if (change == StreakChange.Frozen)
                Notice = days == 1 ? $"A streak freeze saved your {Streak.Count}-day streak yesterday."
                    : $"{days} streak freezes saved your {Streak.Count}-day streak.";
            else if (change == StreakChange.Lost)
                Notice = $"Your {days}-day streak ended. Play {Streak.DailyGoal} rounds today to start a new one!";
            app.SaveSystem.MarkDirty();
        }

        // ---------------- rounds ----------------

        /// <summary>Every finished round of every game comes through here.</summary>
        public void OnRound(RoundReport r)
        {
            CheckDay();
            int xp = Levels.XpFor(r);

            // Missions
            for (int i = 0; i < Save.missionIds.Count; i++)
            {
                if (Save.missionDone[i]) continue;
                var m = Mission(i);
                Save.missionProgress[i] = (int)Math.Min(m.Target, (long)Save.missionProgress[i] + Missions.Progress(m, r));
                if (Save.missionProgress[i] < m.Target) continue;
                Save.missionDone[i] = true;
                app.Wallet.Credit(m.RewardChips);
                xp += m.RewardXp;
                Celebrate?.Invoke(new Celebration
                {
                    Icon = Icon.Target, Color = Palette.Cyan, Title = "MISSION COMPLETE",
                    Subtitle = $"{m.Text}  •  +{ChipFormat.Full(m.RewardChips)} chips",
                });
            }
            if (MissionsDone == Missions.PerDay && !Save.missionBonusPaid)
            {
                Save.missionBonusPaid = true;
                app.Wallet.Credit(Missions.AllDoneBonus);
                Celebrate?.Invoke(new Celebration
                {
                    Icon = Icon.Crown, Color = Palette.Gold, Title = "ALL MISSIONS DONE!",
                    Subtitle = $"Daily bonus +{ChipFormat.Full(Missions.AllDoneBonus)} chips. New missions tomorrow.",
                });
            }

            // XP and levels
            Save.xp += xp;
            int reached = Levels.LevelFor(Save.xp);
            while (Save.level < reached)
            {
                Save.level++;
                long reward = Levels.Reward(Save.level);
                app.Wallet.Credit(reward);
                string unlock = UnlockAt(Save.level);
                Celebrate?.Invoke(new Celebration
                {
                    Icon = Icon.Star, Color = Palette.Gold, Title = $"LEVEL {Save.level}!",
                    Subtitle = $"+{ChipFormat.Full(reward)} chips" + (unlock != null ? "  •  " + unlock : ""),
                });
            }

            // Daily streak
            if (Streak.OnRound(today) == StreakChange.Extended)
            {
                int n = Streak.Count;
                long reward = Streak.MilestoneReward(n);
                if (reward > 0) app.Wallet.Credit(reward);
                string sub = reward > 0 ? $"Milestone! +{ChipFormat.Full(reward)} chips" : "Come back tomorrow to keep it going";
                if (n % 7 == 0) sub += "  •  +1 streak freeze";
                Celebrate?.Invoke(new Celebration { Icon = Icon.Flame, Color = Palette.Hex("FF8A1F"), Title = $"{n} DAY STREAK!", Subtitle = sub });
                StreakExtended?.Invoke();
            }

            app.SaveSystem.MarkDirty();
            Changed?.Invoke();
        }

        /// <summary>Raised when today's goal is met (the lobby uses it to offer reminders the first time).</summary>
        public event Action StreakExtended;

        // ---------------- missions ----------------

        public MissionDef Mission(int i) => Missions.Pool[Save.missionIds[i]];
        public int MissionProgress(int i) => Save.missionProgress[i];
        public bool MissionDone(int i) => Save.missionDone[i];

        public int MissionsDone
        {
            get
            {
                int n = 0;
                foreach (bool d in Save.missionDone) if (d) n++;
                return n;
            }
        }

        // ---------------- daily wheel ----------------

        public bool WheelReady => DailyWheel.Ready(Save.wheelDay, Today);

        /// <summary>Spins today's wheel and pays the prize (the streak multiplies it). Returns the segment.</summary>
        public int SpinWheel(out long prize)
        {
            prize = 0;
            if (!WheelReady) return -1;
            int segment = DailyWheel.Spin(app.Rng);
            prize = DailyWheel.Prize(segment, Streak.Count);
            Save.wheelDay = Today;
            app.Wallet.Credit(prize);
            app.SaveSystem.SaveNow();
            Changed?.Invoke();
            return segment;
        }

        // ---------------- free chips on a timer ----------------

        public bool BonusReady => DateTime.UtcNow.Ticks >= Save.bonusReadyUtcTicks;
        public long BonusAmount => TimedBonus.Amount(Level);

        public TimeSpan BonusIn
        {
            get
            {
                long left = Save.bonusReadyUtcTicks - DateTime.UtcNow.Ticks;
                return left > 0 ? new TimeSpan(left) : TimeSpan.Zero;
            }
        }

        public long CollectBonus()
        {
            if (!BonusReady) return 0;
            long amount = BonusAmount;
            app.Wallet.Credit(amount);
            Save.bonusReadyUtcTicks = DateTime.UtcNow.AddHours(GameConfig.BonusHours).Ticks;
            app.SaveSystem.SaveNow();
            Changed?.Invoke();
            return amount;
        }

        // ---------------- streak freezes ----------------

        public bool CanBuyFreeze => Streak.Freezes < Streak.MaxFreezes;

        /// <summary>A freeze from a rewarded video (or any other free source).</summary>
        public bool AddFreeze()
        {
            if (!Streak.AddFreeze()) return false;
            app.SaveSystem.SaveNow();
            Changed?.Invoke();
            return true;
        }

        public bool BuyFreeze()
        {
            if (!CanBuyFreeze || !app.Wallet.TryDebit(GameConfig.StreakFreezeChips)) return false;
            Streak.AddFreeze();
            app.SaveSystem.SaveNow();
            Changed?.Invoke();
            return true;
        }
    }
}
