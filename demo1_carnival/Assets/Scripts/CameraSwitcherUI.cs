using UnityEngine;
using UnityEngine.UI;

public class CameraSwitcherUI : MonoBehaviour
{
    [Header("Assign the cameras you want to cycle through")]
    public Camera[] cameras;

    [Header("Optional: button to auto-wire at runtime")]
    public Button switchButton;

    [Header("Optional: text label showing active camera")]
    public Text cameraNameLabel;

    [Tooltip("Which camera should be active first")]
    public int startingCameraIndex = 0;

    private int currentCameraIndex;

    private void Start()
    {
        if (cameras == null || cameras.Length == 0)
        {
            Debug.LogWarning("CameraSwitcherUI has no cameras assigned.");
            return;
        }

        currentCameraIndex = Mathf.Clamp(startingCameraIndex, 0, cameras.Length - 1);
        ApplyActiveCamera();

        if (switchButton != null)
        {
            switchButton.onClick.RemoveListener(NextCamera);
            switchButton.onClick.AddListener(NextCamera);
        }
    }

    public void NextCamera()
    {
        if (cameras == null || cameras.Length == 0)
        {
            return;
        }

        currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
        ApplyActiveCamera();
    }

    public void SetCameraByIndex(int index)
    {
        if (cameras == null || cameras.Length == 0)
        {
            return;
        }

        currentCameraIndex = Mathf.Clamp(index, 0, cameras.Length - 1);
        ApplyActiveCamera();
    }

    private void ApplyActiveCamera()
    {
        string activeCameraName = string.Empty;

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
            {
                continue;
            }

            bool isActive = i == currentCameraIndex;
            cameras[i].enabled = isActive;

            if (isActive)
            {
                activeCameraName = cameras[i].name;
            }

            AudioListener listener = cameras[i].GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = isActive;
            }
        }

        UpdateCameraLabel(activeCameraName);
    }

    private void UpdateCameraLabel(string activeCameraName)
    {
        if (cameraNameLabel == null)
        {
            return;
        }

        cameraNameLabel.text = string.IsNullOrEmpty(activeCameraName)
            ? "Active Camera: None"
            : "Active Camera: " + activeCameraName;
    }
}
