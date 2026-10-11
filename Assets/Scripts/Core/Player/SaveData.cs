using System;
using System.Collections.Generic;

namespace Casino.Core.Player
{
    /// <summary>Everything persisted between sessions. Plain public fields so Unity's JsonUtility can serialize it.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public long chips;

        public bool profileComplete;
        public int birthYear;
        public int birthMonth;
        public int birthDay;
        public int gender = (int)PlayerGender.Unspecified;
        public int npcStyle = (int)NpcStyle.Mixed;
        public bool persuasionEnabled;

        public float energy = 1f;
        public long lastSeenUtcTicks;

        public int handsPlayed;
        public int handsWon;
        public int blackjacks;
        public long totalWagered;
        public long biggestWin;

        public int adsWatchedToday;
        public int adDayNumber;
        public long lastAdUtcTicks;

        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
        public int targetFps = 30;

        public string countryCode = "";
        public int countrySource;
        public long countryCheckedUtcTicks;

        public string lastTableId = "";
        public long lastBet;

        /// <summary>Store transaction ids already granted, so a replayed purchase callback never pays twice.</summary>
        public List<string> grantedTransactions = new List<string>();

        public static SaveData CreateNew(long startingChips, DateTime utcNow) => new SaveData
        {
            chips = startingChips,
            lastSeenUtcTicks = utcNow.Ticks,
        };

        public bool HasGranted(string transactionId) => grantedTransactions.Contains(transactionId);

        public void MarkGranted(string transactionId)
        {
            grantedTransactions.Add(transactionId);
            if (grantedTransactions.Count > 200) grantedTransactions.RemoveRange(0, grantedTransactions.Count - 200);
        }
    }
}
