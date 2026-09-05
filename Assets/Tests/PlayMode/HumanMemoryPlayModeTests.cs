using System.Collections;
using System.Collections.Generic;
using LifeEngine.SimulatedHumans;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LifeEngine.Tests
{
    public class HumanMemoryPlayModeTests
    {
        private GameObject human;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (human != null) Object.Destroy(human);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleThreatsPersistTogetherThenExpire()
        {
            HumanMemory memory = CreateMemory(0.05f);
            Vector3 threatA = new Vector3(-3f, 0f, 4f);
            Vector3 threatB = new Vector3(4f, 0f, 2f);
            var visibleThreats = new List<Vector3> { threatA, threatB };

            List<Vector3> duringPerception = memory.GetActiveThreatPositions(visibleThreats);
            AssertContainsThreats(duringPerception, threatA, threatB);

            List<Vector3> afterPerception = memory.GetActiveThreatPositions(null);
            AssertContainsThreats(afterPerception, threatA, threatB);

            yield return new WaitForSeconds(0.08f);

            List<Vector3> afterExpiry = memory.GetActiveThreatPositions(null);
            Assert.That(afterExpiry, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PrimaryThreatStillUsesBoundedMemoryAfterBeingLost()
        {
            HumanMemory memory = CreateMemory(0.05f);
            GameObject threat = new GameObject("PrimaryThreat");
            threat.transform.position = new Vector3(0f, 0f, 5f);

            try
            {
                memory.SetPrimaryThreat(threat.transform);
                memory.SetPrimaryThreat(null);

                AssertContainsThreats(memory.GetActiveThreatPositions(null), threat.transform.position);

                yield return new WaitForSeconds(0.08f);

                Assert.That(memory.GetActiveThreatPositions(null), Is.Empty);
            }
            finally
            {
                Object.Destroy(threat);
            }
        }

        private HumanMemory CreateMemory(float duration)
        {
            human = new GameObject("MemoryTestHuman");
            HumanMemory memory = human.AddComponent<HumanMemory>();
            memory.defaultThreatMemoryDuration = duration;
            return memory;
        }

        private static void AssertContainsThreats(List<Vector3> actual, params Vector3[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            foreach (Vector3 expectedThreat in expected)
            {
                Assert.That(actual, Does.Contain(expectedThreat));
            }
        }
    }
}
