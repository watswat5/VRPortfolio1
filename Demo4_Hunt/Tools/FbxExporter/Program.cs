using System;
using System.IO;
using System.Text.Json;
using Autodesk.Fbx;

// The Unity SDK's logging bridge needs these two types in a standalone tool.
namespace AOT { public class MonoPInvokeCallbackAttribute : Attribute { public MonoPInvokeCallbackAttribute(Type type) {} } }
namespace UnityEngine {
    public class Logger { public bool logEnabled = true; }
    public static class Debug { public static Logger unityLogger = new Logger(); public static void LogError(string text) => Console.Error.WriteLine(text); }
}
class Program
{
    static FbxVector4 Vector(JsonElement v) => new FbxVector4(v[0].GetDouble(), v[1].GetDouble(), v[2].GetDouble());
    static void Main(string[] args)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(args[0]));
        var source = data.RootElement;
        using var manager = FbxManager.Create();
        manager.SetIOSettings(FbxIOSettings.Create(manager, Globals.IOSROOT));
        using var scene = FbxScene.Create(manager, source.GetProperty("name").GetString());
        scene.GetGlobalSettings().SetSystemUnit(FbxSystemUnit.m);
        scene.GetGlobalSettings().SetAxisSystem(FbxAxisSystem.MayaYUp);
        var root = FbxNode.Create(scene, source.GetProperty("name").GetString());
        scene.GetRootNode().AddChild(root);
        int expected = 0;
        foreach (var part in source.GetProperty("meshes").EnumerateArray())
        {
            var node = FbxNode.Create(scene, part.GetProperty("name").GetString());
            root.AddChild(node);
            var pos = part.GetProperty("position");
            node.LclTranslation.Set(new FbxDouble3(pos[0].GetDouble(), pos[1].GetDouble(), pos[2].GetDouble()));
            var mesh = FbxMesh.Create(scene, node.GetName());
            node.SetNodeAttribute(mesh);
            var faces = part.GetProperty("faces");
            mesh.InitControlPoints(faces.GetArrayLength() * 3);
            mesh.CreateLayer();
            var layer = mesh.GetLayer(0);
            var normals = FbxLayerElementNormal.Create(mesh, "Normals");
            normals.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
            normals.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
            layer.SetNormals(normals);
            var materials = FbxLayerElementMaterial.Create(mesh, "Materials");
            materials.SetMappingMode(FbxLayerElement.EMappingMode.eByPolygon);
            materials.SetReferenceMode(FbxLayerElement.EReferenceMode.eIndexToDirect);
            layer.SetMaterials(materials);
            foreach (var materialName in part.GetProperty("materials").EnumerateArray())
            {
                string name = materialName.GetString();
                var color = source.GetProperty("colors").GetProperty(name);
                var material = FbxSurfacePhong.Create(scene, name);
                material.Diffuse.Set(new FbxDouble3(color[0].GetDouble(), color[1].GetDouble(), color[2].GetDouble()));
                node.AddMaterial(material);
            }
            int vertex = 0;
            foreach (var face in faces.EnumerateArray())
            {
                mesh.BeginPolygon();
                foreach (var point in face.GetProperty("points").EnumerateArray())
                {
                    mesh.SetControlPointAt(Vector(point), vertex);
                    normals.GetDirectArray().Add(Vector(face.GetProperty("normal")));
                    mesh.AddPolygon(vertex++);
                }
                mesh.EndPolygon();
                materials.GetIndexArray().Add(face.GetProperty("material").GetInt32());
                expected++;
            }
        }
        using (var exporter = FbxExporter.Create(manager, "Exporter"))
        {
            if (!exporter.Initialize(args[1], -1, manager.GetIOSettings())) throw new Exception(exporter.GetStatus().GetErrorString());
            exporter.SetFileExportVersion("FBX201400");
            if (!exporter.Export(scene)) throw new Exception(exporter.GetStatus().GetErrorString());
        }
        using var importer = FbxImporter.Create(manager, "Validator");
        using var check = FbxScene.Create(manager, "Validation");
        if (!importer.Initialize(args[1], -1, manager.GetIOSettings()) || !importer.Import(check)) throw new Exception(importer.GetStatus().GetErrorString());
        int actual = Validate(check.GetRootNode());
        if (actual != expected) throw new Exception("Triangle count changed during export.");
        Console.WriteLine($"{args[1]}: Autodesk FBX import passed, {actual} triangles.");
    }
    static int Validate(FbxNode node)
    {
        int total = 0;
        var mesh = node.GetMesh();
        if (mesh != null)
        {
            total += mesh.GetPolygonCount();
            var layer = mesh.GetLayer(0);
            if (layer.GetNormals().GetDirectArray().GetCount() != mesh.GetControlPointsCount()) throw new Exception("Missing normals.");
            var indices = layer.GetMaterials().GetIndexArray();
            if (indices.GetCount() != total) throw new Exception("Missing material indices.");
            for (int i = 0; i < total; i++)
                if (indices.GetAt(i) < 0 || indices.GetAt(i) >= node.GetMaterialCount()) throw new Exception("Invalid material index.");
        }
        for (int i = 0; i < node.GetChildCount(); i++) total += Validate(node.GetChild(i));
        return total;
    }
}
