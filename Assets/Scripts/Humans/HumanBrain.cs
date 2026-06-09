using System.Collections.Generic;
using UnityEngine;
using LifeEngine.AI;
using LifeEngine.SimulatedHumans.Behaviors;
using System;

namespace LifeEngine.SimulatedHumans
{
    [Serializable]
    public class ResourceStack
    {
        public World.ResourceType type;
        public int amount;
        public ResourceStack(World.ResourceType type, int amount) { this.type = type; this.amount = amount; }
    }

    [RequireComponent(typeof(HumanLocomotion))]
    [RequireComponent(typeof(HumanPerception))]
    [RequireComponent(typeof(HumanMemory))]
    public class HumanBrain : MonoBehaviour
    {
        [Header("Sleep & Adenosine")]
        public float adenosineConcentration = 10f;
        [Tooltip("nm of adenosine built up per in-game hour while awake")]
        public float adenosineBuildupPerHour = 5.625f; 
        [Tooltip("nm of adenosine cleared per in-game hour while sleeping")]
        public float adenosineClearancePerHour = 11.25f;

        [Header("Hunger & Ghrelin")]
        public float ghrelinConcentration = 500f;
        [Tooltip("pg/mL of ghrelin built up per in-game hour while awake")]
        public float ghrelinBuildupPerHour = 140f;
        [Tooltip("Threshold at which the agent seeks food")]
        public float ghrelinHungerThreshold = 1200f;
        public bool isSleeping = false;

        [Header("Temperature & Comfort")]
        public float comfortRangeMin = 18f;
        public float comfortRangeMax = 26f;
        public float perceivedTemperature = 22f;
        public ThermalStatus currentThermalStatus = ThermalStatus.Comfortable;
        
        public enum ThermalStatus { Cold, Comfortable, Hot }

        [Header("Shade Detection")]
        public bool isInShade;
        private LayerMask shadeMask;

        [Header("Test Mode")]
        [Tooltip("Enable this to force the agent to find and fell the nearest tree.")]
        public bool startFellingTest = false;
        [Tooltip("Enable this to force the agent into a crafting state.")]
        public bool startCraftingTest = false;

        [Header("Selection")]
        [Tooltip("Attach a child GameObject (like a ring or outline) here. It will be enabled when selected.")]
        public GameObject selectionVisual;

        [Header("Crafting Prefabs")]
        public GameObject campfireBlueprintPrefab;
        public GameObject axeBlueprintPrefab;

        [Header("AI State")]
        public string currentStateDisplay;

        [Header("Inventory")]
        public List<ResourceStack> inventory = new List<ResourceStack>();
        public List<string> toolInventory = new List<string>();

        [Header("Inventory Visuals")]
        public Transform toolSlot;
        public Transform resourceSlot;
        public World.ResourceRegistry registry;

        private GameObject currentResourceVisual;
        private GameObject currentToolVisual;

        public bool HasCarriedResource() => inventory.Count > 0;
        public bool HasTool(string toolName) => toolInventory.Contains(toolName);

        public void AddResource(World.ResourceType type, int amount)
        {
            var stack = inventory.Find(s => s.type == type);
            if (stack != null)
            {
                stack.amount += amount;
            }
            else
            {
                inventory.Add(new ResourceStack(type, amount));
            }
            UpdateResourceVisual();
        }

        public void RemoveResource(World.ResourceType type, int amount)
        {
            var stack = inventory.Find(s => s.type == type);
            if (stack != null)
            {
                stack.amount -= amount;
                if (stack.amount <= 0) inventory.Remove(stack);
            }
            UpdateResourceVisual();
        }

        public int GetResourceCount(World.ResourceType type)
        {
            var stack = inventory.Find(s => s.type == type);
            return stack != null ? stack.amount : 0;
        }

