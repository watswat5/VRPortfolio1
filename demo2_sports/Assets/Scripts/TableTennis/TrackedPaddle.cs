using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace Demo2.TableTennis
{
    [RequireComponent(typeof(TrackedPoseDriver))]
    public sealed class TrackedPaddle : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] Rigidbody paddleBody;
        [SerializeField] Collider paddleCollider;
        [Tooltip("Controller/grip rotation pivot used to calculate velocity at the ball contact point.")]
        [SerializeField] Transform centerOfMass;

        public Vector3 LinearVelocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        readonly List<XRInputDevice> devices = new List<XRInputDevice>();
        XRInputDevice device;

        void Awake()
        {
            if (paddleBody == null || paddleCollider == null || centerOfMass == null)
            {
                Debug.LogError("Assign Paddle Body, Paddle Collider, and Center Of Mass on TrackedPaddle.", this);
                enabled = false;
                return;
            }
            paddleBody.isKinematic = true;
            paddleBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void Update()
        {
            if (!device.isValid)
            {
                devices.Clear();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
                if (devices.Count > 0) device = devices[0];
            }
            if (!device.isValid) { LinearVelocity = Vector3.zero; AngularVelocity = Vector3.zero; return; }

            if (!device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 localVelocity)) localVelocity = Vector3.zero;
            if (!device.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out Vector3 localAngularVelocity)) localAngularVelocity = Vector3.zero;
            Transform origin = transform.parent;
            LinearVelocity = origin != null ? origin.TransformVector(localVelocity) : localVelocity;
            AngularVelocity = origin != null ? origin.TransformVector(localAngularVelocity) : localAngularVelocity;
        }


        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.gameObject.GetComponent<TrainingBall>();
            if (ball == null) return;
            var rb = collision.rigidbody;
            ContactPoint contact = collision.GetContact(0);
            Vector3 normal = contact.normal;
            Vector3 centerToContact = contact.point - centerOfMass.position;
            Vector3 paddleContactVelocity = LinearVelocity + Vector3.Cross(AngularVelocity, centerToContact);
            Vector3 relative = rb.linearVelocity - paddleContactVelocity;
            if (Vector3.Dot(relative, normal) >= 0) normal = -normal;
            Vector3 normalRelative = Vector3.Project(relative, normal);
            Vector3 tangentRelative = relative - normalRelative;
            rb.linearVelocity = paddleContactVelocity - normalRelative * .82f + tangentRelative * .78f;
            rb.angularVelocity += Vector3.Cross(normal, -tangentRelative) * 28f + AngularVelocity * .10f;
            ball.RegisterPaddleHit();
        }
    }
}
