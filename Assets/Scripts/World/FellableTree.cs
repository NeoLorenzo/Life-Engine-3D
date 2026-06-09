using UnityEngine;
using System;

namespace LifeEngine.World
{
    [Serializable]
    public struct ResourceSpawnGroup
    {
        public ResourceType type;
        public Transform[] spawnPoints;
    }

    public class FellableTree : MonoBehaviour
    {
        [Header("Resource Registry")]
        public ResourceRegistry registry;

        [Header("Harvest Settings")]
        public bool requiresTool = true;
        public float harvestDurationMinutes = 1.0f;

        [Header("Transformation Prefabs")]
        public GameObject stumpPrefab;
        
        [Header("Spawning Channels (Child objects)")]
        public Transform stumpSpawnPoint;
        public ResourceSpawnGroup[] resourceDrops;

        /// <summary>
        /// Triggers the physical transformation of the tree into a stump and resources.
        /// </summary>
        public void Fell()
        {
            // 1. Instantiate the stump at the marked slot (or base if null)
            if (stumpPrefab != null)
            {
                Vector3 pos = stumpSpawnPoint != null ? stumpSpawnPoint.position : transform.position;
                Quaternion rot = stumpSpawnPoint != null ? stumpSpawnPoint.rotation : transform.rotation;
                Instantiate(stumpPrefab, pos, rot);
            }

            // 2. Spawn Resources from Registry
            if (registry != null && resourceDrops != null)
            {
                foreach (var group in resourceDrops)
                {
                    GameObject prefab = registry.GetWorldPrefab(group.type);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[FellableTree] No world prefab found in registry for {group.type}");
                        continue;
                    }

                    foreach (Transform slot in group.spawnPoints)
                    {
                        if (slot != null) Instantiate(prefab, slot.position, slot.rotation);
                    }
                }
            }
            else if (registry == null)
            {
                Debug.LogError($"[FellableTree] Resource Registry is not assigned on {gameObject.name}!");
            }

            // 3. Cleanup the living tree
            Destroy(gameObject);
        }

        /// <summary>
        /// Checks if this tree/bush yields a specific resource type.
        /// Used by the AI to find appropriate harvesting targets.
        /// </summary>
        public bool DropsResource(ResourceType type)
        {
            if (resourceDrops == null) return false;
            foreach (var group in resourceDrops)
            {
                if (group.type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// Automatically finds and assigns spawn points based on naming conventions.
        /// Trigger this from the Inspector's component context menu (three dots).
        /// </summary>
        [ContextMenu("Auto-Configure Spawn Points")]
        public void AutoConfigureSpawnPoints()
        {
            var allTransforms = GetComponentsInChildren<Transform>();
            var groups = new System.Collections.Generic.Dictionary<ResourceType, System.Collections.Generic.List<Transform>>();

            foreach (var t in allTransforms)
            {
                if (t == transform) continue;

                string n = t.name;

                // 1. Stump Check
                if (n.Contains("StumpSpawn"))
                {
                    stumpSpawnPoint = t;
                    continue;
                }

                // 2. Resource Mapping
                ResourceType? detectedType = null;

                if (n.StartsWith("Log_1_Spawn")) detectedType = ResourceType.Log_1;
                else if (n.StartsWith("Log_2_Spawn")) detectedType = ResourceType.Log_2;
                else if (n.StartsWith("Log_3_Spawn")) detectedType = ResourceType.Log_3;
                else if (n.StartsWith("Log_4_Spawn")) detectedType = ResourceType.Log_4;
                else if (n.StartsWith("Stick_1_Spawn")) detectedType = ResourceType.Stick_1;
                else if (n.StartsWith("Stick_2_Spawn")) detectedType = ResourceType.Stick_2;
                else if (n.StartsWith("Stick_3_Spawn")) detectedType = ResourceType.Stick_3;
                else if (n.StartsWith("Stick_4_Spawn")) detectedType = ResourceType.Stick_4;

                if (detectedType.HasValue)
                {
                    if (!groups.ContainsKey(detectedType.Value)) groups[detectedType.Value] = new System.Collections.Generic.List<Transform>();
                    groups[detectedType.Value].Add(t);
                }
            }

            // Convert dictionary to array
            var newList = new System.Collections.Generic.List<ResourceSpawnGroup>();
            foreach (var kvp in groups)
            {
                newList.Add(new ResourceSpawnGroup
                {
                    type = kvp.Key,
                    spawnPoints = kvp.Value.ToArray()
                });
            }

            resourceDrops = newList.ToArray();
            Debug.Log($"[FellableTree] Automatically configured {resourceDrops.Length} resource groups on {gameObject.name}.");
        }
    }
}
