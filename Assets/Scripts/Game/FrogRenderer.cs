using System.Collections.Generic;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Draws every frog, egg and tadpole with the shapes from <see cref="FrogShapes"/> and the moves
    /// from <see cref="FrogMoves"/>: a frog-shaped body with a pale belly, eyes that blink, back legs
    /// in three bones that fold and kick, front legs that prop and reach, webbed feet (sticky toe
    /// pads on tree frogs), a throat that swells when a male calls, a tongue that flicks out, and
    /// tadpole tails that swish. Genes and mutations still show: size, colour, giant eyes, long or
    /// stubby legs, teeth, claws and the loud-call throat.
    ///
    /// Everything is drawn with GPU instancing in batches that share a shape and a colour.
    /// It only reads the simulation; it never changes it.
    /// </summary>
    public sealed class FrogRenderer
    {
        private enum Part { Body, Belly, Ball, Limb, FootTree, FootToad, Tail, Count }

        private readonly Mesh[] _meshes = new Mesh[(int)Part.Count];
        private readonly Material _material;
        private readonly Matrix4x4[] _batch = new Matrix4x4[1023];
        private readonly Dictionary<int, ColourBatch> _batches = new Dictionary<int, ColourBatch>();

        /// <summary>Per-frog animation that the simulation does not keep: where it is in its hop.</summary>
        private sealed class Anim
        {
            public float Phase;
            public bool Swimming;
            public int SeenFrame;
        }
        private readonly Dictionary<int, Anim> _anims = new Dictionary<int, Anim>();
        private int _frame;

        /// <summary>Where calls come from (the throat swells with each note). Optional.</summary>
        public FrogAudio Audio;

        private sealed class ColourBatch
        {
            public MaterialPropertyBlock Colour;
            public Mesh Mesh;
            public readonly List<Matrix4x4> Items = new List<Matrix4x4>();
        }

        public FrogRenderer(Material material)
        {
            _material = material;
            var body = FrogShapes.Body();
            _meshes[(int)Part.Body] = ToMesh(body, "Frog body");
            _meshes[(int)Part.Belly] = ToMesh(FrogShapes.Belly(body), "Frog belly");
            _meshes[(int)Part.Ball] = ToMesh(FrogShapes.Ball(), "Ball");
            _meshes[(int)Part.Limb] = ToMesh(FrogShapes.Limb(0.65f), "Frog leg bone");
            _meshes[(int)Part.FootTree] = ToMesh(FrogShapes.Foot(4, 1.25f), "Tree frog foot");
            _meshes[(int)Part.FootToad] = ToMesh(FrogShapes.Foot(4, 0.45f), "Toad foot");
            _meshes[(int)Part.Tail] = ToMesh(FrogShapes.TailPiece(), "Tadpole tail");
        }

        public bool Ready => _material != null && _meshes[0] != null;

        /// <summary>Turns a shape made by the simulation code into a Unity mesh.</summary>
        public static Mesh ToMesh(MeshData data, string name)
        {
            var v = new Vector3[data.Positions.Length];
            var n = new Vector3[data.Normals.Length];
            for (int k = 0; k < v.Length; k++)
            {
                v[k] = new Vector3(data.Positions[k].X, data.Positions[k].Y, data.Positions[k].Z);
                n[k] = new Vector3(data.Normals[k].X, data.Normals[k].Y, data.Normals[k].Z);
            }
            var mesh = new Mesh { name = name, vertices = v, normals = n, triangles = data.Triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        // Leg bone directions (in the frog's own space, +Z forward, X to its left for side = -1).
        // Folded = sitting, Out = stretched behind in a hop, Kick = drawn up and out to the sides when swimming.
        private static readonly Vector3 ThighFolded = new Vector3(0.6f, -0.15f, 0.78f), ShinFolded = new Vector3(-0.3f, -0.25f, -0.92f), FootFolded = new Vector3(0.45f, -0.08f, 0.9f);
        private static readonly Vector3 ThighOut = new Vector3(0.25f, -0.25f, -0.93f), ShinOut = new Vector3(0.15f, -0.2f, -0.97f), FootOut = new Vector3(0.1f, -0.15f, -0.98f);
        private static readonly Vector3 ThighKick = new Vector3(0.88f, 0f, 0.45f), ShinKick = new Vector3(-0.1f, 0f, -0.99f), FootKick = new Vector3(0.9f, 0f, 0.4f);
        private static readonly Vector3 ArmProp = new Vector3(0.45f, -0.6f, 0.35f), ForearmProp = new Vector3(0.05f, -0.97f, 0.2f), HandProp = new Vector3(0.4f, -0.05f, 0.9f);
        private static readonly Vector3 ArmReach = new Vector3(0.3f, -0.25f, 0.92f), ForearmReach = new Vector3(0.1f, -0.35f, 0.93f), HandReach = new Vector3(0.15f, -0.3f, 0.94f);

        public void Draw(World world, float simSeconds, float simDelta)
        {
            if (!Ready) return;
            _frame++;
            var a = world.Agents;
            foreach (var b in _batches.Values) b.Items.Clear();
            var eggColour = new Color(0.86f, 0.86f, 0.74f);
            var yolk = new Color(0.12f, 0.1f, 0.08f);
            var tadpoleColour = new Color(0.2f, 0.18f, 0.12f);
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) continue;
                var sp = SpeciesCatalog.FrogOf(a, i);
                float size = sp.AdultSize * sp.SizeFraction(a.Growth[i]) * a.Genes[i].Size;
                var stage = a.Stage[i];
                var pos = new Vector3(a.PosX[i], a.PosY[i], a.PosZ[i]);
                if (stage == LifeStage.Egg)
                {
                    var egg = Matrix4x4.TRS(pos + Vector3.up * size * 0.5f, Quaternion.identity, Vector3.one * size);
                    Add(Part.Ball, eggColour, egg);
                    Add(Part.Ball, yolk, egg * Matrix4x4.TRS(new Vector3(0f, 0.2f, 0f), Quaternion.identity, Vector3.one * 0.45f));
                    continue;
                }
                if (stage == LifeStage.Tadpole)
                {
                    DrawTadpole(a, i, pos, size, tadpoleColour, simSeconds);
                    continue;
                }
                DrawFrog(world, a, i, sp, pos, size, simSeconds, simDelta);
            }
            foreach (var b in _batches.Values)
            {
                if (b.Items.Count == 0) continue;
                var rp = new RenderParams(_material) { matProps = b.Colour };
                int n = 0;
                foreach (var m in b.Items)
                {
                    _batch[n++] = m;
                    if (n == _batch.Length) { Graphics.RenderMeshInstanced(rp, b.Mesh, 0, _batch, n); n = 0; }
                }
                if (n > 0) Graphics.RenderMeshInstanced(rp, b.Mesh, 0, _batch, n);
            }
            // Forget frogs that have gone (eaten, sold, escaped, grown up into a new record).
            if (_frame % 300 == 0)
            {
                var gone = new List<int>();
                foreach (var kv in _anims) if (_frame - kv.Value.SeenFrame > 60) gone.Add(kv.Key);
                foreach (int id in gone) _anims.Remove(id);
            }
        }

        private void DrawTadpole(AgentStore a, int i, Vector3 pos, float size, Color colour, float t)
        {
            float speed = Mathf.Sqrt(a.VelX[i] * a.VelX[i] + a.VelZ[i] * a.VelZ[i]);
            var root = Matrix4x4.TRS(pos + Vector3.up * size * 0.3f, Facing(a, i), Vector3.one * size);
            Add(Part.Ball, colour, root * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.7f, 0.55f, 0.9f)));
            // Three pieces of tail, each bending a little more than the one before, so a wave runs down it.
            var joint = new Vector3(0f, 0f, -0.35f);
            float yaw = 0f;
            for (int k = 0; k < 3; k++)
            {
                yaw += FrogMoves.TailSwish(t, a.Id[i], k / 2f, speed);
                var turn = Quaternion.Euler(0f, yaw, 0f);
                float len = 0.45f - 0.08f * k, tall = 0.55f - 0.12f * k;
                Add(Part.Tail, colour * 1.15f, root * Matrix4x4.TRS(joint, turn, new Vector3(1f, tall, len)));
                joint += turn * new Vector3(0f, 0f, -len);
            }
        }

        private void DrawFrog(World world, AgentStore a, int i, FrogSpecies sp, Vector3 pos, float size, float t, float dt)
        {
            int id = a.Id[i];
            var genes = a.Genes[i];
            ushort muts = genes.Visible;
            bool longLegs = Mutations.Shows(muts, Mutation.LongLegs), stubby = Mutations.Shows(muts, Mutation.StubbyLegs);
            float legScale = longLegs ? 1.4f : stubby ? 0.65f : 1f;
            float legThick = stubby ? 1.35f : longLegs ? 0.85f : 1f;
            float speed = Mathf.Sqrt(a.VelX[i] * a.VelX[i] + a.VelZ[i] * a.VelZ[i]);
            var goal = a.Goal[i];
            bool riding = a.RidePart[i] != 0;

            if (!_anims.TryGetValue(id, out var anim)) { anim = new Anim(); _anims[id] = anim; }
            anim.SeenFrame = _frame;
            float depth = world.Terrain.WaterDepthAt(pos.x, pos.z);
            anim.Swimming = !riding && depth > size * 0.3f;

            // Move through the hop (or kick) by the distance covered; a frog that stops finishes its hop first.
            float hopLength = size * FrogMoves.HopLength * (anim.Swimming ? 0.5f : legScale);
            if (speed > 0.03f) anim.Phase += speed * dt / Mathf.Max(0.01f, hopLength);
            else if (anim.Phase > 0f) anim.Phase += dt * 2.5f;
            if (anim.Phase >= 1f) anim.Phase = speed > 0.03f ? anim.Phase - Mathf.Floor(anim.Phase) : 0f;

            FrogPose pose;
            if (riding) pose = speed > 2.2f ? FrogMoves.Hop(0.3f) : FrogMoves.Sitting;
            else if (anim.Swimming) pose = FrogMoves.Swim(speed > 0.03f ? anim.Phase : 0f);
            else if (goal == Goal.Sleep) pose = FrogMoves.Sleeping;
            else if (goal == Goal.Escape && speed < 0.01f) pose = new FrogPose { Pitch = -70f, Reach = 1f, Stretch = 0.3f, Lift = 0.25f + 0.1f * Mathf.Abs(Mathf.Sin(t * 5f + id)) };
            else pose = FrogMoves.Hop(anim.Phase);

            // The frog's own space: uniform size, turned to face where it is going and tipped about the hips.
            float y = pos.y + pose.Lift * size * 1.3f;
            if (anim.Swimming) y = world.Terrain.WaterLevel - size * 0.12f;
            var facing = Facing(a, i);
            if (riding && speed > 2.2f) facing *= Quaternion.Euler(t * 720f + id * 37f, 0f, 0f);
            var root = Matrix4x4.TRS(new Vector3(pos.x, y, pos.z), facing, Vector3.one * size)
                * Matrix4x4.Translate(new Vector3(0f, 0f, -0.35f)) * Matrix4x4.Rotate(Quaternion.Euler(pose.Pitch, 0f, 0f)) * Matrix4x4.Translate(new Vector3(0f, 0f, 0.35f));

            // Body: breathing, plumper when carrying eggs, flatter and wider for toads.
            bool toad = !sp.LikesPerches;
            float breath = FrogMoves.Breath(t, id);
            float width = breath * (a.MateId[i] != 0 ? 1.15f : 1f) * (toad ? 1.08f : 1f);
            float height = 0.6f * breath * (1f - pose.Squash) * (a.MateId[i] != 0 ? 1.1f : 1f) * (toad ? 0.88f : 1f);
            var bodyCentre = new Vector3(0f, 0.2f * (1f - pose.Squash * 0.5f), 0f);
            var body = root * Matrix4x4.TRS(bodyCentre, Quaternion.identity, new Vector3(width, height, 1.3f));
            bool elder = a.Stage[i] == LifeStage.Elder;
            var skin = RendererColours.Body(sp, genes.Hue, muts, elder);
            var legSkin = RendererColours.Body(sp, genes.Hue - 0.3f, muts, elder);
            var belly = RendererColours.Belly(sp, muts, elder);
            Add(Part.Body, skin, body);
            Add(Part.Belly, belly, body);
            Vector3 OnBody(float bx, float by, float bz) => bodyCentre + new Vector3(bx * width, by * height, bz * 1.3f);

            // Eyes: big round eyes on top of the head, with a dark pupil looking out to each side.
            bool albino = Mutations.Shows(muts, Mutation.Albino);
            var iris = albino ? new Color(0.95f, 0.35f, 0.4f) : new Color(sp.EyeR, sp.EyeG, sp.EyeB);
            float eye = Mutations.Shows(muts, Mutation.GiantEyes) ? 0.36f : 0.24f;
            float open = goal == Goal.Sleep ? 0.12f : Mathf.Max(0.12f, FrogMoves.EyesOpen(t, id));
            for (int side = -1; side <= 1; side += 2)
            {
                var c = OnBody(0.25f * side, 0.27f, 0.3f);
                Add(Part.Ball, iris, root * Matrix4x4.TRS(c, Quaternion.identity, new Vector3(eye, eye * open, eye)));
                if (open > 0.5f)
                {
                    var look = new Vector3(0.8f * side, 0.25f, 0.55f).normalized;
                    Add(Part.Ball, Color.black, root * Matrix4x4.TRS(c + look * eye * 0.42f, Quaternion.LookRotation(look), new Vector3(eye * 0.55f, eye * 0.28f * open, eye * 0.18f)));
                }
                else
                {
                    // Shut: a lid of skin over the eye.
                    Add(Part.Ball, skin, root * Matrix4x4.TRS(c + Vector3.up * eye * 0.05f, Quaternion.identity, new Vector3(eye * 1.04f, eye * 0.3f, eye * 1.04f)));
                }
            }

            // Back legs: thigh, shin and a webbed foot, folding under the body or stretching out.
            var footMesh = sp.LikesPerches ? Part.FootTree : Part.FootToad;
            bool claws = Mutations.Shows(muts, Mutation.Claws);
            var clawColour = new Color(0.12f, 0.1f, 0.08f);
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = OnBody(0.33f * side, -0.1f, -0.32f);
                var d1 = LegDirection(ThighFolded, ThighOut, ThighKick, pose, side);
                var d2 = LegDirection(ShinFolded, ShinOut, ShinKick, pose, side);
                var d3 = LegDirection(FootFolded, FootOut, FootKick, pose, side);
                var knee = Bone(root, hip, d1, 0.42f * legScale, 0.2f * legThick, legSkin);
                var ankle = Bone(root, knee, d2, 0.42f * legScale, 0.14f * legThick, legSkin);
                Add(footMesh, legSkin, root * Matrix4x4.TRS(ankle, Turn(d3), new Vector3(0.32f * legThick, 0.6f, 0.38f * legScale)));
                if (claws) Bone(root, ankle + d3 * 0.38f * legScale, d3, 0.12f, 0.05f, clawColour);
            }
            // Front legs: prop the body up when sitting, reach forward to land.
            float reach = pose.Reach;
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = OnBody(0.3f * side, -0.18f, 0.3f);
                var d1 = Mirror(Vector3.Slerp(ArmProp, ArmReach, reach), side);
                var d2 = Mirror(Vector3.Slerp(ForearmProp, ForearmReach, reach), side);
                var d3 = Mirror(Vector3.Slerp(HandProp, HandReach, reach), side);
                var elbow = Bone(root, shoulder, d1, 0.12f * legScale, 0.11f * legThick, legSkin);
                var wrist = Bone(root, elbow, d2, 0.1f * legScale, 0.09f * legThick, legSkin);
                Add(footMesh, legSkin, root * Matrix4x4.TRS(wrist, Turn(d3), new Vector3(0.18f * legThick, 0.5f, 0.17f)));
                if (claws) Bone(root, wrist + d3 * 0.17f, d3, 0.08f, 0.04f, clawColour);
            }

            // Teeth: two goofy front teeth over the bottom lip.
            if (Mutations.Shows(muts, Mutation.Teeth))
            {
                var white = new Color(0.97f, 0.96f, 0.9f);
                for (int side = -1; side <= 1; side += 2)
                    Add(Part.Ball, white, root * Matrix4x4.TRS(OnBody(0.07f * side, -0.18f, 0.47f), Quaternion.identity, new Vector3(0.07f, 0.13f, 0.05f)));
            }

            // The throat sac: swells with each note while a male calls (much bigger with the Loud call mutation).
            bool loud = Mutations.Shows(muts, Mutation.LoudCall);
            float throat = Audio != null ? Audio.ThroatOf(id) : 0f;
            if (a.Sex[i] == Sex.Male && (throat > 0.02f || loud))
            {
                float s = loud ? 0.3f + 0.45f * throat : 0.12f + 0.3f * throat;
                var sac = Color.Lerp(belly, Color.white, 0.35f);
                Add(Part.Ball, sac, root * Matrix4x4.TRS(OnBody(0f, -0.28f, 0.33f), Quaternion.identity, new Vector3(s * 1.1f, s, s)));
            }

            // The tongue flicks out for an instant when the frog strikes at an insect.
            float sinceStrike = sp.StrikeCooldown - a.Cooldown[i];
            if (goal == Goal.Eat && a.Cooldown[i] > 0f && sinceStrike < 0.2f)
            {
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(sinceStrike / 0.2f));
                float length = sp.TongueReach / Mathf.Max(0.01f, size) * k;
                if (length > 0.05f) Bone(root, OnBody(0f, -0.08f, 0.48f), new Vector3(0f, -0.05f, 1f), length, 0.08f, new Color(0.9f, 0.45f, 0.5f));
            }
        }

        private static Vector3 Mirror(Vector3 v, int side) => new Vector3(v.x * side, v.y, v.z).normalized;

        private static Vector3 LegDirection(Vector3 folded, Vector3 stretched, Vector3 kick, FrogPose pose, int side)
        {
            var d = Vector3.Slerp(folded.normalized, stretched.normalized, pose.Stretch);
            d = Vector3.Slerp(d, kick.normalized, pose.Splay);
            return Mirror(d, side);
        }

        private static Quaternion Turn(Vector3 dir) =>
            Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up);

        /// <summary>Draws one bone from <paramref name="from"/> along <paramref name="dir"/> and returns where it ends.</summary>
        private Vector3 Bone(Matrix4x4 root, Vector3 from, Vector3 dir, float length, float thickness, Color colour)
        {
            Add(Part.Limb, colour, root * Matrix4x4.TRS(from, Turn(dir), new Vector3(thickness, thickness, length)));
            return from + dir * length;
        }

        private static Quaternion Facing(AgentStore a, int i)
        {
            var facing = new Vector3(a.VelX[i], 0f, a.VelZ[i]);
            return facing.sqrMagnitude > 1e-12f ? Quaternion.LookRotation(facing) : Quaternion.identity;
        }

        private void Add(Part part, Color c, Matrix4x4 m)
        {
            // Round the colour to 32 steps a channel so look-alike frogs share a batch.
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 31f), 0, 31);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 31f), 0, 31);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 31f), 0, 31);
            int key = (r | (g << 5) | (b << 10)) * (int)Part.Count + (int)part;
            if (!_batches.TryGetValue(key, out var batch))
            {
                batch = new ColourBatch { Colour = new MaterialPropertyBlock(), Mesh = _meshes[(int)part] };
                batch.Colour.SetColor(GameMaterials.ColourProperty, new Color(r / 31f, g / 31f, b / 31f));
                _batches[key] = batch;
            }
            batch.Items.Add(m);
        }

        /// <summary>How many separate things the last frame drew (for tests and the stats overlay).</summary>
        public int LastInstanceCount
        {
            get
            {
                int n = 0;
                foreach (var b in _batches.Values) n += b.Items.Count;
                return n;
            }
        }
    }

    /// <summary>Skin and belly colours, from the species, the colour gene, colour mutations and age.</summary>
    public static class RendererColours
    {
        public static Color Body(FrogSpecies sp, float hue, ushort muts, bool elder)
        {
            Color c;
            if (Mutations.Shows(muts, Mutation.Albino)) c = new Color(0.96f, 0.92f, 0.86f);
            else if (Mutations.Shows(muts, Mutation.Blue)) c = new Color(0.27f, 0.56f, 0.92f);
            else c = new Color(sp.ColourR, sp.ColourG, sp.ColourB);
            if (Mutations.Shows(muts, Mutation.Melanistic) && !Mutations.Shows(muts, Mutation.Albino)) c *= 0.28f;
            // The colour gene: darker and duller at -1, lighter and brighter at +1.
            float k = 0.88f + 0.12f * Mathf.Clamp(hue, -1f, 1f);
            c = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k));
            if (elder) c = Color.Lerp(c, new Color(0.45f, 0.44f, 0.38f), 0.35f);
            return c;
        }

        public static Color Belly(FrogSpecies sp, ushort muts, bool elder)
        {
            var c = new Color(sp.BellyR, sp.BellyG, sp.BellyB);
            if (Mutations.Shows(muts, Mutation.Albino)) c = new Color(1f, 0.95f, 0.92f);
            else if (Mutations.Shows(muts, Mutation.Melanistic)) c *= 0.45f;
            if (elder) c = Color.Lerp(c, new Color(0.7f, 0.68f, 0.62f), 0.3f);
            return c;
        }
    }
}
