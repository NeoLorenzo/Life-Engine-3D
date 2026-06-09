using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeEngine.World
{
    [ExecuteAlways]
    public class VFX_FireController : MonoBehaviour
    {
        [Serializable]
        public struct VFXSettings
        {
            public float startLifetime;
            public float startSize;
            public float emissionRate;
            public float shapeRadius;
            public int maxParticles;

            public static VFXSettings Lerp(VFXSettings a, VFXSettings b, float t)
            {
                return new VFXSettings
                {
                    startLifetime = Mathf.Lerp(a.startLifetime, b.startLifetime, t),
                    startSize = Mathf.Lerp(a.startSize, b.startSize, t),
                    emissionRate = Mathf.Lerp(a.emissionRate, b.emissionRate, t),
                    shapeRadius = Mathf.Lerp(a.shapeRadius, b.shapeRadius, t),
                    maxParticles = (int)Mathf.Lerp(a.maxParticles, b.maxParticles, t)
                };
            }
        }

        [Header("Global Control")]
        [SerializeField, Range(0f, 10f)] private float fireIntensity = 1f;
        [SerializeField] private Vector3 fireWindDirection = Vector3.zero;

        [Header("Fire Colors")]
        [SerializeField] private Color fireColor = new Color(1f, 0.5f, 0.2f, 1f);
        [SerializeField] private Color smokeColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        [Header("Fire (Volume_Out) Scaling")]
        [SerializeField] private VFXSettings fireMin = new VFXSettings { startLifetime = 1, startSize = 0.08f, emissionRate = 30, shapeRadius = 0.1f, maxParticles = 5000 };
        [SerializeField] private VFXSettings fireMax = new VFXSettings { startLifetime = 3, startSize = 0.1f, emissionRate = 1000, shapeRadius = 1f, maxParticles = 5000 };

        [Header("Smoke Scaling")]
        [SerializeField] private VFXSettings smokeMin = new VFXSettings { startLifetime = 5, startSize = 0.02f, emissionRate = 10, shapeRadius = 0.15f, maxParticles = 5000 };
        [SerializeField] private VFXSettings smokeMax = new VFXSettings { startLifetime = 5, startSize = 0.06f, emissionRate = 1000, shapeRadius = 1f, maxParticles = 5000 };

        [Header("Hearth Scaling")]
        [SerializeField] private float hearthSizeMin = 0.1f;
        [SerializeField] private float hearthSizeMax = 0.5f;

        private List<ParticleSystem> fireSystems = new List<ParticleSystem>();
        private List<ParticleSystem> smokeSystems = new List<ParticleSystem>();
        private List<ParticleSystem> hearthSystems = new List<ParticleSystem>();

        private Light fireLight; 

        private void Awake()
        {
            InitializeReferences();
            ApplyFireSettings();
        }

        private void OnValidate()
        {
            InitializeReferences();
            ApplyFireSettings();
        }

        private void InitializeReferences()
        {
            fireSystems.Clear();
            smokeSystems.Clear();
            hearthSystems.Clear();

            ParticleSystem[] allParticles = GetComponentsInChildren<ParticleSystem>();

            foreach (var ps in allParticles)
            {
                if (ps == null) continue;

                string n = ps.name.ToLower();

                if (n.Contains("smoke"))
                {
                    smokeSystems.Add(ps);
                }
                else if (n.Contains("hearth"))
                {
                    hearthSystems.Add(ps);
                }
                else
                {
                    // Everything else (Volume_Out, etc.) counts as Fire
                    fireSystems.Add(ps);
                }
            }

            fireLight = GetComponentInChildren<Light>();
        }

        private void ApplyFireSettings()
        {
            float t;
            VFXSettings currentFire;
            VFXSettings currentSmoke;
            float currentHearthSize;
            float lightMultiplier = fireIntensity * 2f;

            if (fireIntensity < 1f)
            {
                // Stage 1: Fade from Zero to Min (Intensity 0 -> 1)
                t = fireIntensity; // 0 to 1
                VFXSettings zero = new VFXSettings { maxParticles = 5000 }; // Size/Rate/Radius default to 0
                
                currentFire = VFXSettings.Lerp(zero, fireMin, t);
                currentSmoke = VFXSettings.Lerp(zero, smokeMin, t);
                currentHearthSize = Mathf.Lerp(0f, hearthSizeMin, t);
            }
            else
            {
                // Stage 2: Transition from Min to Max (Intensity 1 -> 10)
                t = Mathf.InverseLerp(1f, 10f, fireIntensity);
                
                currentFire = VFXSettings.Lerp(fireMin, fireMax, t);
                currentSmoke = VFXSettings.Lerp(smokeMin, smokeMax, t);
                currentHearthSize = Mathf.Lerp(hearthSizeMin, hearthSizeMax, t);
            }

            // 1. Update Fire Systems
            foreach (var ps in fireSystems)
            {
                UpdateSystem(ps, currentFire, fireColor);
            }

            // 2. Update Smoke Systems
            foreach (var ps in smokeSystems)
            {
                UpdateSystem(ps, currentSmoke, smokeColor);
            }

            // 3. Update Hearth Systems (Size only)
            foreach (var ps in hearthSystems)
            {
                var main = ps.main;
                main.startSize = currentHearthSize;
                main.startColor = fireColor;
            }

            // 4. Update Light
            if (fireLight != null)
            {
                fireLight.intensity = lightMultiplier; 
                fireLight.color = fireColor;
                fireLight.enabled = fireIntensity > 0.01f; // Turn off light when nearly extinguished
            }
        }

        private void UpdateSystem(ParticleSystem ps, VFXSettings settings, Color col)
        {
            if (ps == null) return;

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var velocity = ps.velocityOverLifetime;

            main.startColor = col;
            main.startLifetime = settings.startLifetime;
            main.startSize = settings.startSize;
            main.maxParticles = settings.maxParticles;

            emission.rateOverTime = settings.emissionRate;
            shape.radius = settings.shapeRadius;

            if (velocity.enabled)
            {
                velocity.xMultiplier = fireWindDirection.x;
                velocity.yMultiplier = fireWindDirection.y;
                velocity.zMultiplier = fireWindDirection.z;
            }
        }

        #region Public API

        public void SetFireColor(Color newColor)
        {
            fireColor = newColor;
            ApplyFireSettings();
        }

        public void SetSmokeColor(Color newColor)
        {
            smokeColor = newColor;
            ApplyFireSettings();
        }

        public void SetFireIntensity(float newIntensity)
        {
            fireIntensity = Mathf.Clamp(newIntensity, 0f, 10f);
            ApplyFireSettings();
        }

        public void SetFireWindDirection(Vector3 newWindDirection)
        {
            fireWindDirection = newWindDirection;
            ApplyFireSettings();
        }

        public Color GetFireColor() => fireColor;
        public Color GetSmokeColor() => smokeColor;
        public float GetFireIntensity() => fireIntensity;
        public Vector3 GetFireWindDirection() => fireWindDirection;

        #endregion
    }
}
