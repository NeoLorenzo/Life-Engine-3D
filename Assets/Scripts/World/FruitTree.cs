using System.Collections.Generic;
using UnityEngine;

namespace LifeEngine.World
{
    public class FruitTree : MonoBehaviour
    {
        [Header("Prefab Settings")]
        [Tooltip("The apple prefab (Must have a Rigidbody to 'drop')")]
        public GameObject applePrefab;

        [Header("Spawning Settings")]
        [Tooltip("Seconds between each potential apple drop")]
        public float spawnInterval = 30f;
        
        [Tooltip("Chance (0 to 1) that an apple will actually spawn when the timer hits")]
        [Range(0f, 1f)]
        public float spawnChance = 0.5f;

        private List<Transform> spawnSlots = new List<Transform>();
        private float timer;

        private void Awake()
        {
            FindSpawnSlots(transform);
            
            if (spawnSlots.Count == 0)
            {
                Debug.LogWarning($"FruitTree on {gameObject.name}: No objects with 'AppleSlot' in name found in children!");
            }

            if (applePrefab != null && applePrefab.GetComponent<Rigidbody>() == null)
            {
                Debug.LogError($"FruitTree on {gameObject.name}: Assigned Apple Prefab is missing a Rigidbody! It won't drop.");
            }
        }

        private void Update()
        {
            if (applePrefab == null || spawnSlots.Count == 0) return;

            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                timer = 0f;
                if (Random.value <= spawnChance)
                {
                    TrySpawnApple();
                }
            }
        }

        private void TrySpawnApple()
        {
            // Pick a random slot and drop!
            Transform randomSlot = spawnSlots[Random.Range(0, spawnSlots.Count)];
            Instantiate(applePrefab, randomSlot.position, randomSlot.rotation);
        }

        private void FindSpawnSlots(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains("AppleSlot"))
                {
                    spawnSlots.Add(child);
                }
                // Recursive search in case slots are deep in the prefab hierarchy
                FindSpawnSlots(child);
            }
        }
    }
}
