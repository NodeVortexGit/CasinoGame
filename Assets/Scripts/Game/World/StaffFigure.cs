using Casino.Art;
using Casino.Core.Physics;
using Casino.Core.Player;
using UnityEngine;

namespace Casino.World
{
    public enum StaffPose { Counter, Polish, Tray, Host }

    /// <summary>
    /// A member of staff as a realistic, rigged 3D person (MakeHuman model from Tools/make_staff.py) animated with
    /// motion-captured CC0 clips through a humanoid Animator: idles, talking, walking, dealing gestures. On top of that:
    /// eye contact and head turning (look-at IK), relaxed hands, the waitress's tray on an upturned palm and the host's
    /// wave (a two-bone arm solver with a proper elbow), the jaw moving while talking, and hair that sways with movement
    /// (spring physics feeding the hair shader).
    /// </summary>
    public sealed class StaffFigure : MonoBehaviour
    {
        // No "talking" clip: the library's one gestures at chest height, which looks wrong on these figures. Talking is
        // the idle plus head emphasis and an open hand at waist height, added in the IK pass.
        const string Idle = "Idle_Loop", Walk = "Walk_Formal_Loop",
            Interact = "Interact", Beckon = "Idle_Rail_Call", Nod = "Yes";

        /// <summary>
        /// The staff's render layer: the world camera draws it, the snapshot of the room for reflections doesn't (people
        /// move, so a frozen copy of them would haunt the floor).
        /// </summary>
        public const int Layer = 8;

        public StaffRole Role { get; private set; }
        public bool Female { get; private set; }
        /// <summary>The name, in the player's language.</summary>
        public string StaffName => Loc.T(EnglishName);
        public string EnglishName { get; private set; }
        public StaffPose Pose { get; private set; }

        /// <summary>World position just above the head, for speech bubbles.</summary>
        public Vector3 HeadPoint => (head != null ? head.position : transform.position + Vector3.up * 1.6f) + Vector3.up * 0.28f;

        /// <summary>World position of the face (about eye height), for the camera's focus.</summary>
        public Vector3 Face => head != null ? head.position + Vector3.up * 0.07f : transform.position + Vector3.up * 1.55f;

        static RuntimeAnimatorController controller;

        Animator animator;
        Transform head, jaw;
        Quaternion jawRest;
        Arm leftArm, rightArm;
        Joint[] fingers = new Joint[0];
        Material[] hairMaterials = new Material[0], materials = new Material[0];
        Transform tray;
        Renderer contactShadow;
        readonly SpringChain hair = new SpringChain(3) { Stiffness = 38f, Damping = 3f, MaxOffset = 0.07f };
        float baseYaw, bodyYaw, phase, talkUntil, waveUntil, nodUntil, nextGesture, gestureUntil;
        const float TalkGestureTime = 1.5f;
        float talkGestureStart = -10f, nextTalkGesture;
        bool talkGestureLeft;
        string state, gesture;
        Vector3 lastPos, lastHeadPos, lastHeadVel;
        Camera lookCam;
        float lookWeight;
        bool started, near;

        // Set once the animator has really posed the body (its IK pass ran). Until then it must keep running even far
        // away, or the figure would freeze in the T-shaped bind pose.
        bool posed;
        Quaternion trayUpper, trayLower, trayHand;
        bool trayPosed;
        Vector3 slideFrom, slideTo;
        float slideStart = -10f, slideTime = 1f;

        /// <summary>Joint angles of a relaxed hand (proximal, middle, tip), from index to little finger it adds a bit more.</summary>
        static readonly float[] Curl = { 14f, 22f, 12f };

        struct Joint
        {
            public HumanBodyBones Bone;
            public Quaternion Relaxed;
        }

        /// <summary>The bones of one arm, plus the knuckles that tell which way the hand and its palm face.</summary>
        sealed class Arm
        {
            public Transform Upper, Lower, Hand, Index, Middle, Little;
            public bool Right;

