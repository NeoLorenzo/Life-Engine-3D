using System;
using System.Collections.Generic;
using UnityEngine;
using LifeEngine.World;

namespace LifeEngine.Crafting
{
    [Serializable]
    public class ResourceRequirement
    {
        public string label;
        public ResourceType type;
        public int amountRequired;
        public int amountCurrent;

        [Header("Visual Toggling")]
        [Tooltip("Components (blueprint version) to deactivate as progress is made")]
        public GameObject[] blueprintVisuals;
        [Tooltip("Components (normal version) to activate as progress is made")]
        public GameObject[] normalVisuals;

        public bool IsSatisfied => amountCurrent >= amountRequired;

        public void RefreshVisuals()
        {
            // We loop through the visuals and toggle them one-by-one based on index.
            // This allows the player to see sticks/stones appearing as they are added.
            
            int visualCount = Mathf.Max(
                blueprintVisuals != null ? blueprintVisuals.Length : 0,
                normalVisuals != null ? normalVisuals.Length : 0
            );

            for (int i = 0; i < visualCount; i++)
            {
                bool isPlaced = i < amountCurrent;

                if (blueprintVisuals != null && i < blueprintVisuals.Length && blueprintVisuals[i] != null)
                {
                    blueprintVisuals[i].SetActive(!isPlaced);
                }

                if (normalVisuals != null && i < normalVisuals.Length && normalVisuals[i] != null)
                {
                    normalVisuals[i].SetActive(isPlaced);
                }
            }
        }
    }

    public class CraftingBlueprint : MonoBehaviour
    {
        public List<ResourceRequirement> requirements = new List<ResourceRequirement>();

        private void Start()
        {
            // Ensure visual state matches initial counts
            foreach (var req in requirements)
            {
                req.RefreshVisuals();
            }
        }

        public bool GetNextMissingResource(out ResourceType nextType)
        {
            nextType = ResourceType.Log_1;
            foreach (var req in requirements)
            {
                if (!req.IsSatisfied)
                {
                    nextType = req.type;
                    return true;
                }
            }
            return false;
        }

        [Header("Completion")]
        public GameObject finalPrefab;

        private bool isCompleted = false;

        public int AddResource(ResourceType type, int amount)
        {
            if (amount <= 0 || isCompleted) return 0;

            foreach (var req in requirements)
            {
                if (req.type == type && !req.IsSatisfied)
                {
                    int needed = req.amountRequired - req.amountCurrent;
                    int accepted = Mathf.Min(needed, amount);
                    
                    req.amountCurrent += accepted;
                    req.RefreshVisuals();
                    
                    if (IsComplete())
                    {
                        Complete();
                    }
                    return accepted;
                }
            }

            return 0;
        }

        private void Complete()
        {
            if (isCompleted) return;
            isCompleted = true;

            if (finalPrefab != null)
            {
                Instantiate(finalPrefab, transform.position, transform.rotation);
            }
            Destroy(gameObject);
        }

        public bool IsComplete()
        {
            foreach (var req in requirements)
            {
                if (!req.IsSatisfied) return false;
            }
            return true;
        }
    }
}