        public void UpdateResourceVisual()
        {
            if (currentResourceVisual != null) Destroy(currentResourceVisual);

            if (HasCarriedResource() && resourceSlot != null && registry != null)
            {
                // Visualize the last added item (the top of our "hands")
                var lastStack = inventory[inventory.Count - 1];
                GameObject prefab = registry.GetResourcePrefab(lastStack.type);
                if (prefab != null)
                {
                    currentResourceVisual = Instantiate(prefab, resourceSlot.position, resourceSlot.rotation, resourceSlot);
                    PrepareVisual(currentResourceVisual);
                }
            }
        }

        public void UpdateToolVisual()
        {
            // For now, specifically handle the Basic_Axe if owned. 
            if (HasTool("Basic_Axe") && currentToolVisual == null && toolSlot != null && registry != null)
            {
                GameObject prefab = registry.GetToolPrefab("Basic_Axe");
                if (prefab != null)
                {
                    currentToolVisual = Instantiate(prefab, toolSlot.position, toolSlot.rotation, toolSlot);
                    PrepareVisual(currentToolVisual);
                }
            }
        }

        private void PrepareVisual(GameObject visual)
        {
            if (visual == null) return;

            // Disable all colliders to prevent physical interference
            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Make rigidbodies kinematic
            foreach (var rb in visual.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Optional: Set to a specific layer (e.g., Ignore Raycast) if needed
            // visual.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        private HumanContext aiContext;
        private Selector rootNode;
        public Selector RootNode => rootNode;

        public HumanLocomotion Locomotion { get; private set; }
        public HumanPerception Perception { get; private set; }
        public HumanMemory Memory { get; private set; }

        public void SetSelected(bool selected)
        {
            if (selectionVisual != null)
            {
                selectionVisual.SetActive(selected);
            }
        }

        private void Awake()
        {
            Locomotion = GetComponent<HumanLocomotion>();
            Perception = GetComponent<HumanPerception>();
            Memory = GetComponent<HumanMemory>();

            aiContext = new HumanContext
            {
                Brain = this,
                Locomotion = this.Locomotion,
                Perception = this.Perception,
                Memory = this.Memory,
                OutsideRoomComfortDuration = 10f,
                OutsideRoomComfortTimer = 10f
            };
        }

        private void Start()
        {
            BuildBehaviorTree();
        }

        private void Update()
        {
            if (!isSleeping)
            {
                if (World.TimeManager.Instance != null && World.TimeManager.Instance.realSecondsPerGameMinute > 0)
                {
                    float gameHours = (Time.deltaTime / World.TimeManager.Instance.realSecondsPerGameMinute) / 60f;
                    adenosineConcentration += adenosineBuildupPerHour * gameHours;
                    ghrelinConcentration += ghrelinBuildupPerHour * gameHours;
                }
            }

            UpdateThermalState();

            if (rootNode != null)
            {
                rootNode.ResetState();
                rootNode.Evaluate();
                currentStateDisplay = GetBehaviorTreeStatus();
            }
        }


        private void UpdateThermalState()
        {
            if (World.EnvironmentManager.Instance != null)
            {
                // Initialize shade mask once (0:Default, 6:Walls, 9:Trees)
                if (shadeMask == 0) shadeMask = (1 << 0) | (1 << 6) | (1 << 9);

                // 1. Start with base world temperature
                float targetTemp = World.EnvironmentManager.Instance.BaseTemperature;

                // 2. Shade Bonus (-10 degrees)
                isInShade = false;
                if (World.DayNightCycle.Instance != null && World.DayNightCycle.Instance.IsDaylight)
                {
                    Vector3 sunDir = World.DayNightCycle.Instance.SunDirection;
                    
                    // 5-point check for "Full Coverage" (Silhouette)
                    Vector3[] checkOffsets = new Vector3[]
                    {
                        Vector3.up * 1.8f,                                     // Head
                        Vector3.up * 1.0f,                                     // Center
                        Vector3.up * 1.5f + transform.right * 0.4f,            // Right Shoulder
                        Vector3.up * 1.5f - transform.right * 0.4f,            // Left Shoulder
                        Vector3.up * 0.2f                                      // Feet
                    };

                    int pointsInShade = 0;
                    foreach (var offset in checkOffsets)
                    {
                        RaycastHit[] hits = Physics.RaycastAll(transform.position + offset, -sunDir, 30f, shadeMask);
                        foreach (var hit in hits)
                        {
                            // Apply consistent filtering (Layer 6 Walls or others > 2m tall)
                            if (hit.collider.gameObject.layer == 6 || hit.collider.bounds.size.y >= 2.0f)
                            {
                                pointsInShade++;
                                break;
                            }
                        }
                    }

                    // Only considered "In Shade" if fully covered (all 5 points)
                    if (pointsInShade >= 5)
                    {
                        isInShade = true;
                        targetTemp -= 10f;
                    }
                }

                // 3. Add local heat source bonuses (distance-based)
                if (Perception != null && Perception.PerformHeatSourceScan(out List<World.HeatSource> sources))
                {
                    foreach (var source in sources)
                    {
                        targetTemp += source.GetHeatBonusAt(transform.position);
                    }
                }

                // Interpolate perceived temperature so it doesn't jump instantly
                perceivedTemperature = Mathf.MoveTowards(perceivedTemperature, targetTemp, Time.deltaTime * 2f);

                // 4. Update Status with Hysteresis (Prevents state ping-pong)
                float buffer = 2.0f;
                if (currentThermalStatus == ThermalStatus.Hot)
                {
                    if (perceivedTemperature <= comfortRangeMax - buffer)
                        currentThermalStatus = ThermalStatus.Comfortable;
                }
                else if (currentThermalStatus == ThermalStatus.Cold)
                {
                    if (perceivedTemperature >= comfortRangeMin + buffer)
                        currentThermalStatus = ThermalStatus.Comfortable;
                }
                else
                {
                    if (perceivedTemperature > comfortRangeMax)
                        currentThermalStatus = ThermalStatus.Hot;
                    else if (perceivedTemperature < comfortRangeMin)
                        currentThermalStatus = ThermalStatus.Cold;
                }
            }
        }

        public void FallAsleep()
        {
            isSleeping = true;
            if (Locomotion != null)
            {
                Locomotion.Stop();
                Locomotion.enabled = false;
            }

            UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; 
            }
            
            // Adjust position so they don't float (capsule rotates around center)
            transform.position -= new Vector3(0f, 0.5f, 0f);
            
            // Rotate -90 on X, but preserve Yaw
            transform.localRotation = Quaternion.Euler(-90f, transform.localEulerAngles.y, 0f);
        }

        public void WakeUp()
        {
            isSleeping = false;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }

            UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
            }

