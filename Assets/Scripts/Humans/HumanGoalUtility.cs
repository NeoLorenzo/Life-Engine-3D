using System;
using System.Collections.Generic;
using LifeEngine.AI;
using UnityEngine;

namespace LifeEngine.SimulatedHumans
{
    public enum HumanGoal
    {
        None,
        Flee,
        Sleep,
        Eat,
        SeekShelter,
        WarmUp,
        CoolDown,
        FellTree,
        Wander
    }

    [Serializable]
    public class HumanGoalScore
    {
        public HumanGoal goal;
        [Range(0f, 1f)] public float utility;
        public bool eligible;

        public HumanGoalScore(HumanGoal goal)
        {
            this.goal = goal;
        }
    }

    /// <summary>
    /// Pure scoring helpers for top-level human motivation arbitration.
    /// These methods convert authoritative simulation state into comparable utility values;
    /// they do not own or mutate physiology, thermal state, perception, or memory.
    /// </summary>
    public static class HumanGoalUtility
    {
        public static float Hunger(float ghrelin, float hungerThreshold)
        {
            float threshold = Mathf.Max(501f, hungerThreshold);
            float upper = Mathf.Max(threshold + 1f, 1600f);

            if (ghrelin <= threshold)
            {
                return Mathf.Lerp(0f, 0.5f, Mathf.InverseLerp(500f, threshold, ghrelin));
            }

            return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(threshold, upper, ghrelin));
        }

        public static float Sleep(float adenosine)
        {
            if (adenosine <= 100f)
            {
                return Mathf.Lerp(0f, 0.8f, Mathf.InverseLerp(10f, 100f, adenosine));
            }

            return Mathf.Lerp(0.8f, 1f, Mathf.InverseLerp(100f, 120f, adenosine));
        }

        public static float Warmth(float perceivedTemperature, float comfortRangeMin, HumanBrain.ThermalStatus status)
        {
            if (status != HumanBrain.ThermalStatus.Cold) return 0f;

            float severity = Mathf.Clamp01((comfortRangeMin - perceivedTemperature) / 8f);
            return Mathf.Lerp(0.5f, 1f, severity);
        }

        public static float Cooling(float perceivedTemperature, float comfortRangeMax, HumanBrain.ThermalStatus status)
        {
            if (status != HumanBrain.ThermalStatus.Hot) return 0f;

            float severity = Mathf.Clamp01((perceivedTemperature - comfortRangeMax) / 9f);
            return Mathf.Lerp(0.5f, 1f, severity);
        }

        public static float Danger(bool visibleThreat, float panicTimer, float panicPersistence)
        {
            if (visibleThreat) return 1f;
            if (panicPersistence <= 0f || panicTimer >= panicPersistence) return 0f;

            return Mathf.Clamp01(1f - (panicTimer / panicPersistence));
        }

        public static float Shelter(bool needsShelter)
        {
            return needsShelter ? 0.6f : 0f;
        }

        public static float FellTreeTest(bool enabled)
        {
            return enabled ? 0.25f : 0f;
        }

        public static bool ShouldSwitchGoal(
            float currentUtility,
            float candidateUtility,
            bool currentEligible,
            bool emergencyOverride,
            bool commitmentActive,
            float switchMargin)
        {
            if (emergencyOverride) return true;
            if (!currentEligible) return true;
            if (commitmentActive) return false;

            return candidateUtility >= currentUtility + Mathf.Max(0f, switchMargin);
        }
    }

    /// <summary>
    /// Stable behavior-tree root that executes only the currently selected goal subtree.
    /// If that subtree cannot currently act, Wander is evaluated as a procedural fallback.
    /// All goal subtrees remain children for live debugger visibility.
    /// </summary>
    public class UtilityGoalRootNode : Node
    {
        private readonly Dictionary<HumanGoal, Node> goalNodes;
        private readonly List<Node> debugChildren;
        private readonly Func<HumanGoal> currentGoalProvider;
        private readonly Node wanderFallback;

        public UtilityGoalRootNode(
            Dictionary<HumanGoal, Node> goalNodes,
            IEnumerable<HumanGoal> debugOrder,
            Func<HumanGoal> currentGoalProvider)
        {
            Name = "Utility Goal Execution";
            this.goalNodes = goalNodes;
            this.currentGoalProvider = currentGoalProvider;

            debugChildren = new List<Node>();
            foreach (HumanGoal goal in debugOrder)
            {
                if (goalNodes.TryGetValue(goal, out Node node) && !debugChildren.Contains(node))
                {
                    debugChildren.Add(node);
                }
            }

            goalNodes.TryGetValue(HumanGoal.Wander, out wanderFallback);
        }

        public override void ResetState()
        {
            base.ResetState();
            foreach (Node node in debugChildren)
            {
                node.ResetState();
            }
        }

        public override IEnumerable<Node> GetChildren()
        {
            return debugChildren;
        }

        public override NodeState Evaluate()
        {
            HumanGoal goal = currentGoalProvider != null ? currentGoalProvider() : HumanGoal.Wander;
            Name = $"Utility Goal Execution [{goal}]";

            if (!goalNodes.TryGetValue(goal, out Node selectedNode))
            {
                selectedNode = wanderFallback;
            }

            if (selectedNode == null)
            {
                state = NodeState.Failure;
                return state;
            }

            NodeState selectedState = selectedNode.Evaluate();
            if (selectedState != NodeState.Failure || selectedNode == wanderFallback || wanderFallback == null)
            {
                state = selectedState;
                return state;
            }

            state = wanderFallback.Evaluate();
            return state;
        }

        public override string GetTreeStateAsString(int indentLevel)
        {
            string result = base.GetTreeStateAsString(indentLevel);
            foreach (Node node in debugChildren)
            {
                result += node.GetTreeStateAsString(indentLevel + 1);
            }
            return result;
        }
    }
}
