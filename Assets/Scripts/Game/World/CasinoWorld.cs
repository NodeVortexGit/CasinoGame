using System;
using System.Collections;
using Casino.Art;
using Casino.Core;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.Screens;
using Casino.Platform;
using UnityEngine;

namespace Casino.World
{
    /// <summary>
    /// The walkable 3D casino behind the lobby HUD. Built once (spread over a few frames), then kept alive and simply
    /// hidden while a table or another full screen is up, so coming back is instant and you stand where you left.
    /// </summary>
    public sealed partial class CasinoWorld : MonoBehaviour
    {
        const float GreetDistance = 3.4f;

        public static CasinoWorld Instance { get; private set; }

        /// <summary>Dev tour only: the world camera draws into this off-screen target every frame.</summary>
        internal static RenderTexture TourTarget;

        public bool Ready { get; private set; }
        public Camera Camera { get; private set; }
        public PlayerRig Player { get; private set; }

        /// <summary>The place the player is standing at and looking towards, if any (drives the HUD action button).</summary>
        public Spot Focused { get; private set; }

        /// <summary>A member of staff says something (the HUD shows it as a speech bubble over their head).</summary>
        public event Action<StaffFigure, string> StaffSpeaks;

        /// <summary>Movement stick from the HUD: x = sideways, y = forward, -1..1.</summary>
        [NonSerialized] public Vector2 Stick;

        bool visible;
        float speakCooldown, waitressPause;

        // Seated at a game: the camera glides between the walking view and the seat.
        bool seated;
        int seatId;
        Vector3 seatEye, fromPos;
        Quaternion seatRot, fromRot;
        float seatFov, fromFov, glide = 1f;
        int patrolIndex = 1;
        readonly System.Collections.Generic.Dictionary<StaffFigure, float> lastGreeted = new System.Collections.Generic.Dictionary<StaffFigure, float>();

        public static CasinoWorld Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("CasinoWorld");
            go.transform.SetParent(App.I.transform, false);
            Instance = go.AddComponent<CasinoWorld>();
            return Instance;
        }

        void Awake()
        {
            var camGo = new GameObject("WorldCamera");
            camGo.transform.SetParent(transform, false);
            Camera = camGo.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Palette.Hex("1A0B22");
            Camera.nearClipPlane = 0.08f;
            Camera.farClipPlane = 45f;
            Camera.cullingMask = ~(1 << 5);
            Camera.allowHDR = false;
            ApplyGraphics();
            Camera.useOcclusionCulling = false;
            Camera.depth = -1;
            Camera.enabled = false;
            if (TourTarget != null) Camera.targetTexture = TourTarget;
        }

