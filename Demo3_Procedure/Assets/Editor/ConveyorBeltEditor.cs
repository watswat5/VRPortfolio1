using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConveyorBelt))]
public sealed class ConveyorBeltEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        var conveyor = (ConveyorBelt)target;
        string problem = conveyor.SetupProblem;
        EditorGUILayout.LabelField("Resolved Cycle Distance", $"{conveyor.CycleDistance:F3} m");
        if (problem != null)
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
        bool canTrigger = Application.isPlaying && !EditorUtility.IsPersistent(conveyor) &&
            conveyor.isActiveAndEnabled && !conveyor.IsMoving && problem == null;

        using (new EditorGUI.DisabledScope(!canTrigger))
        {
            if (GUILayout.Button(conveyor.IsMoving ? "Conveyor Running..." : "Trigger Conveyor"))
                conveyor.TriggerBelt();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to trigger a conveyor cycle.", MessageType.Info);
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
