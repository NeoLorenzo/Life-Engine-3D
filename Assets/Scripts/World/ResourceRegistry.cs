using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeEngine.World
{
    [Serializable]
    public struct ResourceMapping
    {
        public ResourceType type;
        public GameObject visualPrefab;
        public GameObject worldPrefab;
    }

    [Serializable]
    public struct ResourceOutput
    {
        public ResourceType type;
        public int count;
    }

    [Serializable]
    public class Recipe
    {
        public string name;
        public ResourceType input;
        public int inputQuantity = 1;
        public List<ResourceOutput> outputs = new List<ResourceOutput>();
        public float duration = 1.0f;
    }

    [Serializable]
    public struct ToolMapping
    {
        public string toolName;
        public GameObject visualPrefab;
    }

    [CreateAssetMenu(fileName = "ResourceRegistry", menuName = "World/Resource Registry")]
    public class ResourceRegistry : ScriptableObject
    {
        public List<ResourceMapping> resources = new List<ResourceMapping>();
        public List<ToolMapping> tools = new List<ToolMapping>();
        public List<Recipe> recipes = new List<Recipe>();

        public List<Recipe> GetRecipesForInput(ResourceType type)
        {
            return recipes.FindAll(r => r.input == type);
        }

        public GameObject GetResourcePrefab(ResourceType type)
        {
            var mapping = resources.Find(m => m.type == type);
            return mapping.visualPrefab;
        }

        public GameObject GetWorldPrefab(ResourceType type)
        {
            var mapping = resources.Find(m => m.type == type);
            return mapping.worldPrefab;
        }

        public GameObject GetToolPrefab(string toolName)
        {
            var mapping = tools.Find(m => m.toolName == toolName);
            return mapping.visualPrefab;
        }
    }
}
