# Socket assembly scene

`Assets/Scenes/SampleScene.unity` contains the migration. Components, colliders, handles, input bindings, grab transformers, and references are saved in the scene. No custom runtime bootstrap adds GameObjects or components.

## Interaction flow

1. Grab the spring, move it into SpringZone, and release the trigger. The socket holds it.
2. Grab the child winding key and circle that hand's thumbstick clockwise five times. Release and re-grab the key as needed; progress is retained.
3. Release the cover into CoverZone.
4. Release each of the three screws into an available screw socket. Grab each screw's child turning handle and circle the thumbstick clockwise three times. The screw mesh, colliders and handle descend as it turns, stopping when the underside of the head reaches the cover rim. Tightening completes at that seated position.
5. Release the pressure plate into PlateZone.

Mounted parts cannot be pulled out by the hands. The separate `XRSimpleInteractable` handles rotate child visuals; screw visuals also descend to authored seating surfaces. Winding uses the thumbstick, not wrist rotation. The spring key remains mounted after winding.

## Scene configuration

- Six standard `XRSocketInteractor` components: SpringZone, CoverZone, three ScrewZones, and PlateZone.
- `Socket Scale Mode = None`, root attachment points, and scale tracking disabled on the six mountable grabs preserve the user's scene scale.
- Sockets accept only their corresponding interaction layer: Spring, Cover, Screw, or Plate. The hands also accept Mounted Handle.
- `SocketedPart` is a select filter that prevents a hand from stealing a socket-selected part and enables its mounted handle. It does not reparent parts or write transforms/physics.
- Each handle has its own trigger collider, explicitly excluded from the parent grab's collider list.
- `ThumbstickWinder` holds the inline left/right `primary2DAxis` bindings and turns the referenced child visual. SpringController/ScrewController relay completion to MineAssembler.
- MineAssembler subscribes to socket selection and completion events. It unlocks the next sockets in assembly order. Occupied sockets stay active.
- Socket and handle parent-interactable references establish XRI's processing order for moving assemblies.
- The two additional screws and all scene additions are authored in the saved scene. Screw instances were unpacked to create their visual pivots; source prefabs are unchanged.

## Existing unfinished behavior

Plate-impact and stamp-detection scripts were empty before this migration. They remain unimplemented; `MineAssembler.NotifyPlateSeated()` and `NotifyStamped()` are the completion hooks for those future behaviors. The regression checks those hooks, not physical smacking or stamping. Key detachment/disposal was also not implemented in the original spring controller.

## Verification

- `Tools > Assembly > Validate Socket Scene` checks references, component counts, trigger colliders, exclusive collider ownership, scale settings, child visual references, bindings, and missing scripts.
- `Tools > Assembly > Run Socket Regression` briefly enters Play Mode with headset initialization temporarily disabled, tests the actual socket/selection pipeline and simulated left/right thumbstick input, then restores XR initialization and exits Play Mode. Run it with SampleScene open and Play Mode stopped. It does not save Play Mode changes.
- Reports are written to `Temp/SocketMigration-report.txt` and `Temp/SocketRegression-report.txt`.
- `Tools > Assembly > Migrate Scene to XRI Sockets` is an editor-only migration tool. The scene is already migrated; it is not needed during normal use.

Final headset testing should confirm reachability and visual alignment at the chosen scene scale.
