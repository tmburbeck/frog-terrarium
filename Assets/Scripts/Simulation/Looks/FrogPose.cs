using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// How a frog's body is held at one moment: how far its back legs are stretched (0 folded up
    /// sitting, 1 straight out behind), how far the front legs reach, how high it is off the ground
    /// and how its body tips. The game turns this into leg bones every frame.
    /// </summary>
    public struct FrogPose
    {
        /// <summary>0 = back legs folded under the body (sitting), 1 = stretched out straight behind.</summary>
        public float Stretch;
        /// <summary>0 = front legs propping the body up, 1 = reaching forward to land.</summary>
        public float Reach;
        /// <summary>Height above the ground, in frog lengths.</summary>
        public float Lift;
        /// <summary>Nose up (negative) or down (positive), in degrees.</summary>
        public float Pitch;
        /// <summary>Legs kicking out to the sides (swimming) rather than straight back.</summary>
        public float Splay;
        /// <summary>How flat the body is pressed down (sleeping), 0 = normal.</summary>
        public float Squash;
    }

    /// <summary>
    /// The frog's moves, as numbers that are easy to test: a hop, a swimming kick, breathing,
    /// blinking and a calling throat. Everything is a smooth function of a phase from 0 to 1, so
    /// a frog never snaps from one pose to another.
    /// </summary>
    public static class FrogMoves
    {
        /// <summary>Part of a hop spent in the air; the rest is the crouch before the next one.</summary>
        public const float AirShare = 0.65f;
        /// <summary>How high a hop goes, in body lengths.</summary>
        public const float HopHeight = 0.55f;
        /// <summary>How far one hop carries a frog, in body lengths (long legs go further).</summary>
        public const float HopLength = 2.6f;

        /// <summary>The sitting pose: legs folded, front legs propping the head up a little.</summary>
        public static FrogPose Sitting => new FrogPose { Pitch = -10f };

        /// <summary>
        /// One hop: push off with the back legs, fly with them stretched behind and front legs
        /// reaching, land nose down, fold back up. Phase 0 and 1 are both the sitting pose.
        /// </summary>
        public static FrogPose Hop(float phase)
        {
            phase = Wrap(phase);
            var p = Sitting;
            if (phase < AirShare)
            {
                float t = phase / AirShare;                       // 0..1 through the air
                p.Lift = HopHeight * (float)Math.Sin(Math.PI * t);
                p.Stretch = Rise(t, 0f, 0.25f) * (1f - Rise(t, 0.7f, 1f));
                p.Reach = Rise(t, 0.35f, 0.85f) * (1f - Rise(t, 0.92f, 1f) * 0.5f);
                p.Pitch = t < 0.1f ? Lerp(-10f, -25f, Rise(t, 0f, 0.1f)) : Lerp(-25f, 18f, Rise(t, 0.1f, 0.95f));
            }
            else
            {
                float t = (phase - AirShare) / (1f - AirShare);    // 0..1 settling back down
                p.Reach = 0.5f * (1f - Rise(t, 0f, 0.6f));
                p.Pitch = Lerp(18f, -10f, Rise(t, 0f, 0.7f));
            }
            return p;
        }

        /// <summary>
        /// Swimming: a frog kick. The back legs fold out to the sides and shove straight back, the
        /// front legs stay tucked. Phase 0 and 1 are the glide with legs together behind.
        /// </summary>
        public static FrogPose Swim(float phase)
        {
            phase = Wrap(phase);
            var p = new FrogPose { Pitch = -4f, Reach = 0.15f };
            // Draw the legs up and out (0 to 0.55), then kick back hard (0.55 to 0.75), then glide.
            float draw = Rise(phase, 0f, 0.55f) * (1f - Rise(phase, 0.55f, 0.75f));
            p.Stretch = 1f - draw;
            p.Splay = draw;
            return p;
        }

        /// <summary>Asleep: legs tucked, body pressed flat, nose down a little.</summary>
        public static FrogPose Sleeping => new FrogPose { Pitch = 4f, Squash = 0.3f };

        /// <summary>Breathing: the body swells by about 2% and back, a bit under once a second.</summary>
        public static float Breath(float seconds, int id) => 1f + 0.02f * (float)Math.Sin(seconds * 5.2f + id * 1.7f);

        /// <summary>How open the eyes are (1 open, 0 shut). Each frog blinks every few seconds, at its own times.</summary>
        public static float EyesOpen(float seconds, int id)
        {
            float period = 3.5f + (id % 7) * 0.45f;
            float t = (seconds + id * 2.39f) % period;
            const float blink = 0.16f;
            if (t > blink) return 1f;
            return Math.Abs(t / blink * 2f - 1f); // shut in the middle of the blink
        }

        /// <summary>
        /// The throat sac while calling, from 0 (flat) to 1 (full): it swells for each pulse of the
        /// call. <paramref name="callTime"/> is seconds since the call began; outside a call it is flat.
        /// </summary>
        public static float Throat(float callTime, float callLength, float pulsesPerSecond)
        {
            if (callTime < 0f || callTime > callLength) return 0f;
            float s = (float)Math.Sin(Math.PI * Math.Min(1f, callTime * pulsesPerSecond % 1f));
            float fade = Math.Min(1f, Math.Min(callTime, callLength - callTime) * 12f);
            return s * fade;
        }

        /// <summary>The tadpole's tail swish for a piece along the tail (0 at the body, 1 at the tip), in degrees.</summary>
        public static float TailSwish(float seconds, int id, float piece, float speed)
        {
            float beat = 7f + 6f * Math.Min(1f, speed * 4f);
            float wave = (float)Math.Sin(seconds * beat - piece * 2.2f + id);
            return wave * (12f + 22f * piece) * (0.4f + 0.6f * Math.Min(1f, speed * 4f));
        }

        private static float Wrap(float phase)
        {
            phase %= 1f;
            return phase < 0f ? phase + 1f : phase;
        }

        private static float Rise(float t, float from, float to)
        {
            float k = (t - from) / (to - from);
            k = k < 0f ? 0f : k > 1f ? 1f : k;
            return k * k * (3f - 2f * k);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
