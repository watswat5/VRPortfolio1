using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class XRMovement : MonoBehaviour
{
    private const string CharacterControllerLayer = "CharacterController";
    private const string MovementTypePreference = "Comfort.MovementType";
    private const string RotationTypePreference = "Comfort.RotationType";
    private const string MovementSpeedPreference = "Comfort.MovementSpeed";
    private const string VignettePreference = "Comfort.Vignette";
    private const string SeatedPreference = "Comfort.Seated";
    private const string StandingEyeHeightPreference = "Comfort.StandingEyeHeight";

    public enum MovementType
    {
        Teleport,
        Smooth
    }
    public enum RotationType
    {
        Snap,
        Smooth
    }
    public InputActionProperty switchMovementType, switchRotationType;
    public HeadTrackedText headTrackedText;
    public MovementType movementType;
    public RotationType rotationType;
    public float smoothMovementSpeed = 2.0f;// Units per second
    public float snapRotationAngle = 15.0f;// Degrees
    public float smoothRotationSpeed = 15.0f; //Degrees per second

    //Teleport variables
    public GameObject teleportMarker;
    public LineRenderer teleportArcRenderer;
    public float teleportArcVelocity = 10f;
    [SerializeField, Min(0.01f)] private float teleportSphereRadius = 0.05f;
    public float teleportTimestep = 0.02f;
    public int maxTeleportSteps = 500;
    [SerializeField] private LayerMask teleportLayers = ~0;
    public GameObject leftController, rightController;
    public InputActionProperty leftTeleportPreview, rightTeleportPreview;
    public InputActionProperty leftTeleportConfirm, rightTeleportConfirm;
    private InputAction ActiveTeleportPreview => rightHanded ? rightTeleportPreview.action : leftTeleportPreview.action;
    private InputAction ActiveTeleportConfirm => rightHanded ? rightTeleportConfirm.action : leftTeleportConfirm.action;
    private GameObject activeController => rightHanded ? rightController : leftController;
    [SerializeField, Range(0f, 1f)] private float inputDeadzone = 0.1f;
    public Transform xrRig;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform head;
    [Tooltip("Tracking-space parent of the headset and controllers. Defaults to the headset's parent.")]
    [SerializeField] private Transform cameraOffset;
    [SerializeField] private float minimumPlayerHeight = 0.5f;
    [SerializeField] private float maximumPlayerHeight = 2.5f;
    [SerializeField, Range(0f, 1f)] private float movementVignetteIntensity = 0.6f;
    [SerializeField] private float vignetteFadeSpeed = 8f;
    [SerializeField, Min(0.01f)] private float teleportFadeDuration = 0.08f;
    [SerializeField, Min(0f)] private float teleportBlackDuration = 0.04f;
    public bool rightHanded = true;

    [SerializeField] private InputActionProperty leftMoveAction;
    [SerializeField] private InputActionProperty rightMoveAction;

    private InputAction ActiveMoveAction => rightHanded ? rightMoveAction.action : leftMoveAction.action;
    private InputAction ActiveTurnAction => rightHanded ? leftMoveAction.action : rightMoveAction.action;
    private Vignette movementVignette;
    private GameObject vignetteVolumeObject;
    private Volume vignetteVolume;
    private VolumeProfile vignetteProfile;
    private ColorAdjustments teleportFade;
    private Coroutine teleportRoutine;
    private bool isTeleporting;
    private float verticalVelocity;
    private float heightCalibrationOffset;
    private bool isSeatedMode;
    private float savedStandingEyeHeight = 1.65f;
    private Vector3 authoredStartPosition;

    public float SmoothMovementSpeed => smoothMovementSpeed;
    public float MovementVignetteIntensity => movementVignetteIntensity;
    public bool IsSeatedMode => isSeatedMode;
    public float StandingEyeHeight => savedStandingEyeHeight;

    private void Awake()
    {
        if (xrRig != null)
            authoredStartPosition = xrRig.position;

        if (characterController == null && xrRig != null)
        {
            characterController = xrRig.GetComponent<CharacterController>();
        }

        if (characterController != null)
        {
            int characterControllerLayer = LayerMask.NameToLayer(CharacterControllerLayer);
            if (characterControllerLayer >= 0)
                characterController.gameObject.layer = characterControllerLayer;
        }

        if (head == null && Camera.main != null)
        {
            head = Camera.main.transform;
        }

        if (cameraOffset == null && head != null)
        {
            cameraOffset = head.parent;
        }

        if (teleportMarker != null)
        {
            teleportMarker.SetActive(false);
        }

        LoadComfortSettings();
        CreateMovementVignette();

        if (isSeatedMode)
            StartCoroutine(ApplySavedHeightCalibration());

        StartCoroutine(CenterTrackedHeadOnAuthoredStart());
    }

    private void OnEnable()
    {
        if (vignetteVolume != null)
            vignetteVolume.enabled = true;
        ActiveMoveAction?.Enable();
        ActiveTurnAction?.Enable();
        switchMovementType.action?.Enable();
        switchRotationType.action?.Enable();
        leftTeleportPreview.action?.Enable();
        rightTeleportPreview.action?.Enable();
        leftTeleportConfirm.action?.Enable();
        rightTeleportConfirm.action?.Enable();
    }

    private void OnDisable()
    {
        if (teleportRoutine != null)
            StopCoroutine(teleportRoutine);
        teleportRoutine = null;
        isTeleporting = false;
        SetTeleportFade(0f);
        if (movementVignette != null)
            movementVignette.intensity.Override(0f);
        if (vignetteVolume != null)
            vignetteVolume.enabled = false;
        ClearTeleportArc();
        ActiveMoveAction?.Disable();
        ActiveTurnAction?.Disable();
        switchMovementType.action?.Disable();
        switchRotationType.action?.Disable();
        leftTeleportPreview.action?.Disable();
        rightTeleportPreview.action?.Disable();
        leftTeleportConfirm.action?.Disable();
        rightTeleportConfirm.action?.Disable();
    }

    void Update()
    {
        if (isTeleporting)
        {
            UpdateMovementVignette(false);
            return;
        }
        //Switch movement and rotation types
        if (switchMovementType.action?.WasPressedThisFrame() == true)
        {
            movementType = movementType == MovementType.Smooth ? MovementType.Teleport : MovementType.Smooth;
            if (headTrackedText != null)
            {
                headTrackedText.ShowMessage(movementType.ToString());
            }
        }

        if (switchRotationType.action?.WasPressedThisFrame() == true)
        {
            rotationType = rotationType == RotationType.Smooth ? RotationType.Snap : RotationType.Smooth;
            if (headTrackedText != null)
            {
                headTrackedText.ShowMessage(rotationType.ToString());
            }
        }

        //Get the joystick input direction and magnitude, use that to scale the movement speed
        Vector2 joystickInput = ActiveMoveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        Vector2 turnInput = ActiveTurnAction?.ReadValue<Vector2>() ?? Vector2.zero;
        bool canMove = xrRig != null && characterController != null && characterController.enabled;
        bool isMoving = canMove && movementType == MovementType.Smooth && HasInput(joystickInput);
        bool isTurning = xrRig != null && rotationType == RotationType.Smooth && HasInput(turnInput.x);
        Vector3 velocity = Vector3.zero;

        UpdateCharacterController(isMoving);

        //Smooth movement that follows the player's gaze
        if (isMoving) {
            Transform movementReference = head != null ? head : xrRig;
            //Flatten the player head's position to X/Z plane
            Vector3 forward = Vector3.ProjectOnPlane(movementReference.forward, Vector3.up);

            //Player is looking almost straight up or down, edge case
            if (forward.sqrMagnitude < Mathf.Epsilon)
            {
                forward = Vector3.ProjectOnPlane(xrRig.forward, Vector3.up);
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 worldMoveDirection = right * joystickInput.x + forward * joystickInput.y;

            velocity = worldMoveDirection * smoothMovementSpeed;
        } else if (movementType == MovementType.Teleport) {
            Teleport();
        } else {
            ClearTeleportArc();
        }

        if (canMove && movementType == MovementType.Smooth)
        {
            verticalVelocity = characterController.isGrounded && verticalVelocity <= 0f
                ? -2f
                : verticalVelocity + Physics.gravity.y * Time.deltaTime;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }
        else
        {
            verticalVelocity = 0f;
        }

        if (xrRig != null && !isTeleporting)
        {
            if (isTurning)
            {
                RotateRig(turnInput.x * smoothRotationSpeed * Time.deltaTime);
            }
            else if (rotationType == RotationType.Snap &&
                     ActiveTurnAction?.WasPressedThisFrame() == true &&
                     HasInput(turnInput.x) &&
                     Mathf.Abs(turnInput.x) > Mathf.Abs(turnInput.y))
            {
                RotateRig(Mathf.Sign(turnInput.x) * snapRotationAngle);
            }
        }

        UpdateMovementVignette(isMoving || isTurning);
    }

    private bool HasInput(Vector2 input)
    {
        return input.sqrMagnitude > inputDeadzone * inputDeadzone;
    }

    private bool HasInput(float input)
    {
        return Mathf.Abs(input) > inputDeadzone;
    }

    private void UpdateCharacterController(bool isLocomoting)
    {
        if (characterController == null || head == null)
        {
            return;
        }

        Vector3 localHeadPosition = characterController.transform.InverseTransformPoint(head.position);
        float playerHeight = Mathf.Clamp(localHeadPosition.y, minimumPlayerHeight, maximumPlayerHeight);

        characterController.height = playerHeight;
        // Keep the capsule over the player's feet while stationary so headset lean does
        // not push the entire body into low geometry. Follow the headset during smooth
        // locomotion so the moving player's collision volume remains accurate.
        characterController.center = new Vector3(
            isLocomoting ? localHeadPosition.x : 0f,
            playerHeight * 0.5f,
            isLocomoting ? localHeadPosition.z : 0f);
    }

    private void RotateRig(float angle)
    {
        //Make sure to rotate around the player's position
        Vector3 rotationPivot = head != null ? head.position : xrRig.position;
        rotationPivot.y = xrRig.position.y;
        xrRig.RotateAround(rotationPivot, Vector3.up, angle);
    }

    public void SetMovementType(MovementType value)
    {
        movementType = value;
        ClearTeleportArc();
        PlayerPrefs.SetInt(MovementTypePreference, (int)value);
    }

    public void SetRotationType(RotationType value)
    {
        rotationType = value;
        PlayerPrefs.SetInt(RotationTypePreference, (int)value);
    }

    public void SetSmoothMovementSpeed(float value)
    {
        smoothMovementSpeed = Mathf.Clamp(value, 0.5f, 5f);
        PlayerPrefs.SetFloat(MovementSpeedPreference, smoothMovementSpeed);
    }

    public void SetMovementVignetteIntensity(float value)
    {
        movementVignetteIntensity = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VignettePreference, movementVignetteIntensity);
    }

    /// <summary>
    /// In seated mode, raises the tracked camera/controllers so the player's current
    /// headset height matches the configured standing eye height. The rig root stays
    /// on the floor so changing modes cannot move the CharacterController underground.
    /// </summary>
    public void SetSeatedMode(bool seated, float standingEyeHeight = 1.65f)
    {
        isSeatedMode = seated;
        savedStandingEyeHeight = Mathf.Clamp(standingEyeHeight, 1.2f, 2.1f);
        PlayerPrefs.SetInt(SeatedPreference, seated ? 1 : 0);
        PlayerPrefs.SetFloat(StandingEyeHeightPreference, savedStandingEyeHeight);
        ApplyHeightCalibration(seated, savedStandingEyeHeight);
    }

    public void SetStandingEyeHeight(float value)
    {
        savedStandingEyeHeight = Mathf.Clamp(value, 1.2f, 2.1f);
        PlayerPrefs.SetFloat(StandingEyeHeightPreference, savedStandingEyeHeight);

        if (isSeatedMode)
            ApplyHeightCalibration(true, savedStandingEyeHeight);
    }

    private void ApplyHeightCalibration(bool seated, float standingEyeHeight)
    {
        if (xrRig == null || head == null || cameraOffset == null)
            return;

        Vector3 offsetPosition = cameraOffset.localPosition;
        offsetPosition.y -= heightCalibrationOffset;
        heightCalibrationOffset = 0f;

        if (seated)
        {
            float trackedHeight = offsetPosition.y + head.localPosition.y;
            heightCalibrationOffset = Mathf.Clamp(standingEyeHeight - trackedHeight, -0.5f, 2.1f);
            offsetPosition.y += heightCalibrationOffset;
        }

        cameraOffset.localPosition = offsetPosition;
        verticalVelocity = 0f;

        UpdateCharacterController(false);
    }

    private IEnumerator ApplySavedHeightCalibration()
    {
        // Give OpenXR one frame to populate the tracked headset pose after a scene load.
        yield return null;
        ApplyHeightCalibration(true, savedStandingEyeHeight);
    }

    private IEnumerator CenterTrackedHeadOnAuthoredStart()
    {
        // OpenXR supplies the headset's room-scale offset shortly after the scene loads.
        // Compensate for it so the player's actual view starts at the position authored
        // for the XR Origin, rather than offset into nearby level geometry.
        yield return null;
        yield return new WaitForEndOfFrame();

        if (xrRig == null || head == null)
            yield break;

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
            characterController.enabled = false;

        Vector3 trackedHeadOffset = head.position - xrRig.position;
        trackedHeadOffset.y = 0f;
        xrRig.position = new Vector3(
            authoredStartPosition.x - trackedHeadOffset.x,
            authoredStartPosition.y,
            authoredStartPosition.z - trackedHeadOffset.z);

        if (controllerWasEnabled)
            characterController.enabled = true;

        verticalVelocity = 0f;
        UpdateCharacterController(false);
    }

    private void LoadComfortSettings()
    {
        movementType = (MovementType)Mathf.Clamp(
            PlayerPrefs.GetInt(MovementTypePreference, (int)movementType),
            (int)MovementType.Teleport,
            (int)MovementType.Smooth);
        rotationType = (RotationType)Mathf.Clamp(
            PlayerPrefs.GetInt(RotationTypePreference, (int)rotationType),
            (int)RotationType.Snap,
            (int)RotationType.Smooth);
        smoothMovementSpeed = Mathf.Clamp(
            PlayerPrefs.GetFloat(MovementSpeedPreference, smoothMovementSpeed), 0.5f, 5f);
        movementVignetteIntensity = Mathf.Clamp01(
            PlayerPrefs.GetFloat(VignettePreference, movementVignetteIntensity));
        isSeatedMode = PlayerPrefs.GetInt(SeatedPreference, 0) == 1;
        savedStandingEyeHeight = Mathf.Clamp(
            PlayerPrefs.GetFloat(StandingEyeHeightPreference, 1.65f), 1.2f, 2.1f);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Recenters the rig under the headset and makes the current horizontal gaze the
    /// rig's new forward direction without moving the player's perceived head position.
    /// </summary>
    public void ResetView()
    {
        if (xrRig == null || head == null)
            return;

        Vector3 headPosition = head.position;
        Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.ProjectOnPlane(xrRig.forward, Vector3.up);

        Quaternion targetRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        Vector3 localHeadOffset = xrRig.InverseTransformPoint(headPosition);
        xrRig.rotation = targetRotation;
        Vector3 rotatedOffset = xrRig.TransformVector(new Vector3(localHeadOffset.x, 0f, localHeadOffset.z));
        xrRig.position = new Vector3(
            headPosition.x - rotatedOffset.x,
            xrRig.position.y,
            headPosition.z - rotatedOffset.z);
        UpdateCharacterController(false);
    }

    private void CreateMovementVignette()
    {
        vignetteVolumeObject = new GameObject("Locomotion Comfort Volume");
        vignetteVolumeObject.transform.SetParent(transform, false);
        vignetteVolumeObject.layer = LayerMask.NameToLayer("Default");
        vignetteVolume = vignetteVolumeObject.AddComponent<Volume>();
        vignetteVolume.isGlobal = true;
        vignetteVolume.priority = 100f;
        vignetteProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignetteVolume.sharedProfile = vignetteProfile;

        movementVignette = vignetteProfile.Add<Vignette>(true);
        movementVignette.color.Override(Color.black);
        movementVignette.smoothness.Override(1f);
        movementVignette.intensity.Override(0f);
        teleportFade = vignetteProfile.Add<ColorAdjustments>();
    }

    private void UpdateMovementVignette(bool isLocomoting)
    {
        if (movementVignette == null)
        {
            return;
        }

        float targetIntensity = isLocomoting ? movementVignetteIntensity : 0f;
        movementVignette.intensity.Override(Mathf.MoveTowards(
            movementVignette.intensity.value,
            targetIntensity,
            Mathf.Max(0f, vignetteFadeSpeed) * Time.unscaledDeltaTime));
    }

    private void Teleport() {
        if (activeController == null || head == null)
        {
            return;
        }

        Vector3? targetPosition = null;
        Vector2 previewInput = ActiveTeleportPreview?.ReadValue<Vector2>() ?? Vector2.zero;
        if (HasInput(previewInput))
        {
            targetPosition = DrawTeleportArc(activeController.transform, previewInput);
        } else {
            ClearTeleportArc();
        }
        if (targetPosition.HasValue && ActiveTeleportConfirm?.WasPressedThisFrame() == true)
        {
            TeleportTo(targetPosition.Value);
        }

    }

    public void TeleportTo(Vector3 destination)
    {
        if (xrRig == null || !isActiveAndEnabled || isTeleporting)
            return;

        isTeleporting = true;
        ClearTeleportArc();
        teleportRoutine = StartCoroutine(TeleportWithFade(destination));
    }

    private IEnumerator TeleportWithFade(Vector3 destination)
    {
        yield return FadeTeleport(0f, 1f);
        // Leave a fully black rendered frame before changing the rig position.
        yield return null;
        MoveRigTo(destination);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, teleportBlackDuration));
        yield return FadeTeleport(1f, 0f);
        isTeleporting = false;
        teleportRoutine = null;
    }

    private void MoveRigTo(Vector3 destination)
    {
        verticalVelocity = 0f;
        // Keep the player's feet at the destination even after room-scale movement.
        Vector3 headOffset = head != null ? head.position - xrRig.position : Vector3.zero;
        bool controllerEnabled = characterController != null && characterController.enabled;
        if (controllerEnabled)
            characterController.enabled = false;

        xrRig.position = destination - new Vector3(headOffset.x, 0f, headOffset.z);

        if (controllerEnabled)
            characterController.enabled = true;
    }

    private IEnumerator FadeTeleport(float from, float to)
    {
        float duration = Mathf.Max(0.01f, teleportFadeDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            SetTeleportFade(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetTeleportFade(to);
    }

    private void SetTeleportFade(float alpha)
    {
        if (teleportFade != null)
        {
            teleportFade.colorFilter.Override(Color.Lerp(Color.white, Color.black, alpha));
            teleportFade.active = alpha > 0f;
        }
    }

    private void OnDestroy()
    {
        if (vignetteProfile != null)
        {
            foreach (VolumeComponent component in vignetteProfile.components)
                Destroy(component);
            Destroy(vignetteProfile);
        }
        if (vignetteVolumeObject != null)
            Destroy(vignetteVolumeObject);
        else if (vignetteVolume != null)
            Destroy(vignetteVolume);
    }

    private void ClearTeleportArc() {
        if (teleportArcRenderer != null)
        {
            teleportArcRenderer.positionCount = 0;
            teleportArcRenderer.enabled = false;
        }
        if (teleportMarker != null)
        {
            teleportMarker.SetActive(false);
        }
    }

    //Handles drawing the arc and returning the hit point.
    private Vector3? DrawTeleportArc(Transform controllerTransform, Vector2 joystickInput)
    {
        if (controllerTransform == null || teleportArcRenderer == null)
            return null;

        //The hand transform points up so we need to rotate
        Vector3 localLaunchDirection =
            Quaternion.Euler(90f, 0f, 0f) * Vector3.forward;

        Vector3 launchDirection =
            controllerTransform.TransformDirection(localLaunchDirection).normalized;

        Vector3 currentPosition = controllerTransform.position;
        Vector3 currentVelocity = launchDirection * teleportArcVelocity * joystickInput.magnitude;
        List<Vector3> visibleArcPoints = new List<Vector3> { currentPosition };

        RaycastHit? hit = null;
        int castLayers = teleportLayers & ~LayerMask.GetMask("Hands", CharacterControllerLayer);

        for (int i = 1; i < maxTeleportSteps; i++)
        {
            Vector3 startPoint = currentPosition;
            currentVelocity += Physics.gravity * teleportTimestep;
            currentPosition += currentVelocity * teleportTimestep;
            Vector3 endPoint = currentPosition;

            Vector3 segment = endPoint - startPoint;
            float segmentLength = segment.magnitude;

            if (segmentLength <= Mathf.Epsilon)
                continue;

            if (Physics.SphereCast(
                startPoint,
                teleportSphereRadius,
                segment.normalized,
                out RaycastHit raycastHit,
                segmentLength,
                castLayers,
                QueryTriggerInteraction.Ignore))
            {
                // End the visible arc exactly on the collision surface.
                visibleArcPoints.Add(raycastHit.point);

                hit = raycastHit;
                break;
            }

            visibleArcPoints.Add(endPoint);
        }

        teleportArcRenderer.positionCount = visibleArcPoints.Count;
        teleportArcRenderer.SetPositions(visibleArcPoints.ToArray());
        teleportArcRenderer.enabled = visibleArcPoints.Count > 1;

        bool validTarget =
            hit.HasValue && hit.Value.collider.CompareTag("Floor");

        if (teleportMarker != null)
        {
            teleportMarker.SetActive(validTarget);

            if (validTarget)
            {
                teleportMarker.transform.position = hit.Value.point;
                teleportMarker.transform.rotation = Quaternion.identity;
            }
        }

        return validTarget
            ? hit.Value.point
            : (Vector3?)null;
    }
}
