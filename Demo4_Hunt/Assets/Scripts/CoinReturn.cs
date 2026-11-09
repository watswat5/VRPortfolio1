using UnityEngine;
using System.Collections;

public class CoinReturn : MonoBehaviour
{
    public Transform endpoint;
    public GameObject coinPrefab;
    public AnimationCurve movementCurve;
    public float dispenseTime = 0.3f;
    public AudioSource audioSource;
    public int maxCoins = 1; // Maximum number of coins that can be dispensed
    private int currentCoinCount = 0; // Current number of coins dispensed

    public void DispenseCoin()
    {
        if (currentCoinCount >= maxCoins)
        {
            return; // Do not dispense more coins than the maximum allowed
        }

        GameObject coin = Instantiate(coinPrefab, transform.position, Quaternion.identity);
        currentCoinCount++;
        //Disable rigidbody and collider on the coin to prevent physics interactions
        Rigidbody coinRigidbody = coin.GetComponent<Rigidbody>();
        if (coinRigidbody != null)
        {
            coinRigidbody.isKinematic = true;
        }
        Collider coinCollider = coin.GetComponent<Collider>();
        if (coinCollider != null)
        {
            coinCollider.enabled = false;
        }
        //Play sound
        if (audioSource != null)
        {
            audioSource.Play();
        }
        StartCoroutine(MoveCoin(coin));
    }

    private IEnumerator MoveCoin(GameObject coin)
    {
        Vector3 startPosition = coin.transform.position;
        Vector3 endPosition = endpoint.position;
        float elapsedTime = 0f;

        while (elapsedTime < dispenseTime)
        {
            float t = elapsedTime / dispenseTime;
            t = movementCurve.Evaluate(t);
            coin.transform.position = Vector3.Lerp(startPosition, endPosition, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        coin.transform.position = endPosition;
        //Re-enable rigidbody and collider on the coin to allow physics interactions
        Rigidbody coinRigidbody = coin.GetComponent<Rigidbody>();
        if (coinRigidbody != null)
        {
            coinRigidbody.isKinematic = false;      
        }
        Collider coinCollider = coin.GetComponent<Collider>();
        if (coinCollider != null)
        {
            coinCollider.enabled = true;
        }

        
    }
}
