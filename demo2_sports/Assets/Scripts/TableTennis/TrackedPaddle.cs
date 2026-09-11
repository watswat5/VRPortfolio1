using UnityEngine;

namespace Demo2.TableTennis
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TrackedPaddle : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Transform driven by the right-hand Input System Tracked Pose Driver.")]
        [SerializeField] Transform trackingTarget;
        [SerializeField] Rigidbody paddleBody;
        [SerializeField] Collider paddleCollider;
        [Tooltip("Controller/grip rotation pivot used to calculate velocity at the ball contact point.")]
        [SerializeField] Transform centerOfMass;

        [Header("Physics Tracking")]
        [Tooltip("Maximum speed used while catching up to the tracked hand.")]
        [SerializeField, Min(0.1f)] float maximumTrackingSpeed = 12f;
        [Tooltip("Maximum angular speed used while catching up to the tracked hand.")]
        [SerializeField, Min(0.1f)] float maximumTrackingAngularSpeed = 60f;

        public Vector3 LinearVelocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }

        void Awake()
        {
            if (trackingTarget == null || paddleBody == null || paddleCollider == null || centerOfMass == null)
            {
                Debug.LogError("Assign Tracking Target, Paddle Body, Paddle Collider, and Center Of Mass on TrackedPaddle.", this);
                enabled = false;
                return;
            }
            paddleBody.isKinematic = false;
            paddleBody.useGravity = false;
            paddleBody.interpolation = RigidbodyInterpolation.Interpolate;
            paddleBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            paddleBody.maxAngularVelocity = maximumTrackingAngularSpeed;
        }

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


        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.gameObject.GetComponent<TrainingBall>();
            if (ball == null) return;

            if (ball.WasHitByPlayer)
            {
                Debug.Log($"Paddle ignored additional hit on '{collision.gameObject.name}'", this);
                return;
            }

            var rb = collision.rigidbody;
            if (rb == null) return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 normal = GetPaddleFaceNormal(rb.worldCenterOfMass);
            Vector3 centerToContact = contact.point - centerOfMass.position;
            Vector3 paddleContactVelocity = LinearVelocity + Vector3.Cross(AngularVelocity, centerToContact);
            Vector3 incomingBallVelocity = rb.linearVelocity;
            Vector3 relative = incomingBallVelocity - paddleContactVelocity;
            float normalApproachSpeed = Vector3.Dot(relative, normal);

            // Collision callbacks run after PhysX resolves the contact, so a positive
            // value often means PhysX already bounced the ball. It is still a legal
            // player hit and must be registered for scoring.
            if (normalApproachSpeed >= 0f)
            {
                Debug.Log(
                    $"Paddle registered physics-resolved hit on '{collision.gameObject.name}' | " +
                    $"relative normal speed={normalApproachSpeed:F2} m/s, normal={normal:F3}",
                    this);
                RegisterLegalHit(ball);
                return;
            }

            Vector3 normalRelative = Vector3.Project(relative, normal);
            Vector3 tangentRelative = relative - normalRelative;
            rb.linearVelocity = paddleContactVelocity - normalRelative * .82f + tangentRelative * .78f;
            rb.angularVelocity += Vector3.Cross(normal, -tangentRelative) * 28f + AngularVelocity * .10f;

            Debug.Log(
                $"Paddle hit '{collision.gameObject.name}' | " +
                $"ball in={incomingBallVelocity:F3} ({incomingBallVelocity.magnitude:F2} m/s), " +
                $"paddle linear={LinearVelocity:F3} ({LinearVelocity.magnitude:F2} m/s), " +
                $"paddle angular={AngularVelocity:F3} ({AngularVelocity.magnitude:F2} rad/s), " +
                $"contact velocity={paddleContactVelocity:F3}, normal={normal:F3}, " +
                $"ball out={rb.linearVelocity:F3} ({rb.linearVelocity.magnitude:F2} m/s)",
                this);

            RegisterLegalHit(ball);
        }

        void RegisterLegalHit(TrainingBall ball)
        {
            ball.RegisterPaddleHit();

            // A legal volley allows one player strike. Ignoring this pair also stops
            // PhysX from applying an unlogged second impulse after the boolean gate.
            foreach (Collider ballCollider in ball.GetComponents<Collider>())
                Physics.IgnoreCollision(paddleCollider, ballCollider, true);
        }

        Vector3 GetPaddleFaceNormal(Vector3 ballPosition)
        {
            Vector3 faceNormal = transform.up;

            // Use the broad face of a thin box rather than an unstable edge/corner
            // contact normal supplied by the physics engine.
            if (paddleCollider is BoxCollider box)
            {
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                if (size.x <= size.y && size.x <= size.z)
                    faceNormal = box.transform.right;
                else if (size.z <= size.x && size.z <= size.y)
                    faceNormal = box.transform.forward;
                else
                    faceNormal = box.transform.up;
            }

            if (Vector3.Dot(ballPosition - paddleCollider.bounds.center, faceNormal) < 0f)
                faceNormal = -faceNormal;
            return faceNormal.normalized;
        }
    }
}
