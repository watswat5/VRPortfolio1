using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Coordinates multi-lane traffic while the player is inside an active area and
/// permanently completes the traffic game when the player reaches the finish area.
/// </summary>
public class TrafficSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Lane
    {
        public string name = "Lane";
        public Transform spawnPoint;
        public Transform despawnPoint;
    }

    private class ActiveCar
    {
        public CarMovement car;
        public int laneIndex;
        public float closestPlayerDistance = float.PositiveInfinity;
    }

    [Header("Two-Lane Route")]
    [Tooltip("Normally two lanes, facing in opposite directions.")]
    [SerializeField] private Lane[] lanes = new Lane[2];
    [SerializeField] private CarMovement[] carPrefabs = new CarMovement[0];

    [Header("Game Areas")]
    [Tooltip("Transform used for zone, distance, and pass checks.")]
    [SerializeField] private Transform player;
    [Tooltip("Traffic runs while the player is inside any of these colliders.")]
    [SerializeField] private Collider[] activeAreas = new Collider[0];
    [Tooltip("Entering this collider completes the game permanently.")]
    [SerializeField] private Collider completionArea;
    [SerializeField] private UnityEvent onGameCompleted;

    [Header("Traffic Rate")]
    [Min(0.1f)] [SerializeField] private float minimumSpawnInterval = 2.5f;
    [Min(0.1f)] [SerializeField] private float maximumSpawnInterval = 5f;
    [Min(1)] [SerializeField] private int maximumCars = 6;

    [Header("Spacing")]
    [Tooltip("Minimum distance between cars in the same lane.")]
    [Min(0f)] [SerializeField] private float minimumCarSpacing = 14f;
    [SerializeField] private bool avoidSameLaneTwice = true;

    [Header("Headlight Pass Detection")]
    [Tooltip("A car must come at least this close before moving away counts as passing.")]
    [Min(0f)] [SerializeField] private float headlightPassDistance = 35f;
    [Tooltip("Distance the car must move away from its closest point before its spotlights switch off.")]
    [Min(0f)] [SerializeField] private float passedDistanceMargin = 0.5f;

    private readonly List<ActiveCar> activeCars = new List<ActiveCar>();
    private Coroutine spawnRoutine;
    private int previousLane = -1;
    private bool gameCompleted;

    public bool GameCompleted => gameCompleted;

    private void Update()
    {
        RemoveDestroyedCars();

        if (player == null)
            return;

        DisableHeadlightsPastPlayer();

        // Completion permanently stops spawning, but cars already on the road
        // continue toward their normal despawn points.
        if (gameCompleted)
            return;

        if (IsInside(completionArea, player.position))
        {
            CompleteGame();
            return;
        }

        bool shouldRun = IsInsideAny(activeAreas, player.position);
        if (shouldRun && spawnRoutine == null)
            StartTraffic();
        else if (!shouldRun && spawnRoutine != null)
            StopTraffic();
    }

    private void DisableHeadlightsPastPlayer()
    {
        foreach (ActiveCar activeCar in activeCars)
        {
            if (activeCar.car == null)
                continue;

            float distance = Vector3.Distance(activeCar.car.transform.position, player.position);
            if (distance < activeCar.closestPlayerDistance)
            {
                activeCar.closestPlayerDistance = distance;
                continue;
            }

            // Latch the spotlights off after a nearby car reaches its closest
            // approach and has clearly started moving away again.
            if (activeCar.closestPlayerDistance <= headlightPassDistance &&
                distance >= activeCar.closestPlayerDistance + passedDistanceMargin)
                activeCar.car.DisableHeadlights();
        }
    }

    private void OnDisable()
    {
        StopTraffic();
    }

    public void StartTraffic()
    {
        if (!gameCompleted && spawnRoutine == null)
            spawnRoutine = StartCoroutine(SpawnTraffic());
    }

    public void StopTraffic()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        previousLane = -1;
    }

    public void CompleteGame()
    {
        if (gameCompleted)
            return;

        gameCompleted = true;
        StopTraffic();
        onGameCompleted?.Invoke();
    }

    private static bool IsInside(Collider area, Vector3 point)
    {
        if (area == null || !area.enabled || !area.gameObject.activeInHierarchy)
            return false;

        return (area.ClosestPoint(point) - point).sqrMagnitude < 0.000001f;
    }

    private static bool IsInsideAny(Collider[] areas, Vector3 point)
    {
        if (areas == null)
            return false;

        foreach (Collider area in areas)
        {
            if (IsInside(area, point))
                return true;
        }

        return false;
    }

    private IEnumerator SpawnTraffic()
    {
        while (true)
        {
            RemoveDestroyedCars();
            TrySpawnCar();

            float low = Mathf.Min(minimumSpawnInterval, maximumSpawnInterval);
            float high = Mathf.Max(minimumSpawnInterval, maximumSpawnInterval);
            yield return new WaitForSeconds(Random.Range(low, high));
        }

    }

    private void TrySpawnCar()
    {
        if (activeCars.Count >= maximumCars || lanes == null || lanes.Length == 0 ||
            carPrefabs == null || carPrefabs.Length == 0)
            return;

        int start = Random.Range(0, lanes.Length);
        for (int offset = 0; offset < lanes.Length; offset++)
        {
            int laneIndex = (start + offset) % lanes.Length;
            if (avoidSameLaneTwice && lanes.Length > 1 && laneIndex == previousLane)
                continue;

            if (CanSpawnInLane(laneIndex))
            {
                SpawnCar(laneIndex);
                return;
            }
        }

        // If alternating blocked the only safe lane, allow it rather than making
        // traffic pause for an unnaturally long time.
        for (int offset = 0; offset < lanes.Length; offset++)
        {
            int laneIndex = (start + offset) % lanes.Length;
            if (CanSpawnInLane(laneIndex))
            {
                SpawnCar(laneIndex);
                return;
            }
        }
    }

    private bool CanSpawnInLane(int laneIndex)
    {
        Lane lane = lanes[laneIndex];
        if (lane == null || lane.spawnPoint == null || lane.despawnPoint == null)
            return false;

        float spacingSqr = minimumCarSpacing * minimumCarSpacing;
        foreach (ActiveCar activeCar in activeCars)
        {
            if (activeCar.car != null && activeCar.laneIndex == laneIndex &&
                (activeCar.car.transform.position - lane.spawnPoint.position).sqrMagnitude < spacingSqr)
                return false;
        }

        return true;
    }

    private void SpawnCar(int laneIndex)
    {
        Lane lane = lanes[laneIndex];
        CarMovement prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
        if (prefab == null)
            return;

        // Preserve the model-space rotation authored on the prefab (the vehicle
        // prefabs use a 90-degree Y correction) while still allowing each lane's
        // spawn point to provide its own direction.
        Quaternion spawnRotation = lane.spawnPoint.rotation * prefab.transform.rotation *
            Quaternion.Euler(0f, 180f, 0f);
        CarMovement car = Instantiate(prefab, lane.spawnPoint.position, spawnRotation);
        car.Initialize(lane.despawnPoint, player);
        activeCars.Add(new ActiveCar { car = car, laneIndex = laneIndex });
        previousLane = laneIndex;
    }

    private void RemoveDestroyedCars()
    {
        activeCars.RemoveAll(activeCar => activeCar.car == null);
    }

    private void OnValidate()
    {
        maximumSpawnInterval = Mathf.Max(minimumSpawnInterval, maximumSpawnInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (lanes == null)
            return;

        Color[] colors = { Color.cyan, Color.yellow };
        for (int i = 0; i < lanes.Length; i++)
        {
            Lane lane = lanes[i];
            if (lane == null || lane.spawnPoint == null || lane.despawnPoint == null)
                continue;

            Gizmos.color = colors[i % colors.Length];
            Gizmos.DrawLine(lane.spawnPoint.position, lane.despawnPoint.position);
            Gizmos.DrawWireSphere(lane.spawnPoint.position, 0.5f);
            Gizmos.DrawWireCube(lane.despawnPoint.position, Vector3.one);
        }
    }
}
