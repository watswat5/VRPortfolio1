using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Indexing conveyor made from reusable visual/physical segments. Each call to
/// TriggerBelt moves the belt one station, then stops it again.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConveyorBelt : MonoBehaviour
{
    [Header("Belt")]
    [Tooltip("Any number of moving belt links. Resize this list and assign each link once; order does not matter.")]
    [SerializeField] private Transform[] segments = new Transform[0];
    [Tooltip("Belt travel direction, expressed in this object's local space.")]
    [SerializeField] private Vector3 localMoveDirection = Vector3.forward;
    [Tooltip("How far one button press moves the belt. Zero uses Spawn Zone to Stop Zone.")]
    [Min(0f)] [SerializeField] private float distancePerCycle;
    [Tooltip("Distance between neighboring segment centers. Zero infers it from the assigned segments.")]
    [Min(0f)] [SerializeField] private float segmentSpacing;
    [Tooltip("Adds/configures kinematic Rigidbodies so belt colliders can carry other physics objects through friction.")]
    [SerializeField] private bool configureSegmentRigidbodies = true;

    [Header("Segment Recycling")]
    [Tooltip("Optional exit override. When empty, inferred from the starting links and spacing (or the entrance override).")]
    [SerializeField] private Transform exitRecyclePoint;
    [Tooltip("Optional entrance override. When empty, inferred half a link spacing before the first starting link (or from the exit override).")]
    [SerializeField] private Transform entranceRecyclePoint;

    [Header("Motion")]
    [Min(0.01f)] [SerializeField] private float cycleDuration = 2f;
    [Tooltip("Normalized distance over normalized time. This default eases in and out.")]
    [SerializeField] private AnimationCurve accelerationCurve =
        new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("Product Stations")]
    [SerializeField] private Transform spawnZone;
    [SerializeField] private Transform stopZone;
    [SerializeField] private Transform despawnZone;
    [SerializeField] private GameObject productPrefab;
    [Tooltip("Keeps the prefab's authored rotation instead of using the Spawn Zone rotation.")]
    [SerializeField] private bool keepPrefabRotation;
    [Tooltip("Also remove spawned products which have moved past the Despawn Zone plane.")]
    [SerializeField] private bool despawnPastZone = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onCycleStarted = new UnityEvent();
    [SerializeField] private UnityEvent onCycleFinished = new UnityEvent();

    public bool IsMoving { get; private set; }
    public float CycleDistance => ResolveCycleDistance();

    public string SetupProblem
    {
        get
        {
            bool hasSegment = false;
            if (segments != null)
                foreach (Transform segment in segments)
                    if (segment != null) { hasSegment = true; break; }
            if (!hasSegment) return "Assign the moving belt links to Segments.";
            if (distancePerCycle <= Mathf.Epsilon && (spawnZone == null || stopZone == null))
                return "Set Distance Per Cycle above zero, or assign both Spawn Zone and Stop Zone.";
            if (ResolveCycleDistance() <= Mathf.Epsilon)
                return "Stop Zone must be ahead of Spawn Zone along Local Move Direction. Check the axis/sign, or set Distance Per Cycle above zero.";
            return null;
        }
    }

    private readonly List<SpawnedProduct> products = new List<SpawnedProduct>();
    private readonly List<Transform> links = new List<Transform>();
    private float inferredSpacing;
    private Vector3 inferredEntranceLocal;
    private bool initialized;

    private sealed class SpawnedProduct
    {
        public GameObject GameObject;
        public Rigidbody Body;
    }

    private void Awake()
    {
        InitializeLinks();
    }

    private void InitializeLinks()
    {
        links.Clear();
        // Ignore empty slots and duplicate assignments when moving and sizing the loop.
        if (segments != null)
            foreach (Transform segment in segments)
                if (segment != null && !links.Contains(segment)) links.Add(segment);
        inferredSpacing = InferSegmentSpacing(MoveDirectionWorld());
        float spacing = segmentSpacing > Mathf.Epsilon ? segmentSpacing : inferredSpacing;
        inferredEntranceLocal = transform.InverseTransformPoint(InferEntrance(MoveDirectionWorld(), spacing));
        initialized = true;
        if (links.Count > 0 && ResolveLoopLength(MoveDirectionWorld()) <= Mathf.Epsilon)
            Debug.LogWarning($"{nameof(ConveyorBelt)} on {name} cannot infer recycling. Assign separated links, set Segment Spacing, or provide both recycle markers.", this);
        if (!configureSegmentRigidbodies) return;

        for (int i = 0; i < links.Count; i++)
        {
            Transform segment = links[i];
            if (segment == null) continue;

            Rigidbody body = segment.GetComponent<Rigidbody>();
            if (body == null) body = segment.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    /// <summary>Starts one conveyor cycle. Intended for a PhysicsButton UnityEvent.</summary>
    public void TriggerBelt()
    {
        if (!isActiveAndEnabled || IsMoving) return;

        string problem = SetupProblem;
        if (problem != null)
        {
            Debug.LogWarning($"{nameof(ConveyorBelt)} on {name}: {problem}", this);
            return;
        }

        // Script reloads during Play mode discard the runtime cache without calling Awake again.
        if (!initialized) InitializeLinks();
        MineAssembler.RelinquishCurrentDisplayControl();
        StartCoroutine(RunCycle(ResolveCycleDistance()));
    }

    // Short alias that is convenient to find in UnityEvent's method menu.
    public void Trigger() => TriggerBelt();

    private IEnumerator RunCycle(float distance)
    {
        IsMoving = true;
        SpawnProduct();
        onCycleStarted.Invoke();

        float elapsed = 0f;
        float previousDistance = 0f;
        var wait = new WaitForFixedUpdate();

        while (elapsed < cycleDuration)
        {
            yield return wait;
            elapsed = Mathf.Min(elapsed + Time.fixedDeltaTime, cycleDuration);
            float normalizedTime = elapsed / cycleDuration;
            float easedDistance = Mathf.Clamp01(accelerationCurve.Evaluate(normalizedTime)) * distance;
            MoveEverything(easedDistance - previousDistance);
            previousDistance = easedDistance;
        }

        // Curves do not have to end at exactly one; always finish at the requested station.
        MoveEverything(distance - previousDistance);
        DespawnProductsPastZone();
        IsMoving = false;
        onCycleFinished.Invoke();
    }

    private void MoveEverything(float distance)
    {
        if (Mathf.Abs(distance) <= Mathf.Epsilon) return;

        Vector3 direction = MoveDirectionWorld();
        Vector3 delta = direction * distance;
        float loopLength = ResolveLoopLength(direction);
        for (int i = 0; i < links.Count; i++)
        {
            Transform segment = links[i];
            if (segment == null) continue;

            Rigidbody body = segment.GetComponent<Rigidbody>();
            Vector3 target = (body != null && body.isKinematic ? body.position : segment.position) + delta;
            if (RecycleSegment(segment, body, target, direction, loopLength)) continue;
            if (body != null && body.isKinematic) body.MovePosition(target);
            else segment.position = target;
        }

    }

    private bool RecycleSegment(Transform segment, Rigidbody body, Vector3 target, Vector3 direction, float loopLength)
    {
        if (loopLength <= Mathf.Epsilon) return false;

        ResolveRecyclePoints(direction, loopLength, out _, out Vector3 exitPoint);
        float pastExit = Vector3.Dot(target - exitPoint, direction);
        if (pastExit < 0f) return false;

        Vector3 recycledPosition;
        if (entranceRecyclePoint != null)
            recycledPosition = entranceRecyclePoint.position + direction * Mathf.Repeat(pastExit, loopLength);
        else
            recycledPosition = target - direction * (loopLength * (Mathf.Floor(pastExit / loopLength) + 1f));

        // Recycling is intentionally a teleport. Do not MovePosition across the entire loop.
        if (body != null && body.isKinematic) body.position = recycledPosition;
        else segment.position = recycledPosition;
        return true;
    }

    private void SpawnProduct()
    {
        if (productPrefab == null || spawnZone == null) return;

        Quaternion rotation = keepPrefabRotation ? productPrefab.transform.rotation : spawnZone.rotation;
        GameObject instance = Instantiate(productPrefab, spawnZone.position, rotation);
        products.Add(new SpawnedProduct
        {
            GameObject = instance,
            Body = instance.GetComponentInChildren<Rigidbody>()
        });
    }

    private void DespawnProductsPastZone()
    {
        if (!despawnPastZone || despawnZone == null) return;

        Vector3 direction = MoveDirectionWorld();
        for (int i = products.Count - 1; i >= 0; i--)
        {
            SpawnedProduct product = products[i];
            GameObject instance = product.GameObject;
            Vector3 position = product.Body != null ? product.Body.worldCenterOfMass :
                instance != null ? instance.transform.position : Vector3.zero;
            if (instance == null || Vector3.Dot(position - despawnZone.position, direction) >= 0f)
            {
                if (instance != null) Destroy(instance);
                products.RemoveAt(i);
            }
        }
    }

    private float ResolveCycleDistance()
    {
        if (distancePerCycle > Mathf.Epsilon) return distancePerCycle;
        if (spawnZone == null || stopZone == null) return 0f;
        return Mathf.Max(0f, Vector3.Dot(stopZone.position - spawnZone.position, MoveDirectionWorld()));
    }

    private float ResolveLoopLength(Vector3 direction)
    {
        if (entranceRecyclePoint != null && exitRecyclePoint != null)
            return Mathf.Abs(Vector3.Dot(exitRecyclePoint.position - entranceRecyclePoint.position, direction));

        float spacing = segmentSpacing > Mathf.Epsilon ? segmentSpacing : inferredSpacing;
        return spacing * links.Count;
    }

    private Vector3 InferEntrance(Vector3 direction, float spacing)
    {
        Vector3 first = transform.position;
        float minimum = float.PositiveInfinity;
        foreach (Transform link in links)
        {
            float position = Vector3.Dot(link.position - transform.position, direction);
            if (position >= minimum) continue;
            minimum = position;
            first = link.position;
        }
        return first - direction * (spacing * 0.5f);
    }

    private void ResolveRecyclePoints(Vector3 direction, float loopLength, out Vector3 entrance, out Vector3 exit)
    {
        entrance = entranceRecyclePoint != null ? entranceRecyclePoint.position :
            exitRecyclePoint != null ? exitRecyclePoint.position - direction * loopLength :
            transform.TransformPoint(inferredEntranceLocal);
        exit = exitRecyclePoint != null ? exitRecyclePoint.position : entrance + direction * loopLength;
    }

    private float InferSegmentSpacing(Vector3 direction)
    {
        float smallest = float.PositiveInfinity;
        for (int a = 0; a < links.Count; a++)
        for (int b = a + 1; b < links.Count; b++)
        {
            float gap = Mathf.Abs(Vector3.Dot(links[a].position - links[b].position, direction));
            if (gap > 0.0001f) smallest = Mathf.Min(smallest, gap);
        }
        return float.IsPositiveInfinity(smallest) ? 0f : smallest;
    }

    private Vector3 MoveDirectionWorld()
    {
        Vector3 local = localMoveDirection.sqrMagnitude > 0.000001f ? localMoveDirection : Vector3.forward;
        return transform.TransformDirection(local.normalized);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        IsMoving = false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction = MoveDirectionWorld();
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, direction * Mathf.Max(0.25f, ResolveCycleDistance()));
        DrawZone(spawnZone, Color.green);
        DrawZone(stopZone, Color.yellow);
        DrawZone(despawnZone, Color.red);
        DrawZone(entranceRecyclePoint, new Color(0f, 0.7f, 1f));
        DrawZone(exitRecyclePoint, new Color(1f, 0.4f, 0f));
        if (Application.isPlaying && links.Count > 0)
        {
            ResolveRecyclePoints(direction, ResolveLoopLength(direction), out Vector3 entrance, out Vector3 exit);
            Gizmos.color = new Color(0f, 0.7f, 1f);
            Gizmos.DrawWireSphere(entrance, 0.04f);
            Gizmos.color = new Color(1f, 0.4f, 0f);
            Gizmos.DrawWireSphere(exit, 0.04f);
        }
    }

    private static void DrawZone(Transform zone, Color color)
    {
        if (zone == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(zone.position, 0.04f);
    }
}
