# Low-poly vending machine

Drag `Generated/VendingMachine.prefab` into your Unity scene. Materials target the project's Universal Render Pipeline. The machine measures approximately 1.2 × 2.06 × 0.9 meters including the pickup tray. Its origin is on the floor, Y is up, and +Z is the front.

## Vend a snack

In Play Mode, use the VendingMachine component's context menu **Vend first lane (Play Mode)**, or call `machine.Vend(1, 1)` for A1. Rows A�C run top-to-bottom and columns 1�3 run left-to-right. Row/column arguments are one-based: `Vend(2, 3, true)` jams B3. The original zero-based `Vend(laneIndex)` and `Vend(laneIndex, getStuck)` calls still work.

Each object under `Buttons/Button_A1` through `Button_C3` has its own BoxCollider, VendingButton component, cap mesh, and child label. Connect your XR/select UnityEvent to that button's `VendingButton.Press()`, or call `hit.collider.GetComponentInParent<VendingButton>()?.Press()` from your raycast interaction. `Press(true)` produces a jam. Buttons have no dependency on a particular input or XR package; the interaction system must call Press.

Each vend rotates the chosen spiral one full turn, advances the remaining snacks by 0.14 m, and drops the front snack into the collection tray. Each lane holds three snacks. Empty lanes and additional calls during a vend are ignored. Delivered snacks have a BoxCollider and detach from the machine; add your project's grab component as needed. The drop is scripted, not physics-driven. Reload the prefab/scene to restock.

### Get stuck against the glass

Call `machine.Vend(0, true)` to jam the first lane. The screw completes its turn, then the front snack tips forward, rebounds slightly against the glass, and remains wedged above its shelf. `Vend(0)` and `Vend(0, false)` dispense normally. The component context menu also includes **Jam first lane (Play Mode)**.

Read `machine.lanes[0].IsJammed` and `machine.lanes[0].stuckProduct` to inspect the jam. The stuck snack stays attached to the machine and leaves the stock queue; further vends on that lane are ignored while it exists. Other lanes still work once the animation finishes. Destroy the stuck object to clear the jam, or reload the scene to reset everything. The effect uses a scripted pose against the front pane, so it does not require a Rigidbody.

## Animate it yourself

Find `Lanes/Lane_01/Screw_PivotZ` (and lanes 02–09). Every spiral is a separate mesh, with its origin centered on the spiral's rear axis. Rotate around **local Z**, using -360 degrees for forward vending. Use an unwrapped Euler animation curve for a full revolution. Products are siblings of the spiral so they remain upright. Advance them toward local +Z by 0.14 m per revolution. Avoid driving the script and an Animator on the same parts simultaneously.

Cabinet, shelves, controls, spirals, and snacks use solid-color materials and flat mesh normals. A transparent front pane covers the snack display, leaving the collection opening below it accessible. Basic cabinet and glass colliders are included. No textures, external model packages, or runtime mesh generation are required.

## Rebuild

**Tools → Scavenger → Build Vending Machine** recreates the generated prefab, meshes, and materials. It overwrites edits inside `Generated`; duplicate the prefab before customizing it. **Tools → Scavenger → Validate and Render Vending Machine** checks mesh geometry and lane references and updates `Preview.png`.
