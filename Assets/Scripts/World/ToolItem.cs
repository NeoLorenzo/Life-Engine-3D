using UnityEngine;

namespace LifeEngine.World
{
    /// <summary>
    /// Component for finished tools that can be picked up and added to a human inventory.
    /// Unlike ResourceItems, these represent permanent tools (Axes, Pickaxes, etc).
    /// </summary>
    public class ToolItem : MonoBehaviour
    {
        [Tooltip("The unique name/tag of this tool (e.g., Axe)")]
        public string toolName = "Axe";
    }
}
