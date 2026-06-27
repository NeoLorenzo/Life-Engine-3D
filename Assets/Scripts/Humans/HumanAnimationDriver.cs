using UnityEngine;

namespace LifeEngine.SimulatedHumans
{
    [RequireComponent(typeof(Animator))]
    public class HumanAnimationDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private HumanBrain brain;
        [SerializeField] private Rigidbody body;

        [Header("Animator Parameters")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string movingParameter = "IsMoving";
        [SerializeField] private string sleepingParameter = "IsSleeping";
        [SerializeField] private string carryingParameter = "IsCarrying";
        [SerializeField] private string hotParameter = "IsHot";
        [SerializeField] private string coldParameter = "IsCold";

        [Header("Smoothing")]
        [SerializeField] private float speedDampTime = 0.06f;
        [SerializeField] private float stopSpeedThreshold = 0.08f;

        private int speedHash;
        private int movingHash;
        private int sleepingHash;
        private int carryingHash;
        private int hotHash;
        private int coldHash;
        private Vector3 previousPosition;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (brain == null) brain = GetComponentInParent<HumanBrain>();
            if (body == null) body = GetComponentInParent<Rigidbody>();

            speedHash = Animator.StringToHash(speedParameter);
            movingHash = Animator.StringToHash(movingParameter);
            sleepingHash = Animator.StringToHash(sleepingParameter);
            carryingHash = Animator.StringToHash(carryingParameter);
            hotHash = Animator.StringToHash(hotParameter);
            coldHash = Animator.StringToHash(coldParameter);
            previousPosition = transform.position;
        }

        private void Update()
        {
            if (animator == null) return;

            float speed = GetPlanarSpeed();
            if (speed < stopSpeedThreshold)
            {
                speed = 0f;
            }

            animator.SetFloat(speedHash, speed, speedDampTime, Time.deltaTime);
            animator.SetBool(movingHash, speed > 0f);

            if (brain == null) return;

            animator.SetBool(sleepingHash, brain.isSleeping);
            animator.SetBool(carryingHash, brain.HasCarriedResource());
            animator.SetBool(hotHash, brain.currentThermalStatus == HumanBrain.ThermalStatus.Hot);
            animator.SetBool(coldHash, brain.currentThermalStatus == HumanBrain.ThermalStatus.Cold);
        }

        private float GetPlanarSpeed()
        {
            if (body != null)
            {
                return Vector3.ProjectOnPlane(body.linearVelocity, transform.up).magnitude;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return 0f;

            Vector3 currentPosition = transform.position;
            Vector3 movement = currentPosition - previousPosition;
            previousPosition = currentPosition;
            return Vector3.ProjectOnPlane(movement / deltaTime, transform.up).magnitude;
        }
    }
}
