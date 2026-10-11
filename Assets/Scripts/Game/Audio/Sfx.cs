using System;
using System.Collections.Generic;
using UnityEngine;

namespace Casino.Audio
{
    public enum SfxId
    {
        Click, Whoosh, CardDeal, CardFlip, Chip, ChipStack, Win, BigWin, Blackjack, Lose, Push, Bust, Coins, Error,
        Eat, Shuffle, Tick, Ding, VoiceA, VoiceO, VoiceE,
    }

    /// <summary>Which voice a member of staff "speaks" with while their speech bubble types out.</summary>
    public enum VoicePitch { None, High, Low }

    /// <summary>All sound effects are synthesised at startup: no audio files, tiny memory footprint.</summary>
    public static class Sfx
    {
        const int Rate = 22050;
        static readonly Dictionary<SfxId, AudioClip> Clips = new Dictionary<SfxId, AudioClip>();
        static AudioSource[] sources;
        static int next;
        static uint noiseState = 0x12345678;

        public static float Volume { get; set; } = 0.8f;

        /// <summary>The staff's chatter, relative to the effects volume.</summary>
        public static float VoiceVolume { get; set; } = 1f;

        public static void Init(GameObject host)
        {
            sources = new AudioSource[5];
            for (int i = 0; i < sources.Length; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                sources[i] = s;
            }
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                float[] data = Generate(id);
                var clip = AudioClip.Create("sfx_" + id, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                Clips[id] = clip;
            }
        }

        /// <summary>One soft, wordless syllable in a staff member's voice (played as their speech bubble types).</summary>
        public static void Voice(VoicePitch voice)
        {
            if (voice == VoicePitch.None) return;
            var id = (SfxId)((int)SfxId.VoiceA + UnityEngine.Random.Range(0, 3));
            float pitch = voice == VoicePitch.High ? UnityEngine.Random.Range(0.96f, 1.1f) : UnityEngine.Random.Range(0.6f, 0.68f);
            if (VoiceVolume > 0f) PlayPitched(id, 0.32f * VoiceVolume, pitch);
        }

        public static void PlayPitched(SfxId id, float volume, float pitch)
        {
            if (sources == null || Volume <= 0f || !Clips.TryGetValue(id, out var clip)) return;
            var s = sources[next];
            next = (next + 1) % sources.Length;
            s.pitch = pitch;
            s.PlayOneShot(clip, Volume * volume);
        }

        public static void Play(SfxId id, float volume = 1f, float pitchJitter = 0f)
        {
            Buzz(id);
            if (sources == null || Volume <= 0f || !Clips.TryGetValue(id, out var clip)) return;
            var s = sources[next];
            next = (next + 1) % sources.Length;
            s.pitch = 1f + (pitchJitter > 0 ? UnityEngine.Random.Range(-pitchJitter, pitchJitter) : 0f);
            s.PlayOneShot(clip, Volume * volume);
        }

        /// <summary>The vibration that goes with a sound (even with the sound off). Losing never vibrates.</summary>
        static void Buzz(SfxId id)
        {
            switch (id)
            {
                case SfxId.Click:
                case SfxId.Chip:
                case SfxId.ChipStack:
                    Casino.Platform.Haptics.Play(Casino.Platform.Haptic.Tap);
                    break;
                case SfxId.Win:
                case SfxId.Coins:
                case SfxId.Ding:
                    Casino.Platform.Haptics.Play(Casino.Platform.Haptic.Success);
                    break;
                case SfxId.BigWin:
                case SfxId.Blackjack:
                    Casino.Platform.Haptics.Play(Casino.Platform.Haptic.Big);
                    break;
                case SfxId.Error:
                    Casino.Platform.Haptics.Play(Casino.Platform.Haptic.Warning);
                    break;
            }
        }

        // ---------------- synthesis ----------------

