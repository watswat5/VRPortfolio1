using UnityEngine;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Controls the AI opponent paddle from prediction through contact.
    /// It estimates an intercept point, moves with capped acceleration, and chooses accurate or intentionally flawed returns.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(PaddleHitResponder))]
    public sealed class AIPaddleController : MonoBehaviour, IPaddleMotionSource
    {
        // Section: Inspector configuration.
        [Header("Scene References")]
        [SerializeField] VRTableTennisTrainer trainer;
        [SerializeField] ServeController serveController;
        [SerializeField] TableCourt court;
        [SerializeField] Rigidbody paddleBody;
        [SerializeField] Collider paddleCollider;
        [SerializeField] PaddleHitResponder hitResponder;
        [Header("Difficulty")]
        [Tooltip("Main difficulty control. The AI does not predict or move until this long after the player's hit.")]
        [SerializeField, Min(0f)] float reactionTime = .18f;
        [Tooltip("How often the AI corrects its prediction after reacting. Frequent small corrections account for drag and spin without jittering every physics step.")]
        [SerializeField, Range(.02f, .2f)] float predictionRefreshInterval = .05f;
        [SerializeField, Range(.5f, 2f)] float maximumPredictionTime = 1.25f;
        [SerializeField, Range(.01f, .05f)] float predictionTimeStep = .02f;
        [Tooltip("Chance of an inaccurate return even when the paddle was not rushed.")]
        [SerializeField, Range(0f, 1f)] float baseTrajectoryErrorRate = .02f;
        [Tooltip("Extra error chance added as required paddle speed approaches Maximum Move Speed.")]
        [SerializeField, Range(0f, 1f)] float movementTrajectoryErrorRate = .4f;
        [SerializeField, Min(0f)] float comfortableMoveSpeed = 1.5f;

        [Header("Paddle Motion")]
        [SerializeField, Min(.1f)] float maximumMoveSpeed = 4.5f;
        [SerializeField, Min(.1f)] float maximumMoveAcceleration = 30f;
        [SerializeField, Min(.1f)] float maximumAngularSpeed = 18f;
        [SerializeField, Min(.1f)] float maximumAngularAcceleration = 90f;
        [Tooltip("Maximum visual face-angle change from the neutral paddle pose.")]
        [SerializeField, Range(0f, 80f)] float maximumFaceAngle = 55f;
        [Tooltip("Distance behind the opponent end of the table.")]
        [SerializeField, Min(0f)] float distanceBehindTable = .3f;
        [Tooltip("Paddle center height above the table while waiting.")]
        [SerializeField, Min(0f)] float readyHeight = .32f;
        [SerializeField, Min(0f)] float lateralReach = .82f;
        [SerializeField, Min(0f)] float reachBelowReady = .24f;
        [SerializeField, Min(0f)] float reachAboveReady = .55f;
        [Tooltip("Maximum paddle travel from ready position toward and over the table.")]
        [SerializeField, Min(0f)] float tablewardReach = .85f;
        [Tooltip("Maximum paddle travel away from the table to handle deep returns.")]
        [SerializeField, Min(0f)] float backwardReach = .55f;
        [Tooltip("Preferred contact distance in front of the ready position.")]
        [SerializeField, Min(0f)] float preferredContactDistance = .45f;
        [Tooltip("Keeps the bottom of the AI paddle from clipping into the tabletop.")]
        [SerializeField, Min(0f)] float tableClearance = .015f;

        [Header("Return")]
        [SerializeField, Min(.1f)] float minimumReturnSpeed = 5.2f;
        [SerializeField, Min(.1f)] float maximumReturnSpeed = 6.3f;
        [SerializeField, Min(0f)] float returnNetClearance = .06f;
        [SerializeField, Range(0f, .45f)] float returnLengthMargin = .12f;
        [SerializeField, Range(0f, .45f)] float returnWidthMargin = .12f;
        [Tooltip("How far outside the legal table bounds an inaccurate return is aimed.")]
        [SerializeField, Min(.05f)] float illegalAimOffset = .25f;
        [SerializeField] Vector2 returnSideSpinRange = new Vector2(-12f, 12f);
        [SerializeField] Vector2 returnTopSpinRange = new Vector2(8f, 24f);

        // Section: Public telemetry.
        public Vector3 LinearVelocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        public float LastRequiredMoveSpeed { get; private set; }
        public float LastTrajectoryErrorChance { get; private set; }

        // Section: Runtime prediction and motion state.
        TrainingBall trackedBall;
        float reactAt;
        float interceptAt;
        bool predictionCommitted;
        bool aiModeEnabled;
        bool wasAwaitingOpponentHit;
        float nextPredictionAt;
        Vector3 homePosition;
        Quaternion homeRotation;
        Vector3 targetPosition;
        Quaternion targetRotation;
        Vector3 opponentOutward;
        Vector3 courtWidthDirection;
        Renderer[] paddleRenderers;
        bool hasReturnPlan;
        bool plannedReturnInaccurate;
        Vector3 plannedReturnTarget;
        float plannedReturnSpeed;
        Vector3 plannedReturnVelocity;

        // Section: Lifecycle and subscriptions.
        void Awake()
        {
            if (paddleBody == null) paddleBody = GetComponent<Rigidbody>();
            if (paddleCollider == null) paddleCollider = GetComponent<Collider>();
            if (hitResponder == null) hitResponder = GetComponent<PaddleHitResponder>();
            if (court == null && trainer != null) court = trainer.Court;

            paddleRenderers = GetComponentsInChildren<Renderer>(true);

            paddleBody.useGravity = false;
            paddleBody.interpolation = RigidbodyInterpolation.Interpolate;
            paddleBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            paddleBody.maxAngularVelocity = maximumAngularSpeed;
        }

        void OnEnable()
        {
            if (trainer != null) trainer.ModeChanged += HandleModeChanged;
            if (hitResponder != null) hitResponder.LegalHit += HandleLegalHit;
        }

        void Start()
        {
            if (!ValidateReferences()) { enabled = false; return; }
            PlaceAtOpponentEnd();
            SetAIMode(trainer.Mode == TrainerMode.AIOpponent);
        }

        void OnDisable()
        {
            if (trainer != null) trainer.ModeChanged -= HandleModeChanged;
            if (hitResponder != null) hitResponder.LegalHit -= HandleLegalHit;
        }

        // Section: Core AI loop.
        void FixedUpdate()
        {
            if (!aiModeEnabled)
            {
                LinearVelocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                return;
            }

            TrainingBall activeBall = serveController.ActiveBall;
            if (activeBall != trackedBall) BeginTracking(activeBall);

            bool incoming = trackedBall != null &&
                (trackedBall.State == TrainingBallState.TravelingToOpponent ||
                 trackedBall.State == TrainingBallState.AwaitingOpponentHit);
            bool mayReturn = trackedBall != null &&
                trackedBall.State == TrainingBallState.AwaitingOpponentHit;

            // The real bounce is the cue for immediate height and face-angle correction.
            if (mayReturn && !wasAwaitingOpponentHit)
                nextPredictionAt = Time.time;

            if (incoming && reactAt < 0f)
            {
                reactAt = Time.time + reactionTime;
                nextPredictionAt = reactAt;
            }

            if (incoming && Time.time >= nextPredictionAt)
            {
                nextPredictionAt = Time.time + predictionRefreshInterval;
                if (CommitInterceptPrediction(trackedBall))
                    predictionCommitted = true;
                else if (Time.time >= interceptAt)
                    predictionCommitted = false;
            }

            if (!incoming)
            {
                predictionCommitted = false;
                reactAt = -1f;
                nextPredictionAt = -1f;
                targetPosition = homePosition;
                targetRotation = homeRotation;
                interceptAt = -1f;
            }

            paddleCollider.enabled = mayReturn && predictionCommitted;
            DriveTowardTarget();
            wasAwaitingOpponentHit = mayReturn;
        }

        void HandleModeChanged(TrainerMode mode) => SetAIMode(mode == TrainerMode.AIOpponent);

        // Section: Public control.
        public void SetAIMode(bool enabledForAI)
        {
            aiModeEnabled = enabledForAI;
            trackedBall = null;
            predictionCommitted = false;
            wasAwaitingOpponentHit = false;
            hasReturnPlan = false;
            reactAt = -1f;
            nextPredictionAt = -1f;
            interceptAt = -1f;
            targetPosition = homePosition;
            targetRotation = homeRotation;
            paddleCollider.enabled = false;
            paddleBody.linearVelocity = Vector3.zero;
            paddleBody.angularVelocity = Vector3.zero;
            paddleBody.isKinematic = !enabledForAI;

            foreach (Renderer paddleRenderer in paddleRenderers)
                paddleRenderer.enabled = enabledForAI;

            if (!enabledForAI && homeRotation != default)
            {
                paddleBody.position = homePosition;
                paddleBody.rotation = homeRotation;
            }
        }

        // Section: Tracking and prediction.
        void BeginTracking(TrainingBall ball)
        {
            trackedBall = ball;
            predictionCommitted = false;
            wasAwaitingOpponentHit = false;
            hasReturnPlan = false;
            reactAt = -1f;
            nextPredictionAt = -1f;
            interceptAt = -1f;
            targetPosition = homePosition;
            targetRotation = homeRotation;
            paddleCollider.enabled = false;
        }

        bool CommitInterceptPrediction(TrainingBall ball)
        {
            Rigidbody ballBody = ball.GetComponent<Rigidbody>();
            if (ballBody == null) return false;

            Vector3 homePaddleCenter = GetPaddleCenter(homePosition, homeRotation);
            if (!TryPredictIntercept(
                ball,
                ballBody,
                homePaddleCenter,
                out Vector3 intercept,
                out Vector3 incomingVelocity,
                out float flightTime))
            {
                Debug.Log($"AI missed prediction for '{ball.name}': ball already passed or moving away.", this);
                return false;
            }

            bool bounced = ball.State == TrainingBallState.AwaitingOpponentHit;
            Vector3 movementTargetCenter = intercept;
            if (!bounced)
                movementTargetCenter.y = homePaddleCenter.y;

            float moveDistance = Vector3.Distance(
                GetPaddleCenter(paddleBody.position, paddleBody.rotation),
                movementTargetCenter);
            LastRequiredMoveSpeed = moveDistance / Mathf.Max(flightTime, Time.fixedDeltaTime);
            interceptAt = Time.time + flightTime;
            targetRotation = homeRotation;

            if (bounced && EnsureReturnPlan(ballBody) && TryBuildPlannedReturnVelocity(
                intercept,
                GetBallRadius(ballBody),
                out plannedReturnVelocity))
                targetRotation = GetReturnFacing(incomingVelocity, plannedReturnVelocity);

            targetPosition = movementTargetCenter - targetRotation * GetLocalColliderCenter();

            if (!predictionCommitted)
                Debug.Log(
                    $"AI committed to '{ball.name}' after {reactionTime:F2}s reaction | " +
                    $"intercept in={flightTime:F2}s, required speed={LastRequiredMoveSpeed:F2} m/s, " +
                    $"target={movementTargetCenter:F3}.",
                    this);
            return true;
        }

        // Section: Paddle movement.
        void DriveTowardTarget()
        {
            float dt = Time.fixedDeltaTime;
            float moveTime = interceptAt > Time.time
                ? Mathf.Max(interceptAt - Time.time, dt)
                : dt;
            Vector3 desiredVelocity = Vector3.ClampMagnitude(
                (targetPosition - paddleBody.position) / moveTime,
                maximumMoveSpeed);
            LinearVelocity = Vector3.MoveTowards(
                paddleBody.linearVelocity,
                desiredVelocity,
                maximumMoveAcceleration * dt);

            Quaternion delta = targetRotation * Quaternion.Inverse(paddleBody.rotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f) angleDegrees -= 360f;
            Vector3 desiredAngularVelocity = Mathf.Abs(angleDegrees) < .01f || axis.sqrMagnitude < .001f
                ? Vector3.zero
                : axis.normalized * (angleDegrees * Mathf.Deg2Rad / Mathf.Max(dt, .0001f));
            desiredAngularVelocity = Vector3.ClampMagnitude(desiredAngularVelocity, maximumAngularSpeed);
            AngularVelocity = Vector3.MoveTowards(
                paddleBody.angularVelocity,
                desiredAngularVelocity,
                maximumAngularAcceleration * dt);

            paddleBody.linearVelocity = LinearVelocity;
            paddleBody.angularVelocity = AngularVelocity;
        }

        // Section: Return generation.
        void HandleLegalHit(TrainingBall ball)
        {
            if (!aiModeEnabled || ball == null || ball != trackedBall) return;

            Rigidbody ballBody = ball.GetComponent<Rigidbody>();
            if (ballBody != null && EnsureReturnPlan(ballBody) && TryBuildPlannedReturnVelocity(
                ballBody.worldCenterOfMass,
                GetBallRadius(ballBody),
                out Vector3 returnVelocity))
            {
                ballBody.linearVelocity = returnVelocity;
                ballBody.angularVelocity = new Vector3(
                    Random.Range(returnSideSpinRange.x, returnSideSpinRange.y),
                    Random.Range(returnTopSpinRange.x, returnTopSpinRange.y),
                    0f) * (2f * Mathf.PI);
            }

            Debug.Log(
                $"AI return '{ball.name}' | required speed={LastRequiredMoveSpeed:F2} m/s, " +
                $"error chance={LastTrajectoryErrorChance:P0}, " +
                $"inaccurate={plannedReturnInaccurate}, velocity={ballBody?.linearVelocity:F3}.",
                this);
        }

        // Chooses one return for the volley so prediction refreshes do not change paddle aim.
        bool EnsureReturnPlan(Rigidbody ballBody)
        {
            if (hasReturnPlan) return true;

            Collider playerBoundsCollider = court.PlayerZone.GetComponent<Collider>();
            if (playerBoundsCollider == null) return false;

            float movementLoad = maximumMoveSpeed <= comfortableMoveSpeed
                ? Mathf.Clamp01(LastRequiredMoveSpeed / maximumMoveSpeed)
                : Mathf.InverseLerp(comfortableMoveSpeed, maximumMoveSpeed, LastRequiredMoveSpeed);
            LastTrajectoryErrorChance = Mathf.Clamp01(
                baseTrajectoryErrorRate + movementTrajectoryErrorRate * movementLoad);
            plannedReturnInaccurate = Random.value < LastTrajectoryErrorChance;
            plannedReturnTarget = GetReturnTarget(playerBoundsCollider.bounds, plannedReturnInaccurate);
            float ballRadius = GetBallRadius(ballBody);
            plannedReturnTarget.y = court.SurfaceHeight + ballRadius;
            plannedReturnSpeed = Random.Range(minimumReturnSpeed, maximumReturnSpeed);
            hasReturnPlan = true;
            return true;
        }

        // Re-solves the same planned target as the measured contact point becomes more accurate.
        bool TryBuildPlannedReturnVelocity(Vector3 origin, float ballRadius, out Vector3 velocity)
        {
            velocity = default;
            if (!hasReturnPlan) return false;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float speed = attempt == 0
                    ? plannedReturnSpeed
                    : Mathf.Lerp(minimumReturnSpeed, maximumReturnSpeed, (attempt - 1f) / 14f);
                if (!Ballistics.TrySolveLowArc(origin, plannedReturnTarget, speed, out velocity))
                    continue;
                if (plannedReturnInaccurate || Ballistics.ClearsVerticalObstacle(
                    origin,
                    plannedReturnTarget,
                    velocity,
                    court.NetSurface.bounds,
                    ballRadius,
                    returnNetClearance))
                    return true;
            }
            return false;
        }

        // Points the broad face along the impulse needed to turn the incoming ball into the planned return.
        Quaternion GetReturnFacing(Vector3 incomingVelocity, Vector3 outgoingVelocity)
        {
            Vector3 neutralNormal = homeRotation * Vector3.up;
            Vector3 desiredNormal = outgoingVelocity - incomingVelocity;
            if (desiredNormal.sqrMagnitude < .0001f) return homeRotation;
            desiredNormal.Normalize();

            float angle = Vector3.Angle(neutralNormal, desiredNormal);
            if (angle > maximumFaceAngle && angle > .001f)
                desiredNormal = Vector3.Slerp(neutralNormal, desiredNormal, maximumFaceAngle / angle).normalized;

            return Quaternion.FromToRotation(neutralNormal, desiredNormal) * homeRotation;
        }

        // Predicts when and where the current incoming ball can be contacted.
        bool TryPredictIntercept(
            TrainingBall ball,
            Rigidbody ballBody,
            Vector3 paddleCenter,
            out Vector3 intercept,
            out Vector3 contactVelocity,
            out float flightTime)
        {
            Vector3 origin = ballBody.worldCenterOfMass;
            Vector3 velocity = ballBody.linearVelocity;
            intercept = default;
            contactVelocity = default;
            flightTime = 0f;
            if (Vector3.Dot(velocity, opponentOutward) <= .05f) return false;

            float timeBeforeSearch = 0f;
            float bounceHeight = court.SurfaceHeight + GetBallRadius(ballBody);
            if (ball.State == TrainingBallState.TravelingToOpponent)
            {
                if (!TryGetDescendingHeightTime(origin.y, velocity.y, bounceHeight, out float timeToBounce))
                    return false;

                Vector3 bouncePoint = origin + velocity * timeToBounce +
                    .5f * Physics.gravity * timeToBounce * timeToBounce;
                Vector3 impactVelocity = velocity + Physics.gravity * timeToBounce;
                float restitution = GetBallRestitution(ballBody);
                velocity = Vector3.ProjectOnPlane(impactVelocity, Vector3.up) -
                    Vector3.up * impactVelocity.y * restitution;
                origin = bouncePoint;
                origin.y = bounceHeight;
                timeBeforeSearch = timeToBounce;
            }

            if (Vector3.Dot(velocity, opponentOutward) <= .05f || timeBeforeSearch >= maximumPredictionTime)
                return false;

            Vector3 currentPaddleCenter = GetPaddleCenter(paddleBody.position, paddleBody.rotation);
            float verticalExtent = Mathf.Max(.01f, paddleCollider.bounds.extents.y);
            float minimumPaddleHeight = court.SurfaceHeight + verticalExtent + tableClearance;
            float bestScore = float.PositiveInfinity;
            bool found = false;

            for (float timeAfterBounce = predictionTimeStep;
                 timeBeforeSearch + timeAfterBounce <= maximumPredictionTime;
                 timeAfterBounce += predictionTimeStep)
            {
                Vector3 candidateBallPosition = origin + velocity * timeAfterBounce +
                    .5f * Physics.gravity * timeAfterBounce * timeAfterBounce;
                float candidateVerticalSpeed = velocity.y + Physics.gravity.y * timeAfterBounce;

                // Do not plan a contact after the ball's second table bounce.
                if (candidateVerticalSpeed < 0f && candidateBallPosition.y <= bounceHeight)
                    break;

                Vector3 candidatePaddleCenter = candidateBallPosition;
                candidatePaddleCenter.y = Mathf.Max(candidatePaddleCenter.y, minimumPaddleHeight);

                Vector3 fromHome = candidatePaddleCenter - paddleCenter;
                float lateral = Vector3.Dot(fromHome, courtWidthDirection);
                float vertical = Vector3.Dot(fromHome, Vector3.up);
                float depth = Vector3.Dot(fromHome, opponentOutward);
                if (Mathf.Abs(lateral) > lateralReach ||
                    vertical < -reachBelowReady || vertical > reachAboveReady ||
                    depth < -tablewardReach || depth > backwardReach)
                    continue;

                float candidateFlightTime = timeBeforeSearch + timeAfterBounce;
                float moveDistance = Vector3.Distance(currentPaddleCenter, candidatePaddleCenter);
                float requiredSpeed = moveDistance / Mathf.Max(candidateFlightTime, Time.fixedDeltaTime);
                if (requiredSpeed > maximumMoveSpeed)
                    continue;

                // Prefer a stable contact in front of the ready pose, then the least rushed option.
                float contactDistance = -depth;
                float score = Mathf.Abs(contactDistance - preferredContactDistance) +
                    .2f * requiredSpeed / maximumMoveSpeed;
                if (score >= bestScore) continue;

                bestScore = score;
                intercept = candidatePaddleCenter;
                contactVelocity = velocity + Physics.gravity * timeAfterBounce;
                flightTime = candidateFlightTime;
                found = true;
            }

            return found;
        }

        // Section: Math and geometry helpers.
        static bool TryGetDescendingHeightTime(
            float startHeight,
            float verticalSpeed,
            float targetHeight,
            out float time)
        {
            time = 0f;
            float gravity = Physics.gravity.y;
            float discriminant = verticalSpeed * verticalSpeed -
                2f * gravity * (startHeight - targetHeight);
            if (gravity >= -.001f || discriminant < 0f) return false;

            float root = Mathf.Sqrt(discriminant);
            float first = (-verticalSpeed + root) / gravity;
            float second = (-verticalSpeed - root) / gravity;
            time = Mathf.Max(first, second);
            return time > 0f;
        }

        static float GetBallRestitution(Rigidbody ballBody)
        {
            SphereCollider sphere = ballBody.GetComponent<SphereCollider>();
            return sphere != null && sphere.sharedMaterial != null
                ? sphere.sharedMaterial.bounciness
                : .88f;
        }

        Vector3 GetReturnTarget(Bounds bounds, bool inaccurate)
        {
            Vector3 playerCenter = bounds.center;
            Vector3 towardPlayer = -opponentOutward;
            float halfLength = GetProjectedExtent(bounds, towardPlayer);
            float halfWidth = GetProjectedExtent(bounds, courtWidthDirection);
            float lengthRange = halfLength * (1f - 2f * returnLengthMargin);
            float widthRange = halfWidth * (1f - 2f * returnWidthMargin);
            float along = Random.Range(-lengthRange, lengthRange);

            if (inaccurate)
            {
                float side = Random.value < .5f ? -1f : 1f;
                return playerCenter + towardPlayer * along +
                    courtWidthDirection * side * (halfWidth + illegalAimOffset);
            }

            return playerCenter + towardPlayer * along +
                courtWidthDirection * Random.Range(-widthRange, widthRange);
        }

        void PlaceAtOpponentEnd()
        {
            Bounds tableBounds = court.TableSurface.bounds;
            Bounds playerBounds = court.PlayerZone.GetComponent<Collider>().bounds;
            Bounds opponentBounds = court.OpponentZone.GetComponent<Collider>().bounds;
            opponentOutward = Vector3.ProjectOnPlane(opponentBounds.center - playerBounds.center, Vector3.up).normalized;
            courtWidthDirection = Vector3.Cross(Vector3.up, opponentOutward).normalized;

            Vector3 tableCenter = tableBounds.center;
            Vector3 desiredCenter = tableCenter +
                opponentOutward * (GetProjectedExtent(tableBounds, opponentOutward) + distanceBehindTable) +
                Vector3.up * (court.SurfaceHeight + readyHeight - tableCenter.y);
            homeRotation = Quaternion.FromToRotation(Vector3.up, -opponentOutward);
            homePosition = desiredCenter - homeRotation * GetLocalColliderCenter();
            targetPosition = homePosition;
            targetRotation = homeRotation;
            paddleBody.position = homePosition;
            paddleBody.rotation = homeRotation;
        }

        bool ValidateReferences()
        {
            bool valid = trainer != null && serveController != null && court != null &&
                paddleBody != null && paddleCollider != null && hitResponder != null;
            if (!valid)
                Debug.LogError(
                    "Assign Trainer, Serve Controller, Court, body, collider, and hit responder on AIPaddleController.",
                    this);
            else if (hitResponder.Side != TableSide.Opponent)
            {
                Debug.LogError("The AI PaddleHitResponder Side must be Opponent.", this);
                valid = false;
            }
            return valid;
        }

        Vector3 GetPaddleCenter(Vector3 position, Quaternion rotation) =>
            position + rotation * GetLocalColliderCenter();

        Vector3 GetLocalColliderCenter() =>
            paddleCollider is BoxCollider box ? box.center : Vector3.zero;

        static float GetProjectedExtent(Bounds bounds, Vector3 direction)
        {
            direction = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            return Vector3.Dot(bounds.extents, direction);
        }

        static float GetBallRadius(Rigidbody ballBody)
        {
            SphereCollider sphere = ballBody.GetComponent<SphereCollider>();
            if (sphere == null) return .02f;
            Vector3 scale = sphere.transform.lossyScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
