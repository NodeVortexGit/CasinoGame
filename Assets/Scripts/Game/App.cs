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
            ApplyFrameRate();

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

            Sfx.Init(gameObject);
            Sfx.Volume = Save.sfxVolume;
            Music = new MusicPlayer(transform);
            Music.SetVolume(Save.musicVolume);

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

        public void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Save.targetFps <= 30 ? 30 : 60;
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

            if (Input.GetKeyDown(KeyCode.Escape) && !UI.Back() && UI.Current is LobbyScreen)
                UI.Modal<ConfirmModal>(m => m.Setup("Leave the casino?", "Your chips and progress are saved.", "QUIT", Quit, "STAY"));

            SaveSystem.Tick();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save.lastSeenUtcTicks = DateTime.UtcNow.Ticks;
                SaveSystem.SaveNow();
            }
            else
            {
                ApplyTimeAway();
            }
        }

        void OnApplicationQuit()
        {
            Save.lastSeenUtcTicks = DateTime.UtcNow.Ticks;
            SaveSystem.SaveNow();
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
        public void RecordWager(long wagered, long net)
        {
            Save.totalWagered += wagered;
            if (net > Save.biggestWin) Save.biggestWin = net;
            SaveSystem.MarkDirty();
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
        }
    }
}
