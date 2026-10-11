using System;
using System.Collections.Generic;
using UnityEngine;

namespace Casino.Platform
{
    public enum Haptic { Tap, Tick, Success, Big, Warning }

    /// <summary>Where vibration goes. Auto: the controller while one is connected, otherwise the phone.</summary>
    public enum VibrationTarget { Auto, Phone, Controller, Both }

    /// <summary>
    /// Vibration on the phone (Android's vibrator, with strength where the phone supports it) and controller rumble
    /// (Input System motor speeds). Settings pick where it goes and turn each one off or down separately.
    /// Only taps, wins and warnings vibrate: never losses.
    /// </summary>
    public static class Haptics
    {
        struct Step
        {
            public float Low, High, Seconds;
            public Step(float low, float high, float seconds)
            {
                Low = low;
                High = high;
                Seconds = seconds;
            }
        }

        // Phone: Android waveform timings (wait, buzz, wait, buzz... in ms) with an amplitude (0-255) for each.
        // Controller: steps of (low-frequency motor, high-frequency motor, seconds).
        static readonly Dictionary<Haptic, (long[] ms, int[] amp, Step[] pad)> Shapes = new Dictionary<Haptic, (long[], int[], Step[])>
        {
            [Haptic.Tap] = (new long[] { 0, 14 }, new[] { 0, 110 }, new[] { new Step(0f, 0.3f, 0.045f) }),
            [Haptic.Tick] = (new long[] { 0, 8 }, new[] { 0, 70 }, new[] { new Step(0f, 0.18f, 0.03f) }),
            [Haptic.Success] = (new long[] { 0, 30, 60, 45 }, new[] { 0, 160, 0, 210 },
                new[] { new Step(0.25f, 0.45f, 0.08f), new Step(0f, 0f, 0.06f), new Step(0.35f, 0.6f, 0.11f) }),
            [Haptic.Big] = (new long[] { 0, 60, 50, 60, 50, 140 }, new[] { 0, 200, 0, 230, 0, 255 },
                new[] { new Step(0.5f, 0.7f, 0.12f), new Step(0f, 0f, 0.06f), new Step(0.6f, 0.8f, 0.12f), new Step(0f, 0f, 0.06f), new Step(0.9f, 1f, 0.28f) }),
            [Haptic.Warning] = (new long[] { 0, 25, 70, 25 }, new[] { 0, 180, 0, 180 },
                new[] { new Step(0.5f, 0.15f, 0.07f), new Step(0f, 0f, 0.07f), new Step(0.5f, 0.15f, 0.07f) }),
        };

        static Step[] padSteps;
        static int padStep;
        static float padStepEnds;

        static Haptics() => Pad.DeviceChanged += PadVibrator.Reset;

        /// <summary>The phone can vibrate (an Android phone with a vibrator; never desktops).</summary>
        public static bool PhoneSupported => Phone.Supported;

        public static void Play(Haptic h)
        {
            if (App.I == null) return;
            var save = App.I.Save;
            bool pad = Pad.Connected;
            var target = (VibrationTarget)Mathf.Clamp(save.vibrateOn, 0, 3);
            bool toPad, toPhone;
            switch (target)
            {
                case VibrationTarget.Phone: toPhone = true; toPad = false; break;
                case VibrationTarget.Controller: toPad = true; toPhone = false; break;
                case VibrationTarget.Both: toPad = toPhone = true; break;
                default: toPad = pad; toPhone = !pad; break;
            }
            var shape = Shapes[h];
            if (toPhone && save.phoneVibration) Phone.Wave(shape.ms, shape.amp, save.phoneVibrationStrength);
            if (toPad && pad && save.padVibration) Rumble(shape.pad, save.padVibrationStrength);
        }

        /// <summary>A sample at the given strength, while its slider moves.</summary>
        public static void Preview(bool phone, float strength)
        {
            var shape = Shapes[Haptic.Success];
            if (phone) Phone.Wave(shape.ms, shape.amp, strength);
            else if (Pad.Connected) Rumble(shape.pad, strength);
        }

        static void Rumble(Step[] steps, float strength)
        {
            if (strength <= 0.01f || PadVibrator.Play(steps, strength)) return;
            padSteps = new Step[steps.Length];
            for (int i = 0; i < steps.Length; i++)
                padSteps[i] = new Step(Mathf.Clamp01(steps[i].Low * strength), Mathf.Clamp01(steps[i].High * strength), steps[i].Seconds);
            padStep = -1;
            padStepEnds = 0f;
            Tick();
        }

        /// <summary>Called every frame by App: moves controller rumble patterns along and stops the motors at the end.</summary>
        public static void Tick()
        {
            if (padSteps == null || Time.unscaledTime < padStepEnds) return;
            var g = Pad.Current;
            padStep++;
            if (g == null || padStep >= padSteps.Length)
            {
                padSteps = null;
                g?.SetMotorSpeeds(0f, 0f);
                return;
            }
            var s = padSteps[padStep];
            g.SetMotorSpeeds(s.Low, s.High);
            padStepEnds = Time.unscaledTime + s.Seconds;
        }

