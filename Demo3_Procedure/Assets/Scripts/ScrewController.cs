using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ScrewController : MonoBehaviour
{
    [SerializeField] private SocketedPart part;
    [SerializeField] private ThumbstickWinder winder;
    [SerializeField] private Transform movingVisual;
    [SerializeField] private Transform headContactPoint;
    public bool IsTightened => part != null && part.IsSocketed && isSeated;
    public event Action<ScrewController> Tightened;
    private Vector3 initialVisualPosition;
    private Vector3 seatedVisualPosition;
    private bool hasSeat;
    private bool isSeated;

    private void Awake()
    {
        if (movingVisual != null) initialVisualPosition = movingVisual.localPosition;
    }

    private void OnEnable()
    {
        if (winder != null) winder.ProgressChanged += ApplyProgress;
        if (part != null) part.SocketChanged += OnSocketChanged;
    }

    private void OnDisable()
    {
        if (winder != null) winder.ProgressChanged -= ApplyProgress;
        if (part != null) part.SocketChanged -= OnSocketChanged;
    }

    private void OnSocketChanged(bool socketed)
    {
        hasSeat = false;
        isSeated = false;
        if (movingVisual == null || headContactPoint == null) return;
        movingVisual.localPosition = initialVisualPosition;
        if (!socketed) return;

        var socket = part.CurrentSocket;
        var seat = socket.GetComponent<ScrewSeat>();
        if (seat == null)
        {
            Debug.LogError("Screw socket is missing its seating surface.", socket);
            return;
        }

        // Selection events precede the socket's first pose update. Compute
        // travel relative to its intended attachment pose, not the loose screw's
        // current world position. Convert to root-local units to respect scene scale.
        float headHeight = transform.InverseTransformPoint(headContactPoint.position).y;
        float worldUnitsPerLocalUnit = transform.TransformVector(Vector3.up).magnitude;
        float surfaceHeight = Vector3.Dot(seat.ContactPosition - socket.attachTransform.position,
            socket.attachTransform.up) / Mathf.Max(worldUnitsPerLocalUnit, 0.000001f);
        float travel = Mathf.Max(0f, headHeight - surfaceHeight);
        seatedVisualPosition = initialVisualPosition - Vector3.up * travel;
        hasSeat = true;
        ApplyProgress(winder != null ? winder.Progress : 0f);
    }

    private void ApplyProgress(float progress)
    {
        if (!hasSeat || part == null || !part.IsSocketed || movingVisual == null) return;
        movingVisual.localPosition = Vector3.Lerp(initialVisualPosition, seatedVisualPosition, progress);
        // The completed state is the seated pose, not merely a turn counter.
        if (!isSeated && Vector3.Distance(movingVisual.localPosition, seatedVisualPosition) <= 0.000001f)
        {
            isSeated = true;
            Tightened?.Invoke(this);
        }
    }
}
