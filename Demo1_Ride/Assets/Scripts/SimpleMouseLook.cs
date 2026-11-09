using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleMouseLook : MonoBehaviour
{
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float pitchLimit = 80f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 angles = transform.localEulerAngles;
        yaw = angles.y;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * sensitivity * 0.1f;
        pitch -= mouseDelta.y * sensitivity * 0.1f;
        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
