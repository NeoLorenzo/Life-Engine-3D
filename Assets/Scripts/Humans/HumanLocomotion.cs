using UnityEngine;
using UnityEngine.AI;

namespace LifeEngine.SimulatedHumans
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class HumanLocomotion : MonoBehaviour
    {
        [Header("Locomotion Attributes")]
        public float walkSpeed = 1.5f;
        public float runSpeed = 3.5f;
        
        [Header("Stuck Detection")]
        public float stuckDistanceThreshold = 0.3f;
        public float stuckTimeThreshold = 1.0f;

        [Header("Local Avoidance")]
        public float avoidanceRadius = 1.5f;
        public float avoidanceWeight = 2.0f;

        private NavMeshAgent agent;
        private NavMeshPath tempPath;
        private Rigidbody rb;
        private Collider[] nearbyColliders = new Collider[10];

        // Stuck detection state
        private float stuckTimer;
        private float lastDistanceToTarget;
        private int consecutiveStuckCount = 0;
        
        private float rescueActiveTimer = 0f;
        private Vector3 rescueNudgeDir = Vector3.zero;

        // Diagnostic Data
        private Vector3 lastDesiredVelocity;
        private int currentCollisionCount = 0;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            
            // Enforce good avoidance so they steer around each other
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            // 0.3m for navigation simulation, 0.25m for physical collision (Standard human size)
            // This 5cm buffer prevents the agent from "grazing" walls while allowing tight passage.
            agent.radius = 0.3f; 
            
            // Decouple NavMeshAgent completely. We will only use it as an invisible path calculator.
            agent.updatePosition = false;
            agent.updateRotation = false;

            // Setup Physics to prevent strictly phasing through each other
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotation; // We rotate the transform manually
                rb.interpolation = RigidbodyInterpolation.Interpolate; // Smoothing for 8x timescale
            }

            CapsuleCollider col = GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.radius = 0.25f;
                // NEW: Ensure we slide off corners instead of sticking
                PhysicsMaterial pm = new PhysicsMaterial("HumanFrictionless");
                pm.staticFriction = 0f;
                pm.dynamicFriction = 0f;
                pm.frictionCombine = PhysicsMaterialCombine.Minimum;
                col.material = pm;
            }

            tempPath = new NavMeshPath();
            SetWalk(); 
        }

        private void FixedUpdate()
        {
            if (!IsAgentReady() || rb == null) return;

            // 1. Get the exact world position of the next Navigation corner
            Vector3 target = agent.steeringTarget;
            
            // CORORIDOR FLARING: Project target away from walls to prevent tight corner clipping
            if (NavMesh.FindClosestEdge(rb.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
            {
                if (edgeHit.distance < 0.45f)
                {
                    // Push the immediate target away from the wall boundary
                    target += edgeHit.normal * 0.25f;
                }
            }

            Vector3 dir = target - rb.position;
            dir.y = 0;

            // If we are relatively close to the immediate steering target, don't jitter
            if (dir.sqrMagnitude > 0.01f && agent.hasPath)
            {
                Vector3 moveDir = dir.normalized;
                
                // 2. Local Avoidance Repulsion
                Vector3 repulsion = Vector3.zero;
                int hits = Physics.OverlapSphereNonAlloc(rb.position, avoidanceRadius, nearbyColliders);
                for (int i = 0; i < hits; i++)
                {
                    Collider hit = nearbyColliders[i];
                    if (hit.gameObject == gameObject) continue;

                    HumanLocomotion otherHuman = hit.GetComponent<HumanLocomotion>();
                    if (otherHuman != null)
                    {
                        Vector3 pushAway = rb.position - otherHuman.rb.position;
                        pushAway.y = 0; // Keep horizontal
                        float dist = pushAway.magnitude;
                        if (dist > 0.01f && dist < avoidanceRadius)
                        {
                            float strength = 1f - (dist / avoidanceRadius);
                            repulsion += pushAway.normalized * (strength * avoidanceWeight);
                        }
                    }
                }

                // 2b. Predictive Wall Swerve (Bumper Rays)
                // Use Default, Wall, and Tree layers (0, 6, 9)
                int wallMask = (1 << 0) | (1 << 6) | (1 << 9);
                float rayDist = 0.65f;
                Vector3 headPos = rb.position + Vector3.up * 1f;
                
                // Left Bumper
                Vector3 leftDir = Quaternion.Euler(0, -35, 0) * transform.forward;
                if (Physics.Raycast(headPos, leftDir, out RaycastHit leftHit, rayDist, wallMask))
                {
                    repulsion += transform.right * (1.5f * (1f - (leftHit.distance / rayDist)));
                    Debug.DrawRay(headPos, leftDir * leftHit.distance, Color.red);
                }
                
                // Right Bumper
                Vector3 rightDir = Quaternion.Euler(0, 35, 0) * transform.forward;
                if (Physics.Raycast(headPos, rightDir, out RaycastHit rightHit, rayDist, wallMask))
                {
                    repulsion -= transform.right * (1.5f * (1f - (rightHit.distance / rayDist)));
                    Debug.DrawRay(headPos, rightDir * rightHit.distance, Color.red);
                }

                // Blend original move direction with repulsion forces
                if (repulsion.sqrMagnitude > 0)
                {
                    moveDir = (moveDir + repulsion).normalized;
                }
                
                // 3. Realistic smoothed rotation
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                // Reduced from 20 to 8 for more human-like turning weight
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 8f);

                // 4. Move forward using a blended velocity profile
                float alignment = Vector3.Dot(transform.forward, moveDir);
                
                // If in rescue mode, force a high speed multiplier even if not aligned
                float speedMultiplier = (rescueActiveTimer > 0) ? 0.8f : Mathf.Max(0.2f, Mathf.Clamp01(alignment + 0.1f));
                
                Vector3 moveVelocity = Vector3.Lerp(moveDir, transform.forward, 0.7f).normalized;

                // Apply the 'Nudge' force if rescuing
                if (rescueActiveTimer > 0)
                {
                    moveVelocity = (moveVelocity + rescueNudgeDir).normalized;
                    Debug.DrawRay(rb.position + Vector3.up * 1.5f, rescueNudgeDir * 2f, Color.magenta);
                    rescueActiveTimer -= Time.fixedDeltaTime;
                }
                
                // --- INDUSTRY STANDARD: NavMesh Velocity Projection ---
                Vector3 intendedMove = moveVelocity * (agent.speed * speedMultiplier * Time.fixedDeltaTime);
                if (NavMesh.Raycast(rb.position, rb.position + intendedMove, out NavMeshHit wallHit, NavMesh.AllAreas))
                {
                    moveVelocity = Vector3.ProjectOnPlane(moveVelocity, wallHit.normal).normalized;
                    Debug.DrawRay(wallHit.position, wallHit.normal, Color.cyan);
                }

                // OSCILLATION SUPPRESSION & SMOOTHING
                // If velocity is high but we are moving AGAINST our desired direction, dampen it
                if (currentCollisionCount > 0 && Vector3.Dot(rb.linearVelocity, moveVelocity) < -0.1f)
                {
                    rb.linearVelocity *= 0.25f; // Stronger damping for high-speed jitter
                }

                Vector3 vel = moveVelocity * (agent.speed * speedMultiplier);
                vel.y = rb.linearVelocity.y; 
                
                // GLIDE STABILIZATION: Blend velocity to filter high-frequency jitter
                // 30% New, 70% Old - creates a low-pass filter for physics stability
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, vel, 0.3f);

                // DIAGNOSTIC: Store for visualization
                lastDesiredVelocity = vel;

                // Check for stuck state more frequently during movement
                if (CheckIfStuck())
                {
                    PerformRescue();
                }
            }
            else
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                lastDesiredVelocity = Vector3.zero;
            }

            // --- VISUAL DIAGNOSTICS ---
            // White: Desired Velocity
            Debug.DrawRay(rb.position + Vector3.up * 0.5f, lastDesiredVelocity, Color.white);
            // Green: Actual Velocity
            Debug.DrawRay(rb.position + Vector3.up * 0.55f, rb.linearVelocity, Color.green);

            // 5. INDUSTRY STANDARD: Hard Position Clamping
            if (NavMesh.SamplePosition(rb.position, out NavMeshHit navHit, 0.5f, NavMesh.AllAreas))
            {
                float dist = Vector3.Distance(rb.position, navHit.position);
                if (dist > 0.1f)
                {
                    rb.position = navHit.position;
                }
            }

            // 6. Synchronize the invisible NavMeshAgent
            agent.nextPosition = rb.position;
        }

        private void PerformRescue()
        {
            // Apply a strong perpendicular nudge (The 'Manual Nudge' Automation)
            Vector3 perpendicular = Vector3.Cross(transform.forward, Vector3.up).normalized;
            // Alternate direction based on count
            rescueNudgeDir = (consecutiveStuckCount % 2 == 0) ? perpendicular : -perpendicular;
            rescueActiveTimer = 0.5f; // Active for 0.5 seconds
            
            // Hard 'Nudge' Offset: Micro-teleport to break physics solver lock
            rb.MovePosition(rb.position + rescueNudgeDir * 0.05f);

            // Force a path refresh
            if (agent.hasPath)
            {
                agent.SetDestination(agent.destination);
            }
        }

        private void OnCollisionEnter(Collision collision) { currentCollisionCount++; }
        private void OnCollisionExit(Collision collision) { currentCollisionCount--; }

        private void OnCollisionStay(Collision collision)
        {
            // Red Arrows: Show precisely where the physics engine is pushing back
            foreach (ContactPoint contact in collision.contacts)
            {
                Debug.DrawRay(contact.point, contact.normal * 0.5f, Color.red);
            }
        }

        public bool IsAgentReady()
        {
            return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        }

        public void SetWalk()
        {
            if (agent != null) agent.speed = walkSpeed;
        }

        public void SetRun()
        {
            if (agent != null) agent.speed = runSpeed;
        }

        public void Stop()
        {
            if (IsAgentReady())
            {
                agent.ResetPath();
            }
        }

        public bool SetDestination(Vector3 destination)
        {
            if (!IsAgentReady()) return false;
            
            ResetStuckTracking();
            return agent.SetDestination(destination);
        }

        public bool HasReachedDestination(float tolerance = 0.25f)
        {
            if (!IsAgentReady()) return false;
            if (agent.pathPending) return false;
            
            return agent.remainingDistance <= agent.stoppingDistance + tolerance;
        }

        public bool IsPathValid(Vector3 targetPosition, out float pathLength)
        {
            pathLength = 0f;
            if (!IsAgentReady()) return false;

            if (!agent.CalculatePath(targetPosition, tempPath) || tempPath.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            pathLength = CalculatePathLength(tempPath);
            return true;
        }

        private float CalculatePathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
                return 0f;

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length;
        }

        /// <summary>
        /// Checks if the agent has been stuck trying to move to a destination.
        /// Useful during fleeing or navigation where rapid replanning is needed.
        /// </summary>
        /// <summary>
        /// Industry Standard: Progress-Based Stuck Detection
        /// Detects if we are failing to reduce distance to target, even if jittering.
        /// </summary>
        public bool CheckIfStuck()
        {
            if (!agent.hasPath || agent.pathPending || HasReachedDestination())
            {
                ResetStuckTracking();
                return false;
            }

            stuckTimer += Time.fixedDeltaTime;
            
            // Every 0.4 seconds, check if we've actually made any progress toward the goal
            if (stuckTimer >= 0.4f)
            {
                float currentDist = Vector3.Distance(rb.position, agent.destination);
                float progress = lastDistanceToTarget - currentDist;
                
                stuckTimer = 0f;
                lastDistanceToTarget = currentDist;
                
                // If we haven't gotten closer, AND we aren't physically moving fast
                // (This prevents false positives while sliding/flaring along walls)
                if (progress < 0.05f && rb.linearVelocity.magnitude < 0.2f)
                {
                    consecutiveStuckCount++;
                    return true;
                }
                
                consecutiveStuckCount = 0;
            }

            return false;
        }

        public int GetConsecutiveStuckCount() => consecutiveStuckCount;
        
        public void ClearStuckCount() => consecutiveStuckCount = 0;

        private void ResetStuckTracking()
        {
            stuckTimer = 0f;
            lastDistanceToTarget = (agent != null && agent.hasPath) ? Vector3.Distance(rb.position, agent.destination) : float.MaxValue;
            consecutiveStuckCount = 0;
        }


    }
}
