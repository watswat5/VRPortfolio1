using System;
using UnityEngine;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Supplies pre-contact motion for a velocity-driven paddle.
    /// Implemented by tracked and AI paddle controllers so hit response can use consistent motion data.
    /// </summary>
    public interface IPaddleMotionSource
    {
        Vector3 LinearVelocity { get; }
        Vector3 AngularVelocity { get; }
    }

    /// <summary>
    /// Applies shared paddle-ball collision response for both player and AI paddles.
    /// It validates legal rally timing, computes outgoing velocity and spin, and notifies listeners of valid hits.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PaddleHitResponder : MonoBehaviour
    {
        // Section: Inspector configuration.
        [Header("Paddle")]
        [SerializeField] TableSide side = TableSide.Player;
        [SerializeField] Rigidbody paddleBody;
        [SerializeField] Collider paddleCollider;
        [Tooltip("Grip rotation pivot used to calculate velocity at the ball contact point.")]
        [SerializeField] Transform centerOfMass;

        [Header("Ball Response")]
        [SerializeField, Range(0f, 1.5f)] float normalRestitution = .82f;
        [SerializeField, Range(0f, 1f)] float tangentialRetention = .78f;
        [SerializeField, Min(0f)] float spinGrip = 28f;
        [SerializeField, Range(0f, 1f)] float angularVelocityTransfer = .10f;

        // Section: Public contract.
        public TableSide Side => side;
        public event Action<TrainingBall> LegalHit;

        // Section: Runtime dependencies.
        IPaddleMotionSource motionSource;

        // Section: Lifecycle setup.
        void Awake()
        {
            if (paddleBody == null) paddleBody = GetComponent<Rigidbody>();
            FindMotionSource();

            if (side == TableSide.None || paddleBody == null || paddleCollider == null || centerOfMass == null)
            {
                Debug.LogError(
                    "Assign Side, Paddle Body, Paddle Collider, and Center Of Mass on PaddleHitResponder.",
                    this);
                enabled = false;
                return;
            }

            paddleBody.isKinematic = false;
            paddleBody.useGravity = false;
            paddleBody.interpolation = RigidbodyInterpolation.Interpolate;
            paddleBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (motionSource == null)
                Debug.LogWarning(
                    "No IPaddleMotionSource found; PaddleHitResponder will use Rigidbody velocity.",
                    this);
        }

        // Section: Collision handling.
        void OnCollisionEnter(Collision collision)
        {
            TrainingBall ball = collision.gameObject.GetComponent<TrainingBall>();
            if (ball == null) return;

            if (!ball.CanBeHitBy(side))
            {
                Debug.Log(
                    $"{side} paddle ignored hit on '{collision.gameObject.name}' while state is {ball.State}.",
                    this);
                return;
            }

            Rigidbody ballBody = collision.rigidbody;
            if (ballBody == null) return;

            Vector3 linearVelocity = motionSource?.LinearVelocity ?? paddleBody.linearVelocity;
            Vector3 angularVelocity = motionSource?.AngularVelocity ?? paddleBody.angularVelocity;
            ContactPoint contact = collision.GetContact(0);
            Vector3 normal = GetPaddleFaceNormal(ballBody.worldCenterOfMass);
            Vector3 centerToContact = contact.point - centerOfMass.position;
            Vector3 paddleContactVelocity = linearVelocity + Vector3.Cross(angularVelocity, centerToContact);
            Vector3 incomingBallVelocity = ballBody.linearVelocity;
            Vector3 relative = incomingBallVelocity - paddleContactVelocity;
            float normalApproachSpeed = Vector3.Dot(relative, normal);

            // PhysX may already have separated the bodies before this callback.
            if (normalApproachSpeed >= 0f)
            {
                Debug.Log(
                    $"{side} paddle registered physics-resolved hit on '{collision.gameObject.name}' | " +
                    $"relative normal speed={normalApproachSpeed:F2} m/s, normal={normal:F3}",
                    this);
                RegisterLegalHit(ball);
                return;
            }

            Vector3 normalRelative = Vector3.Project(relative, normal);
            Vector3 tangentRelative = relative - normalRelative;
            ballBody.linearVelocity = paddleContactVelocity - normalRelative * normalRestitution +
                tangentRelative * tangentialRetention;
            ballBody.angularVelocity += Vector3.Cross(normal, -tangentRelative) * spinGrip +
                angularVelocity * angularVelocityTransfer;

            Debug.Log(
                $"{side} paddle hit '{collision.gameObject.name}' | " +
                $"ball in={incomingBallVelocity:F3} ({incomingBallVelocity.magnitude:F2} m/s), " +
                $"paddle linear={linearVelocity:F3} ({linearVelocity.magnitude:F2} m/s), " +
                $"paddle angular={angularVelocity:F3} ({angularVelocity.magnitude:F2} rad/s), " +
                $"contact velocity={paddleContactVelocity:F3}, normal={normal:F3}, " +
                $"ball out={ballBody.linearVelocity:F3} ({ballBody.linearVelocity.magnitude:F2} m/s)",
                this);

            RegisterLegalHit(ball);
        }

        // Section: Rally integration helpers.
        void RegisterLegalHit(TrainingBall ball)
        {
            if (!ball.RegisterPaddleHit(side)) return;
            LegalHit?.Invoke(ball);
        }

        // Finds the first component that can provide externally tracked paddle motion.
        void FindMotionSource()
        {
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is IPaddleMotionSource source)
                {
                    motionSource = source;
                    return;
                }
            }
        }

        // Section: Contact geometry helper.
        Vector3 GetPaddleFaceNormal(Vector3 ballPosition)
        {
            Vector3 faceNormal = transform.up;

            // Use the broad face of a thin box instead of an unstable edge normal.
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
