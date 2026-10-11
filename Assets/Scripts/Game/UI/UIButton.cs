using System;
using Casino.Art;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>Glossy casino button: coloured base, top highlight, drop shadow, press-squash and click sound.</summary>
    public sealed class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Button button;
        Image face, gloss, shadow, iconImage;
        Text label;
        Color baseColor;
        float targetScale = 1f;
        Action onClick;

        public Button Button => button;
        public ButtonStyle Style { get; private set; }
        public Text LabelText => label;
        public Image Face => face;
        public RectTransform Rect => (RectTransform)transform;

        public bool Interactable
        {
            get => button.interactable;
            set
            {
                if (button.interactable == value) return;
                button.interactable = value;
                Refresh();
            }
        }

        public void Build(string text, ButtonStyle style, Action click, int fontSize, Icon? icon, FontKind font, float radius)
        {
            onClick = click;
            Style = style;
            baseColor = UIKit.StyleColor(style);

            shadow = UIKit.Image(transform, "Shadow", null, new Color(0, 0, 0, style == ButtonStyle.Ghost ? 0f : 0.45f));
            UIKit.Rounded(shadow, radius);
            ((RectTransform)shadow.transform).Stretch(0, -7, 0, 7);

            face = UIKit.Image(transform, "Face", null, baseColor, true);
            UIKit.Rounded(face, radius);
            ((RectTransform)face.transform).Stretch();

            gloss = UIKit.Image(face.transform, "Gloss", ProceduralArt.GradientV, new Color(1, 1, 1, style == ButtonStyle.Ghost ? 0.05f : 0.28f));
            ((RectTransform)gloss.transform).Stretch(6, 0, 6, 5).anchorMin = new Vector2(0, 0.45f);

            if (style == ButtonStyle.Ghost) UIKit.Outline(face.transform, new Color(1, 1, 1, 0.35f), radius);

            if (icon.HasValue)
            {
                iconImage = UIKit.Image(face.transform, "Icon", ProceduralArt.IconSprite(icon.Value),
                    icon.Value == Icon.Coin ? Color.white : (style == ButtonStyle.Gold ? Palette.TextDark : Color.white));
                iconImage.preserveAspect = true;
                var irt = (RectTransform)iconImage.transform;
                if (string.IsNullOrEmpty(text)) irt.Stretch(14, 14, 14, 14);
                else irt.At(0f, 0.5f, 22, 0, fontSize * 1.1f, fontSize * 1.1f, 0f, 0.5f);
            }

            if (!string.IsNullOrEmpty(text))
            {
                label = UIKit.Label(face.transform, text, fontSize, style == ButtonStyle.Gold ? Palette.TextDark : Color.white,
                    TextAnchor.MiddleCenter, font);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var lrt = (RectTransform)label.transform;
                lrt.Stretch(icon.HasValue ? fontSize * 1.2f + 16 : 10, 4, 10, 0);
                if (style != ButtonStyle.Gold) UIKit.TextShadow(label, -2f, 0.45f);
            }

            button = gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Click);
                onClick?.Invoke();
            });
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void SetStyle(ButtonStyle style)
        {
            Style = style;
            baseColor = UIKit.StyleColor(style);
            if (label != null) label.color = style == ButtonStyle.Gold ? Palette.TextDark : Color.white;
            Refresh();
        }

        public void SetOnClick(Action click) => onClick = click;

        public void SetIcon(Icon icon)
        {
            if (iconImage != null) iconImage.sprite = ProceduralArt.IconSprite(icon);
        }

        void Refresh()
        {
            bool on = button.interactable;
            face.color = on ? baseColor : Palette.Mix(baseColor, new Color(0.25f, 0.22f, 0.3f, baseColor.a), 0.7f);
            if (label != null) label.color = new Color(label.color.r, label.color.g, label.color.b, on ? 1f : 0.45f);
            if (iconImage != null) iconImage.color = new Color(iconImage.color.r, iconImage.color.g, iconImage.color.b, on ? 1f : 0.45f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (button.interactable) targetScale = 0.93f;
        }

        public void OnPointerUp(PointerEventData e) => targetScale = 1f;
        public void OnPointerExit(PointerEventData e) => targetScale = 1f;

        // Squashes the face, not the root, so it composes with Pulse or tweens on the button itself.
        void Update()
        {
            var face = this.face.transform;
            float s = face.localScale.x;
            if (Mathf.Abs(s - targetScale) < 0.001f) return;
            s = Mathf.MoveTowards(s, targetScale, Time.unscaledDeltaTime * 1.6f);
            face.localScale = new Vector3(s, s, 1f);
        }
    }
}
