using System;
using System.Text.RegularExpressions;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The tutorial panel at the bottom right. It starts by itself the first time the game runs,
    /// shows one step at a time and moves on once the step is done. Progress is kept on this
    /// computer (like Settings), so it carries across new games and loads. Settings can restart it.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Where the progress is kept. Not under the Settings prefix, so resetting settings leaves it alone.</summary>
        public const string TutorialKey = "FrogTerrarium.Tutorial";

        /// <summary>How far the player has got (null until the HUD first draws).</summary>
        public TutorialProgress TutorialState { get; private set; }
        private string _tutorialSaved;
        private bool _tutorialFinishSeen;

        // What the tutorial compares against to see that the player did something.
        private bool _cameraKnown;
        private Vector3 _cameraStart;
        private Quaternion _cameraStartTurn;
        private int _savesAtStart = -1;

        private static readonly Regex KeyToken = new Regex(@"\{key:(\w+)\}");

        public void RestartTutorial()
        {
            if (TutorialState == null) LoadTutorial();
            TutorialState.Restart();
            _tutorialFinishSeen = false;
            _cameraKnown = false;
            _savesAtStart = runner != null ? runner.Saves : -1;
            SaveTutorial();
        }

        private void LoadTutorial()
        {
            _tutorialSaved = PlayerPrefs.GetString(TutorialKey, "");
            TutorialState = TutorialProgress.FromText(_tutorialSaved);
            _tutorialFinishSeen = TutorialState.Finished; // a tutorial finished on an earlier run stays quiet
        }

        private void SaveTutorial()
        {
            string text = TutorialState.ToText();
            if (text == _tutorialSaved) return;
            _tutorialSaved = text;
            PlayerPrefs.SetString(TutorialKey, text);
            PlayerPrefs.Save();
        }

        /// <summary>Looks at what the player is doing on screen and tells the tutorial.</summary>
        private void NoteTutorialEvents(World world)
        {
            var t = TutorialState;
            var cam = Camera.main;
            if (cam != null)
            {
                if (!_cameraKnown)
                {
                    _cameraStart = cam.transform.position;
                    _cameraStartTurn = cam.transform.rotation;
                    _cameraKnown = true;
                }
                else if (Vector3.Distance(cam.transform.position, _cameraStart) > 0.75f || Quaternion.Angle(cam.transform.rotation, _cameraStartTurn) > 12f)
                    t.Note(TutorialEvent.CameraMoved);
            }
            int frog = tools.SelectedAgentId != 0 ? world.Agents.IndexOf(tools.SelectedAgentId) : -1;
            if (frog >= 0 && world.Agents.Kind[frog] == AgentKind.Frog) t.Note(TutorialEvent.FrogSelected);
            if (ShowHealth) t.Note(TutorialEvent.HealthOpened);
            if (ShowBuild) t.Note(TutorialEvent.BuildOpened);
            if (runner.Sim.TimeScale > 1f) t.Note(TutorialEvent.SpedUp);
            if (_savesAtStart < 0) _savesAtStart = runner.Saves;
            else if (runner.Saves > _savesAtStart) t.Note(TutorialEvent.Saved);
        }

        /// <summary>Swaps {key:Speed2} and the like for the keys the player has set.</summary>
        private static string WithKeys(string text) =>
            KeyToken.Replace(text, m => Enum.TryParse(m.Groups[1].Value, out GameAction a) ? GameSettings.KeyFor(a) : m.Value);

        /// <summary>Checks the current step once a frame (in Update, so it works even when nothing is drawn).</summary>
        private void Update()
        {
            if (runner == null || runner.Sim == null || tools == null) return;
            if (TutorialState == null) LoadTutorial();
            var t = TutorialState;
            if (t.Hidden || t.Finished) return;
            NoteTutorialEvents(runner.Sim.World);
            t.Update(runner.Sim.World, Time.unscaledDeltaTime);
            SaveTutorial();
        }

        private void DrawTutorial(World world, float w, float h)
        {
            if (TutorialState == null) return;
            var t = TutorialState;
            bool finishing = t.Finished && !_tutorialFinishSeen;
            if (t.Hidden || (t.Finished && !finishing)) return;
            // Big windows in the middle of the screen cover the tank anyway; stay out of their way.
            if (ShowNewGame || ShowFiles || ShowSettings) return;

            const float pw = 380f, ph = 170f;
            var rect = new Rect(w - pw - 12f, h - 104f - ph - 14f, pw, ph);
            // Slide left of anything on the right (the inspectors) rather than cover it.
            foreach (var other in _panels)
                if (other.Overlaps(rect) && other.x > w * 0.5f) rect.x = other.x - pw - 10f;
            var r = Panel(rect);
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f));

            if (finishing)
            {
                GUILayout.Label("You're all set", _headerStyle);
                GUILayout.Label("That's the basics. From here it's your tank: try new species, breed for odd traits, build "
                    + "playgrounds and share what you make from Files. Restart this tutorial any time from Settings.", _smallStyle);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(70f)))
                {
                    _tutorialFinishSeen = true;
                    t.Hidden = true;
                    SaveTutorial();
                }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            var step = t.Current;
            GUILayout.BeginHorizontal();
            GUILayout.Label(step.Title, _headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format("Step {0} of {1}", t.Step + 1, Simulation.Tutorial.Steps.Length), _smallStyle);
            GUILayout.EndHorizontal();
            if (t.ShowingPraise)
            {
                var old = GUI.color;
                GUI.color = GameSettings.Good;
                GUILayout.Label(step.Praise, _labelStyle);
                GUI.color = old;
            }
            else GUILayout.Label(WithKeys(step.Text(world)), _smallStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Skip tutorial", GUILayout.Width(100f)))
            {
                t.SkipAll();
                _tutorialFinishSeen = true;
                SaveTutorial();
            }
            if (GUILayout.Button("Hide", GUILayout.Width(56f)))
            {
                t.Hidden = true;
                SaveTutorial();
            }
            GUILayout.FlexibleSpace();
            GUI.enabled = !t.ShowingPraise;
            if (GUILayout.Button(t.Step == Simulation.Tutorial.Steps.Length - 1 ? "Finish" : "Skip step", GUILayout.Width(84f)))
            {
                t.SkipStep();
                SaveTutorial();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
