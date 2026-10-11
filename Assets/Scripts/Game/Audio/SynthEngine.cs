using System;
using UnityEngine;

namespace Casino.Audio
{
    /// <summary>The synthesiser itself: pure DSP, no Unity objects, so it can also be rendered offline.</summary>
    public sealed class SynthEngine
    {
        // Am7 - Fmaj7 - Dm7 - E7, one chord per bar.
        static readonly int[][] Chords =
        {
            new[] { 57, 60, 64, 67 },
            new[] { 53, 57, 60, 64 },
            new[] { 53, 57, 60, 62 },
            new[] { 52, 56, 59, 62 },
        };
        static readonly int[] BassRoots = { 45, 41, 38, 40 };
        static readonly int[] WalkingSteps = { 0, 7, 12, 10 }; // semitone offsets for the lounge bass walk
        const int TableSize = 2048;
        static readonly float[] SineTable = BuildSine();

        volatile float targetIntensity;
        volatile float volume = 0.6f;
        volatile bool paused;

        double sampleRate = 44100;
        float intensity;
        double stepPos;     // samples elapsed in the current 16th step
        int step = -1;      // 0..15
        int bar;
        uint rng = 0x9E3779B9;

        // keys (Rhodes-like chord stabs)
        readonly double[] keyPhase = new double[4];
        readonly float[] keyFreq = new float[4];
        float keyEnv;
        // pad
        readonly double[] padPhase = new double[8];
        float padLp, padLpR;
        // bass
        double bassPhase;
        float bassFreq = 110f, bassEnv, bassLp;
        // arp
        double arpPhase;
        float arpFreq = 440f, arpEnv, arpLp;
        int arpIndex;
        // drums
        double kickPhase;
        float kickEnv, kickPitch;
        float snareEnv, snareTone;
        double snarePhase;
        float hatEnv, hatDecay = 0.03f, hatPrev, hatHp;
        float duck = 1f;

        readonly float[] padFreq = new float[4];

        public SynthEngine(double sampleRate)
        {
            this.sampleRate = sampleRate > 0 ? sampleRate : 44100;
            SetPadChord(0);
        }

        /// <summary>0 = calm lounge, 1 = maximum adrenaline. Smoothed on the audio thread.</summary>
        public float Intensity { set => targetIntensity = Mathf.Clamp01(value); }
        public float Volume { set => volume = Mathf.Clamp01(value); }
        public bool Paused { set => paused = value; }

        /// <summary>Current smoothed intensity (for tests / debugging).</summary>
        public float CurrentIntensity => intensity;

        void SetPadChord(int index)
        {
            int[] notes = Chords[index];
            for (int k = 0; k < 4; k++) padFreq[k] = Midi(notes[k] + 12);
        }

        static float[] BuildSine()
        {
            var t = new float[TableSize + 1];
            for (int i = 0; i <= TableSize; i++) t[i] = (float)Math.Sin(i * 2.0 * Math.PI / TableSize);
            return t;
        }

        static float Sin(double phase)
        {
            double p = (phase - Math.Floor(phase)) * TableSize;
            int i = (int)p;
            float f = (float)(p - i);
            return SineTable[i] + (SineTable[i + 1] - SineTable[i]) * f;
        }

        static float PolyBlep(double t, double dt)
        {
            if (t < dt)
            {
                t /= dt;
                return (float)(t + t - t * t - 1.0);
            }
            if (t > 1.0 - dt)
            {
                t = (t - 1.0) / dt;
                return (float)(t * t + t + t + 1.0);
            }
            return 0f;
        }

        static float Saw(double phase, double inc)
        {
            double t = phase - Math.Floor(phase);
            return (float)(2.0 * t - 1.0) - PolyBlep(t, inc);
        }

        static float Square(double phase, double inc)
        {
            double t = phase - Math.Floor(phase);
            double t2 = t + 0.5;
            t2 -= Math.Floor(t2);
            float v = t < 0.5 ? 1f : -1f;
            return v + PolyBlep(t, inc) - PolyBlep(t2, inc);
        }

        static float Midi(int note) => 440f * (float)Math.Pow(2.0, (note - 69) / 12.0);

