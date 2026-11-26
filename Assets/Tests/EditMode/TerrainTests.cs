using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    public class TerrainTests
    {
        private static TerrainGrid Flat() => new TerrainGrid(5f, 3f);

        [Test]
        public void NewTerrain_IsFlatAtBaseHeight()
        {
            var t = Flat();
            Assert.AreEqual(40, t.CellsX);
            Assert.AreEqual(24, t.CellsZ);
            Assert.AreEqual(TerrainGrid.DefaultBaseHeight, t.HeightAt(1.3f, -0.7f), 1e-5f);
            Assert.IsFalse(t.IsWater(0f, 0f));
        }

        [Test]
        public void HeightAt_BlendsBetweenGridPoints()
        {
            var t = Flat();
            t.Height[t.Index(20, 12)] = 2f; // the point at (0, 0)
            Assert.AreEqual(2f, t.HeightAt(0f, 0f), 1e-5f);
            float half = t.HeightAt(t.CellSize * 0.5f, 0f);
            Assert.AreEqual(1.5f, half, 1e-5f);
        }

        [Test]
        public void Raise_LiftsTheMiddleMostAndLeavesOutsideAlone()
        {
            var t = Flat();
            Assert.IsTrue(TerrainBrush.Apply(t, BrushMode.Raise, 0f, 0f, 1f, 1f, 0.5f));
            float centre = t.HeightAt(0f, 0f), edge = t.HeightAt(0.8f, 0f), outside = t.HeightAt(2f, 0f);
            Assert.AreEqual(1.5f, centre, 1e-4f);
            Assert.Greater(centre, edge);
            Assert.Greater(edge, 1f);
            Assert.AreEqual(1f, outside, 1e-6f);
        }

        [Test]
        public void Lower_NeverGoesBelowTheTankFloor()
        {
            var t = Flat();
            for (int i = 0; i < 100; i++) TerrainBrush.Apply(t, BrushMode.Lower, 0f, 0f, 1f, 2f, 0.1f);
            Assert.AreEqual(TerrainGrid.MinHeight, t.HeightAt(0f, 0f), 1e-6f);
        }

        [Test]
        public void DiggingBelowTheWaterLine_MakesAPool_AndRaisingDrainsIt()
        {
            var t = Flat();
            TerrainBrush.Apply(t, BrushMode.Lower, 0f, 0f, 1.5f, 1f, 0.8f);
            Assert.IsTrue(t.IsWater(0f, 0f), "a hole below the water line should hold water");
            Assert.Greater(t.WaterDepthAt(0f, 0f), 0.1f);
            // EN-03: raising ground over a pool removes the water cleanly.
            TerrainBrush.Apply(t, BrushMode.Raise, 0f, 0f, 1.5f, 1f, 1.5f);
            Assert.IsFalse(t.IsWater(0f, 0f));
        }

        [Test]
        public void Smooth_FlattensASpike()
        {
            var t = Flat();
            t.Height[t.Index(20, 12)] = 3f;
            TerrainBrush.Apply(t, BrushMode.Smooth, 0f, 0f, 1f, 5f, 0.2f);
            Assert.Less(t.Height[t.Index(20, 12)], 2.2f);
            Assert.Greater(t.Height[t.Index(21, 12)], 1f);
        }

        [Test]
        public void Paint_ChangesTheGroundLayerInsideTheBrush()
        {
            var t = Flat();
            TerrainBrush.Apply(t, BrushMode.Paint, 0f, 0f, 1f, 1f, 0.1f, GroundLayer.Moss);
            Assert.AreEqual(GroundLayer.Moss, t.LayerAt(0f, 0f));
            Assert.AreEqual(GroundLayer.Soil, t.LayerAt(2f, 0f));
        }

        [Test]
        public void Edit_OnlyMarksTheChunksItTouched()
        {
            var t = Flat();
            var before = (int[])t.ChunkVersion.Clone();
            TerrainBrush.Apply(t, BrushMode.Raise, t.MinX + 0.5f, t.MinZ + 0.5f, 0.3f, 1f, 0.1f);
            Assert.Greater(t.ChunkVersion[0], before[0]);
            Assert.AreEqual(before[t.ChunkVersion.Length - 1], t.ChunkVersion[t.ChunkVersion.Length - 1]);
        }

        [Test]
        public void Raycast_FromAboveHitsTheGround()
        {
            var t = Flat();
            TerrainBrush.Apply(t, BrushMode.Raise, 1f, 1f, 1f, 1f, 1f);
            Assert.IsTrue(t.Raycast(1f, 10f, -4f, 0f, -9f, 5f, 50f, out float x, out float y, out float z));
            Assert.AreEqual(t.HeightAt(x, z), y, 1e-4f);
            Assert.That(x, Is.EqualTo(1f).Within(0.01f));
            Assert.IsFalse(t.Raycast(0f, 10f, 0f, 0f, 1f, 0f, 50f, out _, out _, out _), "pointing at the sky misses");
        }

        [Test]
        public void UndoAndRedo_FiftyStrokes_RestoreExactly()
        {
            // QA case EN-05.
            var t = Flat();
            var history = new TerrainHistory(t);
            var original = (float[])t.Height.Clone();
            var rng = new SeededRandom(5);
            for (int s = 0; s < TerrainHistory.MaxSteps; s++)
            {
                history.BeginStroke();
                var mode = (BrushMode)rng.Range(0, 4);
                for (int f = 0; f < 5; f++)
                    TerrainBrush.Apply(t, mode, rng.Range(-4f, 4f), rng.Range(-2f, 2f), 1f, 1f, 0.1f, GroundLayer.Rock);
                history.EndStroke();
            }
            var edited = (float[])t.Height.Clone();
            Assert.That(history.UndoCount, Is.GreaterThan(40));

            while (history.Undo()) { }
            CollectionAssert.AreEqual(original, t.Height);
            Assert.AreEqual(GroundLayer.Soil, t.LayerAt(0f, 0f));

            while (history.Redo()) { }
            CollectionAssert.AreEqual(edited, t.Height);
        }

        [Test]
        public void StrokeThatChangesNothing_IsNotRecorded()
        {
            var t = Flat();
            var history = new TerrainHistory(t);
            history.BeginStroke();
            history.EndStroke();
            Assert.AreEqual(0, history.UndoCount);
        }

        [Test]
        public void MapGenerator_SameSeedSameTank_WithAPool()
        {
            // QA case MG-01.
            var a = new World(99); MapGenerator.Generate(a);
            var b = new World(99); MapGenerator.Generate(b);
            var c = new World(100); MapGenerator.Generate(c);
            CollectionAssert.AreEqual(a.Terrain.Height, b.Terrain.Height);
            CollectionAssert.AreEqual(a.Terrain.Layer, b.Terrain.Layer);
            CollectionAssert.AreNotEqual(a.Terrain.Height, c.Terrain.Height);

            int water = 0;
            for (int i = 0; i < a.Terrain.Height.Length; i++) if (a.Terrain.Height[i] < a.Terrain.WaterLevel) water++;
            Assert.Greater(water, 20, "the starter tank should have a pool");
        }
    }
}
