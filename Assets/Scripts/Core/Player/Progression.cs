using System;
using System.Collections.Generic;

namespace Casino.Core.Player
{
    /// <summary>Calendar days in the player's own time zone, so "today" turns over at their midnight.</summary>
    public static class CalendarDay
    {
        public static int Of(DateTime localTime) => (int)(localTime.Date.Ticks / TimeSpan.TicksPerDay);
    }

    public enum GameKind { Blackjack, Slots, Roulette, Holdem, Baccarat, VideoPoker, SicBo, Craps }

    /// <summary>One finished round of any game, as the progression systems see it.</summary>
    public struct RoundReport
    {
        public GameKind Game;
        public long Wagered, Returned;
        public bool Blackjack, StraightHit;

        public bool Won => Returned > Wagered;
        public long NetWin => Math.Max(0, Returned - Wagered);
        public double Multiple => Wagered > 0 ? (double)Returned / Wagered : 0;
    }

    public enum StreakChange { None, Extended, Frozen, Lost }

    /// <summary>
    /// Duolingo-style daily streak: it grows by one on each day the player meets a small daily goal (a number of rounds
    /// of any game). A missed day uses up a streak freeze if one is held, otherwise the streak starts again from zero.
    /// Freezes are earned every 7 days (or bought with chips); at most <see cref="MaxFreezes"/> are held.
    /// </summary>
    public sealed class Streak
    {
        public const int MaxFreezes = 2;
        public static readonly int[] Milestones = { 3, 7, 14, 30, 50, 100, 200, 365 };

        readonly SaveData s;

        public Streak(SaveData save, int dailyGoal)
        {
            s = save;
            DailyGoal = dailyGoal;
        }

        public int DailyGoal { get; }
        public int Count => s.streakCount;
        public int Best => s.streakBest;
        public int Freezes => s.streakFreezes;
        public int RoundsToday(int today) => s.streakRoundsDay == today ? s.streakRounds : 0;
        public bool DoneToday(int today) => s.streakCount > 0 && s.streakLastDay == today;

        /// <summary>Applies the days missed since the goal was last met. <paramref name="days"/>: days frozen, or the streak lost.</summary>
        public StreakChange Settle(int today, out int days)
        {
            days = 0;
            if (s.streakCount == 0) return StreakChange.None;
            int missed = today - s.streakLastDay - 1;
            if (missed <= 0) return StreakChange.None;
            if (missed <= s.streakFreezes)
            {
                s.streakFreezes -= missed;
                for (int d = s.streakLastDay + 1; d < today; d++) Remember(s.streakFrozenDays, d);
                s.streakLastDay = today - 1;
                days = missed;
                return StreakChange.Frozen;
            }
            days = s.streakCount;
            s.streakCount = 0;
            return StreakChange.Lost;
        }

        /// <summary>Counts a round towards today's goal; returns Extended on the round that meets it.</summary>
        public StreakChange OnRound(int today)
        {
            if (s.streakRoundsDay != today)
            {
                s.streakRoundsDay = today;
                s.streakRounds = 0;
            }
            s.streakRounds++;
            if (DoneToday(today) || s.streakRounds < DailyGoal) return StreakChange.None;
            Settle(today, out _);
            s.streakCount++;
            s.streakLastDay = today;
            Remember(s.streakMetDays, today);
            if (s.streakCount > s.streakBest) s.streakBest = s.streakCount;
            if (s.streakCount % 7 == 0) AddFreeze();
            return StreakChange.Extended;
        }

        public bool AddFreeze()
        {
            if (s.streakFreezes >= MaxFreezes) return false;
            s.streakFreezes++;
            return true;
        }

        /// <summary>How a calendar day looks on the streak calendar.</summary>
        public DayMark Mark(int day) => s.streakMetDays.Contains(day) ? DayMark.Met : s.streakFrozenDays.Contains(day) ? DayMark.Frozen : DayMark.None;

        public static bool IsMilestone(int count) => Array.IndexOf(Milestones, count) >= 0;

        public static long MilestoneReward(int count)
        {
            switch (count)
            {
                case 3: return 500;
                case 7: return 1_500;
                case 14: return 3_000;
                case 30: return 10_000;
                case 50: return 15_000;
                case 100: return 50_000;
                case 200: return 100_000;
                case 365: return 250_000;
                default: return 0;
            }
        }

