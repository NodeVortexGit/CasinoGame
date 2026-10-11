using System;
using System.Collections.Generic;
using System.Globalization;
using Casino.Art;
using Casino.Core.Player;
using Casino.Platform;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// Settings with tabs down the left: General, Graphics, Sound, Touch (Mouse on computers), and when there is
    /// something to set, Controller (only once a controller is detected) and Vibration. Test builds add a Dev tab.
    /// The tabs rebuild by themselves when a controller is connected or unplugged.
    /// </summary>
    public sealed class SettingsModal : UIModal
    {
        enum Tab { General, Graphics, Sound, Touch, Controller, Vibration, Dev }

        const float RowTop = 300f, ContentLeft = -390f, ContentRight = 700f;

        static Tab tab = Tab.General;
        RectTransform body, page;
        readonly Dictionary<Tab, RectTransform> pages = new Dictionary<Tab, RectTransform>();
        readonly Dictionary<Tab, UIButton> tabButtons = new Dictionary<Tab, UIButton>();
        Text regionText, padText, swapText;
        float y, nextPreview;

        public override GameObject DefaultFocus => tabButtons.TryGetValue(tab, out var b) && b != null ? b.gameObject : null;

        static bool Mobile => Application.isMobilePlatform;

        protected override void Build()
        {
            MakePanel(1500, 960, "SETTINGS");
            AddCloseButton();
            App.I.Region.Changed += RefreshRegion;
            Pad.Changed += RefreshPad;
            Pad.DeviceChanged += Rebuild;
            Rebuild();
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Region.Changed -= RefreshRegion;
            Pad.Changed -= RefreshPad;
            Pad.DeviceChanged -= Rebuild;
        }

        static List<Tab> VisibleTabs()
        {
            var tabs = new List<Tab> { Tab.General, Tab.Graphics, Tab.Sound, Tab.Touch };
            if (Pad.Connected) tabs.Add(Tab.Controller);
            if (Haptics.PhoneSupported || Pad.Connected) tabs.Add(Tab.Vibration);
            if (GameConfig.TestServices) tabs.Add(Tab.Dev);
            return tabs;
        }

        static string TabName(Tab t) => t == Tab.Touch ? (Mobile ? "TOUCH" : "MOUSE") : t.ToString().ToUpperInvariant();

        void Rebuild()
        {
            if (body != null) Destroy(body.gameObject);
            pages.Clear();
            tabButtons.Clear();
            regionText = padText = swapText = null;
            body = UIKit.NewRect("Body", Panel).Stretch();

            var tabs = VisibleTabs();
            if (!tabs.Contains(tab)) tab = Tab.General;
            float ty = RowTop;
            foreach (var t in tabs)
            {
                var b = UIKit.Button(body, TabName(t), ButtonStyle.Dark, () => ShowTab(t), 32, null, FontKind.Display, 22f);
                b.Rect.Center(-585, ty, 250, 72);
                tabButtons[t] = b;
                ty -= 86f;
            }
            var divider = UIKit.Panel(body, "Divider", new Color(1, 1, 1, 0.08f), 2f);
            ((RectTransform)divider.transform).Center(-425, -40, 3, 780);

            foreach (var t in tabs)
            {
                page = pages[t] = UIKit.NewRect(t.ToString(), body).Stretch();
                y = RowTop;
                switch (t)
                {
                    case Tab.General: GeneralTab(); break;
                    case Tab.Graphics: GraphicsTab(); break;
                    case Tab.Sound: SoundTab(); break;
                    case Tab.Touch: TouchTab(); break;
                    case Tab.Controller: ControllerTab(); break;
                    case Tab.Vibration: VibrationTab(); break;
                    case Tab.Dev: DevTab(); break;
                }
            }
            ShowTab(tab);

            var credits = UIKit.Label(body,
                "18+ • Chips have no cash value • Fonts: Bebas Neue, Poppins (SIL OFL) • Borders: Natural Earth • Price data: World Bank",
                18, Palette.WithAlpha(Palette.TextMuted, 0.7f), TextAnchor.MiddleRight);
            ((RectTransform)credits.transform).At(1f, 0f, -50, 26, 1100, 40, 1f, 0f);
            credits.horizontalOverflow = HorizontalWrapMode.Overflow;
            RefreshRegion();
            RefreshPad();
        }

        void ShowTab(Tab t)
        {
            tab = t;
            foreach (var p in pages) p.Value.gameObject.SetActive(p.Key == t);
            foreach (var b in tabButtons) b.Value.SetStyle(b.Key == t ? ButtonStyle.Gold : ButtonStyle.Dark);
        }

        // ---------------- tabs ----------------

        void GeneralTab()
        {
            var save = App.I.Save;
            Row("CASINO STAFF", Segmented.Create(page, new[] { "WOMEN", "MEN", "MIXED" }, save.npcStyle, i =>
            {
                save.npcStyle = i;
                App.I.SaveSystem.MarkDirty();
                // Re-open the lobby so the new staff appear right away (tables pick it up next time you sit down).
                Closed -= ReloadLobby;
                Closed += ReloadLobby;
            }, 520));

            Row("REMINDERS", Switch.Create(page, save.remindersEnabled, on =>
            {
                save.remindersEnabled = on;
                save.remindersAsked = true;
                App.I.SaveSystem.MarkDirty();
                if (on) App.I.StartCoroutine(Reminders.AskPermission(null));
            }).transform, "Free chips ready, and an evening nudge before a streak would end.");

            var recheck = UIKit.Button(page, "CHECK AGAIN", ButtonStyle.Dark, () =>
            {
                App.I.StartCoroutine(App.I.Region.Resolve());
                RefreshRegion();
            }, 32, Icon.Globe, FontKind.Display);
            recheck.Rect.sizeDelta = new Vector2(300, 70);
            regionText = Row("PRICING REGION", recheck.transform, " ");
        }

        void GraphicsTab()
        {
            var save = App.I.Save;
            Row("QUALITY", Segmented.Create(page, new[] { "LOW", "MEDIUM", "HIGH", "ULTRA" }, App.I.GraphicsQuality, i =>
            {
                save.graphicsQuality = i;
                Changed(true);
            }, 640), "Texture detail, filtering and character detail.");

            if (Mobile)
            {
                float[] scales = { 0.5f, 0.67f, 0.75f, 0.85f, 1f };
                Row("RESOLUTION", Segmented.Create(page, new[] { "50%", "67%", "75%", "85%", "100%" }, Nearest(scales, save.renderScale), i =>
                {
                    save.renderScale = scales[i];
                    Changed(true);
                }, 640), "Lower is softer but runs cooler and saves battery.");
            }
            else
            {
                Row("DISPLAY MODE", Segmented.Create(page, new[] { "FULLSCREEN", "WINDOWED" }, save.windowed ? 1 : 0, i =>
                {
                    save.windowed = i == 1;
                    Changed(true);
                }, 520));
            }

            Row("ANTI-ALIASING", Segmented.Create(page, new[] { "OFF", "2×", "4×" }, save.antiAliasing >= 4 ? 2 : save.antiAliasing >= 2 ? 1 : 0, i =>
            {
                save.antiAliasing = i == 0 ? 0 : i == 1 ? 2 : 4;
                Changed(true);
            }, 480), "Smooths jagged edges in the casino.");

            bool fast = App.HighRefreshScreen;
            var rates = fast ? new[] { "30", "60", "120" } : new[] { "30", "60" };
            int rate = save.targetFps >= 120 && fast ? 2 : save.targetFps <= 30 ? 0 : 1;
            Row("FRAME RATE", Segmented.Create(page, Array.ConvertAll(rates, r => r + " FPS"), rate, i =>
            {
                save.targetFps = i == 0 ? 30 : i == 1 ? 60 : 120;
                Changed(true);
            }, fast ? 600 : 420), Mobile ? "30 FPS saves battery and keeps older phones cool." : null);

            if (!Mobile)
                Row("V-SYNC", Switch.Create(page, save.vSync, on =>
                {
                    save.vSync = on;
                    Changed(true);
                }).transform);

            Row("FIELD OF VIEW", ValueSlider(Mathf.InverseLerp(70f, 110f, save.fieldOfView), v => Mathf.RoundToInt(Mathf.Lerp(70f, 110f, v)) + "°", v =>
            {
                save.fieldOfView = Mathf.Round(Mathf.Lerp(70f, 110f, v));
                Changed(false);
            }));

            Row("SHOW FPS", Switch.Create(page, save.showFps, on =>
            {
                save.showFps = on;
                Changed(true);
            }).transform);
        }

        void SoundTab()
        {
            var save = App.I.Save;
            Row("MASTER VOLUME", ValueSlider(save.masterVolume, Percent, v =>
            {
                save.masterVolume = v;
                AudioChanged();
            }));
            Row("MUSIC", ValueSlider(save.musicVolume, Percent, v =>
            {
                save.musicVolume = v;
                AudioChanged();
            }));
            Row("SOUND EFFECTS", ValueSlider(save.sfxVolume, Percent, v =>
            {
                save.sfxVolume = v;
                AudioChanged();
            }));
            Row("STAFF VOICES", ValueSlider(save.voiceVolume, Percent, v =>
            {
                save.voiceVolume = v;
                AudioChanged();
                if (Time.unscaledTime > nextPreview)
                {
                    nextPreview = Time.unscaledTime + 0.15f;
                    Casino.Audio.Sfx.Voice(Casino.Audio.VoicePitch.High);
                }
            }), "The murmur staff make while they talk.");
            if (!Mobile)
                Row("MUTE IN BACKGROUND", Switch.Create(page, save.muteInBackground, on =>
                {
                    save.muteInBackground = on;
                    AudioChanged();
                }).transform, "Silence the casino while you're in another window.");
        }

        void TouchTab()
        {
            var save = App.I.Save;
            string what = Mobile ? "dragging" : "dragging with the mouse";
            Row("LOOK SENSITIVITY", SpeedSlider(save.lookSensitivity, k => save.lookSensitivity = k), $"How fast {what} turns the view.");
            Row("INVERT LOOK LEFT / RIGHT", Switch.Create(page, save.invertLookX, on =>
            {
                save.invertLookX = on;
                App.I.SaveSystem.MarkDirty();
            }).transform);
            Row("INVERT LOOK UP / DOWN", Switch.Create(page, save.invertLookY, on =>
            {
                save.invertLookY = on;
                App.I.SaveSystem.MarkDirty();
            }).transform);
        }

        void ControllerTab()
        {
            var save = App.I.Save;
            padText = Row("BUTTON LAYOUT", Segmented.Create(page, new[] { "AUTO", "XBOX", "PS", "NINTENDO" }, Mathf.Clamp(save.padLayout, 0, 3), i =>
            {
                save.padLayout = i;
                App.I.SaveSystem.MarkDirty();
                Pad.NotifyChanged();
            }, 720), " ");
            swapText = Row("SWAP MENU / VIEW", Switch.Create(page, save.padSwapMenuView, on =>
            {
                save.padSwapMenuView = on;
                App.I.SaveSystem.MarkDirty();
                Pad.NotifyChanged();
                RefreshSwap();
            }).transform, " ");
            RefreshSwap();
            Row("STICK SENSITIVITY", SpeedSlider(save.padLookSensitivity, k => save.padLookSensitivity = k), "How fast the right stick turns the view.");
            Row("INVERT STICK LEFT / RIGHT", Switch.Create(page, save.padInvertX, on =>
            {
                save.padInvertX = on;
                App.I.SaveSystem.MarkDirty();
            }).transform);
            Row("INVERT STICK UP / DOWN", Switch.Create(page, save.padInvertY, on =>
            {
                save.padInvertY = on;
                App.I.SaveSystem.MarkDirty();
            }).transform);
            Row("CONTROLLER HINTS", Switch.Create(page, save.padHints, on =>
            {
                save.padHints = on;
                App.I.SaveSystem.MarkDirty();
            }).transform, "Shows what each button does while you play with a controller.");
        }

        void VibrationTab()
        {
            var save = App.I.Save;
            bool phone = Haptics.PhoneSupported, pad = Pad.Connected;
            if (phone && pad)
                Row("VIBRATE ON", Segmented.Create(page, new[] { "AUTO", "PHONE", "CONTROLLER", "BOTH" }, Mathf.Clamp(save.vibrateOn, 0, 3), i =>
                {
                    save.vibrateOn = i;
                    App.I.SaveSystem.MarkDirty();
                    Haptics.Play(Haptic.Success);
                }, 820), "Auto: the controller while one is connected, otherwise the phone.");
            if (phone)
            {
                Row("PHONE VIBRATION", Switch.Create(page, save.phoneVibration, on =>
                {
                    save.phoneVibration = on;
                    App.I.SaveSystem.MarkDirty();
                }).transform);
                Row("PHONE STRENGTH", ValueSlider(save.phoneVibrationStrength, Percent, v =>
                {
                    save.phoneVibrationStrength = v;
                    App.I.SaveSystem.MarkDirty();
                    Preview(true, v);
                }));
            }
            if (pad)
            {
                Row("CONTROLLER VIBRATION", Switch.Create(page, save.padVibration, on =>
                {
                    save.padVibration = on;
                    App.I.SaveSystem.MarkDirty();
                }).transform);
                Row("CONTROLLER STRENGTH", ValueSlider(save.padVibrationStrength, Percent, v =>
                {
                    save.padVibrationStrength = v;
                    App.I.SaveSystem.MarkDirty();
                    Preview(false, v);
                }));
            }
        }

        void DevTab()
        {
            Row("TEST COUNTRY", Segmented.Create(page, new[] { "AUTO", "US", "BG", "IN" }, 0, i =>
            {
                if (i == 0) App.I.StartCoroutine(App.I.Region.Resolve());
                else App.I.Region.SetDevOverride(new[] { "", "US", "BG", "IN" }[i]);
            }, 520));
            var reset = UIKit.Button(page, "RESET PROGRESS", ButtonStyle.Red, () =>
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
            reset.Rect.sizeDelta = new Vector2(360, 70);
            Row("TEST BUILD", reset.transform, "Wipes this device's save and starts over.");
        }

        // ---------------- helpers ----------------

        void Changed(bool graphics)
        {
            App.I.SaveSystem.MarkDirty();
            if (graphics) App.I.ApplyGraphics();
        }

        static void AudioChanged()
        {
            App.I.ApplyAudio();
            App.I.SaveSystem.MarkDirty();
        }

        void Preview(bool phone, float strength)
        {
            if (Time.unscaledTime < nextPreview) return;
            nextPreview = Time.unscaledTime + 0.25f;
            Haptics.Preview(phone, strength);
        }

        static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        static int Nearest(float[] values, float v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
                if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
            return best;
        }

        /// <summary>A 0..1 slider with its value shown on the left.</summary>
        RectTransform ValueSlider(float value, Func<float, string> format, Action<float> changed)
        {
            var rt = UIKit.NewRect("ValueSlider", page);
            rt.sizeDelta = new Vector2(660, 60);
            var label = UIKit.Label(rt, format(value), 34, Palette.GoldLight, TextAnchor.MiddleRight, FontKind.Display);
            ((RectTransform)label.transform).At(0f, 0.5f, 0, 0, 120, 60, 0f, 0.5f);
            var slider = SliderFactory.Create(rt, Mathf.Clamp01(value), v =>
            {
                label.text = format(v);
                changed(v);
            }, 520);
            ((RectTransform)slider.transform).At(1f, 0.5f, 0, 0, 520, 50, 1f, 0.5f);
            return rt;
        }

        /// <summary>Look speed on a log scale: the middle is 1×, the ends a quarter and four times as fast (snaps to 1×).</summary>
        RectTransform SpeedSlider(float current, Action<float> set)
        {
            static float Speed(float v)
            {
                float k = Mathf.Pow(4f, v * 2f - 1f);
                return Mathf.Abs(k - 1f) < 0.06f ? 1f : k;
            }
            float start = Mathf.InverseLerp(-1f, 1f, Mathf.Log(Mathf.Clamp(current, 0.25f, 4f), 4f));
            return ValueSlider(start, v => Speed(v).ToString("0.0#", CultureInfo.InvariantCulture) + "×", v =>
            {
                set(Speed(v));
                App.I.SaveSystem.MarkDirty();
            });
        }

        static void ReloadLobby()
        {
            if (App.I.UI.Current is LobbyScreen) App.I.UI.Show<LobbyScreen>();
        }

        void RefreshRegion()
        {
            if (regionText != null) regionText.text = App.I.Region.Describe();
        }

        void RefreshSwap()
        {
            if (swapText != null)
                swapText.text = $"{Pad.Name(PadButton.Menu)} opens Settings, {Pad.Name(PadButton.View)} opens Daily.";
        }

        void RefreshPad()
        {
            RefreshSwap();
            if (padText == null) return;
            string family = Pad.FamilyName(Pad.Family);
            padText.text = !Pad.Connected ? "No controller connected."
                : $"Connected: {Pad.DeviceName}. Showing {family} buttons" + (Pad.Family == PadFamily.Nintendo ? " (A on the right confirms)." : ".");
        }

        /// <summary>One setting: its name on the left, the control right-aligned, and an optional hint underneath.</summary>
        Text Row(string label, Transform control, string hint = null)
        {
            Text h = null;
            var t = UIKit.Label(page, label, 40, Color.white, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)t.transform).At(0.5f, 0.5f, ContentLeft, y, 640, 60, 0f, 0.5f);
            var crt = (RectTransform)control;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(ContentRight, y);
            if (hint != null)
            {
                h = UIKit.Label(page, hint, 22, Palette.TextMuted, TextAnchor.MiddleLeft);
                ((RectTransform)h.transform).At(0.5f, 0.5f, ContentLeft + 4f, y - 46f, 1080, 34, 0f, 0.5f);
                y -= 40f;
            }
            y -= 92f;
            return h;
        }
    }
}
