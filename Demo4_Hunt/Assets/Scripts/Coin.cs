using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Coin : MonoBehaviour
{
    public int denomination; //0 to 3
    
    [SerializeField] private float depositDistance = 0.5f; // How far into machine
    [SerializeField] private float depositSpeed = 2f; // Units per second
    
    private XRGrabInteractable _grabInteractable;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private bool _isDeposited = false;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
    }

    /// <summary>
    /// Called when coin is deposited (released from socket)
    /// Animates the coin moving into the machine along the z-axis before disappearing
    /// </summary>
    public void OnDeposited()
    {
        _isDeposited = true;
        StartCoroutine(DepositAnimation());
    }

    private IEnumerator DepositAnimation()
    {
        // Disable physics and collisions
        if (_rigidbody != null)
            _rigidbody.isKinematic = true;
        if (_collider != null)
            _collider.enabled = false;

        // Animate coin moving into machine along positive z-axis
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.forward * depositDistance;
        float elapsed = 0f;
        float duration = depositDistance / depositSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        // Ensure final position is exact
        transform.position = endPos;

        // Hide the coin
        gameObject.SetActive(false);
    }

    public bool IsDeposited => _isDeposited;
}
