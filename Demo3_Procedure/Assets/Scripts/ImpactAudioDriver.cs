using UnityEngine;


//A simple class to drive an audio source based on a collision impact.
//Handles scaling volume by speed.
//Supports a list of sounds to choose from randomly.
public class ImpactAudioDriver : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] impactClips;
    public float volume = 1.0f; //Maximum volume for the impact sound
    public float minImpactSpeed = 0.1f; //Minimum speed to trigger an impact sound

    //The rolloff curve to use
    public AnimationCurve rolloffCurve;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Here we check collision impacts and play the appropriate sound based on the impact speed.
    private void OnCollisionEnter(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed) return;

        if (impactClips.Length == 0 || audioSource == null) return;

        AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
        float scaledVolume = volume * rolloffCurve.Evaluate(impactSpeed);
        audioSource.PlayOneShot(clip, scaledVolume);
    }
}
