using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Every building and decoration part in the tank, and the rules for placing them:
    ///
    /// * A part must sit fully inside the tank and must not overlap another part at the same height
    ///   (touching is fine).
    /// * A part dropped onto a stackable part (a slab, hide, pillar or platform) sits on top of it.
    /// * Parts with snap points (walls, slabs, platforms, logs...) snap to a matching snap point of a
    ///   nearby part, turning to line up with it. Grid placement also rounds the spot to 25 cm and the
    ///   turn to 45°; free placement keeps any spot and any angle.
    ///
    /// Parts also change the tank: shady parts keep the ground under them cooler, plants and moss
    /// pads dampen the air, cover (hides, logs, plants, platforms) calms frogs, and tree frogs like
    /// perches (logs, branches, plants, ramps). Saved (version 7).
    /// </summary>
    public sealed class PartStore
    {
        /// <summary>The most parts one tank can hold, so a huge build can never stall the game.</summary>
        public const int MaxParts = 600;

        private readonly List<PlacedPart> _parts = new List<PlacedPart>();
        private int _nextId = 1;

        public IReadOnlyList<PlacedPart> All => _parts;
        public int Count => _parts.Count;
        public int NextId => _nextId;
        /// <summary>Goes up by one whenever a part is added, removed or changed.</summary>
        public int Version { get; private set; }

        public PlacedPart Find(int id)
        {
            if (id == 0) return null;
            foreach (var p in _parts) if (p.Id == id) return p;
            return null;
        }

        /// <summary>
        /// Places a part if the spot is allowed (see <see cref="Check"/>). The part gets a new id.
        /// Returns null and the reason if not.
        /// </summary>
        public PlacedPart Place(PlacedPart candidate, TerrainGrid terrain, out string why)
        {
            var p = candidate.Clone();
            p.Id = 0;
            p.Scale = Parts.ClampScale(p.Scale);
            p.Rotation = Parts.NormaliseAngle(p.Rotation);
            if (!Check(p, terrain, out why)) return null;
            p.Id = _nextId++;
            _parts.Add(p);
            Version++;
            return p;
        }

        /// <summary>
        /// Removes a part. Anything stacked on it drops down onto whatever it was standing on.
        /// Returns false if there is no such part.
        /// </summary>
        public bool Remove(int id)
        {
            int i = _parts.FindIndex(p => p.Id == id);
            if (i < 0) return false;
            int below = _parts[i].OnPartId;
            _parts.RemoveAt(i);
            foreach (var p in _parts) if (p.OnPartId == id) p.OnPartId = below;
            Version++;
            return true;
        }

        /// <summary>
        /// Moves, turns or resizes a placed part to match <paramref name="target"/> (its X, Z, Rotation,
        /// Scale and OnPartId, usually set by <see cref="Snap"/>). Everything stacked on the part comes
        /// along, keeping its place on top. If anything would not fit, nothing changes and the reason
        /// is given.
        /// </summary>
        public bool Move(int id, PlacedPart target, TerrainGrid terrain, out string why)
        {
            why = "";
            var p = Find(id);
            if (p == null) { why = "That part is gone."; return false; }
            var group = new List<PlacedPart> { p };
            foreach (var q in _parts) if (q != p && IsAbove(q, p)) group.Add(q);
            if (target.OnPartId != 0)
            {
                var below = Find(target.OnPartId);
                if (below == null || group.Contains(below)) { why = "A part cannot stand on itself."; return false; }
            }
            var saved = new List<PlacedPart>(group.Count);
            foreach (var q in group) saved.Add(q.Clone());

            float scale = Parts.ClampScale(target.Scale);
            float rotation = Parts.NormaliseAngle(target.Rotation);
            float turn = rotation - p.Rotation;
            float oldX = p.X, oldZ = p.Z;
            foreach (var q in group)
            {
                if (q == p) continue;
                Parts.DirToWorld(turn, q.X - oldX, q.Z - oldZ, out float ox, out float oz);
                q.X = target.X + ox;
                q.Z = target.Z + oz;
                q.Rotation = Parts.NormaliseAngle(q.Rotation + turn);
            }
            p.X = target.X; p.Z = target.Z; p.Rotation = rotation; p.Scale = scale; p.OnPartId = target.OnPartId;
            Version++;

            foreach (var q in group)
            {
                if (Check(q, terrain, out why)) continue;
                for (int k = 0; k < group.Count; k++)
                {
                    var g = group[k];
                    var o = saved[k];
                    g.X = o.X; g.Z = o.Z; g.Rotation = o.Rotation; g.Scale = o.Scale; g.OnPartId = o.OnPartId;
                }
                Version++;
                if (q != p) why = "The " + q.Info.Name.ToLowerInvariant() + " on top would not fit: " + why;
                return false;
            }
            return true;
        }

        /// <summary>The part and everything stacked on it (the part itself first).</summary>
        public List<PlacedPart> StackFrom(int id)
        {
            var list = new List<PlacedPart>();
            var p = Find(id);
            if (p == null) return list;
            list.Add(p);
            foreach (var q in _parts) if (q != p && IsAbove(q, p)) list.Add(q);
            return list;
        }

        public void Clear()
        {
            _parts.Clear();
            _nextId = 1;
            Version++;
        }

        /// <summary>Call after changing a placed part's colour or size, so the tank effects are worked out again.</summary>
        public void Changed() => Version++;

        /// <summary>The height a part's bottom sits at: the ground under its centre, or the top of the part it is on.</summary>
        public float BaseHeight(PlacedPart p, TerrainGrid terrain)
        {
            float extra = 0f;
            var cur = p;
            for (int depth = 0; depth < 32; depth++)
            {
                var below = cur.OnPartId != 0 ? Find(cur.OnPartId) : null;
                if (below == null || below == p) return terrain.HeightAt(cur.X, cur.Z) + extra;
                extra += below.SizeY;
                cur = below;
            }
            return terrain.HeightAt(cur.X, cur.Z) + extra;
        }

        /// <summary>
        /// Whether a part may go where it is: inside the tank, under the part limit, on a stackable part
        /// if it is stacked, and not overlapping another part at the same height. Gives the reason if not.
        /// </summary>
        public bool Check(PlacedPart p, TerrainGrid terrain, out string why)
        {
            why = "";
            if (p.Id == 0 && _parts.Count >= MaxParts) { why = "The tank holds at most " + MaxParts + " parts."; return false; }
            if (p.Scale < Parts.MinScale - 1e-4f || p.Scale > Parts.MaxScale + 1e-4f) { why = "That size is not allowed."; return false; }
            var xs = new float[4];
            var zs = new float[4];
            Parts.Corners(p, xs, zs);
            for (int k = 0; k < 4; k++)
                if (Math.Abs(xs[k]) > terrain.HalfSizeX + 1e-3f || Math.Abs(zs[k]) > terrain.HalfSizeZ + 1e-3f)
                {
                    why = "It would stick out of the tank.";
                    return false;
                }
            if (p.OnPartId != 0)
            {
                var below = Find(p.OnPartId);
                if (below == null || below.Id == p.Id) { why = "The part underneath is gone."; return false; }
                if (!below.Info.Stackable) { why = "Nothing can stand on a " + below.Info.Name.ToLowerInvariant() + "."; return false; }
                if (!Parts.Contains(below, p.X, p.Z)) { why = "It would fall off the " + below.Info.Name.ToLowerInvariant() + "."; return false; }
            }
            float bottom = BaseHeight(p, terrain), top = bottom + p.SizeY;
            foreach (var q in _parts)
            {
                if (q.Id == p.Id || q.Id == p.OnPartId) continue;
                float qb = BaseHeight(q, terrain), qt = qb + q.SizeY;
                if (top - Parts.Touch <= qb || qt - Parts.Touch <= bottom) continue; // one is above the other
                if (Parts.FootprintsOverlap(p, q)) { why = "It would overlap the " + q.Info.Name.ToLowerInvariant() + "."; return false; }
            }
            return true;
        }

        /// <summary>
        /// Moves a part being placed to where it would snap: onto the highest stackable part under its
        /// centre, then (for parts with snap points) against the nearest matching snap point within
        /// <see cref="Parts.SnapRadius"/>, turning to line up. With grid on, the spot and turn are also
        /// rounded to the grid first. Returns true if it snapped to another part.
        /// </summary>
        public bool Snap(PlacedPart p, TerrainGrid terrain, bool grid)
        {
            if (grid)
            {
                float x = p.X, z = p.Z, r = p.Rotation;
                Parts.ToGrid(ref x, ref z, ref r);
                p.X = x; p.Z = z; p.Rotation = r;
            }

            // Stacking: the highest stackable part under the centre.
            p.OnPartId = 0;
            float bestTop = float.MinValue;
            foreach (var q in _parts)
            {
                if (q.Id == p.Id || !q.Info.Stackable || !Parts.Contains(q, p.X, p.Z)) continue;
                if (IsAbove(q, p)) continue; // never stack a part on something resting on it
                float top = BaseHeight(q, terrain) + q.SizeY;
                if (top > bestTop) { bestTop = top; p.OnPartId = q.Id; }
            }

            var info = p.Info;
            if (info.Sockets.Length == 0) return p.OnPartId != 0;
            float myBase = BaseHeight(p, terrain);
            float bestD = Parts.SnapRadius;
            bool snapped = false;
            float bx = p.X, bz = p.Z, br = p.Rotation;
            foreach (var q in _parts)
            {
                if (q.Id == p.Id || q.Id == p.OnPartId) continue; // never snap sideways to the part it stands on
                var qi = q.Info;
                if (qi.Sockets.Length == 0) continue;
                if (Math.Abs(BaseHeight(q, terrain) - myBase) > 0.05f) continue; // only parts at the same level
                foreach (var b in qi.Sockets)
                {
                    Parts.ToWorld(q, b.X, b.Z, out float sx, out float sz);
                    Parts.DirToWorld(q.Rotation, b.DirX, b.DirZ, out float ddx, out float ddz);
                    foreach (var s in info.Sockets)
                    {
                        // Where this socket is now, and how far from the other one.
                        Parts.ToWorld(p, s.X, s.Z, out float ax, out float az);
                        float d = (float)Math.Sqrt((ax - sx) * (ax - sx) + (az - sz) * (az - sz));
                        if (d >= bestD) continue;
                        // Turn so the sockets face each other, then move so they meet.
                        float rot = Parts.NormaliseAngle(Heading(-ddx, -ddz) - Heading(s.DirX, s.DirZ));
                        var trial = p.Clone();
                        trial.Rotation = rot;
                        trial.X = 0f; trial.Z = 0f;
                        Parts.ToWorld(trial, s.X, s.Z, out float ox, out float oz);
                        bestD = d;
                        bx = sx - ox; bz = sz - oz; br = rot;
                        snapped = true;
                    }
                }
            }
            if (snapped) { p.X = bx; p.Z = bz; p.Rotation = br; }
            return snapped || p.OnPartId != 0;
        }

        /// <summary>True if <paramref name="part"/> rests (directly or further up) on <paramref name="maybeBelow"/>.</summary>
        private bool IsAbove(PlacedPart part, PlacedPart maybeBelow)
        {
            var cur = part;
            for (int depth = 0; depth < 32 && cur.OnPartId != 0; depth++)
            {
                if (cur.OnPartId == maybeBelow.Id) return true;
                cur = Find(cur.OnPartId);
                if (cur == null) return false;
            }
            return false;
        }

        /// <summary>Compass heading of a ground direction in degrees: 0 forward (+Z), 90 right (+X).</summary>
        private static float Heading(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);

        /// <summary>The part whose footprint is under (x, z), topmost first, or null.</summary>
        public PlacedPart PartAt(float x, float z, TerrainGrid terrain, float margin = 0.05f)
        {
            PlacedPart best = null;
            float bestTop = float.MinValue;
            foreach (var p in _parts)
            {
                if (!Parts.Contains(p, x, z, margin)) continue;
                float top = BaseHeight(p, terrain) + p.SizeY;
                if (top > bestTop) { bestTop = top; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ effects on frogs

        /// <summary>Size of the cells of the cover and perch maps, in metres.</summary>
        public const float MapCell = 0.5f;
        /// <summary>A frog this close to a cover or perch part counts as under it or on it, in metres.</summary>
        public const float NearMargin = 0.25f;

        private byte[] _map;
        private int _mapVersion = -1, _mapW, _mapH;
        private float _mapHalfX, _mapHalfZ;
        private const byte CoverBit = 1, PerchBit = 2;

        /// <summary>True if a spot is under or right next to cover (a hide, log, plant, fern or platform).</summary>
        public bool CoverAt(float x, float z, float halfSizeX, float halfSizeZ) => (MapAt(x, z, halfSizeX, halfSizeZ) & CoverBit) != 0;

        /// <summary>True if a spot is on or right next to a perch (a log, branch, plant or ramp).</summary>
        public bool PerchAt(float x, float z, float halfSizeX, float halfSizeZ) => (MapAt(x, z, halfSizeX, halfSizeZ) & PerchBit) != 0;

        private byte MapAt(float x, float z, float halfX, float halfZ)
        {
            if (_parts.Count == 0) return 0;
            if (_map == null || _mapVersion != Version || _mapHalfX != halfX || _mapHalfZ != halfZ) BuildMap(halfX, halfZ);
            int cx = (int)((x + halfX) / MapCell), cz = (int)((z + halfZ) / MapCell);
            if (cx < 0 || cz < 0 || cx >= _mapW || cz >= _mapH) return 0;
            return _map[cz * _mapW + cx];
        }

        private void BuildMap(float halfX, float halfZ)
        {
            _mapHalfX = halfX; _mapHalfZ = halfZ;
            _mapW = Math.Max(1, (int)Math.Ceiling(2f * halfX / MapCell));
            _mapH = Math.Max(1, (int)Math.Ceiling(2f * halfZ / MapCell));
            if (_map == null || _map.Length != _mapW * _mapH) _map = new byte[_mapW * _mapH];
            else Array.Clear(_map, 0, _map.Length);
            foreach (var p in _parts)
            {
                var info = p.Info;
                byte bits = (byte)((info.Cover ? CoverBit : 0) | (info.Perch ? PerchBit : 0));
                if (bits == 0) continue;
                float reach = 0.5f * (float)Math.Sqrt(p.SizeX * p.SizeX + p.SizeZ * p.SizeZ) + NearMargin;
                int x0 = Math.Max(0, (int)((p.X - reach + halfX) / MapCell)), x1 = Math.Min(_mapW - 1, (int)((p.X + reach + halfX) / MapCell));
                int z0 = Math.Max(0, (int)((p.Z - reach + halfZ) / MapCell)), z1 = Math.Min(_mapH - 1, (int)((p.Z + reach + halfZ) / MapCell));
                for (int cz = z0; cz <= z1; cz++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        float mx = -halfX + (cx + 0.5f) * MapCell, mz = -halfZ + (cz + 0.5f) * MapCell;
                        if (Parts.Contains(p, mx, mz, NearMargin)) _map[cz * _mapW + cx] |= bits;
                    }
            }
            _mapVersion = Version;
        }

        /// <summary>Used by the save loader.</summary>
        internal void Restore(PlacedPart p)
        {
            _parts.Add(p);
            if (p.Id >= _nextId) _nextId = p.Id + 1;
            Version++;
        }

        internal void SetNextId(int nextId) => _nextId = Math.Max(_nextId, nextId);

        /// <summary>Gives back ids handed out to parts that were taken away again straight after.</summary>
        internal void ResetNextId(int nextId)
        {
            foreach (var p in _parts) if (p.Id >= nextId) return;
            _nextId = Math.Max(1, nextId);
        }
    }
}
