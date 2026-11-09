using UnityEngine;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Provides stateless projectile helpers used by serving and AI return code.
    /// The methods solve low-arc launch vectors and validate net-clearance style vertical obstacle checks.
    /// </summary>
    public static class Ballistics
    {
        // Section: Trajectory solving.
        // Computes a physically valid low-arc launch velocity from origin to target at a fixed speed.
        public static bool TrySolveLowArc(
            Vector3 origin,
            Vector3 target,
            float launchSpeed,
            out Vector3 velocity)
        {
            velocity = default;

            Vector3 toTarget = target - origin;
            Vector3 toTargetXZ = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            float horizontalDistance = toTargetXZ.magnitude;
            if (horizontalDistance < .001f || launchSpeed <= 0f) return false;

            float verticalOffset = toTarget.y;
            float speedSquared = launchSpeed * launchSpeed;
            float gravity = Mathf.Abs(Physics.gravity.y);
            if (gravity < .001f) return false;

            float discriminant = speedSquared * speedSquared - gravity *
                (gravity * horizontalDistance * horizontalDistance +
                 2f * verticalOffset * speedSquared);
            if (discriminant <= 0f) return false;

            float lowArcTangent = (speedSquared - Mathf.Sqrt(discriminant)) /
                (gravity * horizontalDistance);
            float launchAngle = Mathf.Atan(lowArcTangent);
            Vector3 horizontalDirection = toTargetXZ / horizontalDistance;
            velocity = horizontalDirection * (Mathf.Cos(launchAngle) * launchSpeed) +
                Vector3.up * (Mathf.Sin(launchAngle) * launchSpeed);
            return true;
        }

        // Section: Obstacle validation.
        // Samples projectile height where the path crosses an obstacle and checks required clearance.
        public static bool ClearsVerticalObstacle(
            Vector3 origin,
            Vector3 destination,
            Vector3 launchVelocity,
            Bounds obstacleBounds,
            float objectRadius,
            float clearance)
        {
            Vector3 horizontalDelta = Vector3.ProjectOnPlane(destination - origin, Vector3.up);
            float horizontalDistance = horizontalDelta.magnitude;
            float horizontalSpeed = Vector3.ProjectOnPlane(launchVelocity, Vector3.up).magnitude;
            if (horizontalDistance < .0001f || horizontalSpeed < .0001f) return true;

            Vector3 horizontalDirection = horizontalDelta / horizontalDistance;
            float distanceToObstacle = Vector3.Dot(
                Vector3.ProjectOnPlane(obstacleBounds.center - origin, Vector3.up),
                horizontalDirection);
            if (distanceToObstacle <= 0f || distanceToObstacle >= horizontalDistance) return true;

            float timeToObstacle = distanceToObstacle / horizontalSpeed;
            float crossingHeight = origin.y + launchVelocity.y * timeToObstacle +
                .5f * Physics.gravity.y * timeToObstacle * timeToObstacle;
            float requiredHeight = obstacleBounds.max.y + objectRadius + clearance;
            return crossingHeight >= requiredHeight;
        }
    }
}
