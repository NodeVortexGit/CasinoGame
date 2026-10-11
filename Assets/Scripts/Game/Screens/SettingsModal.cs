using Casino.Art;
using Casino.Audio;
using Casino.Core.Player;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    public sealed class SettingsModal : UIModal
    {
        Text regionText;
        float y;

        protected override void Build()
        {
            bool dev = GameConfig.TestServices;
            MakePanel(1500, dev ? 1010 : 920, "SETTINGS");
            AddCloseButton();
            var save = App.I.Save;
            y = dev ? 330 : 290;

            Row("MUSIC", SliderFactory.Create(Panel, save.musicVolume, v =>
            {
                save.musicVolume = v;
                App.I.Music.SetVolume(v);
                App.I.SaveSystem.MarkDirty();
            }, 520).transform);

            Row("SOUND EFFECTS", SliderFactory.Create(Panel, save.sfxVolume, v =>
            {
                save.sfxVolume = v;
                Sfx.Volume = v;
                App.I.SaveSystem.MarkDirty();
            }, 520).transform);

            Row("CASINO STAFF", Segmented.Create(Panel, new[] { "WOMEN", "MEN", "MIXED" }, save.npcStyle, i =>
            {
                save.npcStyle = i;
                App.I.SaveSystem.MarkDirty();
                // Re-open the lobby so the new staff appear right away (tables pick it up next time you sit down).
                Closed -= ReloadLobby;
                Closed += ReloadLobby;
            }, 520));

            Row("DEALERS ASK ME TO STAY", Switch.Create(Panel, save.persuasionEnabled, on =>
            {
                save.persuasionEnabled = on;
                App.I.SaveSystem.MarkDirty();
            }).transform, "When you leave a table, the dealer tries to talk you into one more hand. Off by default; Leave always works.");

            Row("FRAME RATE", Segmented.Create(Panel, new[] { "30 FPS", "60 FPS" }, save.targetFps <= 30 ? 0 : 1, i =>
            {
                save.targetFps = i == 0 ? 30 : 60;
                App.I.ApplyFrameRate();
                App.I.SaveSystem.MarkDirty();
            }, 520), "30 FPS saves battery and keeps older phones cool.");

            var recheck = UIKit.Button(Panel, "CHECK AGAIN", ButtonStyle.Dark, () =>
            {
                App.I.StartCoroutine(App.I.Region.Resolve());
                RefreshRegion();
            }, 32, Icon.Globe, FontKind.Display);
            recheck.Rect.sizeDelta = new Vector2(300, 70);
            regionText = Row("PRICING REGION", recheck.transform, " ");
            App.I.Region.Changed += RefreshRegion;
            RefreshRegion();

            if (dev)
            {
                Row("DEV: TEST COUNTRY", Segmented.Create(Panel, new[] { "AUTO", "US", "BG", "IN" }, 0, i =>
                {
                    if (i == 0) App.I.StartCoroutine(App.I.Region.Resolve());
                    else App.I.Region.SetDevOverride(new[] { "", "US", "BG", "IN" }[i]);
                }, 520));
                var reset = UIKit.Button(Panel, "DEV: RESET PROGRESS", ButtonStyle.Red, () =>
                    App.I.UI.Modal<ConfirmModal>(m => m.Setup("Reset everything?", "Deletes chips, stats and your profile (test builds only).",
                        "RESET", () =>
                        {
                            App.I.SaveSystem.DeleteAll();
                            SaveSystem.UnderageBlocked = false;
                            App.I.Wallet.TryDebit(App.I.Wallet.Balance);
                            App.I.Wallet.Credit(GameConfig.StartingChips);
                            App.I.Luck.HandsPlayed = 0;
                            App.I.Energy.Add(1f);
                            App.I.Route();
                        }, "CANCEL", null, ButtonStyle.Red)), 30);
                reset.Rect.At(0f, 0f, 70, 40, 420, 70, 0f, 0f);
            }

            var credits = UIKit.Label(Panel,
                "18+ • Chips have no cash value • Fonts: Bebas Neue, Poppins (SIL OFL) • Borders: Natural Earth • Price data: World Bank",
                18, Palette.WithAlpha(Palette.TextMuted, 0.7f), TextAnchor.MiddleRight);
            ((RectTransform)credits.transform).At(1f, 0f, -50, 40, 960, 60, 1f, 0f);
        }

        static void ReloadLobby()
        {
            if (App.I.UI.Current is LobbyScreen) App.I.UI.Show<LobbyScreen>();
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Region.Changed -= RefreshRegion;
        }

        void RefreshRegion()
        {
            if (regionText != null) regionText.text = App.I.Region.Describe();
        }

        Text Row(string label, Transform control, string hint = null)
        {
            Text h = null;
            var t = UIKit.Label(Panel, label, 40, Color.white, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)t.transform).Center(-370, y, 620, 60);
            var crt = (RectTransform)control;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(680, y);
            if (hint != null)
            {
                h = UIKit.Label(Panel, hint, 22, Palette.TextMuted, TextAnchor.MiddleLeft);
                ((RectTransform)h.transform).Center(-170, y - 46, 1000, 34);
                y -= 40;
            }
            y -= 100;
            return h;
        }
    }
}
