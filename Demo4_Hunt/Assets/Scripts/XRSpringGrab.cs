using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Pulls a parent rigidbody toward a grabbable handle through a physics spring.
/// Put this and an XR Simple Interactable on a collider-only child handle. The
/// handle must not have its own rigidbody.
/// </summary>
[DisallowMultipleComponent]
public class XRSpringGrab : MonoBehaviour
{
    [Header("Physics Target")]
    [Tooltip("The hinged body to pull. If empty, the nearest jointed rigidbody in the parent hierarchy is used.")]
    [SerializeField] private Rigidbody targetBody;

    [Header("Spring Feel")]
    [Min(0f)] [SerializeField] private float spring = 300f;
    [Min(0f)] [SerializeField] private float damper = 25f;
    [Min(0f)] [SerializeField] private float slack = 0.02f;
    [Min(0f)] [SerializeField] private float maximumDistance = 0.35f;

    [Header("Safety")]
    [Tooltip("Limits how much force the hand can apply to the object. Use Infinity for no limit.")]
    [Min(0f)] [SerializeField] private float breakForce = 1500f;

    private XRBaseInteractable interactable;
    private Rigidbody anchorBody;
    private Transform handAttach;
    private SpringJoint grabJoint;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null)
        {
            Debug.LogError("XRSpringGrab needs an XR Simple Interactable on the handle.", this);
            enabled = false;
            return;
        }

        if (targetBody == null)
            targetBody = FindPhysicsTarget();

        if (targetBody == null)
        {
            Debug.LogError("XRSpringGrab could not find a parent Rigidbody to pull.", this);
            enabled = false;
            return;
        }

        Rigidbody handleBody = GetComponent<Rigidbody>();
        if (handleBody != null && handleBody != targetBody)
        {
            Debug.LogWarning(
                "Remove the Rigidbody and XR Grab Interactable from this handle, then use an XR Simple Interactable instead.",
                this);
        }
    }

    private void OnEnable()
    {
        if (interactable == null || targetBody == null)
            return;

        interactable.selectEntered.AddListener(OnGrabbed);
        interactable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnGrabbed);
            interactable.selectExited.RemoveListener(OnReleased);
        }

        ReleaseSpring();
    }

    private void FixedUpdate()
    {
        if (grabJoint == null && anchorBody != null)
        {
            ReleaseSpring();
            return;
        }

        if (anchorBody == null || handAttach == null)
            return;

        anchorBody.MovePosition(handAttach.position);
        anchorBody.MoveRotation(handAttach.rotation);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        ReleaseSpring();

        handAttach = args.interactorObject.GetAttachTransform(interactable);
        if (handAttach == null)
            return;

        Transform objectAttach = interactable.GetAttachTransform(args.interactorObject);
        Vector3 grabbedPoint = objectAttach != null ? objectAttach.position : handAttach.position;

        GameObject anchor = new GameObject($"{name} Spring Grab Anchor");
        anchor.hideFlags = HideFlags.HideAndDontSave;
        anchor.transform.SetPositionAndRotation(handAttach.position, handAttach.rotation);

        anchorBody = anchor.AddComponent<Rigidbody>();
        anchorBody.isKinematic = true;
        anchorBody.useGravity = false;
        anchorBody.detectCollisions = false;
        anchorBody.interpolation = RigidbodyInterpolation.Interpolate;

        grabJoint = targetBody.gameObject.AddComponent<SpringJoint>();
        grabJoint.autoConfigureConnectedAnchor = false;
        grabJoint.connectedBody = anchorBody;
        grabJoint.anchor = targetBody.transform.InverseTransformPoint(grabbedPoint);
        grabJoint.connectedAnchor = Vector3.zero;
        grabJoint.spring = spring;
        grabJoint.damper = damper;
        grabJoint.minDistance = slack;
        grabJoint.maxDistance = maximumDistance;
        grabJoint.breakForce = breakForce;
        grabJoint.enableCollision = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        ReleaseSpring();
    }

    private void OnJointBreak(float force)
    {
        // A broken spring should never leave its invisible anchor behind.
        ReleaseSpring();
    }

    private Rigidbody FindPhysicsTarget()
    {
        Rigidbody[] parentBodies = GetComponentsInParent<Rigidbody>(true);

        // Prefer the body that owns the hinge/configurable joint rather than an
        // accidental rigidbody on the handle itself.
        foreach (Rigidbody body in parentBodies)
        {
            if (body.GetComponent<Joint>() != null)
                return body;
        }

        return parentBodies.Length > 0 ? parentBodies[0] : null;
    }

    private void ReleaseSpring()
    {
        handAttach = null;

        if (grabJoint != null)
            Destroy(grabJoint);

        if (anchorBody != null)
            Destroy(anchorBody.gameObject);

        grabJoint = null;
        anchorBody = null;
    }
}
