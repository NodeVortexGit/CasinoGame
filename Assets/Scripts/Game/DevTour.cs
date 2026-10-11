using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.Screens;
using Casino.UI;
using Casino.World;
using UnityEngine;

namespace Casino
{
    /// <summary>
    /// Developer smoke test: launch with <c>-casinoTour &lt;folder&gt;</c> and the game plays through every screen on its
    /// own, saving a screenshot of each step plus a log of any errors, then quits. Not reachable by players.
    /// Add <c>-casinoTourUnderage</c> to test the age block instead.
    /// </summary>
    public sealed class DevTour : MonoBehaviour
    {
        string dir;
        bool underage;
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
            Directory.CreateDirectory(tour.dir);
            Application.logMessageReceived += tour.OnLog;

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
                // Close-ups of every member of staff (3D, animated).
                for (int i = 0; i < world.TourStaffCount; i++)
                {
                    world.TourFaceStaff(i, 1.7f);
                    yield return Wait(1.0f);
                    yield return Shot("staff_" + world.TourStaffName(i));
                    world.TourFaceStaff(i, 2.6f, 16f);
                    yield return Wait(0.6f);
                    yield return Shot("staffbody_" + world.TourStaffName(i));
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

            App.I.Save.persuasionEnabled = true;
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
            App.I.UI.Show<LobbyScreen>();
            yield return Wait(2f);
            yield return Shot("lobby_final");
            Finish();
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
