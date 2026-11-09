using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Toggles and positions a world-space comfort menu in front of the headset.
/// Build the panel as a world-space Canvas, then assign its controls below.
/// </summary>
public sealed class WorldComfortMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRMovement movement;
    [SerializeField] private Transform head;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private InputActionProperty toggleMenuAction;
    [SerializeField] private string fallbackToggleBinding = "<XRController>{RightHand}/secondaryButton";
    [SerializeField] private XRRayInteractor menuRayInteractor;

    [Header("Placement")]
    [SerializeField, Min(0.25f)] private float distance = 1.25f;
    [SerializeField] private float verticalOffset = -0.1f;

    [Header("Controls")]
    [SerializeField] private Toggle teleportToggle;
    [SerializeField] private Toggle smoothMovementToggle;
    [SerializeField] private Toggle snapTurnToggle;
    [SerializeField] private Toggle smoothTurnToggle;
    [SerializeField] private Slider movementSpeedSlider;
    [SerializeField] private TMP_Text movementSpeedLabel;
    [SerializeField] private Slider vignetteSlider;
    [SerializeField] private TMP_Text vignetteLabel;
    [SerializeField] private Toggle seatedToggle;
    [SerializeField] private Toggle standingToggle;
    [SerializeField] private Slider seatedHeightSlider;
    [SerializeField] private TMP_Text seatedHeightLabel;
    [SerializeField] private Button resetViewButton;
    [SerializeField] private Button closeButton;

    private bool listenersBound;
    private InputAction runtimeToggleAction;

    private void Awake()
    {
        if (movement == null)
            movement = FindFirstObjectByType<XRMovement>();
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        if (menuRoot == null && transform.childCount > 0)
            menuRoot = transform.GetChild(0).gameObject;
        if (menuRoot == null || menuRoot == gameObject)
        {
            Debug.LogError("WorldComfortMenu needs a child panel assigned as Menu Root. The script's own GameObject must remain active to detect the menu input.", this);
            enabled = false;
            return;
        }

        InputAction configuredToggleAction = toggleMenuAction.action;
        if ((configuredToggleAction == null || configuredToggleAction.bindings.Count == 0) &&
            !string.IsNullOrWhiteSpace(fallbackToggleBinding))
        {
            runtimeToggleAction = new InputAction(
                "Toggle Comfort Menu",
                InputActionType.Button,
                fallbackToggleBinding);
        }

        BindControls();
        RefreshControls();
        SetMenuVisible(false);
    }

    private void OnEnable()
    {
        toggleMenuAction.action?.Enable();
        runtimeToggleAction?.Enable();
    }

    private void OnDisable()
    {
        toggleMenuAction.action?.Disable();
        runtimeToggleAction?.Disable();
    }

    private void Update()
    {
        if (toggleMenuAction.action?.WasPressedThisFrame() == true ||
            runtimeToggleAction?.WasPressedThisFrame() == true)
            ToggleMenu();
    }

    private void OnDestroy()
    {
        runtimeToggleAction?.Dispose();
    }

    public void ToggleMenu()
    {
        SetMenuVisible(!menuRoot.activeSelf);
    }

    public void SetMenuVisible(bool visible)
    {
        if (visible)
        {
            PlaceInFrontOfPlayer();
            RefreshControls();
        }
        if (menuRayInteractor != null)
            menuRayInteractor.gameObject.SetActive(visible);
        menuRoot.SetActive(visible);
    }

    private void PlaceInFrontOfPlayer()
    {
        if (head == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
        forward.Normalize();

        menuRoot.transform.position = head.position + forward * distance + Vector3.up * verticalOffset;
        menuRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private void BindControls()
    {
        if (listenersBound)
            return;
        listenersBound = true;

        teleportToggle?.onValueChanged.AddListener(value =>
        {
            if (!value) return;
            smoothMovementToggle?.SetIsOnWithoutNotify(false);
            movement?.SetMovementType(XRMovement.MovementType.Teleport);
        });
        smoothMovementToggle?.onValueChanged.AddListener(value =>
        {
            if (!value) return;
            teleportToggle?.SetIsOnWithoutNotify(false);
            movement?.SetMovementType(XRMovement.MovementType.Smooth);
        });
        snapTurnToggle?.onValueChanged.AddListener(value =>
        {
            if (!value) return;
            smoothTurnToggle?.SetIsOnWithoutNotify(false);
            movement?.SetRotationType(XRMovement.RotationType.Snap);
        });
        smoothTurnToggle?.onValueChanged.AddListener(value =>
        {
            if (!value) return;
            snapTurnToggle?.SetIsOnWithoutNotify(false);
            movement?.SetRotationType(XRMovement.RotationType.Smooth);
        });
        movementSpeedSlider?.onValueChanged.AddListener(SetMovementSpeed);
        vignetteSlider?.onValueChanged.AddListener(SetVignetteIntensity);
        seatedHeightSlider?.onValueChanged.AddListener(SetSeatedHeight);
        seatedToggle?.onValueChanged.AddListener(value =>
        {
            if (!value) return;
            standingToggle?.SetIsOnWithoutNotify(false);
            if (movement != null)
                movement.SetSeatedMode(true, movement.StandingEyeHeight);
            PlaceInFrontOfPlayer();
        });
        standingToggle?.onValueChanged.AddListener(value =>
        {
            if (!value) return;
            seatedToggle?.SetIsOnWithoutNotify(false);
            if (movement != null)
                movement.SetSeatedMode(false, movement.StandingEyeHeight);
            PlaceInFrontOfPlayer();
        });
        resetViewButton?.onClick.AddListener(() => movement?.ResetView());
        closeButton?.onClick.AddListener(() => SetMenuVisible(false));
    }

    private void SetMovementSpeed(float value)
    {
        movement?.SetSmoothMovementSpeed(value);
        if (movementSpeedLabel != null)
            movementSpeedLabel.text = $"Movement Speed: {value:0.0} m/s";
    }

    private void SetVignetteIntensity(float value)
    {
        movement?.SetMovementVignetteIntensity(value);
        if (vignetteLabel != null)
            vignetteLabel.text = $"Vignette: {value * 100f:0}%";
    }

    private void SetSeatedHeight(float value)
    {
        if (movement != null)
        {
            movement.SetStandingEyeHeight(value);
            if (movement.IsSeatedMode)
                PlaceInFrontOfPlayer();
        }
        if (seatedHeightLabel != null)
            seatedHeightLabel.text = $"Seated Height: {value:0.00} m";
    }

    private void RefreshControls()
    {
        if (movement == null)
            return;

        teleportToggle?.SetIsOnWithoutNotify(movement.movementType == XRMovement.MovementType.Teleport);
        smoothMovementToggle?.SetIsOnWithoutNotify(movement.movementType == XRMovement.MovementType.Smooth);
        snapTurnToggle?.SetIsOnWithoutNotify(movement.rotationType == XRMovement.RotationType.Snap);
        smoothTurnToggle?.SetIsOnWithoutNotify(movement.rotationType == XRMovement.RotationType.Smooth);
        seatedToggle?.SetIsOnWithoutNotify(movement.IsSeatedMode);
        standingToggle?.SetIsOnWithoutNotify(!movement.IsSeatedMode);

        if (movementSpeedSlider != null)
        {
            movementSpeedSlider.minValue = 0.5f;
            movementSpeedSlider.maxValue = 5f;
            movementSpeedSlider.SetValueWithoutNotify(movement.SmoothMovementSpeed);
        }
        if (vignetteSlider != null)
        {
            vignetteSlider.minValue = 0f;
            vignetteSlider.maxValue = 1f;
            vignetteSlider.SetValueWithoutNotify(movement.MovementVignetteIntensity);
        }
        if (seatedHeightSlider != null)
        {
            seatedHeightSlider.minValue = 1.2f;
            seatedHeightSlider.maxValue = 2.1f;
            seatedHeightSlider.SetValueWithoutNotify(movement.StandingEyeHeight);
        }

        SetMovementSpeed(movement.SmoothMovementSpeed);
        SetVignetteIntensity(movement.MovementVignetteIntensity);
        if (seatedHeightLabel != null)
            seatedHeightLabel.text = $"Seated Height: {movement.StandingEyeHeight:0.00} m";
    }
}
