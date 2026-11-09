using System;
using UnityEngine;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Physics-driven paddle button that detects press depth thresholds and emits a pressed event.
    /// Used for in-world mode controls while preserving realistic travel, spring return, and 3D audio.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SpringJoint), typeof(AudioSource))]
    public sealed class PaddleButton : MonoBehaviour
    {
        // Section: Inspector configuration.
        [Header("Travel")]
        [SerializeField, Min(.001f)] float pressDepth = .003f;
        [SerializeField, Min(0f)] float releaseDepth = .001f;

        [Header("Audio")]
        [SerializeField] AudioSource pressAudioSource;
        [SerializeField] AudioClip pressClip;
        [SerializeField, Range(0f, 1f)] float pressVolume = .8f;

        // Section: Public events.
        public event Action<PaddleButton> Pressed;

        // Section: Runtime state.
        Rigidbody body;
        Collider buttonCollider;
        float restY;
        bool isPressed;

        // Section: Lifecycle setup.
        void Awake()
        {
            buttonCollider = GetComponentInChildren<Collider>();
            body = GetComponent<Rigidbody>();
            SpringJoint joint = GetComponent<SpringJoint>();
            if (buttonCollider == null || buttonCollider.isTrigger || body == null || joint == null)
            {
                Debug.LogError(
                    "PaddleButton needs an assigned Rigidbody, SpringJoint, and non-trigger collider.",
                    this);
                enabled = false;
                return;
            }

            restY = body.position.y;
            ConfigureAudio();
        }

        // Section: Press detection loop.
        void FixedUpdate()
        {
            float depression = restY - body.position.y;

            if (!isPressed && depression >= pressDepth)
            {
                isPressed = true;
                if (pressAudioSource != null && pressClip != null)
                    pressAudioSource.PlayOneShot(pressClip, pressVolume);
                Debug.Log(
                    $"Button '{(transform.parent != null ? transform.parent.name : name)}' pressed " +
                    $"at {depression:F3} m depression.",
                    this);
                Pressed?.Invoke(this);
            }
            else if (isPressed && depression <= releaseDepth)
            {
                isPressed = false;
            }
        }

        // Section: Audio helper.
        void ConfigureAudio()
        {
            if (pressAudioSource == null) pressAudioSource = GetComponent<AudioSource>();
            if (pressAudioSource == null) pressAudioSource = gameObject.AddComponent<AudioSource>();

            pressAudioSource.playOnAwake = false;
            pressAudioSource.loop = false;
            pressAudioSource.mute = false;
            pressAudioSource.volume = 1f;
            pressAudioSource.spatialBlend = 1f;
            pressAudioSource.spatialize = true;
            pressAudioSource.minDistance = .25f;
            pressAudioSource.maxDistance = 10f;
            pressAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        // Section: Editor-time guard rails.
        void OnValidate()
        {
            releaseDepth = Mathf.Clamp(releaseDepth, 0f, pressDepth);
        }
    }
}
