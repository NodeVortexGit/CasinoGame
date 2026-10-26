using System.Collections;
using Casino.Art;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Blackjack;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// The lobby: the walkable 3D casino with a light HUD on top. Left half of the screen: drag to walk (a stick appears
    /// under your thumb). Right half: drag to look around. Tap anything to walk there; tap a table, the cashier or the
    /// bar to use it. When you stand at something, a big button appears for it.
    /// </summary>
    public sealed class LobbyScreen : UIScreen
    {
        CasinoWorld world;
        CanvasGroup loading;
        RectTransform actionGroup, bubbleRect;
        UIButton action;
        Text actionTitle, actionSub, freeChipsInfo, hint;
        UIButton freeChips, daily;
        GameObject dailyBadge;
        Pulse freeChipsPulse;
        static int dailyShownDay = -1;
        SpeechBubble bubble;
        StaffFigure speaker;
        float speakerUntil, statusTimer, hintTimer = 9f;
        Spot shownSpot;

        protected override void Build()
        {
            world = CasinoWorld.Ensure();
            world.SetVisible(true);
            world.StandUp();
            world.StaffSpeaks += OnStaffSpeaks;

            // Touch layer first, so every button drawn after it wins the touch.
            var pad = UIKit.Image(Content, "TouchPad", null, new Color(0, 0, 0, 0), true);
            ((RectTransform)pad.transform).Stretch();
            pad.gameObject.AddComponent<WorldTouchPad>().World = world;

            TopBar.Create(Content, null, Icon.Exit, () =>
                App.I.UI.Modal<ConfirmModal>(m => m.Setup(Loc.T("Leave the casino?"), Loc.T("Your chips and progress are saved."), Loc.T("QUIT"),
                    App.I.Quit, Loc.T("STAY"))));

            var luck = LuckBanner.Create(Content);
            ((RectTransform)luck.transform).At(0.5f, 1f, 0, -TopBar.Height - 44, 500, 64);

            // Speech bubble that floats over whoever is talking.
            bubble = SpeechBubble.Create(Content, 520, 120);
            bubbleRect = (RectTransform)bubble.transform;
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(40f / 520f, 0f);

            // Context action (bottom right)
            actionGroup = UIKit.NewRect("Action", Content).At(1f, 0f, -40, 60, 620, 210, 1f, 0f);
            actionSub = UIKit.Label(actionGroup, "", 28, Color.white, TextAnchor.LowerRight, FontKind.Semi);
            ((RectTransform)actionSub.transform).At(1f, 1f, -6, 0, 620, 50, 1f, 1f);
            UIKit.TextShadow(actionSub, -2f, 0.8f);
            action = UIKit.Button(actionGroup, Loc.T("PLAY"), ButtonStyle.Gold, () => { if (shownSpot != null) world.Use(shownSpot); }, 52, Icon.Play);
            action.Rect.At(1f, 0f, 0, 0, 520, 136, 1f, 0f);
            action.gameObject.AddComponent<Pulse>();
            actionTitle = action.LabelText;
            actionGroup.gameObject.SetActive(false);

            // Quick buttons (right edge): the daily screen, free chips and a game list for people who'd rather not walk.
            daily = UIKit.Button(Content, Loc.T("DAILY"), ButtonStyle.Gold, () => App.I.UI.Modal<DailyModal>(), 34, Icon.Gift);
            daily.Rect.At(1f, 0.5f, -40, 270, 330, 100, 1f, 0.5f);
            var badge = UIKit.Image(daily.Rect, "Badge", ProceduralArt.Circle, Palette.Red);
            ((RectTransform)badge.transform).At(1f, 1f, 10, 10, 44, 44, 1f, 1f);
            var mark = UIKit.Label(badge.transform, "!", 30, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)mark.transform).Stretch();
            dailyBadge = badge.gameObject;
            freeChips = UIKit.Button(Content, Loc.T("FREE CHIPS"), ButtonStyle.Green, FreeChips, 34, Icon.Coin);
            freeChips.Rect.At(1f, 0.5f, -40, 150, 330, 100, 1f, 0.5f);
            freeChipsPulse = freeChips.gameObject.AddComponent<Pulse>();
            freeChipsInfo = UIKit.Label(Content, "", 22, Color.white, TextAnchor.MiddleRight, FontKind.Semi);
            ((RectTransform)freeChipsInfo.transform).At(1f, 0.5f, -46, 80, 420, 32, 1f, 0.5f);
            UIKit.TextShadow(freeChipsInfo, -2f, 0.8f);
            var tables = UIKit.Button(Content, Loc.T("GAMES"), ButtonStyle.Purple, OpenGames, 34, Icon.Cards);
            tables.Rect.At(1f, 0.5f, -40, 0, 330, 100, 1f, 0.5f);

            hint = UIKit.Label(Content, Loc.T("Drag left side to walk  •  Drag right side to look  •  Tap to go"), 26, Color.white, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)hint.transform).At(0.5f, 0f, 0, 250, 1400, 40);
            UIKit.TextShadow(hint, -2f, 0.9f);

            var foot = UIKit.Label(Content, Loc.T("18+ only  •  Chips are for fun: no cash value, never exchangeable for money or prizes"), 20, Palette.WithAlpha(Color.white, 0.75f));
            ((RectTransform)foot.transform).At(0.5f, 0f, 0, 14, 1600, 30);
            UIKit.TextShadow(foot, -2f, 0.9f);

            // Loading cover while the casino is being built (first visit only).
            var cover = UIKit.NewRect("Loading", Content).Stretch(-200, -200, -200, -200);
            loading = UIKit.Group(cover);
            Backgrounds.Casino(cover, Palette.Hex("3A1066"), Palette.Hex("0C0614"), new Vector2(0, 200));
            Logo.Create(cover, 120, 0.9f);
            var wait = UIKit.Label(cover, Loc.T("Opening the doors..."), 40, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)wait.transform).Center(0, -120, 1000, 60);
            var spin = UIKit.Image(cover, "Spinner", ProceduralArt.DashedRing, Palette.Gold);
            ((RectTransform)spin.transform).Center(0, -230, 90, 90);
            spin.gameObject.AddComponent<Spinner>().DegreesPerSecond = -180f;
            loading.alpha = world.Ready ? 0f : 1f;
            loading.blocksRaycasts = !world.Ready;

            App.I.Intensity.Baseline = 0.08f;
            StartCoroutine(Welcome());
        }

        void OnDestroy()
        {
            if (world == null || App.I == null || App.I.UI == null) return;
            world.StaffSpeaks -= OnStaffSpeaks;
            var next = App.I.UI.Current;
            if (next != this && (next == null || !next.ShowsWorld)) world.SetVisible(false);
        }

        public override bool ShowsWorld => true;
        public override bool WalksWorld => true;
        public override string PadConfirmHint => shownSpot?.Title();

        public override bool OnPadConfirm()
        {
            if (shownSpot == null) return false;
            world.Use(shownSpot);
            return true;
        }

        void OpenGames()
        {
            if (App.I.Energy.IsEmpty)
            {
                ExhaustedPrompt.Show();
                return;
            }
            App.I.UI.Modal<GamesModal>();
        }

        IEnumerator Welcome()
        {
            while (!world.Ready) yield return null;
            yield return Tween.Fade(loading, 0f, 0.6f);
            loading.blocksRaycasts = false;
            loading.gameObject.SetActive(false);
            yield return Tween.Wait(0.4f);
            var progress = App.I.Progress;
            if (!string.IsNullOrEmpty(progress.Notice))
            {
                world.HostSays(progress.Notice);
                progress.Notice = null;
            }
            else if (App.I.Energy.IsEmpty) world.HostSays(DealerLines.Pick(DealerMoment.Exhausted, App.I.Rng));
            else if (App.I.Wallet.Balance < TableCatalog.Blackjack[0].MinBet) world.HostSays(DealerLines.Pick(DealerMoment.OutOfChips, App.I.Rng));
            else if (App.I.Save.handsPlayed == 0) world.HostSays(Loc.F("Welcome! Your {0} welcome chips are ready. The tables are straight ahead!", ChipFormat.Full(GameConfig.StartingChips)));
            else
            {
                var streak = progress.Streak;
                int left = streak.DailyGoal - streak.RoundsToday(progress.Day);
                world.HostSays(DealerLines.StreakTease(streak.Count, streak.DoneToday(progress.Day), Mathf.Max(1, left), App.I.Rng));
            }

            // The daily screen opens by itself once a day while today's spin is waiting (not on the very first visit).
            yield return Tween.Wait(2.5f);
            if (progress.WheelReady && App.I.Save.handsPlayed > 0 && dailyShownDay != progress.Day && !App.I.UI.HasModal && App.I.UI.Current == this)
            {
                dailyShownDay = progress.Day;
                App.I.UI.Modal<DailyModal>();
            }
        }

        /// <summary>Free chips: the timed bonus when it's ready, otherwise a video if one can play, otherwise when it's next.</summary>
        void FreeChips()
        {
            var p = App.I.Progress;
            if (p.BonusReady)
            {
                long amount = p.CollectBonus();
                Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Coins);
                Confetti.Burst(Content, new Vector2(600, 150), 30, true);
                App.I.UI.Toast(Loc.F("+{0} free chips! More in {1} hours.", ChipFormat.Full(amount), GameConfig.BonusHours.ToString("0")), Palette.Gold, Icon.Gift);
                return;
            }
            if (App.I.Purchases.AdReady)
            {
                App.I.Purchases.WatchAdForChips();
                return;
            }
            App.I.UI.Toast(Loc.F("Free chips refill every {0} hours. Next in {1}.", GameConfig.BonusHours.ToString("0"), FormatWait(p.BonusIn)),
                Palette.TextMuted, Icon.Clock);
        }

        static string FormatWait(System.TimeSpan t) => t.TotalHours >= 1
            ? Loc.F("{0}h {1}m", (int)t.TotalHours, t.Minutes.ToString("00"))
            : Loc.F("{0}m {1}s", t.Minutes, t.Seconds.ToString("00"));

        /// <summary>After the first day of a streak, offer a reminder (once); Android then asks its own permission.</summary>
        void OfferReminders()
        {
            var save = App.I.Save;
            save.remindersAsked = true;
            App.I.SaveSystem.MarkDirty();
            App.I.UI.Modal<ConfirmModal>(m => m.Setup(Loc.T("KEEP YOUR STREAK"),
                Loc.T("Want a reminder on evenings you haven't played yet, and when your free chips are ready? Never more than that, " +
                "and you can turn it off in Settings."),
                Loc.T("REMIND ME"), () => App.I.StartCoroutine(Casino.Platform.Reminders.AskPermission(ok =>
                {
                    save.remindersEnabled = ok;
                    App.I.SaveSystem.MarkDirty();
                })),
                Loc.T("NO THANKS"), () =>
                {
                    save.remindersEnabled = false;
                    App.I.SaveSystem.MarkDirty();
                }, ButtonStyle.Green));
        }

        void OnStaffSpeaks(StaffFigure fig, string line)
        {
            if (!OnScreen(fig, out _))
            {
                // A bubble needs someone to point at. The host's lines can matter (notices, out of chips), so when she
                // is out of view they come as a note instead; anyone else's go unsaid.
                if (fig == world.HostFigure) App.I.UI.Toast(line, Color.white, Icon.Person);
                return;
            }
            speaker = fig;
            speakerUntil = Time.unscaledTime + 3.4f + line.Length * 0.022f;
            bubble.Say(line, 3.2f, null, fig.Female ? VoicePitch.High : VoicePitch.Low);
            PlaceBubble();
        }

        bool OnScreen(StaffFigure fig, out Vector3 screen)
        {
            screen = world.Camera.WorldToScreenPoint(fig.HeadPoint + Vector3.up * 0.12f);
            var px = world.Camera.pixelRect;
            return screen.z > 0.2f && screen.z < 9f && screen.x > px.xMin - 80f && screen.x < px.xMax + 80f;
        }

        void PlaceBubble()
        {
            if (speaker == null) return;
            bool onScreen = OnScreen(speaker, out var screen);
            if (!onScreen)
            {
                // Turned away mid-line: let it go rather than leave a bubble floating in the air (or over a machine).
                bubble.Hide();
                speaker = null;
                return;
            }
            var canvas = App.I.UI.Canvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (onScreen && RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, screen, cam, out var local))
            {
                var half = Content.rect.size * 0.5f;
                local.x = Mathf.Clamp(local.x, -half.x + 60f, half.x - 520f);
                local.y = Mathf.Clamp(local.y, -half.y + 200f, half.y - TopBar.Height - bubbleRect.sizeDelta.y - 20f);
                bubbleRect.anchoredPosition = local;
            }
            else
            {
                bubbleRect.anchoredPosition = new Vector2(-260f, 120f);
            }
        }

        void Update()
        {
            if (Time.unscaledTime > speakerUntil) speaker = null;
            PlaceBubble();

            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer <= 0f)
            {
                statusTimer = 0.25f;
                var p = App.I.Progress;
                bool bonus = p.BonusReady;
                freeChipsInfo.text = bonus ? Loc.F("+{0} chips ready!", ChipFormat.Full(p.BonusAmount))
                    : App.I.Purchases.AdReady ? Loc.F("Watch a video: {0}", App.I.Purchases.AdStatus())
                    : Loc.F("Next free chips in {0}", FormatWait(p.BonusIn));
                freeChips.SetStyle(bonus || App.I.Purchases.AdReady ? ButtonStyle.Green : ButtonStyle.Dark);
                freeChipsPulse.enabled = bonus;
                if (!bonus) freeChips.transform.localScale = Vector3.one;
                dailyBadge.SetActive(p.WheelReady);
                if (!App.I.Save.remindersAsked && Casino.Platform.Reminders.Supported && p.Streak.DoneToday(p.Day) &&
                    !App.I.UI.HasModal && App.I.UI.Current == this)
                    OfferReminders();
            }

            if (hint.gameObject.activeSelf && Casino.Platform.Pad.Active) hint.gameObject.SetActive(false);   // the controller hints take over
            if (hint.gameObject.activeSelf)
            {
                hintTimer -= Time.unscaledDeltaTime;
                if (hintTimer <= 0f || (world.Player != null && world.Player.StepPhase > 4f)) hint.gameObject.SetActive(false);
            }

            var spot = world.Ready ? world.Focused : null;
            if (spot != shownSpot)
            {
                shownSpot = spot;
                actionGroup.gameObject.SetActive(spot != null);
                if (spot != null)
                {
                    actionTitle.text = spot.Title();
                    action.SetIcon(spot.Icon);
                    action.SetStyle(spot.Style);
                    actionSub.text = spot.Staff != null ? spot.Staff.StaffName + "  •  " + spot.Subtitle() : spot.Subtitle();
                    Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Tick);
                }
            }
        }

        public override bool OnBack() => false;
    }

    /// <summary>
    /// Full-screen touch layer under the lobby HUD. Supports two fingers at once: one walking (left half), one looking
    /// (right half). Quick taps go to the world as "walk there / use this".
    /// </summary>
    public sealed class WorldTouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float StickRadius = 120f, TapMove = 22f, TapTime = 0.35f;

        public CasinoWorld World;
        int stickPointer = int.MinValue;
        Vector2 stickOrigin;
        RectTransform ring, knob;
        readonly System.Collections.Generic.Dictionary<int, (Vector2 start, float time, float moved)> presses =
            new System.Collections.Generic.Dictionary<int, (Vector2, float, float)>();

        void Start()
        {
            ring = (RectTransform)UIKit.Image(transform, "StickRing", ProceduralArt.Ring, new Color(1, 1, 1, 0.35f)).transform;
            ring.sizeDelta = new Vector2(StickRadius * 2.2f, StickRadius * 2.2f);
            knob = (RectTransform)UIKit.Image(ring, "Knob", ProceduralArt.Circle, new Color(1, 1, 1, 0.55f)).transform;
            knob.sizeDelta = new Vector2(110, 110);
            ring.gameObject.SetActive(false);
        }

        float Scale => App.I.UI.Canvas.scaleFactor;

        Vector2 Local(PointerEventData e)
        {
            var canvas = App.I.UI.Canvas;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var p);
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            presses[e.pointerId] = (e.position, Time.unscaledTime, 0f);
            if (stickPointer == int.MinValue && e.position.x < Screen.width * 0.42f)
            {
                stickPointer = e.pointerId;
                stickOrigin = Local(e);
                ring.anchoredPosition = stickOrigin;
                knob.anchoredPosition = Vector2.zero;
                ring.gameObject.SetActive(true);
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (presses.TryGetValue(e.pointerId, out var p))
                presses[e.pointerId] = (p.start, p.time, Mathf.Max(p.moved, (e.position - p.start).magnitude / Scale));
            if (e.pointerId == stickPointer)
            {
                Vector2 d = Vector2.ClampMagnitude(Local(e) - stickOrigin, StickRadius);
                knob.anchoredPosition = d;
                World.Stick = d / StickRadius;
            }
            else
            {
                // Look: about 0.13 degrees per reference pixel, so a full-width swipe turns ~180 degrees.
                Vector2 delta = e.delta / Scale;
                World.LookBy(delta.x * 0.13f, -delta.y * 0.11f);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == stickPointer)
            {
                stickPointer = int.MinValue;
                World.Stick = Vector2.zero;
                ring.gameObject.SetActive(false);
            }
            if (presses.TryGetValue(e.pointerId, out var p))
            {
                presses.Remove(e.pointerId);
                if (p.moved < TapMove && Time.unscaledTime - p.time < TapTime) World.Tap(e.position);
            }
        }

        void OnDisable()
        {
            stickPointer = int.MinValue;
            presses.Clear();
            if (World != null) World.Stick = Vector2.zero;
        }
    }

    /// <summary>Shortcut to every game, for players who'd rather not walk.</summary>
    public sealed class GamesModal : UIModal
    {
        readonly System.Collections.Generic.List<(string title, string sub, Sprite art, int dice, Color color, System.Action open)> games =
            new System.Collections.Generic.List<(string, string, Sprite, int, Color, System.Action)>();

        /// <summary>Dice games show dice instead of a picture (art is null): how many.</summary>

        protected override void Build()
        {
            games.Add((Loc.T("BLACKJACK"), Loc.T("4 tables  •  pays 3 to 2"), ProceduralArt.IconSprite(Icon.Cards), 0, Palette.Tables[0].Felt,
                () => App.I.UI.Modal<TableSelectModal>()));
            games.Add((Loc.T("HOLD'EM"), Loc.T("Ultimate Texas Hold'em"), ProceduralArt.IconSprite(Icon.Cards), 0, Palette.Hex("123E6E"),
                () => App.I.UI.Show<HoldemScreen>()));
            games.Add((Loc.T("BACCARAT"), Loc.T("Player, Banker or Tie"), ProceduralArt.IconSprite(Icon.Cards), 0, Palette.Hex("6A1022"),
                () => App.I.UI.Show<BaccaratScreen>()));
            games.Add((Loc.T("SLOTS"), Loc.T("Lucky Ace 7s  •  10 lines"), ProceduralArt.SlotSymbol(Casino.Core.Slots.SlotSymbol.Seven), 0, Palette.Hex("5B2A9E"),
                () => App.I.UI.Show<SlotsScreen>()));
            games.Add((Loc.T("VIDEO POKER"), Loc.T("Jacks or Better"), ProceduralArt.IconSprite(Icon.Cards), 0, Palette.Hex("0B2A4A"),
                () => App.I.UI.Show<VideoPokerScreen>()));
            games.Add((Loc.T("SIC BO"), Loc.T("Three dice  •  big or small"), null, 3, Palette.Hex("7A1446"),
                () => App.I.UI.Show<SicBoScreen>()));
            games.Add((Loc.T("CRAPS"), Loc.T("Pass Line  •  Field  •  Odds"), null, 2, Palette.Hex("0D5E3A"),
                () => App.I.UI.Show<CrapsScreen>()));
            games.Add((Loc.T("ROULETTE"), Loc.T("European single zero"), ProceduralArt.RouletteWheel, 0, Palette.Hex("8A1730"),
                () => App.I.UI.Show<RouletteScreen>()));

            const int perRow = 4;
            int rows = (games.Count + perRow - 1) / perRow;
            const float tileW = 380f, tileH = 340f, gap = 24f;
            MakePanel(perRow * (tileW + gap) + 80f, rows * (tileH + gap) + 200f, Loc.T("GAMES"));
            AddCloseButton();
            for (int i = 0; i < games.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                int inRow = System.Math.Min(perRow, games.Count - row * perRow);
                float x = (col - (inRow - 1) / 2f) * (tileW + gap);
                float y = ((rows - 1) / 2f - row) * (tileH + gap) - 40f;
                var g = games[i];
                Tile(x, y, tileW, tileH, g.title, g.sub, g.art, g.dice, g.color, g.open);
            }
        }

        void Tile(float x, float y, float w, float h, string title, string sub, Sprite art, int dice, Color color, System.Action open)
        {
            var bg = UIKit.Panel(Panel, title, Palette.Shade(color, 0.8f), 28f, true);
            var rt = ((RectTransform)bg.transform).Center(x, y, w, h);
            UIKit.Outline(rt, Palette.Gold, 28f);
            if (art != null)
            {
                var img = UIKit.Image(rt, "Art", art, Color.white);
                img.preserveAspect = true;
                ((RectTransform)img.transform).Center(0, 92, 120, 120);
            }
            else
            {
                int n = dice;
                int[] faces = { 5, 3, 6 };
                for (int i = 0; i < n; i++)
                {
                    var die = DieView.Create(rt, 62, Palette.Hex(n == 2 ? "E8243C" : "FFF8EC"), n == 2 ? Color.white : Palette.Hex("17141C"));
                    die.Set(faces[i]);
                    die.Rect.Center((i - (n - 1) / 2f) * 70, 92, 62, 62);
                    die.Rect.localEulerAngles = new Vector3(0, 0, (i - 1) * 12f);
                }
            }
            var t = UIKit.Label(rt, title, 50, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)t.transform).Center(0, 0, w - 30, 60);
            var s = UIKit.Label(rt, sub, 22, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)s.transform).Center(0, -44, w - 30, 32);
            var play = UIKit.Button(rt, Loc.T("PLAY"), ButtonStyle.Gold, () => { Close(); open(); }, 34, Icon.Play);
            play.Rect.At(0.5f, 0f, 0, 46, 210, 70);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => { Close(); open(); });
        }
    }

    public static class ExhaustedPrompt
    {
        public static void Show() => App.I.UI.Modal<ConfirmModal>(m => m.Setup(Loc.T("YOU'RE EXHAUSTED"),
            Loc.T("Your energy bar is empty. Grab something from the bar to keep playing, or come back later: energy recovers slowly while you're away."),
            Loc.T("VISIT THE BAR"), () => App.I.UI.Modal<ShopModal>(s => s.StartTab = ShopTab.Food), Loc.T("LATER"), null, ButtonStyle.Green));
    }

    /// <summary>Out of chips at a table: a free video when one is available, otherwise the cashier.</summary>
    public static class ChipOffer
    {
        public static void Show(System.Action afterVideo)
        {
            bool ad = App.I.Purchases.CanWatchAd;
            App.I.UI.Modal<ConfirmModal>(m => m.Setup(Loc.T("OUT OF CHIPS"),
                ad ? Loc.F("Watch a short video for {0} free chips, or visit the cashier.", ChipFormat.Full(GameConfig.AdRewardChips))
                    : Loc.T("Visit the cashier to top up your chips."),
                ad ? Loc.T("FREE CHIPS") : Loc.T("CASHIER"),
                () =>
                {
                    if (ad) App.I.Purchases.WatchAdForChips(_ => afterVideo?.Invoke());
                    else App.I.UI.Modal<ShopModal>(s => s.StartTab = ShopTab.Chips);
                },
                ad ? Loc.T("CASHIER") : Loc.T("LATER"),
                () =>
                {
                    if (ad) App.I.UI.Modal<ShopModal>(s => s.StartTab = ShopTab.Chips);
                }, ButtonStyle.Green));
        }
    }

    /// <summary>Pick a table: each has its own stakes, felt colour and dealer.</summary>
    public sealed class TableSelectModal : UIModal
    {
        protected override void Build()
        {
            MakePanel(1700, 820, Loc.T("CHOOSE YOUR TABLE"));
            AddCloseButton();
            var tables = TableCatalog.Blackjack;
            for (int i = 0; i < tables.Count; i++)
            {
                var def = tables[i];
                var theme = Palette.Tables[def.Theme];
                float x = -585 + i * 390;
                var card = UIKit.Panel(Panel, def.Name, Palette.Shade(theme.Felt, 0.8f), 30f, true);
                var rt = ((RectTransform)card.transform).Center(x, -40, 360, 600);
                UIKit.Outline(rt, theme.Accent, 30f);
                var label = UIKit.Label(rt, Loc.T(theme.Label), 56, theme.Accent, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)label.transform).At(0.5f, 1f, 0, -50, 320, 70);
                var dealer = Staff.Portrait(rt, 200, StaffRole.Dealer, def.Theme);
                dealer.Rect.Center(0, 90, 200, 200);
                var stakes = UIKit.Label(rt, Loc.T("BETS") + $"\n<size=46>{ChipFormat.Short(def.MinBet)} - {ChipFormat.Short(def.MaxBet)}</size>", 26, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)stakes.transform).Center(0, -90, 320, 110);

                bool unlocked = App.I.Progress.Unlocked(def);
                bool affordable = App.I.Wallet.Balance >= def.MinBet;
                string sitText = !unlocked ? Loc.F("LEVEL {0}", def.Level) : affordable ? Loc.T("SIT DOWN") : Loc.F("NEED {0}", ChipFormat.Short(def.MinBet));
                var sit = UIKit.Button(rt, sitText, unlocked && affordable ? ButtonStyle.Gold : ButtonStyle.Dark, () => Sit(def), 40,
                    unlocked ? (Icon?)null : Icon.Lock);
                sit.Rect.At(0.5f, 0f, 0, 70, 300, 96);
                if (!unlocked)
                    sit.SetOnClick(() => App.I.UI.Toast(Loc.F("Reach level {0} to play here. You're level {1}.", def.Level, App.I.Progress.Level), Palette.Gold, Icon.Lock));
                else if (!affordable) sit.SetOnClick(() => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips));
            }
        }

        internal void TourSit(TableDef def) => Sit(def);

        void Sit(TableDef def)
        {
            Close();
            App.I.Save.lastTableId = def.Id;
            App.I.UI.Show<BlackjackScreen>(s => s.Table = def);
        }
    }
}
