using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Opt-in playtest stats: a line of numbers about the tank every game minute, written to a file
    /// on the player's own computer (a CSV file that opens in any spreadsheet). Nothing is sent
    /// anywhere; the player shares the file if they want to. It holds no names, paths or anything
    /// personal, only how the tank is doing, so balance can be tuned from real play.
    /// </summary>
    public static class PlaytestLog
    {
        /// <summary>Game seconds between rows.</summary>
        public const double Every = 60.0;

        public const string Header =
            "minute,speed,mode,frogs,young,insects,kinds,hunger,thirst,comfort,stress,money,reputation,research,"
            + "eggs_laid,hatched,deaths,escaped,rehomed,sold_eggs,rides,parts,devices,dirt,alerts";

        /// <summary>One row for the tank as it is now.</summary>
        public static string Row(SimulationLoop loop)
        {
            var w = loop.World;
            var a = w.Agents;
            int frogs = 0, young = 0, insects = 0;
            float hunger = 0f, thirst = 0f, comfort = 0f, stress = 0f;
            var kinds = new HashSet<int>();
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Insect) { insects++; continue; }
                if (!a.IsLandFrog(i)) { young++; continue; }
                frogs++;
                hunger += a.Hunger[i];
                thirst += a.Thirst[i];
                comfort += a.Comfort[i];
                stress += a.Stress[i];
                kinds.Add(Lineage.TypeOf(a, i));
            }
            float n = Math.Max(1, frogs);
            LifecycleSystem life = null;
            foreach (var e in loop.Systems) if (e.System is LifecycleSystem l) life = l;
            int rides = 0;
            foreach (int r in w.PlayRides) rides += r;
            int research = 0;
            for (uint bits = w.Economy.Researched; bits != 0; bits &= bits - 1) research++;
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                (w.SimTime / 60.0).ToString("0.0", c),
                loop.TimeScale.ToString("0.##", c),
                w.InScenario ? "scenario" : w.Economy.Enabled ? "money" : "sandbox",
                frogs.ToString(c), young.ToString(c), insects.ToString(c), kinds.Count.ToString(c),
                (hunger / n).ToString("0.00", c), (thirst / n).ToString("0.00", c),
                (comfort / n).ToString("0.00", c), (stress / n).ToString("0.00", c),
                w.Economy.Money.ToString("0", c), w.Economy.Reputation.ToString("0.0", c), research.ToString(c),
                (life?.EggsLaid ?? 0).ToString(c), (life?.Hatched ?? 0).ToString(c), (life?.Deaths ?? 0).ToString(c),
                w.Escaped.Count.ToString(c), w.Rehomed.ToString(c), w.EggsCollected.ToString(c),
                rides.ToString(c), w.Parts.Count.ToString(c), w.Devices.Count.ToString(c),
                w.Dirt.ToString("0", c), w.Alerts.All.Count.ToString(c));
        }

        /// <summary>
        /// Watches the game clock and says when the next row is due. Starts a fresh count for each
        /// new tank (new game or load).
        /// </summary>
        public sealed class Clock
        {
            private World _world;
            private double _next;

            public bool Due(World w)
            {
                if (w != _world)
                {
                    _world = w;
                    _next = w.SimTime; // a row straight away for the new tank
                }
                if (w.SimTime < _next) return false;
                _next += Every;
                if (_next <= w.SimTime) _next = w.SimTime + Every; // after a long pause or a jump, do not catch up row by row
                return true;
            }
        }
    }

    /// <summary>
    /// A crash report: what went wrong, where in the code, and the state of the game around it, as
    /// plain text the player can read before sharing. Kept on the player's computer only.
    /// </summary>
    public static class CrashReports
    {
        /// <summary>How many reports to keep; older ones are deleted.</summary>
        public const int Keep = 20;
        public const string Prefix = "crash-";
        public const string Extension = ".txt";

        /// <summary>
        /// The text of a report. <paramref name="facts"/> are short "name: value" lines about the
        /// game (version, system, tank size); <paramref name="recent"/> is the log just before.
        /// </summary>
        public static string Build(string message, string stack, DateTime when, IEnumerable<KeyValuePair<string, string>> facts, IEnumerable<string> recent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Frog Terrarium crash report");
            sb.AppendLine("Time (UTC): " + when.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            if (facts != null)
                foreach (var f in facts) sb.AppendLine(f.Key + ": " + f.Value);
            sb.AppendLine();
            sb.AppendLine("What went wrong:");
            sb.AppendLine(string.IsNullOrEmpty(message) ? "(no message)" : message.Trim());
            sb.AppendLine();
            sb.AppendLine("Where:");
            sb.AppendLine(string.IsNullOrEmpty(stack) ? "(no stack trace)" : stack.Trim());
            if (recent != null)
            {
                sb.AppendLine();
                sb.AppendLine("Just before:");
                foreach (var line in recent) sb.AppendLine(line);
            }
            return sb.ToString();
        }

        /// <summary>A file name that sorts by time: crash-20261127-153012.txt (with -2, -3 for more in the same second).</summary>
        public static string FileName(DateTime when, int same = 1) =>
            Prefix + when.ToUniversalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + (same > 1 ? "-" + same : "") + Extension;

        /// <summary>
        /// The same problem gives the same signature (its message and the first line of where it
        /// happened), so a bug that repeats every frame writes one report, not thousands.
        /// </summary>
        public static string Signature(string message, string stack)
        {
            string first = stack ?? "";
            int nl = first.IndexOf('\n');
            if (nl >= 0) first = first.Substring(0, nl);
            return (message ?? "").Trim() + "|" + first.Trim();
        }

        /// <summary>Which report files to delete so only the newest <see cref="Keep"/> stay.</summary>
        public static List<string> ToDelete(IEnumerable<string> names, int keep = Keep)
        {
            var reports = new List<string>();
            foreach (var n in names)
                if (n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Extension, StringComparison.Ordinal)) reports.Add(n);
            reports.Sort(StringComparer.Ordinal);
            int extra = reports.Count - keep;
            return extra > 0 ? reports.GetRange(0, extra) : new List<string>();
        }
    }
}
