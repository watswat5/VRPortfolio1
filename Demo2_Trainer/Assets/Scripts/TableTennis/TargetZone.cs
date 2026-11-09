using UnityEngine;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Trigger volume that awards a temporary bonus flag on the active ball.
    /// The bonus is consumed only when the player wins the current rally outcome.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TargetZone : MonoBehaviour
    {
        // Section: Trigger setup.
        void Awake() { GetComponent<Collider>().isTrigger = true; }
        void Reset() { GetComponent<Collider>().isTrigger = true; }

        // Section: Bonus routing.
        // Hands target-enter events to TrainingBall so rally logic can resolve bonus scoring.
        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out TrainingBall ball)) ball.EnterTargetZone();
        }
    }
}
