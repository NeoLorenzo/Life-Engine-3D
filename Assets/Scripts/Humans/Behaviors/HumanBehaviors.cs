using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using LifeEngine.AI;

namespace LifeEngine.SimulatedHumans.Behaviors
{
    // --- Context Class --- 
    // Passed to nodes so they can access the human's components.
    public class HumanContext
    {
        public HumanBrain Brain;
        public HumanLocomotion Locomotion;
        public HumanPerception Perception;
        public HumanMemory Memory;
        
        public float PanicTimer = 999f;
        public float PanicPersistence = 4.0f;
        public float OutsideRoomComfortDuration;
        public float OutsideRoomComfortTimer;

        // --- Crafting & Tool State ---
        public GameObject TargetBlueprintPrefab;
        public Crafting.CraftingBlueprint CurrentBlueprintInstance;
        public World.ResourceItem TargetResource;
        public World.ToolItem TargetTool;

        public Vector3 CurrentShelterTarget;
        public float WanderTimer;
        public float NextWanderTime;
        public Transform CurrentFoodTarget;
        public float EatingTimer;

        // Tree Felling
        public LifeEngine.World.FellableTree CurrentTreeTarget;
        public float FellingTimer;

        // Crafting
        public float CraftingTimer;
        public Vector3 PlacementSpot;
        public LifeEngine.World.ResourceType CurrentNeededResource;
        public Transform CurrentResourceTarget;

        public Transform CurrentHeatSourceTarget;
        public Vector3 CurrentShadeTarget;
    }

    // --- Nodes ---

    public class NeedsSleepNode : Node
    {
        private HumanContext context;

        public NeedsSleepNode(HumanContext context)
        {
            this.Name = "Needs Sleep";
            this.context = context;
        }

        public override string GetDebugText()
        {
            return $"Adenosine: {context.Brain.adenosineConcentration:F1}nm";
        }

