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
        UIButton freeChips;
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
                App.I.UI.Modal<ConfirmModal>(m => m.Setup("Leave the casino?", "Your chips and progress are saved.", "QUIT", App.I.Quit, "STAY")));

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
            action = UIKit.Button(actionGroup, "PLAY", ButtonStyle.Gold, () => { if (shownSpot != null) world.Use(shownSpot); }, 52, Icon.Play);
            action.Rect.At(1f, 0f, 0, 0, 520, 136, 1f, 0f);
            action.gameObject.AddComponent<Pulse>();
            actionTitle = action.LabelText;
            actionGroup.gameObject.SetActive(false);

            // Quick buttons (right edge): free chips and a table list for people who'd rather not walk.
            freeChips = UIKit.Button(Content, "FREE CHIPS", ButtonStyle.Green, () => App.I.Purchases.WatchAdForChips(), 34, Icon.Video);
            freeChips.Rect.At(1f, 0.5f, -40, 150, 330, 100, 1f, 0.5f);
            freeChipsInfo = UIKit.Label(Content, "", 22, Color.white, TextAnchor.MiddleRight, FontKind.Semi);
            ((RectTransform)freeChipsInfo.transform).At(1f, 0.5f, -46, 80, 420, 32, 1f, 0.5f);
            UIKit.TextShadow(freeChipsInfo, -2f, 0.8f);
            var tables = UIKit.Button(Content, "GAMES", ButtonStyle.Purple, OpenGames, 34, Icon.Cards);
            tables.Rect.At(1f, 0.5f, -40, 0, 330, 100, 1f, 0.5f);

            hint = UIKit.Label(Content, "Drag left side to walk  •  Drag right side to look  •  Tap to go", 26, Color.white, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)hint.transform).At(0.5f, 0f, 0, 250, 1400, 40);
            UIKit.TextShadow(hint, -2f, 0.9f);

            var foot = UIKit.Label(Content, "18+ only  •  Chips are for fun: no cash value, never exchangeable for money or prizes", 20, Palette.WithAlpha(Color.white, 0.75f));
            ((RectTransform)foot.transform).At(0.5f, 0f, 0, 14, 1600, 30);
            UIKit.TextShadow(foot, -2f, 0.9f);

            // Loading cover while the casino is being built (first visit only).
            var cover = UIKit.NewRect("Loading", Content).Stretch(-200, -200, -200, -200);
            loading = UIKit.Group(cover);
            Backgrounds.Casino(cover, Palette.Hex("3A1066"), Palette.Hex("0C0614"), new Vector2(0, 200));
            Logo.Create(cover, 120, 0.9f);
            var wait = UIKit.Label(cover, "Opening the doors...", 40, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
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
            if (App.I.Energy.IsEmpty) world.HostSays(DealerLines.Pick(DealerMoment.Exhausted, App.I.Rng));
            else if (App.I.Wallet.Balance < TableCatalog.Blackjack[0].MinBet) world.HostSays(DealerLines.Pick(DealerMoment.OutOfChips, App.I.Rng));
            else if (App.I.Save.handsPlayed == 0) world.HostSays("Welcome! Your 1,000 welcome chips are ready. The tables are straight ahead!");
        }

        void OnStaffSpeaks(StaffFigure fig, string line)
        {
            speaker = fig;
            speakerUntil = Time.unscaledTime + 3.4f + line.Length * 0.022f;
            bubble.Say(line, 3.2f, null, fig.Female ? VoicePitch.High : VoicePitch.Low);
            PlaceBubble();
        }

        void PlaceBubble()
        {
            if (speaker == null) return;
            Vector3 screen = world.Camera.WorldToScreenPoint(speaker.HeadPoint + Vector3.up * 0.12f);
            var px = world.Camera.pixelRect;
            bool onScreen = screen.z > 0.2f && screen.z < 9f && screen.x > px.xMin - 80f && screen.x < px.xMax + 80f;
            if (!onScreen && speaker != world.HostFigure)
            {
                // Out of sight: let the line go rather than leaving a bubble floating in the air.
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
                freeChips.Interactable = App.I.Purchases.CanWatchAd;
                freeChipsInfo.text = App.I.Purchases.AdStatus();
            }

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
                    actionTitle.text = spot.Title;
                    action.SetIcon(spot.Icon);
                    action.SetStyle(spot.Style);
                    actionSub.text = spot.Staff != null ? $"{spot.Staff.StaffName}  •  {spot.Subtitle}" : spot.Subtitle;
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
        protected override void Build()
        {
            MakePanel(1500, 700, "GAMES");
            AddCloseButton();
            Tile(-470, "BLACKJACK", "4 tables  •  pays 3 to 2", ProceduralArt.IconSprite(Icon.Cards), Palette.Tables[0].Felt,
                () => App.I.UI.Modal<TableSelectModal>());
            Tile(0, "SLOTS", "Lucky Ace 7s  •  10 lines", ProceduralArt.SlotSymbol(Casino.Core.Slots.SlotSymbol.Seven), Palette.Hex("5B2A9E"),
                () => App.I.UI.Show<SlotsScreen>());
            Tile(470, "ROULETTE", "European single zero", ProceduralArt.RouletteWheel, Palette.Hex("8A1730"),
                () => App.I.UI.Show<RouletteScreen>());
        }

        void Tile(float x, string title, string sub, Sprite art, Color color, System.Action open)
        {
            var bg = UIKit.Panel(Panel, title, Palette.Shade(color, 0.8f), 30f, true);
            var rt = ((RectTransform)bg.transform).Center(x, -40, 420, 500);
            UIKit.Outline(rt, Palette.Gold, 30f);
            var img = UIKit.Image(rt, "Art", art, Color.white);
            img.preserveAspect = true;
            ((RectTransform)img.transform).Center(0, 80, 220, 220);
            var t = UIKit.Label(rt, title, 64, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)t.transform).Center(0, -80, 400, 80);
            var s = UIKit.Label(rt, sub, 24, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)s.transform).Center(0, -135, 400, 40);
            var play = UIKit.Button(rt, "PLAY", ButtonStyle.Gold, () => { Close(); open(); }, 44, Icon.Play);
            play.Rect.At(0.5f, 0f, 0, 70, 260, 96);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => { Close(); open(); });
        }
    }

    public static class ExhaustedPrompt
    {
        public static void Show() => App.I.UI.Modal<ConfirmModal>(m => m.Setup("YOU'RE EXHAUSTED",
            "Your energy bar is empty. Grab something from the bar to keep playing, or come back later: energy recovers slowly while you're away.",
            "VISIT THE BAR", () => App.I.UI.Modal<ShopModal>(s => s.StartTab = ShopTab.Food), "LATER", null, ButtonStyle.Green));
    }

    /// <summary>Pick a table: each has its own stakes, felt colour and dealer.</summary>
    public sealed class TableSelectModal : UIModal
    {
        protected override void Build()
        {
            MakePanel(1700, 820, "CHOOSE YOUR TABLE");
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
                var label = UIKit.Label(rt, theme.Label, 56, theme.Accent, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)label.transform).At(0.5f, 1f, 0, -50, 320, 70);
                var dealer = Staff.Portrait(rt, 200, StaffRole.Dealer, def.Theme);
                dealer.Rect.Center(0, 90, 200, 200);
                var stakes = UIKit.Label(rt, $"BETS\n<size=46>{ChipFormat.Short(def.MinBet)} - {ChipFormat.Short(def.MaxBet)}</size>", 26, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)stakes.transform).Center(0, -90, 320, 110);

                bool affordable = App.I.Wallet.Balance >= def.MinBet;
                var sit = UIKit.Button(rt, affordable ? "SIT DOWN" : "NEED " + ChipFormat.Short(def.MinBet), affordable ? ButtonStyle.Gold : ButtonStyle.Dark,
                    () => Sit(def), 40);
                sit.Rect.At(0.5f, 0f, 0, 70, 300, 96);
                if (!affordable) sit.SetOnClick(() => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips));
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
