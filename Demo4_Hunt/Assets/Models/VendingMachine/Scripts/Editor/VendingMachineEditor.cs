using UnityEditor;
using UnityEngine;

namespace Scavenger.Vending.Editor
{
    [CustomEditor(typeof(VendingMachine))]
    public sealed class VendingMachineEditor : UnityEditor.Editor
    {
        private int laneToVend;
        private int rowToVend = 1;
        private int columnToVend = 1;
        private bool jamNextVend;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var machine = (VendingMachine)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to trigger dispensing from the inspector.", MessageType.Info);
                return;
            }

            if (machine.lanes == null || machine.lanes.Length == 0)
            {
                EditorGUILayout.HelpBox("No lanes assigned.", MessageType.Warning);
                return;
            }

            laneToVend = Mathf.Clamp(laneToVend, 0, machine.lanes.Length - 1);
            laneToVend = EditorGUILayout.IntSlider("Lane Index", laneToVend, 0, machine.lanes.Length - 1);
            jamNextVend = EditorGUILayout.Toggle("Jam This Vend", jamNextVend);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Vend Lane"))
                {
                    machine.Vend(laneToVend, jamNextVend);
                }

                if (GUILayout.Button("Vend Lane (No Jam)"))
                {
                    machine.Vend(laneToVend, false);
                }

                if (GUILayout.Button("Jam Lane"))
                {
                    machine.Vend(laneToVend, true);
                }
            }

            EditorGUILayout.Space();
            rowToVend = EditorGUILayout.IntSlider("Row", rowToVend, 1, 3);
            columnToVend = EditorGUILayout.IntSlider("Column", columnToVend, 1, 3);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Vend Row/Column"))
                {
                    machine.Vend(rowToVend, columnToVend, jamNextVend);
                }

                if (GUILayout.Button("Jam Row/Column"))
                {
                    machine.Vend(rowToVend, columnToVend, true);
                }
            }
        }
    }
}
