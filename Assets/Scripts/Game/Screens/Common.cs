using System;
using System.Collections;
using Casino.Art;
using Casino.Core;
using Casino.Core.Economy;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    public static class Backgrounds
    {
        /// <summary>Night-club backdrop: deep gradient, slow light rays, coloured glows and twinkles.</summary>
        public static void Casino(RectTransform root, Color top, Color bottom, Vector2 raysAt, bool twinkles = true)
        {
            var grad = UIKit.Image(root, "Gradient", ProceduralArt.Gradient(top.ToString() + bottom, top, bottom), Color.white);
            ((RectTransform)grad.transform).Stretch();
            var rays = UIKit.Image(root, "Rays", ProceduralArt.Sunburst, new Color(1f, 0.85f, 0.55f, 0.12f));
            ((RectTransform)rays.transform).Center(raysAt.x, raysAt.y, 2600, 2600);
            rays.gameObject.AddComponent<Spinner>().DegreesPerSecond = 4f;
            var g1 = UIKit.Image(root, "GlowPink", ProceduralArt.Glow, Palette.WithAlpha(Palette.Magenta, 0.35f));
            ((RectTransform)g1.transform).Center(-760, -380, 1100, 900);
            var g2 = UIKit.Image(root, "GlowCyan", ProceduralArt.Glow, Palette.WithAlpha(Palette.Cyan, 0.18f));
            ((RectTransform)g2.transform).Center(820, 360, 1000, 800);
            if (twinkles) Twinkles.Create(root, 18, new Rect(-1000, -560, 2000, 1120));
            var vig = UIKit.Image(root, "Vignette", ProceduralArt.Vignette, Color.white);
            ((RectTransform)vig.transform).Stretch(-60, -60, -60, -60);
        }
    }

    /// <summary>Generic message with one or two buttons.</summary>
    public sealed class ConfirmModal : UIModal
    {
        string title, body, yes, no;
        Action onYes, onNo;
        ButtonStyle yesStyle = ButtonStyle.Gold;

        public void Setup(string titleText, string bodyText, string yesLabel, Action yesAction, string noLabel = null, Action noAction = null,
            ButtonStyle style = ButtonStyle.Gold)
        {
            title = titleText;
            body = bodyText;
            yes = yesLabel;
            no = noLabel;
            onYes = yesAction;
            onNo = noAction;
            yesStyle = style;
        }

        protected override void Build()
        {
            MakePanel(980, 520, title);
            var t = UIKit.Label(Panel, body, 36, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Body);
            ((RectTransform)t.transform).Stretch(70, 150, 70, 130);
            if (no != null)
            {
                var n = UIKit.Button(Panel, no, ButtonStyle.Dark, () => { Close(); onNo?.Invoke(); }, 44);
                n.Rect.At(0.5f, 0f, -200, 80, 340, 100);
                var y = UIKit.Button(Panel, yes, yesStyle, () => { Close(); onYes?.Invoke(); }, 44);
                y.Rect.At(0.5f, 0f, 200, 80, 340, 100);
            }
            else
            {
                var y = UIKit.Button(Panel, yes, yesStyle, () => { Close(); onYes?.Invoke(); }, 44);
                y.Rect.At(0.5f, 0f, 0, 80, 400, 100);
            }
        }
    }

    /// <summary>Plain-language explanation of Beginner's Luck, so the boost is never hidden from the player.</summary>
    public sealed class LuckInfoModal : UIModal
    {
        protected override void Build()
        {
            MakePanel(1180, 720, "BEGINNER'S LUCK");
            AddCloseButton();
            var luck = App.I.Luck;
            string body =
                $"Welcome bonus for new players: for your first <b>{luck.FullHands} hands</b> the dealer's shoe is stacked in your favor, " +
                $"so you will win almost every hand.\n\nOver the next <b>{luck.FadeHands} hands</b> the boost fades out, and after that every table " +
                "is standard, fair blackjack dealt from a shuffled 6-deck shoe (blackjack pays 3 to 2, dealer stands on all 17s).\n\n" +
                $"Boost right now: <b><color=#7CFC9A>{Mathf.RoundToInt((float)luck.Luck * 100)}%</color></b>   Hands left: <b>{luck.HandsRemaining}</b>";
            var t = UIKit.Label(Panel, body, 34, Color.white, TextAnchor.UpperLeft, FontKind.Body);
            ((RectTransform)t.transform).Stretch(70, 150, 70, 140);
            var ok = UIKit.Button(Panel, "GOT IT", ButtonStyle.Green, Close, 44);
            ok.Rect.At(0.5f, 0f, 0, 80, 360, 100);
        }
    }

    /// <summary>Development stand-in for the platform payment sheet. Never used in release builds.</summary>
    public sealed class SimulatedPurchaseModal : UIModal
    {
        string sku;
        Action<bool> done;
        bool answered;

        public void Setup(string storeSku, Action<bool> callback)
        {
            sku = storeSku;
            done = callback;
            CloseOnBackdrop = false;
        }

        protected override void Build()
        {
            var product = ProductCatalog.Find(RegionalPricing.ProductIdFromSku(sku));
            MakePanel(1000, 600, "TEST PURCHASE");
            string what = product == null ? sku : product.Kind == ProductKind.Chips ? $"{ChipFormat.Full(product.Chips)} chips" : product.Title;
            var t = UIKit.Label(Panel,
                $"{what}\n<size=30><color=#C9BFDB>Store SKU: {sku}\nPrice shown: {App.I.Purchases.DisplayPrice(product)}\n\nTest build - no real money is charged.</color></size>",
                40, Color.white, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)t.transform).Stretch(60, 150, 60, 130);
            var no = UIKit.Button(Panel, "CANCEL", ButtonStyle.Dark, () => Answer(false), 42);
            no.Rect.At(0.5f, 0f, -210, 80, 360, 100);
            var yes = UIKit.Button(Panel, "BUY (TEST)", ButtonStyle.Green, () => Answer(true), 42);
            yes.Rect.At(0.5f, 0f, 210, 80, 360, 100);
        }

        void Answer(bool ok)
        {
            if (answered) return;
            answered = true;
            Close();
            done?.Invoke(ok);
        }

        public override bool OnBack()
        {
            Answer(false);
            return true;
        }
    }

    /// <summary>Development stand-in for a rewarded video: a short countdown, then the reward.</summary>
    public sealed class SimulatedAdModal : UIModal
    {
        Action<bool> done;
        Text countdown;
        bool finished, answered;

        public void Setup(Action<bool> callback)
        {
            done = callback;
            CloseOnBackdrop = false;
        }

        protected override void Build()
        {
            Backdrop.color = Color.black;
            MakePanel(1100, 640, "ADVERTISEMENT");
            var t = UIKit.Label(Panel, "Your rewarded video would play here.\n<size=28><color=#C9BFDB>(Simulated ad - test build)</color></size>", 38, Color.white);
            ((RectTransform)t.transform).Stretch(60, 250, 60, 140);
            countdown = UIKit.Label(Panel, "5", 120, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)countdown.transform).At(0.5f, 0f, 0, 200, 300, 140);
            var close = UIKit.IconButton(Panel, Icon.Close, ButtonStyle.Dark, Skip, 70f);
            close.Rect.At(1f, 1f, -22, -22, 70, 70, 1f, 1f);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            for (int i = 5; i > 0; i--)
            {
                countdown.text = i.ToString();
                yield return Tween.Wait(1f);
            }
            finished = true;
            countdown.text = "REWARD!";
            yield return Tween.Wait(0.6f);
            Answer(true);
        }

        void Skip() => Answer(finished);

        void Answer(bool earned)
        {
            if (answered) return;
            answered = true;
            Close();
            done?.Invoke(earned);
        }

        public override bool OnBack()
        {
            Skip();
            return true;
        }
    }
}
