using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The Settings window (Settings button or F1): menu size, colour-blind colours, high-contrast
    /// menus, camera speeds, and every key. Settings are kept on this computer, not in saves.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Whether the Settings window is showing.</summary>
        public bool ShowSettings;
        private Vector2 _keysScroll, _settingsScroll;
        private string _settingsMessage = "";
        /// <summary>The key being set: which action and which of its two keys (-1 = none).</summary>
        private GameAction _captureAction;
        private int _captureSlot = -1;

        /// <summary>The menu size being dragged: it only takes effect when the slider is let go, so the window does not move under the mouse.</summary>
        private float _uiScaleDraft = -1f;

        public void OpenSettings(bool open)
        {
            if (!open && ShowSettings) GameSettings.Save();
            ShowSettings = open;
            StopCapture();
            _settingsMessage = "";
            _uiScaleDraft = -1f;
            if (open) { ShowNewGame = false; ShowFiles = false; }
        }

        private void OnApplicationQuit()
        {
            if (ShowSettings) GameSettings.Save();
        }

        private void StopCapture()
        {
            _captureSlot = -1;
            GameSettings.Capturing = false;
        }

        /// <summary>While a key is being set, the next key pressed is taken (Esc cancels, Backspace clears).</summary>
        private void HandleCaptureKey()
        {
            if (_captureSlot < 0) return;
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            if (e.keyCode == KeyCode.Escape) _settingsMessage = "Nothing changed.";
            else if (e.keyCode == KeyCode.Backspace)
            {
                GameSettings.Keys.Clear(_captureAction, _captureSlot);
                _settingsMessage = KeyBindings.Name(_captureAction) + ": key cleared.";
            }
            else _settingsMessage = GameSettings.Keys.Set(_captureAction, _captureSlot, e.keyCode);
            e.Use();
            StopCapture();
            GameSettings.Save();
        }

        private void DrawSettings(float w, float h)
        {
            HandleCaptureKey();
            const float pw = 820f;
            float ph = Mathf.Min(640f, h - 130f);
            var r = Panel(new Rect((w - pw) * 0.5f, Mathf.Max(20f, (h - ph) * 0.5f - 40f), pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 10f, r.width - 24f, r.height - 20f));
            GUILayout.Label("Settings", _headerStyle);
            GUILayout.BeginHorizontal();

            // Left: how the game looks and how the camera moves.
            // It scrolls, because it has grown (sound, playtest stats, tutorial) past what fits on small screens.
            _settingsScroll = GUILayout.BeginScrollView(_settingsScroll, GUILayout.Width(384f));
            GUILayout.BeginVertical(GUILayout.Width(360f));
            GUILayout.Label("Display and accessibility", _labelStyle);
            if (_uiScaleDraft < 0f) _uiScaleDraft = GameSettings.UiScale;
            _uiScaleDraft = SettingSlider("Menu and text size", _uiScaleDraft, GameSettings.MinUiScale, GameSettings.MaxUiScale, 0.05f);
            if (GUIUtility.hotControl == 0 && !Mathf.Approximately(_uiScaleDraft, GameSettings.UiScale)) GameSettings.UiScale = _uiScaleDraft;
            GameSettings.ColourBlind = GUILayout.Toggle(GameSettings.ColourBlind, " Colour-blind friendly colours");
            GUILayout.Label("Good and bad show as blue and orange instead of green and red, in menus, bars and the heat view.", _smallStyle);
            GameSettings.HighContrast = GUILayout.Toggle(GameSettings.HighContrast, " High-contrast menus");
            GUILayout.Label("Solid dark menus with brighter, bolder text.", _smallStyle);
            GameSettings.DepthOfField = GUILayout.Toggle(GameSettings.DepthOfField, " Camera focus blur");
            GUILayout.Label("Keeps what the camera looks at sharp and softly blurs the rest, like a close-up photo.", _smallStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Camera", _labelStyle);
            GameSettings.OrbitSpeed = SettingSlider("Turn speed", GameSettings.OrbitSpeed, GameSettings.MinSpeed, GameSettings.MaxSpeed, 0.05f);
            GameSettings.PanSpeed = SettingSlider("Move speed", GameSettings.PanSpeed, GameSettings.MinSpeed, GameSettings.MaxSpeed, 0.05f);
            GameSettings.ZoomSpeed = SettingSlider("Zoom speed", GameSettings.ZoomSpeed, GameSettings.MinSpeed, GameSettings.MaxSpeed, 0.05f);
            GameSettings.InvertY = GUILayout.Toggle(GameSettings.InvertY, " Invert up and down when turning");
            GUILayout.Space(6f);
            if (GUILayout.Button("Reset display, camera and sound", GUILayout.Width(230f)))
            {
                GameSettings.ResetDisplayAndCamera();
                GameSettings.Save();
                _settingsMessage = "Display, camera and sound are back to normal.";
            }
            GUILayout.Space(8f);
            GUILayout.Label("Sound", _labelStyle);
            GameSettings.Volume = SettingSlider("Volume", GameSettings.Volume, 0f, 1f, 0.05f);
            GameSettings.Ambience = SettingSlider("Background", GameSettings.Ambience, 0f, 1f, 0.05f);
            GUILayout.Space(8f);
            GUILayout.Label("Helping test the game", _labelStyle);
            GameSettings.ShareStats = GUILayout.Toggle(GameSettings.ShareStats, " Record playtest stats");
            GUILayout.Label("A line of numbers about your tank every game minute, saved as a spreadsheet on this computer. "
                + "Nothing is sent anywhere; share the file if you want to help balance the game.", _smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Playtest stats folder", GUILayout.Width(150f))) Diagnostics.OpenFolder(Diagnostics.PlaytestFolder);
            if (GUILayout.Button("Crash reports folder", GUILayout.Width(150f))) Diagnostics.OpenFolder(Diagnostics.CrashFolder);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.Label("Tutorial", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restart tutorial", GUILayout.Width(130f)))
            {
                RestartTutorial();
                _settingsMessage = "The tutorial starts again from the first step.";
            }
            if (TutorialState != null && TutorialState.Hidden && !TutorialState.Finished && GUILayout.Button("Show tutorial", GUILayout.Width(110f)))
            {
                TutorialState.Hidden = false;
                SaveTutorial();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.Space(12f);

            // Right: the keys.
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Keys", _labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset keys", GUILayout.Width(100f)))
            {
                GameSettings.Keys.ResetAll();
                StopCapture();
                GameSettings.Save();
                _settingsMessage = "All keys are back to how the game starts.";
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Click a key, then press the new one. Esc cancels, Backspace clears it.", _smallStyle);
            _keysScroll = GUILayout.BeginScrollView(_keysScroll);
            var keys = GameSettings.Keys;
            foreach (var a in KeyBindings.Actions)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(KeyBindings.Name(a), _smallStyle, GUILayout.Width(200f));
                for (int s = 0; s < KeyBindings.Slots; s++)
                {
                    bool waiting = _captureSlot == s && _captureAction == a;
                    if (GUILayout.Button(waiting ? "press a key" : KeyBindings.KeyName(keys.Get(a, s)), GUILayout.Width(92f)))
                    {
                        GUIUtility.keyboardControl = 0;
                        _captureAction = a;
                        _captureSlot = s;
                        GameSettings.Capturing = true;
                        _settingsMessage = "Press the new key for " + KeyBindings.Name(a) + ".";
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Always the same: Esc (back to Select), Shift (turn the other way), Ctrl+Z and Ctrl+Y (undo and redo), "
                + "right mouse (turn the camera), middle mouse (move it) and the scroll wheel (zoom).", _smallStyle);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (_settingsMessage.Length > 0) GUILayout.Label(_settingsMessage, _smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Settings are kept on this computer, not in your saves.   Version " + Application.version, _smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(70f))) OpenSettings(false);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>A labelled slider that shows its value as a percentage and moves in steps.</summary>
        private float SettingSlider(string label, float value, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _smallStyle, GUILayout.Width(130f));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(150f));
            v = Mathf.Round(v / step) * step;
            GUILayout.Label(Mathf.RoundToInt(v * 100f) + "%", _smallStyle, GUILayout.Width(50f));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
