using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace LifeEngine.UI
{
    public class TimeControlsUI : MonoBehaviour
    {
        [Header("Text Displays")]
        public Text timeText;
        public Text dayText;
        public Text speedText;

        private World.TimeManager tm;

        private void Start()
        {
            tm = World.TimeManager.Instance;

            // Automatically find UI texts anywhere in the hierarchy without requiring manual Inspector dragging
            Text[] allTexts = transform.GetComponentsInChildren<Text>(true);
            foreach (var t in allTexts)
            {
                if (t.gameObject.name == "SpeedText") speedText = t;
                if (t.gameObject.name == "TimeText") timeText = t;
                if (t.gameObject.name == "DayText") dayText = t;
            }
        }

        private void Update()
        {
            if (tm == null) tm = World.TimeManager.Instance;
            if (tm == null) return;

            HandleHotkeys();
            UpdateVisuals();
        }

        private void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Spacebar = Pause/Unpause
            if (kb.spaceKey.wasPressedThisFrame)
            {
                if (Time.timeScale == 0f) 
                {
                    tm.Play(tm.timeScaleMultiplier); // Resume at previous speed
                }
                else 
                {
                    tm.Pause();
                }
            }

            // Enter = Reset to 1x
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                tm.Play(1f);
            }

            // + / = : Multiply speed by 2
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
            {
                float current = Mathf.Max(1f, tm.timeScaleMultiplier); // Ensure we base at 1x if it was somehow lower
                float newSpeed = Mathf.Min(64f, current * 2f); // Cap to 64x max to be safe
                tm.Play(newSpeed);
            }

            // - : Divide speed by 2
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
            {
                float current = tm.timeScaleMultiplier;
                float newSpeed = Mathf.Max(1f, current / 2f); // Min speed stops at 1x
                if (Time.timeScale != 0f) 
                {
                    tm.Play(newSpeed);
                }
            }
        }

        private void UpdateVisuals()
        {
            if (timeText) timeText.text = tm.GetTimeString();
            if (dayText) dayText.text = "Day " + tm.currentDay;

            if (speedText)
            {
                if (Time.timeScale == 0f) speedText.text = "Paused";
                else speedText.text = $"{tm.timeScaleMultiplier}x";
            }
        }
    }
}
