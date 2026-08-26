using UnityEngine;
using UnityEngine.InputSystem;
using LifeEngine.SimulatedHumans;
using System;

namespace LifeEngine.Core
{
    public class AgentSelector : MonoBehaviour
    {
        public static AgentSelector Instance { get; private set; }
        public static event Action<HumanBrain> OnAgentSelected;

        private HumanBrain currentlySelected;
        public HumanBrain CurrentlySelected => currentlySelected;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Prevent raycasting into world when interacting with UI elements
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Camera cam = Camera.main;
                if (cam == null) return;

                Vector2 mousePosition = Mouse.current.position.ReadValue();
                Ray ray = cam.ScreenPointToRay(mousePosition);
                
                // Raycast all to allow selecting humans even when standing underneath foliage/trees
                RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
                if (hits != null && hits.Length > 0)
                {
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                    HumanBrain clickedBrain = null;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var b = hits[i].collider.GetComponentInParent<HumanBrain>();
                        if (b != null)
                        {
                            clickedBrain = b;
                            break;
                        }
                    }

                    if (clickedBrain != null)
                    {
                        SelectAgent(clickedBrain);
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

        public void SelectAgent(HumanBrain brain)
        {
            if (brain == null)
            {
                ClearSelection();
                return;
            }

            if (currentlySelected != brain)
            {
                ClearSelection();
                currentlySelected = brain;
                currentlySelected.SetSelected(true);

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.Selection.activeGameObject = brain.gameObject;
                }
#endif
            }
            else
            {
                currentlySelected.SetSelected(true);
            }

            OnAgentSelected?.Invoke(currentlySelected);
        }

        public void ClearSelection()
        {
            if (currentlySelected != null)
            {
                currentlySelected.SetSelected(false);
            }
            
            currentlySelected = null;
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Selection.activeGameObject = null;
            }
#endif

            OnAgentSelected?.Invoke(null);
        }
    }
}
