using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// The fixed-tick loop. The game calls Advance(realSeconds) once per rendered
    /// frame; each system then runs as many fixed steps as are due at its own rate.
    /// This keeps the simulation identical whether the game runs at 30 or 144 FPS,
    /// and keeps it fully separate from rendering.
    /// </summary>
    public sealed class SimulationLoop
    {
        /// <summary>
        /// If a frame is very slow, cap the work so the game cannot fall further and
        /// further behind ("spiral of death"). Time beyond this is dropped.
        /// </summary>
        public const double MaxCatchUpSeconds = 0.25;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public SimulationLoop(World world)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
        }

        public World World { get; }

        /// <summary>1 = normal speed, 0 = paused, 3 = fast-forward.</summary>
        public float TimeScale { get; set; } = 1f;

        public IReadOnlyList<Entry> Systems => _entries;

        /// <summary>Wall-clock milliseconds spent in all systems during the last Advance call.</summary>
        public double LastAdvanceMs { get; private set; }

        /// <summary>Simulated seconds dropped because frames were too slow.</summary>
        public double DroppedSeconds { get; private set; }

        public void AddSystem(ISimSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (system.TicksPerSecond <= 0) throw new ArgumentException("TicksPerSecond must be positive.", nameof(system));
            _entries.Add(new Entry(system));
        }

        public void Advance(double realSeconds)
        {
            if (realSeconds < 0) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            double dt = realSeconds * TimeScale;
            if (dt > MaxCatchUpSeconds)
            {
                DroppedSeconds += dt - MaxCatchUpSeconds;
                dt = MaxCatchUpSeconds;
            }

            double totalMs = 0;
            foreach (var e in _entries)
            {
                e.Accumulator += dt;
                double step = 1.0 / e.System.TicksPerSecond;
                int ticksThisFrame = 0;
                _stopwatch.Restart();
                while (e.Accumulator >= step)
                {
                    e.System.Tick(World, (float)step);
                    e.Accumulator -= step;
                    e.TotalTicks++;
                    ticksThisFrame++;
                }
                _stopwatch.Stop();
                double ms = _stopwatch.Elapsed.TotalMilliseconds;
                e.TicksLastFrame = ticksThisFrame;
                if (ticksThisFrame > 0) e.LastTickMs = ms / ticksThisFrame;
                totalMs += ms;
            }
            World.SimTime += dt;
            LastAdvanceMs = totalMs;
        }

        /// <summary>Per-system bookkeeping: timing for the debug overlay and the step accumulator.</summary>
        public sealed class Entry
        {
            internal Entry(ISimSystem system) { System = system; }

            public ISimSystem System { get; }
            public double Accumulator { get; internal set; }
            public long TotalTicks { get; internal set; }
            public int TicksLastFrame { get; internal set; }

            /// <summary>Average wall-clock milliseconds per tick in the last frame that ticked.</summary>
            public double LastTickMs { get; internal set; }

            /// <summary>0..1, how far we are between the previous tick and the next. Use it to smooth rendering.</summary>
            public float Alpha => (float)(Accumulator * System.TicksPerSecond);
        }
    }
}