        static void Remember(List<int> days, int day)
        {
            if (!days.Contains(day)) days.Add(day);
            if (days.Count > 21) days.RemoveRange(0, days.Count - 21);
        }
    }

    public enum DayMark { None, Met, Frozen }

    /// <summary>Levels from experience. XP comes per round played (and a little per win), never from the size of bets.</summary>
    public static class Levels
    {
        public const int XpPerRound = 10, XpPerWin = 5;

        /// <summary>Total XP needed to reach a level: 100 for level 2, then each level asks 40 more than the last.</summary>
        public static long XpToReach(int level) => level <= 1 ? 0 : 60L * (level - 1) + 20L * (level - 1) * level;

        public static int LevelFor(long xp)
        {
            int level = 1;
            while (XpToReach(level + 1) <= xp) level++;
            return level;
        }

        public static long Reward(int level) => 250L * level;

        public static int XpFor(RoundReport r) => XpPerRound + (r.Won ? XpPerWin : 0);
    }

    /// <summary>
    /// The daily bonus wheel: one free spin a day; the prize grows with the streak (up to double at 10 days).
    /// The chances of every prize are shown next to the wheel.
    /// </summary>
    public static class DailyWheel
    {
        public static readonly long[] Prizes = { 500, 1_000, 750, 2_500, 600, 1_500, 800, 10_000 };
        public static readonly int[] Weights = { 22, 12, 18, 5, 22, 8, 12, 1 };   // per cent

        public static bool Ready(int lastSpinDay, int today) => lastSpinDay != today;

        public static int Spin(IRandom rng)
        {
            int total = 0;
            foreach (int w in Weights) total += w;
            int roll = rng.Next(total);
            for (int i = 0; i < Weights.Length; i++)
            {
                if (roll < Weights[i]) return i;
                roll -= Weights[i];
            }
            return Weights.Length - 1;
        }

        public static double StreakMultiplier(int streak) => 1.0 + 0.1 * Math.Min(Math.Max(streak, 0), 10);

        public static long Prize(int segment, int streak) => (long)Math.Round(Prizes[segment] * StreakMultiplier(streak));
    }

    /// <summary>Free chips that refill on a timer (and need no ad), a bit more at higher levels.</summary>
    public static class TimedBonus
    {
        public static long Amount(int level) => 300 + 50L * Math.Min(Math.Max(level, 1) - 1, 20);
    }

    public enum MissionGoal { Rounds, Wins, Blackjacks, ChipsWon, BigWin, StraightHit }

    public sealed class MissionDef
    {
        public MissionDef(string id, GameKind? game, MissionGoal goal, int target, long chips, int xp, string text)
        {
            Id = id;
            Game = game;
            Goal = goal;
            Target = target;
            RewardChips = chips;
            RewardXp = xp;
            Text = text;
        }

        public string Id { get; }

        /// <summary>The game it counts in, or null for any game.</summary>
        public GameKind? Game { get; }
        public MissionGoal Goal { get; }
        public int Target { get; }
        public long RewardChips { get; }
        public int RewardXp { get; }
        public string Text { get; }
    }

    /// <summary>Three missions a day (one per game), the same for everyone on the same day; rewards in chips and XP.</summary>
    public static class Missions
    {
        public const int PerDay = 3;
        public const long AllDoneBonus = 1_500;

        /// <summary>A win on slots at least this many times the bet counts as a big win.</summary>
        public const double BigWinMultiple = 5.0;

