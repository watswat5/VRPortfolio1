using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Turns a child visual, never the socketed root. All components are authored in the scene.</summary>
[DisallowMultipleComponent]
public class ThumbstickWinder : MonoBehaviour
{
    [SerializeField] private SocketedPart part;
    [SerializeField] private XRSimpleInteractable handle;
    [SerializeField] private Transform windingVisual;
    [SerializeField] private InputActionProperty leftThumbstick;
    [SerializeField] private InputActionProperty rightThumbstick;
    [SerializeField, Range(0f, 1f)] private float thumbstickDeadzone = 0.6f;
    [SerializeField, Min(1)] private int turnsToComplete = 5;
    [SerializeField] private bool clockwiseToWind = true;
    [SerializeField] private Vector3 localWindingAxis = Vector3.up;
    public float CompletedTurns { get; private set; }
    public bool IsComplete { get; private set; }
    public Transform WindingVisual => windingVisual;
    public IXRSelectInteractor SelectingHand => handle != null && handle.isSelected
        ? handle.interactorsSelecting[0] : null;
    public float Progress => Mathf.Clamp01(CompletedTurns / turnsToComplete);
    public event Action<float> ProgressChanged;
    public event Action Completed;
    private bool leftWasEnabled, rightWasEnabled, hasPreviousAngle;
    private float previousAngle;
    private InteractorHandedness previousHand;
    private Quaternion initialVisualRotation;

    //The audio source and clip for the ratchet sound effect
    public AudioSource ratchetAudioSource;
    public AudioClip ratchetClip;
    //The volume at which the ratchet sound effect will be played
    [SerializeField, Range(0f, 1f)] private float ratchetVolume = 1f;
    //How many degrees of rotation before the clip is played again
    [SerializeField, Range(1f, 360f)] private float ratchetDegrees = 2f;
    private float accumulatedRotation;
    private void Awake()
    {
        if (windingVisual != null) initialVisualRotation = windingVisual.localRotation;
        //If the audio source is null here, check if one is present on this object
        ratchetAudioSource = ratchetAudioSource == null ? GetComponent<AudioSource>() : ratchetAudioSource;
    }
    private void OnEnable()
    {
        leftWasEnabled = leftThumbstick.action != null && leftThumbstick.action.enabled;
        rightWasEnabled = rightThumbstick.action != null && rightThumbstick.action.enabled;
        leftThumbstick.action?.Enable();
        rightThumbstick.action?.Enable();
        if (part != null) part.SocketChanged += OnSocketChanged;
    }
    private void OnDisable()
    {
        if (!leftWasEnabled) leftThumbstick.action?.Disable();
        if (!rightWasEnabled) rightThumbstick.action?.Disable();
        if (part != null) part.SocketChanged -= OnSocketChanged;
        hasPreviousAngle = false;
    }
    private void OnSocketChanged(bool socketed)
    {
        hasPreviousAngle = false;
        if (socketed) return;
        CompletedTurns = 0f;
        IsComplete = false;
        if (windingVisual != null) windingVisual.localRotation = initialVisualRotation;
        ProgressChanged?.Invoke(0f);
    }
    private void Update()
    {
        if (part == null || !part.IsSocketed || handle == null || !handle.isSelected || IsComplete)
        {
            hasPreviousAngle = false;
            return;
        }
        var hand = handle.interactorsSelecting[0].handedness;
        if (hand != previousHand)
        {
            hasPreviousAngle = false;
            previousHand = hand;
        }
        InputAction action = hand switch
        {
            InteractorHandedness.Left => leftThumbstick.action,
            InteractorHandedness.Right => rightThumbstick.action,
            _ => null,
        };
        if (action != null) ProcessStick(action.ReadValue<Vector2>());
    }
    //Rotate and accumulate
    private void ProcessStick(Vector2 stick)
    {
        if (stick.magnitude < thumbstickDeadzone)
        {
            hasPreviousAngle = false;
            return;
        }
        float angle = Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
        if (!hasPreviousAngle)
        {
            previousAngle = angle;
            hasPreviousAngle = true;
            return;
        }
        float delta = Mathf.DeltaAngle(previousAngle, angle);
        previousAngle = angle;
        AddTurns((clockwiseToWind ? -delta : delta) / 360f);
        accumulatedRotation += Mathf.Abs(delta);
        if (ratchetAudioSource != null && ratchetClip != null && accumulatedRotation >= ratchetDegrees)
        {
            ratchetAudioSource.PlayOneShot(ratchetClip, ratchetVolume);
            accumulatedRotation = 0f;
        }
    }
    public void AddTurns(float turns)
    {
        if (part == null || !part.IsSocketed || IsComplete || turns <= 0f) return;
        float applied = Mathf.Min(turns, turnsToComplete - CompletedTurns);
        CompletedTurns += applied;
        if (windingVisual != null)
            windingVisual.Rotate(localWindingAxis.normalized,
                (clockwiseToWind ? -1f : 1f) * applied * 360f, Space.Self);
        ProgressChanged?.Invoke(Progress);
        if (CompletedTurns >= turnsToComplete)
        {
            IsComplete = true;
            Completed?.Invoke();
        }
    }
}
