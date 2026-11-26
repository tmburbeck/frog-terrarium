using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    public enum DeviceKind : byte
    {
        /// <summary>Warms the air around it, with a thermostat. Dries the air a little.</summary>
        Heater = 0,
        /// <summary>Sprays water mist on a timer, raising humidity around it.</summary>
        Mister = 1,
    }

    /// <summary>
    /// One placed device. The meaning of Setting and Setting2 depends on the kind:
    /// heater: Setting = thermostat target °C;
    /// mister: Setting = seconds between sprays, Setting2 = seconds each spray lasts.
    /// </summary>
    public sealed class Device
    {
        public int Id;
        public DeviceKind Kind;
        public float X, Z;
        public bool Enabled = true;
        public float Setting;
        public float Setting2;
        /// <summary>Consumable stock (for devices that use one later, such as feeders).</summary>
        public float Supply;
        /// <summary>Internal countdown, in seconds.</summary>
        public float Timer;
        /// <summary>True while the device is working right now (heater heating, mister spraying).</summary>
        public bool Active;

        public string DisplayName => Kind.ToString() + " " + Id;
    }

    /// <summary>All devices in the tank, plus the rules for their default settings and limits.</summary>
    public sealed class DeviceStore
    {
        public const float HeaterRadius = 4.5f;
        /// <summary>°C per second at the heater itself, fading with distance.</summary>
        public const float HeaterPower = 1.0f;
        /// <summary>The heater's rated maximum: it never heats air above this.</summary>
        public const float HeaterMaxTemperature = 35f;
        public const float HeaterMinTarget = 18f;

        public const float MisterRadius = 4.5f;
        /// <summary>Humidity percentage points per second at the mister while spraying.</summary>
        public const float MisterPower = 6f;

        private readonly List<Device> _devices = new List<Device>();
        private int _nextId = 1;

        public IReadOnlyList<Device> All => _devices;
        public int Count => _devices.Count;
        public int NextId => _nextId;

        public Device Add(DeviceKind kind, float x, float z)
        {
            var d = new Device { Id = _nextId++, Kind = kind, X = x, Z = z };
            ApplyDefaults(d);
            _devices.Add(d);
            return d;
        }

        public static void ApplyDefaults(Device d)
        {
            switch (d.Kind)
            {
                case DeviceKind.Heater: d.Setting = 27f; break;
                case DeviceKind.Mister: d.Setting = 45f; d.Setting2 = 8f; d.Timer = 2f; break;
            }
        }

        /// <summary>Keeps settings inside what the device can do.</summary>
        public static void ClampSettings(Device d)
        {
            switch (d.Kind)
            {
                case DeviceKind.Heater:
                    d.Setting = TerrainGrid.Clamp(d.Setting, HeaterMinTarget, HeaterMaxTemperature);
                    break;
                case DeviceKind.Mister:
                    d.Setting = TerrainGrid.Clamp(d.Setting, 10f, 600f);
                    d.Setting2 = TerrainGrid.Clamp(d.Setting2, 1f, Math.Min(60f, d.Setting));
                    break;
            }
        }

        public bool Remove(int id)
        {
            int i = _devices.FindIndex(d => d.Id == id);
            if (i < 0) return false;
            _devices.RemoveAt(i);
            return true;
        }

        public Device Find(int id) => _devices.Find(d => d.Id == id);

        /// <summary>The device closest to (x, z) within maxDistance, or null.</summary>
        public Device Nearest(float x, float z, float maxDistance)
        {
            Device best = null;
            float bestD2 = maxDistance * maxDistance;
            foreach (var d in _devices)
            {
                float dx = d.X - x, dz = d.Z - z, d2 = dx * dx + dz * dz;
                if (d2 <= bestD2) { bestD2 = d2; best = d; }
            }
            return best;
        }

        public void Clear()
        {
            _devices.Clear();
            _nextId = 1;
        }

        /// <summary>Used by the save loader.</summary>
        internal void Restore(Device d)
        {
            _devices.Add(d);
            if (d.Id >= _nextId) _nextId = d.Id + 1;
        }

        internal void SetNextId(int nextId) => _nextId = Math.Max(_nextId, nextId);
    }
}
