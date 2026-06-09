using UnityEngine;
using UnityEditor;
using LifeEngine.SimulatedHumans;
using LifeEngine.AI;
using System.Collections.Generic;

namespace LifeEngine.Editor
{
    public class BehaviorTreeDebugger : EditorWindow
    {
        private HumanBrain selectedBrain;
        private Vector2 scrollPosition;
        private float maxTreeWidth;
        private float maxTreeHeight;

        private class NodeLayoutData
        {
            public Node Node;
            public Rect Rect;
            public List<NodeLayoutData> Children = new List<NodeLayoutData>();
        }

        private NodeLayoutData rootLayout;
        
        // Layout constants
        private const float NodeWidth = 160f;
        private const float NodeHeight = 45f;
        private const float HorizontalSpacing = 25f;
        private const float VerticalSpacing = 60f;
        private const float CanvasPadding = 50f;

        [MenuItem("Window/Life Engine/Behavior Tree Debugger")]
        public static void ShowWindow()
        {
            GetWindow<BehaviorTreeDebugger>("Behavior Tree");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                HumanBrain newBrain = Selection.activeGameObject.GetComponentInParent<HumanBrain>();
                if (newBrain != null && newBrain != selectedBrain)
                {
                    selectedBrain = newBrain;
                    rootLayout = null; // Rebuild layout
                    Repaint();
                }
                else if (newBrain == null && selectedBrain != null)
                {
                    selectedBrain = null;
                    rootLayout = null;
                    Repaint();
                }
            }
            else
            {
                if (selectedBrain != null)
                {
                    selectedBrain = null;
                    rootLayout = null;
                    Repaint();
                }
            }
        }

        private void OnEditorUpdate()
        {
            if (Application.isPlaying && selectedBrain != null)
            {
                Repaint(); // Force continuous redraw to show active states
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Behavior Tree Debugger is only active in Play Mode.", MessageType.Info);
                return;
            }

            if (selectedBrain == null)
            {
                // Fallback check in case selection changed before opening window
                if (Selection.activeGameObject != null)
                {
                    HumanBrain potentialBrain = Selection.activeGameObject.GetComponentInParent<HumanBrain>();
                    if (potentialBrain != null)
                    {
                        selectedBrain = potentialBrain;
                        rootLayout = null;
                    }
                }

                if (selectedBrain == null)
                {
                    EditorGUILayout.HelpBox("Select a Human in the scene or game to view their Behavior Tree.", MessageType.Warning);
                    return;
                }
            }
            
            if (selectedBrain.RootNode == null)
            {
                EditorGUILayout.HelpBox("Selected Human has no initialized Behavior Tree.", MessageType.Warning);
                return;
            }

            // Build layout once per brain assignment
            if (rootLayout == null)
            {
                rootLayout = BuildLayoutTree(selectedBrain.RootNode);
                CalculateLayoutPositions(rootLayout, CanvasPadding, CanvasPadding, out float totalWidth, out float totalHeight);
                maxTreeWidth = totalWidth + (CanvasPadding * 2f);
                maxTreeHeight = totalHeight + (CanvasPadding * 2f);
            }

            // Draw graph
            Rect viewRect = new Rect(0, 0, Mathf.Max(position.width, maxTreeWidth), Mathf.Max(position.height, maxTreeHeight));
            scrollPosition = GUI.BeginScrollView(new Rect(0, 0, position.width, position.height), scrollPosition, viewRect);
            
            DrawConnections(rootLayout);
            DrawNodes(rootLayout);

            GUI.EndScrollView();
        }

        private NodeLayoutData BuildLayoutTree(Node node)
        {
            NodeLayoutData data = new NodeLayoutData() { Node = node };
            
            var children = node.GetChildren();
            if (children != null)
            {
                foreach (var child in children)
                {
                    data.Children.Add(BuildLayoutTree(child));
                }
            }
            
            return data;
        }

        // Returns the center X position of this node's horizontal span
        private float CalculateLayoutPositions(NodeLayoutData data, float startX, float startY, out float totalWidth, out float totalHeight)
        {
            if (data.Children.Count == 0)
            {
                data.Rect = new Rect(startX, startY, NodeWidth, NodeHeight);
                totalWidth = NodeWidth;
                totalHeight = NodeHeight;
                return startX + (NodeWidth / 2f);
            }

            float currentX = startX;
            float maxChildHeight = 0f;
            float childrenCenterSum = 0f;

            foreach (var child in data.Children)
            {
                float childCenter = CalculateLayoutPositions(child, currentX, startY + NodeHeight + VerticalSpacing, out float childWidth, out float childHeight);
                childrenCenterSum += childCenter;
                currentX += childWidth + HorizontalSpacing;
                if (childHeight > maxChildHeight) maxChildHeight = childHeight;
            }

            totalWidth = (currentX - startX) - HorizontalSpacing; // Remove trailing space
            totalHeight = NodeHeight + VerticalSpacing + maxChildHeight;
            float averageCenter = childrenCenterSum / data.Children.Count;

            data.Rect = new Rect(averageCenter - (NodeWidth / 2f), startY, NodeWidth, NodeHeight);
            return averageCenter;
        }

        private void DrawConnections(NodeLayoutData data)
        {
            foreach (var child in data.Children)
            {
                Vector3 startPos = new Vector3(data.Rect.center.x, data.Rect.yMax, 0);
                Vector3 endPos = new Vector3(child.Rect.center.x, child.Rect.yMin, 0);
                Vector3 startTangent = startPos + Vector3.up * (VerticalSpacing / 2f);
                Vector3 endTangent = endPos + Vector3.down * (VerticalSpacing / 2f);

                Handles.DrawBezier(startPos, endPos, startTangent, endTangent, Color.gray, null, 2f);
                
                DrawConnections(child);
            }
        }

        private void DrawNodes(NodeLayoutData data)
        {
            Color originalBackground = GUI.backgroundColor;
            
            switch (data.Node.stateValue)
            {
                case NodeState.Running:
                    GUI.backgroundColor = Color.green;
                    break;
                case NodeState.Success:
                    GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f); // Dim grey
                    break;
                case NodeState.Failure:
                    GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f); // Dim grey
                    break;
                default:
                    GUI.backgroundColor = Color.gray; // Normal grey
                    break;
            }

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;
            style.richText = true;

            string label = data.Node.Name;
            string debugText = data.Node.GetDebugText();
            if (!string.IsNullOrEmpty(debugText))
            {
                label += $"\n<color=#dddddd><size=11>{debugText}</size></color>";
            }

            GUI.Box(data.Rect, label, style);
            
            GUI.backgroundColor = originalBackground;

            foreach (var child in data.Children)
            {
                DrawNodes(child);
            }
        }
    }
}
