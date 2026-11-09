using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Scavenger.Vending.Editor
{
    public static class VendingTextMeshToTmpMigration
    {
        private static readonly string[] DefaultPrefabPaths =
        {
            "Assets/prefabs/VendingMachine.prefab",
            "Assets/Models/VendingMachine/Generated/VendingMachine.prefab"
        };

        [MenuItem("Tools/Scavenger/Migrate Vending Text To TMP")]
        public static void MigratePrefabTextToTmp()
        {
            var paths = GatherTargetPrefabPaths();
            if (paths.Count == 0)
            {
                throw new Exception("No target prefab found. Select a prefab in the Project window or create one at a default vending path.");
            }

            int totalConverted = 0;
            foreach (var prefabPath in paths)
            {
                totalConverted += MigratePrefabAtPath(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Converted " + totalConverted + " TextMesh labels to TextMeshPro across " + paths.Count + " prefab(s).");
        }

        private static int MigratePrefabAtPath(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (!root)
            {
                throw new Exception("Could not load prefab at " + prefabPath);
            }

            try
            {
                int converted = 0;
                var legacyLabels = root.GetComponentsInChildren<TextMesh>(true);
                foreach (var legacy in legacyLabels)
                {
                    ConvertLabel(legacy);
                    converted++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("Converted " + converted + " TextMesh labels in " + prefabPath);
                return converted;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static List<string> GatherTargetPrefabPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var selected in Selection.objects)
            {
                if (!selected)
                {
                    continue;
                }

                string selectedPath = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(selectedPath) &&
                    selectedPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(selectedPath);
                }
            }

            if (paths.Count == 0)
            {
                foreach (var defaultPath in DefaultPrefabPaths)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath))
                    {
                        paths.Add(defaultPath);
                    }
                }

                foreach (string guid in AssetDatabase.FindAssets("VendingMachine t:Prefab"))
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(foundPath) && foundPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(foundPath);
                    }
                }
            }

            return new List<string>(paths);
        }

        private static void ConvertLabel(TextMesh legacy)
        {
            var go = legacy.gameObject;

            var previousRenderer = go.GetComponent<Renderer>();
            float targetHeight = previousRenderer ? previousRenderer.bounds.size.y : 0f;

            string text = legacy.text;
            Color color = legacy.color;
            bool rich = legacy.richText;
            int fontSize = legacy.fontSize;
            var alignment = MapAlignment(legacy.anchor, legacy.alignment);

            UnityEngine.Object.DestroyImmediate(legacy);

            var tmp = go.GetComponent<TextMeshPro>();
            if (!tmp)
            {
                tmp = go.AddComponent<TextMeshPro>();
            }

            if (TMP_Settings.defaultFontAsset)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            tmp.text = text;
            tmp.color = color;
            tmp.richText = rich;
            tmp.fontSize = fontSize > 0 ? fontSize : 64;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = alignment;

            tmp.ForceMeshUpdate();
            var newRenderer = go.GetComponent<Renderer>();
            if (targetHeight > 0.0001f && newRenderer)
            {
                float currentHeight = newRenderer.bounds.size.y;
                if (currentHeight > 0.0001f)
                {
                    float ratio = targetHeight / currentHeight;
                    var tr = tmp ? tmp.transform : null;
                    if (tr)
                    {
                        tr.localScale *= ratio;
                    }
                }
            }
        }

        private static TextAlignmentOptions MapAlignment(TextAnchor anchor, TextAlignment legacyAlignment)
        {
            bool left = legacyAlignment == TextAlignment.Left;
            bool center = legacyAlignment == TextAlignment.Center;
            bool right = legacyAlignment == TextAlignment.Right;

            return anchor switch
            {
                TextAnchor.UpperLeft => left ? TextAlignmentOptions.TopLeft : center ? TextAlignmentOptions.Top : TextAlignmentOptions.TopRight,
                TextAnchor.UpperCenter => left ? TextAlignmentOptions.TopLeft : center ? TextAlignmentOptions.Top : TextAlignmentOptions.TopRight,
                TextAnchor.UpperRight => left ? TextAlignmentOptions.TopLeft : center ? TextAlignmentOptions.Top : TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => left ? TextAlignmentOptions.MidlineLeft : center ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineRight,
                TextAnchor.MiddleCenter => left ? TextAlignmentOptions.MidlineLeft : center ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineRight,
                TextAnchor.MiddleRight => left ? TextAlignmentOptions.MidlineLeft : center ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => left ? TextAlignmentOptions.BottomLeft : center ? TextAlignmentOptions.Bottom : TextAlignmentOptions.BottomRight,
                TextAnchor.LowerCenter => left ? TextAlignmentOptions.BottomLeft : center ? TextAlignmentOptions.Bottom : TextAlignmentOptions.BottomRight,
                TextAnchor.LowerRight => left ? TextAlignmentOptions.BottomLeft : center ? TextAlignmentOptions.Bottom : TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.MidlineRight
            };
        }
    }
}