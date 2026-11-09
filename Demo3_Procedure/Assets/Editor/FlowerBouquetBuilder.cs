using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Builds the bouquet's native Unity meshes, URP materials, and prefab.</summary>
[InitializeOnLoad]
public static class FlowerBouquetBuilder
{
    private const string Folder = "Assets/Models/FlowerBouquet";
    [Serializable] private class Model { public Part[] parts; }
    [Serializable] private class Part
    {
        public string name, material;
        public Vector3[] vertices;
        public int[] triangles;
        public Vector2[] uv;
    }

    static FlowerBouquetBuilder() { EditorApplication.delayCall += AutoBuild; }

    private static void AutoBuild()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            File.Exists(Folder + "/FlowerBouquet.geometry.json") &&
            !File.Exists(Folder + "/FlowerBouquet.prefab")) Build();
    }

    [MenuItem("Tools/Props/Build Flower Bouquet")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder + "/UnityAssets");
        AssetDatabase.Refresh();
        var model = JsonUtility.FromJson<Model>(File.ReadAllText(Folder + "/FlowerBouquet.geometry.json"));
        var root = new GameObject("FlowerBouquet");
        try
        {
            foreach (var part in model.parts)
            {
                string meshPath = Folder + "/UnityAssets/" + part.name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                bool create = mesh == null;
                if (create) mesh = new Mesh(); else mesh.Clear();
                mesh.name = part.name;
                mesh.vertices = part.vertices;
                mesh.triangles = part.triangles;
                mesh.uv = part.uv;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                if (create) AssetDatabase.CreateAsset(mesh, meshPath);
                else EditorUtility.SetDirty(mesh);

                string materialPath = Folder + "/UnityAssets/" + part.material + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    material = new Material(shader) { name = part.material };
                    material.color = part.material switch
                    {
                        "Ceramic" => new Color(.19f, .51f, .52f),
                        "Rim" => new Color(.38f, .69f, .66f),
                        "Inside" => new Color(.12f, .32f, .33f),
                        "Stem" => new Color(.23f, .40f, .19f),
                        "Leaf" => new Color(.32f, .53f, .25f),
                        "Coral" => new Color(.95f, .36f, .32f),
                        "Peach" => new Color(1f, .65f, .43f),
                        "Cream" => new Color(1f, .91f, .70f),
                        "Pink" => new Color(.87f, .42f, .60f),
                        _ => new Color(.96f, .68f, .18f)
                    };
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .16f);
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                var child = new GameObject(part.name);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = material;
            }

            // Only the vase is solid; foliage will not obstruct the user's hands.
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, .119f, 0f);
            collider.size = new Vector3(.16f, .238f, .16f);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/FlowerBouquet.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("Flower bouquet ready: " + Folder + "/FlowerBouquet.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
