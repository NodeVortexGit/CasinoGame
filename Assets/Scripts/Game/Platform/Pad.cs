using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Casino.Platform
{
    public enum PadFamily { Xbox, PlayStation, Nintendo }

    /// <summary>What a button does. Confirm and Back sit on different physical buttons on Nintendo pads.</summary>
    public enum PadButton { Confirm, Back, Menu, View }

    /// <summary>
    /// Game controllers through Unity's Input System: Xbox, PlayStation and Nintendo-style pads, and generic USB or
    /// Bluetooth ones. Works out which family is connected, so prompts show that controller's button names and a
    /// Nintendo pad confirms with A on the right as on a Switch. Also tracks whether the controller or the touch
    /// screen/mouse was used last, so the controller's focus ring only shows while someone is using it.
    /// </summary>
    public static class Pad
    {
        const int Sony = 0x054C, Nintendo = 0x057E, Microsoft = 0x045E;
        static readonly Regex VendorId = new Regex("\"vendorId\"\\s*:\\s*(\\d+)");

        static int frame = -1;
        static Gamepad device;
        static PadFamily detected;
        static bool active;

        /// <summary>The connected controller (or the detected family, or the chosen layout) changed.</summary>
        public static event Action Changed;

        /// <summary>A controller was connected or disconnected.</summary>
        public static event Action DeviceChanged;

        public static Gamepad Current
        {
            get
            {
                Sync();
                return device;
            }
        }

        public static bool Connected => Current != null;

        /// <summary>The controller was used more recently than the touch screen or the mouse.</summary>
        public static bool Active
        {
            get
            {
                Sync();
                return active && device != null;
            }
        }

        public static PadFamily Family
        {
            get
            {
                Sync();
                int layout = App.I != null ? App.I.Save.padLayout : 0;
                return layout >= 1 && layout <= 3 ? (PadFamily)(layout - 1) : detected;
            }
        }

        public static string DeviceName => Current != null ? Current.displayName : null;

        public static string FamilyName(PadFamily f) => f == PadFamily.PlayStation ? "PlayStation" : f == PadFamily.Nintendo ? "Nintendo" : "Xbox";

        /// <summary>Frame the controller's back button was last handled (Android may also report it as the system Back key).</summary>
        public static int BackFrame = -100;

        /// <summary>
        /// True when an Escape/Back key press is really the controller's own back button, already handled this frame or
        /// the last: Android turns an unhandled B into a system Back as well.
        /// </summary>
        public static bool BackJustHandled
        {
            get
            {
                var g = Current;
                return Time.frameCount - BackFrame <= 2 || (g != null && (g.buttonEast.isPressed || g.buttonSouth.isPressed));
            }
        }

        /// <summary>Settings changed the layout: refresh the prompts.</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        // ---------------- reading ----------------

        public static bool Pressed(PadButton b)
        {
            var c = Control(b);
            return c != null && c.wasPressedThisFrame;
        }

        /// <summary>Left stick: walking.</summary>
        public static Vector2 Move => Current != null ? Current.leftStick.ReadValue() : Vector2.zero;

        /// <summary>Right stick: looking around.</summary>
        public static Vector2 Look => Current != null ? Current.rightStick.ReadValue() : Vector2.zero;

        /// <summary>The D-pad as held directions, -1..1 on each axis.</summary>
        public static Vector2Int Dpad
        {
            get
            {
                var g = Current;
                if (g == null) return Vector2Int.zero;
                return new Vector2Int((g.dpad.right.isPressed ? 1 : 0) - (g.dpad.left.isPressed ? 1 : 0),
                    (g.dpad.up.isPressed ? 1 : 0) - (g.dpad.down.isPressed ? 1 : 0));
            }
        }

        public enum Face { South, East, West, North }

        /// <summary>Which physical button does what: Nintendo confirms with the right one (A), the others the bottom one.</summary>
        public static Face FaceOf(PadButton b) =>
            b == PadButton.Confirm ? (Family == PadFamily.Nintendo ? Face.East : Face.South)
            : Family == PadFamily.Nintendo ? Face.South : Face.East;

        static ButtonControl Control(PadButton b)
        {
            var g = Current;
            if (g == null) return null;
            switch (b)
            {
                case PadButton.Menu:
                case PadButton.View: return IsStart(b) ? g.startButton : g.selectButton;
                default: return FaceOf(b) == Face.East ? g.buttonEast : g.buttonSouth;
            }
        }

        /// <summary>
        /// Whether Menu (Settings) or View (Daily) is the Start button. Settings can swap them, for pads whose middle
        /// buttons sit the other way round.
        /// </summary>
        static bool IsStart(PadButton b)
        {
            bool swap = App.I != null && App.I.Save.padSwapMenuView;
            return (b == PadButton.Menu) != swap;
        }

        /// <summary>The button's name on this controller, for text prompts ("A", "Cross", "Options"...).</summary>
        public static string Name(PadButton b)
        {
            var f = Family;
            switch (b)
            {
                case PadButton.Menu:
                case PadButton.View:
                    return IsStart(b) ? (f == PadFamily.PlayStation ? "Options" : f == PadFamily.Nintendo ? "+" : "Menu")
                        : f == PadFamily.PlayStation ? "Create" : f == PadFamily.Nintendo ? "-" : "View";
                default: return FaceName(FaceOf(b), f);
            }
        }

        public static string FaceName(Face face, PadFamily f)
        {
            switch (f)
            {
                case PadFamily.PlayStation: return face == Face.South ? "Cross" : face == Face.East ? "Circle" : face == Face.West ? "Square" : "Triangle";
                case PadFamily.Nintendo: return face == Face.South ? "B" : face == Face.East ? "A" : face == Face.West ? "Y" : "X";
                default: return face == Face.South ? "A" : face == Face.East ? "B" : face == Face.West ? "X" : "Y";
            }
        }

        // ---------------- per-frame bookkeeping ----------------

        static void Sync()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;

            var g = Gamepad.current;
            if (g != null && !g.added) g = null;
            if (g != device)
            {
                device = g;
                detected = g != null ? Detect(g) : PadFamily.Xbox;
                if (g == null) active = false;
                Changed?.Invoke();
                DeviceChanged?.Invoke();
            }

            if (device != null && Used(device)) active = true;
            if (active && PointerUsed()) active = false;
        }

        static bool Used(Gamepad g)
        {
            if (g.leftStick.ReadValue().sqrMagnitude > 0.25f || g.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;
            var controls = g.allControls;
            for (int i = 0; i < controls.Count; i++)
                if (controls[i] is ButtonControl b && b.wasPressedThisFrame) return true;
            return false;
        }

        static bool PointerUsed()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.press.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            return mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 9f || mouse.leftButton.wasPressedThisFrame ||
                mouse.rightButton.wasPressedThisFrame);
        }

        /// <summary>
        /// Xbox, PlayStation or Nintendo, from the USB vendor id when the platform reports it, else from the names.
        /// Anything unknown gets Xbox names, which most generic pads print on their buttons.
        /// </summary>
        static PadFamily Detect(InputDevice d)
        {
            var desc = d.description;
            int vendor = 0;
            var m = VendorId.Match(desc.capabilities ?? "");
            if (m.Success) int.TryParse(m.Groups[1].Value, out vendor);
            if (vendor == Sony) return PadFamily.PlayStation;
            if (vendor == Nintendo) return PadFamily.Nintendo;
            if (vendor == Microsoft) return PadFamily.Xbox;

            string s = (d.layout + " " + desc.product + " " + desc.manufacturer + " " + d.displayName).ToLowerInvariant();
            if (Has(s, "xbox", "xinput", "microsoft", "x-box")) return PadFamily.Xbox;
            if (Has(s, "dualshock", "dualsense", "playstation", "sony", "wireless controller", "ps3", "ps4", "ps5")) return PadFamily.PlayStation;
            if (Has(s, "nintendo", "switch", "pro controller", "joy-con", "joycon")) return PadFamily.Nintendo;
            return PadFamily.Xbox;
        }

        static bool Has(string s, params string[] words)
        {
            foreach (var w in words)
                if (s.Contains(w)) return true;
            return false;
        }
    }
}
