using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The tank health screen (Health button or H): one score for the whole tank, how each species
    /// is doing against what it likes, food, water, cleaning, automation, and every alert grouped
    /// by what it is about. Also the automation part of the device inspector.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Whether the tank health screen is showing.</summary>
        public bool ShowHealth;
        private Vector2 _healthScroll;
        private TankHealthReport _health;
        private float _healthTime = -1f;

        public void OpenHealth(bool open)
        {
            ShowHealth = open;
            if (!open) return;
            ShowPopulation = false;
            ShowMoney = false;
            if (ShowBuild) OpenBuildMenu(false);
            _healthTime = -1f;
        }

        private void HandleHealthKey()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.H && GUIUtility.keyboardControl == 0)
            {
                OpenHealth(!ShowHealth);
                e.Use();
            }
        }

        private static Color ScoreColour(int score) =>
            score >= 70 ? new Color(0.5f, 0.95f, 0.5f) : score >= 50 ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.45f, 0.4f);

        private void DrawHealthPanel(World world, float h)
        {
            // Worked out twice a second rather than every frame.
            if (_health == null || Time.unscaledTime - _healthTime > 0.5f || _healthTime < 0f)
            {
                _health = TankHealth.Compute(world);
                _healthTime = Time.unscaledTime;
            }
            var r0 = _health;
            const float pw = 460f;
            float ph = Mathf.Min(760f, h - 190f);
            var r = Panel(new Rect(12f, 54f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tank health", _headerStyle);
            GUILayout.FlexibleSpace();
            var old = GUI.color;
            if (r0.Frogs > 0) GUI.color = ScoreColour(r0.Score);
            GUILayout.Label(r0.Frogs > 0 ? r0.Score + " / 100: " + r0.Word : r0.Word, _headerStyle);
            GUI.color = old;
            if (GUILayout.Button("Close", GUILayout.Width(56f))) ShowHealth = false;
            GUILayout.EndHorizontal();
            _healthScroll = GUILayout.BeginScrollView(_healthScroll);

            GUILayout.Label(string.Format("{0} frogs{1}, room for {2}{3}", r0.Frogs,
                r0.Young > 0 ? " (plus " + r0.Young + " eggs or tadpoles)" : "", r0.Capacity,
                r0.Escaped > 0 ? ". " + r0.Escaped + " escaped" : ""), _labelStyle);
            if (r0.Frogs > 0)
            {
                Bar("Comfort", r0.Comfort, false, null);
                Bar("Stress", r0.Stress, true, r0.Stressed > 0 ? r0.Stressed + " stressed" : "calm");
            }
            Bar("Clean", r0.Cleanliness / 100f, false, null);
            GUILayout.Label(string.Format("Food: {0} crickets ({1}){2}", r0.Insects,
                r0.Frogs > 0 ? (r0.Insects / (float)r0.Frogs).ToString("0.0") + " per frog" : "no frogs",
                r0.Hungry > 0 ? ", " + r0.Hungry + " hungry" : ""), _smallStyle);
            GUILayout.Label((r0.HasWater ? "Water: there is a pool" : "Water: no pool! Dig below the water line")
                + (r0.Thirsty > 0 ? ", " + r0.Thirsty + " thirsty" : ""), _smallStyle);
            GUILayout.Label(string.Format("Across the tank: {0:0}–{1:0} °C, {2:0}–{3:0}% humidity",
                r0.MinTemperature, r0.MaxTemperature, r0.MinHumidity, r0.MaxHumidity), _smallStyle);

            if (r0.Species.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Each species", _labelStyle);
                foreach (var s in r0.Species)
                {
                    old = GUI.color;
                    if (s.Unhappy > 0) GUI.color = new Color(1f, 0.85f, 0.4f);
                    GUILayout.Label(string.Format("{0} × {1}: comfort {2:0}%{3}", s.Name, s.Count, s.Comfort * 100f,
                        s.Unhappy > 0 ? ", " + s.Unhappy + " uncomfortable" : ""), _smallStyle);
                    GUI.color = old;
                    GUILayout.Label(string.Format("   Like {0:0}–{1:0} °C and {2:0}–{3:0}%. Sitting in {4:0}–{5:0} °C and {6:0}–{7:0}%.",
                        s.LikesMinTemperature, s.LikesMaxTemperature, s.LikesMinHumidity, s.LikesMaxHumidity,
                        s.MinTemperature, s.MaxTemperature, s.MinHumidity, s.MaxHumidity), _smallStyle);
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("Cleaning", _labelStyle);
            GUILayout.BeginHorizontal();
            var eco = world.Economy;
            GUI.enabled = world.Dirt >= 1f;
            if (GUILayout.Button(eco.Enabled ? "Clean now (" + Economy.Format(Economy.CleaningCost) + ")" : "Clean now", GUILayout.Width(130f))) tools.CleanTank();
            GUI.enabled = true;
            for (var m = DeviceMode.Basic; m <= DeviceMode.Smart; m++)
            {
                string label = World.CleaningName(m) + (eco.ModeUnlocked(m) ? "" : " (locked)");
                if (GUILayout.Toggle(world.Cleaning == m, label, GUI.skin.button) && world.Cleaning != m) tools.SetCleaning(m);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(world.Cleaning == DeviceMode.Basic ? "Frogs make the tank dirtier over time. Above half dirty they get less comfortable, and visitors always like it clean."
                : world.Cleaning == DeviceMode.Sensor ? "The cleaning service cleans whenever the tank is 60% dirty."
                : world.Devices.HasController ? "The smart controller cleans at 30% dirty, so the tank always looks good."
                : "Smart cleaning needs a smart controller. Until then the service cleans at 60%.", _smallStyle);

            GUILayout.Space(6f);
            GUILayout.Label("Automation", _labelStyle);
            GUILayout.Label(string.Format("{0} devices ({1} on), {2} on Sensor or Smart. Smart controller: {3}.", r0.Devices, r0.DevicesOn, r0.DevicesAutomated,
                r0.Controller ? "on" : "none"), _smallStyle);
            GUILayout.Label("Click a device to choose Timer, Sensor or Smart. Smart devices are set to suit the frogs near them, so each corner can have its own climate.", _smallStyle);

            DrawGroupedAlerts(world);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string CategoryName(AlertCategory c)
        {
            switch (c)
            {
                case AlertCategory.Climate: return "Heat and humidity";
                case AlertCategory.FoodAndWater: return "Food and water";
                case AlertCategory.Frogs: return "Frogs";
                case AlertCategory.Tank: return "The tank";
                default: return "News";
            }
        }

        private void DrawGroupedAlerts(World world)
        {
            var alerts = world.Alerts.All;
            GUILayout.Space(6f);
            GUILayout.Label(alerts.Count == 0 ? "No warnings. All is well." : "Warnings", _labelStyle);
            for (var c = AlertCategory.Climate; c <= AlertCategory.News; c++)
            {
                bool header = false;
                foreach (var al in alerts)
                {
                    if (al.Category != c) continue;
                    if (!header) { GUILayout.Label(CategoryName(c), _smallStyle); header = true; }
                    GUILayout.BeginHorizontal();
                    var old = GUI.color;
                    GUI.color = al.Level == AlertLevel.Critical ? new Color(1f, 0.45f, 0.4f)
                              : al.Level == AlertLevel.Warning ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 0.9f, 1f);
                    GUILayout.BeginVertical();
                    GUILayout.Label("  " + al.Text, _smallStyle);
                    GUI.color = old;
                    if (!string.IsNullOrEmpty(al.Fix)) GUILayout.Label("  " + al.Fix, _smallStyle);
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    if ((al.X != 0f || al.Z != 0f) && GUILayout.Button("Show", GUILayout.Width(52f)) && Orbit != null)
                    {
                        Orbit.following = false;
                        Orbit.Focus(new Vector3(al.X, world.Terrain.HeightAt(al.X, al.Z), al.Z));
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        /// <summary>The Timer / Sensor / Smart buttons in the device inspector.</summary>
        private void DrawModeRow(World world, Device d)
        {
            if (d.Kind == DeviceKind.Controller) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Runs on", _labelStyle, GUILayout.Width(60f));
            for (var m = DeviceMode.Basic; m <= DeviceMode.Smart; m++)
            {
                if (!DeviceStore.HasMode(d.Kind, m)) continue;
                string label = DeviceStore.ModeName(d.Kind, m) + (world.Economy.ModeUnlocked(m) ? "" : " (locked)");
                if (GUILayout.Toggle(d.Mode == m, label, GUI.skin.button) && d.Mode != m) tools.SetDeviceMode(d, m);
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>What a Smart device says about its targets.</summary>
        private string SmartNote(World world, string what) =>
            world.Devices.HasController ? what + ", set by the smart controller to suit the frogs nearby."
                                        : what + ". Needs a smart controller to adjust it; until then it keeps this setting.";

        private void DrawControllerInspector(World world)
        {
            int smart = 0;
            foreach (var d in world.Devices.All) if (d.Mode == DeviceMode.Smart) smart++;
            GUILayout.Label(world.Devices.HasController ? "Running " + smart + (smart == 1 ? " smart device" : " smart devices") : "Switched off", _labelStyle);
            GUILayout.Label("It watches the frogs and sets every device on Smart to suit the frogs near it: heaters and misters to the middle of what they like, feeders to how hungry they are. With Smart cleaning it also keeps the tank clean.", _smallStyle);
            if (world.Economy.Enabled)
                GUILayout.Label(string.Format("Power: about ${0:0.00} a minute while on.", EconomySystem.ControllerPower * 60f), _smallStyle);
        }
    }
}
