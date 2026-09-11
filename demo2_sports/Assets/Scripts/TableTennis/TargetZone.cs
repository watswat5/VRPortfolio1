using UnityEngine;

namespace Demo2.TableTennis
{
    [RequireComponent(typeof(Collider))]
    public sealed class TargetZone : MonoBehaviour
    {
        void Awake() { GetComponent<Collider>().isTrigger = true; }
        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out TrainingBall ball)) ball.EnterTargetZone();
        }
    }
}
