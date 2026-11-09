using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Uses XRI to accept a loose part, then permanently joins it to the assembly.</summary>
[DisallowMultipleComponent]
public class SocketedPart : MonoBehaviour, IXRSelectFilter
{
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private XRSimpleInteractable mountedHandle;
    public XRGrabInteractable GrabInteractable => grabInteractable;
    public XRInteractionManager InteractionManager { get; private set; }
    public XRSocketInteractor CurrentSocket { get; private set; }
    public bool IsSocketed => CurrentSocket != null;
    public bool IsMountComplete { get; private set; }
    public Rigidbody AssemblyBody => IsSocketed ? transform.parent.GetComponentInParent<Rigidbody>() : null;
    public bool canProcess => isActiveAndEnabled;
    public event Action<bool> SocketChanged;
    public static event Action PartMounted;
    private bool mounting;

    public static SocketedPart Occupant(XRSocketInteractor socket)
    {
        if (socket == null || socket.attachTransform == null) return null;
        foreach (var part in socket.attachTransform.GetComponentsInChildren<SocketedPart>())
            if (part.CurrentSocket == socket) return part;
        return null;
    }

    public void ReleaseMountedHandle()
    {
        if (mountedHandle != null) mountedHandle.enabled = false;
        mountedHandle = null;
    }

    private void Awake()
    {
        if (grabInteractable == null) grabInteractable = GetComponent<XRGrabInteractable>();
    }
    private void OnEnable()
    {
        if (grabInteractable != null) grabInteractable.selectEntered.AddListener(OnSelected);
    }
    private void OnDisable()
    {
        if (grabInteractable != null) grabInteractable.selectEntered.RemoveListener(OnSelected);
    }
    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (IsSocketed) return false;
        foreach (var selector in interactable.interactorsSelecting)
            if (selector is XRSocketInteractor socket) return ReferenceEquals(interactor, socket);
        return true;
    }
    private void OnSelected(SelectEnterEventArgs args)
    {
        if (!mounting && args.interactorObject is XRSocketInteractor socket)
            StartCoroutine(Mount(socket));
    }

    /// <summary>
    /// Mount this part to the given socket.
    /// This involves disabling the grab interactable, making the rigidbody kinematic,
    /// setting the part's parent to the socket's attach transform, and cleaning up physics.
    /// </summary>
    private IEnumerator Mount(XRSocketInteractor socket)
    {
        mounting = true;
        
        // ===== VALIDATION =====
        // Finish XRI's selection callback before retiring its interaction.
        yield return null;
        if (grabInteractable == null || !grabInteractable.interactorsSelecting.Contains(socket))
        {
            mounting = false;
            yield break;
        }
        
        // ===== GATHER COMPONENTS =====
        InteractionManager = grabInteractable.interactionManager;
        var body = GetComponent<Rigidbody>();
        var highlight = GetComponent<GrabMaterialHighlight>();
        
        // ===== PREPARE FOR MOUNTING =====
        // Update highlight to reference the mounted handle instead of grab interactable
        if (highlight != null) highlight.SetInteractable(mountedHandle);
        
        // Disable grabbing and physics while keeping the object in place
        grabInteractable.throwOnDetach = false;
        grabInteractable.retainTransformParent = false;
        grabInteractable.enabled = false;
        body.isKinematic = true;
        body.detectCollisions = false;
        
        // ===== POSITION AND PARENT THE PART =====
        CurrentSocket = socket;
        transform.SetPositionAndRotation(socket.attachTransform.position, socket.attachTransform.rotation);
        transform.SetParent(socket.attachTransform, true);
        socket.socketActive = false;
        
        // ===== UPDATE INTERACTABLE HIERARCHY =====
        // Handles and downstream sockets now depend on the assembly's surviving grab, not this part's.
        var assemblyGrab = transform.parent.GetComponentInParent<XRGrabInteractable>();
        if (mountedHandle != null) mountedHandle.parentInteractable = assemblyGrab;
        foreach (var childSocket in GetComponentsInChildren<XRSocketInteractor>())
            childSocket.parentInteractable = assemblyGrab;
        
        // ===== DESTROY OLD COMPONENTS =====
        // Remove dependents before the components they require (Unity destroys at frame end).
        yield return null;
        Destroy(grabInteractable);
        yield return null;
        Destroy(body);
        yield return null;
        
        // ===== FINALIZE MOUNT =====
        // Set the final local pose after physics releases its cached world pose.
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        
        // Nested dynamic bodies need the same snap before their joints bind to the assembly.
        foreach (var button in GetComponentsInChildren<PhysicsButton>())
            button.CompleteMount();
        
        // The part's colliders now have no rigidbody; force PhysX to see the new pose immediately.
        Physics.SyncTransforms();
        
        // ===== SIGNAL COMPLETION =====
        IsMountComplete = true;
        if (mountedHandle != null) mountedHandle.enabled = true;
        SocketChanged?.Invoke(true);
        PartMounted?.Invoke();
    }
}
