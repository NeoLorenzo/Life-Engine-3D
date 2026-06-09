using System.Collections.Generic;
using UnityEngine;

namespace LifeEngine.SimulatedHumans
{
    public class HumanPerception : MonoBehaviour
    {
        [Header("Perception Settings")]
        public float dangerDetectionRadius = 15f;
        public float viewAngle = 200f;
        public float hearingRadius = 2.0f;
        public LayerMask obstacleLayer;
        public LayerMask threatLayer;
        public LayerMask foodLayer;
        public LayerMask resourceLayer;
        public LayerMask treeLayer;
        public LayerMask heatSourceLayer;

        [Header("Scan Timings")]
        public float dangerScanInterval = 0.2f;

        private float nextDangerScanTime;
        private float nextFoodScanTime;
        private Collider[] overlapResults = new Collider[512]; // Increased for packed forests
        
        // This list will be heavily utilized by the HumanBrain to pass into HumanMemory
        [HideInInspector] 
        public List<Vector3> currentlyVisibleThreatPositions = new List<Vector3>();

        private Transform primaryThreat;
        private Transform primaryFood;

        private void Awake()
        {
            if (obstacleLayer == 0) obstacleLayer = 1 << 6; // Safety default
            if (treeLayer == 0) treeLayer = 1 << 9;       // Safety default
        }

        /// <summary>
        /// Scans for threats at set intervals. Returns true if a threat is currently visible.
        /// </summary>
        public bool PerformDangerScan(out Transform closestThreat)
        {
            closestThreat = primaryThreat;

            if (Time.time < nextDangerScanTime)
            {
                return currentlyVisibleThreatPositions.Count > 0;
            }

            nextDangerScanTime = Time.time + Mathf.Max(0.01f, dangerScanInterval);
            currentlyVisibleThreatPositions.Clear();
            primaryThreat = null;
            closestThreat = null;

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, dangerDetectionRadius, overlapResults, threatLayer);
            
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Transform target = overlapResults[i].transform;
                if (target == this.transform) continue;

                if (!CanSeeTarget(target)) continue;

                Vector3 targetPos = target.position;
                currentlyVisibleThreatPositions.Add(targetPos);

                float distSqr = (targetPos - transform.position).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    primaryThreat = target;
                }
            }

            closestThreat = primaryThreat;
            return currentlyVisibleThreatPositions.Count > 0;
        }

        public bool PerformResourceScan(World.ResourceType type, out Transform closestResource)
        {
            closestResource = null;
            
            // 1. Primary Scan: Items on the ground
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, dangerDetectionRadius, overlapResults, resourceLayer);
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var resourceItem = overlapResults[i].GetComponent<World.ResourceItem>();
                if (resourceItem == null || resourceItem.type != type) continue;

                Transform target = overlapResults[i].transform;
                if (!CanSeeTarget(target)) continue;

                float distSqr = (target.position - transform.position).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestResource = target;
                }
            }

            // If we found a ground item, return it immediately to avoid breaking more bushes than needed.
            if (closestResource != null) return true;

            // 2. Secondary Scan: Source objects (Bushes/Trees)
            // Only happens if no items are already lying on the ground.
            hitCount = Physics.OverlapSphereNonAlloc(transform.position, dangerDetectionRadius, overlapResults, treeLayer);
            
            for (int i = 0; i < hitCount; i++)
            {
                var tree = overlapResults[i].GetComponent<World.FellableTree>();
                if (tree == null || !tree.DropsResource(type)) continue;

                // 2a. Tool Requirement Check
                if (tree.requiresTool)
                {
                    var brain = GetComponent<SimulatedHumans.HumanBrain>();
                    if (brain != null && !brain.HasTool("Basic_Axe")) continue;
                }

                Transform target = overlapResults[i].transform;
                if (!CanSeeTarget(target)) continue;

                float distSqr = (target.position - transform.position).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestResource = target;
                }
            }

            return closestResource != null;
        }

        public bool PerformFoodScan(out Transform closestFood)
        {
            closestFood = primaryFood;

            if (Time.time < nextFoodScanTime)
            {
                return primaryFood != null;
            }

            nextFoodScanTime = Time.time + Mathf.Max(0.01f, dangerScanInterval); // Same interval frequency
            primaryFood = null;
            closestFood = null;

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, dangerDetectionRadius, overlapResults, foodLayer);
            
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Transform target = overlapResults[i].transform;
                if (target == this.transform) continue;

                if (!CanSeeTarget(target)) continue;

                float distSqr = (target.position - transform.position).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    primaryFood = target;
                }
            }

            closestFood = primaryFood;
            return primaryFood != null;
        }

        public bool PerformHeatSourceScan(out List<World.HeatSource> sources)
        {
            sources = new List<World.HeatSource>();

            // Use the static registry for more reliable detection (avoids collider/layer issues)
            foreach (var source in World.HeatSource.ActiveSources)
            {
                if (source == null || !source.isActive) continue;

                float dist = Vector3.Distance(transform.position, source.transform.position);
                if (dist <= dangerDetectionRadius) 
                {
                    sources.Add(source);
                }
            }

            return sources.Count > 0;
        }

        private bool CanSeeTarget(Transform target)
        {
            Vector3 dirToTarget = target.position - transform.position;
            float dist = dirToTarget.magnitude;

            if (dist > dangerDetectionRadius) return false;

            // 1. Hearing Radius (360 degrees)
            if (dist < hearingRadius) return true;

            // 2. FOV Check
            float angle = Vector3.Angle(transform.forward, dirToTarget);
            if (angle < viewAngle / 2f)
            {
                // 3. LOS Raycast (Eye level to target)
                Vector3 eyePos = transform.position + Vector3.up * 1.5f;
                
                // Dynamic target height based on what we are looking at
                float heightOffset = 0.8f; // Default (Human sized)
                
                if (target.GetComponent<World.ResourceItem>() != null) 
                    heightOffset = 0.15f; // Target the base of small items on ground
                else if (target.GetComponent<World.FellableTree>() != null)
                    heightOffset = 0.6f; // Target trunk of bushes/trees

                Vector3 targetPos = target.position + Vector3.up * heightOffset; 
                Vector3 rayDir = targetPos - eyePos;

                if (!Physics.Raycast(eyePos, rayDir, dist, obstacleLayer))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = (primaryThreat != null) ? Color.red : Color.green;
            Vector3 leftRay = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;
            Gizmos.DrawRay(transform.position + Vector3.up, leftRay * dangerDetectionRadius);
            Gizmos.DrawRay(transform.position + Vector3.up, rightRay * dangerDetectionRadius);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, hearingRadius);
        }
    }
}