        public override NodeState Evaluate()
        {
            // If they are already sleeping, keep succeeding so SleepNode runs
            if (context.Brain.isSleeping)
            {
                state = NodeState.Success;
                return state;
            }

            // Fall asleep if adenosine hits 100
            if (context.Brain.adenosineConcentration >= 100f)
            {
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class SleepNode : Node
    {
        private HumanContext context;

        public SleepNode(HumanContext context)
        {
            this.Name = "Sleeping";
            this.context = context;
        }

        public override string GetDebugText()
        {
            return $"Adenosine: {context.Brain.adenosineConcentration:F1}nm (Clearing)";
        }

        public override NodeState Evaluate()
        {
            // If just starting to sleep
            if (!context.Brain.isSleeping)
            {
                context.Brain.FallAsleep();
            }

            if (World.TimeManager.Instance != null && World.TimeManager.Instance.realSecondsPerGameMinute > 0)
            {
                float gameHours = (Time.deltaTime / World.TimeManager.Instance.realSecondsPerGameMinute) / 60f;
                context.Brain.adenosineConcentration -= context.Brain.adenosineClearancePerHour * gameHours;
            }

            if (context.Brain.adenosineConcentration <= 10f)
            {
                context.Brain.adenosineConcentration = 10f;
                context.Brain.WakeUp();
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Running;
            return state;
        }
    }

    public class CheckDangerNode : Node
    {
        private HumanContext context;

        public CheckDangerNode(HumanContext context)
        {
            this.Name = "Check For Danger";
            this.context = context;
        }

        public override string GetDebugText()
        {
            Transform threat = context.Memory.GetPrimaryThreat();
            if (threat != null) return $"Threat: {threat.name}";
            return $"Panic Timer: {context.PanicTimer:F1}s";
        }

        public override NodeState Evaluate()
        {
            Transform closestThreat;
            bool hasVisibleThreat = context.Perception.PerformDangerScan(out closestThreat);
            
            if (hasVisibleThreat)
            {
                context.Memory.SetPrimaryThreat(closestThreat);
            }
            
            // Re-sync memory's active threat list from perception's raw points
            context.Memory.GetActiveThreatPositions(context.Perception.currentlyVisibleThreatPositions);

            if (hasVisibleThreat && closestThreat != null)
            {
                context.PanicTimer = 0f;
                state = NodeState.Success;
                return state;
            }

            context.Memory.SetPrimaryThreat(null);
            context.PanicTimer += Time.deltaTime;

            if (context.PanicTimer < context.PanicPersistence)
            {
                state = NodeState.Success; // Still panicking from recent threat
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class FleeNode : Node
    {
        private HumanContext context;
        private float nextFleeReplanTime;
        private float fleeReplanInterval = 0.25f;
        private bool forceFleeReplan = false;
        
        // Tunables
        private float fleeNearRadius = 4f;
        private float fleeFarRadius = 8f;
        private int fleeAnglesPerRing = 8;
        private float fleeProbeRadius = 2f;
        private float fleePathCostWeight = 0.35f;
        private float fleeOpennessWeight = 0.25f;

        public FleeNode(HumanContext context)
        {
            this.Name = "Flee Target";
            this.context = context;
        }

        public override void ResetState()
        {
            base.ResetState();
            forceFleeReplan = false;
        }

        public override NodeState Evaluate()
        {
            if (!context.Locomotion.IsAgentReady()) 
            {
                state = NodeState.Running;
                return state;
            }

            context.Locomotion.SetRun();

            List<Vector3> activeThreats = context.Memory.GetActiveThreatPositions(null);
            if (activeThreats == null || activeThreats.Count == 0)
            {
                state = NodeState.Running;
                return state;
            }

            if (context.Locomotion.CheckIfStuck())
            {
                forceFleeReplan = true;
            }

            bool shouldReplan = forceFleeReplan
                || Time.time >= nextFleeReplanTime
                || context.Locomotion.HasReachedDestination();

            if (shouldReplan)
            {
                if (TryFindBestFleeDestination(activeThreats, out Vector3 bestDestination))
                {
                    context.Locomotion.SetDestination(bestDestination);
                    forceFleeReplan = false;
                    nextFleeReplanTime = Time.time + Mathf.Max(0.05f, fleeReplanInterval);
                }
                
                if (context.Locomotion.GetConsecutiveStuckCount() > 0)
                {
                    context.Locomotion.ClearStuckCount();
                }
            }

            state = NodeState.Running;
            return state;
        }

        private bool TryFindBestFleeDestination(List<Vector3> activeThreats, out Vector3 bestDestination)
        {
            bestDestination = context.Brain.transform.position;
            
            float nearRadius = fleeNearRadius;
            float farRadius = fleeFarRadius;
            
            if (context.Locomotion.GetConsecutiveStuckCount() >= 2)
            {
                nearRadius = Mathf.Max(nearRadius, 6f);
                farRadius = Mathf.Max(farRadius, 10f);
            }

            float angleStep = 360f / fleeAnglesPerRing;
            bool hasBest = false;
            float bestScore = float.MinValue;
            float bestPathLength = float.MaxValue;

            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? nearRadius : farRadius;
                for (int i = 0; i < fleeAnglesPerRing; i++)
                {
                    float radians = Mathf.Deg2Rad * (angleStep * i);
                    Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                    Vector3 candidate = context.Brain.transform.position + (direction * radius);

                    TryScoreFleeCandidate(activeThreats, candidate, ref hasBest, ref bestScore, ref bestPathLength, ref bestDestination);
                }
            }

            Vector3 awayFromCentroid = ComputeAwayFromThreatCentroidDirection(activeThreats);
            Vector3 centroidCandidate = context.Brain.transform.position + (awayFromCentroid * farRadius);
            TryScoreFleeCandidate(activeThreats, centroidCandidate, ref hasBest, ref bestScore, ref bestPathLength, ref bestDestination);

            return hasBest;
        }

        private void TryScoreFleeCandidate(
            List<Vector3> activeThreats,
            Vector3 rawCandidate,
            ref bool hasBest,
            ref float bestScore,
            ref float bestPathLength,
            ref Vector3 bestDestination)
        {
            NavMeshHit sampled;
            if (!NavMesh.SamplePosition(rawCandidate, out sampled, Mathf.Max(1f, fleeProbeRadius), NavMesh.AllAreas))
            {
                return;
            }

            if (!context.Locomotion.IsPathValid(sampled.position, out float pathLength))
            {
                return;
            }

            float nearestThreatDist = CalculateNearestThreatDistance(activeThreats, sampled.position);
            float openness = CalculateOpenness(sampled.position);
            
            float score = nearestThreatDist - (pathLength * fleePathCostWeight) + (openness * fleeOpennessWeight);

            bool isBetter = false;
            if (!hasBest || score > bestScore + 0.0001f)
            {
                isBetter = true;
            }
            else if (Mathf.Abs(score - bestScore) <= 0.0001f && pathLength < bestPathLength - 0.0001f)
            {
                isBetter = true;
            }

            if (isBetter)
            {
                hasBest = true;
                bestScore = score;
                bestPathLength = pathLength;
                bestDestination = sampled.position;
            }
        }

        private float CalculateNearestThreatDistance(List<Vector3> threats, Vector3 pos)
        {
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < threats.Count; i++)
            {
                float distSqr = (threats[i] - pos).sqrMagnitude;
                if (distSqr < nearestSqr)
                {
                    nearestSqr = distSqr;
                }
            }
            return Mathf.Sqrt(Mathf.Max(0f, nearestSqr));
        }

        private float CalculateOpenness(Vector3 position)
        {
            int openCount = 0;
            float probeSampleRadius = Mathf.Max(0.5f, fleeProbeRadius * 0.5f);
            float probeDistance = Mathf.Max(0.5f, fleeProbeRadius);

            for (int i = 0; i < 8; i++)
            {
                float radians = Mathf.Deg2Rad * (45f * i);
                Vector3 dir = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                Vector3 probePoint = position + (dir * probeDistance);

                if (NavMesh.SamplePosition(probePoint, out _, probeSampleRadius, NavMesh.AllAreas))
                {
                    openCount++;
                }
            }
            return openCount;
        }

        private Vector3 ComputeAwayFromThreatCentroidDirection(List<Vector3> threats)
        {
            Vector3 weightedCentroid = Vector3.zero;
            float totalWeight = 0f;

            for (int i = 0; i < threats.Count; i++)
            {
                Vector3 toThreat = threats[i] - context.Brain.transform.position;
                float distSqr = Mathf.Max(0.25f, toThreat.sqrMagnitude);
                float weight = 1f / distSqr;
                weightedCentroid += threats[i] * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0f) weightedCentroid /= totalWeight;
            else weightedCentroid = context.Memory.GetLastKnownThreatPosition();

            Vector3 away = context.Brain.transform.position - weightedCentroid;
            away.y = 0f;
            
            if (away.sqrMagnitude <= 0.0001f)
            {
                away = context.Brain.transform.forward;
                away.y = 0f;
            }

            return away.normalized;
        }
    }

    public class NeedsShelterNode : Node
    {
        private HumanContext context;

        public NeedsShelterNode(HumanContext context)
        {
            this.Name = "Needs Shelter";
            this.context = context;
        }

        public override string GetDebugText()
        {
            if (context.OutsideRoomComfortTimer > 0f) return $"Comfort CD: {context.OutsideRoomComfortTimer:F1}s";
            return "Need Room!";
        }

        public override NodeState Evaluate()
        {
            int roomAreaMask = 1 << 3;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(context.Brain.transform.position, out hit, 2.0f, NavMesh.AllAreas))
            {
                if ((hit.mask & roomAreaMask) != 0) 
                {
                    context.OutsideRoomComfortTimer = Mathf.Max(0f, context.OutsideRoomComfortDuration);

                    if (context.CurrentShelterTarget != Vector3.zero)
                    {
                        if (!context.Locomotion.HasReachedDestination(1.5f))
                        {
                            state = NodeState.Success;
                            return state;
                        }
                        context.CurrentShelterTarget = Vector3.zero;
                    }
                    
                    state = NodeState.Failure; 
                    return state;
                }
            }

            if (context.OutsideRoomComfortTimer > 0f)
            {
                context.OutsideRoomComfortTimer = Mathf.Max(0f, context.OutsideRoomComfortTimer - Time.deltaTime);
                state = NodeState.Failure;
                return state;
            }

            state = NodeState.Success; 
            return state;
        }
    }

    public class SeekShelterNode : Node
    {
        private HumanContext context;

        public SeekShelterNode(HumanContext context)
        {
            this.Name = "Seek Shelter";
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            if (!context.Locomotion.IsAgentReady()) 
            {
                state = NodeState.Running;
                return state;
            }

            context.Locomotion.SetWalk();

            if (context.CurrentShelterTarget != Vector3.zero && !context.Locomotion.HasReachedDestination(1.0f))
            {
                state = NodeState.Running;
                return state;
            }
            
            int roomAreaMask = 1 << 3;

            NavMeshHit roomHit;
            if (NavMesh.SamplePosition(context.Brain.transform.position, out roomHit, 100f, roomAreaMask))
            {
                Vector3 approachDirection = (roomHit.position - context.Brain.transform.position).normalized;
                if (approachDirection != Vector3.zero && Vector3.Distance(context.Brain.transform.position, roomHit.position) > 1.0f) 
                {
                    Vector3 deeperTarget = roomHit.position + (approachDirection * 3.5f); 
                    
                    if (NavMesh.SamplePosition(deeperTarget, out NavMeshHit deepHit, 4.0f, roomAreaMask))
                    {
                        context.CurrentShelterTarget = deepHit.position;
                    }
                    else 
                    {
                        context.CurrentShelterTarget = roomHit.position; 
                    }
                }
                else
                {
                    context.CurrentShelterTarget = roomHit.position;
                }

                context.Locomotion.SetDestination(context.CurrentShelterTarget);
                state = NodeState.Running;
                return state;
            }
            
            state = NodeState.Failure;
            return state;
        }
    }

    public class WanderNode : Node
    {
        private HumanContext context;
        private float wanderRadius = 10f;
        private float minWaitTime = 0.5f;
        private float maxWaitTime = 2.0f;
        private int sampleCount = 8;

        public WanderNode(HumanContext context)
        {
            this.Name = "Wander";
            this.context = context;
        }

        public override string GetDebugText()
        {
            float rem = Mathf.Max(0f, context.NextWanderTime - context.WanderTimer);
            return $"Wander CD: {rem:F1}s";
        }

        public override NodeState Evaluate()
        {
            if (!context.Locomotion.IsAgentReady()) 
            {
                state = NodeState.Running;
                return state;
            }

            context.Locomotion.SetWalk();

            if (context.Locomotion.HasReachedDestination(0.5f))
            {
                context.WanderTimer += Time.deltaTime;

                if (context.WanderTimer >= context.NextWanderTime)
                {
                    float actualWanderRadius = wanderRadius;
                    int maskToUse = NavMesh.AllAreas;
                    int roomAreaMask = 1 << 3;
                     
                    if (NavMesh.SamplePosition(context.Brain.transform.position, out NavMeshHit roomCheck, 2.0f, NavMesh.AllAreas))
                    {
                        if ((roomCheck.mask & roomAreaMask) != 0)
                        {
                            maskToUse = roomAreaMask; 
                            actualWanderRadius = 4f; 
                        }
                    }

                    Vector3 bestTarget = context.Brain.transform.position;
                    float bestScore = float.MaxValue;
                    bool foundValidTarget = false;
                    
                    Vector3 fallbackTarget = context.Brain.transform.position;
                    bool hasFallbackTarget = false;

                    for (int i = 0; i < sampleCount; i++)
                    {
                        Vector2 randomDir = Random.insideUnitCircle * actualWanderRadius;
                        Vector3 candidate = context.Brain.transform.position + new Vector3(randomDir.x, 0f, randomDir.y);

                        if (!NavMesh.SamplePosition(candidate, out NavMeshHit candidateHit, 2.0f, maskToUse))
                        {
                            continue;
                        }

                        if (!hasFallbackTarget)
                        {
                            fallbackTarget = candidateHit.position;
                            hasFallbackTarget = true;
                        }

                        if (context.Locomotion.IsPathValid(candidateHit.position, out float pathLen))
                        {
                            if (pathLen < bestScore)
                            {
                                bestScore = pathLen;
                                bestTarget = candidateHit.position;
                                foundValidTarget = true;
                            }
                        }
                    }

                    if (foundValidTarget)
                    {
                        context.Locomotion.SetDestination(bestTarget);
                    }
                    else if (hasFallbackTarget)
                    {
                        context.Locomotion.SetDestination(fallbackTarget);
                    }

                    context.WanderTimer = 0f;
                    context.NextWanderTime = Random.Range(minWaitTime, maxWaitTime);
                }
            }
            else
            {
                context.WanderTimer = 0f;
            }

            state = NodeState.Running;
            return state;
        }
    }

    public class NeedsFoodNode : Node
    {
        private HumanContext context;

        public NeedsFoodNode(HumanContext context)
        {
            this.Name = "Needs Food";
            this.context = context;
        }

        public override string GetDebugText()
        {
            return $"Ghrelin: {context.Brain.ghrelinConcentration:F0} pg/mL";
        }

        public override NodeState Evaluate()
        {
            // If they are actively locked onto food or eating it, prevent hunger from disappearing
            if (context.CurrentFoodTarget != null || context.EatingTimer > 0f)
            {
                state = NodeState.Success;
                return state;
            }

            if (context.Brain.ghrelinConcentration >= context.Brain.ghrelinHungerThreshold)
            {
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class SeesFoodNode : Node
    {
        private HumanContext context;

        public SeesFoodNode(HumanContext context)
        {
            this.Name = "Spot Food";
            this.context = context;
        }

        public override string GetDebugText()
        {
            if (context.CurrentFoodTarget != null) return $"Target: {context.CurrentFoodTarget.name}";
            return "Searching...";
        }

        public override NodeState Evaluate()
        {
            // If we are already eating, stay successful to let the EatFoodNode finish
            if (context.EatingTimer > 0f)
            {
                state = NodeState.Success;
                return state;
            }

            if (context.Perception.PerformFoodScan(out Transform food))
            {
                context.CurrentFoodTarget = food;
                state = NodeState.Success;
                return state;
            }

            context.CurrentFoodTarget = null;
            state = NodeState.Failure;
            return state;
        }
    }

    public class EatFoodNode : Node
    {
        private HumanContext context;

        public EatFoodNode(HumanContext context)
        {
            this.Name = "Eating Food";
            this.context = context;
        }

        public override string GetDebugText()
        {
            if (context.EatingTimer > 0) return $"Chowing Down: {1.5f - context.EatingTimer:F1}s";
            return "Chasing";
        }

        public override NodeState Evaluate()
        {
            if (!context.Locomotion.IsAgentReady())
            {
                state = NodeState.Running;
                return state;
            }

            // check timer first so we can finish eating even if target is destroyed
            if (context.EatingTimer > 0f)
            {
                context.EatingTimer += Time.deltaTime;
                if (context.EatingTimer >= 1.5f)
                {
                    context.EatingTimer = 0f;
                    context.CurrentFoodTarget = null;
                    context.Brain.ghrelinConcentration = 500f; // Reset Ghrelin
                    state = NodeState.Success;
                    return state;
                }
                state = NodeState.Running;
                return state;
            }

            if (context.CurrentFoodTarget == null)
            {
                state = NodeState.Failure;
                return state;
            }

            context.Locomotion.SetRun();
            context.Locomotion.SetDestination(context.CurrentFoodTarget.position);

            if (Vector3.Distance(context.Brain.transform.position, context.CurrentFoodTarget.position) <= 1.0f)
            {
                Object.Destroy(context.CurrentFoodTarget.gameObject);
                context.EatingTimer += 0.01f; 
                context.Locomotion.Stop();
            }

            state = NodeState.Running;
            return state;
        }
    }

    public class FellTreeNode : Node
    {
        private HumanContext context;

        public FellTreeNode(HumanContext context)
        {
            this.Name = "Harvesting Nature Object";
            this.context = context;
        }

        public override void ResetState()
        {
            base.ResetState();
        }

        public override string GetDebugText()
        {
            if (context.CurrentTreeTarget == null) return "No Target";
            float totalRequiredSeconds = context.CurrentTreeTarget.harvestDurationMinutes * LifeEngine.World.TimeManager.Instance.realSecondsPerGameMinute;
            float progress = (context.FellingTimer / totalRequiredSeconds) * 100f;
            return $"Harvesting {context.CurrentTreeTarget.name} ({progress:F0}%)";
        }

        public override NodeState Evaluate()
        {
            // 1. Target Validation
            if (context.CurrentTreeTarget == null)
            {
                context.FellingTimer = 0f;
                state = NodeState.Failure;
                return state;
            }

            // 2. Tool Safety Check (Fallback defense)
            if (context.CurrentTreeTarget.requiresTool && !context.Brain.HasTool("Basic_Axe"))
            {
                Debug.LogWarning($"{context.Brain.name} trying to fell {context.CurrentTreeTarget.name} without an axe! Aborting.");
                context.CurrentTreeTarget = null;
                context.FellingTimer = 0f;
                state = NodeState.Failure;
                return state;
            }

            if (!context.Locomotion.IsAgentReady())
            {
                state = NodeState.Running;
                return state;
            }

            float distance = Vector3.Distance(context.Brain.transform.position, context.CurrentTreeTarget.transform.position);
            float totalRequiredSeconds = context.CurrentTreeTarget.harvestDurationMinutes * LifeEngine.World.TimeManager.Instance.realSecondsPerGameMinute;

            // 2. Proximity check
            if (distance > 1.5f) 
            {
                context.FellingTimer = 0f;
                context.Locomotion.SetWalk();
                context.Locomotion.SetDestination(context.CurrentTreeTarget.transform.position);
                state = NodeState.Running;
                return state;
            }

            // 3. We are close! Perform harvesting.
            context.Locomotion.Stop();
            context.FellingTimer += Time.deltaTime;

            if (context.FellingTimer >= totalRequiredSeconds)
            {
                Debug.Log($"{context.Brain.name} successfully harvested {context.CurrentTreeTarget.name}");
                context.FellingTimer = 0f;
                
                var target = context.CurrentTreeTarget;
                context.CurrentTreeTarget = null;
                target.Fell();

                state = NodeState.Success;
                return state;
            }

            state = NodeState.Running;
            return state;
        }
    }

    public class FindHarvestableSourceNode : Node
    {
        private HumanContext context;
        private bool? requireToolFilter;
        private bool useResourceFilter;

        public FindHarvestableSourceNode(HumanContext context, bool? requireToolFilter = null, bool useResourceFilter = true)
        {
            this.Name = "Find Harvestable Source";
            this.context = context;
            this.requireToolFilter = requireToolFilter;
            this.useResourceFilter = useResourceFilter;
        }

        public override NodeState Evaluate()
        {
            // 1. Validate existing target (Ensure it matches current filter)
            if (context.CurrentTreeTarget != null)
            {
                bool matchesToolFilter = !requireToolFilter.HasValue || context.CurrentTreeTarget.requiresTool == requireToolFilter.Value;
                if (matchesToolFilter)
                {
                    state = NodeState.Success;
                    return state;
                }
                else
                {
                    // Target was for a different felling goal (e.g. Tree when we want Bush), clear it.
                    context.CurrentTreeTarget = null;
                }
            }

            // Fresh scan
            LifeEngine.World.FellableTree[] all = Object.FindObjectsByType<LifeEngine.World.FellableTree>(FindObjectsSortMode.None);
            
            float closestDist = float.MaxValue;
            LifeEngine.World.FellableTree closest = null;

            foreach (var target in all)
            {
                if (target == null) continue;

                // 1. Tool Filter
                if (requireToolFilter.HasValue && target.requiresTool != requireToolFilter.Value) continue;

                // 2. Resource Filter
                if (useResourceFilter && context.CurrentNeededResource != World.ResourceType.None)
                {
                    if (!target.DropsResource(context.CurrentNeededResource)) continue;
                }

                float dist = Vector3.Distance(context.Brain.transform.position, target.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = target;
                }
            }

            if (closest != null)
            {
                context.CurrentTreeTarget = closest;
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class FindPlacementSpotTaskNode : Node
    {
        private HumanContext context;

        public FindPlacementSpotTaskNode(HumanContext context)
        {
            this.Name = "Find Placement Spot";
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            // If we already have a blueprint or a placement target, we've succeeded in finding one.
            if (context.CurrentBlueprintInstance != null || context.PlacementSpot != Vector3.zero)
            {
                state = NodeState.Success;
                return state;
            }

            // Find a spot about 3 meters in front of the human
            Vector3 center = context.Brain.transform.position + context.Brain.transform.forward * 3.0f;
            
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(center, out hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                context.PlacementSpot = hit.position;
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class MoveToPlacementNode : Node
    {
        private HumanContext context;

        public MoveToPlacementNode(HumanContext context)
        {
            this.Name = "Moving to Placement Site";
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            if (context.CurrentBlueprintInstance != null)
            {
                state = NodeState.Success;
                return state;
            }

            if (context.PlacementSpot == Vector3.zero)
            {
                state = NodeState.Failure;
                return state;
            }

            if (!context.Locomotion.IsAgentReady())
            {
                state = NodeState.Running;
                return state;
            }

            context.Locomotion.SetRun();
            context.Locomotion.SetDestination(context.PlacementSpot);

            if (context.Locomotion.HasReachedDestination(1.5f))
            {
                context.Locomotion.Stop();
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Running;
            return state;
        }
    }

    public class PlaceBlueprintNode : Node
    {
        private HumanContext context;

        public PlaceBlueprintNode(HumanContext context)
        {
            this.context = context;
            Name = "Place Blueprint Instance";
        }

        public override NodeState Evaluate()
        {
            if (context.CurrentBlueprintInstance != null)
            {
                state = NodeState.Success;
                return state;
            }

            if (context.TargetBlueprintPrefab == null)
            {
                Debug.LogWarning("PlaceBlueprintNode: No TargetBlueprintPrefab set in context.");
                state = NodeState.Failure;
                return state;
            }

            // Safety: Check if another blueprint or the final object already exists at this exact spot
            // This prevents the infinite spawning loop if the agent re-triggers the build task too quickly.
            
            // 1. Check for blueprints using physics
            Collider[] hits = Physics.OverlapSphere(context.PlacementSpot, 0.5f);
            foreach (var hit in hits)
            {
                var existingBP = hit.GetComponent<Crafting.CraftingBlueprint>();
                if (existingBP != null)
                {
                    context.CurrentBlueprintInstance = existingBP;
                    state = NodeState.Success;
                    return state;
                }
            }

            // 2. Check for heat sources using registry (much more reliable)
            foreach (var source in World.HeatSource.ActiveSources)
            {
                if (source == null || !source.isActive) continue;

                float distToFire = Vector3.Distance(context.PlacementSpot, source.transform.position);
                if (distToFire <= 1.0f) // If there's a fire within 1m of the spot
                {
                    state = NodeState.Success;
                    return state;
                }
            }

            // Detect if this is a tool blueprint to lay it flat (-90 on X)
            Quaternion rotation = Quaternion.identity;
            var bp = context.TargetBlueprintPrefab.GetComponent<Crafting.CraftingBlueprint>();
            if (bp != null && bp.finalPrefab != null && bp.finalPrefab.GetComponent<World.ToolItem>() != null)
            {
                rotation = Quaternion.Euler(-90, 0, 0);
            }

            GameObject blueprintObj = Object.Instantiate(context.TargetBlueprintPrefab, context.PlacementSpot, rotation);
            context.CurrentBlueprintInstance = blueprintObj.GetComponent<Crafting.CraftingBlueprint>();
            
            if (context.CurrentBlueprintInstance == null)
            {
                Debug.LogError("PlaceBlueprintNode: Prefab does not have a CraftingBlueprint component!");
                state = NodeState.Failure;
                return state;
            }

            state = NodeState.Success;
            return state;
        }
    }

    // --- Tool Behavior Nodes ---

    public class CheckHasToolNode : Node
    {
        private HumanContext context;
        private string toolName;

        public CheckHasToolNode(HumanContext context, string toolName)
        {
            this.context = context;
            this.toolName = toolName;
            Name = $"Has Tool: {toolName}?";
        }

        public override NodeState Evaluate()
        {
            state = context.Brain.HasTool(toolName) ? NodeState.Success : NodeState.Failure;
            return state;
        }
    }

    public class SetCraftingTargetNode : Node
    {
        private HumanContext context;
        private GameObject blueprintPrefab;

        public SetCraftingTargetNode(HumanContext context, GameObject blueprintPrefab)
        {
            this.context = context;
            this.blueprintPrefab = blueprintPrefab;
            Name = "Set Crafting Target";
        }

        public override NodeState Evaluate()
        {
            // If already building this, success
            if (context.TargetBlueprintPrefab == blueprintPrefab)
            {
                state = NodeState.Success;
                return state;
            }

            // Fresh setup
            context.TargetBlueprintPrefab = blueprintPrefab;
            context.CurrentBlueprintInstance = null; // Clear old instance if switching
            context.PlacementSpot = Vector3.zero;    // Clear old spot
            state = NodeState.Success;
            return state;
        }
    }

    public class FindToolOnGroundNode : Node
    {
        private HumanContext context;
        private string toolName;

        public FindToolOnGroundNode(HumanContext context, string toolName)
        {
            this.context = context;
            this.toolName = toolName;
            Name = $"Finding {toolName} on ground";
        }

        public override NodeState Evaluate()
        {
            if (context.TargetTool != null && context.TargetTool.toolName == toolName)
            {
                state = NodeState.Success;
                return state;
            }

            World.ToolItem[] tools = Object.FindObjectsByType<World.ToolItem>(FindObjectsSortMode.None);
            World.ToolItem closest = null;
            float closestDist = float.MaxValue;

            foreach (var tool in tools)
            {
                if (tool.toolName != toolName) continue;
                float dist = Vector3.Distance(context.Brain.transform.position, tool.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = tool;
                }
            }

            if (closest != null)
            {
                context.TargetTool = closest;
                state = NodeState.Success;
            }
            else
            {
                state = NodeState.Failure;
            }

            return state;
        }
    }

    public class CollectToolNode : Node
    {
        private HumanContext context;

        public CollectToolNode(HumanContext context)
        {
            this.context = context;
            Name = "Collecting Tool";
        }

        public override NodeState Evaluate()
        {
            if (context.TargetTool == null)
            {
                state = NodeState.Failure;
                return state;
            }

            float dist = Vector3.Distance(context.Brain.transform.position, context.TargetTool.transform.position);
            if (dist > 1.5f)
            {
                context.Locomotion.SetRun();
                context.Locomotion.SetDestination(context.TargetTool.transform.position);
                state = NodeState.Running;
                return state;
            }

            context.Locomotion.Stop();
            
            // Add to inventory
            context.Brain.toolInventory.Add(context.TargetTool.toolName);
            
            Object.Destroy(context.TargetTool.gameObject);
            context.TargetTool = null;
            context.Brain.UpdateToolVisual();

            state = NodeState.Success;
            return state;
        }
    }

    // --- Resource Gathering Nodes ---

    public class CheckRecipeNode : Node
    {
        private HumanContext context;

        public CheckRecipeNode(HumanContext context)
        {
            this.Name = "Checking Recipe";
            this.context = context;
        }

        public override string GetDebugText()
        {
            if (context.CurrentBlueprintInstance == null) return "No Blueprint";
            return $"Next: {context.CurrentNeededResource}";
        }

        public override NodeState Evaluate()
        {
            if (context.CurrentBlueprintInstance == null)
            {
                state = NodeState.Failure;
                return state;
            }

            if (context.CurrentBlueprintInstance.IsComplete())
            {
                state = NodeState.Success;
                return state;
            }

            if (context.CurrentBlueprintInstance.GetNextMissingResource(out LifeEngine.World.ResourceType nextType))
            {
                context.CurrentNeededResource = nextType;
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

        public class FindResourceNode : Node
    {
        private HumanContext context;
        private LifeEngine.World.ResourceType? expectedType;

        public FindResourceNode(HumanContext context, LifeEngine.World.ResourceType? expectedType = null)
        {
            this.Name = "Finding Resource";
            this.context = context;
            this.expectedType = expectedType;
        }

        public override string GetDebugText()
        {
            if (context.Brain.HasCarriedResource()) return "Holding item";
            return $"Looking for {expectedType ?? context.CurrentNeededResource}";
        }

        public override NodeState Evaluate()
        {
            LifeEngine.World.ResourceType target = expectedType ?? context.CurrentNeededResource;

            // NEW: If we already have the resource in inventory, we don't need to "find" it.
            // This ensures we proceed to Deliver even if no more sources exist.
            if (context.Brain.GetResourceCount(target) > 0)
            {
                state = NodeState.Success;
                return state;
            }

            if (context.Perception.PerformResourceScan(target, out Transform found))
            {
                context.CurrentResourceTarget = found;
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

            public class CollectResourceNode : Node
    {
        private HumanContext context;
        private int targetAmount;
        private LifeEngine.World.ResourceType? expectedType;
        
        private float pickupStartTime = -1f;
        private const float pickupDuration = 1.0f;
        private float lastReEvaluationTime = 0f;

        public CollectResourceNode(HumanContext context, int targetAmount = 1, LifeEngine.World.ResourceType? expectedType = null)
        {
            this.Name = "Acquiring Resource";
            this.context = context;
            this.targetAmount = targetAmount;
            this.expectedType = expectedType;
        }

        public override string GetDebugText()
        {
            if (context.CurrentResourceTarget == null) return "No Target";
            
            var tree = context.CurrentResourceTarget.GetComponent<LifeEngine.World.FellableTree>();
            if (tree != null)
            {
                float totalRequiredSeconds = tree.harvestDurationMinutes * LifeEngine.World.TimeManager.Instance.realSecondsPerGameMinute;
                float progress = (context.FellingTimer / totalRequiredSeconds) * 100f;
                return $"Breaking {tree.name} ({progress:F0}%)";
            }

            if (pickupStartTime > 0) return $"Picking up... {Mathf.RoundToInt((Time.time - pickupStartTime) / pickupDuration * 100)}%";
            return $"Getting {context.CurrentResourceTarget.name}";
        }

        public override NodeState Evaluate()
        {
            LifeEngine.World.ResourceType targetType = expectedType ?? context.CurrentNeededResource;

            // 1. Success check - only return success if we have the RIGHT resource and enough of it
            if (context.Brain.GetResourceCount(targetType) >= targetAmount)
            {
                pickupStartTime = -1f;
                state = NodeState.Success;
                return state;
            }

            // 2. Scan for a new target if we don't have one, or periodically to find closer items
            if (context.CurrentResourceTarget == null || Time.time - lastReEvaluationTime > 0.5f)
            {
                lastReEvaluationTime = Time.time;
                if (context.Perception.PerformResourceScan(targetType, out Transform found))
                {
                    if (found != context.CurrentResourceTarget)
                    {
                        context.CurrentResourceTarget = found;
                        context.FellingTimer = 0f;
                    }
                }
                else if (context.CurrentResourceTarget == null)
                {
                    // No target, and scan found nothing -> we are done or failed
                    pickupStartTime = -1f;
                    state = NodeState.Failure;
                    return state;
                }
            }

            if (context.CurrentResourceTarget == null)
            {
                state = NodeState.Failure;
                return state;
            }

            float dist = Vector3.Distance(context.Brain.transform.position, context.CurrentResourceTarget.position);

            var treeSource = context.CurrentResourceTarget.GetComponent<LifeEngine.World.FellableTree>();
            float interactRange = treeSource != null ? 1.5f : 1.25f;

            if (dist > interactRange)
            {
                pickupStartTime = -1f;
                context.FellingTimer = 0f;
                if (!context.Locomotion.IsAgentReady())
                {
                    state = NodeState.Running;
                    return state;
                }

                context.Locomotion.SetRun();
                context.Locomotion.SetDestination(context.CurrentResourceTarget.position);
                
                // Visual Debugging
                Debug.DrawLine(context.Brain.transform.position + Vector3.up, context.CurrentResourceTarget.position, Color.yellow);
                
                state = NodeState.Running;
                return state;
            }

            // In range - Branch between Source or Item
            context.Locomotion.Stop();

            if (treeSource != null)
            {
                // A. Source Harvesting
                float totalRequiredSeconds = treeSource.harvestDurationMinutes * LifeEngine.World.TimeManager.Instance.realSecondsPerGameMinute;
                context.FellingTimer += Time.deltaTime;

                if (context.FellingTimer >= totalRequiredSeconds)
                {
                    context.FellingTimer = 0f;
                    treeSource.Fell();
                    context.CurrentResourceTarget = null;
                    
                    state = NodeState.Running;
                    return state;
                }

                state = NodeState.Running;
                return state;
            }
            else
            {
                // B. Item Pickup
                if (pickupStartTime < 0) pickupStartTime = Time.time;

                if (Time.time - pickupStartTime < pickupDuration)
                {
                    state = NodeState.Running;
                    return state;
                }

                var item = context.CurrentResourceTarget.GetComponent<LifeEngine.World.ResourceItem>();
                if (item != null)
                {
                    context.Brain.AddResource(item.type, item.amount);
                }

                Object.Destroy(context.CurrentResourceTarget.gameObject);
                context.CurrentResourceTarget = null;
                pickupStartTime = -1f;
                
                if (context.Brain.GetResourceCount(targetType) >= targetAmount)
                {
                    state = NodeState.Success;
                    return state;
                }

                state = NodeState.Running;
                return state;
            }
        }
    }

            public class DeliverResourceNode : Node
    {
        private HumanContext context;
        private float deliverStartTime = -1f;
        private const float deliverDuration = 1.0f;

        public DeliverResourceNode(HumanContext context)
        {
            this.Name = "Delivering Resource";
            this.context = context;
        }

        public override string GetDebugText()
        {
            if (deliverStartTime > 0) return $"Adding to project... {Mathf.RoundToInt((Time.time - deliverStartTime) / deliverDuration * 100)}%";
            return "At Blueprint";
        }

        public override NodeState Evaluate()
        {
            // Fail if we don't have the specific resource needed for the current blueprint
            if (context.Brain.GetResourceCount(context.CurrentNeededResource) <= 0)
            {
                deliverStartTime = -1f;
                state = NodeState.Failure;
                return state;
            }

            if (context.CurrentBlueprintInstance == null)
            {
                deliverStartTime = -1f;
                state = NodeState.Failure;
                return state;
            }

            float dist = Vector3.Distance(context.Brain.transform.position, context.CurrentBlueprintInstance.transform.position);
            if (dist > 1.5f)
            {
                deliverStartTime = -1f;
                if (!context.Locomotion.IsAgentReady())
                {
                    state = NodeState.Running;
                    return state;
                }

                context.Locomotion.SetRun();
                context.Locomotion.SetDestination(context.CurrentBlueprintInstance.transform.position);
                state = NodeState.Running;
                return state;
            }

            // In range - perform timed delivery
            if (deliverStartTime < 0) deliverStartTime = Time.time;

            if (Time.time - deliverStartTime < deliverDuration)
            {
                state = NodeState.Running;
                return state;
            }

            // Time elapsed - transfer items
            var blueprint = context.CurrentBlueprintInstance;
            if (blueprint != null)
            {
                int amount = context.Brain.GetResourceCount(context.CurrentNeededResource);
                blueprint.AddResource(context.CurrentNeededResource, amount);
                context.Brain.RemoveResource(context.CurrentNeededResource, amount);

                // If the blueprint is now complete (destroyed), clear the context reference
                if (blueprint == null || blueprint.IsComplete()) 
                {
                    context.CurrentBlueprintInstance = null;
                }
            }

            context.Locomotion.Stop();
            deliverStartTime = -1f;
            state = NodeState.Success;
            return state;
        }
    }

    public class CheckNextResourceNeedsTypeNode : Node
    {
        private HumanContext context;
        private LifeEngine.World.ResourceType targetType;

        public CheckNextResourceNeedsTypeNode(HumanContext context, LifeEngine.World.ResourceType targetType)
        {
            this.context = context;
            this.targetType = targetType;
            this.Name = $"Needs {targetType}?";
        }

        public override NodeState Evaluate()
        {
            if (context.CurrentNeededResource == targetType) return NodeState.Success;
            return NodeState.Failure;
        }
    }

    public class FindSpecificResourceNode : Node
    {
        private HumanContext context;
        private LifeEngine.World.ResourceType specificType;

        public FindSpecificResourceNode(HumanContext context, LifeEngine.World.ResourceType specificType)
        {
            this.context = context;
            this.specificType = specificType;
            this.Name = $"Finding {specificType}";
        }

        public override NodeState Evaluate()
        {
            // Efficiency: If we already have the specific source resource in inventory, 
            // the conversion node can proceed.
            if (context.Brain.GetResourceCount(specificType) > 0)
            {
                context.CurrentResourceTarget = null;
                return NodeState.Success;
            }

            if (context.Perception.PerformResourceScan(specificType, out Transform found))
            {
                context.CurrentResourceTarget = found;
                return NodeState.Success;
            }
            return NodeState.Failure;
        }
    }

    public struct ResourceOutput
    {
        public LifeEngine.World.ResourceType type;
        public int count;
        public ResourceOutput(LifeEngine.World.ResourceType type, int count) { this.type = type; this.count = count; }
    }

            public class ConvertResourceNode : Node
    {
        private HumanContext context;
        private LifeEngine.World.ResourceType inputType;
        private int requiredAmount;
        private ResourceOutput[] outputs;
        private float duration;
        private float conversionStartTime = -1f;

        public ConvertResourceNode(HumanContext context, LifeEngine.World.ResourceType inputType, int requiredAmount, float duration, params ResourceOutput[] outputs)
        {
            this.context = context;
            this.inputType = inputType;
            this.requiredAmount = requiredAmount;
            this.duration = duration;
            this.outputs = outputs;
            
            string outputText = "";
            if (outputs != null) foreach(var o in outputs) outputText += $"{o.count}x {o.type} ";
            this.Name = $"Converting {requiredAmount}x {inputType} to {outputText}";
        }

        // Compatibility constructor for single input
        public ConvertResourceNode(HumanContext context, LifeEngine.World.ResourceType inputType, LifeEngine.World.ResourceType outputType, int outputCount, float duration = 1.0f)
            : this(context, inputType, 1, duration, new ResourceOutput(outputType, outputCount))
        {
        }

        public override string GetDebugText()
        {
            if (conversionStartTime > 0) return $"Working... {Mathf.RoundToInt((Time.time - conversionStartTime) / duration * 100)}%";
            return "Starting work";
        }

        public override NodeState Evaluate()
        {
            if (context.Brain.GetResourceCount(inputType) < requiredAmount)
            {
                conversionStartTime = -1f;
                return NodeState.Failure;
            }

            // Perform timed conversion
            if (conversionStartTime < 0) conversionStartTime = Time.time;

            if (Time.time - conversionStartTime < duration)
            {
                return NodeState.Running;
            }

            // Perform conversion logic (drop at feet)
            foreach (var output in outputs)
            {
                GameObject prefab = context.Brain.registry.GetWorldPrefab(output.type);
                if (prefab == null)
                {
                    Debug.LogError($"[ConvertResourceNode] No world prefab found for {output.type} in registry!");
                    continue;
                }

                for (int i = 0; i < output.count; i++)
                {
                    Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0.1f, UnityEngine.Random.Range(-0.2f, 0.2f));
                    Object.Instantiate(prefab, context.Brain.transform.position + offset, Quaternion.identity);
                }
            }

            // Consume inputs
            context.Brain.RemoveResource(inputType, requiredAmount);
            conversionStartTime = -1f;
            
            return NodeState.Success;
        }
    }

    // --- Thermal Behaviors ---

    public class NeedsWarmthNode : Node
    {
        private HumanContext context;

        public NeedsWarmthNode(HumanContext context)
        {
            this.Name = "Needs Warmth";
            this.context = context;
        }

        public override string GetDebugText()
        {
            return $"Perceived: {context.Brain.perceivedTemperature:F1}°C";
        }

        public override NodeState Evaluate()
        {
            // 10% padding logic
            float threshold = context.Brain.comfortRangeMin * 0.9f;

            if (context.Brain.perceivedTemperature < threshold)
            {
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class FindHeatSourceNode : Node
    {
        private HumanContext context;

        public FindHeatSourceNode(HumanContext context)
        {
            this.Name = "Finding Heat Source";
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            if (context.Perception.PerformHeatSourceScan(out List<World.HeatSource> sources))
            {
                float closestDist = float.MaxValue;
                World.HeatSource closest = null;

                foreach (var source in sources)
                {
                    float dist = Vector3.Distance(context.Brain.transform.position, source.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = source;
                    }
                }

                if (closest != null)
                {
                    context.CurrentHeatSourceTarget = closest.transform;
                    state = NodeState.Success;
                    return state;
                }
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class MoveToHeatSourceNode : Node
    {
        private HumanContext context;

        public MoveToHeatSourceNode(HumanContext context)
        {
            this.Name = "Moving to Warmth";
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            if (context.CurrentHeatSourceTarget == null)
            {
                state = NodeState.Failure;
                return state;
            }

            float distance = Vector3.Distance(context.Brain.transform.position, context.CurrentHeatSourceTarget.position);

            // Stand close but not inside the fire
            if (distance <= 2.2f)
            {
                context.Locomotion.Stop();
                state = NodeState.Success;
                return state;
            }

            if (!context.Locomotion.IsAgentReady())
            {
                state = NodeState.Running;
                return state;
            }

            context.Locomotion.SetRun();
            context.Locomotion.SetDestination(context.CurrentHeatSourceTarget.position);

            state = NodeState.Running;
            return state;
        }
    }

    public class FindShadeSpotNode : Node
    {
        private HumanContext context;

        public FindShadeSpotNode(HumanContext context)
        {
            this.Name = "Finding Shade";
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            if (World.DayNightCycle.Instance == null || !World.DayNightCycle.Instance.IsDaylight)
            {
                state = NodeState.Failure;
                return state;
            }

            // Find closest tree to stand under
            LifeEngine.World.FellableTree[] all = Object.FindObjectsByType<LifeEngine.World.FellableTree>(FindObjectsSortMode.None);
            float closestDist = float.MaxValue;
            LifeEngine.World.FellableTree closest = null;

            foreach (var tree in all)
            {
                // Filter out small bushes (must be > 2m tall to provide real shade)
                Collider col = tree.GetComponent<Collider>();
                if (col != null && col.bounds.size.y < 2.0f) continue;

                float dist = Vector3.Distance(context.Brain.transform.position, tree.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = tree;
                }
            }

            if (closest != null)
            {
                Vector3 sunDir = World.DayNightCycle.Instance.SunDirection;
                // The shadow position is away from the sun relative to the tree
                Vector3 shadowDir = sunDir;
                shadowDir.y = 0;
                shadowDir.Normalize();

                // Stand about 2.5m away from the trunk in the direction of the shadow
                context.CurrentShadeTarget = closest.transform.position + shadowDir * 2.5f;
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }

    public class MoveToShadeNode : Node
    {
        private HumanContext context;

        public MoveToShadeNode(HumanContext context)
        {
            this.Name = "Moving to Shade";
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            // If we are already in the shade and comfortable, we are done
            if (context.Brain.isInShade)
            {
                context.Locomotion.Stop();
                state = NodeState.Success;
                return state;
            }

            if (context.CurrentShadeTarget == Vector3.zero)
            {
                state = NodeState.Failure;
                return state;
            }

            if (!context.Locomotion.IsAgentReady())
            {
                state = NodeState.Running;
                return state;
            }

            // Continuously update destination as sun moves
            context.Locomotion.SetWalk();
            context.Locomotion.SetDestination(context.CurrentShadeTarget);

            state = NodeState.Running;
            return state;
        }
    }

    public class IsInComfortableShadeNode : Node
    {
        private HumanContext context;

        public IsInComfortableShadeNode(HumanContext context)
        {
            this.context = context;
        }

        public override NodeState Evaluate()
        {
            // If in shade and not overheating, we're good
            if (context.Brain.isInShade && context.Brain.currentThermalStatus != HumanBrain.ThermalStatus.Hot)
            {
                state = NodeState.Success;
                return state;
            }

            state = NodeState.Failure;
            return state;
        }
    }
}
