using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 5, parts 1 and 2: the frog playground (play need, riding pieces, chains, lifts, teleporters, traits, saving).</summary>
    public class PlaygroundTests
    {
        /// <summary>A flat 20 x 12 m tank at White's tree frog conditions.</summary>
        private static World Flat(ulong seed = 5)
        {
            var w = new World(seed);
            w.Environment.AmbientTemperature = 26f;
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            return w;
        }

        private static PlacedPart Place(World w, PartKind kind, float x, float z, float rotation = 0f, float scale = 1f)
        {
            var placed = w.Parts.Place(new PlacedPart { Kind = kind, X = x, Z = z, Rotation = rotation, Scale = scale, R = 200, G = 80, B = 60 }, w.Terrain, out string why);
            Assert.IsNotNull(placed, why);
            return placed;
        }

        /// <summary>A grown, fed, calm, bold frog that is very bored.</summary>
        private static int BoredFrog(World w, float x, float z, float boldness = 0.9f)
        {
            int i = w.AddFrog(x, z, 0, Sex.Female);
            var g = w.Agents.Genes[i];
            g.Boldness = boldness;
            w.Agents.Genes[i] = g;
            w.Agents.Play[i] = 1f;
            return w.Agents.Id[i];
        }

        private static void Feed(World w)
        {
            var a = w.Agents;
            for (int i = 0; i < a.Count; i++)
                if (a.IsLandFrog(i)) { a.Hunger[i] = 0.05f; a.Thirst[i] = 0.05f; a.Fatigue[i] = 0.05f; a.Stress[i] = 0f; }
        }

        private static void Run(SimulationLoop loop, float seconds)
        {
            int frames = (int)(seconds * 10f);
            for (int k = 0; k < frames; k++) loop.Advance(0.1);
        }

        [Test]
        public void EveryPiece_HasASensibleRoute()
        {
            foreach (var info in Playground.All)
            {
                var part = Parts.Info(info.Kind);
                Assert.IsTrue(part.Name.Length > 0);
                Assert.Greater(info.Capacity, 0);
                Assert.Greater(info.Relief, 0f);
                Assert.AreEqual(0f, info.Route[0].Y, part.Name + " starts on its base");
                var end = info.ExitRoute ?? info.Route;
                Assert.AreEqual(0f, end[end.Length - 1].Y, part.Name + " ends on the ground");
                Assert.AreEqual(-0.5f * part.SizeZ, info.Route[0].Z, 1e-4f, part.Name + " is entered at the back");
                Assert.AreEqual(2, part.Sockets.Length, part.Name + " snaps end to end");
                if (info.Elevator) continue; // the car carries the frogs: see the elevator test

                var w = Flat();
                var p = Place(w, info.Kind, 0f, 0f);
                if (info.Kind == PartKind.Teleporter) Place(w, PartKind.Teleporter, 4f, 2f);
                var path = new RidePath();
                path.Build(w, p);
                Assert.That(path.Duration, Is.InRange(1.5f, 20f), part.Name + " takes a few seconds");
                float top = 0f;
                for (int k = 0; k < path.Count; k++)
                {
                    path.Point(k, out _, out float y, out _);
                    if (y > top) top = y;
                }
                Assert.Greater(top, w.Terrain.HeightAt(0f, 0f) + 0.1f, part.Name + " lifts the frog off the ground");
            }
        }

        [Test]
        public void WithoutPieces_FrogsNeverGetBored()
        {
            var w = World.CreateStarterTank(7, 6, 40);
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 120f);
            var a = w.Agents;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(0f, a.Play[i]);
                Assert.AreNotEqual(Goal.Play, a.Goal[i]);
            }
        }

        [Test]
        public void BoredFrog_ClimbsTheSlide_AndSlidesDown()
        {
            var w = Flat();
            var slide = Place(w, PartKind.Slide, 0f, 0f);
            int id = BoredFrog(w, -2f, -3f);
            var loop = SimulationFactory.CreateDefault(w);
            float ground = w.Terrain.HeightAt(0f, 0f), highest = ground;
            bool rode = false;
            for (int s = 0; s < 400 && w.PlayRides[(int)PartKind.Slide] == 0; s++)
            {
                Feed(w);
                Run(loop, 0.1f);
                int i = w.Agents.IndexOf(id);
                if (w.Agents.RidePart[i] == slide.Id) rode = true;
                if (w.Agents.PosY[i] > highest) highest = w.Agents.PosY[i];
            }
            Assert.IsTrue(rode, "the bored frog got on the slide");
            Assert.AreEqual(1, w.PlayRides[(int)PartKind.Slide], "and finished the ride");
            Assert.Greater(highest, ground + 0.8f, "it went up the ladder");
            int f = w.Agents.IndexOf(id);
            Assert.Less(w.Agents.Play[f], 0.8f, "a ride takes boredom away");
            Assert.Greater(w.Agents.Fun[f], 0.5f, "and leaves a glow of fun");
            Assert.AreEqual(0, w.Agents.RidePart[f]);
            Playground.Landing(w.Parts, slide, out float lx, out float lz);
            Assert.Less(System.Math.Abs(w.Agents.PosZ[f] - lz), 1f, "it lands off the front of the slide");
        }

        [Test]
        public void Fun_RaisesComfort()
        {
            float ComfortAfter(bool fun)
            {
                var w = Flat();
                w.Environment.AmbientHumidity = 25f; // dry and warm, so comfort has room to rise
                w.Environment.AmbientTemperature = 31f;
                w.Environment.ResetToAmbient();
                Place(w, PartKind.Slide, 5f, 3f);
                int id = w.Agents.Id[w.AddFrog(-3f, 0f, 0, Sex.Female)];
                var loop = SimulationFactory.CreateDefault(w);
                for (int k = 0; k < 20; k++)
                {
                    Feed(w);
                    int i = w.Agents.IndexOf(id);
                    w.Agents.Play[i] = 0f;
                    if (fun) w.Agents.Fun[i] = 1f;
                    Run(loop, 1f);
                }
                return w.Agents.Comfort[w.Agents.IndexOf(id)];
            }
            Assert.Greater(ComfortAfter(true), ComfortAfter(false) + 0.03f);
        }

        [Test]
        public void TunnelSnappedToSlide_MakesAChain()
        {
            var w = Flat();
            var tunnel = Place(w, PartKind.AirTunnel, -3f, 0f);
            // Drop a slide near the tunnel's front: it snaps on, end to end.
            var slide = new PlacedPart { Kind = PartKind.Slide, X = -2.9f, Z = 2.35f, Rotation = 10f, Scale = 1f };
            Assert.IsTrue(w.Parts.Snap(slide, w.Terrain, grid: false), "the slide snaps to the tunnel");
            var placedSlide = w.Parts.Place(slide, w.Terrain, out string why);
            Assert.IsNotNull(placedSlide, why);

            int id = BoredFrog(w, -3f, -2f);
            var loop = SimulationFactory.CreateDefault(w);
            for (int s = 0; s < 600 && w.PlayRides[(int)PartKind.Slide] == 0; s++) { Feed(w); Run(loop, 0.1f); }
            Assert.GreaterOrEqual(w.PlayRides[(int)PartKind.AirTunnel], 1, "through the tunnel");
            Assert.GreaterOrEqual(w.PlayRides[(int)PartKind.Slide], 1, "and down the slide");
            Assert.GreaterOrEqual(w.PlayChains, 1, "straight from one to the other");
        }

        [Test]
        public void Tunnel_TakesOneFrogAtATime()
        {
            var w = Flat();
            var tunnel = Place(w, PartKind.AirTunnel, 0f, 0f);
            for (int k = 0; k < 4; k++) BoredFrog(w, -0.6f + 0.4f * k, -2f);
            var loop = SimulationFactory.CreateDefault(w);
            int most = 0;
            for (int s = 0; s < 400; s++)
            {
                Feed(w);
                Run(loop, 0.1f);
                most = System.Math.Max(most, Playground.RidersOn(w.Agents, tunnel.Id));
            }
            Assert.AreEqual(1, most);
            Assert.GreaterOrEqual(w.PlayRides[(int)PartKind.AirTunnel], 2, "they took turns");
        }

        [Test]
        public void RemovingAPiece_MidRide_PutsTheFrogDown()
        {
            var w = Flat();
            var stairs = Place(w, PartKind.SpiralStairs, 0f, 0f);
            int i = w.AddFrog(0f, -0.45f, 0, Sex.Male);
            int id = w.Agents.Id[i];
            Playground.StartRide(w, i, stairs);
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 5f);
            i = w.Agents.IndexOf(id);
            Assert.Greater(w.Agents.PosY[i], w.Terrain.HeightAt(0f, 0f) + 0.2f, "half way up the stairs");
            Assert.IsTrue(w.Parts.Remove(stairs.Id));
            Run(loop, 0.2f);
            i = w.Agents.IndexOf(id);
            Assert.AreEqual(0, w.Agents.RidePart[i]);
            Assert.AreEqual(MovementSystem.SurfaceHeight(w.Terrain, w.Agents.PosX[i], w.Agents.PosZ[i]), w.Agents.PosY[i], 1e-4f);
        }

        [Test]
        public void RaisedSlide_HopsUpAndDropsDown()
        {
            var w = Flat();
            var deck = Place(w, PartKind.Platform, 0f, 0f, 0f, 2f);
            var slide = new PlacedPart { Kind = PartKind.Slide, X = 0f, Z = 0f, Scale = 0.9f };
            w.Parts.Snap(slide, w.Terrain, grid: true);
            Assert.AreEqual(deck.Id, slide.OnPartId, "the slide stands on the deck");
            var placed = w.Parts.Place(slide, w.Terrain, out string why);
            Assert.IsNotNull(placed, why);
            var path = new RidePath();
            path.Build(w, placed);
            float ground = w.Terrain.HeightAt(0f, -0.9f);
            path.Point(0, out _, out float y0, out _);
            path.Point(path.Count - 1, out float lx, out float ly, out float lz);
            Assert.AreEqual(ground, y0, 1e-3f, "starts on the ground");
            Assert.AreEqual(MovementSystem.SurfaceHeight(w.Terrain, lx, lz), ly, 1e-3f, "ends on the ground");
        }

        [Test]
        public void SaveAndLoad_MidRide_CarriesOn()
        {
            var w = Flat();
            var slide = Place(w, PartKind.Slide, 0f, 0f);
            int i = w.AddFrog(0f, -1f, 0, Sex.Female);
            int id = w.Agents.Id[i];
            w.Agents.Play[i] = 0.7f;
            w.Agents.Fun[i] = 0.4f;
            Playground.StartRide(w, i, slide);
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 2f);

            var back = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            int j = back.Agents.IndexOf(id);
            i = w.Agents.IndexOf(id);
            Assert.AreEqual(Goal.Play, back.Agents.Goal[j]);
            Assert.AreEqual(slide.Id, back.Agents.RidePart[j]);
            Assert.AreEqual(w.Agents.RideTime[i], back.Agents.RideTime[j]);
            Assert.AreEqual(w.Agents.Play[i], back.Agents.Play[j]);
            Assert.AreEqual(w.Agents.Fun[i], back.Agents.Fun[j]);

            var loop2 = SimulationFactory.CreateDefault(back);
            Run(loop2, 10f);
            Assert.AreEqual(1, back.PlayRides[(int)PartKind.Slide], "the loaded frog finishes its ride");
        }

        [Test]
        public void Playground_NeedsResearch_AndTheTunnelUsesPower()
        {
            var w = Flat();
            w.Economy.Enabled = true;
            w.Economy.Money = 1000;
            Assert.IsFalse(w.Economy.PartUnlocked(PartKind.Slide));
            Assert.IsFalse(w.Economy.PartUnlocked(PartKind.AirTunnel));
            Assert.IsTrue(w.Economy.PartUnlocked(PartKind.Rock));
            w.Economy.ResearchPoints = 5;
            Assert.IsTrue(w.Economy.DoResearch(Research.Playground, out string why), why);
            Assert.IsTrue(w.Economy.PartUnlocked(PartKind.SpiralStairs));

            var tunnel = Place(w, PartKind.AirTunnel, 0f, 0f);
            int i = w.AddFrog(0f, -1.2f, 0, Sex.Female);
            Playground.StartRide(w, i, tunnel);
            Assert.AreEqual(1, EconomySystem.PoweredRiders(w));
            var calm = Flat();
            calm.Economy.Enabled = true;
            Assert.Greater(EconomySystem.Appeal(w, out _), EconomySystem.Appeal(calm, out _), "visitors like a frog at play");
        }

        [Test]
        public void PlaygroundBlueprint_RoundTrips()
        {
            var w = Flat();
            Place(w, PartKind.AirTunnel, -3f, 0f);
            var slide = new PlacedPart { Kind = PartKind.Slide, X = -2.9f, Z = 2.3f };
            Assert.IsTrue(w.Parts.Snap(slide, w.Terrain, grid: false));
            w.Parts.Place(slide, w.Terrain, out _);
            Place(w, PartKind.SpiralStairs, 3f, 2f, 90f, 1.5f);
            var text = Blueprint.FromParts(w.Parts, "Frog fun park").ToText();
            var back = Blueprint.FromText(text);
            Assert.AreEqual(3, back.Parts.Count);
            Assert.AreEqual(text, back.ToText());
        }

        [Test]
        public void Elevator_TakesFrogsUpAndBringsThemDown()
        {
            var w = Flat();
            var lift = Place(w, PartKind.Elevator, 0f, 0f);
            var ids = new int[4];
            for (int k = 0; k < 4; k++) ids[k] = BoredFrog(w, -0.6f + 0.4f * k, -1.5f);
            var loop = SimulationFactory.CreateDefault(w);
            float ground = w.Terrain.HeightAt(0f, 0f), highest = ground;
            int most = 0;
            for (int s = 0; s < 600; s++)
            {
                Feed(w);
                Run(loop, 0.1f);
                most = System.Math.Max(most, Playground.RidersOn(w.Agents, lift.Id));
                foreach (int id in ids)
                {
                    int i = w.Agents.IndexOf(id);
                    if (w.Agents.RidePart[i] == lift.Id && w.Agents.PosY[i] > highest) highest = w.Agents.PosY[i];
                }
            }
            Assert.Greater(highest, ground + 1.3f, "the car goes all the way up");
            Assert.AreEqual(3, most, "the car holds three frogs");
            Assert.GreaterOrEqual(w.PlayRides[(int)PartKind.Elevator], 3, "and brings them back down");
            Assert.Greater(w.PlaySquabbles, 0, "three in a car squabble over space");
        }

        [Test]
        public void ElevatorCar_KeepsItsTimetable()
        {
            Assert.AreEqual(0f, Playground.CarHeight(1f, 1f));
            Assert.AreEqual(Playground.LiftRise, Playground.CarHeight(Playground.LiftBottom + Playground.LiftUp + 1f, 1f), 1e-5f);
            Assert.AreEqual(2f * Playground.LiftRise, Playground.CarHeight(Playground.LiftBottom + Playground.LiftUp + 1f, 2f), 1e-5f, "a bigger lift goes higher");
            float half = Playground.CarHeight(Playground.LiftBottom + Playground.LiftUp * 0.5f, 1f);
            Assert.That(half, Is.InRange(0.3f * Playground.LiftRise, 0.7f * Playground.LiftRise));
            Assert.IsTrue(Playground.DoorsOpen(0.5f));
            Assert.IsFalse(Playground.DoorsOpen(Playground.LiftBottom + 1f));
        }

        [Test]
        public void TeleporterPads_PairUp_AndFrogsPopOutOfThePartner()
        {
            var w = Flat();
            var a = Place(w, PartKind.Teleporter, -4f, 0f);
            Assert.AreEqual(1, Playground.UnpairedTeleporters(w.Parts));
            new AlertSystem().Tick(w, 1f);
            Assert.IsNotNull(w.Alerts.Find(AlertSystem.UnpairedPadCause), "a lone pad gets one grouped alert");

            int id = BoredFrog(w, -4f, -1.5f);
            var loop = SimulationFactory.CreateDefault(w);
            for (int s = 0; s < 100; s++) { Feed(w); Run(loop, 0.1f); }
            Assert.AreEqual(0, w.PlayRides[(int)PartKind.Teleporter], "nobody uses a pad with no partner");

            var b = Place(w, PartKind.Teleporter, 5f, 3f);
            Assert.AreSame(b, Playground.PartnerOf(w.Parts, a));
            Assert.AreSame(a, Playground.PartnerOf(w.Parts, b));
            Assert.AreEqual(0, Playground.UnpairedTeleporters(w.Parts));
            bool seenAtB = false;
            for (int s = 0; s < 300 && w.PlayRides[(int)PartKind.Teleporter] == 0; s++)
            {
                Feed(w);
                Run(loop, 0.1f);
                int i = w.Agents.IndexOf(id);
                float dx = w.Agents.PosX[i] - b.X, dz = w.Agents.PosZ[i] - b.Z;
                if (w.Agents.RidePart[i] != 0 && dx * dx + dz * dz < 0.5f) seenAtB = true;
            }
            Assert.GreaterOrEqual(w.PlayRides[(int)PartKind.Teleporter], 1);
            Assert.IsTrue(seenAtB, "it popped out of the other pad");
            new AlertSystem().Tick(w, 1f);
            Assert.IsNull(w.Alerts.Find(AlertSystem.UnpairedPadCause));

            // Remove one pad mid-trip: the frog is put down safely.
            int f = w.Agents.IndexOf(id);
            Playground.StartRide(w, f, b);
            w.Parts.Remove(a.Id);
            Run(loop, 0.2f);
            Assert.AreEqual(0, w.Agents.RidePart[w.Agents.IndexOf(id)]);
        }

        [Test]
        public void LongLeggedFrogs_BoundUpTheStairs()
        {
            var w = Flat();
            var stairs = Place(w, PartKind.SpiralStairs, 0f, 0f);
            int plain = w.AddFrog(-2f, 0f, 0, Sex.Female);
            int jumper = w.AddFrog(2f, 0f, 0, Sex.Female);
            var g = w.Agents.Genes[jumper];
            g.Alleles = Mutations.WithCopies(g.Alleles, Mutation.LongLegs, 1);
            w.Agents.Genes[jumper] = g;
            Assert.AreEqual(RideStyle.Normal, Playground.StyleFor(w.Agents, plain, stairs));
            Assert.AreEqual(RideStyle.BigJump, Playground.StyleFor(w.Agents, jumper, stairs));
            var slow = new RidePath();
            slow.Build(w, stairs, RideStyle.Normal);
            var fast = new RidePath();
            fast.Build(w, stairs, RideStyle.BigJump);
            Assert.Less(fast.Duration, slow.Duration * 0.6f, "big jumpers skip the steps");
        }

        [Test]
        public void StickyFootedFrogs_ClimbBackUpTheSlide()
        {
            var w = Flat();
            var slides = new PlacedPart[8];
            for (int k = 0; k < slides.Length; k++) slides[k] = Place(w, PartKind.Slide, -8f + 2f * k, 0f);
            int toad = w.AddFrog(0f, 3f, 2, Sex.Female);      // fire-bellied toads have no sticky toe pads
            int treeFrog = w.AddFrog(0f, -3f, 0, Sex.Female); // White's tree frogs do
            int sticky = 0;
            foreach (var p in slides)
            {
                Assert.AreEqual(RideStyle.Normal, Playground.StyleFor(w.Agents, toad, p));
                if (Playground.StyleFor(w.Agents, treeFrog, p) == RideStyle.Sticky) sticky++;
            }
            Assert.That(sticky, Is.InRange(1, 7), "a tree frog has its own habit on each slide");

            var normal = new RidePath();
            normal.Build(w, slides[0], RideStyle.Normal);
            var again = new RidePath();
            again.Build(w, slides[0], RideStyle.Sticky);
            Assert.Greater(again.Duration, normal.Duration + 3f, "it goes back up for another slide");
        }

        [Test]
        public void ShyFrogs_DislikeANoisyPiece()
        {
            float ComfortNear(float boldness)
            {
                var w = Flat();
                w.Environment.AmbientHumidity = 25f;
                w.Environment.AmbientTemperature = 31f;
                w.Environment.ResetToAmbient();
                var tunnel = Place(w, PartKind.AirTunnel, 0f, 0f);
                int rider = w.AddFrog(0f, -1.2f, 0, Sex.Male);
                int id = BoredFrog(w, 0.8f, 0.3f, boldness);
                int i = w.Agents.IndexOf(id);
                w.Agents.Play[i] = 0f;
                var needs = new NeedsSystem();
                for (int k = 0; k < 100; k++)
                {
                    int r = w.Agents.IndexOf(w.Agents.Id[rider]);
                    if (w.Agents.RidePart[r] == 0) Playground.StartRide(w, r, tunnel);
                    needs.Tick(w, 0.1f);
                }
                return w.Agents.Comfort[w.Agents.IndexOf(id)];
            }
            Assert.Less(ComfortNear(0.1f), ComfortNear(0.9f) - 0.05f);
        }

        [Test]
        public void TeleporterBlueprint_KeepsItsPair()
        {
            var w = Flat();
            Place(w, PartKind.Teleporter, -3f, 0f);
            Place(w, PartKind.Teleporter, 3f, 1f);
            var back = Blueprint.FromText(Blueprint.FromParts(w.Parts, "Beam me up").ToText());
            var w2 = Flat();
            Assert.AreEqual(2, back.PlaceInto(w2.Parts, w2.Terrain, 0f, 0f, 0f, out string why), why);
            var pads = w2.Parts.PlayPieces;
            Assert.AreEqual(2, pads.Count);
            Assert.AreSame(pads[1], Playground.PartnerOf(w2.Parts, pads[0]));
        }

        /// <summary>
        /// The playground gate in miniature: 30 minutes of an ordinary tank with three kinds of piece,
        /// nobody tending anything. Frogs use every kind on their own, nobody gets stuck on a ride,
        /// and real needs still come first.
        /// </summary>
        [Test]
        public void WatchTest_FrogsUseEveryKindOfPiece_ByThemselves()
        {
            var w = World.CreateStarterTank(11, 8, 60);
            var spots = new[]
            {
                (PartKind.Slide, -4f, 2f), (PartKind.SpiralStairs, 3f, 3f), (PartKind.AirTunnel, 1f, -2f),
                (PartKind.Elevator, -2f, -3f), (PartKind.Teleporter, 5f, -1f), (PartKind.Teleporter, -6f, 0f),
            };
            foreach (var (kind, x, z) in spots)
            {
                var p = new PlacedPart { Kind = kind, X = x, Z = z, R = 1, G = 2, B = 3 };
                w.FindDryGround(w.Random.Get("test"), x - 1f, x + 1f, z - 1f, z + 1f, out p.X, out p.Z);
                Assert.IsNotNull(w.Parts.Place(p, w.Terrain, out string why), why);
            }
            var loop = SimulationFactory.CreateDefault(w);
            float longest = 0f;
            for (int s = 0; s < 1800; s++)
            {
                Run(loop, 1f);
                var a = w.Agents;
                for (int i = 0; i < a.Count; i++) if (a.RideTime[i] > longest) longest = a.RideTime[i];
            }
            TestContext.WriteLine("rides: slide " + w.PlayRides[(int)PartKind.Slide] + ", stairs " + w.PlayRides[(int)PartKind.SpiralStairs]
                + ", tunnel " + w.PlayRides[(int)PartKind.AirTunnel] + ", lift " + w.PlayRides[(int)PartKind.Elevator]
                + ", teleporter " + w.PlayRides[(int)PartKind.Teleporter] + ", chains " + w.PlayChains + ", squabbles " + w.PlaySquabbles
                + ", frogs " + w.FrogCount() + ", escaped " + w.Escaped.Count);
            Assert.Greater(w.PlayRides[(int)PartKind.Slide], 0);
            Assert.Greater(w.PlayRides[(int)PartKind.SpiralStairs], 0);
            Assert.Greater(w.PlayRides[(int)PartKind.AirTunnel], 0);
            Assert.Greater(w.PlayRides[(int)PartKind.Elevator], 0);
            Assert.Greater(w.PlayRides[(int)PartKind.Teleporter], 0);
            Assert.Less(longest, 30f, "no frog gets stuck on a ride");
            Assert.AreEqual(0, w.Escaped.Count, "playing never stresses frogs into escaping");
        }
    }
}
