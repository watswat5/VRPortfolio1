using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Demo2.TableTennis
{
    public enum TargetDifficulty { Easy, Medium, Hard }

    /// <summary>
    /// Owns serve orchestration for the trainer: timing, spawn/reset lifecycle, preview arrows,
    /// legal serve velocity generation, and opponent-side target movement and scaling.
    /// </summary>
    [RequireComponent(typeof(TableCourt))]
    public sealed class ServeController : MonoBehaviour
    {
        const int ServeVelocitySampleCount = 20;

        // Section: Inspector configuration.
        [Header("Scene References")]
        [SerializeField] TrainingBall ballPrefab;
        [SerializeField] Transform ballSpawn;
        [SerializeField] TableCourt court;
        [SerializeField] TargetZone targetZone;
        [SerializeField] AudioSource serveAudioSource;
        [SerializeField] AudioClip startBuzzer;

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

        // Section: Shared accessors.
        public TableCourt Court => court;
        public TrainingBall ActiveBall => activeBall;

        // Section: Runtime state.
        VRTableTennisTrainer trainer;
        TrainingBall activeBall;
        Vector3 preparedServeVelocity;
        Vector3 preparedServeSpin;
        LineRenderer servePreviewArrow;
        LineRenderer servePreviewArrowHead;
        Material runtimePreviewArrowMaterial;
        Vector3 targetBaseScale;
        float targetSurfaceOffset;
        bool initialized;
        bool serving;

        // Section: Lifecycle.
        void Awake()
        {
            if (court == null) court = GetComponent<TableCourt>();
            ConfigureAudio();
        }

        void OnDisable()
        {
            StopServing();
        }

        void OnDestroy()
        {
            if (servePreviewArrow != null) Destroy(servePreviewArrow.gameObject);
            if (servePreviewArrowHead != null) Destroy(servePreviewArrowHead.gameObject);
            if (runtimePreviewArrowMaterial != null) Destroy(runtimePreviewArrowMaterial);
        }

        // Section: Public control API.
        public bool ValidateReferences()
        {
            bool valid = true;
            if (ballPrefab == null) { Debug.LogError("Assign Ball Prefab on ServeController.", this); valid = false; }
            if (ballSpawn == null) { Debug.LogError("Assign Ball Spawn on ServeController.", this); valid = false; }
            if (court == null) { Debug.LogError("Assign Table Court on ServeController.", this); valid = false; }
            else if (!court.ValidateReferences()) valid = false;
            if (targetZone == null) { Debug.LogError("Assign Target Zone on ServeController.", this); valid = false; }
            else if (targetZone.GetComponent<Collider>() == null)
            {
                Debug.LogError("Target Zone needs a Collider so it can be positioned and triggered.", targetZone);
                valid = false;
            }
            return valid;
        }

        public void Initialize(VRTableTennisTrainer owningTrainer)
        {
            trainer = owningTrainer;
            if (initialized) return;

            initialized = true;
            targetBaseScale = targetZone.transform.localScale;
            targetSurfaceOffset = GetTargetSurfaceOffset();
            MoveTargetZone();
        }

        public void BeginServing()
        {
            if (!initialized || serving) return;
            StartCoroutine(ServeLoop());
        }

        public void StopServing()
        {
            StopAllCoroutines();
            serving = false;
            SetServePreviewVisible(false);
            if (serveAudioSource != null) serveAudioSource.Stop();
        }

        public int ResetAndRestart()
        {
            StopServing();
            activeBall = null;

            TrainingBall[] balls = FindObjectsByType<TrainingBall>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (TrainingBall ball in balls)
                Destroy(ball.gameObject);

            MoveTargetZone();
            if (isActiveAndEnabled && trainer != null && trainer.isActiveAndEnabled)
                StartCoroutine(RestartAfterDestroyedBalls());
            return balls.Length;
        }

        public bool IsActiveBall(TrainingBall ball) => ball != null && ball == activeBall;

        public void ReleaseBall(TrainingBall ball)
        {
            if (ball == activeBall) activeBall = null;
        }

        public int GetServeNumber(int attempts)
        {
            if (attempts == 0) return servesPerRound;
            return servesPerRound - ((attempts - 1) % servesPerRound);
        }

        // Section: Target movement.
        public void MoveTargetZone()
        {
            if (targetZone == null || court == null || court.OpponentZone == null || court.TableSurface == null) return;

            Collider targetCollider = targetZone.GetComponent<Collider>();
            if (targetCollider == null) return;

            ApplyTargetDifficultyScale();
            Vector3 targetPoint = GetRandomTargetPoint(targetCollider);
            targetPoint.y = court.SurfaceHeight + targetSurfaceOffset;
            targetZone.transform.position = targetPoint;
        }

        IEnumerator RestartAfterDestroyedBalls()
        {
            yield return null;
            BeginServing();
        }

        // Section: Serve loop.
        IEnumerator ServeLoop()
        {
            serving = true;
            bool firstServe = true;
            while (enabled && trainer != null && trainer.isActiveAndEnabled)
            {
                if (activeBall == null)
                {
                    PrepareServe();
                    yield return new WaitForSeconds(firstServe ? initialDelay : delayBetweenServes);
                    PlayStartBuzzer();
                    yield return new WaitForSeconds(startDelayAfterBuzzer);
                    LaunchPreparedServe();
                    firstServe = false;
                }
                yield return null;
            }
            serving = false;
        }

        // Section: Serve preparation and launch.
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
            if (activeBall != null)
                activeBall.Launch(trainer, preparedServeVelocity, preparedServeSpin);
        }

        // Section: Serve trajectory selection.
        Vector3 GetServeVelocity()
        {
            if (TryGetServeVelocity(out Vector3 velocity)) return velocity;
            if (TryGetFallbackServeVelocity(out velocity)) return velocity;

            Vector3 fallbackDirection = new Vector3(0f, -.18f, 1f).normalized;
            return ballSpawn.TransformDirection(fallbackDirection) * minimumServeSpeed;
        }

        bool TryGetServeVelocity(out Vector3 velocity)
        {
            velocity = default;
            if (court == null || court.PlayerZone == null || court.TableSurface == null || court.NetSurface == null)
                return false;

            for (int attempt = 0; attempt < ServeVelocitySampleCount; attempt++)
            {
                Vector3 bouncePoint = GetRandomBouncePoint();
                float launchSpeed = Random.Range(minimumServeSpeed, maximumServeSpeed);
                if (TryGetClearVelocity(bouncePoint, launchSpeed, out velocity)) return true;
            }
            return false;
        }

        bool TryGetFallbackServeVelocity(out Vector3 velocity)
        {
            velocity = default;
            if (court == null || court.PlayerZone == null || court.TableSurface == null || court.NetSurface == null)
                return false;

            Vector3 bouncePoint = GetRandomBouncePoint();
            float[] fallbackSpeeds = { maximumServeSpeed, maximumServeSpeed + .35f, maximumServeSpeed + .7f };
            foreach (float launchSpeed in fallbackSpeeds)
                if (TryGetClearVelocity(bouncePoint, launchSpeed, out velocity)) return true;
            return false;
        }

        bool TryGetClearVelocity(Vector3 bouncePoint, float launchSpeed, out Vector3 velocity)
        {
            return Ballistics.TrySolveLowArc(ballSpawn.position, bouncePoint, launchSpeed, out velocity) &&
                Ballistics.ClearsVerticalObstacle(
                    ballSpawn.position,
                    bouncePoint,
                    velocity,
                    court.NetSurface.bounds,
                    GetBallRadius(),
                    serveNetClearance);
        }

        // Section: Serve bounce geometry.
        Vector3 GetRandomBouncePoint()
        {
            float ballRadius = GetBallRadius();
            if (court.PlayerZone.TryGetComponent(out BoxCollider zoneBox))
            {
                Vector3 halfSize = zoneBox.size * .5f;
                float xMin = zoneBox.center.x - halfSize.x + zoneBox.size.x * serveLengthMargin;
                float xMax = zoneBox.center.x + halfSize.x - zoneBox.size.x * serveLengthMargin;
                float zMin = zoneBox.center.z - halfSize.z + zoneBox.size.z * serveWidthMargin;
                float zMax = zoneBox.center.z + halfSize.z - zoneBox.size.z * serveWidthMargin;
                Vector3 localPoint = new Vector3(Random.Range(xMin, xMax), zoneBox.center.y, Random.Range(zMin, zMax));
                Vector3 worldPoint = zoneBox.transform.TransformPoint(localPoint);
                worldPoint.y = court.SurfaceHeight + ballRadius;
                return worldPoint;
            }

            Bounds bounds = court.PlayerZone.GetComponent<Collider>().bounds;
            return new Vector3(
                Random.Range(bounds.min.x + bounds.size.x * serveLengthMargin, bounds.max.x - bounds.size.x * serveLengthMargin),
                court.SurfaceHeight + ballRadius,
                Random.Range(bounds.min.z + bounds.size.z * serveWidthMargin, bounds.max.z - bounds.size.z * serveWidthMargin));
        }

        // Section: Target scaling and placement helpers.
        float GetBallRadius()
        {
            SphereCollider sphereCollider = ballPrefab.GetComponent<SphereCollider>();
            if (sphereCollider == null) return .02f;
            Vector3 scale = ballPrefab.transform.localScale;
            return sphereCollider.radius * Mathf.Max(scale.x, scale.y, scale.z);
        }

        void ApplyTargetDifficultyScale()
        {
            Vector3 scaled = targetBaseScale;
            float multiplier = targetDifficulty switch
            {
                TargetDifficulty.Easy => 1.35f,
                TargetDifficulty.Hard => .72f,
                _ => 1f
            };
            scaled.x = targetBaseScale.x * multiplier;
            scaled.z = targetBaseScale.z * multiplier;
            targetZone.transform.localScale = scaled;
            targetSurfaceOffset = GetTargetSurfaceOffset();
        }

        Vector3 GetRandomTargetPoint(Collider targetCollider)
        {
            if (court.OpponentZone.TryGetComponent(out BoxCollider zoneBox))
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
                return zoneBox.transform.TransformPoint(new Vector3(localX, zoneBox.center.y, localZ));
            }

            Bounds bounds = court.OpponentZone.GetComponent<Collider>().bounds;
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

        // Section: Preview rendering and audio.
        float GetTargetSurfaceOffset()
        {
            if (targetZone == null || court == null || court.TableSurface == null) return .01f;
            Collider targetCollider = targetZone.GetComponent<Collider>();
            if (targetCollider == null) return .01f;
            return Mathf.Max(.001f, targetCollider.bounds.min.y - court.SurfaceHeight);
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
                Vector3 point = start + preparedServeVelocity * time + .5f * Physics.gravity * time * time;
                servePreviewArrow.SetPosition(i, point);
            }

            float endTime = previewArrowLengthScale;
            float previousTime = previewArrowLengthScale * (segmentCount - 1f) / segmentCount;
            Vector3 end = start + preparedServeVelocity * endTime + .5f * Physics.gravity * endTime * endTime;
            Vector3 previous = start + preparedServeVelocity * previousTime + .5f * Physics.gravity * previousTime * previousTime;
            Vector3 direction = (end - previous).normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < .001f) side = Vector3.Cross(direction, Vector3.right);
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

            servePreviewArrow = new GameObject("Serve Preview Arrow").AddComponent<LineRenderer>();
            ConfigurePreviewLine(servePreviewArrow);
            servePreviewArrowHead = new GameObject("Serve Preview Arrow Head").AddComponent<LineRenderer>();
            ConfigurePreviewLine(servePreviewArrowHead);

            if (previewArrowMaterial != null)
            {
                servePreviewArrow.sharedMaterial = previewArrowMaterial;
                servePreviewArrowHead.sharedMaterial = previewArrowMaterial;
                return;
            }

            Shader shader = Resources.Load<Shader>("ServePreviewOverlay");
            if (shader == null) shader = Shader.Find("Demo2/Serve Preview Overlay");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
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
            line.sortingOrder = 100;
        }

        void ConfigureAudio()
        {
            if (serveAudioSource == null) return;
            serveAudioSource.spatialBlend = 1f;
            serveAudioSource.spatialize = true;
            serveAudioSource.playOnAwake = false;
        }

        void PlayStartBuzzer()
        {
            if (serveAudioSource != null && startBuzzer != null)
                serveAudioSource.PlayOneShot(startBuzzer);
        }
    }
}
