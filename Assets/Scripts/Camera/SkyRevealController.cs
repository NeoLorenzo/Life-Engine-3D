using UnityEngine;

namespace LifeEngine.Cameras
{
    /// <summary>
    /// Coordinates GPU data upload for active sky reveal targets and manages
    /// target indicator ring visibility based on the active camera mode.
    /// </summary>
    public class SkyRevealController : MonoBehaviour
    {
        private static SkyRevealController instance;
        public static SkyRevealController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = UnityEngine.Object.FindFirstObjectByType<SkyRevealController>();
                }
                return instance;
            }
            private set => instance = value;
        }

        public const int MaxRevealTargets = 64;

        public static bool IsSkyModeActive { get; private set; } = false;

        private static readonly int SkyRevealEnabledId = Shader.PropertyToID("_SkyRevealEnabled");
        private static readonly int SkyRevealCountId = Shader.PropertyToID("_SkyRevealCount");
        private static readonly int SkyRevealTargetsId = Shader.PropertyToID("_SkyRevealTargets");

        private Vector4[] targetBuffer;

        private Vector4[] GetTargetBuffer()
        {
            if (targetBuffer == null || targetBuffer.Length != MaxRevealTargets)
            {
                targetBuffer = new Vector4[MaxRevealTargets];
            }
            return targetBuffer;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize global shader state
            Shader.SetGlobalFloat(SkyRevealEnabledId, 0f);
            Shader.SetGlobalInt(SkyRevealCountId, 0);
            Shader.SetGlobalVectorArray(SkyRevealTargetsId, GetTargetBuffer());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Shader.SetGlobalFloat(SkyRevealEnabledId, 0f);
                Shader.SetGlobalInt(SkyRevealCountId, 0);
                Instance = null;
            }
        }

        private Vector3 GetSkyCameraPosition()
        {
            if (SimulationCameraController.Instance != null && SimulationCameraController.Instance.skyCameraAnchor != null)
            {
                return SimulationCameraController.Instance.skyCameraAnchor.position;
            }
            if (Camera.main != null)
            {
                return Camera.main.transform.position;
            }
            return transform.position;
        }

        /// <summary>
        /// Toggles sky tree cutouts and reveal rings across all registered targets.
        /// </summary>
        public void SetSkyRevealEnabled(bool enabled)
        {
            IsSkyModeActive = enabled;
            Shader.SetGlobalFloat("_SkyRevealEnabled", enabled ? 1.0f : 0.0f);

            if (!enabled)
            {
                Shader.SetGlobalInt("_SkyRevealCount", 0);
            }
            else
            {
                Vector3 skyCamPos = GetSkyCameraPosition();
                Shader.SetGlobalVector("_SkyRevealCameraPosition", new Vector4(skyCamPos.x, skyCamPos.y, skyCamPos.z, 1.0f));
            }

            var targets = SkyRevealTarget.ActiveTargets;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].SetRingVisibility(enabled);
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsSkyModeActive) return;

            Vector3 skyCamPos = GetSkyCameraPosition();
            Shader.SetGlobalVector("_SkyRevealCameraPosition", new Vector4(skyCamPos.x, skyCamPos.y, skyCamPos.z, 1.0f));

            var targets = SkyRevealTarget.ActiveTargets;
            if (targets.Count == 0)
            {
                var found = UnityEngine.Object.FindObjectsByType<SkyRevealTarget>(FindObjectsSortMode.None);
                if (found != null)
                {
                    for (int j = 0; j < found.Length; j++)
                    {
                        if (found[j] != null && found[j].enabled && !targets.Contains(found[j]))
                        {
                            targets.Add(found[j]);
                        }
                    }
                }
            }

            int count = Mathf.Min(targets.Count, MaxRevealTargets);
            Vector4[] buf = GetTargetBuffer();

            for (int i = 0; i < count; i++)
            {
                SkyRevealTarget target = targets[i];
                if (target != null && target.gameObject != null)
                {
                    Vector3 pos = target.transform.position;
                    buf[i] = new Vector4(pos.x, pos.y, pos.z, target.revealRadius);
                }
                else
                {
                    buf[i] = Vector4.zero;
                }
            }

            Shader.SetGlobalInt("_SkyRevealCount", count);
            Shader.SetGlobalVectorArray("_SkyRevealTargets", buf);
        }
    }
}
