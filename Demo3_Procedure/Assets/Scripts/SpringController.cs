using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class SpringController : MonoBehaviour
{
    [SerializeField] private SocketedPart part;
    [SerializeField] private ThumbstickWinder winder;
    public bool IsSnapped => part != null && part.IsSocketed;
    public bool IsWound => winder != null && winder.IsComplete;
    public event Action<SpringController> WoundCompleted;
    public AudioClip detachClip;
    public AudioSource audioSource;
    public float volume = 1f;
    private void OnEnable() { if (winder != null) winder.Completed += OnCompleted; }
    private void OnDisable() { if (winder != null) winder.Completed -= OnCompleted; }
    private void OnCompleted()
    {
        if (audioSource != null && detachClip != null)
            audioSource.PlayOneShot(detachClip, volume);
        DetachHub();
        WoundCompleted?.Invoke(this);
    }

    private void DetachHub()
    {
        var hub = winder.WindingVisual;
        if (hub == null || hub == transform || !hub.IsChildOf(transform)) return;

        // Capture the winding hand before disabling its stationary interaction.
        var hand = winder.SelectingHand;
        var manager = part.InteractionManager;
        part.ReleaseMountedHandle();
        // Preserve completion after detaching, even if the spring later leaves its socket.
        winder.enabled = false;

        var hubColliders = hub.GetComponentsInChildren<Collider>();
        foreach (var collider in hubColliders)
            collider.isTrigger = false;

        hub.SetParent(null, true);
        var body = hub.GetComponent<Rigidbody>();
        if (body == null) body = hub.gameObject.AddComponent<Rigidbody>();
        body.mass = 0.05f;
        body.isKinematic = false;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // The hub starts touching the spring; avoid a separation impulse into the assembly.
        foreach (var hubCollider in hubColliders)
            foreach (var springCollider in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(hubCollider, springCollider);

        var grab = hub.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = hub.gameObject.AddComponent<XRGrabInteractable>();
        grab.interactionManager = manager;
        grab.interactionLayers = 1; // Hands can pick it up again; assembly sockets cannot.
        grab.useDynamicAttach = false;
        grab.attachTransform = hub;
        grab.attachEaseInTime = 0f;
        grab.retainTransformParent = false;
        if (hand != null && manager != null)
        {
            var attachment = hand.GetAttachTransform(grab);
            body.position = attachment.position;
            body.rotation = attachment.rotation;
            hub.SetPositionAndRotation(attachment.position, attachment.rotation);
            manager.SelectEnter(hand, grab);
        }
    }
}
