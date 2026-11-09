using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class PhysicsButton : MonoBehaviour
{
    [SerializeField] private Rigidbody plungerBody;
    [SerializeField] private Vector3 localPressDirection = Vector3.down;
    [SerializeField, Min(0.001f)] private float travel = 0.02f;
    [SerializeField, Min(0f)] private float returnSpring = 800f;
    [SerializeField, Min(0f)] private float returnDamping = 20f;
    [SerializeField, Range(0.01f, 0.99f)] private float pressPoint = 0.8f;
    [SerializeField, Range(0.01f, 0.99f)] private float releasePoint = 0.55f;
    [SerializeField] private bool startInteractable = true;
    [SerializeField] private UnityEvent onPressed = new UnityEvent();
    [SerializeField] private UnityEvent onReleased = new UnityEvent();

    public bool IsInteractable { get; private set; }
    public bool IsPressed { get; private set; }
    public float NormalizedTravel { get; private set; }
    public float LastPressSpeed { get; private set; }
    public event Action Pressed;
    public event Action Released;

    private ConfigurableJoint joint;
    private Rigidbody host;
    private Vector3 restLocal;
    private Vector3 restBodyLocal;
    private Quaternion restRotationLocal;
    private bool waitingForRelease;
    private Vector3 Axis => transform.TransformDirection(localPressDirection.normalized);
    private Vector3 Rest => transform.TransformPoint(restLocal);
    private float Depth => Vector3.Dot(plungerBody.worldCenterOfMass - Rest, Axis) / travel;

    private void Awake()
    {
        if (plungerBody == null || plungerBody.transform == transform)
        {
            Debug.LogError("Assign a separate plunger Rigidbody to PhysicsButton.", this);
            enabled = false;
            return;
        }
        restLocal = transform.InverseTransformPoint(plungerBody.worldCenterOfMass);
        restBodyLocal = transform.InverseTransformPoint(plungerBody.position);
        restRotationLocal = Quaternion.Inverse(transform.rotation) * plungerBody.rotation;
        joint = plungerBody.GetComponent<ConfigurableJoint>();
        if (joint == null) joint = plungerBody.gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = plungerBody.centerOfMass;
        joint.axis = plungerBody.transform.InverseTransformDirection(Axis);
        Vector3 reference = Mathf.Abs(joint.axis.y) < 0.9f ? Vector3.up : Vector3.right;
        joint.secondaryAxis = Vector3.Cross(joint.axis, reference).normalized;
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
        joint.linearLimit = new SoftJointLimit { limit = travel };
        joint.targetPosition = joint.targetVelocity = Vector3.zero;
        joint.xDrive = new JointDrive
        {
            positionSpring = returnSpring,
            positionDamper = returnDamping,
            maximumForce = Mathf.Infinity
        };
        joint.enableCollision = false;
        ConnectHost();
        SetInteractable(startInteractable);
    }

    private void ConnectHost()
    {
        host = GetComponentInParent<Rigidbody>();
        joint.connectedBody = host;
        joint.connectedAnchor = host != null ? host.transform.InverseTransformPoint(Rest) : Rest;
    }

    // Called once after the housing has snapped and its loose Rigidbody is removed.
    public void CompleteMount()
    {
        if (joint == null) return;
        joint.connectedBody = null;
        plungerBody.position = transform.TransformPoint(restBodyLocal);
        plungerBody.rotation = transform.rotation * restRotationLocal;
        ConnectHost();
        plungerBody.linearVelocity = host != null ? host.GetPointVelocity(plungerBody.worldCenterOfMass) : Vector3.zero;
        plungerBody.angularVelocity = host != null ? host.angularVelocity : Vector3.zero;
    }

    private void FixedUpdate()
    {
        // Snapping removes the loose part's Rigidbody; follow the surviving assembly body.
        if (GetComponentInParent<Rigidbody>() != host) ConnectHost();
        NormalizedTravel = Mathf.Clamp01(Depth);
        if (!IsInteractable) return;
        if (waitingForRelease)
        {
            if (NormalizedTravel > releasePoint) return;
            waitingForRelease = false;
        }
        if (!IsPressed && NormalizedTravel >= pressPoint)
        {
            Vector3 mountVelocity = host != null ? host.GetPointVelocity(Rest) : Vector3.zero;
            LastPressSpeed = Mathf.Max(0f, Vector3.Dot(plungerBody.linearVelocity - mountVelocity, Axis));
            IsPressed = true;
            onPressed.Invoke();
            Pressed?.Invoke();
        }
        else if (IsPressed && NormalizedTravel <= releasePoint)
        {
            IsPressed = false;
            onReleased.Invoke();
            Released?.Invoke();
        }
    }

    // Game state gates events only. Physics is never switched off or made kinematic.
    public void SetInteractable(bool interactable)
    {
        if (joint == null || IsInteractable == interactable) return;
        IsInteractable = interactable;
        IsPressed = false;
        LastPressSpeed = 0f;
        waitingForRelease = interactable && Depth > releasePoint;
    }

    private void OnDisable() => SetInteractable(false);

    private void OnValidate()
    {
        travel = Mathf.Max(0.001f, travel);
        releasePoint = Mathf.Min(releasePoint, pressPoint - 0.01f);
        if (localPressDirection.sqrMagnitude < 0.000001f) localPressDirection = Vector3.down;
    }
}
