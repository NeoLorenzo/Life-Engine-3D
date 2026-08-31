using System.Collections;
using LifeEngine.SimulatedHumans;
using LifeEngine.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LifeEngine.Tests
{
    public class HumanPerceptionPlayModeTests
    {
        private GameObject observer;
        private GameObject resource;
        private GameObject obstacle;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (obstacle != null) Object.Destroy(obstacle);
            if (resource != null) Object.Destroy(resource);
            if (observer != null) Object.Destroy(observer);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResourceNearTargetObstructionBlocksActualLOS()
        {
            HumanPerception perception = CreateScene();
            CreateObstacleNearResource();
            Physics.SyncTransforms();

            Assert.IsFalse(perception.PerformResourceScan(ResourceType.Log_1, out _));

            Object.Destroy(obstacle);
            obstacle = null;
            yield return null;
            Physics.SyncTransforms();

            Assert.IsTrue(perception.PerformResourceScan(ResourceType.Log_1, out Transform detected));
            Assert.AreEqual(resource.transform, detected);
        }

        private HumanPerception CreateScene()
        {
            observer = new GameObject("PerceptionObserver");
            observer.transform.forward = Vector3.forward;
            HumanPerception perception = observer.AddComponent<HumanPerception>();
            perception.resourceLayer = 1 << 10;
            perception.obstacleLayer = 1 << 6;
            perception.dangerDetectionRadius = 15f;
            perception.hearingRadius = 2f;

            resource = new GameObject("GroundResource");
            resource.layer = 10;
            resource.transform.position = new Vector3(0f, 0f, 3f);
            resource.AddComponent<ResourceItem>().type = ResourceType.Log_1;
            SphereCollider resourceCollider = resource.AddComponent<SphereCollider>();
            resourceCollider.radius = 0.1f;
            return perception;
        }

        private void CreateObstacleNearResource()
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.layer = 6;
            obstacle.transform.position = new Vector3(0f, 0.17f, 2.95f);
            obstacle.transform.localScale = new Vector3(0.2f, 0.2f, 0.05f);
        }
    }
}
