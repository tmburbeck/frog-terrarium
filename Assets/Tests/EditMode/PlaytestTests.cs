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
    }
}
