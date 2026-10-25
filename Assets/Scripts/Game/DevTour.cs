using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Casino.Audio;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.Platform;
using Casino.Screens;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Casino
{
    /// <summary>
    /// Developer smoke test: launch with <c>-casinoTour &lt;folder&gt;</c> and the game plays through every screen on its
    /// own, saving a screenshot of each step plus a log of any errors, then quits. Not reachable by players.
    /// Add <c>-casinoTourUnderage</c> to test the age block instead, and <c>-tourLanguages bg,ar,hi</c> to screenshot the
    /// main screens in those languages (with <c>-tourLanguagesOnly</c>, nothing else after the lobby).
    /// </summary>
    public sealed class DevTour : MonoBehaviour
    {
        string dir;
        bool underage, languagesOnly;
        string[] languages;
        RenderTexture target;
        Camera cam;
        readonly List<string> errors = new List<string>();
        int shots;

        public static void TryStart(App app)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-casinoTour");
            if (i < 0) return;
            var tour = app.gameObject.AddComponent<DevTour>();
            tour.dir = i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal) ? args[i + 1] : "tour";
            tour.underage = Array.IndexOf(args, "-casinoTourUnderage") >= 0;
            int l = Array.IndexOf(args, "-tourLanguages");
            if (l >= 0 && l + 1 < args.Length) tour.languages = args[l + 1].Split(',');
            tour.languagesOnly = Array.IndexOf(args, "-tourLanguagesOnly") >= 0;
            Directory.CreateDirectory(tour.dir);
            Application.logMessageReceived += tour.OnLog;
            // The tour runs without a window on a developer's machine: keep it silent too. The music is synthesised in
            // OnAudioFilterRead, which the listener volume doesn't reach, so every source is turned down and paused.
            App.ForceMute = true;
            app.ApplyAudio();
            AudioListener.volume = 0f;
            AudioListener.pause = true;

            // Render the UI off-screen at an exact device resolution (default: a 19.5:9 phone, 2340x1080).
            int w = 2340, h = 1080;
            int s = Array.IndexOf(args, "-tourSize");
            if (s >= 0 && s + 1 < args.Length)
            {
                var parts = args[s + 1].Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int pw) && int.TryParse(parts[1], out int ph)) { w = pw; h = ph; }
            }
            tour.target = new RenderTexture(w, h, 24);
            CasinoWorld.TourTarget = tour.target;
            tour.cam = app.GetComponentInChildren<Camera>();
            tour.cam.targetTexture = tour.target;
            tour.cam.cullingMask = 1 << 5;
            var canvas = app.UI.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = tour.cam;
            canvas.planeDistance = 10f;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                errors.Add($"[{type}] {condition}\n{stackTrace}");
        }

        IEnumerator Start()
        {
            yield return Wait(2f);
            yield return Shot("age_gate");
            var gate = App.I.UI.Current as AgeGateScreen;
            if (gate != null)
            {
                if (underage)
                {
                    gate.TourSubmit(1, 1, DateTime.Today.Year - 12);
                    yield return Wait(1.5f);
                    yield return Shot("blocked");
                    Finish();
                    yield break;
                }
                gate.TourSubmit(15, 6, 1990);
                yield return Wait(1f);
                yield return Shot("profile");
                var profile = App.I.UI.Current as ProfileScreen;
                profile.TourChoose(Array.IndexOf(Environment.GetCommandLineArgs(), "-tourFemale") >= 0 ? PlayerGender.Female : PlayerGender.Male);
                yield return Wait(1.2f);
                yield return Shot("profile_chosen");
                profile.TourFinish();
            }
            yield return Wait(1f);
            float building = 0f;
            while (CasinoWorld.Instance != null && !CasinoWorld.Instance.Ready && building < 60f)
            {
                yield return null;
                building += Time.unscaledDeltaTime;
            }
            Debug.Log($"DevTour: casino built in about {building:0.0} s");
            yield return Wait(2f);
            yield return Shot("lobby");
            if (languages != null)
            {
                yield return LanguageTour();
                if (languagesOnly)
                {
                    Finish();
                    yield break;
                }
            }

            // Walk-through of the 3D casino from a few spots.
            var world = CasinoWorld.Instance;
            if (world != null && world.Ready)
            {
                var views = new (string name, Vector2 at, float yaw, float pitch)[]
                {
                    ("casino_entrance_left", new Vector2(0f, -9.6f), -35f, 0f),
                    ("casino_cashier", new Vector2(2.6f, -8.4f), 90f, 0f),
                    ("casino_floor", new Vector2(0f, -5.2f), 0f, -4f),
                    ("casino_table", new Vector2(-4.5f, -3.65f), 0f, 6f),
                    ("casino_bar", new Vector2(-8.4f, 0.6f), -90f, 0f),
                    ("casino_holdem", new Vector2(-6.8f, 5.4f), 0f, 4f),
                    ("casino_baccarat", new Vector2(-4.2f, -5.2f), -135f, 2f),
                    ("casino_sicbo", new Vector2(9.6f, -4.6f), 150f, 6f),
                    ("casino_craps", new Vector2(9.8f, -1.2f), 0f, 6f),
                    ("casino_slots", new Vector2(9.8f, 2.5f), 70f, 0f),
                    ("casino_overview", new Vector2(11.3f, -4.6f), -62f, 6f),
                };
                foreach (var v in views)
                {
                    world.TourView(v.at, v.yaw, v.pitch);
                    yield return Wait(1.2f);
                    yield return Shot(v.name);
                    if (v.name == "casino_entrance_left") Debug.Log("DevTour world: " + world.TourDiagnostics());
                }
                // The same spots at every graphics quality, to compare how real each one looks.
                var compare = new (string name, Vector2 at, float yaw, float pitch)[]
                {
                    ("entrance", new Vector2(0f, -9.6f), -35f, 0f),
                    ("floor", new Vector2(0f, -5.2f), 0f, -4f),
                    ("cashier", new Vector2(2.6f, -8.4f), 90f, 0f),
                    ("table", new Vector2(-4.5f, -3.65f), 0f, 6f),
                    ("bar", new Vector2(-8.4f, 0.6f), -90f, 0f),
                    ("slots", new Vector2(9.8f, 2.5f), 70f, 0f),
                };
                int savedQuality = App.I.Save.graphicsQuality;
                for (int q = 0; q < 4; q++)
                {
                    App.I.Save.graphicsQuality = q;
                    App.I.ApplyGraphics();
                    float restyling = 0f;
                    while (world.TourRestyling && restyling < 30f)
                    {
                        yield return null;
                        restyling += Time.unscaledDeltaTime;
                    }
                    foreach (var v in compare)
                    {
                        world.TourView(v.at, v.yaw, v.pitch);
                        yield return Wait(0.8f);   // long enough for the depth of field to find its focus
                        yield return Shot($"quality{q}_{v.name}");
                    }
                    world.TourFaceStaff(3, 1.1f, 10f);
                    yield return Wait(0.6f);
                    yield return Shot($"quality{q}_face");
                }
                App.I.Save.graphicsQuality = savedQuality;
                App.I.ApplyGraphics();

                // Close-ups of every member of staff (3D, animated).
                for (int i = 0; i < world.TourStaffCount; i++)
                {
                    world.TourFaceStaff(i, 1.7f);
                    yield return Wait(1.0f);
                    yield return Shot("staff_" + world.TourStaffName(i));
                    world.TourFaceStaff(i, 2.6f, 16f);
                    yield return Wait(0.6f);
                    yield return Shot("staffbody_" + world.TourStaffName(i));
                    world.TourStaffTalk(i, 4f);
                    yield return Wait(0.75f);   // the talking hand gesture at its widest
                    yield return Shot("stafftalk_" + world.TourStaffName(i));
                }

                // Walk to a table by tapping it, as a player would.
                world.TourView(new Vector2(-1.5f, -6.5f), -30f, 0f);
                yield return Wait(0.5f);
                world.Tap(world.Camera.WorldToScreenPoint(new Vector3(-4.5f, 1f, -1f)));
                float walking = 0f;
                while (world.Player.AutoWalking && walking < 10f)
                {
                    yield return null;
                    walking += Time.unscaledDeltaTime;
                }
                yield return Wait(1.5f);
                if (App.I.UI.Current is BlackjackScreen) yield return Shot("tapped_table");
                else
                {
                    yield return Shot("tap_walk_failed");
                    Debug.LogError("DevTour: tapping a table did not open it");
                }
                if (App.I.UI.Current is BlackjackScreen sat) sat.TourLeave();
                yield return Wait(1.5f);
                var plea0 = App.I.UI.FindModal<PersuasionModal>();
                plea0?.TourLeave();
                yield return Wait(1.5f);
            }

            var shop = App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips);
            yield return Wait(1f);
            yield return Shot("shop_chips");
            shop.TourTab(ShopTab.Luck);
            yield return Wait(0.8f);
            yield return Shot("shop_luck");
            App.I.UI.CloseAllModals();
            App.I.UI.Modal<LuckOfferModal>();
            yield return Wait(0.8f);
            yield return Shot("luck_offer");
            App.I.UI.CloseAllModals();
            shop = App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips);
            shop.TourTab(ShopTab.Food);
            yield return Wait(0.8f);
            yield return Shot("shop_food");
            shop.Close();
            yield return Wait(0.5f);

            var settings = App.I.UI.Modal<SettingsModal>();
            yield return Wait(0.8f);
            yield return Shot("settings");
            settings.Close();
            yield return Wait(0.5f);

            var tables = App.I.UI.Modal<TableSelectModal>();
            yield return Wait(1f);
            yield return Shot("table_select");
            tables.TourSit(TableCatalog.Blackjack[0]);
            yield return Wait(2f);
            yield return Shot("table");

            var table = App.I.UI.Current as BlackjackScreen;
            for (int hand = 0; hand < 4 && table != null; hand++)
            {
                table.TourBet(2);
                table.TourBet(1);
                yield return Wait(0.4f);
                if (hand == 0) yield return Shot("table_bet");
                table.TourDeal();
                float t = 0f;
                bool shotMid = false;
                while (!table.TourIdle && t < 25f)
                {
                    if (table.TourAwaitingPlayer)
                    {
                        if (!shotMid && hand == 0)
                        {
                            yield return Shot("table_player_turn");
                            shotMid = true;
                        }
                        yield return Wait(0.4f);
                        table.TourPlayBasic();
                    }
                    yield return null;
                    t += Time.unscaledDeltaTime;
                }
                if (hand < 2) yield return Shot("table_result_" + hand);
                yield return Wait(2.5f);
            }

            table?.TourLeave();
            yield return Wait(1f);
            yield return Shot("persuasion");
            var plea = App.I.UI.FindModal<PersuasionModal>();
            plea?.TourLeave();
            yield return Wait(1.5f);
            yield return Shot("lobby_after");

            // Slots
            App.I.UI.Show<SlotsScreen>(s => s.Machine = 1);
            yield return Wait(1.5f);
            yield return Shot("slots");
            var slots = App.I.UI.Current as SlotsScreen;
            for (int i = 0; i < 8 && slots != null; i++)
            {
                slots.TourSpin();
                yield return Wait(0.7f);
                if (i == 0) yield return Shot("slots_spinning");
                float spinT = 0f;
                while (slots.TourSpinning && spinT < 15f)
                {
                    yield return null;
                    spinT += Time.unscaledDeltaTime;
                }
                yield return Wait(1.4f);
                if (i == 0) yield return Shot("slots_result");
            }
            yield return Shot("slots_after_8_spins");
            var paytable = App.I.UI.Modal<SlotPaytableModal>();
            yield return Wait(0.8f);
            yield return Shot("slots_paytable");
            paytable.Close();
            yield return Wait(0.4f);

            // Roulette
            App.I.UI.Show<RouletteScreen>();
            yield return Wait(1.5f);
            yield return Shot("roulette");
            var roulette = App.I.UI.Current as RouletteScreen;
            roulette.TourBet();
            yield return Wait(0.6f);
            yield return Shot("roulette_bets");
            roulette.TourSpin();
            yield return Wait(2.5f);
            yield return Shot("roulette_spinning");
            float rT = 0f;
            while (roulette.TourSpinning && rT < 20f)
            {
                yield return null;
                rT += Time.unscaledDeltaTime;
                if (rT > 4.2f && rT < 4.3f) yield return Shot("roulette_result");
            }
            yield return Wait(0.5f);

            // Hold'em: a few hands played well, with shots at each street of the first.
            App.I.UI.Show<HoldemScreen>();
            yield return Wait(1.5f);
            yield return Shot("holdem");
            var holdem = App.I.UI.Current as HoldemScreen;
            for (int hand = 0; hand < 4 && holdem != null; hand++)
            {
                holdem.TourBet(1);
                yield return Wait(0.4f);
                if (hand == 0) yield return Shot("holdem_bet");
                holdem.TourDeal();
                int street = 0;
                float hT = 0f;
                while (hT < 40f)
                {
                    yield return null;
                    hT += Time.unscaledDeltaTime;
                    if (holdem.TourAwaitingPlayer)
                    {
                        if (hand == 0) yield return Shot("holdem_street" + street);
                        street++;
                        holdem.TourPlay();
                    }
                    else if (holdem.TourIdle) break;
                    if (hand == 0 && street > 0 && hT > 6f && hT < 6.05f) yield return Shot("holdem_showdown");
                }
                yield return Wait(0.3f);
            }
            // Video poker: a few hands with the hint's holds.
            App.I.UI.Show<VideoPokerScreen>();
            yield return Wait(1.5f);
            yield return Shot("videopoker");
            var vp = App.I.UI.Current as VideoPokerScreen;
            for (int hand = 0; hand < 4 && vp != null; hand++)
            {
                vp.TourDealOrDraw();
                float vT = 0f;
                while ((vp.TourBusy || !vp.TourDealt) && vT < 8f)
                {
                    yield return null;
                    vT += Time.unscaledDeltaTime;
                }
                vp.TourHint();
                yield return Wait(0.4f);
                if (hand == 0) yield return Shot("videopoker_held");
                vp.TourDealOrDraw();
                vT = 0f;
                while (vp.TourBusy && vT < 8f)
                {
                    yield return null;
                    vT += Time.unscaledDeltaTime;
                }
                yield return Wait(0.4f);
                if (hand == 0) yield return Shot("videopoker_result");
            }

            // Sic bo: two rolls.
            App.I.UI.Show<SicBoScreen>();
            yield return Wait(1.5f);
            yield return Shot("sicbo");
            var sicbo = App.I.UI.Current as SicBoScreen;
            for (int r = 0; r < 2 && sicbo != null; r++)
            {
                sicbo.TourBet();
                yield return Wait(0.5f);
                if (r == 0) yield return Shot("sicbo_bets");
                sicbo.TourRoll();
                float sT = 0f;
                while (sicbo.TourRolling && sT < 15f)
                {
                    yield return null;
                    sT += Time.unscaledDeltaTime;
                    if (r == 0 && sT > 2.2f && sT < 2.25f) yield return Shot("sicbo_result");
                }
            }

            // Craps: a few rolls on the Pass Line with odds and the field.
            App.I.UI.Show<CrapsScreen>();
            yield return Wait(1.5f);
            yield return Shot("craps");
            var craps = App.I.UI.Current as CrapsScreen;
            for (int r = 0; r < 6 && craps != null; r++)
            {
                craps.TourBet();
                yield return Wait(0.4f);
                if (r == 0) yield return Shot("craps_bets");
                craps.TourRoll();
                float cT = 0f;
                while (craps.TourRolling && cT < 12f)
                {
                    yield return null;
                    cT += Time.unscaledDeltaTime;
                    if (r == 0 && cT > 1.4f && cT < 1.45f) yield return Shot("craps_roll");
                }
                if (r == 0 || (craps.TourPoint > 0 && r < 3)) yield return Shot("craps_after" + r);
            }

            // Baccarat: three coups.
            App.I.UI.Show<BaccaratScreen>();
            yield return Wait(1.5f);
            yield return Shot("baccarat");
            var baccarat = App.I.UI.Current as BaccaratScreen;
            for (int coup = 0; coup < 3 && baccarat != null; coup++)
            {
                baccarat.TourBet(coup);
                yield return Wait(0.4f);
                if (coup == 1) yield return Shot("baccarat_bets");
                baccarat.TourDeal();
                yield return Wait(3.2f);
                if (coup == 1) yield return Shot("baccarat_cards");
                float bT = 0f;
                while (baccarat.TourBusy && bT < 20f)
                {
                    yield return null;
                    bT += Time.unscaledDeltaTime;
                }
            }
            yield return Shot("baccarat_road");
            App.I.UI.Show<LobbyScreen>();
            yield return Wait(2f);
            yield return Shot("lobby_final");
            var gamesMenu = App.I.UI.Modal<GamesModal>();
            yield return Wait(0.8f);
            yield return Shot("games_menu");
            gamesMenu.Close();
            yield return Wait(0.3f);

            // The daily screen: streak, bonus wheel, missions.
            App.I.UI.CloseAllModals();
            var daily = App.I.UI.Modal<DailyModal>();
            yield return Wait(0.8f);
            yield return Shot("daily");
            daily.TourSpin();
            yield return Wait(1.2f);
            yield return Shot("daily_spinning");
            float dT = 0f;
            while (daily != null && daily.TourSpinning && dT < 10f)
            {
                yield return null;
                dT += Time.unscaledDeltaTime;
            }
            yield return Wait(0.6f);
            yield return Shot("daily_won");
            yield return PadTour();
            Finish();
        }

        /// <summary>A virtual controller: D-pad through the lobby buttons, Menu into the Controls tab, then other layouts' hints.</summary>
        IEnumerator PadTour()
        {
            App.I.UI.CloseAllModals();
            var pad = InputSystem.AddDevice<Gamepad>("TourPad");
            yield return Wait(0.5f);
            yield return Press(pad, GamepadButton.DpadUp);
            yield return Wait(0.4f);
            yield return Shot("pad_lobby");
            yield return Press(pad, GamepadButton.DpadUp);
            yield return Wait(0.3f);
            yield return Shot("pad_lobby_moved");

            // Settings with the controller (Select/View/Create opens it by default, see padSwapMenuView), then down the
            // tab list, confirming each tab.
            App.I.Save.padLayout = 2;
            Pad.NotifyChanged();
            yield return Press(pad, GamepadButton.Select);
            yield return Wait(0.7f);
            yield return Shot("settings_general");
            foreach (var name in new[] { "graphics", "sound", "touch", "controller", "vibration" })
            {
                yield return Press(pad, GamepadButton.DpadDown);
                yield return Press(pad, GamepadButton.South);
                yield return Wait(0.4f);
                yield return Shot("settings_" + name);
            }
            yield return Press(pad, GamepadButton.DpadRight);
            yield return Wait(0.3f);
            yield return Shot("settings_vibration_focus");

            App.I.Save.padLayout = 3;
            Pad.NotifyChanged();
            yield return Press(pad, GamepadButton.South);   // Nintendo: B, the bottom button, goes back
            yield return Wait(0.6f);
            yield return Shot("pad_lobby_nintendo");
            App.I.Save.padLayout = 0;
            InputSystem.RemoveDevice(pad);
        }

        static IEnumerator Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
        }

        /// <summary>The main screens in each language of -tourLanguages, priced as a country that speaks it.</summary>
        IEnumerator LanguageTour()
        {
            var world = CasinoWorld.Instance;
            foreach (var code in languages)
            {
                var lang = Loc.Find(code);
                if (lang == null)
                {
                    Debug.LogError("DevTour: no language " + code);
                    continue;
                }
                Loc.Set(code);
                string country = CountryFor(code);
                if (country != null) App.I.Region.SetDevOverride(country);
                yield return Wait(1.5f);
                world?.TourView(new Vector2(2.6f, -8.4f), 90f, 0f);
                yield return Wait(0.8f);
                yield return Shot($"lang_{code}_lobby");
                App.I.UI.Modal<SettingsModal>();
                yield return Wait(0.8f);
                yield return Shot($"lang_{code}_settings");
                App.I.UI.CloseAllModals();
                App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips);
                yield return Wait(0.8f);
                yield return Shot($"lang_{code}_shop");
                App.I.UI.CloseAllModals();
                App.I.UI.Modal<DailyModal>();
                yield return Wait(0.8f);
                yield return Shot($"lang_{code}_daily");
                App.I.UI.CloseAllModals();
                App.I.UI.Modal<LuckInfoModal>();
                yield return Wait(0.8f);
                yield return Shot($"lang_{code}_luck");
                App.I.UI.CloseAllModals();
                App.I.UI.Modal<LanguagePickerModal>();
                yield return Wait(0.8f);
                yield return Shot($"lang_{code}_picker");
                App.I.UI.CloseAllModals();
                world?.TourView(new Vector2(-8.4f, 0.6f), -90f, 0f);
                yield return Wait(0.8f);
                yield return Shot($"lang_{code}_bar");
            }
            Loc.Set("");
            App.I.Region.SetDevOverride("US");
            yield return Wait(1f);
        }

        static string CountryFor(string code)
        {
            switch (code)
            {
                case "en": return "US";
                case "ar": return "SA";
                case "hi": return "IN";
                case "ja": return "JP";
                case "zh-Hans": return "CN";
                case "zh-Hant": return "TW";
                case "pt-BR": return "BR";
                case "pt-PT": return "PT";
                case "he": return "IL";
                case "fa": return "IR";
                case "ur": return "PK";
                case "ko": return "KR";
                case "vi": return "VN";
                case "th": return "TH";
                case "el": return "GR";
                case "uk": return "UA";
                case "ka": return "GE";
                case "hy": return "AM";
                default: return code.Length == 2 ? code.ToUpperInvariant() : null;
            }
        }

        static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
            }
        }

        IEnumerator Shot(string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            var world = CasinoWorld.Instance;
            bool drawWorld = world != null && world.Camera.enabled;
            if (drawWorld)
            {
                world.TourRender(target);
                cam.clearFlags = CameraClearFlags.Depth;
            }
            cam.Render();
            cam.clearFlags = CameraClearFlags.SolidColor;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var tex = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(dir, $"{++shots:00}_{name}.png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        void Finish()
        {
            File.WriteAllText(Path.Combine(dir, "errors.txt"), errors.Count == 0 ? "no errors\n" : string.Join("\n\n", errors));
            Debug.Log($"DevTour finished: {shots} screenshots, {errors.Count} errors -> {dir}");
            Application.Quit();
        }
    }
}