            if (Locomotion != null)
            {
                Locomotion.enabled = true;
            }
            
            // Restore position up
            transform.position += new Vector3(0f, 0.5f, 0f);
            
            // Stand up straight
            transform.localRotation = Quaternion.Euler(0f, transform.localEulerAngles.y, 0f);
        }

        public void BuildBehaviorTree()
        {
            // Priority 0: Sleep Sequence
            Sequence sleepSequence = new Sequence("Sleep Sequence", new List<Node>
            {
                new NeedsSleepNode(aiContext),
                new SleepNode(aiContext)
            });

            // Priority 1: Flee from Threats
            Sequence fleeSequence = new Sequence("Flee Sequence", new List<Node>
            {
                new CheckDangerNode(aiContext),
                new FleeNode(aiContext)
            });

            // Priority 2: Eat Food
            Sequence eatSequence = new Sequence("Eat Sequence", new List<Node>
            {
                new NeedsFoodNode(aiContext),
                new SeesFoodNode(aiContext),
                new EatFoodNode(aiContext)
            });

            // Priority 3: Seek Shelter
            Sequence shelterSequence = new Sequence("Seek Shelter Sequence", new List<Node>
            {
                new NeedsShelterNode(aiContext),
                new SeekShelterNode(aiContext)
            });

            // Helper: Generic Crafting Subtree
            // This expects context.TargetBlueprintPrefab to be set by a previous node
            Sequence craftSubtree = new Sequence("Crafting Subtree", new List<Node>
            {
                new FindPlacementSpotTaskNode(aiContext),
                new MoveToPlacementNode(aiContext),
                new PlaceBlueprintNode(aiContext),
                
                    new Selector("Get Required Resource", new List<Node>
                    {
                        // Option 1: Standard Gathering Path (Find what is exactly needed)
                        new Sequence("Gathering Loop", new List<Node>
                        {
                            new CheckRecipeNode(aiContext),
                            new FindResourceNode(aiContext),
                            new CollectResourceNode(aiContext),
                            new DeliverResourceNode(aiContext)
                        }),

                        // Fallback crafting paths
                        CreateConversionFallback(World.ResourceType.Stick_1, World.ResourceType.Stick_2, 2, 1.0f),
                        CreateMultiOutputFallback(World.ResourceType.Stick_1, World.ResourceType.Stick_3, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Stick_2, 1),
                            new ResourceOutput(World.ResourceType.Stick_1, 1)),
                        CreateMultiOutputFallback(World.ResourceType.Stick_2, World.ResourceType.Stick_3, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Stick_2, 1),
                            new ResourceOutput(World.ResourceType.Stick_1, 1)),
                        CreateMultiOutputFallback(World.ResourceType.Stick_1, World.ResourceType.Stick_4, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Stick_3, 1),
                            new ResourceOutput(World.ResourceType.Stick_1, 1)),
                        CreateMultiOutputFallback(World.ResourceType.Stick_3, World.ResourceType.Stick_4, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Stick_3, 1),
                            new ResourceOutput(World.ResourceType.Stick_1, 1)),
                        CreateConversionFallback(World.ResourceType.Stick_2, World.ResourceType.Stick_4, 2, 1.0f),

                        CreateConversionFallback(World.ResourceType.Log_1, World.ResourceType.Log_2, 2, 1.0f),
                        CreateMultiOutputFallback(World.ResourceType.Log_1, World.ResourceType.Log_3, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Log_2, 1),
                            new ResourceOutput(World.ResourceType.Log_1, 1)),
                        CreateMultiOutputFallback(World.ResourceType.Log_2, World.ResourceType.Log_3, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Log_2, 1),
                            new ResourceOutput(World.ResourceType.Log_1, 1)),
                        CreateMultiOutputFallback(World.ResourceType.Log_1, World.ResourceType.Log_4, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Log_3, 1),
                            new ResourceOutput(World.ResourceType.Log_1, 1)),
                        CreateMultiOutputFallback(World.ResourceType.Log_3, World.ResourceType.Log_4, 1, 1.0f,
                            new ResourceOutput(World.ResourceType.Log_3, 1),
                            new ResourceOutput(World.ResourceType.Log_1, 1)),
                        CreateConversionFallback(World.ResourceType.Log_2, World.ResourceType.Log_4, 2, 1.0f),
                        
                        // Option 7: Multi-Output fallback (Stone)
                        CreateMultiOutputFallback(World.ResourceType.Sharpened_Stone, World.ResourceType.Stone, 2, 5.0f,
                            new ResourceOutput(World.ResourceType.Sharpened_Stone, 1),
                            new ResourceOutput(World.ResourceType.Stone, 1))
                    })
            });

            // Priority 4: Thermal Comfort
            Selector thermalSelector = new Selector("Thermal Comfort Goals", new List<Node>
            {
                // A. Seek Shade (When Hot)
                new Sequence("Seek Shade Branch", new List<Node>
                {
                    new ActionNode("Check Is Hot", () => currentThermalStatus == ThermalStatus.Hot ? NodeState.Success : NodeState.Failure),
                    new FindShadeSpotNode(aiContext),
                    new MoveToShadeNode(aiContext)
                }),

                // B. Seek Warmth (When Cold)
                new Sequence("Seek Warmth Branch", new List<Node>
                {
                    new NeedsWarmthNode(aiContext),
                    new Selector("Find or Build Fire", new List<Node>
                    {
                        new Sequence("Use existing fire", new List<Node>
                        {
                            new FindHeatSourceNode(aiContext),
                            new MoveToHeatSourceNode(aiContext)
                        }),
                        new Sequence("Build new fire fallback", new List<Node>
                        {
                            new SetCraftingTargetNode(aiContext, campfireBlueprintPrefab),
                            craftSubtree
                        })
                    })
                })
            });

            // Priority 5: Fell Tree (Tool Dependency Example)
            Sequence fellTreeSequence = new Sequence("Fell Tree Goal", new List<Node>
            {
                // Only run if test flag is on
                new ActionNode("Check Felling Flag", () => startFellingTest ? NodeState.Success : NodeState.Failure),
                
                // Dependency: Basic_Axe
                new Selector("Acquire Basic_Axe", new List<Node>
                {
                    new CheckHasToolNode(aiContext, "Basic_Axe"),
                    new Selector("Get Basic_Axe on ground or build", new List<Node>
                    {
                        // 1. Check if an axe is already lying around
                        new Sequence("Pick up Basic_Axe", new List<Node>
                        {
                            new FindToolOnGroundNode(aiContext, "Basic_Axe"),
                            new CollectToolNode(aiContext)
                        }),
                        // 2. If not, craft one
                        new Sequence("Craft Basic_Axe Goal", new List<Node>
                        {
                            new SetCraftingTargetNode(aiContext, axeBlueprintPrefab),
                            craftSubtree
                        })
                    })
                }),

                // Action: Fell
                new Sequence("Perform Felling", new List<Node>
                {
                    new FindHarvestableSourceNode(aiContext, useResourceFilter: false),
                    new FellTreeNode(aiContext)
                })
            });

            // Priority 6: Wander
            Sequence wanderSequence = new Sequence("Wander Sequence", new List<Node>
            {
                new WanderNode(aiContext)
            });

            rootNode = new Selector("Human Behavior", new List<Node>
            {
                sleepSequence,
                fleeSequence,
                eatSequence,
                shelterSequence,
                thermalSelector,
                fellTreeSequence,
                wanderSequence
            });
        }

        private Node CreateConversionFallback(World.ResourceType needed, World.ResourceType source, int resultCount, float duration = 1.0f)
        {
            return new Sequence($"{source} to {needed} Fallback", new List<Node>
            {
                new CheckNextResourceNeedsTypeNode(aiContext, needed),
                new FindSpecificResourceNode(aiContext, source),
                new CollectResourceNode(aiContext, 1, source),
                new ConvertResourceNode(aiContext, source, needed, resultCount, duration)
            });
        }

        private Node CreateMultiOutputFallback(World.ResourceType needed, World.ResourceType source, int sourceQty, float duration, params ResourceOutput[] outputs)
        {
            return new Sequence($"{source} to {needed} Fallback", new List<Node>
            {
                new CheckNextResourceNeedsTypeNode(aiContext, needed),
                new FindSpecificResourceNode(aiContext, source),
                new CollectResourceNode(aiContext, sourceQty, source),
                new ConvertResourceNode(aiContext, source, sourceQty, duration, outputs)
            });
        }

        public string GetBehaviorTreeStatus()
        {
            if (rootNode == null) return "Initializing...";
            
            string thermalInfo = currentThermalStatus != ThermalStatus.Comfortable ? $" [{currentThermalStatus}]" : "";
            return rootNode.GetTreeStateAsString(0) + thermalInfo;
        }

    }
}
