using System.Collections;
using System.Collections.Generic;
using LifeEngine.Cameras;
using LifeEngine.Core;
using LifeEngine.SimulatedHumans;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LifeEngine.Tests
{
    public class SimulationCameraControllerRendererStatePlayModeTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.Destroy(createdObject);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupDoesNotEnableInitiallyDisabledHumanRenderer()
        {
            HumanBrain human = CreateHuman("Human");
            Renderer disabledRenderer = CreateRendererChild(human.transform, "DisabledBody", false);
            CreateController();

            yield return null;

            Assert.IsFalse(disabledRenderer.enabled);
        }

        [UnityTest]
        public IEnumerator FirstPersonTransitionsRestoreExactRendererStatesWithoutDrift()
        {
            AgentSelector selector = CreateSelector();
            HumanBrain human = CreateHuman("Human");
            Renderer enabledRenderer = CreateRendererChild(human.transform, "EnabledBody", true);
            Renderer disabledRenderer = CreateRendererChild(human.transform, "DisabledBody", false);
            SimulationCameraController controller = CreateController();

            yield return null;
            selector.SelectAgent(human);

            for (int transition = 0; transition < 2; transition++)
            {
                controller.SetMode(CameraMode.FirstPerson);
                Assert.IsFalse(enabledRenderer.enabled);
                Assert.IsFalse(disabledRenderer.enabled);

                controller.SetMode(CameraMode.ThirdPerson);
                Assert.IsTrue(enabledRenderer.enabled);
                Assert.IsFalse(disabledRenderer.enabled);
            }
        }

        [UnityTest]
        public IEnumerator FirstPersonLeavesExcludedRendererStatesUntouched()
        {
            AgentSelector selector = CreateSelector();
            HumanBrain human = CreateHuman("Human");
            Transform selectionVisual = CreateChild(human.transform, "SelectionVisual");
            human.selectionVisual = selectionVisual.gameObject;
            Renderer selectionRenderer = CreateRendererChild(selectionVisual, "SelectionRenderer", false);
            Renderer ringRenderer = CreateRendererChild(human.transform, "SkyRevealRing", true);
            Transform toolSlot = CreateChild(human.transform, "ToolSlot");
            human.toolSlot = toolSlot;
            Renderer toolRenderer = CreateRendererChild(toolSlot, "Tool", false);
            Transform resourceSlot = CreateChild(human.transform, "ResourceSlot");
            human.resourceSlot = resourceSlot;
            Renderer resourceRenderer = CreateRendererChild(resourceSlot, "Resource", true);
            SimulationCameraController controller = CreateController();

            yield return null;
            selector.SelectAgent(human);
            controller.SetMode(CameraMode.FirstPerson);

            Assert.IsFalse(selectionRenderer.enabled);
            Assert.IsTrue(ringRenderer.enabled);
            Assert.IsFalse(toolRenderer.enabled);
            Assert.IsTrue(resourceRenderer.enabled);

            controller.SetMode(CameraMode.Sky);
            Assert.IsFalse(selectionRenderer.enabled);
            Assert.IsTrue(ringRenderer.enabled);
            Assert.IsFalse(toolRenderer.enabled);
            Assert.IsTrue(resourceRenderer.enabled);
        }

        [UnityTest]
        public IEnumerator ChangingFirstPersonSelectionRestoresOldHumanAndHidesNewHuman()
        {
            AgentSelector selector = CreateSelector();
            HumanBrain humanA = CreateHuman("HumanA");
            Renderer aEnabled = CreateRendererChild(humanA.transform, "EnabledBody", true);
            Renderer aDisabled = CreateRendererChild(humanA.transform, "DisabledBody", false);
            HumanBrain humanB = CreateHuman("HumanB");
            Renderer bEnabled = CreateRendererChild(humanB.transform, "EnabledBody", true);
            Renderer bDisabled = CreateRendererChild(humanB.transform, "DisabledBody", false);
            SimulationCameraController controller = CreateController();

            yield return null;
            selector.SelectAgent(humanA);
            controller.SetMode(CameraMode.FirstPerson);
            selector.SelectAgent(humanB);

            Assert.IsTrue(aEnabled.enabled);
            Assert.IsFalse(aDisabled.enabled);
            Assert.IsFalse(bEnabled.enabled);
            Assert.IsFalse(bDisabled.enabled);

            controller.SetMode(CameraMode.Sky);
            Assert.IsTrue(bEnabled.enabled);
            Assert.IsFalse(bDisabled.enabled);
        }

        [UnityTest]
        public IEnumerator ClearingFirstPersonSelectionRestoresRenderersAndFallsBackToSky()
        {
            AgentSelector selector = CreateSelector();
            HumanBrain human = CreateHuman("Human");
            Renderer enabledRenderer = CreateRendererChild(human.transform, "EnabledBody", true);
            Renderer disabledRenderer = CreateRendererChild(human.transform, "DisabledBody", false);
            SimulationCameraController controller = CreateController();

            yield return null;
            selector.SelectAgent(human);
            controller.SetMode(CameraMode.FirstPerson);
            selector.ClearSelection();

            Assert.AreEqual(CameraMode.Sky, controller.CurrentMode);
            Assert.IsTrue(enabledRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);
        }

        [UnityTest]
        public IEnumerator DisablingControllerRestoresRenderersIdempotently()
        {
            AgentSelector selector = CreateSelector();
            HumanBrain human = CreateHuman("Human");
            Renderer enabledRenderer = CreateRendererChild(human.transform, "EnabledBody", true);
            Renderer disabledRenderer = CreateRendererChild(human.transform, "DisabledBody", false);
            SimulationCameraController controller = CreateController();

            yield return null;
            selector.SelectAgent(human);
            controller.SetMode(CameraMode.FirstPerson);
            controller.enabled = false;

            Assert.IsTrue(enabledRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);

            controller.enabled = true;
            controller.enabled = false;
            Assert.IsTrue(enabledRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);
        }

        [UnityTest]
        public IEnumerator DestroyingControllerRestoresRenderers()
        {
            AgentSelector selector = CreateSelector();
            HumanBrain human = CreateHuman("Human");
            Renderer enabledRenderer = CreateRendererChild(human.transform, "EnabledBody", true);
            Renderer disabledRenderer = CreateRendererChild(human.transform, "DisabledBody", false);
            SimulationCameraController controller = CreateController();

            yield return null;
            selector.SelectAgent(human);
            controller.SetMode(CameraMode.FirstPerson);
            Object.Destroy(controller.gameObject);
            yield return null;

            Assert.IsTrue(enabledRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);
        }

        private AgentSelector CreateSelector()
        {
            return CreateGameObject("Selector").AddComponent<AgentSelector>();
        }

        private SimulationCameraController CreateController()
        {
            GameObject cameraObject = CreateGameObject("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            SimulationCameraController controller = cameraObject.AddComponent<SimulationCameraController>();
            controller.targetCamera = camera;
            return controller;
        }

        private HumanBrain CreateHuman(string name)
        {
            GameObject humanObject = CreateGameObject(name);
            humanObject.SetActive(false);
            return humanObject.AddComponent<HumanBrain>();
        }

        private Renderer CreateRendererChild(Transform parent, string name, bool enabled)
        {
            GameObject rendererObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rendererObject.name = name;
            rendererObject.transform.SetParent(parent, false);
            Renderer renderer = rendererObject.GetComponent<Renderer>();
            renderer.enabled = enabled;
            return renderer;
        }

        private Transform CreateChild(Transform parent, string name)
        {
            GameObject child = CreateGameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }
    }

}
