# Conveyor portal

- Overall size: **0.8 m wide × 2.0 m tall × 0.6 m deep**
- Clear opening: **0.5 m wide × 1.7 m tall**
- Frame thickness: **0.15 m sides, 0.3 m header**
- Origin: bottom center, so the portal can be lowered through the floor without changing its opening dimensions
- Geometry: one connected U-shaped mesh with no overlapping boxes

The OBJ intentionally has material importing disabled to prevent Unity's Linux OBJ importer from trying to extract an external material folder. Assign `Assets/Materials/brushed_metal_frame.mat` to the Mesh Renderer. A non-convex `MeshCollider` is appropriate when the portal is stationary; use three primitive box colliders if it must move as a Rigidbody.