        static float Noise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState & 0xFFFFFF) / (float)0x7FFFFF - 1f;
        }

        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        static float Sine(double phase) => (float)Math.Sin(phase * 2.0 * Math.PI);
        static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        static void AddTone(float[] b, float start, float dur, float freq, float amp, float decay, float shape = 0f, float endFreq = 0f)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = endFreq > 0 ? Mathf.Lerp(freq, endFreq, t / dur) : freq;
                phase += f / Rate;
                float frac = (float)(phase - Math.Floor(phase));
                float tri = 1f - 4f * Mathf.Abs(frac - 0.5f);
                float v = Mathf.Lerp(Sine(phase), tri, shape);
                float env = Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 800f);
                b[s0 + i] += v * env * amp;
            }
        }

        static void AddNoise(float[] b, float start, float dur, float amp, float decay, float lowpass, float highpass)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            float lp = 0, hpPrev = 0, hpOut = 0;
            float aLp = Mathf.Clamp01(lowpass), aHp = Mathf.Clamp01(highpass);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise() - lp) * aLp;
                hpOut = aHp * (hpOut + lp - hpPrev);
                hpPrev = lp;
                float env = Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 2000f);
                b[s0 + i] += hpOut * env * amp;
            }
        }

        static void ChipClack(float[] b, float start, float pitch, float amp)
        {
            AddTone(b, start, 0.12f, 3100f * pitch, amp, 0.03f);
            AddTone(b, start, 0.1f, 4590f * pitch, amp * 0.7f, 0.022f);
            AddTone(b, start, 0.08f, 6290f * pitch, amp * 0.45f, 0.015f);
            AddNoise(b, start, 0.02f, amp * 0.8f, 0.004f, 0.9f, 0.6f);
        }

        /// <summary>
        /// A soft, breathy sung vowel (additive formant synthesis): harmonics of a ~260 Hz voice weighted by two vowel
        /// formants, with a gentle rise and fall in pitch. Played lower for men.
        /// </summary>
        static float[] Syllable(float formant1, float formant2)
        {
            const float dur = 0.11f, f0 = 262f;
            var b = Buffer(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / dur;
                float pitch = f0 * (1f + 0.06f * Mathf.Sin(u * Mathf.PI));
                double phase = pitch * t;
                float v = 0f;
                for (int k = 1; k <= 14; k++)
                {
                    float f = pitch * k;
                    float g1 = Mathf.Exp(-((f - formant1) * (f - formant1)) / (2f * 140f * 140f));
                    float g2 = Mathf.Exp(-((f - formant2) * (f - formant2)) / (2f * 220f * 220f));
                    v += Sine(phase * k) * (g1 + 0.55f * g2 + 0.04f) / k;
                }
                float env = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                env *= env;
                b[i] = (v * 0.5f + Noise() * 0.02f) * env;
            }
            return b;
        }

        static float[] Generate(SfxId id)
        {
            float[] b;
            switch (id)
            {
                case SfxId.Click:
                    b = Buffer(0.06f);
                    AddTone(b, 0, 0.06f, 1500f, 0.45f, 0.012f, 0f, 900f);
                    break;
                case SfxId.Tick:
                    b = Buffer(0.03f);
                    AddTone(b, 0, 0.03f, 2200f, 0.3f, 0.006f);
                    break;
                case SfxId.Whoosh:
                {
                    b = Buffer(0.32f);
                    float lp = 0;
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = i / (float)b.Length;
                        float cutoff = 0.03f + 0.25f * Mathf.Sin(t * Mathf.PI);
                        lp += (Noise() - lp) * cutoff;
                        b[i] = lp * Mathf.Sin(t * Mathf.PI) * 0.5f;
                    }
                    break;
                }
                case SfxId.CardDeal:
                    b = Buffer(0.14f);
                    AddNoise(b, 0, 0.14f, 0.9f, 0.035f, 0.55f, 0.85f);
                    AddNoise(b, 0.09f, 0.04f, 0.5f, 0.008f, 0.9f, 0.7f);
                    break;
                case SfxId.CardFlip:
                    b = Buffer(0.07f);
                    AddNoise(b, 0, 0.07f, 0.8f, 0.012f, 0.8f, 0.8f);
                    AddTone(b, 0, 0.03f, 900f, 0.15f, 0.008f);
                    break;
                case SfxId.Chip:
                    b = Buffer(0.22f);
                    ChipClack(b, 0, 1f, 0.35f);
                    ChipClack(b, 0.045f, 1.06f, 0.18f);
                    break;
                case SfxId.ChipStack:
                    b = Buffer(0.45f);
                    for (int k = 0; k < 5; k++) ChipClack(b, k * 0.065f + (k % 2) * 0.01f, 0.92f + 0.05f * (k % 3), 0.28f - k * 0.03f);
                    break;
                case SfxId.Coins:
                    b = Buffer(0.9f);
                    for (int k = 0; k < 11; k++) ChipClack(b, k * 0.06f, 0.95f + k * 0.035f, 0.26f);
                    AddTone(b, 0.62f, 0.28f, 2637f, 0.12f, 0.12f);
                    break;
                case SfxId.Win:
                {
                    b = Buffer(0.75f);
                    int[] notes = { 72, 76, 79, 84 };
                    for (int k = 0; k < notes.Length; k++)
                    {
                        bool last = k == notes.Length - 1;
                        AddTone(b, k * 0.085f, last ? 0.4f : 0.15f, Note(notes[k]), 0.24f, last ? 0.2f : 0.08f, 0.6f);
                        AddTone(b, k * 0.085f, last ? 0.4f : 0.15f, Note(notes[k] + 12), 0.07f, last ? 0.15f : 0.06f);
                    }
                    break;
                }
                case SfxId.BigWin:
                {
                    b = Buffer(1.6f);
                    int[] notes = { 60, 64, 67, 72, 76, 79, 84, 88 };
                    for (int k = 0; k < notes.Length; k++) AddTone(b, k * 0.06f, 0.2f, Note(notes[k]), 0.18f, 0.08f, 0.5f);
                    foreach (int n in new[] { 72, 76, 79, 84 }) AddTone(b, 0.5f, 1.1f, Note(n), 0.11f, 0.45f, 0.3f);
                    for (int k = 0; k < 14; k++) AddTone(b, 0.5f + k * 0.07f, 0.12f, Note(96 + (k * 5) % 12), 0.04f, 0.04f);
                    break;
                }
                case SfxId.Blackjack:
                {
                    b = Buffer(1.5f);
                    AddTone(b, 0.00f, 0.12f, Note(67), 0.2f, 0.07f, 0.7f);
                    AddTone(b, 0.10f, 0.12f, Note(72), 0.2f, 0.07f, 0.7f);
                    AddTone(b, 0.20f, 0.12f, Note(76), 0.2f, 0.07f, 0.7f);
                    foreach (int n in new[] { 72, 76, 79, 84 }) AddTone(b, 0.32f, 1.1f, Note(n), 0.12f, 0.5f, 0.5f);
                    AddNoise(b, 0.32f, 1f, 0.12f, 0.35f, 0.9f, 0.95f);
                    break;
                }
                case SfxId.Lose:
                    b = Buffer(0.5f);
                    AddTone(b, 0, 0.2f, Note(64), 0.22f, 0.12f, 1f);
                    AddTone(b, 0.17f, 0.33f, Note(60), 0.22f, 0.18f, 1f);
                    break;
                case SfxId.Push:
                    b = Buffer(0.4f);
                    AddTone(b, 0, 0.15f, Note(67), 0.18f, 0.08f, 0.8f);
                    AddTone(b, 0.15f, 0.25f, Note(67), 0.18f, 0.12f, 0.8f);
                    break;
                case SfxId.Bust:
                    b = Buffer(0.55f);
                    AddTone(b, 0, 0.25f, 120f, 0.5f, 0.09f, 0f, 45f);
                    AddTone(b, 0.05f, 0.5f, 330f, 0.12f, 0.2f, 1f, 150f);
                    break;
                case SfxId.Error:
                    b = Buffer(0.3f);
                    AddTone(b, 0, 0.12f, 160f, 0.3f, 0.08f, 1f);
                    AddTone(b, 0.14f, 0.12f, 140f, 0.3f, 0.08f, 1f);
                    break;
                case SfxId.Eat:
                    b = Buffer(0.7f);
                    for (int k = 0; k < 3; k++) AddNoise(b, k * 0.16f, 0.12f, 0.6f, 0.04f, 0.15f, 0.9f);
                    for (int k = 0; k < 3; k++) AddTone(b, 0.48f + k * 0.05f, 0.08f, 300f + k * 120f, 0.15f, 0.03f, 0f, 700f + k * 150f);
                    break;
                case SfxId.Shuffle:
                {
                    b = Buffer(0.75f);
                    float t = 0f;
                    for (int k = 0; t < 0.68f; k++)
                    {
                        AddNoise(b, t, 0.03f, 0.35f, 0.006f, 0.85f, 0.8f);
                        t += Mathf.Lerp(0.04f, 0.012f, t / 0.7f);
                    }
                    break;
                }
                case SfxId.Ding:
                    b = Buffer(1f);
                    AddTone(b, 0, 1f, Note(88), 0.18f, 0.35f);
                    AddTone(b, 0, 1f, Note(95), 0.1f, 0.25f);
                    AddTone(b, 0.08f, 0.9f, Note(100), 0.08f, 0.3f);
                    break;
                case SfxId.VoiceA:
                    b = Syllable(780f, 1220f);
                    break;
                case SfxId.VoiceO:
                    b = Syllable(520f, 900f);
                    break;
                case SfxId.VoiceE:
                    b = Syllable(420f, 2100f);
                    break;
                default:
                    b = Buffer(0.01f);
                    break;
            }
            float peak = 0f;
            foreach (float v in b) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.95f)
                for (int i = 0; i < b.Length; i++) b[i] *= 0.95f / peak;
            return b;
        }
    }
}
