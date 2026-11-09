using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scavenger.Vending.Editor
{
    public static class VendingMachinePreview
    {
        [MenuItem("Tools/Scavenger/Validate and Render Vending Machine")]
        public static void Render()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VendingMachine/Generated/VendingMachine.prefab");
            if (!prefab) throw new Exception("Build the vending machine first.");
            var machine = prefab.GetComponent<VendingMachine>();
            if (machine.lanes.Length != 9 || !machine.deliveryPoint) throw new Exception("Incomplete vending setup.");
            var buttons = prefab.GetComponentsInChildren<VendingButton>();
            if (buttons.Length != 9) throw new Exception("Expected nine separate vending buttons.");
            var addresses = new System.Collections.Generic.HashSet<string>();
            foreach (var button in buttons)
                if (button.machine != machine || !button.GetComponent<BoxCollider>() ||
                    !button.GetComponentInChildren<TextMesh>() || !addresses.Add(button.Address))
                    throw new Exception("Invalid independent button: " + button.name);
            int triangles = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh) throw new Exception("Missing mesh: " + filter.name);
                var mesh = filter.sharedMesh;
                triangles += mesh.triangles.Length / 3;
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                    if (Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]).sqrMagnitude < 1e-16f)
                        throw new Exception("Degenerate triangle in " + filter.name);
            }
            foreach (var lane in machine.lanes)
            {
                if (!lane.screw || lane.products.Length != 3) throw new Exception("Incomplete lane.");
                foreach (var product in lane.products)
                    if (!product || product.IsChildOf(lane.screw)) throw new Exception("Product must be independent of rotating screw.");
                if (Mathf.Abs(lane.products[0].localPosition.z - lane.products[1].localPosition.z - machine.productPitch) > .0001f)
                    throw new Exception("Product spacing does not match spiral pitch.");
            }
            Debug.Log("VENDING VALIDATION PASSED: 9 independent spirals, 27 products, " + triangles + " triangles (excluding text).");
            RenderImage(prefab, "Assets/VendingMachine/Preview.png");
            AssetDatabase.Refresh();
        }

        public static void RenderImage(GameObject source, string path)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var instance = UnityEngine.Object.Instantiate(source);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                preview.AddSingleGO(instance);
                preview.camera.transform.position = new Vector3(3.2f, 2.5f, 5.5f);
                preview.camera.transform.LookAt(new Vector3(0, 1.04f, 0));
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = 1.29f;
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = 30;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.075f, .105f, .13f);
                preview.lights[0].intensity = 1.4f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40, 210, 0);
                preview.lights[1].intensity = .8f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340, 40, 0);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 1200));
                preview.Render(true);
                var image = preview.EndStaticPreview();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
    }
}
