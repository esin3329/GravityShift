using UnityEngine;
using Object = UnityEngine.Object;

namespace GravityShift
{
    internal enum SfxKind
    {
        Hit, Hurt, Click, Pulse,
        Clear, Dash, Death, ExitOpen, FieldPlace, FieldRemove, FieldReverseAll,
        Grab, GravityFlip, Impact, Jump, Land, RotationWarning, Swing, Throw
    }

    internal sealed class Sfx : System.IDisposable
    {
        private readonly AudioAssets assets;
        private readonly AudioSource voice;
        private readonly AudioSource music;
        private readonly AudioClip hit;
        private readonly AudioClip hurt;
        private readonly AudioClip click;
        private readonly AudioClip pulse;
        private int currentZone = -1;

        public Sfx(GameObject host, AudioAssets assets)
        {
            if (assets == null) throw new System.ArgumentNullException(nameof(assets), "Assign AudioAssets on GravityShiftGame.");
            assets.RequireClips();
            this.assets = assets;
            voice = CreateSource(host);
            music = CreateSource(host);
            music.loop = true;
            music.volume = 0.35f;
            hit = Sweep("Hit", 880f, 1320f, 0.09f, false);
            hurt = Sweep("Hurt", 220f, 140f, 0.12f, false);
            click = Noise("Click", 0.02f);
            pulse = Sweep("Pulse", 180f, 70f, 0.16f, true);
        }

        public void SetZone(int zone)
        {
            if (zone == currentZone) return;
            AudioClip clip = assets.MusicFor(zone);
            music.Stop();
            music.clip = clip;
            music.Play();
            currentZone = zone;
        }

        public void Play(SfxKind kind)
        {
            AudioClip clip = kind switch
            {
                SfxKind.Hit => hit,
                SfxKind.Hurt => hurt,
                SfxKind.Click => click,
                SfxKind.Pulse => pulse,
                _ => assets.ClipFor(kind)
            };
            voice.pitch = kind == SfxKind.Hit ? UnityEngine.Random.Range(0.92f, 1.08f) : 1f;
            voice.PlayOneShot(clip, kind == SfxKind.Click ? 0.35f : 0.55f);
        }

        public void Dispose()
        {
            ReleaseSource(voice);
            ReleaseSource(music);
            Object.Destroy(hit);
            Object.Destroy(hurt);
            Object.Destroy(click);
            Object.Destroy(pulse);
        }

        private static AudioSource CreateSource(GameObject host)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static void ReleaseSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            Object.Destroy(source);
        }

        private static AudioClip Sweep(string name, float from, float to, float duration, bool square)
        {
            const int rate = 44100;
            int count = Mathf.Max(2, Mathf.RoundToInt(rate * duration));
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)rate;
                float freq = Mathf.Lerp(from, to, i / (float)(count - 1));
                phase += 2f * Mathf.PI * freq / rate;
                float wave = square ? (Mathf.Sin(phase) >= 0f ? 1f : -1f) : Mathf.Sin(phase);
                data[i] = wave * Envelope(time, duration) * 0.35f;
            }
            return Clip(name, data, rate);
        }

        private static AudioClip Noise(string name, float duration)
        {
            const int rate = 44100;
            int count = Mathf.Max(2, Mathf.RoundToInt(rate * duration));
            var data = new float[count];
            var random = new System.Random(3);
            for (int i = 0; i < count; i++)
                data[i] = ((float)random.NextDouble() * 2f - 1f) * Envelope(i / (float)rate, duration) * 0.25f;
            return Clip(name, data, rate);
        }

        private static float Envelope(float time, float duration)
        {
            const float fade = 0.005f;
            return Mathf.Clamp01(Mathf.Min(time / fade, (duration - time) / fade));
        }

        private static AudioClip Clip(string name, float[] data, int rate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
