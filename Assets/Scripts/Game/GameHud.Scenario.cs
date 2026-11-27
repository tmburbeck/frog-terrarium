using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Scenarios on screen: the Scenarios tab of the New game window, the goal tracker at the top
    /// left while one is played, and the result window when it is won or lost. The best stars for
    /// each scenario are remembered on this computer.
    /// </summary>
    public sealed partial class GameHud
    {
        private bool _scenarioTab;
        private int _pickedScenario;
        private bool _trackerOpen = true;
        /// <summary>The scenario state whose result window was closed, so it is not shown again.</summary>
        private ScenarioState _resultSeen;

        private static string BestKey(byte id) => "FrogTerrarium.Scenario." + id + ".Stars";

        /// <summary>The most stars ever won on this scenario on this computer (0 if never won).</summary>
        public static int BestStars(byte id) => PlayerPrefs.GetInt(BestKey(id), 0);

        private static string StarText(int stars) => stars <= 0 ? "not won yet" : new string('*', stars) + new string('-', 3 - stars);

        /// <summary>The tab buttons at the top of the New game window. Returns true when the Scenarios tab is showing.</summary>
        private bool DrawNewGameTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_scenarioTab, "Free game", GUI.skin.button, GUILayout.Width(140f))) _scenarioTab = false;
            if (GUILayout.Toggle(_scenarioTab, "Scenarios", GUI.skin.button, GUILayout.Width(140f))) _scenarioTab = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            return _scenarioTab;
        }

        private void DrawScenarioPicker()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(250f));
            for (int k = 0; k < Scenarios.All.Length; k++)
            {
                var s = Scenarios.All[k];
                string label = s.Name + "   " + StarText(BestStars(s.Id)).Replace("not won yet", "");
                if (GUILayout.Toggle(_pickedScenario == k, label, GUI.skin.button, GUILayout.Width(240f))) _pickedScenario = k;
            }
            GUILayout.EndVertical();

            var info = Scenarios.All[Mathf.Clamp(_pickedScenario, 0, Scenarios.All.Length - 1)];
            GUILayout.BeginVertical(GUILayout.Width(460f));
            GUILayout.Label(info.Name, _headerStyle);
            GUILayout.Label(info.Description, _smallStyle);
            GUILayout.Space(4f);
            GUILayout.Label(string.Format("{0} biome, {1} frogs, {2}.", info.Map.Info.Name, info.Frogs,
                info.MoneyMode ? "money mode with " + Economy.Format(info.StartMoney) : "sandbox (everything free)"), _smallStyle);
            GUILayout.Label(info.TimeLimitMinutes > 0f
                ? "Time limit: " + info.TimeLimitMinutes.ToString("0") + " minutes of game time. Finish in half the time for 3 stars."
                : "No time limit.", _smallStyle);
            if (info.MaxEscapes >= 0) GUILayout.Label("Lost if more than " + info.MaxEscapes + " frogs escape.", _smallStyle);
            GUILayout.Label("Also lost if no frogs are left.", _smallStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Goals", _labelStyle);
            foreach (var g in info.Goals) GUILayout.Label("• " + g.Describe(), _smallStyle);
            if (!string.IsNullOrEmpty(info.Hint)) GUILayout.Label("Tip: " + info.Hint, _smallStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Best: " + StarText(BestStars(info.Id)), _smallStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        /// <summary>Starts the scenario picked in the Scenarios tab.</summary>
        private void StartPickedScenario()
        {
            var info = Scenarios.All[Mathf.Clamp(_pickedScenario, 0, Scenarios.All.Length - 1)];
            runner.StartScenario(info);
            _trackerOpen = true;
            tools.LastAction = runner.LastMessage;
            ShowNewGame = false;
        }

        /// <summary>The goal tracker under the money bar, and the result window once the scenario ends.</summary>
        private void DrawScenario(World world, float w, float h)
        {
            var s = world.Scenario;
            var info = s?.Info;
            if (info == null) return;
            if (s.Status == ScenarioStatus.Won)
            {
                int stars = Scenarios.Stars(world);
                if (stars > BestStars(s.Id)) { PlayerPrefs.SetInt(BestKey(s.Id), stars); PlayerPrefs.Save(); }
            }
            if (!s.Active && _resultSeen != s) DrawScenarioResult(world, w, h);

            // The tracker hides while a menu on the left is open, so they don't overlap.
            if (ShowHealth || ShowMoney || ShowBuild || ShowPopulation || ShowNewGame || ShowFiles || ShowSettings) return;
            float ph = _trackerOpen ? 60f + 22f * info.Goals.Length + (string.IsNullOrEmpty(info.Hint) ? 0f : 44f) : 34f;
            var r = Panel(new Rect(12f, 54f, MoneyBarWidth * 0.75f, ph));
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 5f, r.width - 16f, r.height - 10f));
            GUILayout.BeginHorizontal();
            string status = s.Status == ScenarioStatus.Won ? "won " + StarText(Scenarios.Stars(world))
                          : s.Status == ScenarioStatus.Lost ? "lost"
                          : Scenarios.MinutesLeft(world) >= 0 ? TimeText(Scenarios.MinutesLeft(world)) + " left" : "no time limit";
            GUILayout.Label(info.Name + ": " + status, _labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_trackerOpen ? "Hide" : "Goals", GUILayout.Width(56f))) _trackerOpen = !_trackerOpen;
            GUILayout.EndHorizontal();
            if (_trackerOpen)
            {
                for (int k = 0; k < info.Goals.Length; k++)
                {
                    var g = info.Goals[k];
                    bool done = s.IsDone(k);
                    float target = Scenarios.Target(g);
                    float now = Mathf.Min(Scenarios.Progress(world, k), target);
                    string amount = g.Kind == GoalKind.Money ? Economy.Format(now) + " / " + Economy.Format(target)
                                  : g.Kind == GoalKind.HealthFor || g.Kind == GoalKind.Survive ? TimeText(now) + " / " + TimeText(target)
                                  : Mathf.FloorToInt(now) + " / " + Mathf.RoundToInt(target);
                    var old = GUI.color;
                    if (done) GUI.color = GameSettings.Good;
                    GUILayout.Label((done ? "[done] " : "[  ] ") + g.Describe() + (done ? "" : "   (" + amount + ")"), _smallStyle);
                    GUI.color = old;
                }
                if (info.MaxEscapes >= 0) GUILayout.Label("Escapes: " + s.Escapes + " of " + info.MaxEscapes + " allowed", _smallStyle);
                if (!string.IsNullOrEmpty(info.Hint)) GUILayout.Label("Tip: " + info.Hint, _smallStyle);
            }
            GUILayout.EndArea();
        }

        private static string TimeText(double minutes)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt((float)(minutes * 60.0)));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        private void DrawScenarioResult(World world, float w, float h)
        {
            var s = world.Scenario;
            var info = s.Info;
            const float pw = 460f, ph = 220f;
            var r = Panel(new Rect((w - pw) * 0.5f, (h - ph) * 0.4f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 14f, r.y + 10f, r.width - 28f, r.height - 20f));
            bool won = s.Status == ScenarioStatus.Won;
            GUILayout.Label(won ? "Scenario complete!" : "Scenario lost", _headerStyle);
            GUILayout.Label(info.Name, _labelStyle);
            if (won)
            {
                GUILayout.Label("Stars: " + StarText(Scenarios.Stars(world)) + "   Best: " + StarText(BestStars(s.Id)), _labelStyle);
                GUILayout.Label("Finished in " + TimeText(Scenarios.MinutesPlayed(world)) + " of game time.", _smallStyle);
            }
            else GUILayout.Label(s.Reason, _labelStyle);
            GUILayout.Label("You can keep playing this tank as long as you like.", _smallStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep playing", GUILayout.Width(120f))) _resultSeen = s;
            if (GUILayout.Button("Try again", GUILayout.Width(100f)))
            {
                runner.StartScenario(info);
                _trackerOpen = true;
            }
            if (GUILayout.Button("Scenarios", GUILayout.Width(100f)))
            {
                _resultSeen = s;
                OpenNewGame(true);
                _scenarioTab = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
