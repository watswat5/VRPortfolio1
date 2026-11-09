using UnityEditor;
using UnityEngine;

public class RandomPrefabPlacer : EditorWindow
{
    [SerializeField] private GameObject[] prefabs = new GameObject[0];
    [SerializeField] private Transform center;
    [SerializeField] private float innerRadius = 20f;
    [SerializeField] private float outerRadius = 40f;
    [SerializeField] private int count = 30;
    [SerializeField] private bool randomRotation = true;

    [MenuItem("Tools/Scavenger/Random Prefab Placer")]
    private static void Open()
    {
        GetWindow<RandomPrefabPlacer>("Random Prefab Placer");
    }

    private void OnGUI()
    {
        var settings = new SerializedObject(this);
        settings.Update();
        EditorGUILayout.PropertyField(settings.FindProperty("prefabs"), true);
        EditorGUILayout.PropertyField(settings.FindProperty("center"));
        EditorGUILayout.PropertyField(settings.FindProperty("innerRadius"));
        EditorGUILayout.PropertyField(settings.FindProperty("outerRadius"));
        EditorGUILayout.PropertyField(settings.FindProperty("count"));
        EditorGUILayout.PropertyField(settings.FindProperty("randomRotation"));
        settings.ApplyModifiedProperties();

        EditorGUILayout.HelpBox("Places prefabs on the X/Z plane at the center's height. Leave Center empty to use the world origin. Overlap is allowed.", MessageType.Info);
        bool valid = prefabs.Length > 0 && count > 0 && innerRadius >= 0f && outerRadius > innerRadius;
        foreach (GameObject prefab in prefabs)
            valid &= prefab != null && PrefabUtility.IsPartOfPrefabAsset(prefab);

        using (new EditorGUI.DisabledScope(!valid || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Place Prefabs"))
                PlacePrefabs();
        }
        if (!valid)
            EditorGUILayout.HelpBox("Assign prefab assets to every slot, a positive count, and radii with 0 <= Inner < Outer.", MessageType.Warning);
    }

    private void PlacePrefabs()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place Random Prefabs");

        Vector3 origin = center != null ? center.position : Vector3.zero;
        var group = new GameObject("Random Prefabs");
        group.transform.position = origin;
        Undo.RegisterCreatedObjectUndo(group, "Create Prefab Group");

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Place Prefab");

            float angle = Random.Range(0f, Mathf.PI * 2f);
            // Sampling squared radius distributes objects evenly over the ring's area.
            float radius = Mathf.Sqrt(Mathf.Lerp(innerRadius * innerRadius, outerRadius * outerRadius, Random.value));
            instance.transform.position = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (randomRotation)
                instance.transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * instance.transform.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = group;
    }
}
