using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Core;
using Casino.Core.Blackjack;
using Casino.Core.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>A playing card built from a 9-sliced face, rank text and suit sprites. Flips by squashing on X.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public const float Width = 140f, Height = 196f;

        Image face, back;
        GameObject faceContent;
        Text rankTop, rankBottom, centerLetter;
        Image suitTop, suitBottom, suitCenter;

        public Card Card { get; private set; }
        public bool FaceUp { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public static CardView Create(Transform parent)
        {
            var rt = UIKit.NewRect("Card", parent);
            rt.sizeDelta = new Vector2(Width, Height);
            var v = rt.gameObject.AddComponent<CardView>();

            var shadow = UIKit.Image(rt, "Shadow", ProceduralArt.SoftShadow, new Color(0, 0, 0, 0.45f));
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 0.9f;
            ((RectTransform)shadow.transform).Stretch(-14, -20, -10, -6);

            v.face = UIKit.Image(rt, "Face", ProceduralArt.CardFace, Color.white);
            v.face.type = Image.Type.Sliced;
            v.face.pixelsPerUnitMultiplier = 1.1f;
            ((RectTransform)v.face.transform).Stretch();
            v.faceContent = v.face.gameObject;

            v.rankTop = UIKit.Label(v.face.transform, "", 46, Color.black, TextAnchor.UpperCenter, FontKind.Bold, "RankTop");
            v.rankTop.horizontalOverflow = HorizontalWrapMode.Overflow;
            ((RectTransform)v.rankTop.transform).At(0f, 1f, 8, -2, 50, 56, 0f, 1f);
            v.suitTop = UIKit.Image(v.face.transform, "SuitTop", null, Color.black);
            ((RectTransform)v.suitTop.transform).At(0f, 1f, 17, -58, 32, 32, 0f, 1f);

            v.rankBottom = UIKit.Label(v.face.transform, "", 46, Color.black, TextAnchor.UpperCenter, FontKind.Bold, "RankBottom");
            v.rankBottom.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rb = ((RectTransform)v.rankBottom.transform).At(1f, 0f, -8, 2, 50, 56, 0f, 1f);
            rb.localEulerAngles = new Vector3(0, 0, 180);
            v.suitBottom = UIKit.Image(v.face.transform, "SuitBottom", null, Color.black);
            var sb = ((RectTransform)v.suitBottom.transform).At(1f, 0f, -17, 58, 32, 32, 0f, 1f);
            sb.localEulerAngles = new Vector3(0, 0, 180);

            v.suitCenter = UIKit.Image(v.face.transform, "SuitCenter", null, Color.black);
            ((RectTransform)v.suitCenter.transform).Center(0, -6, 78, 78);
            v.centerLetter = UIKit.Label(v.face.transform, "", 92, Color.black, TextAnchor.MiddleCenter, FontKind.Display, "Letter");
            ((RectTransform)v.centerLetter.transform).Center(0, -4, 120, 120);

            v.back = UIKit.Image(rt, "Back", ProceduralArt.CardBack, Color.white);
            ((RectTransform)v.back.transform).Stretch();
            return v;
        }

        public void Set(Card card, bool faceUp)
        {
            Card = card;
            Color c = card.IsRed ? Palette.CardRed : Palette.CardBlack;
            Sprite suit = ProceduralArt.Suit(card.Suit);
            rankTop.text = rankBottom.text = card.RankLabel;
            rankTop.color = rankBottom.color = c;
            suitTop.sprite = suitBottom.sprite = suitCenter.sprite = suit;
            suitTop.color = suitBottom.color = c;
            if (card.IsFace)
            {
                centerLetter.text = card.RankLabel;
                centerLetter.color = c;
                centerLetter.enabled = true;
                suitCenter.color = Palette.WithAlpha(c, 0.16f);
                ((RectTransform)suitCenter.transform).sizeDelta = new Vector2(112, 112);
            }
            else
            {
                centerLetter.enabled = false;
                suitCenter.color = c;
                ((RectTransform)suitCenter.transform).sizeDelta = card.IsAce ? new Vector2(96, 96) : new Vector2(70, 70);
            }
            SetFaceUp(faceUp);
        }

        public void SetFaceUp(bool up)
        {
            FaceUp = up;
            faceContent.SetActive(up);
            back.gameObject.SetActive(!up);
        }

        public IEnumerator Flip(float duration = 0.22f)
        {
            Vector3 s = transform.localScale;
            yield return Tween.Run(duration / 2f, Ease.InQuad, k => transform.localScale = new Vector3(s.x * (1f - k), s.y * (1f + 0.06f * k), 1f));
            SetFaceUp(!FaceUp);
            Audio.Sfx.Play(Audio.SfxId.CardFlip, 0.8f, 0.08f);
            yield return Tween.Run(duration / 2f, Ease.OutQuad, k => transform.localScale = new Vector3(s.x * k, s.y * (1.06f - 0.06f * k), 1f));
            transform.localScale = s;
        }
    }

    /// <summary>A visual pile of casino chips for an amount, with the amount written underneath.</summary>
    public sealed class ChipStack : MonoBehaviour
    {
        readonly List<Image> chips = new List<Image>();
        Text label;
        long[] denominations;

        public long Amount { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public static ChipStack Create(Transform parent, long[] denominations, float chipSize = 92f)
        {
            var rt = UIKit.NewRect("ChipStack", parent);
            rt.sizeDelta = new Vector2(chipSize, chipSize);
            var s = rt.gameObject.AddComponent<ChipStack>();
            s.denominations = denominations;
            s.label = UIKit.Label(rt, "", 30, Color.white, TextAnchor.MiddleCenter, FontKind.Display, "Amount");
            ((RectTransform)s.label.transform).At(0.5f, 0f, 0, -24, 260, 40);
            UIKit.TextShadow(s.label, -2f, 0.9f);
            return s;
        }

        public void SetAmount(long amount)
        {
            Amount = amount;
            foreach (var c in chips) Destroy(c.gameObject);
            chips.Clear();
            label.text = amount > 0 ? ChipFormat.Short(amount) : "";
            if (amount <= 0) return;

            // Greedy breakdown into the table's chip values, biggest first, at most 10 chips drawn.
            var pile = new List<int>();
            long rest = amount;
            for (int d = denominations.Length - 1; d >= 0 && pile.Count < 10; d--)
                while (rest >= denominations[d] && pile.Count < 10)
                {
                    rest -= denominations[d];
                    pile.Add(d);
                }
            if (pile.Count == 0) pile.Add(0);
            pile.Reverse();
            float size = Rect.sizeDelta.x;
            for (int i = 0; i < pile.Count; i++)
            {
                var img = UIKit.Image(transform, "Chip", ProceduralArt.Chip(pile[i]), Color.white);
                var rt = ((RectTransform)img.transform).Center(0, i * 7f, size, size);
                rt.localScale = new Vector3(1f, 0.62f, 1f);
                chips.Add(img);
            }
            label.transform.SetAsLastSibling();
        }
    }

    /// <summary>White speech bubble with a tail and a typewriter reveal.</summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        Text text;
        CanvasGroup group;
        Coroutine routine;

        float minHeight;

        public static SpeechBubble Create(Transform parent, float width, float height, bool tailLeft = true)
        {
            var rt = UIKit.NewRect("Speech", parent);
            rt.sizeDelta = new Vector2(width, height);
            var b = rt.gameObject.AddComponent<SpeechBubble>();
            b.minHeight = height;
            b.group = UIKit.Group(b);
            b.group.alpha = 0f;
            b.group.blocksRaycasts = false;
            var shadowHost = UIKit.NewRect("ShadowHost", rt).Stretch();
            UIKit.DropShadow(rt, shadowHost, 14f, -8f, 0.45f);
            Destroy(shadowHost.gameObject);
            var bg = UIKit.Panel(rt, "Bubble", Palette.Hex("FFFDF7"), 30f);
            ((RectTransform)bg.transform).Stretch();
            var tail = UIKit.Image(rt, "Tail", ProceduralArt.BubbleTail, Palette.Hex("FFFDF7"));
            var trt = ((RectTransform)tail.transform).At(tailLeft ? 0f : 1f, 0f, tailLeft ? 40 : -40, -26, 46, 40);
            if (!tailLeft) trt.localScale = new Vector3(-1, 1, 1);
            b.text = UIKit.Label(rt, "", 30, Palette.TextDark, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)b.text.transform).Stretch(24, 10, 24, 10);
            return b;
        }

        public void Say(string line, float hold = 3.2f, StaffPortrait talker = null, Casino.Audio.VoicePitch voice = Casino.Audio.VoicePitch.None)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SayRoutine(line, hold, talker, voice));
        }

        public void Hide()
        {
            if (routine != null) StopCoroutine(routine);
            group.alpha = 0f;
        }

        IEnumerator SayRoutine(string line, float hold, StaffPortrait talker, Casino.Audio.VoicePitch voice)
        {
            // Size the bubble for the whole line first, so longer lines grow it instead of spilling out.
            text.text = line;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(minHeight, text.preferredHeight + 32f));
            text.text = "";
            group.alpha = 1f;
            transform.localScale = Vector3.one * 0.7f;
            StartCoroutine(Tween.Scale(transform, Vector3.one, 0.28f));
            if (talker != null) talker.Talk(line.Length * 0.03f + 0.2f);
            for (int i = 1; i <= line.Length; i++)
            {
                text.text = line.Substring(0, i);
                // A soft syllable every few letters, like the staff member murmuring the line.
                if (voice != Casino.Audio.VoicePitch.None && i % 4 == 1 && char.IsLetter(line[i - 1])) Casino.Audio.Sfx.Voice(voice);
                yield return Tween.Wait(0.022f);
            }
            yield return Tween.Wait(hold);
            yield return Tween.Fade(group, 0f, 0.3f);
            routine = null;
        }
    }

    /// <summary>Framed staff portrait: gentle idle bob, mouth animation while talking, a wink on good news.</summary>
    public sealed class StaffPortrait : MonoBehaviour
    {
        Image portrait;
        Text nameText;
        bool female;
        int seed;
        StaffRole role;
        float talkUntil, winkUntil, phase;
        Expression shown = Expression.Smile;

        public string StaffName { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        static readonly string[] FemaleNames = { "Sofia", "Elena", "Mia", "Valentina", "Chloe", "Isabella", "Nadia", "Aria", "Luna", "Camila" };
        static readonly string[] MaleNames = { "Marco", "Leo", "Adrian", "Lucas", "Viktor", "Daniel", "Gabriel", "Ethan", "Julian", "Rafael" };

        public static string NameFor(bool female, int index) => (female ? FemaleNames : MaleNames)[Mathf.Abs(index) % FemaleNames.Length];

        /// <param name="nameIndex">Distinct per post so no two staff share a name; defaults to the portrait seed.</param>
        public static StaffPortrait Create(Transform parent, float size, bool female, int seed, StaffRole role, bool nameplate = true, int nameIndex = -1)
        {
            var rt = UIKit.NewRect("Portrait", parent);
            rt.sizeDelta = new Vector2(size, size);
            UIKit.OwnCanvas(rt.gameObject, true);
            var p = rt.gameObject.AddComponent<StaffPortrait>();
            p.female = female;
            p.seed = seed;
            p.role = role;
            p.phase = seed * 1.7f;
            p.StaffName = NameFor(female, nameIndex >= 0 ? nameIndex : seed);

            var glow = UIKit.Image(rt, "Glow", ProceduralArt.Glow, Palette.WithAlpha(Palette.Gold, 0.35f));
            ((RectTransform)glow.transform).Center(0, 0, size * 1.45f, size * 1.45f);
            var ring = UIKit.Image(rt, "Rim", ProceduralArt.Circle, Palette.GoldDark);
            ((RectTransform)ring.transform).Center(0, 0, size + 14, size + 14);
            var ring2 = UIKit.Image(rt, "Rim2", ProceduralArt.Circle, Palette.Gold);
            ((RectTransform)ring2.transform).Center(0, 0, size + 6, size + 6);
            p.portrait = UIKit.Image(rt, "Face", ProceduralArt.Portrait(female, seed, role, Expression.Smile), Color.white);
            ((RectTransform)p.portrait.transform).Center(0, 0, size, size);

            if (nameplate)
            {
                var plate = UIKit.Panel(rt, "Nameplate", Palette.Hex("1A1024"), 20f);
                var prt = ((RectTransform)plate.transform).At(0.5f, 0f, 0, -8, Mathf.Max(170, size * 0.62f), 44);
                UIKit.Outline(prt, Palette.Gold, 20f);
                p.nameText = UIKit.Label(prt, p.StaffName.ToUpperInvariant(), 30, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
                ((RectTransform)p.nameText.transform).Stretch(0, 2, 0, 0);
            }
            return p;
        }

        /// <summary>Generates the talk and wink frames over the next two frames so the first line doesn't stutter.</summary>
        public void Prewarm() => StartCoroutine(PrewarmRoutine());

        IEnumerator PrewarmRoutine()
        {
            yield return null;
            ProceduralArt.Portrait(female, seed, role, Expression.Talk);
            yield return null;
            ProceduralArt.Portrait(female, seed, role, Expression.Wink);
        }

        public void Talk(float seconds) => talkUntil = Time.unscaledTime + seconds;
        public void Wink(float seconds = 1.2f) => winkUntil = Time.unscaledTime + seconds;

        void Update()
        {
            float t = Time.unscaledTime;
            float bob = Mathf.Sin(t * 1.6f + phase) * 0.012f;
            portrait.transform.localScale = new Vector3(1f + bob * 0.4f, 1f + bob, 1f);

            Expression want = Expression.Smile;
            if (t < winkUntil) want = Expression.Wink;
            else if (t < talkUntil) want = Mathf.Repeat(t * 7f, 1f) < 0.5f ? Expression.Talk : Expression.Smile;
            if (want == shown) return;
            shown = want;
            portrait.sprite = ProceduralArt.Portrait(female, seed, role, want);
        }
    }
}
