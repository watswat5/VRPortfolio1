using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class VRDeathRespawn : MonoBehaviour
{
    [Header("XR References")]
    [Tooltip("Root of the XR Origin / XR Rig.")]
    [SerializeField] private Transform xrRigRoot;

    [Tooltip("Tracked headset camera used to preserve room-scale offset while teleporting.")]
    [SerializeField] private Transform trackedHead;

    [Tooltip("Where the player's tracked head should be centered after respawning.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Point inside a completely black death room.")]
    [SerializeField] private Transform deathRoomPoint;


    [Header("Collision")]
    [Tooltip("Tag placed on the car trigger colliders.")]
    [SerializeField] private string carTag = "Car";


    [Header("Black Fade")]
    [Min(0f)] [SerializeField] private float fadeToBlackDuration = 1f;
    [Min(0f)] [SerializeField] private float fadeFromBlackDuration = 1f;


    [Header("Death Message")]
    [Tooltip("Your existing head-tracked message script.")]
    [SerializeField] private HeadTrackedText headTrackingText;

    [Tooltip("How long to stay in the death room before respawning.")]
    [SerializeField] private float deathSequenceTime = 3f;


    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip thudClip;


    private CharacterController characterController;
    private GameObject fadeVolumeObject;
    private Volume fadeVolume;
    private VolumeProfile fadeProfile;
    private ColorAdjustments fadeColor;
    private bool dead;


    private void Awake()
    {
        characterController = xrRigRoot != null
            ? xrRigRoot.GetComponent<CharacterController>()
            : null;
        CreateFadeVolume();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (dead)
            return;

        if (other.CompareTag(carTag))
        {
            Die();
        }
    }


    public void Die()
    {
        if (dead)
            return;

        StartCoroutine(DeathSequence());
    }


    /// <summary>
    /// Fades out, moves the player into the existing black death room, and leaves
    /// a persistent world-space message in front of the tracked headset.
    /// </summary>
    public void EndGame(string message, float delayBeforeFade = 3f)
    {
        if (dead)
            return;

        StartCoroutine(EndGameSequence(message, delayBeforeFade));
    }


    private IEnumerator EndGameSequence(string message, float delayBeforeFade)
    {
        dead = true;

        if (delayBeforeFade > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeFade);

        yield return Fade(0f, 1f, fadeToBlackDuration);

        // Keep one fully black frame between the visible scene and teleport.
        yield return null;

        if (deathRoomPoint != null)
            MoveTrackedHeadTo(deathRoomPoint);

        // Let tracked transforms settle in the black room before placing the text.
        yield return null;
        SetFade(0f);

        //Show the three part message
        if (headTrackingText != null)
            headTrackingText.ShowMessage("You died of starvation in the desert.", null, 3f, 3f);
            while (headTrackingText.IsShowing)
                yield return null;
            headTrackingText.ShowMessage("Nobody noticed", null, 3f, 3f);
            while (headTrackingText.IsShowing)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            headTrackingText.ShowMessage("Thanks for playing!", null, 3f, 3f);
            while (headTrackingText.IsShowing)
                yield return null;
            //Reload scene = cheap reset
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    private IEnumerator DeathSequence()
    {
        dead = true;


        // =========================================================
        // HIT
        // =========================================================

        // Play the thud immediately.
        if (audioSource != null && thudClip != null)
        {
            audioSource.PlayOneShot(thudClip);
        }

        SetFade(1f);

        // Wait one frame so the black screen is actually rendered
        // before moving the XR rig.
        yield return null;


        // =========================================================
        // MOVE TO BLACK DEATH ROOM
        // =========================================================

        if (deathRoomPoint != null)
        {
            MoveTrackedHeadTo(deathRoomPoint);
        }

        // Let XR tracking/transforms settle after teleporting.
        yield return null;


        // =========================================================
        // SHOW DEATH MESSAGE
        // =========================================================

        // We're now physically inside a completely black environment,
        // so the blackout overlay can be removed.
        //
        // This means your world-space/head-tracked text still has
        // proper stereo depth.
        SetFade(0f);

        if (headTrackingText != null)
        {
            headTrackingText.ShowMessage("YOU DIED", Color.red, deathSequenceTime);
            yield return new WaitUntil(() => !headTrackingText.IsShowing);
        }


        // =========================================================
        // BLACK OUT AGAIN
        // =========================================================

        SetFade(1f);

        // Give the headset one full black frame before teleporting.
        yield return null;


        // =========================================================
        // RESPAWN
        // =========================================================

        if (spawnPoint != null)
        {
            MoveTrackedHeadTo(spawnPoint);
        }

        // Let XR tracking update after the move.
        yield return null;


        // =========================================================
        // FADE BACK INTO THE GAME
        // =========================================================

        yield return Fade(1f, 0f, fadeFromBlackDuration);

        dead = false;
    }


    /// <summary>
    /// Moves the XR Rig so that the player's actual tracked head
    /// ends up horizontally centered on the target.
    ///
    /// This compensates for the player's physical room-scale
    /// movement relative to the XR Origin.
    ///
    /// Y is preserved so the player's real-world headset height
    /// remains correct.
    /// </summary>
    private void MoveTrackedHeadTo(Transform target)
    {
        if (xrRigRoot == null || target == null)
            return;


        // Temporarily disable the CharacterController while moving.
        if (characterController != null)
        {
            characterController.enabled = false;
        }


        Vector3 headPosition = trackedHead != null ? trackedHead.position : xrRigRoot.position;
        Vector3 rigPosition = xrRigRoot.position;


        // Determine how far the physical player's head has moved
        // away from the XR Origin.
        Vector3 headOffset = new Vector3(
            headPosition.x - rigPosition.x,
            0f,
            headPosition.z - rigPosition.z
        );


        // Move the rig in the opposite direction so the actual
        // tracked head lands on the target.
        Vector3 newRigPosition = target.position - headOffset;


        // Preserve existing XR rig height.
        //
        // The player's headset height should come from actual
        // tracking rather than being forced to the target's Y.
        newRigPosition.y = xrRigRoot.position.y;


        xrRigRoot.position = newRigPosition;


        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }


    private void CreateFadeVolume()
    {
        fadeVolumeObject = new GameObject("Death Fade Volume");
        fadeVolumeObject.transform.SetParent(transform, false);
        fadeVolumeObject.layer = LayerMask.NameToLayer("Default");
        fadeVolume = fadeVolumeObject.AddComponent<Volume>();
        fadeVolume.isGlobal = true;
        fadeVolume.priority = 200f;

        fadeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        fadeVolume.sharedProfile = fadeProfile;
        fadeColor = fadeProfile.Add<ColorAdjustments>();
        SetFade(0f);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetFade(to);
            yield break;
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }

        SetFade(to);
    }

    private void SetFade(float amount)
    {
        if (fadeColor == null)
            return;

        fadeColor.colorFilter.Override(Color.Lerp(Color.white, Color.black, amount));
        fadeColor.active = amount > 0f;
    }

    private void OnDestroy()
    {
        if (fadeProfile != null)
        {
            foreach (VolumeComponent component in fadeProfile.components)
                Destroy(component);
            Destroy(fadeProfile);
        }

        if (fadeVolumeObject != null)
            Destroy(fadeVolumeObject);
        else if (fadeVolume != null)
            Destroy(fadeVolume);
    }
}
