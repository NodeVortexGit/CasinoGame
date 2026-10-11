using System;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.Core.Region;
using Casino.Platform;
using Casino.Screens;
using Casino.UI;
using UnityEngine;

namespace Casino
{
    /// <summary>Composition root: owns the save, the economy, platform services, audio and the UI.</summary>
    public sealed class App : MonoBehaviour
    {
        public static App I { get; private set; }

        public SaveSystem SaveSystem { get; private set; }
        public SaveData Save => SaveSystem.Data;
        public Wallet Wallet { get; private set; }
        public EnergyModel Energy { get; private set; }
        public BeginnerLuck Luck { get; private set; }
        public MusicIntensity Intensity { get; private set; }
        public Progress Progress { get; private set; }
        public PersuasionTracker Persuasion { get; } = new PersuasionTracker();
        public IRandom Rng { get; } = Pcg32.FromTime();
        public RegionService Region { get; } = new RegionService();
        public IStoreService Store { get; private set; }
        public IAdService Ads { get; private set; }
        public Purchases Purchases { get; private set; }
        public MusicPlayer Music { get; private set; }
        public UIRoot UI { get; private set; }

        /// <summary>Clears the screen behind the UI; switched off while the 3D casino camera is drawing.</summary>
        public Camera UiCamera { get; private set; }

        /// <summary>True once the player is past the age gate and profile: energy drains from here on.</summary>
        public bool InCasino { get; set; }

        public NpcStyle Staff => (NpcStyle)Save.npcStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (I != null) return;
            var go = new GameObject("Casino");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        void Awake()
        {
            I = this;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Input.multiTouchEnabled = false;

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Casino.Art.Palette.Night;
            cam.cullingMask = 0;
            cam.orthographic = true;
            cam.gameObject.AddComponent<AudioListener>();
            UiCamera = cam;

            SaveSystem = new SaveSystem();
            SaveSystem.Load();
            nativeLong = Mathf.Max(Screen.width, Screen.height);
            nativeShort = Mathf.Min(Screen.width, Screen.height);
            ApplyGraphics();

            Wallet = new Wallet(Math.Max(0, Save.chips));
            Wallet.Changed += (balance, _) =>
            {
                Save.chips = balance;
                SaveSystem.MarkDirty();
            };
            Energy = new EnergyModel(Save.energy, GameConfig.EnergyMinutesToEmpty, GameConfig.EnergyHoursToFullWhileAway);
            Energy.Changed += v =>
            {
                Save.energy = v;
                SaveSystem.MarkDirty();
            };
            ApplyTimeAway();
            Luck = new BeginnerLuck(GameConfig.BeginnerLuckFullHands, GameConfig.BeginnerLuckFadeHands, Save.handsPlayed);
            Intensity = new MusicIntensity();
            Progress = new Progress(this);
            Reminders.Cancel();

            Sfx.Init(gameObject);
            Music = new MusicPlayer(transform);
            ApplyAudio();

            SteamBootstrap.Create(transform);
            Purchases = new Purchases(this);
            Store = StoreFactory.Create();
            Store.Initialize(Purchases.AllSkus(), Purchases.Fulfill);
            Ads = AdFactory.Create();
            Ads.Initialize();

            Region.RestoreCached(Save.countryCode, (RegionSource)Save.countrySource);
            Region.ExplainLocation = done =>
            {
                var modal = UI.Modal<ConfirmModal>(m => m.Setup("FAIR PRICES FOR YOUR COUNTRY",
                    "Shop prices are adjusted to your country's economy, so players everywhere pay a fair price.\n\n" +
                    "To find your country we check your approximate location once. It is matched to a country on your device " +
                    "and never sent anywhere. If you decline, standard prices apply.",
                    "CONTINUE", null));
                modal.Closed += done;
            };
            Region.Changed += () =>
            {
                Save.countryCode = Region.Country ?? "";
                Save.countrySource = (int)Region.Source;
                Save.countryCheckedUtcTicks = DateTime.UtcNow.Ticks;
                SaveSystem.MarkDirty();
            };

            UI = UIRoot.Create(transform);
            FpsCounter.Show(UI.Overlay, Save.showFps);
            Progress.Celebrate += c => CelebrationBanner.Show(UI.Overlay, c);
            Route();
            DevTour.TryStart(this);
        }

        /// <summary>Picks the first screen: blocked, age gate, profile or lobby.</summary>
        public void Route()
        {
            InCasino = false;
            if (SaveSystem.UnderageBlocked)
            {
                UI.Show<BlockedScreen>();
                return;
            }
            if (!Save.profileComplete)
            {
                UI.Show<AgeGateScreen>();
                return;
            }
            EnterCasino();
        }

        public void EnterCasino()
        {
            InCasino = true;
            UI.Show<LobbyScreen>();
            var checkedAt = new DateTime(Save.countryCheckedUtcTicks, DateTimeKind.Utc);
            if (Region.Source == RegionSource.None || (DateTime.UtcNow - checkedAt).TotalHours > GameConfig.CountryRecheckHours)
                StartCoroutine(Region.Resolve());
        }

        // ---------------- graphics and sound settings ----------------

        static readonly int[] QualityLevels = { 1, 2, 3, 5 };   // Low, Medium, High, Ultra in ProjectSettings/QualitySettings
        int nativeLong, nativeShort;
        bool focused = true;

        /// <summary>The dev tour runs silent whatever the settings say.</summary>
        public static bool ForceMute;