        public static readonly MissionDef[] Pool =
        {
            new MissionDef("bj_hands", GameKind.Blackjack, MissionGoal.Rounds, 10, 400, 30, Tr.Mark("Play 10 hands of blackjack")),
            new MissionDef("bj_wins", GameKind.Blackjack, MissionGoal.Wins, 5, 600, 40, Tr.Mark("Win 5 hands of blackjack")),
            new MissionDef("bj_natural", GameKind.Blackjack, MissionGoal.Blackjacks, 1, 800, 50, Tr.Mark("Get a blackjack")),
            new MissionDef("bj_chips", GameKind.Blackjack, MissionGoal.ChipsWon, 500, 600, 40, Tr.Mark("Win 500 chips at blackjack")),
            new MissionDef("sl_spins", GameKind.Slots, MissionGoal.Rounds, 20, 400, 30, Tr.Mark("Spin the slots 20 times")),
            new MissionDef("sl_wins", GameKind.Slots, MissionGoal.Wins, 5, 500, 35, Tr.Mark("Win 5 slot spins")),
            new MissionDef("sl_big", GameKind.Slots, MissionGoal.BigWin, 1, 800, 50, Tr.Mark("Win 5× your bet on the slots")),
            new MissionDef("ro_rounds", GameKind.Roulette, MissionGoal.Rounds, 5, 400, 30, Tr.Mark("Play 5 rounds of roulette")),
            new MissionDef("ro_wins", GameKind.Roulette, MissionGoal.Wins, 3, 500, 35, Tr.Mark("Win 3 roulette spins")),
            new MissionDef("ro_straight", GameKind.Roulette, MissionGoal.StraightHit, 1, 1_000, 60, Tr.Mark("Hit a number straight up")),
            new MissionDef("uh_hands", GameKind.Holdem, MissionGoal.Rounds, 8, 400, 30, Tr.Mark("Play 8 hands of hold'em")),
            new MissionDef("uh_wins", GameKind.Holdem, MissionGoal.Wins, 4, 600, 40, Tr.Mark("Win 4 hands of hold'em")),
            new MissionDef("ba_coups", GameKind.Baccarat, MissionGoal.Rounds, 10, 400, 30, Tr.Mark("Play 10 hands of baccarat")),
            new MissionDef("ba_wins", GameKind.Baccarat, MissionGoal.Wins, 5, 500, 35, Tr.Mark("Win 5 hands of baccarat")),
            new MissionDef("vp_hands", GameKind.VideoPoker, MissionGoal.Rounds, 15, 400, 30, Tr.Mark("Play 15 hands of video poker")),
            new MissionDef("sb_rolls", GameKind.SicBo, MissionGoal.Rounds, 8, 400, 30, Tr.Mark("Play 8 rounds of sic bo")),
            new MissionDef("sb_wins", GameKind.SicBo, MissionGoal.Wins, 4, 500, 35, Tr.Mark("Win 4 rounds of sic bo")),
            new MissionDef("cr_rolls", GameKind.Craps, MissionGoal.Rounds, 15, 400, 30, Tr.Mark("Roll the dice 15 times at craps")),
            new MissionDef("cr_wins", GameKind.Craps, MissionGoal.Wins, 4, 500, 35, Tr.Mark("Win 4 rolls at craps")),
            new MissionDef("vp_big", GameKind.VideoPoker, MissionGoal.BigWin, 1, 800, 50, Tr.Mark("Make a flush or better at video poker")),
        };

        /// <summary>Today's missions: three different games, one mission each, picked by the date.</summary>
        public static int[] ForDay(int day)
        {
            var rng = new Pcg32((ulong)day * 2654435761UL + 1442695040888963407UL, 54);
            var picks = new int[PerDay];
            var games = new List<GameKind>();
            foreach (GameKind g in Enum.GetValues(typeof(GameKind)))
                if (Array.Exists(Pool, m => m.Game == g)) games.Add(g);
            for (int i = games.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (games[i], games[j]) = (games[j], games[i]);
            }
            for (int g = 0; g < PerDay; g++)
            {
                var options = new List<int>();
                for (int i = 0; i < Pool.Length; i++)
                    if (Pool[i].Game == games[g]) options.Add(i);
                picks[g] = options[rng.Next(options.Count)];
            }
            return picks;
        }

        /// <summary>How much a round moves a mission forward.</summary>
        public static int Progress(MissionDef m, RoundReport r)
        {
            if (m.Game.HasValue && m.Game.Value != r.Game) return 0;
            switch (m.Goal)
            {
                case MissionGoal.Rounds: return 1;
                case MissionGoal.Wins: return r.Won ? 1 : 0;
                case MissionGoal.Blackjacks: return r.Blackjack ? 1 : 0;
                case MissionGoal.ChipsWon: return (int)Math.Min(int.MaxValue, r.NetWin);
                case MissionGoal.BigWin: return r.Multiple >= BigWinMultiple ? 1 : 0;
                case MissionGoal.StraightHit: return r.StraightHit ? 1 : 0;
                default: return 0;
            }
        }
    }
}
