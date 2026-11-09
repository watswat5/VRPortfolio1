using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class Ladder : MonoBehaviour
{
    [Tooltip("Place this at the floor position where the player should arrive.")]
    public Transform destination;
    [Tooltip("Separate interactable on the climb-down collider object.")]
    [SerializeField] private XRSimpleInteractable climbDownInteractable;
    [SerializeField] private Transform climbDownDestination;
    [SerializeField] private TMP_Text hoverText;
    [SerializeField] private Transform head;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        SetPromptVisible(false);
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnGrabbed);
        if (climbDownInteractable != null && climbDownInteractable != interactable)
            climbDownInteractable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnGrabbed);
        if (climbDownInteractable != null && climbDownInteractable != interactable)
            climbDownInteractable.selectEntered.RemoveListener(OnGrabbed);
        SetPromptVisible(false);
    }

    private void LateUpdate()
    {
        if (hoverText != null && head != null)
        {
            Vector3 position = hoverText.transform.position;
            position.y = head.position.y;
            hoverText.transform.position = position;
        }

        bool hoveringUp = IsHoverTarget(interactable, destination);
        bool hoveringDown = IsHoverTarget(climbDownInteractable, climbDownDestination);
        if (hoverText != null && (hoveringUp || hoveringDown))
            hoverText.text = hoveringUp ? "Climb Up" : "Climb Down";
        SetPromptVisible(hoveringUp || hoveringDown);
    }

    private bool IsHoverTarget(XRSimpleInteractable target, Transform targetDestination)
    {
        return target != null && targetDestination != null && target.isHovered && !target.isSelected &&
            target.interactionManager != null && target.interactionManager.IsHighestPriorityTarget(target);
    }

    private void SetPromptVisible(bool visible)
    {
        if (hoverText != null)
            hoverText.enabled = visible;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Transform targetDestination = ReferenceEquals(args.interactableObject, climbDownInteractable)
            ? climbDownDestination : destination;
        if (targetDestination == null)
            return;

        XRMovement movement = args.interactorObject.transform.GetComponentInParent<XRMovement>();
        if (movement == null)
            movement = FindFirstObjectByType<XRMovement>();

        if (movement != null)
        {
            SetPromptVisible(false);
            movement.TeleportTo(targetDestination.position);
        }
    }
}
