using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows a lighter in place of the right-hand visual while the A button is held.
/// Attach this to the tracked Right Hand object.
/// </summary>
public class LighterController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject lighterRoot;
    [SerializeField] private Light pointLight;
    [SerializeField] private Transform flameTransform;
    [SerializeField] private AudioSource lightAudioSource;

    [Header("Flame Flicker")]
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.15f;
    [SerializeField, Min(0f)] private float flickerSpeed = 12f;
    [SerializeField, Range(0f, 0.5f)] private float flameScaleAmount = 0.16f;
    [SerializeField, Range(0f, 1f)] private float flameSway = 0.12f;
    [SerializeField, Range(0f, 20f)] private float flameTilt = 7f;

    private InputAction lighterAction;
    private Renderer[] handRenderers;
    private float baseIntensity;
    private float noiseOffset;
    private bool isLit;
    private Vector3 baseFlamePosition;
    private Vector3 baseFlameScale;
    private Quaternion baseFlameRotation;

    private void Awake()
    {
        lighterAction = new InputAction(
            "Hold Lighter",
            InputActionType.Button,
            "<XRController>{RightHand}/primaryButton");

        CacheHandRenderers();

        if (pointLight != null)
        {
            baseIntensity = pointLight.intensity;

            if (flameTransform == null)
                flameTransform = pointLight.transform;
        }
        CacheFlameTransform();
        if (lightAudioSource == null)
        {
            if (pointLight != null)
                lightAudioSource = pointLight.GetComponent<AudioSource>();
            if (lightAudioSource == null && lighterRoot != null)
                lightAudioSource = lighterRoot.GetComponentInChildren<AudioSource>(true);
            if (lightAudioSource == null)
                lightAudioSource = GetComponent<AudioSource>();
        }

        noiseOffset = Random.value * 1000f;
        SetLighterActive(false);
    }

    private void OnEnable()
    {
        lighterAction?.Enable();
    }

    private void OnDisable()
    {
        lighterAction?.Disable();
        SetLighterActive(false);
    }

    private void Update()
    {
        bool shouldBeLit = lighterAction?.IsPressed() == true;
        if (shouldBeLit != isLit)
        {
            SetLighterActive(shouldBeLit);

            // The source is part of the lighter hierarchy, so the lighter must
            // be active before Unity can begin playback.
            if (shouldBeLit && lightAudioSource != null)
                lightAudioSource.Play();
        }

        if (isLit && pointLight != null)
        {
            float noise = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed);
            pointLight.intensity = baseIntensity * Mathf.Lerp(
                1f - flickerAmount,
                1f + flickerAmount,
                noise);
        }

        if (isLit)
            AnimateFlame();
    }

    private void CacheFlameTransform()
    {
        if (flameTransform == null && lighterRoot != null)
        {
            foreach (Transform child in lighterRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals("flame", System.StringComparison.OrdinalIgnoreCase))
                {
                    flameTransform = child;
                    break;
                }
            }
        }

        if (flameTransform == null)
            return;

        baseFlamePosition = flameTransform.localPosition;
        baseFlameScale = flameTransform.localScale;
        baseFlameRotation = flameTransform.localRotation;
    }

    private void AnimateFlame()
    {
        if (flameTransform == null)
            return;

        float time = Time.time * flickerSpeed;
        float heightNoise = Mathf.PerlinNoise(noiseOffset + 13.7f, time);
        float sideNoise = Mathf.PerlinNoise(noiseOffset + 41.3f, time * 0.73f) * 2f - 1f;
        float depthNoise = Mathf.PerlinNoise(noiseOffset + 79.1f, time * 0.61f) * 2f - 1f;

        float heightScale = Mathf.Lerp(1f - flameScaleAmount, 1f + flameScaleAmount, heightNoise);
        float widthScale = Mathf.Lerp(1f + flameScaleAmount * 0.45f, 1f - flameScaleAmount * 0.35f, heightNoise);
        flameTransform.localScale = Vector3.Scale(
            baseFlameScale,
            new Vector3(widthScale, heightScale, widthScale));

        flameTransform.localPosition = baseFlamePosition + new Vector3(
            sideNoise * flameSway,
            (heightScale - 1f) * flameSway * 0.35f,
            depthNoise * flameSway * 0.45f);
        flameTransform.localRotation = baseFlameRotation * Quaternion.Euler(
            depthNoise * flameTilt,
            sideNoise * flameTilt * 0.35f,
            -sideNoise * flameTilt);
    }

    private void ResetFlameTransform()
    {
        if (flameTransform == null)
            return;

        flameTransform.localPosition = baseFlamePosition;
        flameTransform.localScale = baseFlameScale;
        flameTransform.localRotation = baseFlameRotation;
    }

    private void CacheHandRenderers()
    {
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> renderers = new List<Renderer>(childRenderers.Length);

        foreach (Renderer childRenderer in childRenderers)
        {
            bool belongsToLighter = lighterRoot != null &&
                childRenderer.transform.IsChildOf(lighterRoot.transform);
            if (!belongsToLighter)
                renderers.Add(childRenderer);
        }

        handRenderers = renderers.ToArray();
    }

    private void SetLighterActive(bool active)
    {
        isLit = active;

        if (handRenderers != null)
        {
            foreach (Renderer handRenderer in handRenderers)
            {
                if (handRenderer != null)
                    handRenderer.enabled = !active;
            }
        }

        if (active)
        {
            if (lighterRoot != null)
                lighterRoot.SetActive(true);
            if (pointLight != null)
                pointLight.enabled = true;
        }
        else
        {
            ResetFlameTransform();
            if (pointLight != null)
            {
                pointLight.enabled = false;
                pointLight.intensity = baseIntensity;
            }
            if (lighterRoot != null)
                lighterRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        lighterAction?.Dispose();
    }
}
