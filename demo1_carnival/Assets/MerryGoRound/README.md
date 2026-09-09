# Low-poly merry-go-round for Unity

## Import

Copy the `MerryGoRound` folder into your Unity project's `Assets` folder. Use `Models/LowPolyMerryGoRound.dae`; it preserves its hierarchy and has URP materials applied without scripts.

Drag `Models/LowPolyMerryGoRound.dae` directly into the scene. `Carousel_Body` is one UV-unwrapped mesh. The horses, poles, and lights remain separate children.

## Editable parts

- `Horse_01` through `Horse_08` are independent child objects.
- `Horse_Pole_01` through `Horse_Pole_08` are independent child objects.
- `Light_01` through `Light_16` are independent child objects.
- `Carousel_Body` uses the included `Carousel_Body_Albedo.png` UV texture.
- `Light_01` through `Light_16` use the emissive `Lights` material and remain independent renderers.

Materials can be edited in `Assets/MerryGoRound/Materials`.
