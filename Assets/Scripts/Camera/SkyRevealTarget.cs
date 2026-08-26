using System.Collections.Generic;
using UnityEngine;

namespace LifeEngine.Cameras
{
    /// <summary>
    /// Marks an agent or constructed world object to generate a cylindrical tree cutout
    /// and an indicator ring while in Sky camera mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkyRevealTarget : MonoBehaviour
    {
        private static readonly List<SkyRevealTarget> activeTargetsList = new List<SkyRevealTarget>();

        /// <summary>
        /// Global list of currently active sky reveal targets in deterministic registration order.
        /// </summary>
        public static List<SkyRevealTarget> ActiveTargets
        {
            get
            {
                for (int i = activeTargetsList.Count - 1; i >= 0; i--)
                {
                    if (activeTargetsList[i] == null)
                    {
                        activeTargetsList.RemoveAt(i);
                    }
                }
                return activeTargetsList;
            }
        }

        [Header("Tree Cutout")]
        [Tooltip("Horizontal radius in meters around this target where tree geometry will be clipped.")]
        [Min(0.1f)]
        public float revealRadius = 2.0f;

        [Header("Scene View Gizmo")]
        [Tooltip("Whether to display an indicator gizmo disc in the Scene View.")]
        public bool drawGizmo = true;

        [Tooltip("Color of the Scene View indicator gizmo disc.")]
        public Color gizmoColor = new Color(0.2f, 0.9f, 1.0f, 0.75f);

        [Tooltip("Vertical height offset relative to target base for Scene View gizmo drawing.")]
        public float verticalOffset = 0.05f;

        // Backwards compatibility properties for tooling/editor scripts
        public bool drawRing { get => drawGizmo; set => drawGizmo = value; }
        public float ringRadius { get => revealRadius; set => revealRadius = value; }
        public Color ringColor { get => gizmoColor; set => gizmoColor = value; }

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }
        }

        private void Start()
        {
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
        }

        /// <summary>
        /// Compatibility hook for sky reveal visibility toggle.
        /// </summary>
        public void SetRingVisibility(bool isVisible)
        {
            // Indicator rings are only rendered in Scene View via Gizmos.
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmo) return;

            Vector3 center = transform.position + Vector3.up * verticalOffset;
            float radius = revealRadius > 0.01f ? revealRadius : 2.0f;

            UnityEditor.Handles.color = gizmoColor;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);

            // Draw line toward Sky Camera in Scene view when sky camera is active
            if (SkyRevealController.IsSkyModeActive && SimulationCameraController.Instance != null && SimulationCameraController.Instance.skyCameraAnchor != null)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.35f);
                Gizmos.DrawLine(center, SimulationCameraController.Instance.skyCameraAnchor.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Vector3 center = transform.position + Vector3.up * verticalOffset;
            float radius = revealRadius > 0.01f ? revealRadius : 2.0f;

            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
        }
#endif
    }
}