            public static Arm Find(Animator a, bool right)
            {
                var arm = new Arm
                {
                    Right = right,
                    Upper = a.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm),
                    Lower = a.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm),
                    Hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand),
                    Index = a.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal),
                    Middle = a.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal),
                    Little = a.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal),
                };
                return arm.Upper && arm.Lower && arm.Hand && arm.Index && arm.Middle && arm.Little ? arm : null;
            }

            /// <summary>Where the fingers point.</summary>
            public Vector3 Along => (Middle.position - Hand.position).normalized;

            /// <summary>The way the palm faces (out of the palm, the side the fingers curl towards).</summary>
            public Vector3 Palm
            {
                get
                {
                    Vector3 along = Along, across = Index.position - Little.position;
                    return Vector3.ProjectOnPlane(Right ? Vector3.Cross(along, across) : Vector3.Cross(across, along), along).normalized;
                }
            }

            /// <summary>
            /// Puts the wrist at <paramref name="target"/> with the elbow bent towards <paramref name="hint"/>, then turns
            /// the hand so the fingers point along <paramref name="along"/> and the palm faces <paramref name="palm"/>; the
            /// forearm takes half of that roll, like a real one. <paramref name="weight"/> blends from the animation.
            /// </summary>
            public void Reach(Vector3 target, Vector3 hint, Vector3 along, Vector3 palm, float weight)
            {
                if (weight <= 0.001f) return;
                Vector3 s = Upper.position, e = Lower.position, w = Hand.position;
                float a = Vector3.Distance(s, e), b = Vector3.Distance(e, w);
                Vector3 toTarget = Vector3.Lerp(w, target, weight) - s;
                if (toTarget.sqrMagnitude < 1e-6f) return;
                Vector3 dir = toTarget.normalized;
                float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.001f);
                Vector3 bend = Vector3.ProjectOnPlane(Vector3.Lerp(e - s, hint - s, weight), dir);
                if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
                float x = (a * a - b * b + d * d) / (2f * d);
                Vector3 elbow = s + dir * x + bend.normalized * Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
                Upper.rotation = Quaternion.FromToRotation(e - s, elbow - s) * Upper.rotation;
                e = Lower.position;
                Lower.rotation = Quaternion.FromToRotation(Hand.position - e, s + dir * d - e) * Lower.rotation;

                palm = Vector3.ProjectOnPlane(palm, along);
                if (palm.sqrMagnitude < 1e-6f || along.sqrMagnitude < 1e-6f) return;
                Vector3 axis = (Hand.position - Lower.position).normalized;
                float roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(Palm, axis), Vector3.ProjectOnPlane(palm, axis), axis);
                Lower.rotation = Quaternion.AngleAxis(roll * 0.5f * weight, axis) * Lower.rotation;
                Quaternion turn = Quaternion.LookRotation(along, palm) * Quaternion.Inverse(Quaternion.LookRotation(Along, Palm));
                Hand.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * Hand.rotation;
            }
        }

        /// <param name="model">Folder/FBX name under Resources/Staff, e.g. "f_dealer1".</param>
        public static StaffFigure Create(Transform parent, string model, Vector3 position, float yaw, bool female, StaffRole role,
            string staffName, Color tint, StaffPose pose, AvatarLook look)
        {
            var prefab = Resources.Load<GameObject>("Staff/" + model + "/" + model);
            if (prefab == null)
            {
                Debug.LogError("Missing staff model " + model);
                return null;
            }
            var go = new GameObject("Staff " + staffName);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            // The exported people face -Z once imported (Unity undoes the exporter's axis choice), so turn them round.
            var body = Instantiate(prefab, go.transform);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var fig = go.AddComponent<StaffFigure>();
            fig.Role = role;
            fig.Female = female;
            fig.EnglishName = staffName;
            fig.Pose = pose;
            fig.phase = Mathf.Abs(staffName.GetHashCode() % 1000) * 0.01f;
            fig.nextGesture = Time.time + 3f + fig.phase;
            fig.baseYaw = fig.bodyYaw = yaw;
            fig.Setup(look, tint);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            return fig;
        }

        void Setup(AvatarLook look, Color tint)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) animator = transform.GetChild(0).gameObject.AddComponent<Animator>();
            controller ??= Resources.Load<RuntimeAnimatorController>("Staff/StaffAnimator");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
                leftArm = Arm.Find(animator, false);
                rightArm = Arm.Find(animator, true);
                RelaxFingers();
            }
            if (jaw != null) jawRest = jaw.localRotation;
            animator.gameObject.AddComponent<StaffIK>().Figure = this;
            Dress(look, tint);
            if (Pose == StaffPose.Tray) BuildTray();
            Play(Idle, 0f);
        }

        /// <summary>
        /// Works out a relaxed hand from the rest pose (still in place: the animator hasn't run yet). Retargeted finger
        /// animation curls these hands into fists, so the fingers are posed here instead; the tray hand stays flatter.
        /// </summary>
        void RelaxFingers()
        {
            var list = new System.Collections.Generic.List<Joint>();
            foreach (var arm in new[] { leftArm, rightArm })
            {
                if (arm == null) continue;
                Vector3 palm = arm.Palm;
                bool flat = arm.Right && Pose == StaffPose.Tray;
                int first = (int)(arm.Right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        var bone = animator.GetBoneTransform((HumanBodyBones)(first + f * 3 + j));
                        if (bone == null) continue;
                        var next = j < 2 ? animator.GetBoneTransform((HumanBodyBones)(first + f * 3 + j + 1)) : null;
                        Vector3 dir = next != null ? next.position - bone.position : bone.position - bone.parent.position;
                        Vector3 axis = Vector3.Cross(dir, palm);
                        if (axis.sqrMagnitude < 1e-10f) continue;
                        float angle = f == 0 ? (j == 0 ? 0f : 12f) : Curl[j] + f * 3f;
                        if (flat) angle *= -0.25f;   // fingers spread flat, tips a touch down, under the tray
                        var local = Quaternion.Inverse(bone.rotation) * axis.normalized;
                        list.Add(new Joint { Bone = (HumanBodyBones)(first + f * 3 + j), Relaxed = bone.localRotation * Quaternion.AngleAxis(angle, local) });
                    }
            }
            fingers = list.ToArray();
        }

        // ------------------------------------------------------------------ looks

        void Dress(AvatarLook look, Color tint)
        {
            var shader = Resources.Load<Shader>("Shaders/CasinoPerson");
            var hairList = new System.Collections.Generic.List<Material>();
            var all = new System.Collections.Generic.List<Material>();
            Color rim = Female ? new Color(1f, 0.42f, 0.78f) : new Color(0.45f, 0.75f, 1f);
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.updateWhenOffscreen = false;
                var src = r.sharedMaterials;
                var mats = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = new Material(shader);
                    string n = src[i] != null ? src[i].name.ToLowerInvariant() : "";
                    var tex = src[i] != null && src[i].HasProperty("_MainTex") ? src[i].mainTexture : null;
                    m.mainTexture = tex != null ? tex : Texture2D.whiteTexture;
                    m.SetColor("_Tint", tint);
                    m.SetColor("_Rim", rim);
                    bool hairLike = n.Contains("long") || n.Contains("bob") || n.Contains("ponytail") || n.Contains("short") || n.Contains("braid") || n.Contains("afro");
                    bool fringe = n.Contains("eyebrow") || n.Contains("eyelash");
                    if (hairLike || fringe)
                    {
                        // Brows and lashes are fine hairs: a lower cut-off keeps them solid instead of dashed.
                        m.SetFloat("_Cutoff", fringe ? 0.3f : 0.45f);
                        m.SetFloat("_Cull", 0f);
                        m.renderQueue = 2450;
                        if (hairLike)
                        {
                            m.SetFloat("_SwayAmount", 1f);
                            m.SetFloat("_Gloss", 0.35f);
                            hairList.Add(m);
                        }
                    }
                    else if (n.Contains("dress"))
                    {
                        m.mainTexture = DressTexture(Role, look);
                        m.SetFloat("_Gloss", 0.5f);
                    }
                    else if (n.Contains("suit"))
                    {
                        m.SetColor("_Color", SuitColor(Role));
                        m.SetFloat("_Gloss", 0.2f);
                    }
                    else if (n.Contains("shoes"))
                    {
                        m.SetColor("_Color", Female ? ShoeColor(Role) : new Color(0.25f, 0.22f, 0.24f));
                        m.SetFloat("_Gloss", 0.6f);
                    }
                    else if (n.Contains("eye") || n.Contains("high-poly") || n.Contains("low-poly"))
                    {
                        // The detailed eyes have a cornea shell that is transparent in the texture: cut it out, or it
                        // covers the eye in black.
                        m.SetFloat("_Gloss", 0.9f);
                        m.SetFloat("_Cutoff", 0.5f);
                    }
                    else if (n.Contains("teeth"))
                    {
                        m.SetFloat("_Gloss", 0.4f);
                    }
                    else
                    {
                        m.SetFloat("_Gloss", 0.12f); // skin
                        m.SetFloat("_Skin", 1f);
                    }
                    m.name = src[i] != null ? src[i].name : "mat";
                    mats[i] = m;
                    all.Add(m);
                }
                r.sharedMaterials = mats;
            }
            hairMaterials = hairList.ToArray();
            materials = all.ToArray();
        }

        /// <summary>
        /// The room's lamp nearest in effect (direction towards it, colour times strength), which the high graphics
        /// settings light them with; walkers get it updated as they go.
        /// </summary>
        public void SetRoomLight(Vector3 direction, Color color)
        {
            // Softened and capped, so a bright lamp shapes the face without bleaching it.
            float peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (peak > 0.55f) color *= 0.55f / peak;
            var dir = new Vector4(direction.x, direction.y, direction.z, 0f);
            foreach (var m in materials)
            {
                m.SetVector("_RoomLightDir", dir);
                m.SetColor("_RoomLightColor", color);
            }
        }

        /// <summary>A soft shadow on the floor under them, for anyone walking around (standing staff have theirs baked).</summary>
        public void ShowContactShadow(bool on, Texture falloff)
        {
            if (contactShadow == null)
            {
                if (!on) return;
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(go.GetComponent<Collider>());
                go.name = "Contact shadow";
                go.layer = Layer;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.012f;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(0.95f, 0.8f, 1f);
                contactShadow = go.GetComponent<Renderer>();
                contactShadow.sharedMaterial = new Material(Resources.Load<Shader>("Shaders/CasinoShadow")) { mainTexture = falloff };
                contactShadow.sharedMaterial.SetFloat("_Strength", 0.55f);
                contactShadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                contactShadow.receiveShadows = false;
            }
            contactShadow.enabled = on;
        }

        static Color SuitColor(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Cashier: return new Color(0.35f, 0.45f, 0.85f);
                case StaffRole.Server: return new Color(0.8f, 0.25f, 0.35f);
                default: return new Color(0.3f, 0.28f, 0.32f); // black tie
            }
        }

        static Color ShoeColor(StaffRole role) =>
            role == StaffRole.Server ? new Color(1f, 0.3f, 0.35f) : role == StaffRole.Host ? new Color(1f, 0.85f, 0.45f) : new Color(0.3f, 0.28f, 0.3f);

        /// <summary>Satin dress print: role colour, gold (or black) trims at the neckline and hem, sequins, a corset for servers.</summary>
        static Texture2D DressTexture(StaffRole role, AvatarLook look)
        {
            Color baseCol = role == StaffRole.Dealer ? Palette.Hex("17141C") : role == StaffRole.Cashier ? Palette.Hex("1B2A5E")
                : role == StaffRole.Server ? Palette.Hex("C0122F") : look.Outfit;
            Color trim = role == StaffRole.Server ? Palette.Hex("15131A") : Palette.Gold;
            bool sequins = role == StaffRole.Host || role == StaffRole.Dealer;
            const int w = 64, h = 256;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.Lerp(Palette.Shade(baseCol, 0.85f), Palette.Shade(baseCol, 1.15f), Mathf.Sin(x / (float)w * Mathf.PI * 6f) * 0.5f + 0.5f);
                    if (v < 0.035f || v > 0.962f) c = trim;
                    else if (role == StaffRole.Server && v > 0.44f && v < 0.62f)
                        c = (v < 0.452f || v > 0.608f) ? Palette.Gold : Palette.Hex("15131A");
                    else if (sequins)
                    {
                        uint hsh = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                        hsh ^= hsh >> 13; hsh *= 0x5bd1e995; hsh ^= hsh >> 15;
                        if ((hsh & 255) > 236) c = Color.Lerp(c, role == StaffRole.Dealer ? Palette.Gold : Color.white, 0.55f);
                    }
                    px[y * w + x] = c;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Dress", wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        void BuildTray()
        {
            var kit = new MeshKit { Emission = 1f };
            Color glass = new Color(0.85f, 0.95f, 1f), silver = new Color(0.82f, 0.84f, 0.9f);
            kit.Finish(silver, 0.75f, true);
            kit.Finish(glass, 0.85f);
            kit.Cylinder(Vector3.zero, 0.17f, 0.012f, 18, silver);
            var m = new Vector3(-0.06f, 0.012f, 0.02f);
            kit.Cylinder(m, 0.03f, 0.004f, 10, glass);
            kit.Cylinder(m, 0.004f, 0.07f, 6, glass, false);
            kit.Cylinder(m + Vector3.up * 0.07f, 0.004f, 0.06f, 12, new Color(1f, 0.85f, 0.55f), true, 0.045f);
            var f = new Vector3(0.06f, 0.012f, -0.02f);
            kit.Cylinder(f, 0.025f, 0.004f, 10, glass);
            kit.Cylinder(f, 0.004f, 0.05f, 6, glass, false);
            kit.Cylinder(f + Vector3.up * 0.05f, 0.018f, 0.1f, 10, new Color(0.98f, 0.9f, 0.6f));
            var go = new GameObject("Tray");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = kit.Build(new LightRig(), "Tray");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Resources.Load<Shader>("Shaders/CasinoWorld")) { mainTexture = Texture2D.whiteTexture };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tray = go.transform;
        }

        // ------------------------------------------------------------------ behaviour

        public void Talk(float seconds) => talkUntil = Time.time + seconds;
        public void Wink() => nodUntil = Time.time + 1.2f;   // a little nod (the models have no eyelid bones)
        public void Wave() => waveUntil = Time.time + 2.4f;

        /// <summary>
        /// Slides something across the table with the nearer hand: a card from the shoe to its place, or the ball into
        /// the wheel. The hand follows as far as the arm reaches, then comes back.
        /// </summary>
        public void SlideOnTable(Vector3 from, Vector3 to, float seconds = 0.45f)
        {
            slideFrom = from;
            slideTo = to;
            slideStart = Time.time;
            slideTime = seconds;
            nextGesture = Time.time + 6f;
        }

        void Play(string clip, float fade)
        {
            if (state == clip || animator == null || animator.runtimeAnimatorController == null) return;
            state = clip;
            if (fade <= 0f) animator.Play(clip, 0, Random.value);
            else animator.CrossFadeInFixedTime(clip, fade);
        }

        /// <summary>Called every frame by the world. <paramref name="near"/> = close enough for full detail.</summary>
        public void Animate(Camera cam, float dt, bool near)
        {
            if (dt <= 0f) return;
            lookCam = cam;
            Vector3 pos = transform.position;
            if (!started)
            {
                started = true;
                lastPos = pos;
                lastHeadPos = head != null ? head.position : pos;
            }
            Vector3 vel = (pos - lastPos) / dt;
            lastPos = pos;
            float speed = new Vector2(vel.x, vel.z).magnitude;

            // Heading: walkers face where they go; everyone else turns a little towards you when you're near.
            Vector3 toCam = cam.transform.position - pos;
            toCam.y = 0f;
            float camYaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
            float camDist = toCam.magnitude;
            float targetYaw = baseYaw;
            if (speed > 0.15f) targetYaw = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
            else if (Pose == StaffPose.Tray) targetYaw = camDist < 3.5f ? camYaw : bodyYaw;
            else if (camDist < 5f)
            {
                float rel = Mathf.DeltaAngle(baseYaw, camYaw);
                if (Mathf.Abs(rel) < 100f) targetYaw = baseYaw + Mathf.Clamp(rel, -25f, 25f);
            }
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, targetYaw, (speed > 0.15f ? 220f : 90f) * dt);
            if (speed > 0.15f) baseYaw = bodyYaw;
            transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);

            // Which animation: walking, a gesture, talking, or the idle.
            float now = Time.time;
            if (speed > 0.15f) Play(Walk, 0.25f);
            else if (now < gestureUntil) Play(gesture, 0.2f);
            else if (now < talkUntil)
            {
                Play(Idle, 0.3f);
                if (near && now > nextTalkGesture)
                {
                    // Every few seconds one hand opens out, like someone explaining; tray carriers use the free hand.
                    talkGestureStart = now;
                    talkGestureLeft = Pose == StaffPose.Tray || Mathf.Repeat(phase * 7f + now, 2f) < 1f;
                    nextTalkGesture = now + TalkGestureTime + 1.2f + Mathf.Repeat(phase * 5f + now * 0.37f, 1.8f);
                }
            }
            else if (now < nodUntil) Play(Nod, 0.25f);
            else
            {
                if (now > nextGesture && near)
                {
                    // Dealers' own moves come from the game (dealing with their hands); others get an occasional gesture.
                    gesture = Pose == StaffPose.Polish ? Interact : Pose == StaffPose.Host ? Beckon : null;
                    nextGesture = now + 7f + Mathf.Repeat(phase * 13f, 6f);
                    if (gesture != null) gestureUntil = now + 2.2f;
                }
                Play(Idle, 0.35f);
            }
            animator.enabled = near || speed > 0.05f || !posed;
            this.near = animator.enabled;
            lookWeight = Mathf.MoveTowards(lookWeight, camDist < 6f && Mathf.Abs(Mathf.DeltaAngle(bodyYaw, camYaw)) < 110f ? 1f : 0f, dt * 1.5f);

            if (near) Hair(dt);
        }

        /// <remarks>
        /// Bone changes made here don't reach the skinned body (it is skinned from the animator's pose), so this only
        /// works out the tray arm; the IK pass below hands the result to the animator a frame later.
        /// </remarks>
        void LateUpdate()
        {
            if (!near || rightArm == null || Pose != StaffPose.Tray) return;
            // Upper arm down by her side, forearm forward, palm up under the middle of the tray.
            Vector3 up = Vector3.up, fwd = transform.forward, right = transform.right;
            Vector3 shoulder = rightArm.Upper.position;
            rightArm.Reach(shoulder + right * 0.05f + fwd * 0.3f - up * 0.24f, shoulder + right * 0.2f - up * 0.3f - fwd * 0.12f,
                (fwd + right * 0.15f).normalized, up, 1f);
            trayUpper = rightArm.Upper.localRotation;
            trayLower = rightArm.Lower.localRotation;
            trayHand = rightArm.Hand.localRotation;
            trayPosed = true;
            if (tray != null)
            {
                tray.position = rightArm.Hand.position + rightArm.Along * 0.07f + up * 0.055f;
                tray.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            }
        }

        void Hair(float dt)
        {
            if (hairMaterials.Length == 0 || head == null) return;
            Vector3 hp = head.position;
            Vector3 v = (hp - lastHeadPos) / dt;
            Vector3 a = (v - lastHeadVel) / dt;
            lastHeadPos = hp;
            lastHeadVel = v;
            Vector3 right = transform.right, fwd = transform.forward;
            float ax = Mathf.Clamp(Vector3.Dot(a, right), -25f, 25f), az = Mathf.Clamp(Vector3.Dot(a, fwd), -25f, 25f);
            float breeze = Mathf.Sin(Time.time * 1.1f + phase) * 0.18f;
            hair.Step(dt, ax, az, breeze, 0f);
            Vector3 sway = right * hair.OffsetX(2) + fwd * hair.OffsetY(2);
            foreach (var m in hairMaterials)
            {
                m.SetVector("_Sway", sway);
                m.SetFloat("_HeadY", hp.y + 0.05f);
            }
        }

        /// <summary>
        /// IK pass, where pose changes reach the body: eye contact, relaxed fingers, the jaw while talking, the tray arm
        /// (solved in LateUpdate) and the host's wave (hand up, elbow out to the side).
        /// </summary>
        internal void OnIK()
        {
            if (animator == null) return;
            posed = true;
            if (lookCam != null && lookWeight > 0.01f)
            {
                Vector3 look = lookCam.transform.position;
                if (Time.time < talkUntil)
                {
                    // Small nods and tilts on the words (a few degrees, whatever the distance).
                    float t = Time.time + phase, d = Vector3.Distance(look, transform.position);
                    look += d * (Vector3.up * (Mathf.Sin(t * 4.7f) * 0.06f + Mathf.Sin(t * 1.9f) * 0.03f) + transform.right * Mathf.Sin(t * 1.3f) * 0.05f);
                }
                animator.SetLookAtWeight(lookWeight, 0.12f, 0.75f, 1f, 0.55f);
                animator.SetLookAtPosition(look);
            }
            foreach (var j in fingers) animator.SetBoneLocalRotation(j.Bone, j.Relaxed);
            if (jaw != null && Time.time < talkUntil)
                animator.SetBoneLocalRotation(HumanBodyBones.Jaw, jawRest * Quaternion.Euler(Mathf.Abs(Mathf.Sin(Time.time * 11f + phase)) * 9f, 0f, 0f));
            float slide = (Time.time - slideStart) / slideTime;
            bool sliding = slide >= 0f && slide < 1.5f;
            float g = (Time.time - talkGestureStart) / TalkGestureTime;
            bool waving = Time.time < waveUntil;
            if (!sliding && g >= 0f && g < 1f && !(talkGestureLeft ? leftArm == null : rightArm == null || trayPosed || waving))
            {
                // Open hand at waist height, forward and out to the side: well clear of the body.
                var arm = talkGestureLeft ? leftArm : rightArm;
                float w = Mathf.Sin(g * Mathf.PI) * 0.85f;
                Vector3 side = talkGestureLeft ? -transform.right : transform.right, fwd = transform.forward, shoulder = arm.Upper.position;
                var goal = talkGestureLeft ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
                var hint = talkGestureLeft ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
                animator.SetIKPositionWeight(goal, w);
                animator.SetIKPosition(goal, shoulder + side * (0.24f + 0.05f * Mathf.Sin(g * Mathf.PI)) + fwd * 0.28f - Vector3.up * 0.36f);
                animator.SetIKHintPositionWeight(hint, w);
                animator.SetIKHintPosition(hint, shoulder + side * 0.3f - Vector3.up * 0.32f - fwd * 0.12f);
            }
            if (sliding && leftArm != null && rightArm != null)
            {
                // Out along the table (0..1), then back to the animation (1..1.5).
                float w = slide < 0.2f ? slide / 0.2f : slide > 1f ? 1f - (slide - 1f) / 0.5f : 1f;
                Vector3 p = Vector3.Lerp(slideFrom, slideTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slide))) + Vector3.up * 0.04f;
                bool left = Vector3.Dot(p - transform.position, transform.right) < 0f;
                var arm = left ? leftArm : rightArm;
                Vector3 side = left ? -transform.right : transform.right, shoulder = arm.Upper.position;
                Vector3 reach = p - shoulder;
                if (reach.magnitude > 0.58f) p = shoulder + reach.normalized * 0.58f;
                var goal = left ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
                var hint = left ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
                animator.SetIKPositionWeight(goal, w);
                animator.SetIKPosition(goal, p);
                animator.SetIKHintPositionWeight(hint, w);
                animator.SetIKHintPosition(hint, shoulder + side * 0.35f - Vector3.up * 0.3f);
            }
            if (trayPosed)
            {
                animator.SetBoneLocalRotation(HumanBodyBones.RightUpperArm, trayUpper);
                animator.SetBoneLocalRotation(HumanBodyBones.RightLowerArm, trayLower);
                animator.SetBoneLocalRotation(HumanBodyBones.RightHand, trayHand);
            }
            else if (waving && rightArm != null)
            {
                float w = Mathf.Clamp01((waveUntil - Time.time) * 2f) * Mathf.Clamp01((Time.time - (waveUntil - 2.4f)) * 3f);
                Vector3 shoulder = rightArm.Upper.position, up = Vector3.up, right = transform.right;
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
                animator.SetIKPosition(AvatarIKGoal.RightHand, shoulder + right * (0.2f + Mathf.Sin(Time.time * 9f) * 0.06f) + up * 0.36f + transform.forward * 0.1f);
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w);
                animator.SetIKHintPosition(AvatarIKHint.RightElbow, shoulder + right * 0.4f - up * 0.1f);
            }
        }
    }

    /// <summary>Forwards Unity's IK callback (it only reaches components next to the Animator).</summary>
    public sealed class StaffIK : MonoBehaviour
    {
        public StaffFigure Figure;
        void OnAnimatorIK(int layerIndex) => Figure?.OnIK();
    }
}
