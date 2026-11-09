using System;
using System.IO;
using UnityEngine;
using UnityEditor;

// Builds native Unity assets from the companion portable model data.
[InitializeOnLoad]
public static class CartoonCRTBuilder
{
    const string Folder = "Assets/Models/CartoonCRT";
    [Serializable] class Model { public Part[] parts; }
    [Serializable] class Part { public string name, material; public Vector3[] vertices; public int[] triangles; public Vector2[] uv; }
    static CartoonCRTBuilder() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Folder + "/CartoonCRT.geometry.json") && !File.Exists(Folder + "/CartoonCRT.prefab")) Build();
    }
    [MenuItem("Tools/Props/Build Cartoon CRT")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder + "/UnityAssets");
        AssetDatabase.Refresh();
        var model = JsonUtility.FromJson<Model>(File.ReadAllText(Folder + "/CartoonCRT.geometry.json"));
        var root = new GameObject("CartoonCRT");
        try
        {
            foreach (var p in model.parts)
            {
                string path = Folder + "/UnityAssets/" + p.name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool create = mesh == null;
                if (create) mesh = new Mesh(); else mesh.Clear();
                mesh.name = p.name; mesh.vertices = p.vertices; mesh.triangles = p.triangles; mesh.uv = p.uv;
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                if (create) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
                var child = new GameObject(p.name); child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                string matPath = Folder + "/UnityAssets/" + p.material + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null)
                {
                    var shader = Shader.Find(p.material == "Screen" ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find(p.material == "Screen" ? "Unlit/Texture" : "Standard");
                    material = new Material(shader) { name = p.material };
                    Color color = p.material switch {
                        "Burgundy" => new Color(.36f,.075f,.105f), "Pink" => new Color(.68f,.36f,.43f),
                        "Steel" => new Color(.30f,.34f,.37f), "Screen" => Color.white, _ => new Color(.045f,.055f,.065f) };
                    material.color = color;
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .22f);
                    AssetDatabase.CreateAsset(material, matPath);
                }
                child.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0,.27f,.008f); collider.size = new Vector3(.60f,.54f,.416f);
            var display = root.AddComponent<TVStageDisplay>();
            var serializedDisplay = new SerializedObject(display);
            serializedDisplay.FindProperty("screenRenderer").objectReferenceValue = root.transform.Find("Screen_RenderTexture").GetComponent<Renderer>();
            serializedDisplay.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/CartoonCRT.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("CartoonCRT ready: prefab with recessed screen and TVStageDisplay at " + Folder);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
