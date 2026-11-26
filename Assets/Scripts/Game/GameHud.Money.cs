using System;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Money mode on screen: the money bar at the top left, the Money menu (totals, orders from
    /// buyers, research), and the Sell and contract buttons in the frog inspector.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Whether the Money menu is showing.</summary>
        public bool ShowMoney;
        private const float MoneyBarWidth = 580f;
        private Vector2 _moneyScroll;

        public void OpenMoneyMenu(bool open)
        {
            ShowMoney = open;
            if (!open) return;
            ShowPopulation = false;
            ShowHealth = false;
            if (ShowBuild) OpenBuildMenu(false);
        }

        private void DrawMoneyBar(World world)
        {
            var eco = world.Economy;
            var r = Panel(new Rect(12f, 10f, MoneyBarWidth, 36f));
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 5f, r.width - 16f, r.height - 8f));
            GUILayout.BeginHorizontal();
            if (eco.Enabled)
            {
                var old = GUI.color;
                if (eco.Money < 0) GUI.color = new Color(1f, 0.5f, 0.45f);
                GUILayout.Label(Economy.Format(eco.Money), _headerStyle, GUILayout.Width(90f));
                GUI.color = old;
                GUILayout.Label(string.Format("Reputation {0:0}   {1:0.0} visitors/min   costs {2}/min   {3} research pts",
                    eco.Reputation, eco.VisitorsPerMinute, Economy.Format(eco.CostPerMinute), eco.ResearchPoints), _smallStyle, GUILayout.Width(380f));
            }
            else
            {
                GUILayout.Label("Sandbox: everything is free", _labelStyle, GUILayout.Width(470f));
            }
            GUILayout.FlexibleSpace();
            string label = eco.Contracts.Count > 0 && eco.Enabled ? "Money (" + eco.Contracts.Count + ")" : "Money";
            bool show = GUILayout.Toggle(ShowMoney, label, GUI.skin.button, GUILayout.Width(84f));
            if (show != ShowMoney) OpenMoneyMenu(show);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawMoneyPanel(World world, float h)
        {
            var eco = world.Economy;
            const float pw = 420f;
            float ph = Mathf.Min(720f, h - 190f);
            var r = Panel(new Rect(12f, 54f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.Label("Money", _headerStyle);

            GUI.enabled = !world.InScenario; // a scenario decides whether money counts
            bool on = GUILayout.Toggle(eco.Enabled, " Money mode (off is the sandbox: everything free and unlocked)");
            GUI.enabled = true;
            if (on != eco.Enabled)
            {
                if (on && eco.Money == 0 && eco.Visitors == 0 && eco.Researched == 0) eco.StartMoneyMode();
                else eco.Enabled = on;
            }
            _moneyScroll = GUILayout.BeginScrollView(_moneyScroll);
            if (!eco.Enabled)
            {
                GUILayout.Label("Switch money mode on to earn money from visitors, sales and buyers' orders, "
                    + "and to research new species and parts.", _smallStyle);
                DrawSandboxRules(world);
            }
            else
            {
                GUILayout.Label(string.Format("{0} in the bank. {1} visitors so far, {2} frogs sold.",
                    Economy.Format(eco.Money), eco.Visitors, eco.Sold), _labelStyle);
                Bar("Reputation", eco.Reputation / 100f, false, eco.Reputation.ToString("0"));
                GUILayout.Label(string.Format("Appeal {0:0}: {1:0.0} visitors a minute, each paying about {2}.",
                    eco.Appeal, eco.VisitorsPerMinute, Economy.Format(EconomySystem.Ticket + eco.Appeal / 100f)), _smallStyle);
                GUILayout.Label("Visitors like many species, hybrids, mutations and new species, plenty of frogs, "
                    + "and lots of different decorations and colours. Stressed frogs put them off; escapes hurt your reputation.", _smallStyle);
                GUILayout.Label(string.Format("Earned: visitors {0}, sales {1}, orders {2}.   Spent: shop {3}, power {4}, upkeep {5}.",
                    Economy.Format(eco.EarnedVisitors), Economy.Format(eco.EarnedSales), Economy.Format(eco.EarnedContracts),
                    Economy.Format(eco.SpentShop), Economy.Format(eco.SpentPower), Economy.Format(eco.SpentUpkeep)), _smallStyle);
                GUILayout.Space(8f);

                GUILayout.Label("Orders from buyers", _labelStyle);
                if (eco.Contracts.Count == 0) GUILayout.Label("None right now. New orders come every few minutes.", _smallStyle);
                foreach (var c in eco.Contracts)
                {
                    float minutes = (float)Math.Max(0.0, (c.Deadline - world.SimTime) / 60.0);
                    GUILayout.Label(string.Format("• {0} wants {1}{2}. Pays {3}. {4:0} min left.", c.Buyer, c.Describe(world),
                        c.Delivered > 0 ? " (" + c.Delivered + " given)" : "", Economy.Format(c.Reward), Math.Ceiling(minutes)), _smallStyle);
                }
                if (eco.Contracts.Count > 0)
                    GUILayout.Label("Click a frog that fits and use its order button to hand it over.", _smallStyle);
                GUILayout.Space(8f);

                GUILayout.Label("Research (" + eco.ResearchPoints + " points)", _labelStyle);
                GUILayout.Label("Points come from new mutations and hybrids (2), registering a species (4), finished orders (1) "
                    + "and every 50 visitors (1).", _smallStyle);
                foreach (var info in Economy.AllResearch)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(info.Name + ": " + info.Description, _smallStyle, GUILayout.Width(250f));
                    if (eco.Has(info.Id)) GUILayout.Label("Done", _smallStyle, GUILayout.Width(110f));
                    else
                    {
                        GUI.enabled = eco.ResearchPoints >= info.Points && eco.Money >= info.Money;
                        if (GUILayout.Button(info.Points + " pts + " + Economy.Format(info.Money), GUILayout.Width(110f)))
                            tools.LastAction = eco.DoResearch(info.Id, out string why) ? "Researched " + info.Name + "!" : why;
                        GUI.enabled = true;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(tools.LastAction) && Time.unscaledTime - tools.LastActionTime < 8f) GUILayout.Label(tools.LastAction, _smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(60f))) ShowMoney = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static int MatchingContracts(World world, int i)
        {
            int n = 0;
            foreach (var c in world.Economy.Contracts) if (c.Matches(world, i)) n++;
            return n;
        }

        /// <summary>The inspector's Sell button and a button for each order this frog would fill.</summary>
        private void DrawFrogSales(World world, int i, int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Worth about " + Economy.Format(Economy.SaleValue(world, i)), _smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Sell", GUILayout.Width(60f))) tools.SellFrog(id);
            GUILayout.EndHorizontal();
            foreach (var c in world.Economy.Contracts.ToArray())
            {
                if (!c.Matches(world, i)) continue;
                if (GUILayout.Button(string.Format("Give to {0}'s order ({1}/{2}, pays {3})", c.Buyer.Replace("A ", "the "), c.Delivered + 1, c.Needed, Economy.Format(c.Reward))))
                    tools.DeliverFrog(c.Id, id);
            }
        }
    }
}
