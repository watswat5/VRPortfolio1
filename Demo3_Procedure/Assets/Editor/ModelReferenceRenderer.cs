using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ModelReferenceRenderer
{
    private const int RenderLayer = 31;
    private const int ImageSize = 1024;
    private const string OutputFolder = "ReferenceRenders";

    private static readonly (string fileName, string assetPath)[] AssetsToRender =
    {
        ("01_shell.png", "Assets/Prefabs/01_CartoonShell.prefab"),
        ("02_windup_spring_and_key.png", "Assets/Prefabs/02_WindupSpring.prefab"),
        ("03_top_cover.png", "Assets/Prefabs/03_TopPlate_WithPost.prefab"),
        ("04_screw.png", "Assets/Prefabs/04_TopPlateScrew_Duplicate3x.prefab"),
        ("05_pressure_plate.png", "Assets/Prefabs/05_PressurePlate.prefab"),
        ("06_inspection_stamp.png", "Assets/Prefabs/06_InspectionStamp.prefab"),
        ("07_complete_assembly.png", "Assets/Models/ACME_PropFactory/ACME_PropFactory_Complete.fbx")
    };

    [MenuItem("Tools/Tutorial/Render Model References")]
    public static void RenderAll()
    {
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputFolder));
        Directory.CreateDirectory(outputPath);

        try
        {
            foreach ((string fileName, string assetPath) in AssetsToRender)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null) throw new InvalidOperationException($"Could not load {assetPath}");
                GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
                if (instance == null) throw new InvalidOperationException($"Could not instantiate {assetPath}");
                RenderObject(instance, Path.Combine(outputPath, fileName));
            }

            RenderHammer(Path.Combine(outputPath, "08_hammer.png"));
            Debug.Log($"Rendered tutorial references to {outputPath}");
        }
        finally
        {
            AssetDatabase.Refresh();
        }
    }

    private static void RenderHammer(string outputPath)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        GameObject hammer = GameObject.Find("Hammer");
        if (hammer == null) throw new InvalidOperationException("Could not find Hammer in SampleScene.");
        RenderObject(hammer, outputPath);
    }

    private static void RenderObject(GameObject root, string outputPath)
    {
        SetLayerRecursively(root, RenderLayer);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException($"{root.name} has no renderers.");

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        GameObject cameraObject = new GameObject("Reference Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.cullingMask = 1 << RenderLayer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.075f, 1f);
        camera.fieldOfView = 28f;
        camera.nearClipPlane = 0.005f;
        camera.farClipPlane = 1000f;
        camera.allowHDR = true;
        camera.allowMSAA = true;

        float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
        float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.18f;
        Vector3 viewDirection = new Vector3(1f, 0.72f, -1f).normalized;
        camera.transform.position = bounds.center + viewDirection * distance;
        camera.transform.LookAt(bounds.center);

        Light key = CreateLight("Key Light", LightType.Directional, 1.45f, new Color(1f, 0.92f, 0.82f));
        key.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        Light fill = CreateLight("Fill Light", LightType.Directional, 0.8f, new Color(0.55f, 0.72f, 1f));
        fill.transform.rotation = Quaternion.Euler(20f, 145f, 0f);
        Light rim = CreateLight("Rim Light", LightType.Directional, 1.0f, new Color(0.75f, 0.9f, 1f));
        rim.transform.rotation = Quaternion.Euler(65f, 180f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.3f, 0.34f);

        var renderTexture = new RenderTexture(ImageSize, ImageSize, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        renderTexture.Create();
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var image = new Texture2D(ImageSize, ImageSize, TextureFormat.RGBA32, false, false);
        image.ReadPixels(new Rect(0, 0, ImageSize, ImageSize), 0, 0);
        image.Apply();
        File.WriteAllBytes(outputPath, image.EncodeToPNG());
        RenderTexture.active = previous;

        UnityEngine.Object.DestroyImmediate(image);
        camera.targetTexture = null;
        renderTexture.Release();
        UnityEngine.Object.DestroyImmediate(renderTexture);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(key.gameObject);
        UnityEngine.Object.DestroyImmediate(fill.gameObject);
        UnityEngine.Object.DestroyImmediate(rim.gameObject);
    }

    private static Light CreateLight(string name, LightType type, float intensity, Color color)
    {
        GameObject lightObject = new GameObject(name);
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.color = color;
        light.cullingMask = 1 << RenderLayer;
        light.shadows = LightShadows.Soft;
        return light;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
    }
}
