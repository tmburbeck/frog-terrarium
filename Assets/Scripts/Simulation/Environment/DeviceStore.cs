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
        /// <summary>Releases a few live crickets on a timer, from a supply the player refills.</summary>
        Feeder = 2,
        /// <summary>
        /// The smart controller: one box that watches the frogs and sets every device in Smart mode
        /// (and Smart cleaning) to suit the frogs near it. Uses a little power.
        /// </summary>
        Controller = 3,
    }

    /// <summary>
    /// How much a device does on its own (the automation tiers).
    /// Basic: heaters keep to the thermostat you set, misters spray on a timer, feeders release on a timer.
    /// Sensor: misters spray whenever the air near them is drier than their target; feeders release
    /// only when crickets are running short and refill themselves when low (paying for the crickets).
    /// Smart: like Sensor, but the smart controller picks the targets from the frogs near the device.
    /// Heaters have no Sensor tier: their thermostat already is one.
    /// </summary>
    public enum DeviceMode : byte
    {
        Basic = 0,
        Sensor = 1,
        Smart = 2,
    }

    /// <summary>
    /// One placed device. The meaning of Setting and Setting2 depends on the kind:
    /// heater: Setting = thermostat target °C;
    /// mister: Setting = seconds between sprays, Setting2 = seconds each spray lasts;
    /// feeder: Setting = seconds between releases, Setting2 = crickets per release, Supply = crickets left.
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
        /// <summary>The automation tier (saved from version 10).</summary>
        public DeviceMode Mode;
        /// <summary>
        /// The target in Sensor and Smart mode (saved from version 10): mister = humidity %,
        /// feeder = crickets to keep per frog. In Smart mode the controller sets it.
        /// </summary>
        public float Target;
        /// <summary>True when a self-refilling feeder could not pay for its crickets. Not saved.</summary>
        public bool RefillFailed;

        public string DisplayName => (Kind == DeviceKind.Controller ? "Smart controller" : Kind.ToString()) + " " + Id;
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

        public const int FeederCapacity = 200;
        /// <summary>The feeder warns when its supply drops below this share of its capacity. Self-refilling feeders refill here.</summary>
        public const float FeederLowShare = 0.2f;

        /// <summary>A sensor mister switches on this far below its target humidity and off this far above.</summary>
        public const float MisterBand = 3f;
        /// <summary>How far around itself a mister's humidity sensor reads (metres).</summary>
        public const float SensorRadius = 1.5f;
        public const float MisterMinTarget = 40f, MisterMaxTarget = 98f;
        public const float FeederMinTarget = 0.5f, FeederMaxTarget = 8f;
        /// <summary>The controller suits a device to the frogs within this distance of it (metres); with none that close, to all the frogs.</summary>
        public const float SmartRadius = 6f;

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
                case DeviceKind.Feeder: d.Setting = 60f; d.Setting2 = 5f; d.Supply = 100f; d.Timer = 10f; d.Target = 2f; break;
            }
            if (d.Kind == DeviceKind.Mister) d.Target = 75f;
        }

        /// <summary>The automation tiers a kind of device can use.</summary>
        public static bool HasMode(DeviceKind kind, DeviceMode mode)
        {
            switch (kind)
            {
                case DeviceKind.Heater: return mode != DeviceMode.Sensor;
                case DeviceKind.Controller: return mode == DeviceMode.Basic;
                default: return true;
            }
        }

        /// <summary>The automation tier's name for menus.</summary>
        public static string ModeName(DeviceKind kind, DeviceMode mode)
        {
            switch (mode)
            {
                case DeviceMode.Sensor: return kind == DeviceKind.Feeder ? "Sensor + refill" : "Sensor";
                case DeviceMode.Smart: return "Smart";
                default: return kind == DeviceKind.Heater ? "Thermostat" : "Timer";
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
                    d.Target = TerrainGrid.Clamp(d.Target, MisterMinTarget, MisterMaxTarget);
                    break;
                case DeviceKind.Feeder:
                    d.Setting = TerrainGrid.Clamp(d.Setting, 10f, 600f);
                    d.Setting2 = (float)Math.Round(TerrainGrid.Clamp(d.Setting2, 1f, 20f));
                    d.Supply = TerrainGrid.Clamp(d.Supply, 0f, FeederCapacity);
                    d.Target = TerrainGrid.Clamp(d.Target, FeederMinTarget, FeederMaxTarget);
                    break;
            }
            if (!HasMode(d.Kind, d.Mode)) d.Mode = DeviceMode.Basic;
        }

        public bool Remove(int id)
        {
            int i = _devices.FindIndex(d => d.Id == id);
            if (i < 0) return false;
            _devices.RemoveAt(i);
            return true;
        }

        public Device Find(int id) => _devices.Find(d => d.Id == id);

        /// <summary>True if a smart controller is in the tank and switched on.</summary>
        public bool HasController
        {
            get
            {
                foreach (var d in _devices) if (d.Kind == DeviceKind.Controller && d.Enabled) return true;
                return false;
            }
        }

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
