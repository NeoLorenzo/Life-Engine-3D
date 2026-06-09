using UnityEngine;

namespace LifeEngine.World
{
    public class ResourceItem : MonoBehaviour
    {
        public ResourceType type;
        public int amount = 1;

        private void Update()
        {
            // Safety check for objects falling through the floor
            if (transform.position.y < -10.0f)
            {
                // Teleport back to surface level
                transform.position = new Vector3(transform.position.x, 1.0f, transform.position.z);
                
                // Kill momentum to prevent immediate re-clipping
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Debug.Log($"[ResourceItem] Recovered {gameObject.name} (type: {type}) from depth {transform.position.y}");
            }
        }
    }
}
