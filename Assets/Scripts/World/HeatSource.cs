using UnityEngine;

namespace LifeEngine.World
{
    public class HeatSource : MonoBehaviour
    {
        [Header("Heat Properties")]
        [Tooltip("The temperature bonus in Celsius at the center of the source.")]
        public float strength = 25f;

        [Tooltip("The distance at which the heat bonus drops to zero.")]
        public float radius = 6f;

        [Header("Options")]
        public bool isActive = true;

        // Static registry for high-performance, collider-independent scanning
        public static readonly System.Collections.Generic.List<HeatSource> ActiveSources = new System.Collections.Generic.List<HeatSource>();

        private void OnEnable()
        {
            if (!ActiveSources.Contains(this))
            {
                ActiveSources.Add(this);
            }
        }

        private void OnDisable()
        {
            if (ActiveSources.Contains(this))
            {
                ActiveSources.Remove(this);
            }
        }

        /// <summary>
        /// Calculates the heat contribution at a specific world position.
        /// Uses linear falloff.
        /// </summary>
        public float GetHeatBonusAt(Vector3 observerPosition)
        {
            if (!isActive) return 0f;

            float distance = Vector3.Distance(transform.position, observerPosition);
            if (distance >= radius) return 0f;

            // Linear falloff: 1.0 at distance 0, 0.0 at distance radius
            float t = 1f - (distance / radius);
            return strength * t;
        }

        private void OnDrawGizmosSelected()
        {
            if (!isActive) return;
            
            // Draw the heat radius in the editor
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, radius);
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.05f);
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