        /// <summary>Going to the background: no motor may keep running.</summary>
        public static void Stop()
        {
            padSteps = null;
            Pad.Current?.SetMotorSpeeds(0f, 0f);
            PadVibrator.Cancel();
        }

        /// <summary>
        /// Unity's Input System can't drive controller motors on Android, so there rumble goes through the controller's
        /// own vibrator (InputDevice.getVibrator), which Android provides for Xbox, PlayStation and most Bluetooth pads.
        /// Elsewhere Play returns false and the Input System's motor speeds are used.
        /// </summary>
        static class PadVibrator
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const int SourceGamepad = 0x00000401;
            static bool looked;
            static AndroidJavaObject vibrator;

            public static void Reset()
            {
                looked = false;
                vibrator?.Dispose();
                vibrator = null;
            }

            static AndroidJavaObject Find()
            {
                if (looked) return vibrator;
                looked = true;
                try
                {
                    using var devices = new AndroidJavaClass("android.view.InputDevice");
                    foreach (int id in devices.CallStatic<int[]>("getDeviceIds"))
                    {
                        using var device = devices.CallStatic<AndroidJavaObject>("getDevice", id);
                        if (device == null || (device.Call<int>("getSources") & SourceGamepad) != SourceGamepad) continue;
                        var v = device.Call<AndroidJavaObject>("getVibrator");
                        if (v != null && v.Call<bool>("hasVibrator"))
                        {
                            vibrator = v;
                            break;
                        }
                        v?.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Controller vibration unavailable: " + e.Message);
                }
                return vibrator;
            }

            public static bool Play(Step[] steps, float strength)
            {
                var v = Find();
                if (v == null) return false;
                try
                {
                    var ms = new long[steps.Length];
                    var amp = new int[steps.Length];
                    for (int i = 0; i < steps.Length; i++)
                    {
                        ms[i] = Math.Max(1, (long)(steps[i].Seconds * 1000f));
                        float k = Mathf.Max(steps[i].Low, steps[i].High) * strength;
                        amp[i] = k <= 0.001f ? 0 : Mathf.Clamp(Mathf.RoundToInt(k * 255f), 1, 255);
                    }
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effects.CallStatic<AndroidJavaObject>("createWaveform", ms, amp, -1);
                    v.Call("vibrate", effect);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Controller vibration failed: " + e.Message);
                    return false;
                }
            }

            public static void Cancel()
            {
                try { vibrator?.Call("cancel"); }
                catch (Exception) { }
            }
#else
            public static void Reset() { }
            public static bool Play(Step[] steps, float strength) => false;
            public static void Cancel() { }
#endif
        }

        /// <summary>Android's vibrator through JNI: VibrationEffect waveforms (Android 8+), plain patterns before that.</summary>
        static class Phone
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            static bool tried;
            static AndroidJavaObject vibrator;
            static bool amplitudeControl;
            static int sdk;

            static bool Init()
            {
                if (tried) return vibrator != null;
                tried = true;
                try
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    if (vibrator != null && !vibrator.Call<bool>("hasVibrator")) vibrator = null;
                    amplitudeControl = vibrator != null && sdk >= 26 && vibrator.Call<bool>("hasAmplitudeControl");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Vibration unavailable: " + e.Message);
                    vibrator = null;
                }
                return vibrator != null;
            }

            public static bool Supported => Init();

            public static void Wave(long[] ms, int[] amp, float strength)
            {
                if (strength <= 0.01f || !Init()) return;
                try
                {
                    // Phones without strength control get shorter buzzes instead, which feel weaker.
                    var timings = (long[])ms.Clone();
                    if (!amplitudeControl)
                        for (int i = 1; i < timings.Length; i += 2) timings[i] = Math.Max(4, (long)(timings[i] * Mathf.Lerp(0.4f, 1f, strength)));
                    if (sdk >= 26)
                    {
                        var scaled = new int[amp.Length];
                        for (int i = 0; i < amp.Length; i++) scaled[i] = amp[i] == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(amp[i] * strength), 1, 255);
                        using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = amplitudeControl
                                   ? effects.CallStatic<AndroidJavaObject>("createWaveform", timings, scaled, -1)
                                   : effects.CallStatic<AndroidJavaObject>("createWaveform", timings, -1))
                            vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        vibrator.Call("vibrate", timings, -1);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Vibration failed: " + e.Message);
                    Handheld.Vibrate();
                }
            }
#else
            public static bool Supported => false;
            public static void Wave(long[] ms, int[] amp, float strength) { }
#endif
        }
    }
}
