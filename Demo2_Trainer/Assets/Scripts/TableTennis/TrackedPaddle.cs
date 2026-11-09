using UnityEngine;
using UnityEngine.XR;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Converts tracked XR hand pose into physically simulated paddle motion.
    /// It exposes measured linear and angular velocity for shared hit-response logic and triggers hit haptics.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(PaddleHitResponder))]
    public sealed class TrackedPaddle : MonoBehaviour, IPaddleMotionSource
    {
        // Section: Inspector configuration.
        [Header("Scene References")]
        [Tooltip("Transform driven by the right-hand Input System Tracked Pose Driver.")]
        [SerializeField] Transform trackingTarget;
        [SerializeField] Rigidbody paddleBody;
        [SerializeField] PaddleHitResponder hitResponder;

        [Header("Physics Tracking")]
        [Tooltip("Maximum speed used while catching up to the tracked hand.")]
        [SerializeField, Min(0.1f)] float maximumTrackingSpeed = 12f;
        [Tooltip("Maximum angular speed used while catching up to the tracked hand.")]
        [SerializeField, Min(0.1f)] float maximumTrackingAngularSpeed = 60f;

        [Header("Haptics")]
        [SerializeField, Range(0f, 1f)] float hitHapticStrength = .45f;
        [SerializeField, Min(0f)] float hitHapticDuration = .06f;

        // Section: Public motion feed.
        public Vector3 LinearVelocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }

        // Section: Lifecycle and event wiring.
        void Awake()
        {
            if (paddleBody == null) paddleBody = GetComponent<Rigidbody>();
            if (hitResponder == null) hitResponder = GetComponent<PaddleHitResponder>();

            if (trackingTarget == null || paddleBody == null || hitResponder == null)
            {
                Debug.LogError("Assign Tracking Target, Paddle Body, and Hit Responder on TrackedPaddle.", this);
                enabled = false;
                return;
            }
            paddleBody.maxAngularVelocity = maximumTrackingAngularSpeed;
        }

        void OnEnable()
        {
            if (hitResponder == null) hitResponder = GetComponent<PaddleHitResponder>();
            if (hitResponder != null) hitResponder.LegalHit += HandleLegalHit;
        }

        void OnDisable()
        {
            if (hitResponder != null) hitResponder.LegalHit -= HandleLegalHit;
        }

        // Section: Physics tracking.
        // Drive the paddle from the tracked hand pose while keeping it physically collidable.
        void FixedUpdate()
        {
            float step = Time.fixedDeltaTime;
            LinearVelocity = Vector3.ClampMagnitude(
                (trackingTarget.position - paddleBody.position) / step,
                maximumTrackingSpeed);

            Quaternion rotationDelta = trackingTarget.rotation * Quaternion.Inverse(paddleBody.rotation);
            if (rotationDelta.w < 0f)
                rotationDelta = new Quaternion(-rotationDelta.x, -rotationDelta.y, -rotationDelta.z, -rotationDelta.w);
            rotationDelta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f) angleDegrees -= 360f;
            AngularVelocity = Vector3.ClampMagnitude(
                axis * (angleDegrees * Mathf.Deg2Rad / step),
                maximumTrackingAngularSpeed);

            // Driving a dynamic body by velocity makes its full path visible to
            // continuous collision detection instead of teleporting its Transform.
            paddleBody.linearVelocity = LinearVelocity;
            paddleBody.angularVelocity = AngularVelocity;
        }

        // Section: Haptic feedback.
        void HandleLegalHit(TrainingBall _) => PlayHitHaptic();

        void PlayHitHaptic()
        {
            InputDevice controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (controller.TryGetHapticCapabilities(out HapticCapabilities capabilities) &&
                capabilities.supportsImpulse)
            {
                controller.SendHapticImpulse(0, hitHapticStrength, hitHapticDuration);
            }
        }
    }
}
