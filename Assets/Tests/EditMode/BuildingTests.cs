using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 3: building parts, snapping, stacking, blueprints and what parts do for frogs.</summary>
    public class BuildingTests
    {
        /// <summary>A flat 20 x 12 m tank at White's tree frog conditions.</summary>
        private static World Flat()
        {
            var w = new World(3);
            w.Environment.AmbientTemperature = 26f;
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            return w;
        }

        private static PlacedPart Part(PartKind kind, float x, float z, float rotation = 0f, float scale = 1f) =>
            new PlacedPart { Kind = kind, X = x, Z = z, Rotation = rotation, Scale = scale, R = 1, G = 2, B = 3 };

        private static PlacedPart Place(World w, PlacedPart p)
        {
            var placed = w.Parts.Place(p, w.Terrain, out string why);
            Assert.IsNotNull(placed, why);
            return placed;
        }

        [Test]
        public void EveryPart_HasSensibleData()
        {
            for (int k = 0; k < Parts.All.Length; k++)
            {
                var info = Parts.All[k];
                Assert.AreEqual(k, (int)info.Kind, "parts are listed in the order of their numbers");
                Assert.Greater(info.SizeX, 0f);
                Assert.Greater(info.SizeY, 0f);
                Assert.Greater(info.SizeZ, 0f);
                Assert.IsFalse(string.IsNullOrEmpty(info.Name));
                Assert.IsFalse(string.IsNullOrEmpty(info.Description));
            }
        }

        [Test]
        public void Part_MustFitInsideTheTank()
        {
            var w = Flat();
            Place(w, Part(PartKind.Rock, 0f, 0f));
            Assert.IsNull(w.Parts.Place(Part(PartKind.Log, 9.9f, 0f), w.Terrain, out string why), "a log poking through the glass");
            StringAssert.Contains("tank", why);
            Assert.IsNull(w.Parts.Place(Part(PartKind.Rock, 0f, 0f, 0f, 5f), w.Terrain, out _), "sizes are limited");
            Assert.AreEqual(1, w.Parts.Count);
        }

        [Test]
        public void Parts_CannotOverlap_ButMayTouch()
        {
            var w = Flat();
            Place(w, Part(PartKind.Slab, 0f, 0f));
            Assert.IsNull(w.Parts.Place(Part(PartKind.Slab, 0.5f, 0.2f), w.Terrain, out string why));
            StringAssert.Contains("overlap", why);
            Place(w, Part(PartKind.Slab, 1f, 0f)); // edge to edge
            Place(w, Part(PartKind.Slab, 0f, 1f));
            Assert.IsNull(w.Parts.Place(Part(PartKind.Slab, 1f, 1f, 45f), w.Terrain, out _), "a turned slab across the corner overlaps");
        }

        [Test]
        public void Wall_SnapsEndToEnd_AndTurnsToLineUp()
        {
            var w = Flat();
            Place(w, Part(PartKind.Wall, 0f, 0f));
            var next = Part(PartKind.Wall, 1.12f, 0.1f, 12f);
            Assert.IsTrue(w.Parts.Snap(next, w.Terrain, grid: false));
            Assert.AreEqual(1f, next.X, 1e-3f);
            Assert.AreEqual(0f, next.Z, 1e-3f);
            Assert.AreEqual(0f, Parts.NormaliseAngle(next.Rotation + 0.0005f), 0.01f, "it turns to line up");
            Assert.IsTrue(w.Parts.Check(next, w.Terrain, out string why), why);

            // A pillar's side snaps to the wall's end, and a wall then snaps to the pillar's far side at 90°.
            var pillar = Part(PartKind.Pillar, -0.6f, 0.05f);
            w.Parts.Snap(pillar, w.Terrain, grid: false);
            Assert.AreEqual(-0.6f, pillar.X, 1e-3f);
            Assert.AreEqual(0f, pillar.Z, 1e-3f);
            Place(w, pillar);
            var corner = Part(PartKind.Wall, -0.6f, -0.65f, 80f);
            w.Parts.Snap(corner, w.Terrain, grid: false);
            Assert.AreEqual(-0.6f, corner.X, 1e-3f);
            Assert.AreEqual(-0.6f, corner.Z, 1e-3f);
            Assert.AreEqual(90f, corner.Rotation % 180f, 0.01f, "a corner: the wall runs the other way");
            Assert.IsTrue(w.Parts.Check(corner, w.Terrain, out why), why);
        }

        [Test]
        public void GridPlacement_RoundsSpotAndTurn_FreePlacementDoesNot()
        {
            var w = Flat();
            var p = Part(PartKind.Rock, 1.13f, -2.31f, 50f);
            w.Parts.Snap(p, w.Terrain, grid: true);
            Assert.AreEqual(1.25f, p.X, 1e-5f);
            Assert.AreEqual(-2.25f, p.Z, 1e-5f);
            Assert.AreEqual(45f, p.Rotation, 1e-4f);
            var q = Part(PartKind.Rock, 1.13f, -2.31f, 50f);
            w.Parts.Snap(q, w.Terrain, grid: false);
            Assert.AreEqual(1.13f, q.X, 1e-5f);
            Assert.AreEqual(50f, q.Rotation, 1e-4f);
        }

        [Test]
        public void Parts_StackOnSlabsAndPlatforms_ButNotOnRocks()
        {
            var w = Flat();
            float ground = w.Terrain.HeightAt(0f, 0f);
            var slab = Place(w, Part(PartKind.Slab, 0f, 0f));
            var rock = Part(PartKind.Rock, 0.1f, 0.1f);
            w.Parts.Snap(rock, w.Terrain, grid: false);
            Assert.AreEqual(slab.Id, rock.OnPartId, "dropped on the slab, it sits on top");
            rock = Place(w, rock);
            Assert.AreEqual(ground + slab.SizeY, w.Parts.BaseHeight(rock, w.Terrain), 1e-4f);

            var onRock = Part(PartKind.Fern, 0.1f, 0.1f);
            w.Parts.Snap(onRock, w.Terrain, grid: false);
            Assert.AreEqual(slab.Id, onRock.OnPartId, "a rock is not stackable, so the fern lands on the slab");
            Assert.IsFalse(w.Parts.Check(onRock, w.Terrain, out _), "and would overlap the rock");

            // Pillars hold up a platform; things stand on the platform.
            var p1 = Place(w, Part(PartKind.Pillar, 3f, 0f));
            var deck = Part(PartKind.Platform, 3f, 0f);
            w.Parts.Snap(deck, w.Terrain, grid: false);
            Assert.AreEqual(p1.Id, deck.OnPartId);
            deck = Place(w, deck);
            var hide = Part(PartKind.Hide, 3.2f, 0.2f);
            w.Parts.Snap(hide, w.Terrain, grid: false);
            Assert.AreEqual(deck.Id, hide.OnPartId);
            hide = Place(w, hide);
            Assert.AreEqual(ground + p1.SizeY + deck.SizeY, w.Parts.BaseHeight(hide, w.Terrain), 1e-4f);

            // Taking the deck away lets the hide drop onto the pillar.
            Assert.IsTrue(w.Parts.Remove(deck.Id));
            Assert.AreEqual(p1.Id, w.Parts.Find(hide.Id).OnPartId);
        }

        [Test]
        public void PartLimit_IsEnforced()
        {
            var w = new World(1, 30f, 30f);
            int placed = 0;
            for (int k = 0; k < PartStore.MaxParts + 10; k++)
            {
                var p = Part(PartKind.MossPad, -29f + (k % 50) * 1.1f, -29f + (k / 50) * 1.1f);
                if (w.Parts.Place(p, w.Terrain, out _) != null) placed++;
            }
            Assert.AreEqual(PartStore.MaxParts, placed);
        }

        // ------------------------------------------------------------------ blueprints

        private static World SmallBuild()
        {
            var w = Flat();
            Place(w, Part(PartKind.Slab, 0f, 0f));
            var slab = w.Parts.All[0];
            var hide = Part(PartKind.Hide, 0f, 0f, 30f, 1.2f);
            hide.R = 200; hide.G = 50; hide.B = 90;
            w.Parts.Snap(hide, w.Terrain, grid: false);
            Assert.AreEqual(slab.Id, hide.OnPartId);
            Place(w, hide);
            Place(w, Part(PartKind.Plant, 1.5f, 0f));
            return w;
        }

        [Test]
        public void Blueprint_RoundTripsAsText_AndCanBePlacedElsewhereTurned()
        {
            var w = SmallBuild();
            var bp = Blueprint.FromParts(w.Parts, "My cosy corner!");
            Assert.AreEqual("My cosy corner", bp.Name, "names keep only safe characters");
            string text = bp.ToText();
            StringAssert.StartsWith(Blueprint.TextPrefix, text);

            // Pasted with extra spaces and a line break in the middle, it still reads.
            string pasted = "  " + text.Substring(0, 20) + "\n " + text.Substring(20) + "  ";
            var back = Blueprint.FromText(pasted);
            Assert.AreEqual(bp.Name, back.Name);
            Assert.AreEqual(3, back.Parts.Count);

            var other = Flat();
            int n = back.PlaceInto(other.Parts, other.Terrain, -5f, 3f, 90f, out string why);
            Assert.AreEqual(3, n, why);
            PlacedPart slab = null, hide = null, plant = null;
            foreach (var p in other.Parts.All)
            {
                if (p.Kind == PartKind.Slab) slab = p;
                if (p.Kind == PartKind.Hide) hide = p;
                if (p.Kind == PartKind.Plant) plant = p;
            }
            Assert.IsNotNull(slab); Assert.IsNotNull(hide); Assert.IsNotNull(plant);
            Assert.AreEqual(slab.Id, hide.OnPartId, "the hide is still on the slab");
            Assert.AreEqual(200, hide.R, "colours come along");
            Assert.AreEqual(1.2f, hide.Scale, 1e-5f, "sizes come along");
            Assert.AreEqual(120f, hide.Rotation, 1e-3f, "turned with the whole build");
            // The plant was 1.5 m to the right (+X) of the slab; turned 90° clockwise, that is 1.5 m behind (-Z).
            Assert.AreEqual(slab.X, plant.X, 1e-3f);
            Assert.AreEqual(slab.Z - 1.5f, plant.Z, 1e-3f);
        }

        [Test]
        public void Blueprint_ThatDoesNotFit_PlacesNothing()
        {
            var bp = Blueprint.FromParts(SmallBuild().Parts, "x");
            var w = Flat();
            Place(w, Part(PartKind.Rock, 9.4f, 0f));
            int n = bp.PlaceInto(w.Parts, w.Terrain, 9.2f, 0f, 0f, out string why);
            Assert.AreEqual(0, n);
            Assert.IsNotEmpty(why);
            Assert.AreEqual(1, w.Parts.Count, "all or nothing: the tank is as it was");
        }

        [Test]
        public void BrokenBlueprintText_IsRefusedWithAPlainMessage()
        {
            Assert.Throws<SaveFormatException>(() => Blueprint.FromText("hello"));
            Assert.Throws<SaveFormatException>(() => Blueprint.FromText(Blueprint.TextPrefix + "!!!notbase64"));
            string good = Blueprint.FromParts(SmallBuild().Parts, "x").ToText();
            Assert.Throws<SaveFormatException>(() => Blueprint.FromText(good.Substring(0, good.Length / 2)));
        }

        [Test]
        public void Parts_AreSavedAndLoaded()
        {
            var w = SmallBuild();
            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.AreEqual(w.Parts.Count, loaded.Parts.Count);
            Assert.AreEqual(w.Parts.NextId, loaded.Parts.NextId);
            for (int k = 0; k < w.Parts.Count; k++)
            {
                var a = w.Parts.All[k];
                var b = loaded.Parts.All[k];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Kind, b.Kind);
                Assert.AreEqual(a.X, b.X);
                Assert.AreEqual(a.Z, b.Z);
                Assert.AreEqual(a.Rotation, b.Rotation);
                Assert.AreEqual(a.Scale, b.Scale);
                Assert.AreEqual(a.R, b.R);
                Assert.AreEqual(a.G, b.G);
                Assert.AreEqual(a.B, b.B);
                Assert.AreEqual(a.OnPartId, b.OnPartId);
            }
        }

        // ------------------------------------------------------------------ what parts do

        [Test]
        public void Plant_ShadesAndDampensTheAirAroundIt()
        {
            var w = Flat();
            Place(w, Part(PartKind.Plant, -4f, 0f));
            var env = new EnvironmentSystem();
            for (int k = 0; k < 300; k++) env.Tick(w, 0.2f);
            var e = w.Environment;
            Assert.Less(e.TemperatureAt(-4f, 0f), e.TemperatureAt(4f, 0f) - 0.5f, "cooler in the shade");
            Assert.Greater(e.HumidityAt(-4f, 0f), e.HumidityAt(4f, 0f) + 3f, "damper by the plant");
        }

        [Test]
        public void Cover_CalmsFrogsFaster_AndPerchesComfortTreeFrogs()
        {
            var w = Flat();
            w.Environment.AmbientTemperature = 31.5f; // a little too warm for White's tree frogs
            w.Environment.ResetToAmbient();
            Place(w, Part(PartKind.Hide, -4f, 0f));
            Place(w, Part(PartKind.Branch, 4f, 0f));
            int hidden = w.AddFrog(-4f, 0f, 0, Sex.Female);
            int open = w.AddFrog(0f, 3f, 0, Sex.Female);
            int perched = w.AddFrog(4f, 0f, 0, Sex.Female);
            int toad = w.AddFrog(4.05f, 0.3f, 2, Sex.Female);
            var a = w.Agents;
            foreach (int i in new[] { hidden, open, perched, toad }) { a.Stress[i] = 0.8f; a.Comfort[i] = 0.9f; a.Social[i] = 0f; }
            var needs = new NeedsSystem();
            for (int k = 0; k < 40; k++)
            {
                needs.Tick(w, 0.25f);
                foreach (int i in new[] { hidden, open, perched, toad }) { a.Hunger[i] = 0f; a.Thirst[i] = 0f; a.Social[i] = 0f; }
            }
            Assert.Greater(a.Comfort[hidden], a.Comfort[open] + 0.04f, "cover is comforting");
            Assert.Greater(a.Comfort[perched], a.Comfort[open] + 0.04f, "a tree frog likes its perch");
            Assert.IsTrue(w.Parts.PerchAt(a.PosX[toad], a.PosZ[toad], w.HalfSizeX, w.HalfSizeZ));
            Assert.IsFalse(SpeciesCatalog.FrogOf(a, toad).LikesPerches, "fire-bellied toads live on the ground");
        }

        [Test]
        public void UnderCover_StressFadesFaster()
        {
            var w = Flat();
            Place(w, Part(PartKind.Hide, -4f, 0f));
            int hidden = w.AddFrog(-4f, 0f, 0, Sex.Female);
            int open = w.AddFrog(4f, 0f, 0, Sex.Female);
            var a = w.Agents;
            var needs = new NeedsSystem();
            foreach (int i in new[] { hidden, open }) { a.Stress[i] = 0.8f; a.Comfort[i] = 1f; }
            for (int k = 0; k < 40; k++)
            {
                needs.Tick(w, 0.25f);
                foreach (int i in new[] { hidden, open }) { a.Hunger[i] = 0f; a.Thirst[i] = 0f; a.Social[i] = 0f; }
            }
            float calmedHidden = 0.8f - a.Stress[hidden], calmedOpen = 0.8f - a.Stress[open];
            Assert.Greater(calmedOpen, 0f);
            Assert.AreEqual(calmedOpen * NeedsSystem.CoverCalming, calmedHidden, calmedOpen * 0.05f);
        }
    }
}
