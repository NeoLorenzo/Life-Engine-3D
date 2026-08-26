using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using LifeEngine.Cameras;
using LifeEngine.UI;

namespace LifeEngine.Editor
{
    public static class CameraSystemSetup
    {
        [MenuItem("LifeEngine/Force Recompile Scripts")]
        public static void ForceRecompile()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[CameraSystemSetup] Requested script compilation!");
        }

        [MenuItem("LifeEngine/Setup Camera System & Prefabs")]
        public static void SetupAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SetupPrefabs();
            SetupScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[CameraSystemSetup] Successfully completed camera system and prefab configuration!");
        }

        private static void SetupPrefabs()
        {
            // 1. Agent Prefabs
            string[] agentPrefabPaths = new string[]
            {
                "Assets/Prefabs/Agents/male01_1.prefab",
                "Assets/Prefabs/Agents/male01_2.prefab",
                "Assets/Prefabs/Agents/male01_3.prefab",
                "Assets/Prefabs/Agents/male02_1.prefab",
                "Assets/Prefabs/Agents/male02_2.prefab",
                "Assets/Prefabs/Agents/male02_3.prefab",
                "Assets/Prefabs/Agents/male03_1.prefab",
                "Assets/Prefabs/Agents/male03_2.prefab",
                "Assets/Prefabs/Agents/male03_3.prefab",
                "Assets/Prefabs/Agents/Human 1.prefab"
            };

            foreach (var path in agentPrefabPaths)
            {
                ConfigurePrefab(path, 1.75f, 1.75f, new Color(0.2f, 0.9f, 1.0f, 0.85f));
            }

            // 2. Campfire Prefabs
            string[] campfirePaths = new string[]
            {
                "Assets/Prefabs/Campfires/Small Campfire.prefab",
                "Assets/Prefabs/Campfires/Tiny Campfire.prefab",
                "Assets/Prefabs/Campfires/Blueprint_Tiny_Campfire.prefab"
            };

            foreach (var path in campfirePaths)
            {
                ConfigurePrefab(path, 2.5f, 2.5f, new Color(1.0f, 0.6f, 0.1f, 0.85f));
            }

            // 3. Shelter Prefabs
            string[] shelterPaths = new string[]
            {
                "Assets/Prefabs/Shelter/Basic Shelter.prefab"
            };

            foreach (var path in shelterPaths)
            {
                ConfigurePrefab(path, 4.0f, 4.0f, new Color(0.3f, 1.0f, 0.4f, 0.85f));
            }

            // 4. Tool Blueprint Prefabs
            string[] toolBlueprintPaths = new string[]
            {
                "Assets/Prefabs/Tools/Blueprint_Basic_Axe.prefab"
            };

            foreach (var path in toolBlueprintPaths)
            {
                ConfigurePrefab(path, 1.5f, 1.5f, new Color(1.0f, 0.6f, 0.1f, 0.85f));
            }
        }

        private static void ConfigurePrefab(string path, float revealRadius, float ringRadius, Color ringColor)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents != null)
            {
                var target = contents.GetComponent<SkyRevealTarget>();
                if (target == null)
                {
                    target = contents.AddComponent<SkyRevealTarget>();
                }
                target.revealRadius = revealRadius;
                target.drawGizmo = true;
                target.gizmoColor = ringColor;

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void SetupScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null) mainCam = camObj.GetComponent<Camera>();
                if (mainCam == null) mainCam = Object.FindFirstObjectByType<Camera>();
            }

            if (mainCam == null)
            {
                Debug.LogWarning("[CameraSystemSetup] No MainCamera found in active scene.");
                return;
            }

            // 1. Ensure SkyCameraAnchor
            GameObject anchorObj = GameObject.Find("SkyCameraAnchor");
            if (anchorObj == null)
            {
                anchorObj = new GameObject("SkyCameraAnchor");
                anchorObj.transform.position = mainCam.transform.position;
                anchorObj.transform.rotation = mainCam.transform.rotation;
                Undo.RegisterCreatedObjectUndo(anchorObj, "Create SkyCameraAnchor");
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(mainCam.gameObject);

            // 2. Setup Camera Components
            var camController = mainCam.GetComponent<SimulationCameraController>();
            if (camController == null)
            {
                camController = Undo.AddComponent<SimulationCameraController>(mainCam.gameObject);
            }
            camController.targetCamera = mainCam;
            camController.skyCameraAnchor = anchorObj.transform;

            var revealController = mainCam.GetComponent<SkyRevealController>();
            if (revealController == null)
            {
                revealController = Undo.AddComponent<SkyRevealController>(mainCam.gameObject);
            }



            // 3. Setup UI in GameHUD_Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform existingPanel = canvas.transform.Find("CameraControlsPanel");
                GameObject panelObj;
                if (existingPanel != null)
                {
                    panelObj = existingPanel.gameObject;
                }
                else
                {
                    panelObj = new GameObject("CameraControlsPanel", typeof(RectTransform));
                    panelObj.transform.SetParent(canvas.transform, false);
                    Undo.RegisterCreatedObjectUndo(panelObj, "Create CameraControlsPanel");
                }

                RectTransform panelRect = panelObj.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 1f);
                panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 1f);
                panelRect.anchoredPosition = new Vector2(0f, -10f);
                panelRect.sizeDelta = new Vector2(360f, 40f);

                // Add HorizontalLayoutGroup
                var hlg = panelObj.GetComponent<HorizontalLayoutGroup>();
                if (hlg == null) hlg = panelObj.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 8f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;

                // Create buttons
                Button fpBtn = CreateOrGetButton(panelObj.transform, "FirstPersonButton", "1: First Person");
                Button tpBtn = CreateOrGetButton(panelObj.transform, "ThirdPersonButton", "2: Third Person");
                Button skyBtn = CreateOrGetButton(panelObj.transform, "SkyButton", "3: Sky");

                var uiController = panelObj.GetComponent<CameraControlsUI>();
                if (uiController == null) uiController = panelObj.AddComponent<CameraControlsUI>();
                uiController.firstPersonButton = fpBtn;
                uiController.thirdPersonButton = tpBtn;
                uiController.skyButton = skyBtn;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            bool savedAll = EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[CameraSystemSetup] Scene saved: {saved}, SaveOpenScenes: {savedAll}");
        }

        private static Button CreateOrGetButton(Transform parent, string name, string labelText)
        {
            Transform existing = parent.Find(name);
            GameObject btnObj;
            if (existing != null)
            {
                btnObj = existing.gameObject;
            }
            else
            {
                btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(parent, false);
            }

            var image = btnObj.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);

            var button = btnObj.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            // Label
            Transform textTrans = btnObj.transform.Find("Text");
            GameObject textObj;
            if (textTrans != null)
            {
                textObj = textTrans.gameObject;
            }
            else
            {
                textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(btnObj.transform, false);
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObj.GetComponent<Text>();
            text.text = labelText;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 13;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.color = Color.white;

            return button;
        }
    }
}
