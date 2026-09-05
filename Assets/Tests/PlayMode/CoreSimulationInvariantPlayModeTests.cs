using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LifeEngine.AI;
using LifeEngine.Crafting;
using LifeEngine.SimulatedHumans;
using LifeEngine.SimulatedHumans.Behaviors;
using LifeEngine.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace LifeEngine.Tests
{
    public class CoreSimulationInvariantPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private NavMeshDataInstance navMeshInstance;
        private bool hasNavMeshInstance;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hasNavMeshInstance)
            {
                navMeshInstance.Remove();
                hasNavMeshInstance = false;
            }

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

        [Test]
        public void BlueprintDeliveryAcceptsOnlyNeededQuantityAndPreservesSurplus()
        {
            GameObject blueprintObject = CreateObject("BlueprintInvariantTest");
            CraftingBlueprint blueprint = blueprintObject.AddComponent<CraftingBlueprint>();
            ResourceRequirement requirement = new ResourceRequirement
            {
                type = ResourceType.Log_1,
                amountRequired = 5,
                amountCurrent = 4
            };
            blueprint.requirements.Add(requirement);

            int deliveringInventory = 3;
            int accepted = blueprint.AddResource(ResourceType.Log_1, deliveringInventory);
            deliveringInventory -= accepted;

            Assert.That(accepted, Is.EqualTo(1), "Blueprint must report only the quantity it actually absorbs.");
            Assert.That(requirement.amountCurrent, Is.EqualTo(5), "Blueprint requirements must never overfill.");
            Assert.That(deliveringInventory, Is.EqualTo(2), "The caller must retain any surplus that the blueprint did not accept.");
        }

        [UnityTest]
        public IEnumerator ProximateDestinationUpdatesPreserveStuckProgressButMaterialChangesResetIt()
        {
            BuildFlatRuntimeNavMesh();

            GameObject human = CreateObject("LocomotionInvariantTest");
            human.transform.position = Vector3.zero;
            HumanLocomotion locomotion = human.AddComponent<HumanLocomotion>();

            Rigidbody body = human.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            yield return null;

            Assert.That(locomotion.IsAgentReady(), Is.True, "Test human must be attached to the runtime NavMesh.");
            Assert.That(locomotion.SetDestination(new Vector3(2f, 0f, 0f)), Is.True);

            SetPrivateField(locomotion, "consecutiveStuckCount", 2);
            SetPrivateField(locomotion, "isCurrentlyStuck", true);

            Assert.That(locomotion.SetDestination(new Vector3(2.2f, 0f, 0f)), Is.True);
            Assert.That(locomotion.GetConsecutiveStuckCount(), Is.EqualTo(2), "A proximate replan must not erase accumulated stuck progress.");
            Assert.That(locomotion.IsCurrentlyStuck, Is.True, "A proximate replan must preserve the current stuck state.");

            Assert.That(locomotion.SetDestination(new Vector3(3f, 0f, 0f)), Is.True);
            Assert.That(locomotion.GetConsecutiveStuckCount(), Is.Zero, "A materially changed destination must establish a fresh progress baseline.");
            Assert.That(locomotion.IsCurrentlyStuck, Is.False, "A materially changed destination must clear the prior stuck state.");
        }

        [UnityTest]
        public IEnumerator DeliverResourceFailsCleanlyWhenBlueprintIsDestroyedMidAction()
        {
            GameObject blueprintObject = CreateObject("DestroyedBlueprintInvariantTest");
            CraftingBlueprint blueprint = blueprintObject.AddComponent<CraftingBlueprint>();
            HumanContext context = new HumanContext
            {
                CurrentBlueprintInstance = blueprint
            };
            DeliverResourceNode node = new DeliverResourceNode(context);

            Object.Destroy(blueprintObject);
            yield return null;

            Assert.That(context.CurrentBlueprintInstance == null, Is.True, "Unity-destroyed targets must be recognized as invalid.");
            Assert.That(node.Evaluate(), Is.EqualTo(NodeState.Failure), "Delivery must fail cleanly instead of dereferencing a destroyed blueprint.");
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private void BuildFlatRuntimeNavMesh()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(10f, 0.1f, 10f),
                    transform = Matrix4x4.TRS(new Vector3(0f, -0.05f, 0f), Quaternion.identity, Vector3.one),
                    area = 0
                }
            };

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                new Bounds(Vector3.zero, new Vector3(10f, 2f, 10f)),
                Vector3.zero,
                Quaternion.identity);

            Assert.That(data, Is.Not.Null, "Runtime NavMesh construction failed.");
            navMeshInstance = NavMesh.AddNavMeshData(data);
            hasNavMeshInstance = true;
        }

        private static void SetPrivateField<T>(HumanLocomotion locomotion, string fieldName, T value)
        {
            FieldInfo field = typeof(HumanLocomotion).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected HumanLocomotion field '{fieldName}' to exist for test setup.");
            field.SetValue(locomotion, value);
        }
    }
}
