using UnityEngine;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Centralizes court geometry references used by gameplay scripts.
    /// It validates required colliders and classifies contacts as table side, net, or floor.
    /// </summary>
    public sealed class TableCourt : MonoBehaviour
    {
        // Section: Inspector references.
        [SerializeField] Collider tableSurface;
        [SerializeField] Transform floorCollisionRoot;
        [SerializeField] TableZone playerZone;
        [SerializeField] TableZone opponentZone;
        [SerializeField] Collider netSurface;

        // Section: Shared accessors.
        public Collider TableSurface => tableSurface;
        public TableZone PlayerZone => playerZone;
        public TableZone OpponentZone => opponentZone;
        public Collider NetSurface => netSurface;
        public float SurfaceHeight => tableSurface != null ? tableSurface.bounds.max.y : 0f;

        // Section: Reference validation.
        public bool ValidateReferences()
        {
            bool valid = true;
            if (tableSurface == null)
            {
                Debug.LogError("Assign the table surface collider on TableCourt.", this);
                valid = false;
            }
            else if (tableSurface.isTrigger)
            {
                Debug.LogError("Table Surface must use a non-trigger collider.", tableSurface);
                valid = false;
            }

            if (floorCollisionRoot == null)
                Debug.LogWarning("Assign Floor Collision Root to identify floor contacts explicitly.", this);

            if (!ValidateZone(playerZone, TableSide.Player, "Player Zone")) valid = false;
            if (!ValidateZone(opponentZone, TableSide.Opponent, "Opponent Zone")) valid = false;

            if (netSurface == null)
            {
                Debug.LogError("Assign the solid net collider on TableCourt.", this);
                valid = false;
            }
            else if (netSurface.isTrigger)
            {
                Debug.LogError("Net Surface must reference a non-trigger collider.", netSurface);
                valid = false;
            }

            return valid;
        }

        // Section: Contact classification.
        // Identifies floor contact either by explicit root parenting or a height fallback.
        public bool IsFloorCollider(Collider collisionCollider)
        {
            if (collisionCollider == null) return false;

            if (floorCollisionRoot != null)
            {
                Transform colliderTransform = collisionCollider.transform;
                if (colliderTransform == floorCollisionRoot || colliderTransform.IsChildOf(floorCollisionRoot))
                    return true;
            }

            // Covers floor pieces that have not been grouped under Floor Collision Root.
            return tableSurface != null && collisionCollider != tableSurface &&
                collisionCollider.bounds.max.y < tableSurface.bounds.max.y - .05f;
        }

        // Checks whether a collider belongs to the configured net surface hierarchy.
        public bool IsNetCollider(Collider collisionCollider)
        {
            if (netSurface == null || collisionCollider == null) return false;

            Transform colliderTransform = collisionCollider.transform;
            return colliderTransform == netSurface.transform || colliderTransform.IsChildOf(netSurface.transform);
        }

        // Maps a table-surface collision point to the nearest logical table side.
        public bool TryGetTableSide(Collider collisionCollider, Vector3 contactPoint, out TableSide side)
        {
            side = TableSide.None;
            if (tableSurface == null || collisionCollider == null ||
                (collisionCollider != tableSurface && collisionCollider.transform != tableSurface.transform) ||
                playerZone == null || opponentZone == null)
                return false;

            Collider playerBounds = playerZone.GetComponent<Collider>();
            Collider opponentBounds = opponentZone.GetComponent<Collider>();
            if (playerBounds == null || opponentBounds == null) return false;

            Vector3 playerCenter = playerBounds.bounds.center;
            Vector3 opponentCenter = opponentBounds.bounds.center;
            playerCenter.y = contactPoint.y;
            opponentCenter.y = contactPoint.y;
            side = (contactPoint - playerCenter).sqrMagnitude <=
                (contactPoint - opponentCenter).sqrMagnitude
                ? TableSide.Player
                : TableSide.Opponent;
            return true;
        }

        // Section: Private helpers.
        static bool ValidateZone(TableZone zone, TableSide expectedSide, string label)
        {
            if (zone == null)
            {
                Debug.LogError($"Assign {label} on TableCourt.");
                return false;
            }

            if (zone.Side != expectedSide)
            {
                Debug.LogError($"{label} must use TableSide.{expectedSide}.", zone);
                return false;
            }

            if (!zone.TryGetComponent(out Collider zoneCollider) || !zoneCollider.isTrigger)
            {
                Debug.LogError($"{label} needs a trigger collider for its bounds.", zone);
                return false;
            }

            return true;
        }
    }
}
