using System;
using UnityEngine;

namespace Casino.Audio
{
    /// <summary>
    /// Real-time adaptive casino music. At intensity 0 it is a slow swung lounge groove (Rhodes stabs, walking bass,
    /// brushed hats); as intensity rises the tempo climbs from 92 to 142 BPM and it adds a four-on-the-floor kick,
    /// claps, 16th hats, a driving saw bass, a bright pad and a racing arpeggio. Runs on the audio thread, no allocations.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SynthMusic : MonoBehaviour
    {
        SynthEngine engine;

        public float Intensity { set => engine.Intensity = value; }
        public float Volume { set => engine.Volume = value; }
        public bool Paused { set => engine.Paused = value; }

        void Awake()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100; // 0 without an audio device
            engine = new SynthEngine(rate);
            var src = GetComponent<AudioSource>();
            src.clip = AudioClip.Create("synth_carrier", rate, 1, rate, false);
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.volume = 1f;
            src.Play();
        }

        void OnAudioFilterRead(float[] data, int channels) => engine.Render(data, channels);
    }

    /// <summary>
    /// Optional real music: put loops named intensity_0 .. intensity_N in Resources/Music (same tempo and length).
    /// They play in sync and crossfade by intensity, replacing the synth.
    /// </summary>
    public sealed class StemMusic : MonoBehaviour
    {
        AudioSource[] sources;
        float intensity, volume = 0.6f;

        public static StemMusic TryCreate(GameObject host)
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            for (int i = 0; i < 8; i++)
            {
                var clip = Resources.Load<AudioClip>("Music/intensity_" + i);
                if (clip == null) break;
                clips.Add(clip);
            }
            if (clips.Count < 2) return null;
            var stems = host.AddComponent<StemMusic>();
            stems.sources = new AudioSource[clips.Count];
            double start = AudioSettings.dspTime + 0.2;
            for (int i = 0; i < clips.Count; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.clip = clips[i];
                s.loop = true;
                s.volume = 0f;
                s.spatialBlend = 0f;
                s.PlayScheduled(start);
                stems.sources[i] = s;
            }
            return stems;
        }

        public float Intensity { set => intensity = Mathf.Clamp01(value); }
        public float Volume { set => volume = Mathf.Clamp01(value); }

        void Update()
        {
            float x = intensity * (sources.Length - 1);
            for (int i = 0; i < sources.Length; i++)
            {
                float w = Mathf.Clamp01(1f - Mathf.Abs(x - i));
                sources[i].volume = Mathf.Sqrt(w) * volume;
            }
        }
    }

    /// <summary>Front end for whichever music source is in use.</summary>
    public sealed class MusicPlayer
    {
        readonly SynthMusic synth;
        readonly StemMusic stems;

        public MusicPlayer(Transform parent)
        {
            // Own GameObject: OnAudioFilterRead attaches to the AudioSource on the same object.
            var host = new GameObject("Music");
            host.transform.SetParent(parent, false);
            stems = StemMusic.TryCreate(host);
            if (stems == null)
            {
                host.AddComponent<AudioSource>();
                synth = host.AddComponent<SynthMusic>();
            }
        }

        public void SetIntensity(float value)
        {
            if (synth != null) synth.Intensity = value;
            if (stems != null) stems.Intensity = value;
        }

        public void SetVolume(float value)
        {
            if (synth != null) synth.Volume = value;
            if (stems != null) stems.Volume = value;
        }

        public void SetPaused(bool value)
        {
            if (synth != null) synth.Paused = value;
            AudioListener.pause = value;
        }
    }
}
