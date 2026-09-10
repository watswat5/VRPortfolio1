using UnityEngine;

namespace Demo2.TableTennis
{
    public sealed class TargetZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out TrainingBall ball)) ball.EnterTargetZone();
        }
    }
}
