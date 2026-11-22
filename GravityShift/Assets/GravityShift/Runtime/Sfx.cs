using UnityEngine;
using Object = UnityEngine.Object;

namespace GravityShift
{
    internal enum SfxKind { Hit, Hurt, Click, Sting, Pulse }

    internal sealed class Sfx : System.IDisposable
    {
        private readonly AudioSource voice;
        private readonly AudioSource bed;
        private readonly AudioClip hit;
        private readonly AudioClip hurt;
        private readonly AudioClip click;
        private readonly AudioClip sting;
        private readonly AudioClip pulse;
        private readonly AudioClip room;

        public Sfx(GameObject host)
        {
            voice = host.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            bed = host.AddComponent<AudioSource>();
            bed.playOnAwake = false;
            bed.spatialBlend = 0f;
            bed.loop = true;
            bed.volume = 0.035f;
            hit = Sweep("Hit", 880f, 1320f, 0.09f, false);
            hurt = Sweep("Hurt", 220f, 140f, 0.12f, false);
            click = Noise("Click", 0.02f);
            sting = Sting();
            pulse = Sweep("Pulse", 180f, 70f, 0.16f, true);
            room = Brown("Room Tone", 6f);
            bed.clip = room;
            bed.Play();
        }

        public void Play(SfxKind kind)
        {
            AudioClip clip = kind == SfxKind.Hit ? hit : kind == SfxKind.Hurt ? hurt :
                kind == SfxKind.Click ? click : kind == SfxKind.Sting ? sting : pulse;
            voice.pitch = kind == SfxKind.Hit ? UnityEngine.Random.Range(0.92f, 1.08f) : 1f;
            voice.PlayOneShot(clip, kind == SfxKind.Click ? 0.35f : 0.55f);
        }

        public void Dispose()
        {
            if (bed != null) bed.Stop();
            foreach (AudioClip clip in new[] { hit, hurt, click, sting, pulse, room })
                if (clip != null) Object.Destroy(clip);
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

        private static AudioClip Sting()
        {
            const int rate = 44100;
            const float note = 0.12f;
            int noteCount = Mathf.RoundToInt(rate * note);
            float[] pitches = { 523f, 659f, 784f };
            var data = new float[noteCount * pitches.Length];
            for (int n = 0; n < pitches.Length; n++)
            {
                float phase = 0f;
                for (int i = 0; i < noteCount; i++)
                {
                    float time = i / (float)rate;
                    phase += 2f * Mathf.PI * pitches[n] / rate;
                    data[n * noteCount + i] = Mathf.Sin(phase) * Envelope(time, note) * 0.3f;
                }
            }
            return Clip("Sting", data, rate);
        }

        private static AudioClip Brown(string name, float duration)
        {
            const int rate = 44100;
            int count = Mathf.Max(2, Mathf.RoundToInt(rate * duration));
            var data = new float[count];
            var random = new System.Random(7);
            float hold = 0f;
            for (int i = 0; i < count; i++)
            {
                hold = Mathf.Clamp(hold + ((float)random.NextDouble() * 2f - 1f) * 0.04f, -1f, 1f);
                float time = i / (float)rate;
                float edge = Mathf.Clamp01(Mathf.Min(time / 0.05f, (duration - time) / 0.05f));
                data[i] = hold * edge * 0.2f;
            }
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
