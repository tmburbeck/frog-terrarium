using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>Everything a key can be set to do. The order here is the order in the Settings window.</summary>
    public enum GameAction
    {
        PanForward, PanBack, PanLeft, PanRight,
        BrushSmaller, BrushBigger,
        TurnPart, ToggleGrid, Delete,
        Pause, Speed1, Speed2, Speed3,
        QuickSave, QuickLoad,
        HealthScreen, SettingsWindow, StatsOverlay, SpeedTest,
    }

    /// <summary>
    /// Which keys do what. Each action can have two keys (for example W and the up arrow). Setting a
    /// key that another action already uses takes it away from that action, so one key never does
    /// two things. Some keys are kept for themselves: Esc (cancel), Backspace (clear a key in the
    /// Settings window), Shift (turn the other way), Ctrl, Alt and Cmd (undo and redo).
    /// </summary>
    public sealed class KeyBindings
    {
        public const int Slots = 2;
        public static readonly GameAction[] Actions = (GameAction[])Enum.GetValues(typeof(GameAction));
        private readonly KeyCode[,] _keys = new KeyCode[Actions.Length, Slots];

        public KeyBindings() { ResetAll(); }

        /// <summary>The keys the game starts with (the same as before keys could be changed).</summary>
        public static KeyCode Default(GameAction a, int slot)
        {
            switch (a)
            {
                case GameAction.PanForward: return slot == 0 ? KeyCode.W : KeyCode.UpArrow;
                case GameAction.PanBack: return slot == 0 ? KeyCode.S : KeyCode.DownArrow;
                case GameAction.PanLeft: return slot == 0 ? KeyCode.A : KeyCode.LeftArrow;
                case GameAction.PanRight: return slot == 0 ? KeyCode.D : KeyCode.RightArrow;
                case GameAction.BrushSmaller: return slot == 0 ? KeyCode.LeftBracket : KeyCode.None;
                case GameAction.BrushBigger: return slot == 0 ? KeyCode.RightBracket : KeyCode.None;
                case GameAction.TurnPart: return slot == 0 ? KeyCode.R : KeyCode.None;
                case GameAction.ToggleGrid: return slot == 0 ? KeyCode.G : KeyCode.None;
                case GameAction.Delete: return slot == 0 ? KeyCode.Delete : KeyCode.None;
                case GameAction.Pause: return slot == 0 ? KeyCode.P : KeyCode.None;
                case GameAction.Speed1: return slot == 0 ? KeyCode.Alpha1 : KeyCode.None;
                case GameAction.Speed2: return slot == 0 ? KeyCode.Alpha2 : KeyCode.None;
                case GameAction.Speed3: return slot == 0 ? KeyCode.Alpha3 : KeyCode.None;
                case GameAction.QuickSave: return slot == 0 ? KeyCode.F5 : KeyCode.None;
                case GameAction.QuickLoad: return slot == 0 ? KeyCode.F9 : KeyCode.None;
                case GameAction.HealthScreen: return slot == 0 ? KeyCode.H : KeyCode.None;
                case GameAction.SettingsWindow: return slot == 0 ? KeyCode.F1 : KeyCode.None;
                case GameAction.StatsOverlay: return slot == 0 ? KeyCode.F3 : KeyCode.O;
                case GameAction.SpeedTest: return slot == 0 ? KeyCode.F8 : KeyCode.B;
                default: return KeyCode.None;
            }
        }

        /// <summary>What an action is called in the Settings window.</summary>
        public static string Name(GameAction a)
        {
            switch (a)
            {
                case GameAction.PanForward: return "Move camera forward";
                case GameAction.PanBack: return "Move camera back";
                case GameAction.PanLeft: return "Move camera left";
                case GameAction.PanRight: return "Move camera right";
                case GameAction.BrushSmaller: return "Smaller brush";
                case GameAction.BrushBigger: return "Bigger brush";
                case GameAction.TurnPart: return "Turn part (Shift turns back)";
                case GameAction.ToggleGrid: return "Grid or free placement";
                case GameAction.Delete: return "Remove selected";
                case GameAction.Pause: return "Pause";
                case GameAction.Speed1: return "Speed x1";
                case GameAction.Speed2: return "Speed x2";
                case GameAction.Speed3: return "Speed x4";
                case GameAction.QuickSave: return "Quick-save";
                case GameAction.QuickLoad: return "Quick-load";
                case GameAction.HealthScreen: return "Tank health screen";
                case GameAction.SettingsWindow: return "Settings window";
                case GameAction.StatsOverlay: return "Stats overlay";
                case GameAction.SpeedTest: return "Speed test";
                default: return a.ToString();
            }
        }

        /// <summary>Keys that keep their own job and can't be given to an action.</summary>
        public static bool IsReserved(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Escape: case KeyCode.Backspace:
                case KeyCode.LeftShift: case KeyCode.RightShift:
                case KeyCode.LeftControl: case KeyCode.RightControl:
                case KeyCode.LeftAlt: case KeyCode.RightAlt:
                case KeyCode.LeftCommand: case KeyCode.RightCommand:
                case KeyCode.LeftWindows: case KeyCode.RightWindows:
                    return true;
                default:
                    return key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6;
            }
        }

        public KeyCode Get(GameAction a, int slot) => _keys[(int)a, slot];

        /// <summary>True if this key is one of the action's keys.</summary>
        public bool Uses(GameAction a, KeyCode key)
        {
            if (key == KeyCode.None) return false;
            for (int s = 0; s < Slots; s++) if (_keys[(int)a, s] == key) return true;
            return false;
        }

        /// <summary>The action that uses a key, or null.</summary>
        public GameAction? ActionOf(KeyCode key)
        {
            foreach (var a in Actions) if (Uses(a, key)) return a;
            return null;
        }

        /// <summary>
        /// Gives a key to an action. Returns what happened in a sentence for the Settings window
        /// (including which action lost the key), or why the key can't be used.
        /// </summary>
        public string Set(GameAction a, int slot, KeyCode key)
        {
            if (key == KeyCode.None) { Clear(a, slot); return Name(a) + ": key cleared."; }
            if (IsReserved(key)) return KeyName(key) + " is kept for its own job and can't be changed.";
            string note = "";
            foreach (var other in Actions)
                for (int s = 0; s < Slots; s++)
                    if (_keys[(int)other, s] == key && !(other == a && s == slot))
                    {
                        _keys[(int)other, s] = KeyCode.None;
                        if (other != a) note = " It was used for " + Name(other) + ", which no longer has it.";
                    }
            _keys[(int)a, slot] = key;
            return Name(a) + " is now " + KeyName(key) + "." + note;
        }

        public void Clear(GameAction a, int slot) => _keys[(int)a, slot] = KeyCode.None;

        public void ResetAll()
        {
            foreach (var a in Actions)
                for (int s = 0; s < Slots; s++) _keys[(int)a, s] = Default(a, s);
        }

        /// <summary>True if no key has been changed from the start.</summary>
        public bool IsDefault
        {
            get
            {
                foreach (var a in Actions)
                    for (int s = 0; s < Slots; s++) if (_keys[(int)a, s] != Default(a, s)) return false;
                return true;
            }
        }

        /// <summary>Writes the keys as text, by name, so adding actions later does not mix them up ("Pause=P,None;...").</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            foreach (var a in Actions)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(a).Append('=').Append(_keys[(int)a, 0]).Append(',').Append(_keys[(int)a, 1]);
            }
            return sb.ToString();
        }

        /// <summary>Reads keys written by <see cref="ToText"/>. Anything unknown or broken keeps its default.</summary>
        public static KeyBindings FromText(string text)
        {
            var b = new KeyBindings();
            if (string.IsNullOrEmpty(text)) return b;
            var inText = new HashSet<GameAction>();
            foreach (var entry in text.Split(';'))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0 || !Enum.TryParse(entry.Substring(0, eq), out GameAction a) || !Enum.IsDefined(typeof(GameAction), a)) continue;
                var keys = entry.Substring(eq + 1).Split(',');
                if (keys.Length != Slots) continue;
                var parsed = new KeyCode[Slots];
                bool ok = true;
                for (int s = 0; s < Slots; s++)
                    ok &= Enum.TryParse(keys[s], out parsed[s]) && Enum.IsDefined(typeof(KeyCode), parsed[s]);
                if (!ok) continue;
                inText.Add(a);
                for (int s = 0; s < Slots; s++) b._keys[(int)a, s] = IsReserved(parsed[s]) ? KeyCode.None : parsed[s];
            }
            // One key never does two things. The player's own choices win over the defaults of
            // actions they never saw (ones added by a later version of the game).
            var used = new HashSet<KeyCode>();
            for (int pass = 0; pass < 2; pass++)
                foreach (var a in Actions)
                {
                    if (inText.Contains(a) != (pass == 0)) continue;
                    for (int s = 0; s < Slots; s++)
                    {
                        var k = b._keys[(int)a, s];
                        if (k != KeyCode.None && !used.Add(k)) b._keys[(int)a, s] = KeyCode.None;
                    }
                }
            return b;
        }

        /// <summary>A short name for a key ("1", "[", "Up", "F5").</summary>
        public static string KeyName(KeyCode key)
        {
            if (key == KeyCode.None) return "-";
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "Num " + (int)(key - KeyCode.Keypad0);
            switch (key)
            {
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Backslash: return "\\";
                case KeyCode.KeypadPlus: return "Num +";
                case KeyCode.KeypadMinus: return "Num -";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
                default: return key.ToString();
            }
        }
    }

    /// <summary>
    /// The player's settings on this computer: keys, menu size and colours, and how the camera
    /// moves. Kept in Unity's PlayerPrefs, never in save files, so they follow the player rather
    /// than the tank.
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "FrogTerrarium.Settings.";
        private static KeyBindings _keys;
        private static bool _loaded;
        private static float _uiScale = 1f, _orbitSpeed = 1f, _zoomSpeed = 1f, _panSpeed = 1f, _volume = DefaultVolume, _ambience = DefaultAmbience;
        public const float DefaultVolume = 0.8f, DefaultAmbience = 0.6f;
        private static bool _invertY, _colourBlind, _highContrast, _shareStats;

        public const float MinUiScale = 0.75f, MaxUiScale = 2f;
        public const float MinSpeed = 0.25f, MaxSpeed = 3f;

        /// <summary>True while the Settings window waits for a key to be pressed: no other key does anything then.</summary>
        public static bool Capturing;

        public static KeyBindings Keys { get { Load(); return _keys; } }

        /// <summary>How big the menus and their text are (1 = normal).</summary>
        public static float UiScale { get { Load(); return _uiScale; } set { _uiScale = Mathf.Clamp(value, MinUiScale, MaxUiScale); } }
        /// <summary>How fast the camera turns when you drag with the right mouse button (1 = normal).</summary>
        public static float OrbitSpeed { get { Load(); return _orbitSpeed; } set { _orbitSpeed = Mathf.Clamp(value, MinSpeed, MaxSpeed); } }
        public static float ZoomSpeed { get { Load(); return _zoomSpeed; } set { _zoomSpeed = Mathf.Clamp(value, MinSpeed, MaxSpeed); } }
        public static float PanSpeed { get { Load(); return _panSpeed; } set { _panSpeed = Mathf.Clamp(value, MinSpeed, MaxSpeed); } }
        /// <summary>Dragging the mouse up tilts the camera down instead of up.</summary>
        public static bool InvertY { get { Load(); return _invertY; } set => _invertY = value; }
        /// <summary>Uses blue and orange instead of green and red for good and bad, so they are easy to tell apart with colour blindness.</summary>
        public static bool ColourBlind { get { Load(); return _colourBlind; } set => _colourBlind = value; }
        /// <summary>Solid dark menus with bright text, easier to read over a busy tank.</summary>
        public static bool HighContrast { get { Load(); return _highContrast; } set => _highContrast = value; }

        /// <summary>How loud the game is, 0 (silent) to 1.</summary>
        public static float Volume { get { Load(); return _volume; } set { _volume = Mathf.Clamp01(value); } }
        /// <summary>How loud the background (crickets, distant frogs) is compared with everything else, 0 to 1.</summary>
        public static float Ambience { get { Load(); return _ambience; } set { _ambience = Mathf.Clamp01(value); } }

        /// <summary>The player chose to record playtest stats (a spreadsheet on this computer; nothing is sent). Off unless switched on.</summary>
        public static bool ShareStats { get { Load(); return _shareStats; } set => _shareStats = value; }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _keys = KeyBindings.FromText(PlayerPrefs.GetString(Prefix + "Keys", ""));
            _uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "UiScale", 1f), MinUiScale, MaxUiScale);
            _orbitSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "OrbitSpeed", 1f), MinSpeed, MaxSpeed);
            _zoomSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "ZoomSpeed", 1f), MinSpeed, MaxSpeed);
            _panSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "PanSpeed", 1f), MinSpeed, MaxSpeed);
            _invertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) != 0;
            _colourBlind = PlayerPrefs.GetInt(Prefix + "ColourBlind", 0) != 0;
            _highContrast = PlayerPrefs.GetInt(Prefix + "HighContrast", 0) != 0;
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", DefaultVolume));
            _ambience = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Ambience", DefaultAmbience));
            _shareStats = PlayerPrefs.GetInt(Prefix + "ShareStats", 0) != 0;
        }

        /// <summary>Remembers the settings on this computer.</summary>
        public static void Save()
        {
            Load();
            PlayerPrefs.SetString(Prefix + "Keys", _keys.ToText());
            PlayerPrefs.SetFloat(Prefix + "UiScale", _uiScale);
            PlayerPrefs.SetFloat(Prefix + "OrbitSpeed", _orbitSpeed);
            PlayerPrefs.SetFloat(Prefix + "ZoomSpeed", _zoomSpeed);
            PlayerPrefs.SetFloat(Prefix + "PanSpeed", _panSpeed);
            PlayerPrefs.SetInt(Prefix + "InvertY", _invertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "ColourBlind", _colourBlind ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "HighContrast", _highContrast ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "Volume", _volume);
            PlayerPrefs.SetFloat(Prefix + "Ambience", _ambience);
            PlayerPrefs.SetInt(Prefix + "ShareStats", _shareStats ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Puts every setting except the keys back to how the game starts.</summary>
        public static void ResetDisplayAndCamera()
        {
            Load();
            _uiScale = _orbitSpeed = _zoomSpeed = _panSpeed = 1f;
            _invertY = _colourBlind = _highContrast = false;
            _volume = DefaultVolume;
            _ambience = DefaultAmbience;
        }

        /// <summary>Forgets what was loaded, so the next use reads PlayerPrefs again (for tests).</summary>
        public static void Reload() { _loaded = false; Capturing = false; }

        /// <summary>True while a text box has the keyboard or a key is being set, when keys should not act.</summary>
        public static bool KeysBlocked => Capturing || GUIUtility.keyboardControl != 0;

#if ENABLE_LEGACY_INPUT_MANAGER
        /// <summary>True on the frame one of the action's keys was pressed (use from Update, not OnGUI).</summary>
        public static bool Pressed(GameAction a)
        {
            if (KeysBlocked) return false;
            var k = Keys;
            for (int s = 0; s < KeyBindings.Slots; s++)
            {
                var key = k.Get(a, s);
                if (key != KeyCode.None && Input.GetKeyDown(key)) return true;
            }
            return false;
        }

        /// <summary>True while one of the action's keys is held down.</summary>
        public static bool Held(GameAction a)
        {
            if (KeysBlocked) return false;
            var k = Keys;
            for (int s = 0; s < KeyBindings.Slots; s++)
            {
                var key = k.Get(a, s);
                if (key != KeyCode.None && Input.GetKey(key)) return true;
            }
            return false;
        }
#endif

        /// <summary>The name of an action's key for hints on screen ("R"), or "-" if it has none.</summary>
        public static string KeyFor(GameAction a)
        {
            var k = Keys.Get(a, 0);
            if (k == KeyCode.None) k = Keys.Get(a, 1);
            return KeyBindings.KeyName(k);
        }

        /// <summary>True if an OnGUI key event is one of the action's keys (and keys are not blocked).</summary>
        public static bool IsKeyEvent(Event e, GameAction a) =>
            e != null && e.type == EventType.KeyDown && !KeysBlocked && Keys.Uses(a, e.keyCode);

        // Colours for good, so-so and bad. The colour-blind set (from the Okabe-Ito palette) swaps
        // green and red for blue and orange.
        public static Color Good => ColourBlind ? new Color(0.35f, 0.7f, 1f) : new Color(0.5f, 0.95f, 0.5f);
        public static Color Warn => ColourBlind ? new Color(0.95f, 0.9f, 0.35f) : new Color(1f, 0.85f, 0.4f);
        public static Color Bad => ColourBlind ? new Color(0.95f, 0.55f, 0.1f) : new Color(1f, 0.45f, 0.4f);
        public static Color Info => new Color(0.75f, 0.9f, 1f);

        /// <summary>The fill colour of a bar, from bad (0) to good (1).</summary>
        public static Color BarColour(float good) => ColourBlind
            ? Color.Lerp(new Color(0.9f, 0.45f, 0.05f), new Color(0.2f, 0.55f, 0.95f), good)
            : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.3f, 0.8f, 0.3f), good);
    }
}
