using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace LifeEngine.EditorTools
{
    public class EnvironmentPrefabBrush : EditorWindow
    {
        [MenuItem("Tools/Environment Prefab Brush")]
        public static void ShowWindow()
        {
            GetWindow<EnvironmentPrefabBrush>("Env Brush");
        }

        [Header("Brush Settings")]
        public GameObject selectedPrefab;
        public float brushRadius = 5f;
        public int objectsPerClick = 5;
        public float spawnInterval = 0.5f; // Seconds between spawns while dragging
        public LayerMask groundLayer = 1 << 0; // Default layer

        [Header("Overlap Prevention")]
        public bool preventOverlaps = true;
        public float minSpacing = 2.0f;

        [Header("Randomization")]
        public bool randomizeRotation = true;
        public bool randomizeScale = false;
        public float minScale = 0.8f;
        public float maxScale = 1.2f;

        private float lastSpawnTime = 0f;
        private bool isPainting = false;
        private Transform environmentParent;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            
            // Try to find default prefabs if none selected
            if (selectedPrefab == null)
            {
                string path = "Assets/Prefabs/Vegitation/Tree_Oak.prefab";
                selectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            GUILayout.Label("Environment Prefab Brush", EditorStyles.boldLabel);

            selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab to Paint", selectedPrefab, typeof(GameObject), false);
            brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 1f, 20f);
            objectsPerClick = EditorGUILayout.IntSlider("Objects Per Click", objectsPerClick, 1, 20);
            spawnInterval = EditorGUILayout.Slider("Stroke Interval (sec)", spawnInterval, 0.01f, 1f);
            groundLayer = LayerMaskField("Ground Layer", groundLayer);

            EditorGUILayout.Space();
            GUILayout.Label("Overlap Prevention", EditorStyles.boldLabel);
            preventOverlaps = EditorGUILayout.Toggle("Prevent Overlaps", preventOverlaps);
            if (preventOverlaps)
            {
                minSpacing = EditorGUILayout.FloatField("Min Spacing (m)", minSpacing);
            }

            EditorGUILayout.Space();
            GUILayout.Label("Randomization", EditorStyles.boldLabel);
            randomizeRotation = EditorGUILayout.Toggle("Randomize Rotation (Y)", randomizeRotation);
            randomizeScale = EditorGUILayout.Toggle("Randomize Scale", randomizeScale);
            
            if (randomizeScale)
            {
                EditorGUI.indentLevel++;
                minScale = EditorGUILayout.FloatField("Min Scale", minScale);
                maxScale = EditorGUILayout.FloatField("Max Scale", maxScale);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Find/Create Environment Parent"))
            {
                GetEnvironmentParent();
            }

            EditorGUILayout.HelpBox("Hold CTRL and Left Click/Drag in Scene View to paint clusters.", MessageType.Info);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            // Only paint if CTRL is held
            if (!e.control) return;

            // Intercept mouse events
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                isPainting = true;
                Paint(e.mousePosition);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting)
            {
                if (Time.realtimeSinceStartup - lastSpawnTime > spawnInterval)
                {
                    Paint(e.mousePosition);
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                isPainting = false;
            }

            // Draw Brush Preview
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
            {
                Handles.color = new Color(0, 1, 0, 0.1f);
                Handles.DrawSolidDisc(hit.point, hit.normal, brushRadius);
                Handles.color = Color.green;
                Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
                
                // Repaint to keep cursor updating
                sceneView.Repaint();
            }
        }

        private void Paint(Vector2 mousePos)
        {
            if (selectedPrefab == null) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
            {
                // Attempt to spawn multiple objects in a cluster
                for (int i = 0; i < objectsPerClick; i++)
                {
                    // Find potential spawn point within radius
                    Vector2 randomCircle = (i == 0) ? Vector2.zero : Random.insideUnitCircle * brushRadius;
                    Vector3 candidatePos = hit.point + new Vector3(randomCircle.x, 0, randomCircle.y);

                    // Sample ground height at the jittered point
                    Vector3 finalPos = candidatePos;
                    if (Physics.Raycast(candidatePos + Vector3.up * 50f, Vector3.down, out RaycastHit groundHit, 100f, groundLayer))
                    {
                        finalPos = groundHit.point;
                    }

                    // Check for overlaps with non-ground objects
                    if (preventOverlaps)
                    {
                        // Check everything EXCEPT the ground layer
                        if (Physics.CheckSphere(finalPos, minSpacing, ~groundLayer))
                        {
                            continue; // Skip this specific spawn if it overlaps
                        }
                    }

                    CreateObject(finalPos);
                }

                lastSpawnTime = Time.realtimeSinceStartup;
            }
        }

        private void CreateObject(Vector3 position)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
            if (go == null) return;

            Undo.RegisterCreatedObjectUndo(go, "Paint Environment Prefab");

            go.transform.position = position;
            
            if (randomizeRotation)
            {
                go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
            else
            {
                go.transform.rotation = Quaternion.identity;
            }

            if (randomizeScale)
            {
                float s = Random.Range(minScale, maxScale);
                go.transform.localScale = new Vector3(s, s, s);
            }

            if (environmentParent == null) GetEnvironmentParent();
            if (environmentParent != null) go.transform.SetParent(environmentParent);
            
            // Mark scene as dirty
            EditorUtility.SetDirty(go);
        }

        private void GetEnvironmentParent()
        {
            GameObject root = GameObject.Find("World_Environment");
            if (root == null)
            {
                root = new GameObject("World_Environment");
                Undo.RegisterCreatedObjectUndo(root, "Create Environment Parent");
            }
            environmentParent = root.transform;
        }

        private static LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            List<string> layers = new List<string>();
            List<int> layerNumbers = new List<int>();

            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (layerName != "")
                {
                    layers.Add(layerName);
                    layerNumbers.Add(i);
                }
            }

            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if (((1 << layerNumbers[i]) & layerMask.value) != 0)
                    maskWithoutEmpty |= (1 << i);
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

            int mask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) != 0)
                    mask |= (1 << layerNumbers[i]);
            }
            layerMask.value = mask;

            return layerMask;
        }
    }
}
