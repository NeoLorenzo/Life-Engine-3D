using System.Collections.Generic;
using UnityEngine;

namespace LifeEngine.SimulatedHumans
{
    public class HumanMemory : MonoBehaviour
    {
        public float defaultThreatMemoryDuration = 4.0f;

        private struct ThreatMemoryData
        {
            public Vector3 position;
            public float expiresAt;

            public ThreatMemoryData(Vector3 position, float expiresAt)
            {
                this.position = position;
                this.expiresAt = expiresAt;
            }
        }

        private readonly List<ThreatMemoryData> recentThreatPositions = new List<ThreatMemoryData>(24);
        private readonly List<Vector3> activeThreatPositions = new List<Vector3>(32);
        
        private Transform primaryThreat;
        private Vector3 lastKnownThreatPosition;

        private void Update()
        {
            PruneExpiredThreatMemory();
        }

        public void AddOrRefreshThreat(Vector3 threatPosition)
        {
            float expiryTime = Time.time + Mathf.Max(0.01f, defaultThreatMemoryDuration);
            const float mergeDistanceSqr = 1.0f;

            for (int i = 0; i < recentThreatPositions.Count; i++)
            {
                ThreatMemoryData memory = recentThreatPositions[i];
                if ((memory.position - threatPosition).sqrMagnitude <= mergeDistanceSqr)
                {
                    memory.position = threatPosition;
                    memory.expiresAt = expiryTime;
                    recentThreatPositions[i] = memory;
                    return;
                }
            }

            recentThreatPositions.Add(new ThreatMemoryData(threatPosition, expiryTime));
        }

        public void SetPrimaryThreat(Transform threatTransform)
        {
            primaryThreat = threatTransform;
            if (threatTransform != null)
            {
                lastKnownThreatPosition = threatTransform.position;
                AddOrRefreshThreat(lastKnownThreatPosition);
            }
        }

        public Transform GetPrimaryThreat() => primaryThreat;
        public Vector3 GetLastKnownThreatPosition() => lastKnownThreatPosition;

        /// <summary>
        /// Retrieves an aggregated list of all recently seen threat positions, merging duplicates.
        /// Useful for fleeing from multiple threats or avoiding "hot" areas.
        /// </summary>
        public List<Vector3> GetActiveThreatPositions(List<Vector3> currentlyVisibleThreats)
        {
            activeThreatPositions.Clear();

            if (currentlyVisibleThreats != null)
            {
                for (int i = 0; i < currentlyVisibleThreats.Count; i++)
                {
                    AddUniqueThreatPosition(activeThreatPositions, currentlyVisibleThreats[i]);
                }
            }

            float now = Time.time;
            for (int i = 0; i < recentThreatPositions.Count; i++)
            {
                ThreatMemoryData memory = recentThreatPositions[i];
                if (memory.expiresAt > now)
                {
                    AddUniqueThreatPosition(activeThreatPositions, memory.position);
                }
            }

            if (activeThreatPositions.Count == 0 && primaryThreat == null && lastKnownThreatPosition != Vector3.zero)
            {
                // We lost the threat but still remember its last known spot momentarily.
                activeThreatPositions.Add(lastKnownThreatPosition);
            }

            return activeThreatPositions;
        }

        private void PruneExpiredThreatMemory()
        {
            float now = Time.time;
            for (int i = recentThreatPositions.Count - 1; i >= 0; i--)
            {
                if (recentThreatPositions[i].expiresAt <= now)
                {
                    recentThreatPositions.RemoveAt(i);
                }
            }
        }

        private void AddUniqueThreatPosition(List<Vector3> positions, Vector3 candidate)
        {
            const float duplicateDistanceSqr = 0.25f;
            for (int i = 0; i < positions.Count; i++)
            {
                if ((positions[i] - candidate).sqrMagnitude <= duplicateDistanceSqr)
                {
                    return;
                }
            }
            positions.Add(candidate);
        }
    }
}
