using UnityEngine;

namespace Scavenger.Vending
{
    /// <summary>Independent button target. Connect your raycast or XR select event to Press.</summary>
    public sealed class VendingButton : MonoBehaviour
    {
        public VendingMachine machine;
        [Range(1, 3)] public int row = 1;
        [Range(1, 3)] public int column = 1;
        [Tooltip("If true, Press() will request a jammed vend.")]
        public bool jamOnPress;
        public string Address => $"{(char)('A' + row - 1)}{column}";

        public void Press() => Press(jamOnPress);
        public void Press(bool getStuck)
        {
            if (isActiveAndEnabled && machine) machine.Vend(row, column, getStuck);
        }

        [ContextMenu("Press (No Jam)")]
        private void PressNoJam() => Press(false);

        [ContextMenu("Press (Jam)")]
        private void PressJam() => Press(true);
    }
}
