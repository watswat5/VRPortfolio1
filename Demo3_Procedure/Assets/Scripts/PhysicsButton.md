# PhysicsButton setup

1. Add `PhysicsButton` to the stationary button housing (or to an empty child of the object it is mounted on).
2. Make the visible plunger a child object with a non-trigger collider and a dynamic `Rigidbody`. Configure gravity, interpolation, and collision detection in the Inspector; the script does not change these settings.
3. Assign that Rigidbody to **Plunger Body**, set **Local Press Direction**, and set **Travel** in metres.
4. Connect **On Pressed** and **On Released** in the Inspector.

Physical motion is always enabled. To allow button events only after another gameplay step, turn off **Start Interactable** and activate it from code:

```csharp
button.SetInteractable(true);
button.Pressed += HandlePressed;
```

`IsInteractable`, `IsPressed`, `NormalizedTravel`, and `LastPressSpeed` expose its current state. `LastPressSpeed` is the plunger's mounting-relative speed when the press threshold was crossed.

The starting plunger center is point A. A `ConfigurableJoint` locks sideways movement and rotation, permits axial movement up to Travel on either side of A, and applies its built-in spring/damper toward A. Only displacement in Local Press Direction counts as a press. There is no scripted AddForce, position correction, or kinematic switching. If the housing is under a Rigidbody—such as an `XRGrabInteractable`—the joint connects to that body. Without a host Rigidbody, the joint connects to a fixed world-space anchor; no extra object or Rigidbody is created. It also reconnects if reparenting changes the host Rigidbody.

For reliable VR contact, hand/controller objects need physics colliders. Do not make the plunger kinematic. Keep the housing collider from overlapping the plunger throughout its travel. The default spring values suit a small plunger around 50–200 g; heavier plungers generally need a stronger spring and damping.

Code can subscribe without depending on UnityEvents:

```csharp
button.Released += HandleReleased;
```

## Pressure plate wiring in SampleScene

The `05_PressurePlate` root is the housing and keeps its existing grab and socket components. Its `PhysicsButton` starts non-interactable. `Plate_Plunger` in the prefab owns a 0.1 kg Rigidbody, a contact collider, and the `Pressure_Plate` and `Impact_Target` visuals. The collar and underbase stay on the housing. Travel is 0.02 m along local down.

The scene's existing `collider (1)` is authored as a trigger for grabbing. The plunger's separate non-trigger collider supplies physical contact.

`SocketedPart.IsMountComplete` becomes true only after the loose Rigidbody has been removed and the final snapped pose synchronized. `MineAssembler` discovers the mounted plate's button and enables it only in `smack_plate`; leaving that step (including failure) disables its events while movement remains enabled. No Inspector press event is needed: the assembler subscribes in code. Existing soft/seated/detonated speed thresholds still apply.

Play-mode check: grab and snap the loose plate, confirm it can move before mounting completes without firing gameplay events, then press during `smack_plate`. A soft press should release for another attempt; a valid press advances to `stamp`; an excessive-speed press enters `failed`. Both terminal outcomes should disable button events while leaving it movable. A press held across activation must release before a new press counts. Also check contact while moving the assembly and tune travel/spring/speed thresholds in VR.
