using UnityEngine;

namespace Shieldbreak.Presentation.Feedback
{
    /// <summary>
    /// Placeholder sounds synthesized at startup (no audio assets before M4, CLAUDE.md 7): whoosh, impacts for flesh,
    /// wood and steel, grunt, windup cues per direction, step, weapon draw. Deterministic noise, so every run sounds
    /// the same. Replace with recorded layers in M5 (Sound).
    /// </summary>
    public static class ProceduralSfx
    {
        public const int Rate = 44100;

        /// <summary>Seeded white noise in [-1, 1].</summary>
        struct Noise
        {
            uint state;
            public Noise(uint seed) => state = seed == 0 ? 1u : seed;
            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0xFFFFFF) / (float)0x7FFFFF - 1f;
            }
        }

        /// <summary>Two-pole resonant band-pass (RBJ biquad), enough for formants and coloured noise.</summary>
        struct BandPass
        {
            float b0, b2, a1, a2, x1, x2, y1, y2;
            public BandPass(float freq, float q) { b0 = b2 = a1 = a2 = x1 = x2 = y1 = y2 = 0f; Set(freq, q); }
            public void Set(float freq, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(freq, 20f, Rate * 0.45f) / Rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float a0 = 1f + alpha;
                b0 = alpha / a0;
                b2 = -alpha / a0;
                a1 = -2f * Mathf.Cos(w) / a0;
                a2 = (1f - alpha) / a0;
            }
            public float Process(float x)
            {
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static AudioClip Clip(string name, float seconds, System.Func<int, float, float> sample)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            float peak = 1e-6f;
            for (int i = 0; i < n; i++)
            {
                data[i] = sample(i, i / (float)Rate);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // Normalize to -3 dB and fade the last 5 ms to avoid clicks
            int fade = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < n; i++)
            {
                data[i] *= 0.7f / peak;
                if (i >= n - fade) data[i] *= (n - i) / (float)fade;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Air rushing past a blade: band-passed noise sweeping up then down.</summary>
        public static AudioClip Whoosh(float seconds = 0.26f, float centre = 900f)
        {
            var noise = new Noise(11);
            var band = new BandPass(centre, 1.2f);
            return Clip("sfx_whoosh", seconds, (i, t) =>
            {
                float u = t / seconds;
                band.Set(centre * (0.6f + 0.9f * Mathf.Sin(Mathf.PI * u)), 1.4f);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
                return band.Process(noise.Next()) * env;
            });
        }

        /// <summary>Blade into a body: low thump plus a short wet noise burst.</summary>
        public static AudioClip Flesh()
        {
            var noise = new Noise(23);
            var band = new BandPass(500f, 0.9f);
            return Clip("sfx_flesh", 0.22f, (i, t) =>
            {
                float thump = Mathf.Sin(2f * Mathf.PI * (75f - 40f * t) * t) * Mathf.Exp(-t * 22f);
                float splat = band.Process(noise.Next()) * Mathf.Exp(-t * 35f);
                return thump * 0.9f + splat * 1.6f;
            });
        }

        /// <summary>Blade on a wooden shield: hollow knock.</summary>
        public static AudioClip Wood()
        {
            var noise = new Noise(37);
            var band = new BandPass(750f, 2.5f);
            return Clip("sfx_wood", 0.25f, (i, t) =>
            {
                float knock = Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-t * 28f);
                float body = band.Process(noise.Next()) * Mathf.Exp(-t * 18f);
                return knock * 0.8f + body * 2.2f;
            });
        }

        /// <summary>Steel on steel: inharmonic partials with a long ring.</summary>
        public static AudioClip Steel()
        {
            var noise = new Noise(41);
            float[] partials = { 920f, 1460f, 2190f, 3050f, 4120f };
            return Clip("sfx_steel", 0.6f, (i, t) =>
            {
                float s = 0f;
                for (int p = 0; p < partials.Length; p++)
                    s += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * (5f + 3f * p)) / (1f + p * 0.6f);
                return s + noise.Next() * Mathf.Exp(-t * 90f) * 0.6f;
            });
        }

        /// <summary>Short vocal grunt: buzzy source through two vowel formants.</summary>
        public static AudioClip Grunt(float pitch = 120f, float seconds = 0.2f, uint seed = 53)
        {
            var noise = new Noise(seed);
            var f1 = new BandPass(650f, 4f);
            var f2 = new BandPass(1100f, 5f);
            float phase = 0f;
            return Clip("sfx_grunt", seconds, (i, t) =>
            {
                float u = t / seconds;
                float f = pitch * (1.1f - 0.25f * u);
                phase += f / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f + noise.Next() * 0.25f;
                float env = Mathf.Min(1f, t * 60f) * Mathf.Exp(-u * 3f);
                return (f1.Process(saw) + 0.6f * f2.Process(saw)) * env;
            });
        }

        /// <summary>Readable windup cue: a soft tone gliding from <paramref name="from"/> to <paramref name="to"/> Hz.</summary>
        public static AudioClip Cue(string name, float from, float to, float seconds)
        {
            float phase = 0f;
            return Clip(name, seconds, (i, t) =>
            {
                float u = t / seconds;
                phase += Mathf.Lerp(from, to, u) / Rate;
                float env = Mathf.Sin(Mathf.PI * u);
                return (Mathf.Sin(2f * Mathf.PI * phase) + 0.3f * Mathf.Sin(4f * Mathf.PI * phase)) * env;
            });
        }

        /// <summary>Feet on grass: soft low noise.</summary>
        public static AudioClip Step()
        {
            var noise = new Noise(67);
            var band = new BandPass(300f, 0.8f);
            return Clip("sfx_step", 0.14f, (i, t) => band.Process(noise.Next()) * Mathf.Exp(-t * 25f));
        }

        /// <summary>Drawing a blade: bright scrape.</summary>
        public static AudioClip Draw()
        {
            var noise = new Noise(71);
            var band = new BandPass(3200f, 3f);
            return Clip("sfx_draw", 0.32f, (i, t) =>
            {
                float u = t / 0.32f;
                return band.Process(noise.Next()) * Mathf.Sin(Mathf.PI * u) + Mathf.Sin(2f * Mathf.PI * 3400f * t) * Mathf.Exp(-t * 12f) * 0.2f;
            });
        }
    }
}
