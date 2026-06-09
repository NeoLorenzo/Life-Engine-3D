using UnityEngine;
using UnityEngine.InputSystem;
using LifeEngine.SimulatedHumans;
using System;

namespace LifeEngine.Core
{
    public class AgentSelector : MonoBehaviour
    {
        public static event Action<HumanBrain> OnAgentSelected;

        private HumanBrain currentlySelected;

        private void Update()
        {
            if (Mouse.current == null) return;

            // Select on left click
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                Ray ray = Camera.main.ScreenPointToRay(mousePosition);
                
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    HumanBrain brain = hit.collider.GetComponentInParent<HumanBrain>();
                    
                    if (brain != null)
                    {
                        SelectAgent(brain);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
                else
                {
                    ClearSelection();
                }
            }
        }

        private void SelectAgent(HumanBrain brain)
        {
            if (currentlySelected == brain) return; // Already selected

            ClearSelection();

            currentlySelected = brain;
            currentlySelected.SetSelected(true);

#if UNITY_EDITOR
            UnityEditor.Selection.activeGameObject = brain.gameObject;
#endif

            OnAgentSelected?.Invoke(currentlySelected);
        }

        private void ClearSelection()
        {
            if (currentlySelected != null)
            {
                currentlySelected.SetSelected(false);
            }
            
            currentlySelected = null;
            
#if UNITY_EDITOR
            UnityEditor.Selection.activeGameObject = null;
#endif

            OnAgentSelected?.Invoke(null);
        }
    }
}
