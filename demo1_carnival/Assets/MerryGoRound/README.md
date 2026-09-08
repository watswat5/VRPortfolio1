# Low-poly merry-go-round for Unity

## Import

Copy the `MerryGoRound` folder into your Unity project's `Assets` folder. Use `Models/LowPolyMerryGoRound.dae`; it preserves its hierarchy and has URP materials applied without scripts.

Drag `Models/LowPolyMerryGoRound.dae` directly into the scene. Its root contains 40 separately selectable child objects. You can make a prefab by dragging that scene instance back into the Project window.

## Editable parts

- `Horse_01` through `Horse_08` are independent child objects.
- `Horse_Pole_01` through `Horse_Pole_08` are independent child objects.
- `Light_01` through `Light_16` are independent child objects.
- Seven included URP materials color the carousel. `Light_01` through `Light_16` use the emissive `Lights` material and remain independent renderers.

Materials can be edited in `Assets/MerryGoRound/Materials`.
