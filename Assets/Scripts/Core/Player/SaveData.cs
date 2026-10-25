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

        // Sound beyond music and effects: master volume, the staff's chatter, muting while in the background.
        public float masterVolume = 1f;
        public float voiceVolume = 1f;
        public bool muteInBackground = true;

        // Graphics: quality preset (-1 the platform default: Medium on phones, Ultra on computers; else 0 Low, 1 Medium,
        // 2 High, 3 Ultra), render resolution on phones, anti-aliasing samples (0, 2, 4), the walking view's horizontal
        // field of view in degrees, the FPS counter, and on computers window mode and v-sync.
        public int graphicsQuality = -1;
        public float renderScale = 1f;
        public int antiAliasing;
        public float fieldOfView = 92f;
        public bool showFps;
        public bool windowed;
        public bool vSync;

        // Controls. Touch/mouse and the controller's right stick each have their own speed (1 = default) and inverted
        // axes. padLayout picks button names and confirm/back (0 auto-detect, 1 Xbox, 2 PlayStation, 3 Nintendo).
        public float lookSensitivity = 1f;
        public bool invertLookX;
        public bool invertLookY;
        public float padLookSensitivity = 1f;
        public bool padInvertX;
        public bool padInvertY;
        public int padLayout;
        public bool padHints = true;
        // Which middle button opens Settings: swapped (the default) puts Settings on Select/View/Create/-, which suits
        // pads like the Manba One whose buttons sit the other way round; off is the console convention (Start/Menu/Options/+).
        public bool padSwapMenuView = true;

        // Vibration: where it goes (0 auto: the controller while one is connected, 1 phone, 2 controller, 3 both),
        // and each one on/off with its own strength (0..1).
        public int vibrateOn;
        public bool phoneVibration = true;
        public float phoneVibrationStrength = 0.7f;
        public bool padVibration = true;
        public float padVibrationStrength = 0.7f;

        /// <summary>The chosen language code ("" = follow the device's language).</summary>
        public string language = "";

        /// <summary>Test builds: a country picked in Settings > Dev overrides the detected one ("" = none).</summary>
        public string testCountry = "";

        public string countryCode = "";
        public int countrySource;
        public long countryCheckedUtcTicks;

        public string lastTableId = "";
        public long lastBet;
        public long lastHoldemAnte;

        // The craps layout and point, saved after every bet, take-down and roll so an app kill mid-round loses
        // nothing (CrapsScreen restores them). The stakes are already out of chips.
        public int crapsPoint;
        public List<int> crapsBetKinds = new List<int>();
        public List<int> crapsBetNumbers = new List<int>();
        public List<long> crapsBetAmounts = new List<long>();

        // Progression: levels, the daily streak, the bonus wheel, timed free chips and daily missions (Progression.cs).
        // Paid luck packs still to use: each entry is a boost (percent) with its hands left. luckOfferShown: the one-time
        // offer after Beginner's Luck ends has been made.
        public List<int> luckPackPercent = new List<int>();
        public List<int> luckPackHands = new List<int>();
        public bool luckOfferShown;

        public long xp;
        public int level = 1;
        public int streakCount;
        public int streakBest;
        public int streakLastDay = -1;
        public int streakFreezes;
        public int streakRounds;
        public int streakRoundsDay = -1;
        public List<int> streakMetDays = new List<int>();
        public List<int> streakFrozenDays = new List<int>();
        public int wheelDay = -1;
        public long bonusReadyUtcTicks;
        public int missionDay = -1;
        public List<int> missionIds = new List<int>();
        public List<int> missionProgress = new List<int>();
        public List<bool> missionDone = new List<bool>();
        public bool missionBonusPaid;
        public bool remindersEnabled = true;
        public bool remindersAsked;

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
