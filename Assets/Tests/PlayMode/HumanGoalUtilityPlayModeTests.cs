using LifeEngine.SimulatedHumans;
using NUnit.Framework;

namespace LifeEngine.Tests
{
    public class HumanGoalUtilityPlayModeTests
    {
        [Test]
        public void HungerAndSleepUtilitiesPreserveExistingActivationLandmarks()
        {
            Assert.That(HumanGoalUtility.Hunger(500f, 1200f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(HumanGoalUtility.Hunger(1200f, 1200f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(HumanGoalUtility.Hunger(1600f, 1200f), Is.EqualTo(1f).Within(0.001f));

            Assert.That(HumanGoalUtility.Sleep(10f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(HumanGoalUtility.Sleep(100f), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(HumanGoalUtility.Sleep(120f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SevereColdCanOutrankNewlyTriggeredHunger()
        {
            float hunger = HumanGoalUtility.Hunger(1200f, 1200f);
            float warmth = HumanGoalUtility.Warmth(
                10f,
                18f,
                HumanBrain.ThermalStatus.Cold);

            Assert.That(warmth, Is.GreaterThan(hunger));
        }

        [Test]
        public void VisibleDangerProducesEmergencyMaximumUtility()
        {
            float danger = HumanGoalUtility.Danger(true, 999f, 4f);
            float sleepy = HumanGoalUtility.Sleep(100f);

            Assert.That(danger, Is.EqualTo(1f));
            Assert.That(danger, Is.GreaterThan(sleepy));
        }

        [Test]
        public void ResidualPanicUtilityDecaysOverPersistenceWindow()
        {
            Assert.That(HumanGoalUtility.Danger(false, 0f, 4f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(HumanGoalUtility.Danger(false, 2f, 4f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(HumanGoalUtility.Danger(false, 4f, 4f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SwitchGuardPreventsThrashingButAllowsEmergencyPreemption()
        {
            Assert.That(
                HumanGoalUtility.ShouldSwitchGoal(0.60f, 0.66f, true, false, false, 0.10f),
                Is.False,
                "Near-equal goals should not switch inside the utility margin.");

            Assert.That(
                HumanGoalUtility.ShouldSwitchGoal(0.60f, 0.90f, true, false, true, 0.10f),
                Is.False,
                "Normal candidates should not interrupt an active commitment window.");

            Assert.That(
                HumanGoalUtility.ShouldSwitchGoal(0.60f, 1.00f, true, true, true, 0.10f),
                Is.True,
                "An emergency threat must bypass commitment and hysteresis.");

            Assert.That(
                HumanGoalUtility.ShouldSwitchGoal(0.60f, 0.20f, false, false, true, 0.10f),
                Is.True,
                "An ineligible current goal must be abandoned even during its commitment window.");
        }
    }
}
