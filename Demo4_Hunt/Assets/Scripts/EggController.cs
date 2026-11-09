using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EggController : MonoBehaviour
{
    public float breakingForce;
    public DecalProjector breakingDecal;
    public AudioSource breakingSound;
    public GameObject splitEggPrefab;
    public GameObject renderer;
    bool broken = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Disbale the decal for now
        if (breakingDecal != null)
        {
            breakingDecal.enabled = false;
        }
    }

    private IEnumerator playAndWait()
    {
        if (breakingSound != null)
        {
            breakingSound.Play();
            yield return new WaitWhile(() => breakingSound.isPlaying);
        }
        Destroy(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude >= breakingForce)
        {
            broken = true;
            //Play the sound, enable and move the decal to the hit normal, disable the egg visuals, spawn the two egg halves
            if (renderer != null)
            {
                //Disable visuals and collider
                renderer.SetActive(false);
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                }
            }
            if (breakingDecal != null)
            {
                breakingDecal.enabled = true;
                //Move the decal to the hit point and align it with the hit surface's normal, inversed.
                //Also, add a small distance in the normal direction to avoid z-fighting.
                ContactPoint contact = collision.contacts[0];
                breakingDecal.transform.position = contact.point + contact.normal * 0.01f;
                breakingDecal.transform.rotation = Quaternion.LookRotation(-contact.normal);
                breakingDecal.enabled = true;
                breakingDecal.transform.SetParent(null);
            }
            if (breakingSound != null)
            {
                breakingSound.Play();
            }
            if (splitEggPrefab != null)
            {
                GameObject splitEgg = Instantiate(splitEggPrefab, transform.position, transform.rotation);
                //Set the velocity of the prefab's children to that of this object
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    foreach (Rigidbody childRb in splitEgg.GetComponentsInChildren<Rigidbody>())
                    {
                        //Inverse the velocity of the child rigidbody relative to the parent
                        childRb.linearVelocity = -rb.linearVelocity;
                        //assign a random angular velocity
                        childRb.angularVelocity = Random.insideUnitSphere;
                    }
                }
            }

            StartCoroutine(playAndWait());
        }
    }
}
