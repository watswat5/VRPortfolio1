using UnityEngine;

public class DayNightLightingSwitcher : MonoBehaviour
{
    [Header("Skybox Materials")]
    public Material daySkybox;
    public Material nightSkybox;

    [Header("Main Directional Light")]
    public Light sunLight;
    public float dayLightIntensity = 1f;
    public float nightLightIntensity = 0.2f;

    [Header("Start at night")]
    public bool startNight;

    private bool isNight;

    private void Start()
    {
        EnsureSkyboxes();

        if (sunLight == null)
        {
            sunLight = RenderSettings.sun;
        }

        SetNight(startNight);
    }

    public void ToggleDayNight()
    {
        SetNight(!isNight);
    }

    public void SetNight(bool night)
    {
        isNight = night;

        Material targetSkybox = isNight ? nightSkybox : daySkybox;
        if (targetSkybox != null)
        {
            RenderSettings.skybox = targetSkybox;
            DynamicGI.UpdateEnvironment();
        }

        if (sunLight != null)
        {
            sunLight.intensity = isNight ? nightLightIntensity : dayLightIntensity;
        }
    }

    private void EnsureSkyboxes()
    {
        Shader proceduralSkybox = Shader.Find("Skybox/Procedural");
        if (proceduralSkybox == null)
        {
            Debug.LogWarning("Skybox/Procedural shader not found.");
            return;
        }

        if (daySkybox == null)
        {
            daySkybox = new Material(proceduralSkybox);
            daySkybox.name = "RuntimeDaySkybox";
            daySkybox.SetFloat("_SunSize", 0.03f);
            daySkybox.SetFloat("_AtmosphereThickness", 1.0f);
            daySkybox.SetColor("_SkyTint", new Color(0.35f, 0.55f, 0.9f));
            daySkybox.SetColor("_GroundColor", new Color(0.4f, 0.4f, 0.4f));
            daySkybox.SetFloat("_Exposure", 1.2f);
        }

        if (nightSkybox == null)
        {
            nightSkybox = new Material(proceduralSkybox);
            nightSkybox.name = "RuntimeNightSkybox";
            nightSkybox.SetFloat("_SunSize", 0.0f);
            nightSkybox.SetFloat("_AtmosphereThickness", 0.3f);
            nightSkybox.SetColor("_SkyTint", new Color(0.03f, 0.06f, 0.15f));
            nightSkybox.SetColor("_GroundColor", new Color(0.02f, 0.02f, 0.04f));
            nightSkybox.SetFloat("_Exposure", 0.35f);
        }
    }
}
