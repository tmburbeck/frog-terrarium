using System;
using System.Collections.Generic;
using System.IO;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Crash reports and opt-in playtest stats, both kept on the player's own computer (nothing is
    /// sent anywhere). When something goes wrong in the code, a readable report is written to the
    /// crash-reports folder (once per problem, the newest 20 kept) and a note on screen says so.
    /// If the player switches on "Record playtest stats" in Settings, a line of numbers about the
    /// tank is added to a spreadsheet file every game minute (see PlaytestLog).
    /// </summary>
    public sealed class Diagnostics : MonoBehaviour
    {
        public SimulationRunner runner;

        public static string CrashFolder => Path.Combine(Application.persistentDataPath, "crash-reports");
        public static string PlaytestFolder => Path.Combine(Application.persistentDataPath, "playtest");

        /// <summary>The report written most recently this session (null if none), and when (real seconds).</summary>
        public string LastReport { get; private set; }
        public float LastReportTime { get; private set; } = float.NegativeInfinity;
        public int ReportsWritten { get; private set; }
        /// <summary>The playtest file this session writes to (null until the first row).</summary>
        public string PlaytestFile { get; private set; }
        public int PlaytestRows { get; private set; }

        /// <summary>Hides the on-screen note about the last report.</summary>
        public void DismissNotice() => LastReportTime = float.NegativeInfinity;

        private const int RecentLines = 30;
        private readonly Queue<string> _recent = new Queue<string>();
        private readonly HashSet<string> _seen = new HashSet<string>();
        private readonly PlaytestLog.Clock _clock = new PlaytestLog.Clock();
        private bool _writing;

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string message, string stack, LogType type)
        {
            if (_writing) return; // a problem while writing a report must not write another
            string line = DateTime.Now.ToString("HH:mm:ss") + " " + type + ": " + FirstLine(message);
            _recent.Enqueue(line);
            while (_recent.Count > RecentLines) _recent.Dequeue();
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (!_seen.Add(CrashReports.Signature(message, stack))) return;
            WriteReport(message, stack);
        }

        /// <summary>Writes a report now (also used by tests). Returns its path, or null if it could not be written.</summary>
        public string WriteReport(string message, string stack)
        {
            _writing = true;
            try
            {
                Directory.CreateDirectory(CrashFolder);
                var now = DateTime.UtcNow;
                string path = Path.Combine(CrashFolder, CrashReports.FileName(now));
                for (int k = 2; File.Exists(path) && k < 100; k++) path = Path.Combine(CrashFolder, CrashReports.FileName(now, k));
                File.WriteAllText(path, CrashReports.Build(message, stack, now, Facts(), _recent));
                var names = new List<string>();
                foreach (var f in Directory.GetFiles(CrashFolder)) names.Add(Path.GetFileName(f));
                foreach (var old in CrashReports.ToDelete(names)) File.Delete(Path.Combine(CrashFolder, old));
                LastReport = path;
                LastReportTime = Time.unscaledTime;
                ReportsWritten++;
                return path;
            }
            catch (Exception)
            {
                return null; // a full or read-only disk: carry on without a report
            }
            finally
            {
                _writing = false;
            }
        }

        /// <summary>Short facts about the game and computer. Nothing personal: no names, folders or accounts.</summary>
        private IEnumerable<KeyValuePair<string, string>> Facts()
        {
            yield return Fact("Game version", Application.version);
            yield return Fact("Unity", Application.unityVersion);
            yield return Fact("System", SystemInfo.operatingSystem);
            yield return Fact("Graphics", SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsMemorySize + " MB)");
            yield return Fact("Memory", SystemInfo.systemMemorySize + " MB");
            yield return Fact("Screen", Screen.width + " x " + Screen.height);
            var sim = runner != null ? runner.Sim : null;
            if (sim == null) yield break;
            var w = sim.World;
            yield return Fact("Tank", string.Format("{0:0} x {1:0} m, {2} animals, {3} parts, {4} devices, {5:0.0} game minutes, speed x{6}",
                w.HalfSizeX * 2f, w.HalfSizeZ * 2f, w.Agents.Count, w.Parts.Count, w.Devices.Count, w.SimTime / 60.0, sim.TimeScale));
            yield return Fact("Mode", w.InScenario ? "scenario" : w.Economy.Enabled ? "money" : "sandbox");
        }

        private static KeyValuePair<string, string> Fact(string k, string v) => new KeyValuePair<string, string>(k, v);

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOf('\n');
            return nl >= 0 ? s.Substring(0, nl) : s;
        }

        private void Update()
        {
            if (!GameSettings.ShareStats || runner == null || runner.Sim == null || PerformanceBenchmark.AnyRunning) return;
            var w = runner.Sim.World;
            if (!_clock.Due(w)) return;
            WritePlaytestRow();
        }

        /// <summary>Adds one row of playtest stats now (starting this session's file if needed).</summary>
        public void WritePlaytestRow()
        {
            try
            {
                if (PlaytestFile == null)
                {
                    Directory.CreateDirectory(PlaytestFolder);
                    PlaytestFile = Path.Combine(PlaytestFolder, "playtest-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".csv");
                    File.WriteAllText(PlaytestFile, PlaytestLog.Header + "\n");
                }
                File.AppendAllText(PlaytestFile, PlaytestLog.Row(runner.Sim) + "\n");
                PlaytestRows++;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Playtest stats could not be written: " + e.Message);
            }
        }

        /// <summary>Opens a folder in the computer's file browser (making it first if needed).</summary>
        public static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Application.OpenURL("file:///" + path.Replace('\\', '/'));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not open " + path + ": " + e.Message);
            }
        }
    }
}