        float Noise()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng & 0xFFFFFF) / (float)0x7FFFFF - 1f;
        }

        public void Render(float[] data, int channels)
        {
            if (paused)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            int frames = data.Length / channels;
            float bufferSeconds = frames / (float)sampleRate;
            intensity += (targetIntensity - intensity) * (1f - (float)Math.Exp(-bufferSeconds / 1.2f));
            float I = intensity;
            float vol = volume;

            double bpm = 92.0 + 50.0 * Math.Pow(I, 1.2);
            double baseStep = sampleRate * 60.0 / bpm / 4.0;
            float swing = 0.16f * (1f - Mathf.Clamp01(I * 1.6f));
            float inv = 1f / (float)sampleRate;

            // Layer gains from intensity.
            float keysGain = Mathf.Lerp(0.32f, 0.12f, I);
            float padGain = Mathf.Lerp(0.035f, 0.11f, I);
            float padCut = Mathf.Lerp(0.025f, 0.18f, I);
            float bassGain = Mathf.Lerp(0.42f, 0.5f, I);
            float arpGain = Mathf.Clamp01((I - 0.5f) / 0.5f) * 0.11f;
            float drive = 1f + I * 2.5f;
            float driveNorm = 1f / (float)Math.Tanh(drive);

            for (int f = 0; f < frames; f++)
            {
                double stepLen = baseStep * ((step & 1) == 0 ? 1.0 + swing : 1.0 - swing);
                if (step < 0 || stepPos >= stepLen)
                {
                    stepPos = step < 0 ? 0 : stepPos - stepLen;
                    step++;
                    if (step >= 16)
                    {
                        step = 0;
                        bar = (bar + 1) & 3;
                        SetPadChord(bar);
                    }
                    OnStep(I);
                }
                stepPos += 1.0;

                // keys
                float keys = 0f;
                if (keyEnv > 0.0005f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        keyPhase[k] += keyFreq[k] * inv;
                        keys += Sin(keyPhase[k]) + 0.25f * Sin(keyPhase[k] * 2.0);
                    }
                    keys *= keyEnv * 0.25f;
                    keyEnv *= 1f - 2.6f * inv;
                }

                // pad: two detuned saws per chord note, low-passed, stereo spread
                float padL = 0f, padR = 0f;
                if (padGain > 0.001f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        float fr = padFreq[k];
                        double incA = fr * 1.003f * inv, incB = fr * 0.997f * inv;
                        padPhase[k * 2] += incA;
                        padPhase[k * 2 + 1] += incB;
                        padL += Saw(padPhase[k * 2], incA);
                        padR += Saw(padPhase[k * 2 + 1], incB);
                    }
                    padLp += (padL * 0.25f - padLp) * padCut;
                    padLpR += (padR * 0.25f - padLpR) * padCut;
                    padL = padLp * padGain * duck;
                    padR = padLpR * padGain * duck;
                }

                // bass
                double bInc = bassFreq * inv;
                bassPhase += bInc;
                float bassRaw = I < 0.35f
                    ? Sin(bassPhase) * 0.8f + 0.35f * (1f - 4f * Math.Abs((float)(bassPhase - Math.Floor(bassPhase)) - 0.5f))
                    : Mathf.Lerp(Sin(bassPhase), Saw(bassPhase, bInc), Mathf.Clamp01((I - 0.35f) * 2f));
                bassLp += (bassRaw - bassLp) * Mathf.Lerp(0.08f, 0.3f, I);
                float bass = (float)Math.Tanh(bassLp * drive) * driveNorm * bassEnv * bassGain * duck;
                bassEnv *= 1f - (I < 0.35f ? 2.5f : 7f) * inv;

                // arp
                float arp = 0f;
                if (arpGain > 0f && arpEnv > 0.0005f)
                {
                    double aInc = arpFreq * inv;
                    arpPhase += aInc;
                    arpLp += (Square(arpPhase, aInc) - arpLp) * 0.22f;
                    arp = arpLp * arpEnv * arpGain;
                    arpEnv *= 1f - 9f * inv;
                }

                // kick
                float kick = 0f;
                if (kickEnv > 0.0005f)
                {
                    kickPitch = 45f + (kickPitch - 45f) * (1f - 30f * inv);
                    kickPhase += kickPitch * inv;
                    kick = Sin(kickPhase) * kickEnv * 0.9f;
                    kickEnv *= 1f - 9f * inv;
                }
                duck += (1f - duck) * 6f * inv;

                // snare / clap
                float snare = 0f;
                if (snareEnv > 0.0005f)
                {
                    snarePhase += 185.0 * inv;
                    snare = (Noise() * 0.8f + Sin(snarePhase) * snareTone) * snareEnv * 0.35f;
                    snareEnv *= 1f - 14f * inv;
                    snareTone *= 1f - 30f * inv;
                }

                // hats: high-passed noise
                float hat = 0f;
                if (hatEnv > 0.0005f)
                {
                    float n = Noise();
                    hatHp = 0.92f * (hatHp + n - hatPrev);
                    hatPrev = n;
                    hat = hatHp * hatEnv * 0.16f;
                    hatEnv *= 1f - inv / hatDecay;
                }

                float mono = keys * keysGain + bass + kick + snare;
                float l = mono + padL + hat * 0.7f + arp * 0.8f;
                float r = mono + padR + hat * 1.0f + arp * 1.2f;
                l = l / (1f + Math.Abs(l)) * vol;
                r = r / (1f + Math.Abs(r)) * vol;

                int o = f * channels;
                if (channels == 1) data[o] = (l + r) * 0.5f;
                else
                {
                    data[o] = l;
                    data[o + 1] = r;
                    for (int c = 2; c < channels; c++) data[o + c] = 0f;
                }
            }
        }

        void OnStep(float I)
        {
            int[] chord = Chords[bar];
            int root = BassRoots[bar];

            // Keys: lounge comping, thinning out as the track gets harder.
            bool keyHit = I < 0.6f ? (step == 0 || step == 6 || step == 10) : (step == 0 || step == 8);
            if (keyHit)
            {
                for (int k = 0; k < 4; k++) keyFreq[k] = Midi(chord[k]);
                keyEnv = step == 0 ? 1f : 0.65f;
            }

            // Bass
            if (I < 0.35f)
            {
                if (step % 4 == 0)
                {
                    bassFreq = Midi(root + WalkingSteps[step / 4]);
                    bassEnv = 1f;
                }
            }
            else if (I < 0.7f)
            {
                if ((step & 1) == 0)
                {
                    bassFreq = Midi(root + ((step & 4) != 0 ? 12 : 0));
                    bassEnv = 1f;
                }
            }
            else
            {
                bassFreq = Midi(root + (step % 4 == 2 ? 12 : 0));
                bassEnv = 1f;
            }

            // Kick
            bool kickHit = I > 0.55f ? step % 4 == 0 || (I > 0.85f && step == 14)
                : I > 0.2f && (step == 0 || step == 8 || (I > 0.4f && step == 10));
            if (kickHit)
            {
                kickEnv = 1f;
                kickPitch = 150f;
                if (I > 0.6f) duck = 0.35f;
            }

            // Snare / clap
            bool fill = I > 0.7f && bar == 3 && step >= 12;
            if ((I > 0.35f && (step == 4 || step == 12)) || fill || (I > 0.8f && step == 15))
            {
                snareEnv = fill && step != 12 ? 0.6f : 1f;
                snareTone = 0.6f;
            }

            // Hats
            bool hatHit = I > 0.5f ? true : I > 0.06f && (step & 1) == 0;
            if (hatHit)
            {
                bool open = I > 0.75f && (step % 4 == 2);
                hatEnv = open ? 0.9f : ((step % 4 == 0) ? 0.8f : 0.5f);
                hatDecay = open ? 0.12f : 0.025f;
            }

            // Arpeggio
            if (I > 0.5f)
            {
                int note = chord[arpIndex % 4] + 12 + (I > 0.85f && (arpIndex / 4) % 2 == 1 ? 12 : 0);
                arpFreq = Midi(note);
                arpEnv = 1f;
                arpIndex++;
            }
        }
    }
}
