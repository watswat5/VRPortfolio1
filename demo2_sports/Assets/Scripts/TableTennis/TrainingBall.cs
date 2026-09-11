using UnityEngine;

namespace Demo2.TableTennis
{
    public enum TrainingBallState { Serving, Playable, Rally, Scored, Faulted, Retired }
    public enum TrainingFoul { None, Opponent, Player }

    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class TrainingBall : MonoBehaviour
    {
        const float ActiveLifetime = 7f;
        const float TerminalLifetime = 8f;
        const float MinimumTerminalLifetime = .15f;

        [Header("Spin")]
        [Tooltip("Maximum Rigidbody angular speed in radians per second.")]
        [SerializeField, Min(0f)] float maximumAngularVelocity = 400f;

        [Header("Surface Bounce")]
        [SerializeField, Range(0f, 1f)] float bounciness = .88f;
        [SerializeField, Range(0f, 1f)] float surfaceFriction = .08f;
        [SerializeField, Min(0f)] float rigidbodySleepThreshold = .0005f;
        [SerializeField, Min(0f)] float globalBounceThreshold = .05f;

        [Header("3D Impact Audio")]
        [SerializeField] AudioSource impactAudioSource;
        [SerializeField] AudioClip tableAndFloorImpactClip;
        [SerializeField] AudioClip paddleImpactClip;
        [SerializeField, Range(0f, 1f)] float impactVolume = 1f;
        [SerializeField, Min(0f)] float impactSoundCooldown = .035f;
        [SerializeField, Min(0f)] float minimumImpactSpeed = .2f;
        [SerializeField, Min(0f)] float maximumImpactSpeed = 8f;
        [SerializeField, Range(0f, 1f)] float minimumImpactVolumeScale = .12f;

        public bool WasHitByPlayer { get; set; }
        public bool Scored { get; set; }
        public TableSide LastTableSide { get; private set; }
        public TrainingBallState State { get; private set; }
        public TrainingFoul Foul { get; private set; }
        public bool CountsTowardServe { get; private set; }

        bool enteredTarget;
        bool enteredOpponentScoreZone;
        bool scorePending;
        bool floorHitPending;
        int tableBouncesAfterPlayerHit;

        Rigidbody body;
        VRTableTennisTrainer trainer;
        PhysicsMaterial runtimeMaterial;
        float born;
        float terminalStateTime = float.NegativeInfinity;
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
            body.sleepThreshold = rigidbodySleepThreshold;
            if (Physics.bounceThreshold > globalBounceThreshold)
                Physics.bounceThreshold = globalBounceThreshold;
            ConfigureSurfaceBounce();
            if (impactAudioSource != null) impactAudioSource.spatialBlend = 1f;
        }

        void ConfigureSurfaceBounce()
        {
            runtimeMaterial = new PhysicsMaterial("TrainingBallRuntime")
            {
                bounciness = bounciness,
                dynamicFriction = surfaceFriction,
                staticFriction = surfaceFriction,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };

            foreach (SphereCollider sphereCollider in GetComponents<SphereCollider>())
                sphereCollider.material = runtimeMaterial;
        }