        /// <summary>0 Low, 1 Medium, 2 High, 3 Ultra; phones start on Medium, computers on Ultra.</summary>
        public int GraphicsQuality => Save.graphicsQuality >= 0 && Save.graphicsQuality < QualityLevels.Length ? Save.graphicsQuality
            : Application.isMobilePlatform ? 1 : 3;

        /// <summary>The screen refreshes fast enough for the 120 FPS option.</summary>
        public static bool HighRefreshScreen => Screen.currentResolution.refreshRateRatio.value >= 89.0;

        public void ApplyGraphics()
        {
            int level = QualityLevels[GraphicsQuality];
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            QualitySettings.antiAliasing = Save.antiAliasing >= 4 ? 4 : Save.antiAliasing >= 2 ? 2 : 0;
            ApplyFrameRate();
            ApplyResolution();
            Casino.World.CasinoWorld.Instance?.ApplyGraphics();
            if (UI != null) FpsCounter.Show(UI.Overlay, Save.showFps);
        }

        public void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = !Application.isMobilePlatform && Save.vSync ? 1 : 0;
            int fps = Save.targetFps >= 120 && HighRefreshScreen ? 120 : Save.targetFps <= 30 ? 30 : 60;
            Application.targetFrameRate = QualitySettings.vSyncCount > 0 ? -1 : fps;
        }

        void ApplyResolution()
        {
            if (Application.isEditor || Application.isBatchMode || nativeLong <= 0) return;
            if (Application.isMobilePlatform)
            {
                // Phones draw fewer pixels and let the screen scale them up: a little softer, cooler and lighter on battery.
                float k = Mathf.Clamp(Save.renderScale, 0.5f, 1f);
                int w = Mathf.RoundToInt(nativeLong * k), h = Mathf.RoundToInt(nativeShort * k);
                if (Screen.width < Screen.height) (w, h) = (h, w);
                if (Screen.width != w || Screen.height != h) Screen.SetResolution(w, h, true);
                return;
            }
            var mode = Save.windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            if (Screen.fullScreenMode == mode) return;
            if (Save.windowed)
            {
                int h = Mathf.Max(540, Mathf.RoundToInt(Display.main.systemHeight * 0.75f));
                Screen.SetResolution(Mathf.RoundToInt(h * 16f / 9f), h, mode);
            }
            else
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, mode);
            }
        }

        public void ApplyAudio()
        {
            float master = ForceMute || (!focused && Save.muteInBackground) ? 0f : Mathf.Clamp01(Save.masterVolume);
            Sfx.Volume = Save.sfxVolume * master;
            Sfx.VoiceVolume = Mathf.Clamp01(Save.voiceVolume);
            Music?.SetVolume(Save.musicVolume * master);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (Music != null) ApplyAudio();
        }

        void ApplyTimeAway()
        {
            var now = DateTime.UtcNow;
            if (Save.lastSeenUtcTicks > 0)
            {
                double away = (now - new DateTime(Save.lastSeenUtcTicks, DateTimeKind.Utc)).TotalSeconds;
                if (away > 0) Energy.Regenerate(away);
            }
            Save.lastSeenUtcTicks = now.Ticks;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (InCasino && dt < 1f) Energy.Drain(dt);

            Intensity.Update(dt);
            Music.SetIntensity(Intensity.Current);

            dayCheck -= dt;
            if (dayCheck <= 0f)
            {
                dayCheck = 30f;
                Progress.CheckDay();
            }

            if (Input.GetKeyDown(KeyCode.Escape) && !Pad.BackJustHandled && !UI.Back() && UI.Current is LobbyScreen)
                UI.Modal<ConfirmModal>(m => m.Setup("Leave the casino?", "Your chips and progress are saved.", "QUIT", Quit, "STAY"));

            SaveSystem.Tick();
            Haptics.Tick();
        }

        float dayCheck;

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save.lastSeenUtcTicks = DateTime.UtcNow.Ticks;
                SaveSystem.SaveNow();
                Reminders.Schedule(this);
                Haptics.Stop();
            }
            else
            {
                ApplyTimeAway();
                Reminders.Cancel();
                Progress.CheckDay();
            }
        }

        void OnApplicationQuit()
        {
            Save.lastSeenUtcTicks = DateTime.UtcNow.Ticks;
            SaveSystem.SaveNow();
            Reminders.Schedule(this);
        }

        public void Quit()
        {
            SaveSystem.SaveNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- stats ----------------

        /// <summary>Stats for slots and roulette (they don't count as blackjack hands for Beginner's Luck).</summary>
        public void RecordWager(GameKind game, long wagered, long net, bool straightHit = false)
        {
            Save.totalWagered += wagered;
            if (net > Save.biggestWin) Save.biggestWin = net;
            SaveSystem.MarkDirty();
            Progress.OnRound(new RoundReport { Game = game, Wagered = wagered, Returned = wagered + net, StraightHit = straightHit });
        }

        public void RecordHand(long wagered, long net, bool blackjack, int handsWon)
        {
            Save.handsPlayed++;
            Save.handsWon += handsWon;
            Save.totalWagered += wagered;
            if (blackjack) Save.blackjacks++;
            if (net > Save.biggestWin) Save.biggestWin = net;
            Luck.HandsPlayed = Save.handsPlayed;
            Persuasion.OnHandPlayed();
            SaveSystem.MarkDirty();
            Progress.OnRound(new RoundReport { Game = GameKind.Blackjack, Wagered = wagered, Returned = wagered + net, Blackjack = blackjack });
        }
    }
}
