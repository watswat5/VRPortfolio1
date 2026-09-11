using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Demo2.TableTennis
{
    public enum TargetDifficulty { Easy, Medium, Hard }

    /// <summary>Inspector-wired serving, scoring, and ball lifecycle coordinator.</summary>
    public sealed class VRTableTennisTrainer : MonoBehaviour
    {
        const int ServeVelocitySampleCount = 20;

        [Header("Scene References")]
        [SerializeField] TrainingBall ballPrefab;
        [SerializeField] Transform ballSpawn;
        [SerializeField] Collider tableSurface;
        [SerializeField] Transform floorCollisionRoot;
        [SerializeField] TableZone playerServeZone;
        [SerializeField] TableZone opponentTargetZone;
        [SerializeField] Collider netSurface;
        [SerializeField] TargetZone targetZone;
        [SerializeField] TextMeshPro scoreText;
        [SerializeField] TextMeshPro missesText;
        [SerializeField] TextMeshPro bonusText;
        [SerializeField] TextMeshPro serveNumberText;

        [Header("Feedback Audio")]
        [SerializeField] AudioSource feedbackAudioSource;
        [SerializeField] AudioClip startBuzzer;
        [SerializeField] AudioClip foulBuzzer;
        [SerializeField] AudioClip failBuzzer;
        [SerializeField] AudioClip successDing;
        [Tooltip("Played instead of Success Ding when a valid score also hits the bonus target.")]
        [SerializeField] AudioClip bonusSuccessDing;

        [Header("Serving")]
        [SerializeField, Min(0f)] float initialDelay = 1.5f;
        [FormerlySerializedAs("delayBetweenBalls")]
        [SerializeField, Min(0f)] float delayBetweenServes = .8f;
        [Tooltip("Time between the start buzzer and the ball appearing.")]
        [SerializeField, Min(0f)] float startDelayAfterBuzzer = .5f;
        [SerializeField, Min(1)] int servesPerRound = 9;
        [SerializeField] float minimumServeSpeed = 5.7f;
        [SerializeField] float maximumServeSpeed = 6.5f;
        [SerializeField, Range(0f, .45f)] float serveLengthMargin = .12f;
        [SerializeField, Range(0f, .45f)] float serveWidthMargin = .08f;
        [SerializeField, Min(0f)] float serveNetClearance = .08f;
        [SerializeField] Vector2 sideSpinRange = new Vector2(-18f, 18f);
        [SerializeField] Vector2 topSpinRange = new Vector2(15f, 35f);

        [Header("Serve Preview")]
        [SerializeField] bool showServePreview = true;
        [Tooltip("Seconds of the predicted trajectory to display. Faster serves still produce longer arrows.")]
        [SerializeField, Min(.01f)] float previewArrowLengthScale = .32f;
        [SerializeField, Range(4, 64)] int previewTrajectorySegments = 20;
        [SerializeField, Min(.001f)] float previewArrowWidth = .018f;
        [SerializeField, Min(.01f)] float previewArrowHeadSize = .1f;
        [SerializeField] Color previewArrowColor = new Color(0f, .9f, 1f, 1f);
        [Tooltip("Optional URP-compatible material. A runtime unlit material is created when left empty.")]
        [SerializeField] Material previewArrowMaterial;

        [Header("Target")]
        [SerializeField] TargetDifficulty targetDifficulty = TargetDifficulty.Medium;
        [SerializeField, Min(0f)] float targetNetMargin = .2f;

        public int Score { get; private set; }
        public int Bonuses { get; private set; }
        public int Attempts { get; private set; }
        public int Misses { get; private set; }

        TrainingBall activeBall;
        Vector3 preparedServeVelocity;
        Vector3 preparedServeSpin;
        LineRenderer servePreviewArrow;
        LineRenderer servePreviewArrowHead;
        Material runtimePreviewArrowMaterial;
        Vector3 targetBaseScale;
        float targetSurfaceOffset;
        bool serving;

        // Engine setup and initial scene validation.
        void Awake()
        {
            Physics.defaultSolverIterations = Mathf.Max(Physics.defaultSolverIterations, 12);
            Physics.defaultSolverVelocityIterations = Mathf.Max(Physics.defaultSolverVelocityIterations, 4);
            Time.fixedDeltaTime = 1f / 72f; //Match the physics timestep to the headset refresh rate for more consistent ball simulation.

            if (feedbackAudioSource != null)
            {
                feedbackAudioSource.spatialBlend = 1f;
                feedbackAudioSource.spatialize = true;
                feedbackAudioSource.playOnAwake = false;
            }
        }

        void Start()
        {
            if (!ValidateReferences()) { enabled = false; return; }
            targetBaseScale = targetZone.transform.localScale;
            targetSurfaceOffset = GetTargetSurfaceOffset();
            MoveTargetZone();
            RefreshStatDisplays();
            StartCoroutine(ServeLoop());
        }

        bool ValidateReferences()
        {
            bool valid = true;
            if (ballPrefab == null) { Debug.LogError("Assign Ball Prefab on VRTableTennisTrainer.", this); valid = false; }
            if (ballSpawn == null) { Debug.LogError("Assign Ball Spawn on VRTableTennisTrainer.", this); valid = false; }
            if (tableSurface == null) { Debug.LogError("Assign the table surface collider on VRTableTennisTrainer.", this); valid = false; }
            else if (tableSurface.isTrigger) { Debug.LogError("Table surface must use a non-trigger collider.", tableSurface); valid = false; }
            if (floorCollisionRoot == null)
                Debug.LogWarning("Assign Floor Collision Root to end rallies early when the ball hits the floor.", this);
            if (!ValidateTableZone(playerServeZone, TableSide.Player, "Player Serve Zone")) valid = false;
            if (!ValidateTableZone(opponentTargetZone, TableSide.Opponent, "Opponent Target Zone")) valid = false;
            if (netSurface == null) { Debug.LogError("Assign the net trigger collider on VRTableTennisTrainer.", this); valid = false; }
            else if (!netSurface.isTrigger) { Debug.LogError("Net surface must use a trigger collider.", netSurface); valid = false; }
            if (targetZone == null) { Debug.LogError("Assign Target Zone on VRTableTennisTrainer.", this); valid = false; }
            else if (targetZone.GetComponent<Collider>() == null) { Debug.LogError("Target Zone needs a Collider so it can be positioned and triggered.", targetZone); valid = false; }
            return valid;
        }

        static bool ValidateTableZone(TableZone tableZone, TableSide expectedSide, string label)
        {
            if (tableZone == null)
            {
                Debug.LogError($"Assign {label} on VRTableTennisTrainer.");
                return false;
            }

            if (tableZone.Side != expectedSide)
            {
                Debug.LogError($"{label} must use TableSide.{expectedSide}.", tableZone);
                return false;
            }

            if (!tableZone.TryGetComponent(out Collider zoneCollider) || !zoneCollider.isTrigger)
            {
                Debug.LogError($"{label} needs a trigger collider.", tableZone);
                return false;
            }

            return true;
        }

        // Main trainer loop that schedules the next serve.
        IEnumerator ServeLoop()
        {
            bool firstServe = true;
            while (enabled)
            {
                if (activeBall == null && !serving)
                {
                    serving = true;
                    PrepareServe();
                    yield return new WaitForSeconds(firstServe ? initialDelay : delayBetweenServes);
                    PlayFeedback(startBuzzer);
                    yield return new WaitForSeconds(startDelayAfterBuzzer);
                    LaunchPreparedServe();
                    firstServe = false;
                    serving = false;
                }
                yield return null;
            }
        }

        // Create the ball and trajectory preview before any serve delay begins.
        void PrepareServe()
        {
            activeBall = Instantiate(ballPrefab, ballSpawn.position, ballSpawn.rotation);
            preparedServeVelocity = GetServeVelocity();
            preparedServeSpin = new Vector3(
                Random.Range(sideSpinRange.x, sideSpinRange.y),
                Random.Range(topSpinRange.x, topSpinRange.y),
                0f);
            activeBall.PrepareServePreview();
            SetServePreviewVisible(showServePreview);
        }

        void LaunchPreparedServe()
        {
            SetServePreviewVisible(false);
            if (activeBall == null) return;
            activeBall.Launch(this, preparedServeVelocity, preparedServeSpin);
        }

        void SetServePreviewVisible(bool visible)
        {
            if (!visible)
            {
                if (servePreviewArrow != null) servePreviewArrow.enabled = false;
                if (servePreviewArrowHead != null) servePreviewArrowHead.enabled = false;
                return;
            }

            EnsureServePreviewArrow();
            if (servePreviewArrow == null || servePreviewArrowHead == null) return;

            Vector3 start = ballSpawn.position;
            int segmentCount = Mathf.Max(4, previewTrajectorySegments);
            servePreviewArrow.positionCount = segmentCount + 1;
            for (int i = 0; i <= segmentCount; i++)
            {
                float time = previewArrowLengthScale * i / segmentCount;
                Vector3 point = start
                    + preparedServeVelocity * time
                    + .5f * Physics.gravity * time * time;
                servePreviewArrow.SetPosition(i, point);
            }

            float endTime = previewArrowLengthScale;
            float previousTime = previewArrowLengthScale * (segmentCount - 1f) / segmentCount;
            Vector3 end = start + preparedServeVelocity * endTime + .5f * Physics.gravity * endTime * endTime;
            Vector3 previous = start + preparedServeVelocity * previousTime + .5f * Physics.gravity * previousTime * previousTime;
            Vector3 direction = (end - previous).normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < .001f)
                side = Vector3.Cross(direction, Vector3.right);
            side.Normalize();
            Vector3 headBase = end - direction * previewArrowHeadSize;
            float headHalfWidth = previewArrowHeadSize * .5f;

            servePreviewArrowHead.positionCount = 3;
            servePreviewArrowHead.SetPosition(0, headBase + side * headHalfWidth);
            servePreviewArrowHead.SetPosition(1, end);
            servePreviewArrowHead.SetPosition(2, headBase - side * headHalfWidth);
            servePreviewArrow.enabled = true;
            servePreviewArrowHead.enabled = true;
        }

        void EnsureServePreviewArrow()
        {
            if (servePreviewArrow != null) return;

            GameObject arrowObject = new GameObject("Serve Preview Arrow");
            servePreviewArrow = arrowObject.AddComponent<LineRenderer>();
            ConfigurePreviewLine(servePreviewArrow);

            GameObject headObject = new GameObject("Serve Preview Arrow Head");
            servePreviewArrowHead = headObject.AddComponent<LineRenderer>();
            ConfigurePreviewLine(servePreviewArrowHead);

            if (previewArrowMaterial != null)
            {
                servePreviewArrow.sharedMaterial = previewArrowMaterial;
                servePreviewArrowHead.sharedMaterial = previewArrowMaterial;
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("No compatible shader found for the serve preview arrow.", this);
                return;
            }

            runtimePreviewArrowMaterial = new Material(shader) { color = previewArrowColor };
            servePreviewArrow.sharedMaterial = runtimePreviewArrowMaterial;
            servePreviewArrowHead.sharedMaterial = runtimePreviewArrowMaterial;
        }

        void ConfigurePreviewLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.loop = false;
            line.startWidth = previewArrowWidth;
            line.endWidth = previewArrowWidth;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.startColor = previewArrowColor;
            line.endColor = previewArrowColor;
        }

        void OnDestroy()
        {
            if (servePreviewArrow != null) Destroy(servePreviewArrow.gameObject);
            if (servePreviewArrowHead != null) Destroy(servePreviewArrowHead.gameObject);
            if (runtimePreviewArrowMaterial != null)
                Destroy(runtimePreviewArrowMaterial);
        }

        // Legal serve generation, including first-bounce targeting and net clearance.
        Vector3 GetServeVelocity()
        {
            if (TryGetServeVelocity(out Vector3 velocity))
                return velocity;

            if (TryGetFallbackServeVelocity(out velocity))
                return velocity;

            Vector3 fallbackDirection = new Vector3(0f, -.18f, 1f).normalized;
            return ballSpawn.TransformDirection(fallbackDirection) * minimumServeSpeed;
        }

        bool TryGetServeVelocity(out Vector3 velocity)
        {
            velocity = default;
            if (playerServeZone == null || tableSurface == null) return false;

            for (int attempt = 0; attempt < ServeVelocitySampleCount; attempt++)
            {
                Vector3 bouncePoint = GetRandomBouncePoint();
                float launchSpeed = Random.Range(minimumServeSpeed, maximumServeSpeed);
                if (TrySolveBallisticArc(ballSpawn.position, bouncePoint, launchSpeed, out velocity)
                    && HasServeNetClearance(ballSpawn.position, bouncePoint, velocity))
                    return true;
            }

            return false;
        }

        bool TryGetFallbackServeVelocity(out Vector3 velocity)
        {
            velocity = default;
            if (playerServeZone == null || tableSurface == null) return false;

            Vector3 bouncePoint = GetRandomBouncePoint();
            float[] fallbackSpeeds = { maximumServeSpeed, maximumServeSpeed + .35f, maximumServeSpeed + .7f };
            for (int i = 0; i < fallbackSpeeds.Length; i++)
            {
                float launchSpeed = fallbackSpeeds[i];
                if (TrySolveBallisticArc(ballSpawn.position, bouncePoint, launchSpeed, out velocity)
                    && HasServeNetClearance(ballSpawn.position, bouncePoint, velocity))
                    return true;
            }

            return false;
        }

        bool HasServeNetClearance(Vector3 origin, Vector3 bouncePoint, Vector3 launchVelocity)
        {
            if (netSurface == null) return true;

            Vector3 horizontalDelta = Vector3.ProjectOnPlane(bouncePoint - origin, Vector3.up);
            float horizontalDistance = horizontalDelta.magnitude;
            float horizontalSpeed = Vector3.ProjectOnPlane(launchVelocity, Vector3.up).magnitude;
            if (horizontalDistance < .0001f || horizontalSpeed < .0001f) return true;

            Vector3 horizontalDirection = horizontalDelta / horizontalDistance;
            float distanceToNet = Vector3.Dot(
                Vector3.ProjectOnPlane(netSurface.bounds.center - origin, Vector3.up),
                horizontalDirection);
            if (distanceToNet <= 0f || distanceToNet >= horizontalDistance) return true;

            float timeToNet = distanceToNet / horizontalSpeed;
            float netCrossingHeight = origin.y + (launchVelocity.y * timeToNet) + (.5f * Physics.gravity.y * timeToNet * timeToNet);
            float requiredHeight = netSurface.bounds.max.y + GetBallRadius() + serveNetClearance;
            return netCrossingHeight >= requiredHeight;
        }

        Vector3 GetRandomBouncePoint()
        {
            float ballRadius = GetBallRadius();
            if (playerServeZone.TryGetComponent(out BoxCollider zoneBox))
            {
                Vector3 halfSize = zoneBox.size * .5f;
                float xMin = zoneBox.center.x - halfSize.x + zoneBox.size.x * serveLengthMargin;
                float xMax = zoneBox.center.x + halfSize.x - zoneBox.size.x * serveLengthMargin;
                float zMin = zoneBox.center.z - halfSize.z + zoneBox.size.z * serveWidthMargin;
                float zMax = zoneBox.center.z + halfSize.z - zoneBox.size.z * serveWidthMargin;
                Vector3 localPoint = new Vector3(Random.Range(xMin, xMax), zoneBox.center.y, Random.Range(zMin, zMax));
                Vector3 worldPoint = zoneBox.transform.TransformPoint(localPoint);
                worldPoint.y = tableSurface.bounds.max.y + ballRadius;
                return worldPoint;
            }

            Bounds bounds = playerServeZone.GetComponent<Collider>().bounds;
            return new Vector3(
                Random.Range(bounds.min.x + bounds.size.x * serveLengthMargin, bounds.max.x - bounds.size.x * serveLengthMargin),
                tableSurface.bounds.max.y + ballRadius,
                Random.Range(bounds.min.z + bounds.size.z * serveWidthMargin, bounds.max.z - bounds.size.z * serveWidthMargin));
        }

        float GetBallRadius()
        {
            SphereCollider sphereCollider = ballPrefab.GetComponent<SphereCollider>();
            if (sphereCollider == null) return .02f;
            return sphereCollider.radius * Mathf.Max(ballPrefab.transform.localScale.x, ballPrefab.transform.localScale.y, ballPrefab.transform.localScale.z);
        }

        // Opponent-side target placement and difficulty-based sizing.
        void MoveTargetZone()
        {
            if (targetZone == null || opponentTargetZone == null || tableSurface == null) return;

            Collider targetCollider = targetZone.GetComponent<Collider>();
            if (targetCollider == null) return;

            ApplyTargetDifficultyScale();

            Vector3 targetPoint = GetRandomTargetPoint(targetCollider);
            targetPoint.y = tableSurface.bounds.max.y + targetSurfaceOffset;
            targetZone.transform.position = targetPoint;
        }

        void ApplyTargetDifficultyScale()
        {
            Vector3 scaled = targetBaseScale;
            float multiplier = GetTargetScaleMultiplier();
            scaled.x = targetBaseScale.x * multiplier;
            scaled.z = targetBaseScale.z * multiplier;
            targetZone.transform.localScale = scaled;
            targetSurfaceOffset = GetTargetSurfaceOffset();
        }

        float GetTargetScaleMultiplier()
        {
            switch (targetDifficulty)
            {
                case TargetDifficulty.Easy:
                    return 1.35f;
                case TargetDifficulty.Hard:
                    return .72f;
                default:
                    return 1f;
            }
        }

        Vector3 GetRandomTargetPoint(Collider targetCollider)
        {
            if (opponentTargetZone.TryGetComponent(out BoxCollider zoneBox))
            {
                Vector3 halfSize = zoneBox.size * .5f;
                Vector3 targetHalfSize = GetTargetHalfSizeInZoneSpace(zoneBox.transform, targetCollider.bounds.extents);
                float netMargin = Mathf.Max(targetNetMargin, targetHalfSize.z);
                float xPadding = Mathf.Min(targetHalfSize.x, halfSize.x);
                float zPadding = Mathf.Min(netMargin, halfSize.z);
                float xMin = zoneBox.center.x - halfSize.x + xPadding;
                float xMax = zoneBox.center.x + halfSize.x - xPadding;
                float zMin = zoneBox.center.z - halfSize.z + zPadding;
                float zMax = zoneBox.center.z + halfSize.z - zPadding;
                float localX = xMin <= xMax ? Random.Range(xMin, xMax) : zoneBox.center.x;
                float localZ = zMin <= zMax ? Random.Range(zMin, zMax) : zoneBox.center.z;
                Vector3 localPoint = new Vector3(localX, zoneBox.center.y, localZ);
                return zoneBox.transform.TransformPoint(localPoint);
            }

            Bounds bounds = opponentTargetZone.GetComponent<Collider>().bounds;
            Vector3 extents = targetCollider.bounds.extents;
            float minX = bounds.min.x + extents.x;
            float maxX = bounds.max.x - extents.x;
            float minZ = bounds.min.z + extents.z;
            float maxZ = bounds.max.z - extents.z;
            return new Vector3(
                minX <= maxX ? Random.Range(minX, maxX) : bounds.center.x,
                bounds.center.y,
                minZ <= maxZ ? Random.Range(minZ, maxZ) : bounds.center.z);
        }

        static Vector3 GetTargetHalfSizeInZoneSpace(Transform zoneTransform, Vector3 worldExtents)
        {
            Vector3 localX = zoneTransform.InverseTransformVector(new Vector3(worldExtents.x, 0f, 0f));
            Vector3 localY = zoneTransform.InverseTransformVector(new Vector3(0f, worldExtents.y, 0f));
            Vector3 localZ = zoneTransform.InverseTransformVector(new Vector3(0f, 0f, worldExtents.z));
            return new Vector3(
                Mathf.Abs(localX.x) + Mathf.Abs(localY.x) + Mathf.Abs(localZ.x),
                Mathf.Abs(localX.y) + Mathf.Abs(localY.y) + Mathf.Abs(localZ.y),
                Mathf.Abs(localX.z) + Mathf.Abs(localY.z) + Mathf.Abs(localZ.z));
        }

        float GetTargetSurfaceOffset()
        {
            if (targetZone == null || tableSurface == null) return .01f;

            Collider targetCollider = targetZone.GetComponent<Collider>();
            if (targetCollider == null) return .01f;

            return Mathf.Max(.001f, targetCollider.bounds.min.y - tableSurface.bounds.max.y);
        }

        // Shared ballistic helper used to solve low-arc serve trajectories.
        static bool TrySolveBallisticArc(Vector3 origin, Vector3 target, float launchSpeed, out Vector3 velocity)
        {
            velocity = default;

            Vector3 toTarget = target - origin;
            Vector3 toTargetXZ = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            float horizontalDistance = toTargetXZ.magnitude;
            if (horizontalDistance < .001f) return false;

            float verticalOffset = toTarget.y;
            float speedSquared = launchSpeed * launchSpeed;
            float gravity = Mathf.Abs(Physics.gravity.y);
            float discriminant = speedSquared * speedSquared - gravity * (gravity * horizontalDistance * horizontalDistance + 2f * verticalOffset * speedSquared);
            if (discriminant <= 0f) return false;

            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float lowArcTangent = (speedSquared - sqrtDiscriminant) / (gravity * horizontalDistance);
            float launchAngle = Mathf.Atan(lowArcTangent);
            Vector3 horizontalDirection = toTargetXZ / horizontalDistance;
            velocity = horizontalDirection * (Mathf.Cos(launchAngle) * launchSpeed) + Vector3.up * (Mathf.Sin(launchAngle) * launchSpeed);
            return true;
        }

        // Rally outcome handling from the ball state machine.
        public void RegisterScore(TrainingBall ball, bool earnedTargetBonus)
        {
            if (ball != activeBall || !ball.WasHitByPlayer || ball.Scored || ball.State == TrainingBallState.Faulted) return;
            ball.MarkScored();
            ReleaseActiveBall(ball);
            Score++;
            if (earnedTargetBonus) Bonuses++;
            Debug.Log($"Valid score awarded. Score={Score}, Bonuses={Bonuses}, target bonus={earnedTargetBonus}.", this);
            MoveTargetZone();
            RefreshStatDisplays();
            AudioClip successClip = earnedTargetBonus && bonusSuccessDing != null
                ? bonusSuccessDing
                : successDing;
            PlayFeedback(successClip);
        }

        public void NotifyServeInPlay(TrainingBall ball)
        {
            if (ball != activeBall || !ball.CountsTowardServe || ball.State == TrainingBallState.Faulted) return;

            Attempts++;
            RefreshStatDisplays();
        }

        public void RegisterFoul(TrainingBall ball, TrainingFoul foul)
        {
            if (ball != activeBall) return;

            if (foul == TrainingFoul.Player)
            {
                Misses++;
                MoveTargetZone();
                RefreshStatDisplays();
                PlayFeedback(failBuzzer);
            }
            else
            {
                PlayFeedback(foulBuzzer);
            }

            ReleaseActiveBall(ball);
        }

        public void Retire(TrainingBall ball)
        {
            if (ball == activeBall && !ball.Scored && ball.State != TrainingBallState.Faulted)
            {
                Misses++;
                MoveTargetZone();
                RefreshStatDisplays();
                PlayFeedback(failBuzzer);
            }

            ReleaseActiveBall(ball);
            ball.MarkRetired();
        }

        public bool IsFloorCollider(Collider collisionCollider)
        {
            if (floorCollisionRoot == null || collisionCollider == null) return false;

            Transform colliderTransform = collisionCollider.transform;
            return colliderTransform == floorCollisionRoot || colliderTransform.IsChildOf(floorCollisionRoot);
        }

        public bool IsNetCollider(Collider collisionCollider)
        {
            if (netSurface == null || collisionCollider == null) return false;

            Transform colliderTransform = collisionCollider.transform;
            return colliderTransform == netSurface.transform || colliderTransform.IsChildOf(netSurface.transform);
        }

        void ReleaseActiveBall(TrainingBall ball)
        {
            if (ball == activeBall)
                activeBall = null;
        }

        // Feedback and display updates.
        void PlayFeedback(AudioClip clip)
        {
            if (feedbackAudioSource != null && clip != null)
                feedbackAudioSource.PlayOneShot(clip);
        }

        void RefreshStatDisplays()
        {
            if (scoreText != null) scoreText.text = Score.ToString();
            if (missesText != null) missesText.text = Misses.ToString();
            if (bonusText != null) bonusText.text = Bonuses.ToString();
            if (serveNumberText != null) serveNumberText.text = GetCurrentServeNumber().ToString();
        }

        int GetCurrentServeNumber()
        {
            if (Attempts == 0) return servesPerRound;
            return servesPerRound - ((Attempts - 1) % servesPerRound);
        }
    }
}
