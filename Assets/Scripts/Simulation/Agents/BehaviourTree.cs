using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    public enum BtStatus : byte
    {
        Success,
        Failure,
        Running,
    }

    /// <summary>Everything a behaviour needs to know about the agent it is running for.</summary>
    public sealed class AgentContext
    {
        public World World;
        public AgentStore Agents;
        public int Index;
        public float Dt;
        public SeededRandom Random;
        public SpatialHash Hash;
        /// <summary>Agents removed this tick (eaten). They stay in the arrays until the tick ends.</summary>
        public bool[] Removed;
        /// <summary>Finds an agent's current index from its id, or -1.</summary>
        public Func<int, int> IndexOfId;
        /// <summary>Centres of the water cells, for frogs looking for a drink.</summary>
        public List<float> WaterPointsX, WaterPointsZ;
    }

    /// <summary>
    /// A tiny behaviour tree. A tree is built once from these nodes and shared by every agent;
    /// all per-agent memory (targets, timers) lives in the agent arrays, so the tree is simply
    /// run again from the top on every brain tick and reacts at once when things change.
    ///
    /// Sequence: runs children in order and stops at the first that does not succeed ("do A, then B").
    /// Selector: runs children in order and stops at the first that does not fail ("try A, else B").
    /// Condition: a yes/no check. Action: does something and reports how it went.
    /// </summary>
    public abstract class BtNode
    {
        protected BtNode(string name) { Name = name; }
        public string Name { get; }
        public abstract BtStatus Tick(AgentContext c);

        public static BtNode Sequence(string name, params BtNode[] children) => new SequenceNode(name, children);
        public static BtNode Selector(string name, params BtNode[] children) => new SelectorNode(name, children);
        public static BtNode Condition(string name, Func<AgentContext, bool> check) => new ConditionNode(name, check);
        public static BtNode Action(string name, Func<AgentContext, BtStatus> act) => new ActionNode(name, act);

        private sealed class SequenceNode : BtNode
        {
            private readonly BtNode[] _children;
            public SequenceNode(string name, BtNode[] children) : base(name) { _children = children; }

            public override BtStatus Tick(AgentContext c)
            {
                foreach (var child in _children)
                {
                    var s = child.Tick(c);
                    if (s != BtStatus.Success) return s;
                }
                return BtStatus.Success;
            }
        }

        private sealed class SelectorNode : BtNode
        {
            private readonly BtNode[] _children;
            public SelectorNode(string name, BtNode[] children) : base(name) { _children = children; }

            public override BtStatus Tick(AgentContext c)
            {
                foreach (var child in _children)
                {
                    var s = child.Tick(c);
                    if (s != BtStatus.Failure) return s;
                }
                return BtStatus.Failure;
            }
        }

        private sealed class ConditionNode : BtNode
        {
            private readonly Func<AgentContext, bool> _check;
            public ConditionNode(string name, Func<AgentContext, bool> check) : base(name) { _check = check; }
            public override BtStatus Tick(AgentContext c) => _check(c) ? BtStatus.Success : BtStatus.Failure;
        }

        private sealed class ActionNode : BtNode
        {
            private readonly Func<AgentContext, BtStatus> _act;
            public ActionNode(string name, Func<AgentContext, BtStatus> act) : base(name) { _act = act; }
            public override BtStatus Tick(AgentContext c) => _act(c);
        }
    }

    /// <summary>
    /// Picks what an agent should do by scoring every goal from 0 (no reason) upward and taking
    /// the highest ("utility AI"). To stop an agent flipping between two goals with similar scores,
    /// the current goal gets a bonus, and a bigger one during its first seconds.
    /// </summary>
    public sealed class UtilityGoalPicker
    {
        public float StickBonus = 0.1f;
        public float FreshBonus = 0.25f;
        public float MinCommitSeconds = 1.5f;

        private readonly List<Goal> _goals = new List<Goal>();
        private readonly List<Func<AgentContext, float>> _scorers = new List<Func<AgentContext, float>>();

        public UtilityGoalPicker Add(Goal goal, Func<AgentContext, float> score)
        {
            _goals.Add(goal);
            _scorers.Add(score);
            return this;
        }

        public IReadOnlyList<Goal> Goals => _goals;

        /// <summary>The raw score of one goal for this agent, without the stick bonus. Used by the inspector.</summary>
        public float Score(AgentContext c, Goal goal)
        {
            int k = _goals.IndexOf(goal);
            return k < 0 ? 0f : _scorers[k](c);
        }

        public Goal Pick(AgentContext c)
        {
            var a = c.Agents;
            int i = c.Index;
            Goal current = a.Goal[i];
            Goal best = current;
            float bestScore = float.MinValue;
            for (int k = 0; k < _goals.Count; k++)
            {
                float s = _scorers[k](c);
                if (_goals[k] == current && s > 0f)
                    s += a.GoalTime[i] < MinCommitSeconds ? FreshBonus : StickBonus;
                if (s > bestScore) { bestScore = s; best = _goals[k]; }
            }
            return best;
        }
    }
}