        public void PrepareServePreview()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
            foreach (Collider ballCollider in GetComponents<Collider>())
                ballCollider.enabled = false;
            enabled = false;
        }

        public void Launch(VRTableTennisTrainer owningTrainer, Vector3 velocity, Vector3 spinRevolutionsPerSecond)
        {
            enabled = true;
            trainer = owningTrainer;
            born = Time.time;
            WasHitByPlayer = false;
            Scored = false;
            CountsTowardServe = false;
            enteredTarget = false;
            enteredOpponentScoreZone = false;
            scorePending = false;
            floorHitPending = false;
            tableBouncesAfterPlayerHit = 0;
            LastTableSide = TableSide.None;
            Foul = TrainingFoul.None;
            State = TrainingBallState.Serving;
            body.isKinematic = false;
            body.detectCollisions = true;
            foreach (Collider ballCollider in GetComponents<Collider>())
                ballCollider.enabled = true;
            body.linearVelocity = velocity;
            body.angularVelocity = spinRevolutionsPerSecond * (2f * Mathf.PI);
            PlayServeSound();
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

            if (IsTerminalState())
            {
                if (CanBeDestroyed())
                    Destroy(gameObject);
                return;
            }

            if (scorePending)
            {
                TryScore();
                if (IsTerminalState()) return;
            }

            if (floorHitPending)
            {
                floorHitPending = false;
                trainer.Retire(this);
                return;
            }

            if (Time.time - born > ActiveLifetime || transform.position.y < -.35f || transform.position.sqrMagnitude > 100f)
                trainer.Retire(this);
        }

        public void RegisterPaddleHit()
        {
            if (State == TrainingBallState.Faulted || State == TrainingBallState.Retired || State == TrainingBallState.Scored) return;

            CommitServeAttempt();
            WasHitByPlayer = true;
            enteredTarget = false;
            enteredOpponentScoreZone = false;
            LastTableSide = TableSide.None;
            tableBouncesAfterPlayerHit = 0;
            State = TrainingBallState.Rally;
            Debug.Log($"Scoring state: player hit registered for '{name}'.", this);
        }

        void OnCollisionEnter(Collision collision)
        {
            bool hitPaddle = collision.gameObject.GetComponentInParent<TrackedPaddle>() != null;
            PlayImpact(hitPaddle ? paddleImpactClip : tableAndFloorImpactClip, collision.relativeVelocity.magnitude);

            if (!hitPaddle && trainer != null && trainer.IsNetCollider(collision.collider))
            {
                HandleNetContact();
                return;
            }

            if (!hitPaddle && trainer != null && trainer.IsFloorCollider(collision.collider))
                HandleFloorCollision();
        }

        void OnTriggerEnter(Collider other)
        {
            if (trainer != null && trainer.IsNetCollider(other))
                HandleNetContact();
        }

        void PlayImpact(AudioClip clip, float impactSpeed)
        {
            if (impactAudioSource == null || clip == null || Time.time - lastImpactSoundTime < impactSoundCooldown) return;
            lastImpactSoundTime = Time.time;

            float speedRange = Mathf.Max(maximumImpactSpeed - minimumImpactSpeed, .0001f);
            float normalizedSpeed = Mathf.Clamp01((impactSpeed - minimumImpactSpeed) / speedRange);
            float scaledVolume = impactVolume * Mathf.Lerp(minimumImpactVolumeScale, 1f, normalizedSpeed);
            impactAudioSource.PlayOneShot(clip, scaledVolume);
        }

        void PlayServeSound()
        {
            if (impactAudioSource == null || paddleImpactClip == null) return;

            impactAudioSource.PlayOneShot(paddleImpactClip, impactVolume);
        }

        public void EnterTargetZone()
        {
            if (!WasHitByPlayer) return;
            enteredTarget = true;
            scorePending = true;
        }

        public void EnterTableZone(TableSide side)
        {
            if (State == TrainingBallState.Serving && side == TableSide.Player)
            {
                CommitServeAttempt();
                State = TrainingBallState.Playable;
                return;
            }

            if (State == TrainingBallState.Serving && side == TableSide.Opponent)
            {
                RegisterFoul(TrainingFoul.Opponent);
                return;
            }

            if (!WasHitByPlayer || IsTerminalState()) return;

            tableBouncesAfterPlayerHit++;
            LastTableSide = side;
            scorePending = true;

            Debug.Log(
                $"Scoring state: table bounce #{tableBouncesAfterPlayerHit} after player hit " +
                $"on {side} side for '{name}'.",
                this);

            // A legal return must land on the opponent's side first. Once it has,
            // a second table bounce means the opponent failed to return the ball.
            if (tableBouncesAfterPlayerHit == 1 && side != TableSide.Opponent)
            {
                RegisterFoul(TrainingFoul.Player);
                return;
            }

            if (tableBouncesAfterPlayerHit >= 2)
            {
                scorePending = false;
                trainer.RegisterScore(this, enteredTarget);
                return;
            }

            TryScore();
        }

        public void EnterNetZone()
        {
            HandleNetContact();
        }

        public void EnterOpponentScoreZone()
        {
            if (!WasHitByPlayer) return;
            enteredOpponentScoreZone = true;
            scorePending = true;
            Debug.Log($"Scoring state: opponent score zone entered by '{name}', last side={LastTableSide}.", this);
            TryScore();
        }

        void TryScore()
        {
            if (WasHitByPlayer && LastTableSide == TableSide.Opponent && enteredOpponentScoreZone)
            {
                scorePending = false;
                trainer.RegisterScore(this, enteredTarget);
            }
        }

        void CommitServeAttempt()
        {
            if (CountsTowardServe) return;

            CountsTowardServe = true;
            trainer.NotifyServeInPlay(this);
        }

        void RegisterFoul(TrainingFoul foul)
        {
            if (State == TrainingBallState.Faulted || State == TrainingBallState.Retired || State == TrainingBallState.Scored) return;

            scorePending = false;
            Foul = foul;
            State = TrainingBallState.Faulted;
            terminalStateTime = Time.time;
            trainer.RegisterFoul(this, foul);
        }

        public void MarkScored()
        {
            Scored = true;
            scorePending = false;
            State = TrainingBallState.Scored;
            terminalStateTime = Time.time;
        }

        public void MarkRetired()
        {
            if (State == TrainingBallState.Scored || State == TrainingBallState.Faulted)
                return;

            scorePending = false;
            State = TrainingBallState.Retired;
            terminalStateTime = Time.time;
        }

        void HandleFloorCollision()
        {
            if (IsTerminalState()) return;

            // Resolve this in the next FixedUpdate so all trigger callbacks from
            // the same physics step can register a completed score first.
            floorHitPending = true;
        }

        void HandleNetContact()
        {
            if (IsTerminalState()) return;

            RegisterFoul(WasHitByPlayer ? TrainingFoul.Player : TrainingFoul.Opponent);
        }

        bool IsTerminalState()
        {
            return State == TrainingBallState.Scored || State == TrainingBallState.Faulted || State == TrainingBallState.Retired;
        }

        bool CanBeDestroyed()
        {
            float terminalAge = Time.time - terminalStateTime;
            if (terminalAge >= TerminalLifetime) return true;
            return terminalAge >= MinimumTerminalLifetime && body.IsSleeping();
        }
    }
}
