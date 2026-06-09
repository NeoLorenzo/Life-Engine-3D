using UnityEngine;

namespace LifeEngine.World
{
    public class TimeManager : MonoBehaviour
    {
        [Header("Time Settings")]
        [Tooltip("Number of actual real-world seconds to pass for 1 in-game minute to elapse.")]
        public float realSecondsPerGameMinute = 1f;

        [Header("Time State")]
        public int currentDay = 1;
        public float currentTimeHours = 8.0f; // Start at 8 AM
        
        [Header("Play Controls")]
        public float timeScaleMultiplier = 1f;

        // Internal
        private bool isPaused = false;

        public static TimeManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            if (isPaused) return;

            // 1 IRL second = (1 / realSecondsPerGameMinute) game minutes
            float gameHoursToAdd = (Time.unscaledDeltaTime / realSecondsPerGameMinute) / 60f;
            gameHoursToAdd *= timeScaleMultiplier;

            currentTimeHours += gameHoursToAdd;

            if (currentTimeHours >= 24f)
            {
                currentTimeHours -= 24f;
                currentDay++;
            }
        }

        public void Pause()
        {
            isPaused = true;
            Time.timeScale = 0f;
        }

        public void Play(float speedMultiplier)
        {
            isPaused = false;
            timeScaleMultiplier = speedMultiplier;
            Time.timeScale = speedMultiplier; // Affect physics/animations directly 
        }

        // Returns string formatted HH:MM military
        public string GetTimeString()
        {
            int hours = Mathf.FloorToInt(currentTimeHours);
            int minutes = Mathf.FloorToInt((currentTimeHours - hours) * 60f);
            return $"{hours:00}:{minutes:00}";
        }
    }
}
