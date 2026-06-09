using UnityEngine;

namespace LifeEngine.World
{
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }
        public Transform directionalLight;
        
        public Vector3 SunDirection => directionalLight != null ? directionalLight.forward : Vector3.down;
        public bool IsDaylight => sun != null && sun.intensity > 0.1f;
        [Header("Visual Interpolation")]
        [Header("Sun & Moon")]
        public Gradient lightColor;
        public AnimationCurve lightIntensity;
        
        [Header("Ambient & Reflections")]
        public Gradient ambientColor;
        public AnimationCurve ambientIntensity;
        public AnimationCurve reflectionIntensity;

        [Header("Fog")]
        public Gradient fogColor;
        public AnimationCurve fogDensity;
        
        private Light sun;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (directionalLight != null)
                sun = directionalLight.GetComponent<Light>();
                
            // Provide sensible defaults if empty
            if (lightColor == null || lightColor.colorKeys == null || lightColor.colorKeys.Length == 0)
            {
                lightColor = new Gradient();
                lightColor.SetKeys(
                    new GradientColorKey[] { 
                        new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 0.0f), // Midnight
                        new GradientColorKey(new Color(0.8f, 0.4f, 0.2f), 0.25f), // Sunrise (6AM)
                        new GradientColorKey(Color.white, 0.5f), // Noon (12PM)
                        new GradientColorKey(new Color(0.8f, 0.4f, 0.2f), 0.75f), // Sunset (18PM)
                        new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 1.0f) // Midnight
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }
            
            if (lightIntensity == null || lightIntensity.keys == null || lightIntensity.keys.Length == 0)
            {
                lightIntensity = new AnimationCurve();
                lightIntensity.AddKey(0.0f, 0.1f); // Midnight
                lightIntensity.AddKey(0.24f, 0.1f); // Just before sunrise
                lightIntensity.AddKey(0.26f, 1.0f); // Just after sunrise
                lightIntensity.AddKey(0.5f, 1.2f); // Noon
                lightIntensity.AddKey(0.74f, 1.0f); // Just before sunset
                lightIntensity.AddKey(0.76f, 0.1f); // Just after sunset
                lightIntensity.AddKey(1.0f, 0.1f); // Midnight
            }

            // --- Ambient Defaults ---
            if (ambientColor == null || ambientColor.colorKeys == null || ambientColor.colorKeys.Length == 0)
            {
                ambientColor = new Gradient();
                ambientColor.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0.0f), // Deep night blue
                        new GradientColorKey(new Color(0.5f, 0.5f, 0.6f), 0.5f),   // Standard sky day
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 1.0f) 
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            if (ambientIntensity == null || ambientIntensity.keys == null || ambientIntensity.keys.Length == 0)
            {
                ambientIntensity = new AnimationCurve();
                ambientIntensity.AddKey(0f, 0.05f);   // Darkest Night
                ambientIntensity.AddKey(0.5f, 1.0f);  // Brightest Day
                ambientIntensity.AddKey(1f, 0.05f);
            }

            if (reflectionIntensity == null || reflectionIntensity.keys == null || reflectionIntensity.keys.Length == 0)
            {
                reflectionIntensity = new AnimationCurve();
                reflectionIntensity.AddKey(0f, 0.1f);  // Dull Night
                reflectionIntensity.AddKey(0.5f, 1.0f); // Shiny Day
                reflectionIntensity.AddKey(1f, 0.1f);
            }

            // --- Fog Defaults ---
            if (fogColor == null || fogColor.colorKeys == null || fogColor.colorKeys.Length == 0)
            {
                fogColor = new Gradient();
                fogColor.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0f, 0f, 0.05f), 0f), // Night
                        new GradientColorKey(new Color(0.7f, 0.8f, 0.9f), 0.5f), // Day
                        new GradientColorKey(new Color(0f, 0f, 0.05f), 1f)
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            if (fogDensity == null || fogDensity.keys == null || fogDensity.keys.Length == 0)
            {
                fogDensity = new AnimationCurve();
                fogDensity.AddKey(0f, 0.005f);   // Thick Night
                fogDensity.AddKey(0.5f, 0.005f); // Clear Day
                fogDensity.AddKey(1f, 0.005f);
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }

        private void Update()
        {
            if (TimeManager.Instance == null || directionalLight == null) return;

            float time = TimeManager.Instance.currentTimeHours;
            
            // Map 0 -> 24 hours to 0 -> 1 cycle for gradients
            float timeNormalized = time / 24f;

            // Sunrise at 6:00 (X = 0), Noon is 12:00 (X = 90), Sunset is 18:00 (X = 180).
            float sunRotationX = ((time - 6f) / 24f) * 360f;
            directionalLight.rotation = Quaternion.Euler(sunRotationX, -30f, 0f);

            if (sun != null)
            {
                sun.color = lightColor.Evaluate(timeNormalized);
                sun.intensity = lightIntensity.Evaluate(timeNormalized);
            }

            // Apply Environment Settings
            RenderSettings.ambientLight = ambientColor.Evaluate(timeNormalized);
            RenderSettings.ambientIntensity = ambientIntensity.Evaluate(timeNormalized);
            RenderSettings.reflectionIntensity = reflectionIntensity.Evaluate(timeNormalized);

            // Apply Fog
            RenderSettings.fogColor = fogColor.Evaluate(timeNormalized);
            RenderSettings.fogDensity = fogDensity.Evaluate(timeNormalized);
        }
    }
}
