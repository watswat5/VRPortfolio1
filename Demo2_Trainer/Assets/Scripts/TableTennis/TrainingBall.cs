using UnityEngine;

namespace Demo2.TableTennis
{
    public enum TrainingBallState
    {
        Serving,
        AwaitingPlayerHit,
        TravelingToOpponent,
        AwaitingOpponentHit,
        TravelingToPlayer,
        Scored,
        Faulted,
        Retired
    }

    public enum TrainingFoul { None, Opponent, Player }

    /// <summary>
    /// Simulates one rally ball through serve, bounce, paddle, score, foul, and retirement states.
    /// It applies aerodynamics, routes physics contacts into game rules, and reports outcomes to the trainer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class TrainingBall : MonoBehaviour
    {
        /*
         * Rally state flow
         *
         * Serving --bounce Player--> AwaitingPlayerHit --Player hit--> TravelingToOpponent
         *      |                                                         |
         *      +--bad bounce/net--> Faulted              bounce Opponent |
         *                                                                v
         * TravelingToPlayer <--Opponent hit-- AwaitingOpponentHit <-------+
         *          |
         *          +--bounce Player--> AwaitingPlayerHit
         *
         * Awaiting* --second bounce/floor--> point to last hitter --> Scored
         * Traveling* --wrong bounce/net----> fault by last hitter --> Faulted
         * Any live state --cleanup/timeout-------------------------> Retired
         */
        // Section: Lifetime and timing constants.
        const float ActiveLifetime = 7f;
        const float TerminalLifetime = 8f;
        const float MinimumTerminalLifetime = .15f;
        const float TableBounceDebounce = .06f;

        // Section: Inspector configuration.
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

        // Section: Public state and status.
        public bool WasHitByPlayer => LastHitter == TableSide.Player;
        public bool Scored { get; set; }
        public TableSide LastHitter { get; private set; }
        public TableSide LastTableSide { get; private set; }
        public TrainingBallState State { get; private set; }
        public TrainingFoul Foul { get; private set; }
        public bool CountsTowardServe { get; private set; }
        public bool EarnedTargetBonus => enteredTarget;

        // Section: Runtime dependencies and transient state.
        bool enteredTarget;
        bool floorHitPending;

        Rigidbody body;
        VRTableTennisTrainer trainer;
        TableCourt court;
        PhysicsMaterial runtimeMaterial;
        float born;
        float lastTableBounceTime = float.NegativeInfinity;
        float terminalStateTime = float.NegativeInfinity;
        float lastImpactSoundTime = float.NegativeInfinity;
        // Air density, regulation ball area, and experimentally reasonable coefficients.
        const float AirDensity = 1.225f;
        const float Area = 0.0012566f;
        const float DragCoefficient = .47f;
        const float MagnusCoefficient = .0000042f;

        // Section: Lifecycle setup.
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

        // Section: Physics material setup.
        // Give every spawned ball a consistent bounce response without editing the prefab asset.
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

        // Section: Serve preparation and launch.
        // Disable physics while the trainer is drawing the serve preview.
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

        // Reset state, re-enable collisions, and launch the serve.
        public void Launch(VRTableTennisTrainer owningTrainer, Vector3 velocity, Vector3 spinRevolutionsPerSecond)
        {
            enabled = true;
            trainer = owningTrainer;
            court = owningTrainer != null ? owningTrainer.Court : null;
            born = Time.time;
            Scored = false;
            CountsTowardServe = false;
            enteredTarget = false;
            floorHitPending = false;
            lastTableBounceTime = float.NegativeInfinity;
            LastHitter = TableSide.Opponent;
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

        // Section: Per-step simulation and cleanup.
        // Apply drag, magnus lift, and terminal-state cleanup once per physics step.
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

            if (floorHitPending)
            {
                floorHitPending = false;
                ResolveUnreturnedBall();
                return;
            }

            if (Time.time - born > ActiveLifetime || transform.position.y < -.35f || transform.position.sqrMagnitude > 100f)
                ResolveUnreturnedBall();
        }

        // Section: Public rally actions.
        public bool CanBeHitBy(TableSide hitter)
        {
            return (hitter == TableSide.Player && State == TrainingBallState.AwaitingPlayerHit)
                || (hitter == TableSide.Opponent && State == TrainingBallState.AwaitingOpponentHit);
        }

        // Shared by tracked and AI paddles. A legal hit hands the rally to the other side.
        public bool RegisterPaddleHit(TableSide hitter)
        {
            if (!CanBeHitBy(hitter))
            {
                Debug.Log($"Ignored illegal {hitter} hit while ball state is {State}.", this);
                return false;
            }

            if (hitter == TableSide.Player) CommitServeAttempt();
            born = Time.time;
            LastHitter = hitter;
            enteredTarget = false;
            LastTableSide = TableSide.None;
            State = hitter == TableSide.Player
                ? TrainingBallState.TravelingToOpponent
                : TrainingBallState.TravelingToPlayer;
            Debug.Log($"Rally state: {hitter} hit '{name}', now {State}.", this);
            return true;
        }

        // Section: Collision routing and sound.
        // Route physics contacts to the correct scoring or foul path.
        void OnCollisionEnter(Collision collision)
        {
            bool hitPaddle = collision.gameObject.GetComponentInParent<PaddleHitResponder>() != null;
            PlayImpact(hitPaddle ? paddleImpactClip : tableAndFloorImpactClip, collision.relativeVelocity.magnitude);

            if (!hitPaddle && court != null && court.IsNetCollider(collision.collider))
            {
                HandleNetContact();
                return;
            }

            if (!hitPaddle && court != null &&
                court.TryGetTableSide(collision.collider, collision.GetContact(0).point, out TableSide side))
            {
                EnterTableZone(side);
                return;
            }

            if (!hitPaddle && court != null && court.IsFloorCollider(collision.collider))
                HandleFloorCollision();
        }

        // Scale hit volume by contact speed so harder impacts sound stronger.
        void PlayImpact(AudioClip clip, float impactSpeed)
        {
            if (impactAudioSource == null || clip == null || Time.time - lastImpactSoundTime < impactSoundCooldown) return;
            lastImpactSoundTime = Time.time;

            float speedRange = Mathf.Max(maximumImpactSpeed - minimumImpactSpeed, .0001f);
            float normalizedSpeed = Mathf.Clamp01((impactSpeed - minimumImpactSpeed) / speedRange);
            float scaledVolume = impactVolume * Mathf.Lerp(minimumImpactVolumeScale, 1f, normalizedSpeed);
            impactAudioSource.PlayOneShot(clip, scaledVolume);
        }

        // Use the paddle clip as the single serve cue the user already assigned.
        void PlayServeSound()
        {
            if (impactAudioSource == null || paddleImpactClip == null) return;

            impactAudioSource.PlayOneShot(paddleImpactClip, impactVolume);
        }

        // Section: Trigger-entry scoring inputs.
        // The target is a bonus only for the player's current return.
        public void EnterTargetZone()
        {
            if (LastHitter != TableSide.Player || IsTerminalState()) return;
            enteredTarget = true;
        }

        // Physical table contacts advance the rally or resolve a missed return.
        public void EnterTableZone(TableSide side)
        {
            if (IsTerminalState()) return;
            if (Time.time - lastTableBounceTime < TableBounceDebounce) return;
            lastTableBounceTime = Time.time;
            LastTableSide = side;

            switch (State)
            {
                case TrainingBallState.Serving:
                    if (side != TableSide.Player) { RegisterFoul(TrainingFoul.Opponent); return; }
                    CommitServeAttempt();
                    State = TrainingBallState.AwaitingPlayerHit;
                    break;

                case TrainingBallState.TravelingToOpponent:
                    if (side != TableSide.Opponent) { RegisterFoul(TrainingFoul.Player); return; }
                    State = TrainingBallState.AwaitingOpponentHit;
                    break;

                case TrainingBallState.TravelingToPlayer:
                    if (side != TableSide.Player) { RegisterFoul(TrainingFoul.Opponent); return; }
                    State = TrainingBallState.AwaitingPlayerHit;
                    break;

                case TrainingBallState.AwaitingOpponentHit:
                    trainer.RegisterPoint(this, TableSide.Player, enteredTarget);
                    break;

                case TrainingBallState.AwaitingPlayerHit:
                    trainer.RegisterPoint(this, TableSide.Opponent, false);
                    break;
            }

            Debug.Log($"Rally state: bounce on {side}, now {State}.", this);
        }

        // Section: Rally resolution helpers.
        // Count the serve once play has legitimately started.
        void CommitServeAttempt()
        {
            if (CountsTowardServe) return;

            CountsTowardServe = true;
            trainer.NotifyServeInPlay(this);
        }

        // Foul state ends the ball immediately and lets the trainer handle the outcome.
        void RegisterFoul(TrainingFoul foul)
        {
            if (State == TrainingBallState.Faulted || State == TrainingBallState.Retired || State == TrainingBallState.Scored) return;

            Foul = foul;
            State = TrainingBallState.Faulted;
            terminalStateTime = Time.time;
            trainer.RegisterFoul(this, foul);
        }

        // Mark the ball as finished once the trainer accepts the score.
        public void MarkScored()
        {
            Scored = true;
            State = TrainingBallState.Scored;
            terminalStateTime = Time.time;
        }

        // Retired balls can still linger briefly for audio and bounce cleanup.
        public void MarkRetired()
        {
            if (State == TrainingBallState.Scored || State == TrainingBallState.Faulted)
                return;

            State = TrainingBallState.Retired;
            terminalStateTime = Time.time;
        }

        // Section: Contact rule handlers.
        // Floor contact ends the rally, but the ball waits one more physics step
        // so any same-step trigger events can still settle the final outcome.
        void HandleFloorCollision()
        {
            if (IsTerminalState()) return;

            // Resolve this in the next FixedUpdate so all trigger callbacks from
            // the same physics step can register a completed score first.
            floorHitPending = true;
        }

        // Net contact is always a foul in this trainer's simplified rules.
        void HandleNetContact()
        {
            if (IsTerminalState()) return;

            RegisterFoul(LastHitter == TableSide.Player ? TrainingFoul.Player : TrainingFoul.Opponent);
        }

        // A floor hit after a legal bounce is a missed return; before one, it is a bad shot.
        void ResolveUnreturnedBall()
        {
            switch (State)
            {
                case TrainingBallState.AwaitingOpponentHit:
                    trainer.RegisterPoint(this, TableSide.Player, enteredTarget);
                    break;
                case TrainingBallState.AwaitingPlayerHit:
                    trainer.RegisterPoint(this, TableSide.Opponent, false);
                    break;
                case TrainingBallState.TravelingToOpponent:
                    RegisterFoul(TrainingFoul.Player);
                    break;
                case TrainingBallState.Serving:
                case TrainingBallState.TravelingToPlayer:
                    RegisterFoul(TrainingFoul.Opponent);
                    break;
                default:
                    trainer.Retire(this);
                    break;
            }
        }

        // Section: Terminal-state checks.
        // Scored, faulted, and retired balls no longer participate in gameplay.
        bool IsTerminalState()
        {
            return State == TrainingBallState.Scored || State == TrainingBallState.Faulted || State == TrainingBallState.Retired;
        }

        // Delay destruction slightly so the ball can finish bounce audio before cleanup.
        bool CanBeDestroyed()
        {
            float terminalAge = Time.time - terminalStateTime;
            if (terminalAge >= TerminalLifetime) return true;
            return terminalAge >= MinimumTerminalLifetime && body.IsSleeping();
        }
    }
}
