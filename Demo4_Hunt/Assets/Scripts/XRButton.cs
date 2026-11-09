using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class XRButton : MonoBehaviour
{
    public GameObject button;
    public Vector3 pressDirection;
    public float pressDistance = 0.02f;
    private Vector3 originalPosition;
    public UnityEvent onPress;
    public UnityEvent onRelease;
    GameObject presser;
    public AudioClip pressSound, releasedSoud;
    AudioSource audioSource;
    bool isPressed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        isPressed = false;
        originalPosition = button.transform.localPosition;
        //Set press direction to z by default if not set
        if (pressDirection == Vector3.zero || pressDirection == null)
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
            audioSource.PlayOneShot(pressSound);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == presser && isPressed)
        {
            isPressed = false;
            button.transform.localPosition = originalPosition;
            presser = null;
            audioSource.PlayOneShot(releasedSoud);
            onRelease.Invoke();
        }
    }

}
