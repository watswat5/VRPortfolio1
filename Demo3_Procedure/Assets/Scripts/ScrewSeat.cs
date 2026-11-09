using UnityEngine;

/// <summary>The surface where the underside of a screw head seats.</summary>
[DisallowMultipleComponent]
public class ScrewSeat : MonoBehaviour
{
    [SerializeField] private Transform contactPoint;
    public Vector3 ContactPosition => contactPoint != null ? contactPoint.position : transform.position;
}
