using System;
using UnityEngine;

/// <summary>Legacy component retained so existing scenes load without a missing script.</summary>
[Obsolete("Replace this component with PhysicsButton.")]
public sealed class PressurePlateTrigger : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning("PressurePlateTrigger is retired. Configure a non-interactable PhysicsButton on this plate instead.", this);
        enabled = false;
    }
}
