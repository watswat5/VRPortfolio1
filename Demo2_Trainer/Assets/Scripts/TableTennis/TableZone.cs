using UnityEngine;

namespace Demo2.TableTennis
{
    public enum TableSide { None, Player, Opponent }

    /// <summary>
    /// Declares which logical side of the table this trigger volume represents.
    /// Used for serve targeting, bounce classification, and score resolution.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TableZone : MonoBehaviour
    {
        // Section: Inspector configuration.
        [SerializeField] TableSide side;

        // Section: Public data.
        public TableSide Side => side;

        // Section: Lifecycle defaults.
        void Reset() { GetComponent<Collider>().isTrigger = true; }

        // This collider is bounds-only. Physical table contacts drive scoring.
    }
}
