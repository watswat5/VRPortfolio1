using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public sealed class XRButton : MonoBehaviour
{
    public GameObject button;
    public Vector3 pressDirection;
    public float pressDistance = 0.02f;
    private Vector3 originalPosition;
    public UnityEvent onPress;
    public UnityEvent onRelease;
    private GameObject presser;
    public AudioClip pressSound, releasedSoud;
    private AudioSource audioSource;
    private bool isPressed;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (pressSound != null || releasedSoud != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        isPressed = false;
        originalPosition = button.transform.localPosition;

        if (pressDirection == Vector3.zero)
        {
            pressDirection = Vector3.forward;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isPlayer = other.gameObject.CompareTag("Player");
        bool isDirectInteractor = other.GetComponentInParent<XRDirectInteractor>() != null;
        if ((isPlayer || isDirectInteractor) && !isPressed)
        {
            isPressed = true;
            button.transform.localPosition = originalPosition + pressDirection.normalized * pressDistance;
            presser = other.gameObject;
            onPress.Invoke();

            if (audioSource != null && pressSound != null)
            {
                audioSource.PlayOneShot(pressSound);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == presser && isPressed)
        {
            isPressed = false;
            button.transform.localPosition = originalPosition;
            presser = null;

            if (audioSource != null && releasedSoud != null)
            {
                audioSource.PlayOneShot(releasedSoud);
            }

            onRelease.Invoke();
        }
    }
}
