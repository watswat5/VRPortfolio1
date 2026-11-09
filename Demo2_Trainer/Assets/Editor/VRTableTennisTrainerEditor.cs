using Demo2.TableTennis;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VRTableTennisTrainer))]
public sealed class VRTableTennisTrainerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VRTableTennisTrainer trainer = (VRTableTennisTrainer)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode Controls", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to reset the trainer or switch modes.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Current Mode", trainer.Mode.ToString());

        if (GUILayout.Button("Reset Trainer"))
            trainer.ResetTrainer();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(trainer.Mode == TrainerMode.NormalPractice))
            {
                if (GUILayout.Button("Normal Practice"))
                    trainer.SetMode(TrainerMode.NormalPractice);
            }

            using (new EditorGUI.DisabledScope(trainer.Mode == TrainerMode.AIOpponent))
            {
                if (GUILayout.Button("AI Opponent"))
                    trainer.SetMode(TrainerMode.AIOpponent);
            }
        }
    }
}
