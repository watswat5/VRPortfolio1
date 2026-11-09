using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class MineAssembler : MonoBehaviour
{
    public enum Step { insert_spring, wind_spring, place_cover, screw_cover, place_plate, smack_plate, stamp, done, failed }
    [SerializeField] private XRSocketInteractor springSocket;
    [SerializeField] private XRSocketInteractor coverSocket;
    [SerializeField] private XRSocketInteractor[] screwSockets;
    [SerializeField] private XRSocketInteractor plateSocket;
    [SerializeField] private SpringController springController;
    [SerializeField] private ScrewController[] screws;
    [SerializeField] private UnityEvent onAssemblyCompleted;
    [SerializeField] private UnityEvent onAssemblyFailed;
    [SerializeField] private Step currentStep;
    [SerializeField] private TextMeshPro currentStepText;
    [SerializeField] private TVStageDisplay stageDisplay;
    [Header("Pressure plate (meters per second)")]
    [SerializeField, Min(0f)] private float seatingSpeed = 0.8f;
    [SerializeField, Min(0f)] private float detonationSpeed = 2.5f;
    [SerializeField] private UnityEvent<float> onPlateTooSoft;
    [SerializeField] private UnityEvent<float> onPlateSeated;
    [SerializeField] private UnityEvent<float> onPlateDetonated;
    public Step CurrentStep => currentStep;
    private bool plateSeated, stamped, failed;
    private PhysicsButton pressureButton;
    private bool started;
    private bool displayControlReleased;
    private static MineAssembler displayController;

    private void OnValidate()
    {
        if (detonationSpeed < seatingSpeed)
            detonationSpeed = seatingSpeed;
    }

    private void OnPressurePlatePressed()
    {
        var plate = SocketedPart.Occupant(plateSocket);
        if (currentStep != Step.smack_plate || plateSeated || pressureButton == null ||
            !pressureButton.IsInteractable || plate == null || !plate.IsMountComplete)
            return;

        float speed = pressureButton.LastPressSpeed;
        Debug.Log($"[PLATE_BUTTON] Press speed: {speed:F2} m/s", this);
        if (speed >= detonationSpeed)
        {
            Debug.LogWarning($"[PLATE_BUTTON] Over-speed failure: {speed:F2} m/s", this);
            onPlateDetonated?.Invoke(speed);
            NotifyDetonated();
        }
        else if (speed < seatingSpeed)
        {
            Debug.Log($"[PLATE_BUTTON] Too soft: {speed:F2} m/s", this);
            onPlateTooSoft?.Invoke(speed);
        }
        else
        {
            Debug.Log($"[PLATE_BUTTON] Fired: {speed:F2} m/s", this);
            onPlateSeated?.Invoke(speed);
            NotifyPlateSeated();
        }
    }

    private void OnEnable()
    {
        if (started) RefreshStep();
        //InvokeRepeating(nameof(LogHeartbeat), 0f, 1f);
        SocketedPart.PartMounted += RefreshStep;
        if (springController != null) springController.WoundCompleted += OnSpringWound;
        if (screws != null)
            foreach (var screw in screws)
                if (screw != null) screw.Tightened += OnScrewTightened;
    }
    private void Start()
    {
        started = true;
        ClaimDisplayControl();
        RefreshStep();
    }
    private void OnDisable()
    {
        //CancelInvoke(nameof(LogHeartbeat));
        SocketedPart.PartMounted -= RefreshStep;
        if (springController != null) springController.WoundCompleted -= OnSpringWound;
        if (screws != null)
            foreach (var screw in screws)
                if (screw != null) screw.Tightened -= OnScrewTightened;
        SetPressureButton(null);
        RelinquishDisplayControl();
    }
    private void LogHeartbeat() => Debug.Log($"[ACME_HEARTBEAT] frame={Time.frameCount} uptime={Time.realtimeSinceStartup:F1}s step={currentStep}");
    private void OnSpringWound(SpringController spring) => RefreshStep();
    private void OnScrewTightened(ScrewController screw) => RefreshStep();
    private void SetStep(Step step)
    {
        currentStep = step;
        var plate = SocketedPart.Occupant(plateSocket);
        if (pressureButton != null)
            pressureButton.SetInteractable(isActiveAndEnabled && step == Step.smack_plate &&
                plate != null && plate.IsMountComplete);
        if (displayController != this) ClaimDisplayControl();
        if (displayController != this) return;
        if (currentStepText != null) currentStepText.text = step.ToString();
        if (stageDisplay != null) stageDisplay.SetStage(this, (int)step, step.ToString());
    }
    private void RefreshStep()
    {
        if (failed)
        {
            SetStep(Step.failed);
            return;
        }

        bool springPlaced = SocketedPart.Occupant(springSocket) != null;
        bool wound = springPlaced && springController != null && springController.IsWound;
        bool coverPlaced = wound && SocketedPart.Occupant(coverSocket) != null;
        bool screwsDone = coverPlaced && screwSockets != null && screwSockets.Length == 3 &&
            screwSockets.All(s => SocketedPart.Occupant(s) != null) && screws != null && screws.Length == 3 &&
            screws.All(s => s != null && s.IsTightened);
        bool platePlaced = screwsDone && SocketedPart.Occupant(plateSocket) != null &&
            SocketedPart.Occupant(plateSocket).IsMountComplete;
        var platePart = platePlaced ? SocketedPart.Occupant(plateSocket) : null;
        SetPressureButton(platePart != null ? platePart.GetComponentInChildren<PhysicsButton>(true) : null);
        if (springSocket != null) springSocket.socketActive = !springPlaced;
        // Occupied sockets are retired; mounted children record assembly state.
        if (coverSocket != null) coverSocket.socketActive = wound && SocketedPart.Occupant(coverSocket) == null;
        if (screwSockets != null)
            foreach (var socket in screwSockets)
                if (socket != null) socket.socketActive = coverPlaced && SocketedPart.Occupant(socket) == null;
        if (plateSocket != null) plateSocket.socketActive = screwsDone && SocketedPart.Occupant(plateSocket) == null;
        SetStep(!springPlaced ? Step.insert_spring : !wound ? Step.wind_spring :
            !coverPlaced ? Step.place_cover : !screwsDone ? Step.screw_cover :
            !platePlaced ? Step.place_plate : !plateSeated ? Step.smack_plate :
            !stamped ? Step.stamp : Step.done);
    }

    private void SetPressureButton(PhysicsButton button)
    {
        if (pressureButton == button) return;
        if (pressureButton != null)
        {
            pressureButton.Pressed -= OnPressurePlatePressed;
            pressureButton.SetInteractable(false);
        }
        pressureButton = button;
        if (pressureButton != null)
            pressureButton.Pressed += OnPressurePlatePressed;
    }
    // Hooks for plate impact and stamping, which were placeholders before migration.
    public void NotifyPlateSeated()
    {
        if (currentStep != Step.smack_plate) return;
        plateSeated = true;
        RefreshStep();
    }
    public void NotifyStamped(GameObject stampedObject)
    {
        if (currentStep != Step.stamp || stampedObject == null) return;
        if (stampedObject.GetComponentInParent<MineAssembler>() != this) return;
        stamped = true;
        RefreshStep();
        onAssemblyCompleted?.Invoke();
    }

    public void NotifyDetonated()
    {
        if (currentStep != Step.smack_plate || failed) return;
        failed = true;
        SetStep(Step.failed);
        Debug.LogWarning("[ASSEMBLY_FAILED] Pressure plate detonated.", this);
        onAssemblyFailed?.Invoke();
    }

    public void ResetAssembly()
    {
        failed = false;
        plateSeated = false;
        stamped = false;
        RefreshStep();
    }

    private void ClaimDisplayControl()
    {
        if (displayControlReleased) return;
        if (displayController != null && displayController != this) return;
        if (stageDisplay == null)
            stageDisplay = FindFirstObjectByType<TVStageDisplay>();
        if (stageDisplay == null || !stageDisplay.TryClaim(this)) return;

        currentStepText = stageDisplay.StageText;
        displayController = this;
    }

    public void RelinquishDisplayControl()
    {
        displayControlReleased = true;
        if (displayController != this) return;
        if (stageDisplay != null) stageDisplay.Release(this);
        displayController = null;
        stageDisplay = null;
        currentStepText = null;
    }

    public static void RelinquishCurrentDisplayControl()
    {
        if (displayController != null)
            displayController.RelinquishDisplayControl();
    }
}
