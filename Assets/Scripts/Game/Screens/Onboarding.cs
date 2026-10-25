using System;
using System.Collections;
using Casino.Art;
using Casino.Core.Player;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    static class Logo
    {
        public static void Create(RectTransform parent, float y, float scale = 1f)
        {
            var holder = UIKit.NewRect("Logo", parent).Center(0, y, 900 * scale, 220 * scale);
            Marquee.Create(holder, new Vector2(860 * scale, 200 * scale), 44f * scale);
            var title = UIKit.Label(holder, GameConfig.CasinoName, (int)(130 * scale), Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            title.font = Fonts.Brand;   // the name is a logo: the same lettering in every language
            ((RectTransform)title.transform).Center(0, 18 * scale, 860 * scale, 150 * scale);
            UIKit.TextOutline(title, Palette.Hex("6B2A00"), 3f * scale);
            UIKit.TextShadow(title, -7f * scale, 0.8f);
            var sub = UIKit.Label(holder, "•  " + Loc.T("CASINO") + "  •", (int)(44 * scale), Palette.Pink, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)sub.transform).Center(0, -58 * scale, 600 * scale, 60 * scale);
        }
    }

    /// <summary>
    /// Neutral age screen: asks for a date of birth without saying what the minimum age is. Digits are entered on an
    /// on-screen keypad, so the phone keyboard never opens and never covers the fields. Hardware keys also work.
    /// </summary>
    public sealed class AgeGateScreen : UIScreen
    {
        static readonly string[] Placeholders = { Loc.N("DD"), Loc.N("MM"), Loc.N("YYYY") };
        static readonly string[] Captions = { Loc.N("DAY"), Loc.N("MONTH"), Loc.N("YEAR") };
        static readonly int[] MaxDigits = { 2, 2, 4 };

        readonly string[] values = { "", "", "" };
        readonly Text[] texts = new Text[3];
        readonly Image[] frames = new Image[3];
        readonly Image[] carets = new Image[3];
        int active;
        Text error;

        protected override void Build()
        {
            Backgrounds.Casino(Root, Palette.Hex("2A0E4A"), Palette.Hex("0B0612"), new Vector2(-420, 260));

            var left = UIKit.NewRect("Left", Content).Center(-400, 0, 1000, 1080);
            Logo.Create(left, 330, 0.85f);

            // The first screen: the language can be changed right here, named in its own language.
            var language = UIKit.Button(Content, Loc.Current.NativeName, ButtonStyle.Dark, () => App.I.UI.Modal<LanguagePickerModal>(), 30,
                Icon.Globe, FontKind.Semi);
            language.LabelText.font = Fonts.LanguageNames;
            if (language.LabelText is LocText native) native.KeepOrder = true;
            language.Rect.At(1f, 1f, -36, -30, 380, 80, 1f, 1f);

            var panel = UIKit.Panel(left, "Panel", Palette.Panel, 36f, true);
            var prt = ((RectTransform)panel.transform).Center(0, -110, 980, 560);
            UIKit.Outline(prt, Palette.WithAlpha(Palette.Gold, 0.55f), 36f);
            var title = UIKit.Label(prt, Loc.T("WHEN WERE YOU BORN?"), 60, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)title.transform).At(0.5f, 1f, 0, -66, 900, 80);

            DateBox(prt, 0, -260, 200);
            DateBox(prt, 1, -30, 200);
            DateBox(prt, 2, 250, 300);

            error = UIKit.Label(prt, "", 30, Palette.Red, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)error.transform).Center(0, -72, 900, 50);

            var go = UIKit.Button(prt, Loc.T("CONTINUE"), ButtonStyle.Gold, Submit, 52);
            go.Rect.At(0.5f, 0f, 0, 88, 460, 110);

            BuildKeypad(UIKit.NewRect("Keypad", Content).Center(540, -60, 600, 700));

            var foot = UIKit.Label(Content, Loc.T("Chips are for entertainment only. They have no cash value and can never be exchanged for money or prizes."),
                26, Palette.TextMuted, TextAnchor.MiddleCenter);
            ((RectTransform)foot.transform).At(0.5f, 0f, 0, 40, 1600, 60);

            Refresh();
        }

        void DateBox(RectTransform parent, int index, float x, float width)
        {
            var caption = UIKit.Label(parent, Loc.T(Captions[index]), 26, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)caption.transform).Center(x, 140, width, 40);

            var bg = UIKit.Panel(parent, "Field " + Placeholders[index], Palette.Hex("FFF9EE"), 22f, true);
            var rt = ((RectTransform)bg.transform).Center(x, 50, width, 124);
            frames[index] = UIKit.Outline(rt, Palette.Gold, 22f);
            var text = UIKit.Label(rt, "", 66, Palette.TextDark, TextAnchor.MiddleCenter, FontKind.Bold, "Text");
            text.supportRichText = false;
            ((RectTransform)text.transform).Stretch(8, 4, 8, 0);
            texts[index] = text;
            var caret = UIKit.Image(rt, "Caret", null, Palette.GoldDark);
            ((RectTransform)caret.transform).At(0.5f, 0f, 0, 18, width * 0.5f, 6);
            carets[index] = caret;
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Click);
                active = index;
                Refresh();
            });
        }

        void BuildKeypad(RectTransform pad)
        {
            const float w = 176, h = 132, gap = 20;
            for (int i = 0; i < 12; i++)
            {
                int row = i / 3, col = i % 3;
                float x = (col - 1) * (w + gap), y = (1.5f - row) * (h + gap);
                UIButton key;
                if (i < 9)
                {
                    int digit = i + 1;
                    key = UIKit.Button(pad, digit.ToString(), ButtonStyle.Dark, () => Digit(digit), 72, null, FontKind.Bold);
                }
                else if (i == 9) key = UIKit.Button(pad, null, ButtonStyle.Red, Backspace, 0, Icon.Back);
                else if (i == 10) key = UIKit.Button(pad, "0", ButtonStyle.Dark, () => Digit(0), 72, null, FontKind.Bold);
                else key = UIKit.Button(pad, null, ButtonStyle.Green, Next, 0, Icon.Check);
                key.Rect.Center(x, y, w, h);
            }
        }

        void Digit(int d)
        {
            if (values[active].Length >= MaxDigits[active]) return;
            values[active] += (char)('0' + d);
            if (values[active].Length == MaxDigits[active] && active < 2) active++;
            error.text = "";
            Refresh();
        }

        void Backspace()
        {
            if (values[active].Length == 0 && active > 0) active--;
            if (values[active].Length > 0) values[active] = values[active].Substring(0, values[active].Length - 1);
            Refresh();
        }

        void Next()
        {
            if (active < 2 && values[active].Length > 0) { active++; Refresh(); }
            else Submit();
        }

        void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                bool empty = values[i].Length == 0;
                texts[i].text = empty ? Loc.T(Placeholders[i]) : values[i];
                texts[i].color = empty ? Palette.Hex("B9AFC8") : Palette.TextDark;
                frames[i].enabled = i == active;
            }
        }

        void Update()
        {
            for (int i = 0; i < 3; i++) carets[i].enabled = i == active && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;

            // Hardware keyboards (desktop, Chromebooks, phones with a keyboard attached).
            foreach (char c in Input.inputString)
            {
                if (c >= '0' && c <= '9') Digit(c - '0');
                else if (c == '\b') Backspace();
                else if (c == '\n' || c == '\r') Next();
                else if (c == '/' || c == '.' || c == '-' || c == ' ' || c == '\t') { if (active < 2) { active++; Refresh(); } }
            }
        }

        internal void TourSubmit(int d, int m, int y)
        {
            values[0] = d.ToString();
            values[1] = m.ToString();
            values[2] = y.ToString();
            Refresh();
            Submit();
        }

        void Submit()
        {
            int.TryParse(values[0], out int d);
            int.TryParse(values[1], out int m);
            int.TryParse(values[2], out int y);
            var today = DateTime.Today;
            if (!AgeGate.TryGetBirthDate(y, m, d, today, out var birth, out string problem))
            {
                error.text = Loc.T(problem);
                Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Error);
                return;
            }
            if (!AgeGate.IsOldEnough(birth, today, GameConfig.MinimumAge))
            {
                SaveSystem.UnderageBlocked = true;
                App.I.UI.Show<BlockedScreen>();
                return;
            }
            var save = App.I.Save;
            save.birthYear = y;
            save.birthMonth = m;
            save.birthDay = d;
            App.I.SaveSystem.MarkDirty();
            App.I.UI.Show<ProfileScreen>();
        }
    }

    /// <summary>Shown to players under the minimum age: no way back in, and the app closes itself.</summary>
    public sealed class BlockedScreen : UIScreen
    {
        Text countdown;

        protected override void Build()
        {
            var bg = UIKit.Image(Root, "Bg", ProceduralArt.Gradient("blocked", Palette.Hex("1A0B26"), Palette.Hex("050208")), Color.white);
            ((RectTransform)bg.transform).Stretch();
            var icon = UIKit.Image(Content, "Lock", ProceduralArt.IconSprite(Icon.Lock), Palette.TextMuted);
            ((RectTransform)icon.transform).Center(0, 240, 170, 170);
            var title = UIKit.Label(Content, Loc.T("SORRY, YOU CAN'T PLAY"), 84, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)title.transform).Center(0, 70, 1500, 110);
            var body = UIKit.Label(Content, Loc.F("{0} is only available to adults.", GameConfig.CasinoName + " " + Loc.T("CASINO")), 38,
                Palette.TextMuted);
            ((RectTransform)body.transform).Center(0, -40, 1400, 60);
            countdown = UIKit.Label(Content, "", 30, Palette.TextMuted);
            ((RectTransform)countdown.transform).Center(0, -130, 900, 50);
            var exit = UIKit.Button(Content, Loc.T("EXIT"), ButtonStyle.Dark, () => App.I.Quit(), 48);
            exit.Rect.Center(0, -250, 360, 110);
            StartCoroutine(AutoQuit());
        }

        IEnumerator AutoQuit()
        {
            for (int s = 8; s > 0; s--)
            {
                countdown.text = Loc.F("Closing in {0}...", s);
                yield return Tween.Wait(1f);
            }
            App.I.Quit();
        }

        public override bool OnBack()
        {
            App.I.Quit();
            return true;
        }
    }

    /// <summary>"Tell us about you": the answer picks who staffs the casino (changeable later in Settings).</summary>
    public sealed class ProfileScreen : UIScreen
    {
        PlayerGender? choice;
        readonly UIButton[] options = new UIButton[3];
        RectTransform preview;
        UIButton enter;
        Text previewTitle;

        protected override void Build()
        {
            Backgrounds.Casino(Root, Palette.Hex("2A0E4A"), Palette.Hex("0B0612"), new Vector2(0, 300));
            var title = UIKit.Label(Content, Loc.T("TELL US ABOUT YOU"), 88, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)title.transform).Center(0, 420, 1400, 110);
            UIKit.TextShadow(title, -5f, 0.8f);
            var sub = UIKit.Label(Content, Loc.T("We'll pick your hosts, dealers and bar staff to match. You can change this any time in Settings."), 32, Palette.TextMuted);
            ((RectTransform)sub.transform).Center(0, 340, 1500, 50);

            string[] labels = { Loc.T("I'M A MAN"), Loc.T("I'M A WOMAN"), Loc.T("PREFER NOT TO SAY") };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                options[i] = UIKit.Button(Content, labels[i], ButtonStyle.Dark, () => Choose((PlayerGender)index), 46, Icon.Person);
                options[i].Rect.Center(-560 + i * 560, 200, 500, 120);
            }

            previewTitle = UIKit.Label(Content, "", 34, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)previewTitle.transform).Center(0, 90, 1200, 50);
            preview = UIKit.NewRect("Preview", Content).Center(0, -120, 1400, 330);

            enter = UIKit.Button(Content, Loc.T("ENTER THE CASINO"), ButtonStyle.Gold, Finish, 56);
            enter.Rect.At(0.5f, 0f, 0, 110, 640, 120);
            enter.Interactable = false;
        }

        internal void TourChoose(PlayerGender gender) => Choose(gender);
        internal void TourFinish() => Finish();

        void Choose(PlayerGender gender)
        {
            choice = gender;
            for (int i = 0; i < options.Length; i++) options[i].SetStyle(i == (int)gender ? ButtonStyle.Purple : ButtonStyle.Dark);
            enter.Interactable = true;
            if (enter.GetComponent<Pulse>() == null) enter.gameObject.AddComponent<Pulse>();

            foreach (Transform child in preview) Destroy(child.gameObject);
            var style = NpcStyles.DefaultFor(gender);
            previewTitle.text = Loc.T("Your staff tonight:");
            StaffRole[] roles = { StaffRole.Host, StaffRole.Dealer, StaffRole.Server };
            for (int i = 0; i < roles.Length; i++)
            {
                var p = Staff.Portrait(preview, 250, roles[i], 0, true, style);
                p.Rect.Center(-380 + i * 380, 20, 250, 250);
                p.transform.localScale = Vector3.one * 0.6f;
                StartCoroutine(Tween.Scale(p.transform, Vector3.one, 0.35f + i * 0.08f));
            }
        }

        void Finish()
        {
            if (choice == null) return;
            var save = App.I.Save;
            save.gender = (int)choice.Value;
            save.npcStyle = (int)NpcStyles.DefaultFor(choice.Value);
            save.profileComplete = true;
            App.I.SaveSystem.SaveNow();
            Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Ding);
            App.I.EnterCasino();
        }
    }
}
