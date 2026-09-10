using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Demo2.TableTennis
{
    /// <summary>Inspector-wired serving, scoring, and ball lifecycle coordinator.</summary>
    public sealed class VRTableTennisTrainer : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] TrainingBall ballPrefab;
        [SerializeField] Transform ballSpawn;
        [SerializeField] TextMeshPro statusText;

        [Header("Feedback Audio")]
        [SerializeField] AudioSource feedbackAudioSource;
        [SerializeField] AudioClip startBuzzer;
        [SerializeField] AudioClip failBuzzer;
        [SerializeField] AudioClip successDing;

        [Header("Serving")]
        [SerializeField, Min(0f)] float initialDelay = 1.5f;
        [FormerlySerializedAs("delayBetweenBalls")]
        [SerializeField, Min(0f)] float delayBetweenServes = .8f;
        [Tooltip("Time between the start buzzer and the ball appearing.")]
        [SerializeField, Min(0f)] float startDelayAfterBuzzer = .5f;
        [SerializeField] float minimumServeSpeed = 5.7f;
        [SerializeField] float maximumServeSpeed = 6.5f;
        [SerializeField] Vector2 horizontalAimRange = new Vector2(-.14f, .14f);
        [SerializeField] Vector2 sideSpinRange = new Vector2(-18f, 18f);
        [SerializeField] Vector2 topSpinRange = new Vector2(15f, 35f);

        public int Score { get; private set; }
        public int Attempts { get; private set; }

        TrainingBall activeBall;
        bool serving;

        void Awake()
        {
            Physics.defaultSolverIterations = Mathf.Max(Physics.defaultSolverIterations, 12);
            Physics.defaultSolverVelocityIterations = Mathf.Max(Physics.defaultSolverVelocityIterations, 4);
            Time.fixedDeltaTime = 1f / 120f;
        }

        void Start()
        {
            if (!ValidateReferences()) { enabled = false; return; }
            UpdateStatus("GET READY");
            StartCoroutine(ServeLoop());
        }

        bool ValidateReferences()
        {
            bool valid = true;
            if (ballPrefab == null) { Debug.LogError("Assign Ball Prefab on VRTableTennisTrainer.", this); valid = false; }
            if (ballSpawn == null) { Debug.LogError("Assign Ball Spawn on VRTableTennisTrainer.", this); valid = false; }
            return valid;
        }

        IEnumerator ServeLoop()
        {
            bool firstServe = true;
            while (enabled)
            {
                if (activeBall == null && !serving)
                {
                    serving = true;
                    UpdateStatus("NEXT BALL");
                    yield return new WaitForSeconds(firstServe ? initialDelay : delayBetweenServes);
                    PlayFeedback(startBuzzer);
                    yield return new WaitForSeconds(startDelayAfterBuzzer);
                    Serve();
                    firstServe = false;
                    serving = false;
                }
                yield return null;
            }
        }

        void Serve()
        {
            Attempts++;
            activeBall = Instantiate(ballPrefab, ballSpawn.position, ballSpawn.rotation);
            Vector3 localDirection = new Vector3(Random.Range(horizontalAimRange.x, horizontalAimRange.y), 1f, -1f).normalized;
            Vector3 velocity = ballSpawn.TransformDirection(localDirection) * Random.Range(minimumServeSpeed, maximumServeSpeed);
            Vector3 spin = new Vector3(Random.Range(sideSpinRange.x, sideSpinRange.y), Random.Range(topSpinRange.x, topSpinRange.y), 0f);
            activeBall.Launch(this, velocity, spin);
            UpdateStatus("SCORE  " + Score + "\nBALL  " + Attempts);
        }

        public void RegisterScore(TrainingBall ball)
        {
            if (ball != activeBall || !ball.WasHitByPlayer || ball.Scored) return;
            ball.Scored = true;
            Score++;
            PlayFeedback(successDing);
            UpdateStatus("HIT!  +1\nSCORE  " + Score);
        }

        public void Retire(TrainingBall ball)
        {
            if (ball == activeBall && !ball.Scored) PlayFeedback(failBuzzer);
            if (ball == activeBall) activeBall = null;
            Destroy(ball.gameObject);
        }

        void PlayFeedback(AudioClip clip)
        {
            if (feedbackAudioSource != null && clip != null)
                feedbackAudioSource.PlayOneShot(clip);
        }

        void UpdateStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
