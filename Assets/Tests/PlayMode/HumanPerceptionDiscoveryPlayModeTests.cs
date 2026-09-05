using System.Collections;
using LifeEngine.SimulatedHumans;
using LifeEngine.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LifeEngine.Tests
{
    public class HumanPerceptionDiscoveryPlayModeTests
    {
        private GameObject observer;
        private GameObject target;
        private GameObject obstacle;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (obstacle != null) Object.Destroy(obstacle);
            if (target != null) Object.Destroy(target);
            if (observer != null) Object.Destroy(observer);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HarvestableSourceScanRejectsOccludedTreeAndAcceptsVisibleTree()
        {
            HumanPerception perception = CreatePerception();
            FellableTree tree = CreateTree(new Vector3(0f, 0f, 4f), 3f);
            tree.requiresTool = false;
            tree.resourceDrops = new[]
            {
                new ResourceSpawnGroup { type = ResourceType.Log_1, spawnPoints = new Transform[0] }
            };

            CreateObstacle(new Vector3(0f, 0.6f, 2f), new Vector3(1f, 2f, 0.2f));
            Physics.SyncTransforms();

            Assert.IsFalse(perception.PerformHarvestableSourceScan(false, ResourceType.Log_1, true, out _));

            Object.Destroy(obstacle);
            obstacle = null;
            yield return null;
            Physics.SyncTransforms();

            Assert.IsTrue(perception.PerformHarvestableSourceScan(false, ResourceType.Log_1, true, out FellableTree detected));
            Assert.AreEqual(tree, detected);
        }

        [UnityTest]
        public IEnumerator ToolScanRejectsOutOfRangeToolAndAcceptsVisibleTool()
        {
            HumanPerception perception = CreatePerception();
            target = new GameObject("BasicAxe");
            target.transform.position = new Vector3(0f, 0f, 20f);
            target.AddComponent<SphereCollider>().radius = 0.2f;
            ToolItem tool = target.AddComponent<ToolItem>();
            tool.toolName = "Basic_Axe";
            Physics.SyncTransforms();

            Assert.IsFalse(perception.PerformToolScan("Basic_Axe", out _));

            target.transform.position = new Vector3(0f, 0f, 4f);
            Physics.SyncTransforms();

            Assert.IsTrue(perception.PerformToolScan("Basic_Axe", out ToolItem detected));
            Assert.AreEqual(tool, detected);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShadeTreeScanRejectsOccludedTreeAndAcceptsVisibleTallTree()
        {
            HumanPerception perception = CreatePerception();
            FellableTree tree = CreateTree(new Vector3(0f, 0f, 5f), 3f);
            CreateObstacle(new Vector3(0f, 0.6f, 2.5f), new Vector3(1f, 2f, 0.2f));
            Physics.SyncTransforms();

            Assert.IsFalse(perception.PerformShadeTreeScan(2f, out _));

            Object.Destroy(obstacle);
            obstacle = null;
            yield return null;
            Physics.SyncTransforms();

            Assert.IsTrue(perception.PerformShadeTreeScan(2f, out FellableTree detected));
            Assert.AreEqual(tree, detected);
        }

        private HumanPerception CreatePerception()
        {
            observer = new GameObject("PerceptionObserver");
            observer.transform.forward = Vector3.forward;
            HumanPerception perception = observer.AddComponent<HumanPerception>();
            perception.treeLayer = 1 << 9;
            perception.obstacleLayer = 1 << 6;
            perception.dangerDetectionRadius = 15f;
            perception.hearingRadius = 2f;
            return perception;
        }

        private FellableTree CreateTree(Vector3 position, float height)
        {
            target = new GameObject("PerceivedTree");
            target.layer = 9;
            target.transform.position = position;
            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, height, 1f);
            return target.AddComponent<FellableTree>();
        }

        private void CreateObstacle(Vector3 position, Vector3 scale)
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.layer = 6;
            obstacle.transform.position = position;
            obstacle.transform.localScale = scale;
        }
    }
}
