# Roadside vehicles

- `Pickup.fbx`: 986 triangles, open bed, approximately 5.3 m long.
- `Sedan.fbx`: 906 triangles, four doors, approximately 4.8 m long.
- Both use metre units, Y up, and model-space +Z forward, with their root at ground level.
- Each model has a `Body` mesh and four named wheel meshes with pivots at the axle centers. Wheels rotate around their local X axis.

## Change the paint

In Unity, edit the Base Map color of `Pickup_BodyPaint.mat` or `Sedan_BodyPaint.mat`. The FBX importers are already mapped to these materials. Glass, tires, trim, and lights use separate materials.

For several color variants at once, duplicate the paint material and assign the copy to the paint slot on the instance's `Body` Mesh Renderer. Editing the original material recolors every instance using it.

No textures, driving scripts, or colliders are required by the meshes. Add vehicle behavior and suitable colliders to prefabs as needed.

The preview was rendered from the exported FBX geometry; it is not a Unity lighting screenshot. Regenerate assets with `python Tools/build_vehicles.py` and the preview with `python Tools/preview_vehicles.py` from the project directory.
