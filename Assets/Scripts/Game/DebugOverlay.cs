using System.Text;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// On-screen numbers for spotting performance problems early:
    /// frame time, agent count, and how long each simulation system takes per tick.
    /// Press O (or F3, or the Stats button) to show or hide it.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        public SimulationRunner runner;
        public PerformanceBenchmark benchmark;
        public bool visible = true;

        private float _smoothedFrameMs;
        private float _worstFrameMs;
        private float _worstResetTimer;
        private readonly StringBuilder _sb = new StringBuilder(512);
        private GUIStyle _style;

        public float SmoothedFrameMs => _smoothedFrameMs;

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.O)) visible = !visible;
#endif
            float ms = Time.unscaledDeltaTime * 1000f;
            _smoothedFrameMs = _smoothedFrameMs <= 0f ? ms : Mathf.Lerp(_smoothedFrameMs, ms, 0.1f);
            _worstFrameMs = Mathf.Max(_worstFrameMs, ms);
            _worstResetTimer += Time.unscaledDeltaTime;
            if (_worstResetTimer > 2f) { _worstResetTimer = 0f; _worstFrameMs = ms; }
        }

        /// <summary>The overlay text, also used by tests.</summary>
        public string BuildText()
        {
            _sb.Clear();
            float fps = _smoothedFrameMs > 0f ? 1000f / _smoothedFrameMs : 0f;
            _sb.AppendFormat("Frame  {0,6:0.00} ms  ({1:0} FPS, worst {2:0.0} ms)\n", _smoothedFrameMs, fps, _worstFrameMs);
            if (runner != null && runner.Sim != null)
            {
                var sim = runner.Sim;
                var agents = sim.World.Agents;
                int frogs = 0;
                for (int i = 0; i < agents.Count; i++) if (agents.Kind[i] == AgentKind.Frog) frogs++;
                _sb.AppendFormat("Agents {0}  (frogs {1}, insects {2})\n", agents.Count, frogs, agents.Count - frogs);
                _sb.AppendFormat("Sim    {0,6:0.000} ms this frame   speed x{1:0.#}   seed {2}\n", sim.LastAdvanceMs, sim.TimeScale, sim.World.Seed);
                foreach (var e in sim.Systems)
                    _sb.AppendFormat("  {0,-9} {1,2} Hz  {2,6:0.000} ms/tick  ticks {3}\n", e.System.Name, e.System.TicksPerSecond, e.LastTickMs, e.TotalTicks);
                if (sim.DroppedSeconds > 0) _sb.AppendFormat("  dropped {0:0.00}s of slow frames\n", sim.DroppedSeconds);
                if (!string.IsNullOrEmpty(runner.LastMessage)) _sb.Append(runner.LastMessage).Append('\n');
                if (benchmark != null && !string.IsNullOrEmpty(benchmark.Status)) _sb.Append(benchmark.Status).Append('\n');
            }
            _sb.Append("O stats  B speed test  F5 save  F9 load  P pause  1/2/3 speed  Ctrl+Z undo");
            return _sb.ToString();
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = false };
                _style.normal.textColor = Color.white;
            }
            string text = BuildText();
            var size = _style.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(10, 10, size.x + 10, size.y + 6), text, _style);
        }
    }
}
