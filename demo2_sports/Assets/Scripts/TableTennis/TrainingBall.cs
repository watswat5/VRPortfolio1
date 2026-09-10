using UnityEngine;

namespace Demo2.TableTennis
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class TrainingBall : MonoBehaviour
    {
        [Header("Spin")]
        [Tooltip("Maximum Rigidbody angular speed in radians per second.")]
        [SerializeField, Min(0f)] float maximumAngularVelocity = 400f;

        [Header("3D Impact Audio")]
        [SerializeField] AudioSource impactAudioSource;
        [SerializeField] AudioClip tableAndFloorImpactClip;
        [SerializeField] AudioClip paddleImpactClip;
        [SerializeField, Range(0f, 1f)] float impactVolume = 1f;
        [SerializeField, Min(0f)] float impactSoundCooldown = .035f;

        public bool WasHitByPlayer { get; set; }
        public bool Scored { get; set; }
        public TableSide LastTableSide { get; private set; }

        bool enteredTarget;
        bool touchedNet;
        bool enteredOpponentScoreZone;

        Rigidbody body;
        VRTableTennisTrainer trainer;
        float born;
        float lastImpactSoundTime = float.NegativeInfinity;
        // Air density, regulation ball area, and experimentally reasonable coefficients.
        const float AirDensity = 1.225f;
        const float Area = 0.0012566f;
        const float DragCoefficient = .47f;
        const float MagnusCoefficient = .0000042f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.maxAngularVelocity = maximumAngularVelocity;
            if (impactAudioSource != null) impactAudioSource.spatialBlend = 1f;
        }

        public void Launch(VRTableTennisTrainer owningTrainer, Vector3 velocity, Vector3 spinRevolutionsPerSecond)
        {
            trainer = owningTrainer;
            born = Time.time;
            body.linearVelocity = velocity;
            body.angularVelocity = spinRevolutionsPerSecond * (2f * Mathf.PI);
        }

        void FixedUpdate()
        {
            if (body == null) return;
            Vector3 v = body.linearVelocity;
            if (v.sqrMagnitude > .001f)
            {
                Vector3 drag = -.5f * AirDensity * DragCoefficient * Area * v.magnitude * v;
                Vector3 magnus = MagnusCoefficient * Vector3.Cross(body.angularVelocity, v);
                body.AddForce(drag + magnus, ForceMode.Force);
            }
            // Spin decays gradually in air.
            body.angularVelocity *= Mathf.Pow(.992f, Time.fixedDeltaTime / .008333f);
            if (Time.time - born > 7f || transform.position.y < -.35f || transform.position.sqrMagnitude > 100f)
                trainer.Retire(this);
        }

        public void RegisterPaddleHit()
        {
            WasHitByPlayer = true;
            enteredTarget = false;
            touchedNet = false;
            enteredOpponentScoreZone = false;
            LastTableSide = TableSide.None;
        }

        void OnCollisionEnter(Collision collision)
        {
            bool hitPaddle = collision.gameObject.GetComponentInParent<TrackedPaddle>() != null;
            PlayImpact(hitPaddle ? paddleImpactClip : tableAndFloorImpactClip);
        }

        void PlayImpact(AudioClip clip)
        {
            if (impactAudioSource == null || clip == null || Time.time - lastImpactSoundTime < impactSoundCooldown) return;
            lastImpactSoundTime = Time.time;
            impactAudioSource.PlayOneShot(clip, impactVolume);
        }

        public void EnterTargetZone()
        {
            if (!WasHitByPlayer) return;
            enteredTarget = true;
            TryScore();
        }

        public void EnterTableZone(TableSide side)
        {
            if (!WasHitByPlayer) return;
            LastTableSide = side;
            TryScore();
        }

        public void EnterNetZone()
        {
            if (WasHitByPlayer) touchedNet = true;
        }

        public void EnterOpponentScoreZone()
        {
            if (!WasHitByPlayer) return;
            enteredOpponentScoreZone = true;
            TryScore();
        }

        void TryScore()
        {
            if (enteredTarget && LastTableSide == TableSide.Opponent && enteredOpponentScoreZone && !touchedNet)
                trainer.RegisterScore(this);
        }
    }
}
