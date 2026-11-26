using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Every agent's data, kept as plain parallel arrays ("structure of arrays").
    /// Agent i is described by PosX[i], PosZ[i], Hunger[i] and so on. This layout is
    /// cache friendly and can later be handed to Unity Jobs and Burst unchanged.
    /// Rendering reads these arrays; only simulation systems write them.
    /// Removing an agent moves the last agent into its slot, so indices are not
    /// stable over time. Use Id to refer to a specific agent.
    /// </summary>
    public sealed class AgentStore
    {
        public int Count { get; private set; }
        public int Capacity => Id.Length;

        public int[] Id;
        public AgentKind[] Kind;
        public float[] PosX, PosY, PosZ;
        public float[] VelX, VelZ;
        /// <summary>Needs run from 0 (fully satisfied) to 1 (desperate). Fatigue is shown as "Sleep".</summary>
        public float[] Hunger, Thirst, Fatigue;
        /// <summary>How comfortable the surroundings are, from 0 (miserable) to 1 (just right).</summary>
        public float[] Comfort;
        /// <summary>What the agent is trying to do right now, picked by the utility brain.</summary>
        public Goal[] Goal;
        /// <summary>Seconds spent on the current goal.</summary>
        public float[] GoalTime;
        /// <summary>Id of the agent being chased or fled from (0 = none).</summary>
        public int[] TargetId;
        /// <summary>A place the agent is heading for (water, a comfier spot, a wander point).</summary>
        public float[] TargetX, TargetZ;
        /// <summary>Age in seconds.</summary>
        public float[] Age;
        /// <summary>How many seconds this agent will live (insects; 0 = no limit).</summary>
        public float[] Lifespan;
        /// <summary>Countdown in seconds until the agent picks a new wander or comfort spot.</summary>
        public float[] Timer;
        /// <summary>Countdown in seconds until a frog can strike with its tongue again.</summary>
        public float[] Cooldown;

        // Frog life and family (save version 4). Insects keep the defaults.
        /// <summary>Which frog species, as an index into SpeciesCatalog.Frogs.</summary>
        public byte[] Species;
        public Sex[] Sex;
        public LifeStage[] Stage;
        /// <summary>Growth so far, in seconds. Adds up at full speed only when fed and comfortable.</summary>
        public float[] Growth;
        /// <summary>0 calm to 1 at breaking point. A frog at 1 tries to escape.</summary>
        public float[] Stress;
        /// <summary>Ids of the parents (0 = unknown, for example a frog bought in).</summary>
        public int[] MotherId, FatherId;
        /// <summary>For a female carrying eggs: the father's id (0 = not carrying eggs).</summary>
        public int[] MateId;
        /// <summary>For a female carrying eggs: seconds she has been carrying them.</summary>
        public float[] Gravid;
        /// <summary>Seconds until this frog can breed again.</summary>
        public float[] BreedCooldown;
        /// <summary>The frog's own genes.</summary>
        public Genome[] Genes;
        /// <summary>For a female carrying eggs: the father's genes, kept in case he is gone by the time she lays.</summary>
        public Genome[] MateGenes;

        private int _nextId = 1;
        public int NextId => _nextId;

        public AgentStore(int initialCapacity = 256)
        {
            Allocate(Math.Max(1, initialCapacity));
        }

        public int Add(AgentKind kind, float x, float y, float z)
        {
            if (Count == Capacity) Grow(Capacity * 2);
            int i = Count++;
            Id[i] = _nextId++;
            Kind[i] = kind;
            PosX[i] = x; PosY[i] = y; PosZ[i] = z;
            VelX[i] = 0f; VelZ[i] = 0f;
            Hunger[i] = 0f; Thirst[i] = 0f; Fatigue[i] = 0f;
            Comfort[i] = 1f;
            Goal[i] = Simulation.Goal.Idle;
            GoalTime[i] = 0f;
            TargetId[i] = 0;
            TargetX[i] = x; TargetZ[i] = z;
            Age[i] = 0f; Lifespan[i] = 0f; Timer[i] = 0f; Cooldown[i] = 0f;
            Species[i] = 0;
            Sex[i] = Simulation.Sex.Female;
            Stage[i] = LifeStage.Adult;
            Growth[i] = 0f; Stress[i] = 0f;
            MotherId[i] = 0; FatherId[i] = 0; MateId[i] = 0;
            Gravid[i] = 0f; BreedCooldown[i] = 0f;
            Genes[i] = Genome.Default;
            MateGenes[i] = Genome.Default;
            return i;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            int last = --Count;
            if (index != last)
            {
                Id[index] = Id[last];
                Kind[index] = Kind[last];
                PosX[index] = PosX[last]; PosY[index] = PosY[last]; PosZ[index] = PosZ[last];
                VelX[index] = VelX[last]; VelZ[index] = VelZ[last];
                Hunger[index] = Hunger[last]; Thirst[index] = Thirst[last]; Fatigue[index] = Fatigue[last];
                Comfort[index] = Comfort[last];
                Goal[index] = Goal[last];
                GoalTime[index] = GoalTime[last];
                TargetId[index] = TargetId[last];
                TargetX[index] = TargetX[last]; TargetZ[index] = TargetZ[last];
                Age[index] = Age[last]; Lifespan[index] = Lifespan[last]; Timer[index] = Timer[last]; Cooldown[index] = Cooldown[last];
                Species[index] = Species[last];
                Sex[index] = Sex[last];
                Stage[index] = Stage[last];
                Growth[index] = Growth[last]; Stress[index] = Stress[last];
                MotherId[index] = MotherId[last]; FatherId[index] = FatherId[last]; MateId[index] = MateId[last];
                Gravid[index] = Gravid[last]; BreedCooldown[index] = BreedCooldown[last];
                Genes[index] = Genes[last];
                MateGenes[index] = MateGenes[last];
            }
        }

        public int IndexOf(int id)
        {
            for (int i = 0; i < Count; i++) if (Id[i] == id) return i;
            return -1;
        }

        public void Clear()
        {
            Count = 0;
            _nextId = 1;
        }

        /// <summary>Used by the save loader to restore the id counter.</summary>
        internal void SetNextId(int nextId) => _nextId = nextId;

        private void Allocate(int capacity)
        {
            Id = new int[capacity];
            Kind = new AgentKind[capacity];
            PosX = new float[capacity]; PosY = new float[capacity]; PosZ = new float[capacity];
            VelX = new float[capacity]; VelZ = new float[capacity];
            Hunger = new float[capacity]; Thirst = new float[capacity]; Fatigue = new float[capacity];
            Comfort = new float[capacity];
            Goal = new Goal[capacity];
            GoalTime = new float[capacity];
            TargetId = new int[capacity];
            TargetX = new float[capacity]; TargetZ = new float[capacity];
            Age = new float[capacity]; Lifespan = new float[capacity]; Timer = new float[capacity]; Cooldown = new float[capacity];
            Species = new byte[capacity];
            Sex = new Sex[capacity];
            Stage = new LifeStage[capacity];
            Growth = new float[capacity]; Stress = new float[capacity];
            MotherId = new int[capacity]; FatherId = new int[capacity]; MateId = new int[capacity];
            Gravid = new float[capacity]; BreedCooldown = new float[capacity];
            Genes = new Genome[capacity];
            MateGenes = new Genome[capacity];
        }

        private void Grow(int capacity)
        {
            Array.Resize(ref Id, capacity);
            Array.Resize(ref Kind, capacity);
            Array.Resize(ref PosX, capacity); Array.Resize(ref PosY, capacity); Array.Resize(ref PosZ, capacity);
            Array.Resize(ref VelX, capacity); Array.Resize(ref VelZ, capacity);
            Array.Resize(ref Hunger, capacity); Array.Resize(ref Thirst, capacity); Array.Resize(ref Fatigue, capacity);
            Array.Resize(ref Comfort, capacity);
            Array.Resize(ref Goal, capacity);
            Array.Resize(ref GoalTime, capacity);
            Array.Resize(ref TargetId, capacity);
            Array.Resize(ref TargetX, capacity); Array.Resize(ref TargetZ, capacity);
            Array.Resize(ref Age, capacity); Array.Resize(ref Lifespan, capacity); Array.Resize(ref Timer, capacity); Array.Resize(ref Cooldown, capacity);
            Array.Resize(ref Species, capacity);
            Array.Resize(ref Sex, capacity);
            Array.Resize(ref Stage, capacity);
            Array.Resize(ref Growth, capacity); Array.Resize(ref Stress, capacity);
            Array.Resize(ref MotherId, capacity); Array.Resize(ref FatherId, capacity); Array.Resize(ref MateId, capacity);
            Array.Resize(ref Gravid, capacity); Array.Resize(ref BreedCooldown, capacity);
            Array.Resize(ref Genes, capacity);
            Array.Resize(ref MateGenes, capacity);
        }

        /// <summary>True for frogs that live on land (froglets and older), false for eggs, tadpoles and insects.</summary>
        public bool IsLandFrog(int i) => Kind[i] == AgentKind.Frog && Stage[i] >= LifeStage.Froglet;

        /// <summary>True for frog eggs and tadpoles, which live in the water.</summary>
        public bool IsAquaticYoung(int i) => Kind[i] == AgentKind.Frog && Stage[i] <= LifeStage.Tadpole;

        /// <summary>Gives an agent a specific id (used when loading or bringing back an escaped frog).</summary>
        internal void SetId(int index, int id)
        {
            Id[index] = id;
            if (id >= _nextId) _nextId = id + 1;
        }

        public int CountOf(AgentKind kind)
        {
            int n = 0;
            for (int i = 0; i < Count; i++) if (Kind[i] == kind) n++;
            return n;
        }
    }
}
