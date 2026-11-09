using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Scavenger.Vending.Editor
{
    // Batch-only integration check; leaves the user's saved scenes untouched.
    public static class VendingMachinePlaybackCheck
    {
        private const string Key = "Scavenger.Vending.PlaybackCheck";
        private static VendingMachine machine;
        private static int completed;
        private static double deadline;
        private static bool jamStarted;
        private static bool otherLaneStarted;
        private static Vector3 stuckPosition;
        private static Quaternion stuckRotation;

        public static void Begin()
        {
            VendingMachineBuilder.Build();
            VendingMachinePreview.Render();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VendingMachine/Generated/VendingMachine.prefab"));
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (SessionState.GetBool(Key, false))
            {
                deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update += Tick;
            }
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Playback check timed out.");
                if (!EditorApplication.isPlaying) return;
                if (!machine)
                {
                    machine = UnityEngine.Object.FindFirstObjectByType<VendingMachine>();
                    if (!machine) throw new Exception("Missing vending machine in Play Mode.");
                    machine.transform.SetPositionAndRotation(new Vector3(2, 0, -3), Quaternion.Euler(0, 32, 0));
                    machine.transform.localScale = Vector3.one * 1.2f;
                    machine.turnDuration = .2f; machine.dropDuration = .2f;
                    machine.Vend(-1); machine.Vend(9);
                    machine.Vend(0, 1); machine.Vend(1, 4);
                    if (machine.IsVending) throw new Exception("Invalid lane accepted.");
                    machine.transform.Find("Buttons/Button_A1").GetComponent<VendingButton>().Press();
                    machine.Vend(1);
                    if (!machine.IsVending || machine.lanes[1].nextProduct != 0) throw new Exception("Busy guard failed.");
                    return;
                }
                if (machine.IsVending) return;
                if (jamStarted)
                {
                    var jammedLane = machine.lanes[1];
                    var stuck = jammedLane.stuckProduct;
                    if (!jammedLane.IsJammed || !stuck || !stuck.IsChildOf(machine.transform) || jammedLane.nextProduct != 1)
                        throw new Exception("Jammed item or stock state is incorrect.");
                    if (!otherLaneStarted)
                    {
                        float maxZ = float.NegativeInfinity;
                        var box = stuck.GetComponent<BoxCollider>();
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 corner = box.center + Vector3.Scale(box.size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                            maxZ = Mathf.Max(maxZ, machine.transform.InverseTransformPoint(stuck.TransformPoint(corner)).z);
                        }
                        if (Mathf.Abs(maxZ - .358f) > .002f) throw new Exception("Stuck item is not touching the inner glass surface.");
                        if (Mathf.Abs(Quaternion.Angle(machine.transform.rotation, stuck.rotation) - 22) > .1f)
                            throw new Exception("Stuck snack did not tip forward.");
                        stuckPosition = stuck.position; stuckRotation = stuck.rotation;
                        machine.Vend(1, false);
                        if (machine.IsVending) throw new Exception("Jammed lane accepted another vend.");
                        VendingMachinePreview.RenderImage(machine.gameObject, "Assets/VendingMachine/JamPreview.png");
                        otherLaneStarted = true;
                        machine.Vend(1, 3, false);
                        if (!machine.IsVending) throw new Exception("Jam blocked a different lane.");
                        return;
                    }
                    if (Vector3.Distance(stuck.position, stuckPosition) > .0001f || Quaternion.Angle(stuck.rotation, stuckRotation) > .01f)
                        throw new Exception("Jammed snack did not remain stuck.");
                    var delivered = machine.lanes[2].products[0];
                    if (delivered.parent != null || Vector3.Distance(delivered.position, machine.deliveryPoint.position) > .001f)
                        throw new Exception("Explicit false did not dispense normally.");
                    Debug.Log("VENDING PLAYBACK PASSED: normal stock exhaustion, bool false delivery, bool true glass contact and forward tilt, stable jam, blocked jammed lane, working other lane, transformed machine.");
                    Finish(0);
                    return;
                }
                completed++;
                var lane = machine.lanes[0];
                if (lane.nextProduct != completed) throw new Exception("Incorrect stock count.");
                var snack = lane.products[completed - 1];
                if (snack.parent != null || Vector3.Distance(snack.position, machine.deliveryPoint.position) > .001f)
                    throw new Exception("Snack did not reach the pickup tray.");
                if (Quaternion.Angle(lane.screw.localRotation, Quaternion.identity) > .01f) throw new Exception("Screw did not finish its revolution.");
                if (completed < 3) { machine.Vend(0); return; }
                machine.Vend(0);
                if (machine.IsVending) throw new Exception("Empty lane accepted a vend.");
                jamStarted = true;
                machine.transform.Find("Buttons/Button_A2").GetComponent<VendingButton>().Press(true);
                if (!machine.IsVending) throw new Exception("Jam animation did not start.");
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
