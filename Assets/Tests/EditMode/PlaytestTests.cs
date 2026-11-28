using System;
using System.Collections.Generic;
using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 5, part 5: crash reports and opt-in playtest stats.</summary>
    public class PlaytestTests
    {
        [Test]
        public void Row_HasAValueForEveryColumn_AndNoPersonalText()
        {
            var w = World.CreateStarterTank(3, 6, 20);
            w.Economy.StartMoneyMode();
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 50; k++) loop.Advance(0.1);
            string row = PlaytestLog.Row(loop);
            var cols = PlaytestLog.Header.Split(',');
            var vals = row.Split(',');
            Assert.AreEqual(cols.Length, vals.Length, row);
            Assert.AreEqual("money", vals[Array.IndexOf(cols, "mode")]);
            Assert.AreEqual("6", vals[Array.IndexOf(cols, "frogs")]);
            Assert.AreEqual("20", vals[Array.IndexOf(cols, "insects")], "nothing ate in five seconds");
            foreach (var v in vals) Assert.IsFalse(string.IsNullOrEmpty(v));
            StringAssert.DoesNotContain(" ", row);
        }

        [Test]
        public void Row_UsesDotsForDecimals_WhateverTheComputersLanguage()
        {
            var before = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var loop = SimulationFactory.CreateDefault(World.CreateStarterTank(3, 4, 10));
                string row = PlaytestLog.Row(loop);
                Assert.AreEqual(PlaytestLog.Header.Split(',').Length, row.Split(',').Length, "a German comma would add columns");
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = before; }
        }

        [Test]
        public void Clock_GivesARowAGameMinute_AndStartsAgainForANewTank()
        {
            var w = World.CreateStarterTank(3, 4, 10);
            var loop = SimulationFactory.CreateDefault(w);
            var clock = new PlaytestLog.Clock();
            Assert.IsTrue(clock.Due(w), "a row straight away");
            Assert.IsFalse(clock.Due(w));
            int rows = 0;
            for (int k = 0; k < 1805; k++) { loop.Advance(0.1); if (clock.Due(w)) rows++; }
            Assert.AreEqual(3, rows, "three minutes, three rows");
            Assert.IsTrue(clock.Due(World.CreateStarterTank(4, 4, 10)), "a new tank starts straight away");
        }

        [Test]
        public void CrashReport_ReadsWell()
        {
            var when = new DateTime(2026, 11, 27, 15, 30, 12, DateTimeKind.Utc);
            string text = CrashReports.Build("NullReferenceException: Object reference not set",
                "FrogTerrarium.Game.GameHud.DrawThing () (at GameHud.cs:12)\nUnityEngine.GUI.CallWindowDelegate ()",
                when, new[] { new KeyValuePair<string, string>("Version", "0.5.0") }, new[] { "Saved to quicksave" });
            StringAssert.Contains("2026-11-27 15:30:12", text);
            StringAssert.Contains("Version: 0.5.0", text);
            StringAssert.Contains("NullReferenceException", text);
            StringAssert.Contains("GameHud.cs:12", text);
            StringAssert.Contains("Saved to quicksave", text);
            Assert.AreEqual("crash-20261127-153012.txt", CrashReports.FileName(when));
            Assert.AreEqual("crash-20261127-153012-2.txt", CrashReports.FileName(when, 2));
            StringAssert.Contains("(no stack trace)", CrashReports.Build("x", null, when, null, null));
        }

        [Test]
        public void SameProblem_SameSignature()
        {
            string a = CrashReports.Signature("Boom", "A.B () (at x.cs:1)\nC.D ()");
            Assert.AreEqual(a, CrashReports.Signature("Boom ", "A.B () (at x.cs:1)\nE.F ()"));
            Assert.AreNotEqual(a, CrashReports.Signature("Boom", "A.B () (at x.cs:2)"));
            Assert.AreNotEqual(a, CrashReports.Signature("Bang", "A.B () (at x.cs:1)"));
        }

        [Test]
        public void OnlyTheNewestReportsAreKept()
        {
            var names = new List<string> { "notes.txt", "crash-20260101-000000.txt", "crash-20251231-235959.txt" };
            for (int k = 0; k < CrashReports.Keep; k++) names.Add(CrashReports.FileName(new DateTime(2026, 11, 1, 0, 0, k, DateTimeKind.Utc)));
            var gone = CrashReports.ToDelete(names);
            CollectionAssert.AreEquivalent(new[] { "crash-20251231-235959.txt", "crash-20260101-000000.txt" }, gone);
            Assert.IsEmpty(CrashReports.ToDelete(new[] { "crash-20260101-000000.txt" }));
        }
            [Test]
        public void Deaths_AreCountedSeparately_ForFrogsYoungAndCrickets()
        {
            // The first playtest showed 300 "deaths" in a tank whose frogs were fine: they were crickets.
            var w = World.CreateStarterTank(5, 4, 10);
            var loop = SimulationFactory.CreateDefault(w);
            var a = w.Agents;
            for (int i = 0; i < a.Count; i++) a.Lifespan[i] = a.Kind[i] == AgentKind.Insect ? 1f : 1e9f;
            for (int k = 0; k < 30; k++) loop.Advance(0.1);
            LifecycleSystem life = null;
            foreach (var e in loop.Systems) if (e.System is LifecycleSystem l) life = l;
            Assert.AreEqual(0, life.FrogDeaths);
            Assert.GreaterOrEqual(life.InsectDeaths, 10);
            Assert.AreEqual(life.Deaths, life.FrogDeaths + life.YoungDeaths + life.InsectDeaths);
            var cols = PlaytestLog.Header.Split(',');
            var vals = PlaytestLog.Row(loop).Split(',');
            Assert.AreEqual("0", vals[Array.IndexOf(cols, "frog_deaths")]);
            Assert.AreEqual(life.InsectDeaths.ToString(), vals[Array.IndexOf(cols, "insect_deaths")]);
        }

        [Test]
        public void Balance_AStarterTankLeftAlone_EarnsALittle_AndAFullerOneEarnsMore()
        {
            // From the first playtest: a starter tank used to lose money while the player watched,
            // and a tank with six species made under 1 a minute.
            var w = World.CreateStarterTank(1, 8, 80);
            w.Economy.StartMoneyMode();
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 15 * 600; k++) loop.Advance(0.1);
            Assert.Greater(w.Economy.Money, Economy.StartingMoney + 5f, "15 minutes untouched should not lose money");

            var rich = World.CreateStarterTank(1, 8, 80);
            rich.Economy.StartMoneyMode();
            var rng = rich.Random.Get("test");
            for (int f = 0; f < 8; f++)
            {
                rich.FindDryGround(rng, -rich.HalfSizeX, rich.HalfSizeX, -rich.HalfSizeZ, rich.HalfSizeZ, out float x, out float z);
                rich.AddFrog(x, z, (byte)(1 + f % 5), f % 2 == 0 ? Sex.Female : Sex.Male);
            }
            var richLoop = SimulationFactory.CreateDefault(rich);
            for (int k = 0; k < 15 * 600; k++) richLoop.Advance(0.1);
            Assert.Greater(rich.Economy.Money - Economy.StartingMoney, 3f * (w.Economy.Money - Economy.StartingMoney), "six species draw far more visitors");
        }
    }
}
