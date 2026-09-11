using UnityEngine;

namespace Demo2.TableTennis
{
    public enum TableSide { None, Player, Opponent }

    [RequireComponent(typeof(Collider))]
    public sealed class TableZone : MonoBehaviour
    {
        [SerializeField] TableSide side;

        public TableSide Side => side;

        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            if (side != TableSide.None && other.TryGetComponent(out TrainingBall ball))
                ball.EnterTableZone(side);
        }
    }
}
