using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    public class MovementTests
    {
        [Test]
        public void Agents_StandOnTheTerrain_AndInsectsStayOutOfThePool()
        {
            var w = World.CreateStarterTank(12345, 10, 80);
            var loop = SimulationFactory.CreateDefault(w);
            var a = w.Agents;
            var t = w.Terrain;
            for (int step = 0; step < 60 * 30; step++)
            {
                loop.Advance(1.0 / 60.0);
                if (step % 30 != 0) continue;
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(MovementSystem.SurfaceHeight(t, a.PosX[i], a.PosZ[i]), a.PosY[i], 1e-4f, "agent not on the surface");
                    Assert.IsTrue(t.Contains(a.PosX[i], a.PosZ[i]), "agent left the tank");
                    if (a.Kind[i] == AgentKind.Insect)
                        Assert.IsFalse(t.IsWater(a.PosX[i], a.PosZ[i]), "insect walked into the water");
                }
            }
        }

        [Test]
        public void SculptingUnderAnAgent_MovesItToTheNewSurface()
        {
            // QA case EN-04.
            var w = new World(3);
            int i = w.Agents.Add(AgentKind.Frog, 0f, 1f, 0f);
            var loop = SimulationFactory.CreateDefault(w);
            TerrainBrush.Apply(w.Terrain, BrushMode.Raise, 0f, 0f, 2f, 1f, 1f);
            loop.Advance(0.1);
            Assert.AreEqual(MovementSystem.SurfaceHeight(w.Terrain, w.Agents.PosX[i], w.Agents.PosZ[i]), w.Agents.PosY[i], 1e-4f);
            Assert.Greater(w.Agents.PosY[i], 1.5f);
        }
    }
}
