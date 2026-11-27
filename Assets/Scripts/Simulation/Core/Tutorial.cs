using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>Things the player does on screen that the tutorial waits for. The game reports them; the tank cannot see them.</summary>
    [Flags]
    public enum TutorialEvent : ushort
    {
        None = 0,
        CameraMoved = 1,
        FrogSelected = 2,
        HealthOpened = 4,
        SpedUp = 8,
        Saved = 16,
        BuildOpened = 32,
    }

    /// <summary>
    /// Counts taken from the tank when a step starts, so a step like "dig a pool" asks for a new
    /// pool rather than the one the tank already had.
    /// </summary>
    public struct TutorialCounts
    {
        public int WaterPoints, CoverParts, Insects, Feeders, PlayPieces, Young;

        public static TutorialCounts Of(World w)
        {
            var c = new TutorialCounts();
            var t = w.Terrain;
            float below = t.WaterLevel - 0.02f;
            foreach (float y in t.Height) if (y < below) c.WaterPoints++;
            foreach (var p in w.Parts.All)
            {
                var info = p.Info;
                if ((info.Cover || info.Perch) && !Playground.IsPiece(p.Kind)) c.CoverParts++;
            }
            foreach (var d in w.Devices.All) if (d.Kind == DeviceKind.Feeder) c.Feeders++;
            c.PlayPieces = w.Parts.PlayPieces.Count;
            var a = w.Agents;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Insect) c.Insects++;
                else if (a.Stage[i] <= LifeStage.Tadpole) c.Young++;
            }
            return c;
        }
    }

    /// <summary>
    /// One step of the tutorial. Text can name keys as {key:Speed2}; the game swaps in whatever key
    /// the player has set for that action.
    /// </summary>
    public sealed class TutorialStep
    {
        public string Id;
        public string Title;
        /// <summary>What to do, written for the tank in front of the player (money mode or sandbox).</summary>
        public Func<World, string> Text;
        /// <summary>Whether the step is finished: the tank now, what the player has done, and the counts from when the step started.</summary>
        public Func<World, TutorialEvent, TutorialCounts, bool> Done;
        /// <summary>Shown once the step is done, before the next one.</summary>
        public string Praise = "Nice!";
    }

    /// <summary>
    /// The first-30-minutes tutorial: a short list of steps that teach looking around, caring for
    /// the frogs, building and saving. Each step watches the tank (or the player's clicks) and moves
    /// on by itself once it is done. The player can skip a step, hide the panel, or skip it all.
    /// </summary>
    public static class Tutorial
    {
        /// <summary>How many more water points (a quarter metre apart) count as "a pool".</summary>
        public const int PoolPoints = 6;
        /// <summary>How many more insects count as "added food".</summary>
        public const int FoodInsects = 5;

        public static readonly TutorialStep[] Steps =
        {
            new TutorialStep
            {
                Id = "look", Title = "Look around",
                Text = w => "Hold the right mouse button and drag to orbit the tank. Scroll to zoom, and drag with the middle button or use {key:PanForward}{key:PanLeft}{key:PanBack}{key:PanRight} to pan.",
                Done = (w, e, c) => (e & TutorialEvent.CameraMoved) != 0,
            },
            new TutorialStep
            {
                Id = "frog", Title = "Meet a frog",
                Text = w => "Click a frog. Its card on the right shows its needs, mood and genes. Every frog is a little different.",
                Done = (w, e, c) => (e & TutorialEvent.FrogSelected) != 0,
            },
            new TutorialStep
            {
                Id = "health", Title = "Check the tank's health",
                Text = w => "Press {key:HealthScreen} or click Health. It lists anything the frogs need and how warm and damp the tank is.",
                Done = (w, e, c) => (e & TutorialEvent.HealthOpened) != 0,
            },
            new TutorialStep
            {
                Id = "pool", Title = "Dig a pool",
                Text = w => "Pick Lower in the bar below and hold the left mouse on the ground. Dig below the water line and water fills the hole. Frogs drink and lay eggs in it.",
                Done = (w, e, c) => TutorialCounts.Of(w).WaterPoints >= c.WaterPoints + PoolPoints,
            },
            new TutorialStep
            {
                Id = "cover", Title = "Give them somewhere to hide",
                Text = w => "Click Build and place a hide, log or plant. Shy frogs feel safer with cover nearby.",
                Done = (w, e, c) => TutorialCounts.Of(w).CoverParts > c.CoverParts,
            },
            new TutorialStep
            {
                Id = "food", Title = "Add food",
                Text = w => "Pick + 10 Crickets and click the ground, or place a + Feeder that lets crickets out on a timer."
                    + (w.Economy.Enabled ? " Food costs money, so keep an eye on the bar at the top." : ""),
                Done = (w, e, c) =>
                {
                    var now = TutorialCounts.Of(w);
                    return now.Insects >= c.Insects + FoodInsects || now.Feeders > c.Feeders;
                },
            },
            new TutorialStep
            {
                Id = "speed", Title = "Speed up time",
                Text = w => "Click 2x at the top, or press {key:Speed2} (and {key:Speed3} for faster still). {key:Pause} pauses, {key:Speed1} goes back to normal.",
                Done = (w, e, c) => (e & TutorialEvent.SpedUp) != 0,
            },
            new TutorialStep
            {
                Id = "play", Title = "Build them a playground",
                Text = w => (w.Economy.Enabled && !w.Economy.Has(Research.Playground)
                        ? "Open Money and research Frog playground first. Then c" : "C")
                    + "lick Build and place a slide, spiral staircase or air tunnel. Bored frogs ride them on their own, and pieces snapped end to end make chains.",
                Done = (w, e, c) => TutorialCounts.Of(w).PlayPieces > c.PlayPieces,
            },
            new TutorialStep
            {
                Id = "save", Title = "Save your tank",
                Text = w => "Click Save in the bar below or press {key:QuickSave}. Load brings it back, and Files keeps named saves you can share.",
                Done = (w, e, c) => (e & TutorialEvent.Saved) != 0,
            },
            new TutorialStep
            {
                Id = "eggs", Title = "Wait for eggs",
                Text = w => "Happy, well-fed adults pair up and lay eggs in the water. Speed up time and watch the pool. Eggs hatch into tadpoles, then froglets, and babies can mix their parents' genes in surprising ways.",
                Done = (w, e, c) => TutorialCounts.Of(w).Young > 0,
                Praise = "Eggs! A new generation is on its way.",
            },
        };

        public static int IndexOf(string id)
        {
            for (int k = 0; k < Steps.Length; k++) if (Steps[k].Id == id) return k;
            return -1;
        }
    }

    /// <summary>
    /// How far the player has got. Kept on the computer (not in saves), so it carries across new
    /// games and loads; it is written as a short line of text.
    /// </summary>
    public sealed class TutorialProgress
    {
        /// <summary>How long "Nice!" shows before the next step, in real seconds.</summary>
        public const float PraiseSeconds = 2.5f;

        public int Step;
        public TutorialEvent Events;
        /// <summary>Every step is done (or the player skipped the whole tutorial).</summary>
        public bool Finished;
        /// <summary>The player closed the panel. It can be brought back from Settings.</summary>
        public bool Hidden;
        /// <summary>While above zero, the current step is done and its praise is showing.</summary>
        public float PraiseLeft;

        private World _countedWorld;
        private int _countedStep = -1;
        private TutorialCounts _counts;

        public TutorialStep Current => !Finished && Step >= 0 && Step < Tutorial.Steps.Length ? Tutorial.Steps[Step] : null;
        public bool ShowingPraise => PraiseLeft > 0f;

        public void Note(TutorialEvent e) => Events |= e;

        /// <summary>
        /// Checks the current step against the tank. Returns true if anything changed that is worth
        /// writing down. A new tank (new game or load) takes fresh counts.
        /// </summary>
        public bool Update(World w, float realSeconds)
        {
            var step = Current;
            if (step == null || w == null) return false;
            if (_countedWorld != w || _countedStep != Step)
            {
                _counts = TutorialCounts.Of(w);
                _countedWorld = w;
                _countedStep = Step;
            }
            if (PraiseLeft > 0f)
            {
                PraiseLeft -= realSeconds;
                if (PraiseLeft > 0f) return false;
                PraiseLeft = 0f;
                Advance();
                return true;
            }
            if (!step.Done(w, Events, _counts)) return false;
            PraiseLeft = PraiseSeconds;
            return false;
        }

        public void SkipStep()
        {
            if (Current == null) return;
            PraiseLeft = 0f;
            Advance();
        }

        public void SkipAll()
        {
            Finished = true;
            Hidden = true;
            PraiseLeft = 0f;
        }

        public void Restart()
        {
            Step = 0;
            Events = TutorialEvent.None;
            Finished = false;
            Hidden = false;
            PraiseLeft = 0f;
            _countedStep = -1;
        }

        private void Advance()
        {
            Step++;
            if (Step >= Tutorial.Steps.Length) Finished = true;
        }

        /// <summary>Writes the progress as one line: "step id;events;finished;hidden".</summary>
        public string ToText()
        {
            string id = Step < Tutorial.Steps.Length ? Tutorial.Steps[Step].Id : "end";
            return id + ";" + (int)Events + ";" + (Finished ? 1 : 0) + ";" + (Hidden ? 1 : 0);
        }

        /// <summary>Reads progress written by ToText. Anything it cannot read starts the tutorial afresh.</summary>
        public static TutorialProgress FromText(string text)
        {
            var p = new TutorialProgress();
            if (string.IsNullOrEmpty(text)) return p;
            var bits = text.Split(';');
            if (bits.Length != 4) return p;
            int step = bits[0] == "end" ? Tutorial.Steps.Length : Tutorial.IndexOf(bits[0]);
            if (step < 0 || !int.TryParse(bits[1], out int events)) return p;
            p.Step = step;
            p.Events = (TutorialEvent)(events & 0xFFFF);
            p.Finished = bits[2] == "1" || step >= Tutorial.Steps.Length;
            p.Hidden = bits[3] == "1";
            return p;
        }
    }
}
