using UnityEngine;
using System.Collections;

public class AmbianceSoundController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private AudioSource[] soundSources;
    [SerializeField] private AudioClip[] ambianceClips;
    [SerializeField, Min(0f)] private float minimumDistance = 2f;
    [SerializeField, Min(0f)] private float maximumDistance = 10f;
    [SerializeField, Min(0f)] private float minimumDelayBetweenSounds = 2f;
    [SerializeField, Min(0f)] private float maximumDelayBetweenSounds = 6f;
    [SerializeField, Range(0f, 1f)] private float playChance = 0.8f;

    private Coroutine playbackRoutine;

    private void OnEnable()
    {
        playbackRoutine = StartCoroutine(PlayAmbianceSounds());
    }

    private void OnDisable()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }
    }

    private IEnumerator PlayAmbianceSounds()
    {
        while (true)
        {
            float delay = Random.Range(minimumDelayBetweenSounds, maximumDelayBetweenSounds);
            yield return new WaitForSeconds(delay);

            if (soundSources == null || soundSources.Length == 0 ||
                ambianceClips == null || ambianceClips.Length == 0)
            {
                continue;
            }

            if (Random.value > playChance)
            {
                continue;
            }

            AudioSource source = GetRandomIdleSource();
            if (source == null)
            {
                continue;
            }

            AudioClip clip = ambianceClips[Random.Range(0, ambianceClips.Length)];
            if (clip != null)
            {
                MoveSourceAroundPlayer(source);
                source.PlayOneShot(clip);
            }
        }
    }

    private void MoveSourceAroundPlayer(AudioSource source)
    {
        Transform playerTransform = player;
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        if (playerTransform == null)
        {
            return;
        }

        float minDistance = Mathf.Min(minimumDistance, maximumDistance);
        float maxDistance = Mathf.Max(minimumDistance, maximumDistance);
        float distance = Random.Range(minDistance, maxDistance);

        source.transform.position = playerTransform.position + Random.onUnitSphere * distance;
    }

    private AudioSource GetRandomIdleSource()
    {
        AudioSource[] idleSources = new AudioSource[soundSources.Length];
        int idleSourceCount = 0;

        foreach (AudioSource source in soundSources)
        {
            if (source != null && !source.isPlaying)
            {
                idleSources[idleSourceCount] = source;
                idleSourceCount++;
            }
        }

        return idleSourceCount == 0 ? null : idleSources[Random.Range(0, idleSourceCount)];
    }
}
