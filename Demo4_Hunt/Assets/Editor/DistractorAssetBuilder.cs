using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DistractorAssetBuilder
{
    private const string OutputFolder = "Assets/Models/Distractors";
    private const string SessionKey = "Demo4.DistractorAssetBuilder.v2";

    static DistractorAssetBuilder()
    {
        EditorApplication.delayCall += BuildOnce;
    }

    [MenuItem("Tools/Demo 4/Rebuild Distractor Models")]
    public static void RebuildFromMenu()
    {
        BuildAssets();
    }

    private static void BuildOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        BuildAssets();
    }

    private static void BuildAssets()
    {
        EnsureFolder("Assets/Models");
        EnsureFolder(OutputFolder);

        Material washerMaterial = SaveMaterial("Washer_Steel", new Color(0.34f, 0.37f, 0.38f), 0.82f, 0.32f);
        Material capMaterial = SaveMaterial("BottleCap_FadedRed", new Color(0.42f, 0.075f, 0.055f), 0.38f, 0.23f);
        Material tokenMaterial = SaveMaterial("ArcadeToken_Gold", new Color(0.62f, 0.37f, 0.075f), 0.86f, 0.48f);
        Material buttonMaterial = SaveMaterial("Button_Brass", new Color(0.50f, 0.30f, 0.085f), 0.74f, 0.40f);
        Material darkMaterial = SaveMaterial("Recess_Dark", new Color(0.025f, 0.022f, 0.018f), 0.10f, 0.20f);
        Material coolerBodyMaterial = SaveMaterial("Cooler_Red", new Color(0.62f, 0.065f, 0.045f), 0.03f, 0.26f);
        Material coolerTrimMaterial = SaveMaterial("Cooler_Ivory", new Color(0.82f, 0.79f, 0.68f), 0.02f, 0.30f);
        Material chairFrameMaterial = SaveMaterial("Chair_Aluminum", new Color(0.46f, 0.50f, 0.51f), 0.78f, 0.38f);
        Material chairFabricMaterial = SaveMaterial("Chair_TealFabric", new Color(0.025f, 0.32f, 0.30f), 0.0f, 0.18f);

        Mesh washer = SaveMesh("Washer", CreateRingMesh("Washer", 0.016f, 0.007f, 0.0022f, 16));
        Mesh bottleCap = SaveMesh("BottleCap", CreateRidgedCylinderMesh("BottleCap", 0.0155f, 0.0042f, 24, 0.0012f));
        Mesh token = SaveMesh("ArcadeToken", CreateBeveledDiscMesh("ArcadeToken", 0.0145f, 0.0028f, 18));
        Mesh tokenRim = SaveMesh("ArcadeToken_RaisedRim", CreateRingMesh("ArcadeToken_RaisedRim", 0.0118f, 0.0102f, 0.0007f, 18));
        Mesh tokenCenter = SaveMesh("ArcadeToken_CenterMark", CreateCylinderMesh("ArcadeToken_CenterMark", 0.0045f, 0.00025f, 8));
        Mesh button = SaveMesh("BrassButton", CreateDomedDiscMesh("BrassButton", 0.0135f, 0.0032f, 16));
        Mesh hole = SaveMesh("ButtonHole", CreateCylinderMesh("ButtonHole", 0.00145f, 0.00045f, 10));
        Mesh capInset = SaveMesh("BottleCap_Inset", CreateCylinderMesh("BottleCap_Inset", 0.0115f, 0.00025f, 18));
        Mesh lowPolyBox = SaveMesh("LowPolyBox", CreateBoxMesh("LowPolyBox"));

        SaveSingleMeshPrefab("SteelWasher", washer, washerMaterial, new Vector3(0.034f, 0.003f, 0.034f));
        SaveBottleCapPrefab(bottleCap, capInset, capMaterial, darkMaterial);
        SaveTokenPrefab(token, tokenRim, tokenCenter, tokenMaterial, darkMaterial);
        SaveButtonPrefab(button, hole, buttonMaterial, darkMaterial);
        SaveCoolerPrefab(lowPolyBox, coolerBodyMaterial, coolerTrimMaterial, darkMaterial);
        SaveLawnChairPrefab(lowPolyBox, chairFrameMaterial, chairFabricMaterial, darkMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created the low-poly distractor prefabs and their materials in " + OutputFolder + ".");
    }

    private static void SaveSingleMeshPrefab(string name, Mesh mesh, Material material, Vector3 colliderSize)
    {
        GameObject root = NewMeshObject(name, mesh, material);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = colliderSize;
        PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    private static void SaveBottleCapPrefab(Mesh cap, Mesh insetMesh, Material material, Material darkMaterial)
    {
        GameObject root = NewMeshObject("FadedBottleCap", cap, material);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.034f, 0.005f, 0.034f);

        GameObject inset = NewMeshObject("Dark Inner Face", insetMesh, darkMaterial);
        inset.transform.SetParent(root.transform, false);
        inset.transform.localPosition = Vector3.up * 0.00215f;

        PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/FadedBottleCap.prefab");
        Object.DestroyImmediate(root);
    }

    private static void SaveTokenPrefab(Mesh token, Mesh rim, Mesh centerMesh, Material material, Material darkMaterial)
    {
        GameObject root = NewMeshObject("ArcadeToken", token, material);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.031f, 0.004f, 0.031f);

        GameObject raisedRim = NewMeshObject("Raised Rim", rim, material);
        raisedRim.transform.SetParent(root.transform, false);
        raisedRim.transform.localPosition = Vector3.up * 0.00165f;

        GameObject centerMark = NewMeshObject("Center Mark", centerMesh, darkMaterial);
        centerMark.transform.SetParent(root.transform, false);
        centerMark.transform.localPosition = Vector3.up * 0.00166f;
        centerMark.transform.localScale = new Vector3(1f, 1f, 0.58f);

        PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/ArcadeToken.prefab");
        Object.DestroyImmediate(root);
    }

    private static void SaveButtonPrefab(Mesh button, Mesh hole, Material material, Material darkMaterial)
    {
        GameObject root = NewMeshObject("BrassButton", button, material);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.029f, 0.005f, 0.029f);

        Vector3[] holePositions =
        {
            new(-0.0032f, 0.00215f, -0.0032f), new(0.0032f, 0.00215f, -0.0032f),
            new(-0.0032f, 0.00215f, 0.0032f), new(0.0032f, 0.00215f, 0.0032f)
        };
        for (int i = 0; i < holePositions.Length; i++)
        {
            GameObject holeObject = NewMeshObject($"Thread Hole {i + 1}", hole, darkMaterial);
            holeObject.transform.SetParent(root.transform, false);
            holeObject.transform.localPosition = holePositions[i];
        }

        PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/BrassButton.prefab");
        Object.DestroyImmediate(root);
    }

    private static void SaveCoolerPrefab(Mesh box, Material body, Material trim, Material dark)
    {
        GameObject root = new GameObject("LowPolyCooler");
        AddBoxPart(root.transform, "Insulated Body", box, body,
            new Vector3(0f, 0.16f, 0f), new Vector3(0.46f, 0.29f, 0.31f));
        AddBoxPart(root.transform, "White Lid", box, trim,
            new Vector3(0f, 0.335f, 0f), new Vector3(0.49f, 0.065f, 0.34f));
        AddBoxPart(root.transform, "Lid Inset", box, dark,
            new Vector3(0f, 0.371f, 0f), new Vector3(0.34f, 0.009f, 0.22f));

        AddBoxPart(root.transform, "Handle Left", box, trim,
            new Vector3(-0.255f, 0.31f, 0f), new Vector3(0.035f, 0.30f, 0.035f));
        AddBoxPart(root.transform, "Handle Right", box, trim,
            new Vector3(0.255f, 0.31f, 0f), new Vector3(0.035f, 0.30f, 0.035f));
        AddBoxPart(root.transform, "Handle Grip", box, trim,
            new Vector3(0f, 0.455f, 0f), new Vector3(0.545f, 0.04f, 0.045f));

        AddBoxPart(root.transform, "Front Latch", box, dark,
            new Vector3(0f, 0.285f, -0.178f), new Vector3(0.075f, 0.10f, 0.022f));
        AddBoxPart(root.transform, "Left Foot", box, dark,
            new Vector3(-0.16f, 0.012f, 0f), new Vector3(0.085f, 0.025f, 0.23f));
        AddBoxPart(root.transform, "Right Foot", box, dark,
            new Vector3(0.16f, 0.012f, 0f), new Vector3(0.085f, 0.025f, 0.23f));

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.235f, 0f);
        collider.size = new Vector3(0.56f, 0.47f, 0.36f);
        PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/LowPolyCooler.prefab");
        Object.DestroyImmediate(root);
    }

    private static void SaveLawnChairPrefab(Mesh box, Material frame, Material fabric, Material dark)
    {
        GameObject root = new GameObject("LowPolyLawnChair");

        AddBoxPart(root.transform, "Seat Fabric", box, fabric,
            new Vector3(0f, 0.39f, -0.03f), new Vector3(0.47f, 0.035f, 0.46f));
        AddBoxPart(root.transform, "Back Fabric", box, fabric,
            new Vector3(0f, 0.69f, 0.19f), new Vector3(0.47f, 0.52f, 0.035f), new Vector3(-10f, 0f, 0f));

        AddBoxPart(root.transform, "Left Seat Rail", box, frame,
            new Vector3(-0.265f, 0.39f, -0.03f), new Vector3(0.035f, 0.035f, 0.54f));
        AddBoxPart(root.transform, "Right Seat Rail", box, frame,
            new Vector3(0.265f, 0.39f, -0.03f), new Vector3(0.035f, 0.035f, 0.54f));

        AddBoxPart(root.transform, "Left Back Post", box, frame,
            new Vector3(-0.265f, 0.69f, 0.19f), new Vector3(0.035f, 0.67f, 0.035f), new Vector3(-10f, 0f, 0f));
        AddBoxPart(root.transform, "Right Back Post", box, frame,
            new Vector3(0.265f, 0.69f, 0.19f), new Vector3(0.035f, 0.67f, 0.035f), new Vector3(-10f, 0f, 0f));
        AddBoxPart(root.transform, "Back Top Rail", box, frame,
            new Vector3(0f, 1.015f, 0.135f), new Vector3(0.56f, 0.035f, 0.035f), new Vector3(-10f, 0f, 0f));

        AddBoxPart(root.transform, "Left Front Leg", box, frame,
            new Vector3(-0.265f, 0.205f, -0.19f), new Vector3(0.04f, 0.43f, 0.04f), new Vector3(-16f, 0f, 0f));
        AddBoxPart(root.transform, "Right Front Leg", box, frame,
            new Vector3(0.265f, 0.205f, -0.19f), new Vector3(0.04f, 0.43f, 0.04f), new Vector3(-16f, 0f, 0f));
        AddBoxPart(root.transform, "Left Rear Leg", box, frame,
            new Vector3(-0.265f, 0.205f, 0.19f), new Vector3(0.04f, 0.43f, 0.04f), new Vector3(16f, 0f, 0f));
        AddBoxPart(root.transform, "Right Rear Leg", box, frame,
            new Vector3(0.265f, 0.205f, 0.19f), new Vector3(0.04f, 0.43f, 0.04f), new Vector3(16f, 0f, 0f));

        AddBoxPart(root.transform, "Left Armrest", box, dark,
            new Vector3(-0.315f, 0.62f, -0.015f), new Vector3(0.07f, 0.045f, 0.48f));
        AddBoxPart(root.transform, "Right Armrest", box, dark,
            new Vector3(0.315f, 0.62f, -0.015f), new Vector3(0.07f, 0.045f, 0.48f));
        AddBoxPart(root.transform, "Front Cross Brace", box, frame,
            new Vector3(0f, 0.13f, -0.245f), new Vector3(0.57f, 0.035f, 0.035f));
        AddBoxPart(root.transform, "Rear Cross Brace", box, frame,
            new Vector3(0f, 0.13f, 0.245f), new Vector3(0.57f, 0.035f, 0.035f));

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.51f, 0f);
        collider.size = new Vector3(0.68f, 1.04f, 0.62f);
        PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/LowPolyLawnChair.prefab");
        Object.DestroyImmediate(root);
    }

    private static void AddBoxPart(Transform parent, string name, Mesh box, Material material,
        Vector3 position, Vector3 scale, Vector3 euler = default)
    {
        GameObject part = NewMeshObject(name, box, material);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localEulerAngles = euler;
        part.transform.localScale = scale;
    }

    private static GameObject NewMeshObject(string name, Mesh mesh, Material material)
    {
        GameObject gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        return gameObject;
    }

    private static Material SaveMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{OutputFolder}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material generated = new Material(shader) { name = name, color = color };
        if (generated.HasProperty("_BaseColor")) generated.SetColor("_BaseColor", color);
        if (generated.HasProperty("_Metallic")) generated.SetFloat("_Metallic", metallic);
        if (generated.HasProperty("_Smoothness")) generated.SetFloat("_Smoothness", smoothness);

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh SaveMesh(string name, Mesh generated)
    {
        string path = $"{OutputFolder}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh CreateRingMesh(string name, float outerRadius, float innerRadius, float thickness, int segments)
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();
        float half = thickness * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices.Add(new Vector3(x * outerRadius, half, z * outerRadius));
            vertices.Add(new Vector3(x * innerRadius, half, z * innerRadius));
            vertices.Add(new Vector3(x * outerRadius, -half, z * outerRadius));
            vertices.Add(new Vector3(x * innerRadius, -half, z * innerRadius));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int a = i * 4;
            int b = next * 4;
            AddQuad(triangles, a, b, b + 1, a + 1);
            AddQuad(triangles, a + 2, a + 3, b + 3, b + 2);
            AddQuad(triangles, a, a + 2, b + 2, b);
            AddQuad(triangles, a + 1, b + 1, b + 3, a + 3);
        }
        return FinishMesh(name, vertices, triangles);
    }

    private static Mesh CreateRidgedCylinderMesh(string name, float radius, float thickness, int segments, float ridge)
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();
        float half = thickness * 0.5f;
        vertices.Add(new Vector3(0f, half, 0f));
        vertices.Add(new Vector3(0f, -half, 0f));

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float r = radius + (i % 2 == 0 ? ridge : 0f);
            vertices.Add(new Vector3(Mathf.Cos(angle) * r, half, Mathf.Sin(angle) * r));
            vertices.Add(new Vector3(Mathf.Cos(angle) * r, -half, Mathf.Sin(angle) * r));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int top = 2 + i * 2;
            int bottom = top + 1;
            int nextTop = 2 + next * 2;
            int nextBottom = nextTop + 1;
            triangles.AddRange(new[] { 0, nextTop, top, 1, bottom, nextBottom });
            AddQuad(triangles, top, nextTop, nextBottom, bottom);
        }
        return FinishMesh(name, vertices, triangles);
    }

    private static Mesh CreateBeveledDiscMesh(string name, float radius, float thickness, int segments)
    {
        return CreateLathedDisc(name, segments, new[]
        {
            new Vector2(0f, -thickness * 0.5f),
            new Vector2(radius * 0.90f, -thickness * 0.5f),
            new Vector2(radius, -thickness * 0.22f),
            new Vector2(radius, thickness * 0.22f),
            new Vector2(radius * 0.90f, thickness * 0.5f),
            new Vector2(0f, thickness * 0.5f)
        });
    }

    private static Mesh CreateDomedDiscMesh(string name, float radius, float thickness, int segments)
    {
        return CreateLathedDisc(name, segments, new[]
        {
            new Vector2(0f, -thickness * 0.45f),
            new Vector2(radius * 0.88f, -thickness * 0.45f),
            new Vector2(radius, -thickness * 0.10f),
            new Vector2(radius * 0.96f, thickness * 0.30f),
            new Vector2(radius * 0.70f, thickness * 0.55f),
            new Vector2(0f, thickness * 0.70f)
        });
    }

    private static Mesh CreateLathedDisc(string name, int segments, Vector2[] profile)
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();
        for (int ring = 0; ring < profile.Length; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * profile[ring].x,
                    profile[ring].y,
                    Mathf.Sin(angle) * profile[ring].x));
            }
        }

        for (int ring = 0; ring < profile.Length - 1; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int a = ring * segments + i;
                int b = ring * segments + next;
                int c = (ring + 1) * segments + next;
                int d = (ring + 1) * segments + i;
                AddQuad(triangles, a, b, c, d);
            }
        }
        return FinishMesh(name, vertices, triangles);
    }

    private static Mesh CreateCylinderMesh(string name, float radius, float thickness, int segments)
    {
        return CreateRidgedCylinderMesh(name, radius, thickness, segments, 0f);
    }

    private static Mesh CreateBoxMesh(string name)
    {
        Vector3[] vertices =
        {
            // Front
            new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
            new(0.5f, 0.5f, -0.5f), new(0.5f, -0.5f, -0.5f),
            // Back
            new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f),
            new(-0.5f, 0.5f, 0.5f), new(-0.5f, -0.5f, 0.5f),
            // Left
            new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
            new(-0.5f, 0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f),
            // Right
            new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f),
            new(0.5f, 0.5f, 0.5f), new(0.5f, -0.5f, 0.5f),
            // Top
            new(-0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, 0.5f),
            new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f),
            // Bottom
            new(-0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, -0.5f),
            new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f)
        };
        int[] triangles =
        {
            0, 1, 2, 0, 2, 3,
            4, 5, 6, 4, 6, 7,
            8, 9, 10, 8, 10, 11,
            12, 13, 14, 12, 14, 15,
            16, 17, 18, 16, 18, 19,
            20, 21, 22, 20, 22, 23
        };
        Vector2[] uvs = new Vector2[24];
        for (int face = 0; face < 6; face++)
        {
            int start = face * 4;
            uvs[start] = new Vector2(0f, 0f);
            uvs[start + 1] = new Vector2(0f, 1f);
            uvs[start + 2] = new Vector2(1f, 1f);
            uvs[start + 3] = new Vector2(1f, 0f);
        }
        Mesh mesh = new Mesh { name = name, vertices = vertices, triangles = triangles, uv = uvs };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh FinishMesh(string name, List<Vector3> vertices, List<int> triangles)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.AddRange(new[] { a, b, c, a, c, d });
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        int slash = folder.LastIndexOf('/');
        AssetDatabase.CreateFolder(folder[..slash], folder[(slash + 1)..]);
    }
}
