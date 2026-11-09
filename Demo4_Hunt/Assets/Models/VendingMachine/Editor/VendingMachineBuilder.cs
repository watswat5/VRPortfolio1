using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scavenger.Vending.Editor
{
    public static class VendingMachineBuilder
    {
        private const string Folder = "Assets/VendingMachine/Generated";
        private static readonly Dictionary<string, Material> Materials = new();
        private static Transform root;
        private static Mesh cube;

        [MenuItem("Tools/Scavenger/Build Vending Machine")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Materials.Clear();
            Mat("Teal", new Color32(28, 111, 117, 255));
            Mat("Cream", new Color32(240, 227, 191, 255));
            Mat("Dark", new Color32(24, 37, 46, 255));
            Mat("Steel", new Color32(154, 181, 181, 255), 0.65f);
            Mat("Coral", new Color32(236, 103, 75, 255));
            Mat("Gold", new Color32(242, 180, 58, 255));
            Mat("Mint", new Color32(131, 197, 160, 255));
            Mat("Glass", new Color(.65f, .87f, .94f, .09f));
            Material glass = Materials["Glass"];
            glass.SetFloat("_Surface", 1);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0);
            glass.SetFloat("_Smoothness", .7f);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.renderQueue = (int)RenderQueue.Transparent;
            cube = BoxMesh();
            cube = Save(cube, "Box");
            Mesh snack = BevelBox(new Vector3(.17f, .23f, .055f), .022f);
            snack = Save(snack, "Snack");
            Mesh screw = Helix();
            screw = Save(screw, "Spiral");
            var machine = new GameObject("VendingMachine");
            root = machine.transform;
            var controller = machine.AddComponent<VendingMachine>();
            Transform body = Group("Cabinet", root);
            Box("Back", body, new(0, 1.06f, -.32f), new(1.2f, 1.92f, .08f), "Teal", true);
            Box("Left", body, new(-.57f, 1.04f, 0), new(.10f, 1.96f, .64f), "Teal", true);
            Box("Right", body, new(.57f, 1.04f, 0), new(.10f, 1.96f, .64f), "Teal", true);
            Box("Roof", body, new(0, 2.015f, 0), new(1.2f, .09f, .72f), "Teal", true);
            Box("Base", body, new(0, .14f, 0), new(1.2f, .16f, .72f), "Teal", true);
            for (int i = 0; i < 4; i++)
                Box("Foot_" + i, body, new(i % 2 == 0 ? -.46f : .46f, .04f, i < 2 ? -.24f : .24f), new(.13f, .08f, .13f), "Dark", true);
            Box("Interior", body, new(-.105f, 1.16f, -.269f), new(.88f, 1.38f, .024f), "Dark");
            Box("LeftTrim", body, new(-.515f, 1.195f, .34f), new(.045f, 1.40f, .065f), "Cream");
            Box("RightTrim", body, new(.325f, 1.195f, .34f), new(.045f, 1.40f, .065f), "Cream");
            Transform pane = MeshObject("FrontGlass", body, cube, new(-.105f, 1.1575f, .366f), new(.79f, 1.325f, .012f), "Glass");
            pane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            controller.frontGlass = pane.gameObject.AddComponent<BoxCollider>();
            Box("GlassLeftFrame", body, new(-.515f, 1.1575f, .35f), new(.035f, 1.36f, .045f), "Cream");
            Box("GlassRightFrame", body, new(.305f, 1.1575f, .35f), new(.035f, 1.36f, .045f), "Cream");
            Box("GlassBottomFrame", body, new(-.105f, .49f, .35f), new(.85f, .025f, .045f), "Cream");
            Box("GlassTopFrame", body, new(-.105f, 1.825f, .35f), new(.85f, .025f, .045f), "Cream");
            Box("Header", body, new(0, 1.91f, .345f), new(1.10f, .18f, .08f), "Cream");
            Label("SNACK / O / MAT", body, new(-.46f, 1.91f, .391f), .077f, "Dark");
            Box("ControlPanel", body, new(.445f, 1.16f, .335f), new(.19f, 1.30f, .08f), "Cream", true);
            Box("Screen", body, new(.445f, 1.62f, .384f), new(.148f, .14f, .025f), "Dark");
            Label("READY", body, new(.381f, 1.635f, .401f), .025f, "Mint");
            Label("$ 1.00", body, new(.381f, 1.59f, .401f), .022f, "Cream");
            Transform buttons = Group("Buttons", root);
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new(.494f - i % 3 * .052f, 1.43f - i / 3 * .063f, .39f);
                string address = $"{(char)('A' + i / 3)}{i % 3 + 1}";
                Transform button = Group("Button_" + address, buttons);
                button.localPosition = p;
                Box("Cap", button, Vector3.zero, new(.039f, .044f, .027f), "Dark");
                Label(address, button, new(-.013f, 0, .018f), .021f, "Cream");
                button.gameObject.AddComponent<BoxCollider>().size = new(.039f, .044f, .037f);
                var interaction = button.gameObject.AddComponent<VendingButton>();
                interaction.machine = controller;
                interaction.row = i / 3 + 1;
                interaction.column = i % 3 + 1;
            }
            Box("CoinSlot", body, new(.445f, 1.10f, .38f), new(.09f, .018f, .015f), "Dark");
            Box("ReturnButton", body, new(.445f, .99f, .389f), new(.065f, .055f, .035f), "Coral");
            Box("PaymentSlot", body, new(.445f, .84f, .385f), new(.125f, .07f, .018f), "Dark");
            Box("PaymentLip", body, new(.445f, .812f, .403f), new(.13f, .015f, .04f), "Steel");
            Box("PickupBack", body, new(0, .365f, .22f), new(1.08f, .25f, .035f), "Dark");
            Box("PickupTray", body, new(0, .23f, .35f), new(1.08f, .045f, .32f), "Dark", true);
            Box("PickupLip", body, new(0, .27f, .50f), new(1.10f, .085f, .045f), "Cream", true);
            Label("P I C K   U P", body, new(-.19f, .27f, .525f), .035f, "Dark");
            Transform lanes = Group("Lanes", root);
            controller.lanes = new VendingMachine.Lane[9];
            string[] colors = { "Coral", "Gold", "Mint" };
            for (int row = 0; row < 3; row++)
            {
                float y = 1.47f - row * .40f;
                Box("Shelf_" + row, body, new(-.105f, y - .025f, -.015f), new(.79f, .035f, .55f), "Steel", true);
                Box("ShelfRail_" + row, body, new(-.105f, y - .026f, .275f), new(.79f, .062f, .032f), "Dark");
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    float x = .16f - col * .265f;
                    string address = $"{(char)('A' + row)}{col + 1}";
                    Transform lane = Group("Lane_" + address, lanes);
                    Transform coil = MeshObject("Screw_PivotZ", lane, screw, new(x, y + .08f, -.22f), Vector3.one, "Steel");
                    var products = new Transform[3];
                    for (int depth = 0; depth < 3; depth++)
                    {
                        products[depth] = MeshObject("Snack_" + depth, lane, snack, new(x, y + .135f, .18f - depth * .14f), Vector3.one, colors[(row + col) % 3]);
                        var collider = products[depth].gameObject.AddComponent<BoxCollider>();
                        collider.size = new(.17f, .23f, .055f);
                        Box("Label", products[depth], new(0, .01f, .0285f), new(.115f, .09f, .002f), "Cream");
                        Box("Mark", products[depth], new(0, .015f, .030f), new(.055f, .02f, .002f), colors[(row + col) % 3]);
                        Box("TopSeal", products[depth], new(0, .104f, 0), new(.135f, .012f, .041f), "Cream");
                    }
                    Label(address, body, new(x - .018f, y - .027f, .294f), .034f, "Cream");
                    controller.lanes[index] = new VendingMachine.Lane { screw = coil, products = products };
                }
            }
            controller.deliveryPoint = Group("DeliveryPoint", root);
            controller.deliveryPoint.localPosition = new(0, .34f, .40f);
            PrefabUtility.SaveAsPrefabAsset(machine, Folder + "/VendingMachine.prefab");
            Object.DestroyImmediate(machine);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Vending machine built: " + Folder + "/VendingMachine.prefab");
        }

        private static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
        private static Transform MeshObject(string name, Transform parent, Mesh mesh, Vector3 pos, Vector3 size, string mat)
        {
            Transform t = Group(name, parent);
            t.localPosition = pos; t.localScale = size;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Materials[mat];
            return t;
        }
        private static void Box(string name, Transform parent, Vector3 pos, Vector3 size, string mat, bool collision = false)
        {
            Transform t = MeshObject(name, parent, cube, pos, size, mat);
            if (collision) t.gameObject.AddComponent<BoxCollider>();
        }
        private static void Label(string text, Transform parent, Vector3 pos, float height, string color)
        {
            Transform t = Group("Text_" + text, parent);
            t.localPosition = pos; t.localRotation = Quaternion.Euler(0, 180, 0);
            var label = t.gameObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = 64;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.color = Materials[color].color;

            // Scale world-space TMP text to match the requested letter height.
            label.ForceMeshUpdate();
            var bounds = label.textBounds;
            if (bounds.size.y > 0.0001f)
            {
                float uniformScale = height / bounds.size.y;
                t.localScale = Vector3.one * uniformScale;
            }
        }
        private static void Mat(string name, Color color, float metal = 0)
        {
            string path = Folder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = color; m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(m); Materials[name] = m;
        }
        private static Mesh Save(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = Folder + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old) { EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); }
            else AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
        private static Mesh BoxMesh() => BevelBox(Vector3.one, 0);
        private static Mesh BevelBox(Vector3 size, float bevel)
        {
            // Octagonal silhouette with separate face vertices for crisp, flat normals.
            float x = size.x / 2, y = size.y / 2, z = size.z / 2;
            Vector2[] ring = { new(-x + bevel, -y), new(x - bevel, -y), new(x, -y + bevel), new(x, y - bevel), new(x - bevel, y), new(-x + bevel, y), new(-x, y - bevel), new(-x, -y + bevel) };
            if (bevel == 0) ring = new Vector2[] { new(-x, -y), new(x, -y), new(x, y), new(-x, y) };
            var v = new List<Vector3>(); var indices = new List<int>();
            for (int i = 0; i < ring.Length; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Length];
                Quad(v, indices, new(a.x, a.y, -z), new(b.x, b.y, -z), new(b.x, b.y, z), new(a.x, a.y, z));
                Tri(v, indices, new(0, 0, z), new(a.x, a.y, z), new(b.x, b.y, z));
                Tri(v, indices, new(0, 0, -z), new(b.x, b.y, -z), new(a.x, a.y, -z));
            }
            return Finish(v, indices);
        }
        private static Mesh Helix()
        {
            var v = new List<Vector3>(); var indices = new List<int>();
            const int steps = 72, sides = 5;
            Vector3 Point(int i, int side)
            {
                float a = i / (float)steps * Mathf.PI * 2 * 3.5f;
                Vector3 radial = new(Mathf.Cos(a), Mathf.Sin(a), 0);
                Vector3 tangent = new(-.073f * Mathf.Sin(a), .073f * Mathf.Cos(a), .14f / (2 * Mathf.PI));
                Vector3 binormal = Vector3.Cross(tangent.normalized, radial).normalized;
                float b = side * Mathf.PI * 2 / sides;
                return radial * .073f + Vector3.forward * (i / (float)steps * .49f) + .008f * (radial * Mathf.Cos(b) + binormal * Mathf.Sin(b));
            }
            for (int i = 0; i < steps; i++)
                for (int s = 0; s < sides; s++)
                    Quad(v, indices, Point(i, s), Point(i, s + 1), Point(i + 1, s + 1), Point(i + 1, s));
            for (int s = 1; s < sides - 1; s++)
            {
                Tri(v, indices, Point(0, 0), Point(0, s + 1), Point(0, s));
                Tri(v, indices, Point(steps, 0), Point(steps, s), Point(steps, s + 1));
            }
            return Finish(v, indices);
        }
        private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        { int n = v.Count; v.AddRange(new[] { a, b, c, d }); t.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 }); }
        private static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        { int n = v.Count; v.AddRange(new[] { a, b, c }); t.AddRange(new[] { n, n + 1, n + 2 }); }
        private static Mesh Finish(List<Vector3> v, List<int> t)
        { var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m; }
    }
}
