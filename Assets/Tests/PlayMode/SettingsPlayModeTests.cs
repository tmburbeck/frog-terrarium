using FrogTerrarium.Game;
using NUnit.Framework;
using UnityEngine;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 4 part 4: changing keys. (Opening the Settings window in a running game is in SandboxPlayModeTests.)</summary>
    public class SettingsPlayModeTests
    {
        [Test]
        public void DefaultKeys_AreTheOldKeys_AndNeverClash()
        {
            var b = new KeyBindings();
            Assert.IsTrue(b.IsDefault);
            Assert.IsTrue(b.Uses(GameAction.Pause, KeyCode.P));
            Assert.IsTrue(b.Uses(GameAction.PanForward, KeyCode.W));
            Assert.IsTrue(b.Uses(GameAction.PanForward, KeyCode.UpArrow));
            Assert.IsTrue(b.Uses(GameAction.StatsOverlay, KeyCode.O));
            Assert.IsTrue(b.Uses(GameAction.QuickSave, KeyCode.F5));
            var seen = new System.Collections.Generic.HashSet<KeyCode>();
            foreach (var a in KeyBindings.Actions)
                for (int s = 0; s < KeyBindings.Slots; s++)
                {
                    var k = b.Get(a, s);
                    if (k == KeyCode.None) continue;
                    Assert.IsFalse(KeyBindings.IsReserved(k), a + " starts on a reserved key");
                    Assert.IsTrue(seen.Add(k), k + " is used twice");
                }
        }

        [Test]
        public void SettingAKeyTakesItFromTheOtherAction()
        {
            var b = new KeyBindings();
            string said = b.Set(GameAction.Pause, 0, KeyCode.Space);
            StringAssert.Contains("Space", said);
            Assert.IsTrue(b.Uses(GameAction.Pause, KeyCode.Space));
            Assert.IsFalse(b.Uses(GameAction.Pause, KeyCode.P));

            said = b.Set(GameAction.Pause, 0, KeyCode.H);
            StringAssert.Contains("Tank health screen", said, "says which action lost the key");
            Assert.IsFalse(b.Uses(GameAction.HealthScreen, KeyCode.H));
            Assert.AreEqual(GameAction.Pause, b.ActionOf(KeyCode.H));

            said = b.Set(GameAction.Pause, 1, KeyCode.Escape);
            StringAssert.Contains("can't", said);
            Assert.IsFalse(b.Uses(GameAction.Pause, KeyCode.Escape));
            Assert.IsFalse(b.IsDefault);
            b.ResetAll();
            Assert.IsTrue(b.IsDefault);
        }

        [Test]
        public void Keys_AreRememberedAsText_AndBrokenTextKeepsDefaults()
        {
            var b = new KeyBindings();
            b.Set(GameAction.TurnPart, 0, KeyCode.E);
            b.Set(GameAction.ToggleGrid, 1, KeyCode.Tab);
            b.Clear(GameAction.SpeedTest, 1);
            var back = KeyBindings.FromText(b.ToText());
            Assert.AreEqual(b.ToText(), back.ToText());
            Assert.IsTrue(back.Uses(GameAction.TurnPart, KeyCode.E));
            Assert.IsFalse(back.Uses(GameAction.SpeedTest, KeyCode.B));

            Assert.IsTrue(KeyBindings.FromText("").IsDefault);
            Assert.IsTrue(KeyBindings.FromText("nonsense;;Pause=Banana,P;NoSuchAction=W,None").IsDefault);

            // A key the player chose wins over the default of an action they never saw.
            var mine = KeyBindings.FromText("Pause=W,None");
            Assert.IsTrue(mine.Uses(GameAction.Pause, KeyCode.W));
            Assert.IsFalse(mine.Uses(GameAction.PanForward, KeyCode.W));
            Assert.IsTrue(mine.Uses(GameAction.PanForward, KeyCode.UpArrow));
            // The same key twice in the text is only kept once.
            var twice = KeyBindings.FromText("Pause=K,None;Speed1=K,None");
            Assert.IsTrue(twice.Uses(GameAction.Pause, KeyCode.K));
            Assert.IsFalse(twice.Uses(GameAction.Speed1, KeyCode.K));
        }

        [Test]
        public void KeyNames_AreShort()
        {
            Assert.AreEqual("1", KeyBindings.KeyName(KeyCode.Alpha1));
            Assert.AreEqual("[", KeyBindings.KeyName(KeyCode.LeftBracket));
            Assert.AreEqual("Up", KeyBindings.KeyName(KeyCode.UpArrow));
            Assert.AreEqual("F5", KeyBindings.KeyName(KeyCode.F5));
            Assert.AreEqual("-", KeyBindings.KeyName(KeyCode.None));
        }
    }
}