        IEnumerator Start()
        {
            yield return null;
            yield return Build();
            // World signs are built with UI helpers; keep them off the UI layer so only the world camera draws them.
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer == 5) t.gameObject.layer = 0;
            Player = new PlayerRig(obstacles, new Vector2(MinX, MinZ), new Vector2(MaxX, MaxZ));
            Player.Teleport(new Vector2(0f, -9.6f), 12f);
            Ready = true;
            UpdateCamera(0f);
            StartCoroutine(Restyle());   // photographs the room for reflections, now the signs are on the world's layer
        }

        /// <summary>Show or hide the 3D casino (hidden worlds cost nothing: the camera is simply off).</summary>
        public void SetVisible(bool show)
        {
            visible = show;
            Camera.enabled = show && Ready;
            if (App.I.UiCamera != null) App.I.UiCamera.enabled = !Camera.enabled;
            Input.multiTouchEnabled = show;
            if (!show)
            {
                Stick = Vector2.zero;
                Player?.Cancel();
            }
        }

        /// <summary>
        /// Turns the view with the player's speed and inverted axes, which are set separately for touch/mouse and for
        /// the controller's right stick.
        /// </summary>
        public void LookBy(float yawDegrees, float pitchDegrees, bool stick = false)
        {
            var save = App.I.Save;
            float k = Mathf.Clamp(stick ? save.padLookSensitivity : save.lookSensitivity, 0.25f, 4f);
            bool invertX = stick ? save.padInvertX : save.invertLookX, invertY = stick ? save.padInvertY : save.invertLookY;
            Player?.Look(yawDegrees * k * (invertX ? -1f : 1f), pitchDegrees * k * (invertY ? -1f : 1f));
        }

        /// <summary>True while a game screen has the player sitting at a table or machine.</summary>
        public bool Seated => seated;

        /// <summary>
        /// Sits down at a game: the camera glides to <paramref name="eye"/>, looking at <paramref name="lookAt"/>, and
        /// stays there (no walking) until <see cref="StandUp"/>. <paramref name="standAt"/> is where you'll be standing
        /// when you get up again (next to the game, wherever you came from). Returns the seat, for <see cref="StandUp"/>.
        /// </summary>
        public int SitDown(Vector3 eye, Vector3 lookAt, float verticalFov, Vector2 standAt, float standYaw)
        {
            if (!Ready) return 0;
            StartGlide();
            seated = true;
            seatEye = eye;
            seatRot = Quaternion.LookRotation(lookAt - eye);
            seatFov = verticalFov;
            Stick = Vector2.zero;
            Player.Teleport(standAt, standYaw);
            Focused = null;
            return ++seatId;
        }

        /// <summary>Gets up from the given seat (or from any, with 0); ignored if the player has moved to another game.</summary>
        public void StandUp(int seat = 0)
        {
            if (!seated || (seat != 0 && seat != seatId)) return;
            StartGlide();
            seated = false;
        }

        void StartGlide()
        {
            fromPos = Camera.transform.position;
            fromRot = Camera.transform.rotation;
            fromFov = Camera.fieldOfView;
            glide = 0f;
        }

        /// <summary>The seat camera's field of view, widened on squarer screens so the whole game stays in view.</summary>
        public float SeatFov(float verticalFov, float minHorizontalFov)
        {
            float aspect = Camera.aspect > 0.01f ? Camera.aspect : 16f / 9f;
            float needed = 2f * Mathf.Atan(Mathf.Tan(minHorizontalFov * 0.5f * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
            return Mathf.Max(verticalFov, needed);
        }

        void Update()
        {
            if (!Ready) return;
            if (visible && !Camera.enabled)
            {
                Camera.enabled = true;
                if (App.I.UiCamera != null) App.I.UiCamera.enabled = false;
            }
            if (!visible) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector2 stick = Stick;
#if UNITY_STANDALONE || UNITY_EDITOR
            if (!seated)
            {
                stick += new Vector2(Axis(KeyCode.D, KeyCode.A), Axis(KeyCode.W, KeyCode.S) + Axis(KeyCode.UpArrow, KeyCode.DownArrow));
                float turn = Axis(KeyCode.RightArrow, KeyCode.LeftArrow) + Axis(KeyCode.E, KeyCode.Q);
                if (turn != 0f) Player.Look(turn * 110f * dt, 0f);
            }
#endif
            bool blocked = App.I.UI.HasModal || seated;
            if (!blocked && Pad.Connected)
            {
                // Controller: left stick walks, right stick looks (squared, so small tilts aim finely).
                stick += Pad.Move;
                Vector2 look = Pad.Look;
                if (look.sqrMagnitude > 0.0001f)
                {
                    look *= look.magnitude;
                    LookBy(look.x * 150f * dt, -look.y * 95f * dt, true);
                }
            }
            Player.Tick(blocked ? Vector2.zero : Vector2.ClampMagnitude(stick, 1f), dt);
            UpdateCamera(dt);

            if (rouletteWheel != null) rouletteWheel.Rotate(0f, 0f, 25f * dt, Space.Self);
            Patrol(dt);
            foreach (var s in spots)
                if (s.FollowsStaff && s.Staff != null) s.Focus = s.Staff.transform.position + Vector3.up * 1.1f;

            LightWaitress(dt);
            Vector3 eye = Camera.transform.position;
            foreach (var fig in staff)
            {
                if (fig == null) continue;
                bool near = (fig.transform.position - eye).sqrMagnitude < StaffAnimDistance * StaffAnimDistance;
                fig.Animate(Camera, dt, near);
            }

            Focused = blocked ? null : PickFocused();
            AimFocus();
            if (!seated) Greet(dt);
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        static float Axis(KeyCode plus, KeyCode minus) => (Input.GetKey(plus) ? 1f : 0f) - (Input.GetKey(minus) ? 1f : 0f);
#endif

        void UpdateCamera(float dt)
        {
            float aspect = Camera.aspect > 0.01f ? Camera.aspect : 16f / 9f;
            // Keep the chosen horizontal view (about 90 degrees by default) on any screen shape.
            float half = Mathf.Clamp(App.I.Save.fieldOfView, 70f, 110f) * 0.5f;
            float vfov = 2f * Mathf.Atan(Mathf.Tan(half * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
            Camera.fieldOfView = Mathf.Clamp(vfov, 36f, 80f);
            float bob = Mathf.Sin(Player.StepPhase * Mathf.PI * 2f / 1.3f) * 0.022f * Mathf.Clamp01(Player.Speed / PlayerRig.WalkSpeed);
            Vector3 pos = seated ? seatEye : Player.Eye + Vector3.up * bob;
            Quaternion rot = seated ? seatRot : Player.Rotation;
            float fov = seated ? seatFov : Camera.fieldOfView;
            glide = Mathf.MoveTowards(glide, 1f, dt / 0.7f);
            if (glide < 1f)
            {
                float k = Mathf.SmoothStep(0f, 1f, glide);
                pos = Vector3.Lerp(fromPos, pos, k);
                rot = Quaternion.Slerp(fromRot, rot, k);
                fov = Mathf.Lerp(fromFov, fov, k);
            }
            rot *= Handheld(dt);
            Camera.fieldOfView = fov;
            Camera.transform.SetPositionAndRotation(pos, rot);
        }

        Spot PickFocused()
        {
            Spot best = null;
            float bestScore = float.MaxValue;
            float yawRad = Player.Yaw * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(yawRad), Mathf.Cos(yawRad));
            foreach (var s in spots)
            {
                var to = new Vector2(s.Focus.x, s.Focus.z) - Player.Position;
                float d = to.magnitude;
                if (d > s.UseDistance) continue;
                float angle = d > 0.01f ? Vector2.Angle(fwd, to / d) : 0f;
                if (angle > 65f && d > 1.4f) continue;
                float score = d + angle / 40f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ interaction

        /// <summary>The player tapped the 3D view: walk to (and use) what was tapped, or walk to that spot on the floor.</summary>
        public void Tap(Vector2 screenPoint)
        {
            if (!Ready) return;
            var ray = Camera.ScreenPointToRay(screenPoint);
            Spot hit = null;
            float hitDistance = float.MaxValue;
            foreach (var s in spots)
            {
                Vector3 center = s.Focus;
                float radius = s.TapRadius;
                if (s.Staff != null && s.FollowsStaff) radius = 0.6f;
                if (RaySphere(ray, center, radius, out float t) && t < hitDistance)
                {
                    hit = s;
                    hitDistance = t;
                }
                if (s.Staff != null && RaySphere(ray, s.Staff.HeadPoint - Vector3.up * 0.55f, 0.5f, out float ts) && ts < hitDistance)
                {
                    hit = s;
                    hitDistance = ts;
                }
            }
            if (hit != null)
            {
                Go(hit);
                return;
            }
            if (ray.direction.y < -0.02f)
            {
                float t = -ray.origin.y / ray.direction.y;
                if (t < 30f)
                {
                    Vector3 p = ray.origin + ray.direction * t;
                    Player.WalkTo(new Vector2(p.x, p.z), null, null);
                }
            }
        }

        /// <summary>Walks to a spot (unless already there) and uses it.</summary>
        public void Go(Spot spot)
        {
            var focus = new Vector2(spot.Focus.x, spot.Focus.z);
            if ((focus - Player.Position).magnitude <= spot.UseDistance)
            {
                Use(spot);
                return;
            }
            Vector2 approach = spot.Approach;
            float yaw = spot.ApproachYaw;
            if (spot.FollowsStaff)
            {
                Vector2 dir = (Player.Position - focus).normalized;
                approach = focus + dir * 1.4f;
                yaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;
            }
            Player.WalkTo(approach, yaw, () => Use(spot));
        }

        public void Use(Spot spot)
        {
            Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Click);
            if (spot.Staff != null) spot.Staff.Talk(1.2f);
            spot.Use?.Invoke();
        }

        static bool RaySphere(Ray ray, Vector3 center, float radius, out float t)
        {
            Vector3 oc = ray.origin - center;
            float b = Vector3.Dot(oc, ray.direction);
            float c = oc.sqrMagnitude - radius * radius;
            float disc = b * b - c;
            t = 0f;
            if (disc < 0f) return false;
            t = -b - Mathf.Sqrt(disc);
            if (t < 0f) t = -b + Mathf.Sqrt(disc);
            return t >= 0f;
        }

        void SitAt(TableDef def)
        {
            if (!App.I.Progress.Unlocked(def))
            {
                App.I.UI.Toast(Loc.F("The {0} opens at level {1}. You're level {2}: every round you play counts.", Loc.T(def.Name), def.Level,
                        App.I.Progress.Level),
                    Palette.Gold, Icon.Lock);
                return;
            }
            if (App.I.Energy.IsEmpty)
            {
                ExhaustedPrompt.Show();
                return;
            }
            if (App.I.Wallet.Balance < def.MinBet)
            {
                App.I.UI.Toast(Loc.F("This table needs at least {0} chips.", ChipFormat.Short(def.MinBet)), Palette.Gold, Icon.Coin);
                App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips);
                return;
            }
            App.I.Save.lastTableId = def.Id;
            App.I.UI.Show<BlackjackScreen>(s => s.Table = def);
        }

        StaffFigure StaffAt(int index) => index >= 0 && index < staff.Count ? staff[index] : null;

        /// <summary>The blackjack table for a table definition: its centre (dealer on the +z side) and its dealer.</summary>
        public bool TryBlackjackTable(string id, out Vector3 center, out StaffFigure dealer)
        {
            dealer = null;
            center = default;
            if (!Ready || id == null || !blackjackTables.TryGetValue(id, out var t)) return false;
            center = t.center;
            dealer = StaffAt(t.dealer);
            return true;
        }

        /// <summary>A slot machine's screen (centre of the reel window) and the way it faces.</summary>
        public bool TrySlotMachine(int index, out Vector3 screen, out Vector3 facing)
        {
            screen = facing = default;
            if (!Ready || slotMachines.Count == 0) return false;
            (screen, facing) = slotMachines[Mathf.Abs(index) % slotMachines.Count];
            return true;
        }

        /// <summary>The hold'em table's centre (dealer on the +z side) and its dealer.</summary>
        public bool TryHoldemTable(out Vector3 center, out StaffFigure dealer)
        {
            center = HoldemCenter;
            dealer = StaffAt(holdemDealer);
            return Ready;
        }

        /// <summary>A video poker machine's screen and the way it faces.</summary>
        public bool TryVideoPokerMachine(int index, out Vector3 screen, out Vector3 facing)
        {
            screen = facing = default;
            if (!Ready || videoPokerMachines.Count == 0) return false;
            (screen, facing) = videoPokerMachines[Mathf.Abs(index) % videoPokerMachines.Count];
            return true;
        }

        /// <summary>The craps table (layout origin on the floor) and its stickman.</summary>
        public bool TryCraps(out Vector3 center, out StaffFigure dealer)
        {
            center = CrapsCenter;
            dealer = StaffAt(crapsDealer);
            return Ready;
        }

        public void ShowCrapsDecor(bool show)
        {
            if (crapsDecor != null) crapsDecor.gameObject.SetActive(show);
        }

        /// <summary>The sic bo table (layout origin on the floor) and its dealer.</summary>
        public bool TrySicBo(out Vector3 center, out StaffFigure dealer)
        {
            center = SicBoCenter;
            dealer = StaffAt(sicBoDealer);
            return Ready;
        }

        public void ShowSicBoDecor(bool show)
        {
            if (sicBoDecor != null) sicBoDecor.gameObject.SetActive(show);
        }

        /// <summary>The baccarat table's centre (dealer on the +z side) and its dealer.</summary>
        public bool TryBaccaratTable(out Vector3 center, out StaffFigure dealer)
        {
            center = BaccaratCenter;
            dealer = StaffAt(baccaratDealer);
            return Ready;
        }

        /// <summary>The roulette table (layout origin on the floor) and its croupier.</summary>
        public bool TryRoulette(out Vector3 center, out StaffFigure croupier)
        {
            center = RouletteCenter;
            croupier = StaffAt(croupierIndex);
            return Ready;
        }

        /// <summary>The wheel and board shown on the roulette table while nobody is playing there.</summary>
        public void ShowRouletteDecor(bool show)
        {
            if (rouletteDecor != null) rouletteDecor.gameObject.SetActive(show);
        }

        public StaffFigure HostFigure => hostIndex >= 0 && hostIndex < staff.Count ? staff[hostIndex] : null;

        /// <summary>The host says something (welcome lines); shown wherever you're standing.</summary>
        public void HostSays(string line) => Say(HostFigure, line);

        bool CanPlay()
        {
            if (!App.I.Energy.IsEmpty) return true;
            ExhaustedPrompt.Show();
            return false;
        }

        void OpenSlots(int machine)
        {
            if (CanPlay()) App.I.UI.Show<SlotsScreen>(s => s.Machine = machine);
        }

        void OpenRoulette()
        {
            if (CanPlay()) App.I.UI.Show<RouletteScreen>();
        }

        void OpenVideoPoker(int machine)
        {
            if (CanPlay()) App.I.UI.Show<VideoPokerScreen>(s => s.Machine = machine);
        }

        void OpenCraps()
        {
            if (CanPlay()) App.I.UI.Show<CrapsScreen>();
        }

        void OpenSicBo()
        {
            if (CanPlay()) App.I.UI.Show<SicBoScreen>();
        }

        void OpenBaccarat()
        {
            if (CanPlay()) App.I.UI.Show<BaccaratScreen>();
        }

        void OpenHoldem()
        {
            var def = TableCatalog.Holdem;
            if (!CanPlay()) return;
            if (App.I.Wallet.Balance < def.MinBet * 6)
            {
                App.I.UI.Toast(Loc.F("This table needs at least {0} chips.", ChipFormat.Short(def.MinBet * 6)), Palette.Gold, Icon.Coin);
                App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips);
                return;
            }
            App.I.UI.Show<HoldemScreen>();
        }

        void HostTips()
        {
            var host = hostIndex >= 0 && hostIndex < staff.Count ? staff[hostIndex] : null;
            string[] tips =
            {
                Loc.N("Blackjack tables are straight ahead. Walk up to one and take a seat!"),
                Loc.N("Slot machines line the right wall, video poker the back wall, and the roulette wheel is at the back."),
                Loc.N("Hold'em is in the back left corner, past the bar."),
                Loc.N("Baccarat is just to the left as you come in."),
                Loc.N("Sic bo is in the corner past the cashier: three dice, big or small."),
                Loc.N("The craps table is on the right, in front of the slots. Bet the Pass Line and roll!"),
                Loc.N("The cashier is right here on your right, and the bar is along the left wall."),
                Loc.N("Feeling tired? A snack at the bar fills your energy right up."),
                Loc.N("Tap anywhere on the floor and you'll walk there. Tap a table to sit down."),
            };
            Say(host, Loc.T(tips[App.I.Rng.Next(tips.Length)]));
        }

        // ------------------------------------------------------------------ staff life

        void Say(StaffFigure fig, string line)
        {
            if (fig == null) return;
            fig.Talk(Mathf.Clamp(line.Length * 0.05f, 1.2f, 3.5f));
            speakCooldown = 6f;
            lastGreeted[fig] = Time.time;
            StaffSpeaks?.Invoke(fig, line);
        }

        void Greet(float dt)
        {
            speakCooldown -= dt;
            if (speakCooldown > 0f) return;
            Vector3 eye = Camera.transform.position;
            Vector3 fwd = Camera.transform.forward;
            foreach (var fig in staff)
            {
                if (fig == null) continue;
                Vector3 to = fig.HeadPoint - eye;
                to.y = 0f;
                float d = to.magnitude;
                if (d > GreetDistance || d < 0.3f) continue;
                if (Vector3.Angle(new Vector3(fwd.x, 0f, fwd.z), to) > 40f) continue;
                if (Hidden(eye, fig.HeadPoint)) continue;   // behind a machine: the bubble would point at the machine
                if (lastGreeted.TryGetValue(fig, out float when) && Time.time - when < 40f) continue;
                int pick = App.I.Rng.Next(App.I.Save.handsPlayed > 0 ? 3 : 2);
                string line = pick == 0 ? DealerLines.Charm(fig.Role, App.I.Rng) : pick == 1 ? DealerLines.Greeting(fig.Role, App.I.Rng) : StreakTease();
                Say(fig, line);
                if (fig.Role == StaffRole.Host) fig.Wave();
                else if (App.I.Rng.Next(3) == 0) fig.Wink();
                return;
            }
        }

        /// <summary>
        /// Whether a sight blocker stands between the camera and a point, seen from above. A blocker that holds the
        /// point itself doesn't count (the cashier inside her booth).
        /// </summary>
        bool Hidden(Vector3 eye, Vector3 at)
        {
            Vector2 a = new Vector2(eye.x, eye.z), b = new Vector2(at.x, at.z);
            foreach (var o in sightBlockers)
            {
                if (o.Circle)
                {
                    if ((b - o.Center).sqrMagnitude < o.Radius * o.Radius) continue;
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(o.Center - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    if ((a + ab * t - o.Center).sqrMagnitude < o.Radius * o.Radius) return true;
                }
                else
                {
                    if (b.x >= o.Min.x && b.x <= o.Max.x && b.y >= o.Min.y && b.y <= o.Max.y) continue;
                    if (SegmentHitsRect(a, b, o.Min, o.Max)) return true;
                }
            }
            return false;
        }

        /// <summary>Liang–Barsky: does the segment a-b cross the axis-aligned rectangle?</summary>
        static bool SegmentHitsRect(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                float p = d[axis], lo = min[axis] - a[axis], hi = max[axis] - a[axis];
                if (Mathf.Abs(p) < 1e-6f)
                {
                    if (lo > 0f || hi < 0f) return false;
                    continue;
                }
                float u = lo / p, v = hi / p;
                if (u > v) (u, v) = (v, u);
                t0 = Mathf.Max(t0, u);
                t1 = Mathf.Min(t1, v);
                if (t0 > t1) return false;
            }
            return true;
        }

        static string StreakTease()
        {
            var p = App.I.Progress;
            int left = p.Streak.DailyGoal - p.Streak.RoundsToday(p.Day);
            return DealerLines.StreakTease(p.Streak.Count, p.Streak.DoneToday(p.Day), Mathf.Max(1, left), App.I.Rng);
        }

        void Patrol(float dt)
        {
            if (waitress == null || patrol.Count == 0) return;
            Vector3 p = waitress.transform.position;
            var pos = new Vector2(p.x, p.z);
            // She stops for a chat when you're close.
            if ((pos - Player.Position).magnitude < 2.6f) return;
            if (waitressPause > 0f)
            {
                waitressPause -= dt;
                return;
            }
            Vector2 target = patrol[patrolIndex];
            Vector2 to = target - pos;
            float step = 1.05f * dt;
            if (to.magnitude <= step)
            {
                pos = target;
                patrolIndex = (patrolIndex + 1) % patrol.Count;
                waitressPause = 1.5f;
            }
            else
            {
                pos += to.normalized * step;
            }
            waitress.transform.position = new Vector3(pos.x, 0f, pos.y);
        }

        // ------------------------------------------------------------------ dev tour

        internal string TourDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var mr in GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                var mat = mr.sharedMaterial;
                sb.Append($"\n  {mr.name}: visible={mr.isVisible} verts={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : -1)} " +
                    $"shader={(mat != null && mat.shader != null ? mat.shader.name + (mat.shader.isSupported ? "" : " UNSUPPORTED") : "none")} queue={(mat != null ? mat.renderQueue : -1)} " +
                    $"bounds={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.ToString() : "")}");
            }
            sb.Append($"\n  glows={glows.Count}");
            return sb.ToString();
        }

        internal int TourStaffCount => staff.Count;
        internal bool TourRestyling => restyling;
        internal string TourStaffName(int i) => staff[i] == null ? "missing" : staff[i].EnglishName + "_" + staff[i].Role;

        internal void TourStaffTalk(int i, float seconds) => staff[i]?.Talk(seconds);

        /// <summary>Stands the camera in front of a member of staff, looking at them.</summary>
        internal void TourFaceStaff(int i, float distance, float pitch = 6f)
        {
            var fig = staff[i];
            if (fig == null) return;
            Vector3 f = fig.transform.forward;
            Vector3 at = fig.transform.position + f * distance;
            Player.Teleport(new Vector2(at.x, at.z), Mathf.Atan2(-f.x, -f.z) * Mathf.Rad2Deg);
            // look slightly down at the face from eye height
            Player.Pitch = pitch;
            glide = 1f;
            UpdateCamera(0f);
            if (post != null) post.SnapFocus();
        }

        internal void TourRender(RenderTexture target)
        {
            Camera.targetTexture = target;
            UpdateCamera(0f);
            Camera.Render();
        }

        internal void TourView(Vector2 position, float yaw, float pitch = 0f)
        {
            Player.Teleport(position, yaw);
            Player.Pitch = pitch;
            glide = 1f;
            UpdateCamera(0f);
            if (post != null) post.SnapFocus();
        }
    }
}
