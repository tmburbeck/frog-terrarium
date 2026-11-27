using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>The sounds the game makes, apart from frog calls (which come from each species' voice).</summary>
    public enum SoundKind : byte
    {
        Splash = 0,
        Cricket = 1,
        Gulp = 2,
        Slide = 3,
        Tunnel = 4,
        Lift = 5,
        Teleport = 6,
        Stairs = 7,
        Plop = 8,
    }

    /// <summary>
    /// Makes every sound in the game from maths instead of recordings, so there are no licences to
    /// track and each species (and each hybrid) gets its own call. A sound is a list of samples from
    /// -1 to 1; the game turns it into an audio clip once at start-up.
    ///
    /// This is presentation, not simulation: its little random number generator is its own and
    /// never touches World.Random, so sounds cannot change what happens in the tank.
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 22050;
        public static readonly int KindCount = Enum.GetValues(typeof(SoundKind)).Length;

        /// <summary>How long one call of this species lasts, in seconds.</summary>
        public static float CallLength(FrogSpecies sp) => Math.Max(1, sp.CallPulses) / Math.Max(0.5f, sp.CallRate) + 0.05f;

        /// <summary>
        /// A species' call: <see cref="FrogSpecies.CallPulses"/> notes at <see cref="FrogSpecies.CallRate"/>
        /// a second. Each note is a tone at <see cref="FrogSpecies.CallPitch"/> that drops a little as it
        /// ends; buzzy voices are rattled by a fast pulse (the "rr" in a croak), pure voices stay smooth.
        /// </summary>
        public static float[] Call(FrogSpecies sp, uint seed = 1)
        {
            float length = CallLength(sp);
            var s = new float[(int)(length * SampleRate)];
            var rng = new Noise(seed);
            int pulses = Math.Max(1, sp.CallPulses);
            float gap = 1f / Math.Max(0.5f, sp.CallRate);
            float note = Math.Min(gap * 0.7f, 0.28f);
            float buzz = 1f - Clamp01(sp.CallPurity);
            float rasp = 70f + sp.CallPitch * 0.08f;  // rattles per second in a buzzy croak
            double phase = 0, raspPhase = 0;
            for (int n = 0; n < s.Length; n++)
            {
                float t = n / (float)SampleRate;
                int k = (int)(t / gap);
                float local = t - k * gap;
                if (k >= pulses || local > note) { phase = 0; continue; }
                float u = local / note;
                float pitch = sp.CallPitch * (1.04f - 0.1f * u);
                phase += 2 * Math.PI * pitch / SampleRate;
                raspPhase += 2 * Math.PI * rasp / SampleRate;
                // A few harmonics give the croak its body; buzzy voices get more of them.
                float tone = (float)(Math.Sin(phase) + buzz * (0.6 * Math.Sin(2 * phase) + 0.35 * Math.Sin(3 * phase) + 0.2 * Math.Sin(5 * phase)));
                float rattle = 1f - buzz * 0.85f * (float)(0.5 + 0.5 * Math.Sin(raspPhase));
                float env = Envelope(u, 0.08f, 0.35f);
                s[n] = tone * rattle * env + buzz * 0.04f * env * rng.Next();
            }
            Normalise(s, 0.9f);
            return s;
        }

        /// <summary>One of the other sounds: splashes, crickets, gulps and the playground pieces.</summary>
        public static float[] Make(SoundKind kind, uint seed = 1)
        {
            var rng = new Noise(seed + (uint)kind * 7919u);
            switch (kind)
            {
                case SoundKind.Splash:
                {
                    // A burst of bubbly noise that dies away, low-passed so it sounds wet, not hissy.
                    var s = Buffer(0.45f);
                    float lp = 0f;
                    for (int n = 0; n < s.Length; n++)
                    {
                        float t = n / (float)SampleRate;
                        float env = (float)Math.Exp(-t * 9f) * Math.Min(1f, t * 300f);
                        lp += 0.25f * (rng.Next() - lp);
                        float bubble = (float)Math.Sin(2 * Math.PI * (500 + 900 * Math.Exp(-t * 20)) * t) * (float)Math.Exp(-t * 25f);
                        s[n] = lp * env * 2f + 0.3f * bubble;
                    }
                    return Normalise(s, 0.8f);
                }
                case SoundKind.Plop:
                {
                    // A small, quick drop of a pitch: an egg or a froglet going in.
                    var s = Buffer(0.12f);
                    double ph = 0;
                    for (int n = 0; n < s.Length; n++)
                    {
                        float t = n / (float)SampleRate;
                        ph += 2 * Math.PI * (1400 - 6000 * t) / SampleRate;
                        s[n] = (float)Math.Sin(ph) * (float)Math.Exp(-t * 30f);
                    }
                    return Normalise(s, 0.6f);
                }
                case SoundKind.Cricket:
                {
                    // Three quick chirps of a high, pure note (crickets rub their wings, not sing).
                    var s = Buffer(0.5f);
                    for (int n = 0; n < s.Length; n++)
                    {
                        float t = n / (float)SampleRate;
                        float chirp = t % 0.13f;
                        if (t > 0.39f || chirp > 0.06f) continue;
                        float env = (float)Math.Sin(Math.PI * chirp / 0.06f);
                        float am = 0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * 30 * t);
                        s[n] = (float)Math.Sin(2 * Math.PI * 4600 * t) * env * am;
                    }
                    return Normalise(s, 0.5f);
                }
                case SoundKind.Gulp:
                {
                    // The tongue: a quick "thwip" (a fast drop in pitch) then a soft click.
                    var s = Buffer(0.14f);
                    double ph = 0;
                    for (int n = 0; n < s.Length; n++)
                    {
                        float t = n / (float)SampleRate;
                        ph += 2 * Math.PI * (2200 * Math.Exp(-t * 35) + 180) / SampleRate;
                        s[n] = (float)Math.Sin(ph) * (float)Math.Exp(-t * 28f) + (t > 0.09f && t < 0.095f ? 0.4f * rng.Next() : 0f);
                    }
                    return Normalise(s, 0.6f);
                }
                case SoundKind.Slide:
                case SoundKind.Tunnel:
                {
                    // A whoosh: noise through a filter that sweeps up then down. The tunnel is longer and breathier.
                    bool tunnel = kind == SoundKind.Tunnel;
                    var s = Buffer(tunnel ? 1.6f : 0.9f);
                    float lp = 0f, bp = 0f;
                    for (int n = 0; n < s.Length; n++)
                    {
                        float u = n / (float)s.Length;
                        float cut = 0.03f + 0.25f * (float)Math.Sin(Math.PI * u);
                        lp += cut * (rng.Next() - lp);
                        bp += cut * (lp - bp);
                        s[n] = (lp - bp) * (float)Math.Sin(Math.PI * u) * (tunnel ? 1f : 1.3f);
                    }
                    return Normalise(s, tunnel ? 0.5f : 0.6f);
                }
                case SoundKind.Lift:
                {
                    // A soft motor hum with a little "ding" at the start.
                    var s = Buffer(1.2f);
                    for (int n = 0; n < s.Length; n++)
                    {
                        float t = n / (float)SampleRate;
                        float hum = (float)(Math.Sin(2 * Math.PI * 110 * t) + 0.5 * Math.Sin(2 * Math.PI * 220 * t) + 0.2 * Math.Sin(2 * Math.PI * 330 * t));
                        float env = Envelope(t / 1.2f, 0.1f, 0.2f);
                        float ding = (float)Math.Sin(2 * Math.PI * 1320 * t) * (float)Math.Exp(-t * 8f);
                        s[n] = 0.3f * hum * env + 0.5f * ding;
                    }
                    return Normalise(s, 0.45f);
                }
                case SoundKind.Teleport:
                {
                    // A sparkly sweep up, then a pop.
                    var s = Buffer(0.6f);
                    double ph = 0;
                    for (int n = 0; n < s.Length; n++)
                    {
                        float t = n / (float)SampleRate;
                        ph += 2 * Math.PI * (300 + 3000 * t * t / 0.36) / SampleRate;
                        float env = t < 0.5f ? t / 0.5f : (float)Math.Exp(-(t - 0.5f) * 60f);
                        float sparkle = 0.6f + 0.4f * (float)Math.Sin(2 * Math.PI * 25 * t);
                        s[n] = (float)Math.Sin(ph) * env * sparkle + (t > 0.5f ? 0.5f * rng.Next() * env : 0f);
                    }
                    return Normalise(s, 0.55f);
                }
                case SoundKind.Stairs:
                {
                    // Little wet footsteps: soft taps, six of them.
                    var s = Buffer(1.1f);
                    for (int n = 0; n < s.Length; n++)
                    {
                        float t = n / (float)SampleRate;
                        float step = t % 0.18f;
                        s[n] = (float)Math.Sin(2 * Math.PI * 260 * step) * (float)Math.Exp(-step * 60f) + 0.15f * rng.Next() * (float)Math.Exp(-step * 120f);
                    }
                    return Normalise(s, 0.4f);
                }
            }
            return Buffer(0.1f);
        }

        /// <summary>
        /// The pitch of the loudest steady tone in a sound, found by counting how often the wave crosses
        /// zero. Rough, but enough for tests to tell a deep croak from a high chirp.
        /// </summary>
        public static float RoughPitch(float[] s)
        {
            int crossings = 0, active = 0;
            for (int n = 1; n < s.Length; n++)
            {
                if (Math.Abs(s[n]) < 0.02f && Math.Abs(s[n - 1]) < 0.02f) continue;
                active++;
                if ((s[n - 1] < 0f) != (s[n] < 0f)) crossings++;
            }
            return active == 0 ? 0f : crossings * 0.5f * SampleRate / active;
        }

        private static float[] Buffer(float seconds) => new float[(int)(seconds * SampleRate)];

        /// <summary>Rises over the first part, holds, and falls away over the last part (u from 0 to 1).</summary>
        private static float Envelope(float u, float attack, float release)
        {
            if (u < attack) return u / attack;
            if (u > 1f - release) return Math.Max(0f, (1f - u) / release);
            return 1f;
        }

        private static float[] Normalise(float[] s, float peak)
        {
            float max = 0f;
            foreach (float v in s) max = Math.Max(max, Math.Abs(v));
            if (max > 1e-6f)
                for (int n = 0; n < s.Length; n++) s[n] *= peak / max;
            return s;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>A tiny repeatable noise maker (xorshift), so a sound is the same every time the game starts.</summary>
        private struct Noise
        {
            private uint _state;
            public Noise(uint seed) { _state = seed == 0 ? 2463534242u : seed; }
            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / (float)0x800000 - 1f;
            }
        }
    }
}
