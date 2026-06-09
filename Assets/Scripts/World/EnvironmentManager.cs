using UnityEngine;

namespace LifeEngine.World
{
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        [Header("Temperature Settings (Celsius)")]
        [Tooltip("X-axis: 0..24 hours, Y-axis: Celsius")]
        public AnimationCurve temperatureCurve;

        [Header("Current State")]
        [SerializeField, ReadOnly] private float currentBaseTemperature;
        public float BaseTemperature => currentBaseTemperature;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Reset()
        {
            // Reset runs when the component is first added or manually reset in the editor.
            // This provides defaults without overriding manual changes at runtime.
            temperatureCurve = new AnimationCurve();
            temperatureCurve.AddKey(0f, 5f);   // Midnight: 5°C
            temperatureCurve.AddKey(6f, 8f);   // 6 AM: 8°C
            temperatureCurve.AddKey(12f, 22f); // Noon: 22°C
            temperatureCurve.AddKey(14f, 25f); // 2 PM: 25°C
            temperatureCurve.AddKey(18f, 18f); // 6 PM: 18°C
            temperatureCurve.AddKey(24f, 5f);  // Midnight: 5°C
        }

        private void Update()
        {
            if (TimeManager.Instance != null && temperatureCurve != null)
            {
                currentBaseTemperature = temperatureCurve.Evaluate(TimeManager.Instance.currentTimeHours);
            }
        }
    }

    // Simple custom attribute to help differentiate runtime values in inspector
    public class ReadOnlyAttribute : PropertyAttribute { }
}
