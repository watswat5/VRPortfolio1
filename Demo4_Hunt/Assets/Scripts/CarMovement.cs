using UnityEngine;

/// <summary>
/// Moves a car toward a destination. Emissive headlight meshes remain fully lit,
/// while real Light components fade in only when the player is nearby.
/// </summary>
public class CarMovement : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] [SerializeField] private float speed = 10f;
    [Min(0.05f)] [SerializeField] private float despawnDistance = 0.5f;

    [Header("Headlights")]
    [Tooltip("Renderers using a material with Emission enabled. Headlight cards/planes are ideal.")]
    [SerializeField] private Renderer[] headlightRenderers = new Renderer[0];
    [ColorUsage(true, true)] [SerializeField] private Color headlightColor = new Color(4f, 3.5f, 2.5f, 1f);
    [Tooltip("Optional. Leave empty to use only the inexpensive emissive headlight meshes.")]
    [SerializeField] private Light[] realLights = new Light[0];
    [Tooltip("Distance at which the real spotlights reach full intensity.")]
    [Min(0f)] [SerializeField] private float realLightRange = 18f;
    [Tooltip("Distance at which the real spotlights begin fading in.")]
    [Min(0f)] [SerializeField] private float fadeStartDistance = 35f;
    [Min(0.01f)] [SerializeField] private float fadeDuration = 0.35f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock propertyBlock;
    private Transform destination;
    private Transform player;
    private float headlightAmount;
    private float[] originalLightIntensities;
    private bool headlightsDisabled;

    public void Initialize(Transform despawnPoint, Transform playerTransform)
    {
        destination = despawnPoint;
        player = playerTransform;
    }

    /// <summary>Permanently switches off this car's real spotlights.</summary>
    public void DisableHeadlights()
    {
        if (headlightsDisabled)
            return;

        headlightsDisabled = true;
        headlightAmount = 0f;

        foreach (Light carLight in realLights)
        {
            if (carLight != null)
                carLight.enabled = false;
        }
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        originalLightIntensities = new float[realLights.Length];
        for (int i = 0; i < realLights.Length; i++)
        {
            if (realLights[i] == null)
                continue;

            originalLightIntensities[i] = realLights[i].intensity;
            realLights[i].enabled = false;
        }
    }

    private void Update()
    {
        if (destination == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination.position,
            speed * Time.deltaTime);

        float sqrDistanceToEnd = (transform.position - destination.position).sqrMagnitude;
        if (sqrDistanceToEnd <= despawnDistance * despawnDistance)
        {
            Destroy(gameObject);
            return;
        }

        UpdateHeadlights();
    }

    private void UpdateHeadlights()
    {
        if (headlightsDisabled)
            return;

        float distance = player == null
            ? float.PositiveInfinity
            : Vector3.Distance(transform.position, player.position);

        float nearDistance = Mathf.Min(realLightRange, fadeStartDistance);
        float farDistance = Mathf.Max(realLightRange, fadeStartDistance);
        float target = distance >= farDistance
            ? 0f
            : distance <= nearDistance
                ? 1f
                : Mathf.InverseLerp(farDistance, nearDistance, distance);
        headlightAmount = Mathf.MoveTowards(headlightAmount, target, Time.deltaTime / fadeDuration);

        bool useRealLights = headlightAmount > 0.01f;
        for (int i = 0; i < realLights.Length; i++)
        {
            Light carLight = realLights[i];
            if (carLight == null)
                continue;

            carLight.enabled = useRealLights;
            if (useRealLights)
                carLight.intensity = originalLightIntensities[i] * headlightAmount;
        }
    }
}
