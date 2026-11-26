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
        /// <summary>Needs run from 0 (fully satisfied) to 1 (desperate).</summary>
        public float[] Hunger, Thirst, Fatigue;

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
        }

        private void Grow(int capacity)
        {
            Array.Resize(ref Id, capacity);
            Array.Resize(ref Kind, capacity);
            Array.Resize(ref PosX, capacity); Array.Resize(ref PosY, capacity); Array.Resize(ref PosZ, capacity);
            Array.Resize(ref VelX, capacity); Array.Resize(ref VelZ, capacity);
            Array.Resize(ref Hunger, capacity); Array.Resize(ref Thirst, capacity); Array.Resize(ref Fatigue, capacity);
        }
    }
}
