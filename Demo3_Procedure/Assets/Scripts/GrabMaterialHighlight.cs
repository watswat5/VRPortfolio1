using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GrabMaterialHighlight : MonoBehaviour
{
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Material selectableMaterial;

    private Material[][] originalMaterials;
    private XRBaseInteractable interactable;
    private bool isHighlighted;

    private void Awake()
    {
        interactable = GetComponent<XRGrabInteractable>();

        originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalMaterials[i] = renderers[i].sharedMaterials;
        }
    }

    private void OnEnable()
    {
        isHighlighted = false;
    }

    private void OnDisable()
    {
        RestoreOriginalMaterials();
        isHighlighted = false;
    }

    // Socketing retires the loose grab and replaces it with a mounted handle.
    public void SetInteractable(XRBaseInteractable target)
    {
        RestoreOriginalMaterials();
        isHighlighted = false;
        interactable = target;
    }

    private void LateUpdate()
    {
        bool shouldHighlight = IsHighestPriorityTarget();
        if (shouldHighlight == isHighlighted)
            return;

        if (shouldHighlight)
            ShowHighlight();
        else
            RestoreOriginalMaterials();

        isHighlighted = shouldHighlight;
    }

    private bool IsHighestPriorityTarget()
    {
        if (interactable == null || !interactable.isActiveAndEnabled)
            return false;

        foreach (IXRHoverInteractor hoverInteractor in interactable.interactorsHovering)
        {
            if (hoverInteractor is not IXRTargetPriorityInteractor priorityInteractor)
                continue;

            var targets = priorityInteractor.targetsForSelection;
            if (targets != null && targets.Count > 0 && ReferenceEquals(targets[0], interactable))
                return true;
        }

        return false;
    }

    private void ShowHighlight()
    {
        if (selectableMaterial == null)
            return;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
                continue;

            Material[] highlightedMaterials = new Material[targetRenderer.sharedMaterials.Length];
            for (int i = 0; i < highlightedMaterials.Length; i++)
                highlightedMaterials[i] = selectableMaterial;

            targetRenderer.sharedMaterials = highlightedMaterials;
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (originalMaterials == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && originalMaterials[i] != null)
                renderers[i].sharedMaterials = originalMaterials[i];
        }
    }
}
