using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace LifeEngine.AI
{
    public enum NodeState
    {
        Idle,
        Running,
        Success,
        Failure
    }

    public abstract class Node
    {
        protected NodeState state;
        public string Name { get; set; } = "Node";

        public NodeState stateValue => state;

        public virtual void ResetState()
        {
            state = NodeState.Idle;
        }

        public virtual string GetDebugText()
        {
            return string.Empty;
        }

        public abstract NodeState Evaluate();

        public virtual IEnumerable<Node> GetChildren()
        {
            return new List<Node>();
        }

        public virtual string GetTreeStateAsString(int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string stateColor = state == NodeState.Running ? "<color=yellow>Running</color>" :
                                state == NodeState.Success ? "<color=green>Success</color>" : 
                                state == NodeState.Failure ? "<color=red>Failure</color>" :
                                                             "<color=#888888>Idle</color>";
            return $"{indent}- {Name} [{stateColor}]\n";
        }

        public string ToMermaid()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("graph TD");
            int idCounter = 0;
            BuildMermaid(sb, ref idCounter, -1);
            return sb.ToString();
        }

        protected virtual void BuildMermaid(StringBuilder sb, ref int idCounter, int parentId)
        {
            int currentId = idCounter++;
            // Clean name for Mermaid compatibility
            string cleanName = Name.Replace("\"", "").Replace("(", "[").Replace(")", "]");
            
            // Style based on node type
            string startCap = "(";
            string endCap = ")"; 
            if (this is Selector) { startCap = "{{"; endCap = "}}"; }
            if (this is Sequence) { startCap = "["; endCap = "]"; }
            
            sb.AppendLine($"    Node{currentId}{startCap}\"{cleanName}\"{endCap}");
            
            if (parentId != -1)
            {
                sb.AppendLine($"    Node{parentId} --> Node{currentId}");
            }

            foreach (var child in GetChildren())
            {
                child.BuildMermaid(sb, ref idCounter, currentId);
            }
        }
    }

    public class Sequence : Node
    {
        protected List<Node> nodes = new List<Node>();

        public Sequence(string name, List<Node> nodes)
        {
            Name = name;
            this.nodes = nodes;
        }

        public Sequence(List<Node> nodes) : this("Sequence", nodes) { }

        public override void ResetState()
        {
            base.ResetState();
            foreach (var node in nodes) node.ResetState();
        }

        public override IEnumerable<Node> GetChildren()
        {
            return nodes;
        }

        public override NodeState Evaluate()
        {
            bool anyChildIsRunning = false;

            foreach (Node node in nodes)
            {
                switch (node.Evaluate())
                {
                    case NodeState.Failure:
                        state = NodeState.Failure;
                        return state;
                    case NodeState.Success:
                        continue;
                    case NodeState.Running:
                        state = NodeState.Running;
                        return state;
                    default:
                        state = NodeState.Success;
                        return state;
                }
            }

            state = anyChildIsRunning ? NodeState.Running : NodeState.Success;
            return state;
        }

        public override string GetTreeStateAsString(int indentLevel)
        {
            string result = base.GetTreeStateAsString(indentLevel);
            foreach (var node in nodes)
            {
                result += node.GetTreeStateAsString(indentLevel + 1);
            }
            return result;
        }
    }

    public class Selector : Node
    {
        protected List<Node> nodes = new List<Node>();

        public Selector(string name, List<Node> nodes)
        {
            Name = name;
            this.nodes = nodes;
        }

        public Selector(List<Node> nodes) : this("Selector", nodes) { }

        public override void ResetState()
        {
            base.ResetState();
            foreach (var node in nodes) node.ResetState();
        }

        public override IEnumerable<Node> GetChildren()
        {
            return nodes;
        }

        public override NodeState Evaluate()
        {
            foreach (Node node in nodes)
            {
                switch (node.Evaluate())
                {
                    case NodeState.Failure:
                        continue;
                    case NodeState.Success:
                        state = NodeState.Success;
                        return state;
                    case NodeState.Running:
                        state = NodeState.Running;
                        return state;
                    default:
                        continue;
                }
            }

            state = NodeState.Failure;
            return state;
        }

        public override string GetTreeStateAsString(int indentLevel)
        {
            string result = base.GetTreeStateAsString(indentLevel);
            foreach (var node in nodes)
            {
                result += node.GetTreeStateAsString(indentLevel + 1);
            }
            return result;
        }
    }

    public class ActionNode : Node
    {
        public delegate NodeState ActionNodeDelegate();
        private ActionNodeDelegate action;

        public ActionNode(string name, ActionNodeDelegate action)
        {
            Name = name;
            this.action = action;
        }

        public ActionNode(ActionNodeDelegate action) : this(action.Method.Name, action) { }

        public override NodeState Evaluate()
        {
            switch (action())
            {
                case NodeState.Success:
                    state = NodeState.Success;
                    return state;
                case NodeState.Failure:
                    state = NodeState.Failure;
                    return state;
                case NodeState.Running:
                    state = NodeState.Running;
                    return state;
                default:
                    state = NodeState.Failure;
                    return state;
            }
        }
    }
}
