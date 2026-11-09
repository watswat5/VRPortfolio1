# Low-poly flower bouquet

Ten seven-petal flowers in coral, peach, cream, and pink, with golden centers,
faceted green leaves, and a hollow teal ceramic vase. Original procedural geometry.

- 2,780 triangles; flat shading; no texture dependencies.
- Approximately 0.56 m tall, authored in metres with Y up.
- Pivot at the bottom center of the vase for placement on tables.
- OBJ + MTL are portable source assets. The PNG previews the exported geometry.

## Unity

`FlowerBouquetBuilder` automatically creates `FlowerBouquet.prefab` and native
meshes/materials in `UnityAssets` after the Editor imports its script outside Play
mode. You can also run **Tools > Props > Build Flower Bouquet**.

Drag the prefab into the scene. It uses URP Lit materials and a box collider on
the vase only. It is a decorative prop without a Rigidbody or grab interaction.
Rebuilding updates generated meshes; existing material edits are preserved.

To regenerate the portable geometry and preview, run
`python Tools/generate_flower_bouquet.py` from the project directory, then rebuild
the Unity prefab from the menu.
