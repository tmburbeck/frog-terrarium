using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// A saved build: a set of parts with their positions measured from the build's middle, so it
    /// can be placed anywhere, turned, and shared. A blueprint is written as a short line of text
    /// starting "FTBP1:" (letters and numbers only), which can be saved as a .ftbp file, pasted into
    /// a message, or copied to the clipboard and pasted into someone else's game.
    /// </summary>
    public sealed class Blueprint
    {
        public const string TextPrefix = "FTBP1:";
        public const int MaxNameLength = 40;
        private const int FormatVersion = 1;

        public string Name = "";
        /// <summary>The parts, with X and Z measured from the middle of the build and OnPartId pointing at the index + 1 of the part below (0 = ground).</summary>
        public readonly List<PlacedPart> Parts = new List<PlacedPart>();

        /// <summary>
        /// Makes a blueprint of some placed parts (or all of them when ids is null). Parts resting on
        /// a part that is left out of the blueprint stand on the ground in it.
        /// </summary>
        public static Blueprint FromParts(PartStore store, string name, ICollection<int> ids = null)
        {
            var bp = new Blueprint { Name = CleanName(name) };
            var chosen = new List<PlacedPart>();
            foreach (var p in store.All) if (ids == null || ids.Contains(p.Id)) chosen.Add(p);
            if (chosen.Count == 0) return bp;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in chosen)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Z < minZ) minZ = p.Z;
                if (p.Z > maxZ) maxZ = p.Z;
            }
            // Measure from the middle, rounded to the grid so grid builds stay on the grid when placed.
            float cx = (float)Math.Round(0.5f * (minX + maxX) / Simulation.Parts.GridStep) * Simulation.Parts.GridStep;
            float cz = (float)Math.Round(0.5f * (minZ + maxZ) / Simulation.Parts.GridStep) * Simulation.Parts.GridStep;
            // Order so every part comes after the part it rests on.
            var ordered = new List<PlacedPart>();
            var done = new HashSet<int>();
            while (ordered.Count < chosen.Count)
            {
                int before = ordered.Count;
                foreach (var p in chosen)
                {
                    if (done.Contains(p.Id)) continue;
                    bool belowReady = p.OnPartId == 0 || done.Contains(p.OnPartId) || !chosen.Exists(q => q.Id == p.OnPartId);
                    if (!belowReady) continue;
                    ordered.Add(p);
                    done.Add(p.Id);
                }
                if (ordered.Count == before) break; // a loop in the data; should never happen
            }
            var indexOf = new Dictionary<int, int>();
            foreach (var p in ordered)
            {
                var c = p.Clone();
                c.X -= cx;
                c.Z -= cz;
                c.OnPartId = indexOf.TryGetValue(p.OnPartId, out int below) ? below + 1 : 0;
                indexOf[p.Id] = bp.Parts.Count;
                c.Id = 0;
                bp.Parts.Add(c);
            }
            return bp;
        }

        /// <summary>
        /// The parts this blueprint would make if placed with its middle at (x, z), turned by rotation
        /// degrees. OnPartId still holds index + 1 of the part below.
        /// </summary>
        public List<PlacedPart> PartsAt(float x, float z, float rotation)
        {
            var list = new List<PlacedPart>(Parts.Count);
            foreach (var p in Parts)
            {
                var c = p.Clone();
                Simulation.Parts.DirToWorld(rotation, p.X, p.Z, out float rx, out float rz);
                c.X = x + rx;
                c.Z = z + rz;
                c.Rotation = Simulation.Parts.NormaliseAngle(p.Rotation + rotation);
                list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// Places the whole blueprint with its middle at (x, z), turned by rotation degrees. Either every
        /// part goes in or none does; if one would not fit, nothing changes and the reason is given.
        /// Returns the number of parts placed.
        /// </summary>
        public int PlaceInto(PartStore store, TerrainGrid terrain, float x, float z, float rotation, out string why)
        {
            why = "";
            if (Parts.Count == 0) { why = "The blueprint is empty."; return 0; }
            if (store.Count + Parts.Count > PartStore.MaxParts) { why = "Not enough room: the tank holds at most " + PartStore.MaxParts + " parts."; return 0; }
            var list = PartsAt(x, z, rotation);
            var placed = new List<int>();
            var idOf = new int[list.Count];
            for (int k = 0; k < list.Count; k++)
            {
                var c = list[k];
                c.OnPartId = c.OnPartId > 0 && c.OnPartId - 1 < k ? idOf[c.OnPartId - 1] : 0;
                var p = store.Place(c, terrain, out why);
                if (p == null)
                {
                    foreach (int id in placed) store.Remove(id);
                    why = "Part " + (k + 1) + " (" + c.Info.Name.ToLowerInvariant() + ") does not fit here: " + why;
                    return 0;
                }
                idOf[k] = p.Id;
                placed.Add(p.Id);
            }
            return placed.Count;
        }

        // ------------------------------------------------------------------ text form

        /// <summary>The blueprint as one line of text, for files, the clipboard and messages.</summary>
        public string ToText()
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    w.Write(FormatVersion);
                    w.Write(Name ?? "");
                    w.Write(Parts.Count);
                    foreach (var p in Parts)
                    {
                        w.Write((byte)p.Kind);
                        w.Write(p.X); w.Write(p.Z);
                        w.Write(p.Rotation);
                        w.Write(p.Scale);
                        w.Write(p.R); w.Write(p.G); w.Write(p.B);
                        w.Write(p.OnPartId);
                    }
                }
                return TextPrefix + Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// Reads a blueprint from its text form. Extra spaces and line breaks (from pasting) are
        /// ignored. Throws <see cref="SaveFormatException"/> with a plain message if the text is not a
        /// blueprint or is damaged.
        /// </summary>
        public static Blueprint FromText(string text)
        {
            text = (text ?? "").Trim();
            int start = text.IndexOf(TextPrefix, StringComparison.Ordinal);
            if (start < 0) throw new SaveFormatException("That is not a Frog Terrarium blueprint (it should start with " + TextPrefix + ").");
            var sb = new StringBuilder();
            foreach (char ch in text.Substring(start + TextPrefix.Length)) if (!char.IsWhiteSpace(ch)) sb.Append(ch);
            byte[] data;
            try { data = Convert.FromBase64String(sb.ToString()); }
            catch (FormatException) { throw new SaveFormatException("The blueprint text is damaged (part of it may be missing)."); }
            try
            {
                using (var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
                {
                    int version = r.ReadInt32();
                    if (version > FormatVersion) throw new SaveFormatException("This blueprint was made by a newer game.");
                    if (version < 1) throw new SaveFormatException("The blueprint text is damaged.");
                    var bp = new Blueprint { Name = CleanName(r.ReadString()) };
                    int n = r.ReadInt32();
                    if (n < 0 || n > PartStore.MaxParts) throw new SaveFormatException("The blueprint text is damaged.");
                    for (int k = 0; k < n; k++)
                    {
                        byte kind = r.ReadByte();
                        if (kind >= Simulation.Parts.All.Length) throw new SaveFormatException("The blueprint uses a part this game does not have.");
                        var p = new PlacedPart
                        {
                            Kind = (PartKind)kind,
                            X = r.ReadSingle(), Z = r.ReadSingle(),
                            Rotation = r.ReadSingle(),
                            Scale = r.ReadSingle(),
                            R = r.ReadByte(), G = r.ReadByte(), B = r.ReadByte(),
                            OnPartId = r.ReadInt32(),
                        };
                        if (float.IsNaN(p.X) || float.IsNaN(p.Z) || float.IsNaN(p.Rotation) || float.IsNaN(p.Scale)
                            || Math.Abs(p.X) > 1000f || Math.Abs(p.Z) > 1000f)
                            throw new SaveFormatException("The blueprint text is damaged.");
                        if (p.OnPartId < 0 || p.OnPartId > k) p.OnPartId = 0;
                        p.Scale = Simulation.Parts.ClampScale(p.Scale);
                        p.Rotation = Simulation.Parts.NormaliseAngle(p.Rotation);
                        bp.Parts.Add(p);
                    }
                    return bp;
                }
            }
            catch (EndOfStreamException) { throw new SaveFormatException("The blueprint text is cut short."); }
        }

        /// <summary>A name safe to use as a file name: letters, numbers, spaces, dashes and underscores only.</summary>
        public static string CleanName(string name)
        {
            var sb = new StringBuilder();
            foreach (char ch in name ?? "")
                if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_') sb.Append(ch);
            string s = sb.ToString().Trim();
            if (s.Length > MaxNameLength) s = s.Substring(0, MaxNameLength).Trim();
            return s.Length == 0 ? "Untitled build" : s;
        }
    }
}
