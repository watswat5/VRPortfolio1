import bpy
from pathlib import Path

asset_dir = Path(__file__).resolve().parent
fbx = asset_dir / "PointingHand.fbx"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(fbx))

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
assert len(meshes) == 3, f"Expected 3 mesh objects, found {len(meshes)}"
assert {obj.name for obj in meshes} == {
    "PointingHand_Glove",
    "PointingHand_Cuff",
    "PointingHand_Creases",
}
assert sum(len(obj.data.polygons) for obj in meshes) > 1000
assert all(obj.dimensions.length > 0 for obj in meshes)

print("FBX validation passed")
print("Meshes:", ", ".join(sorted(obj.name for obj in meshes)))
print("Triangles:", sum(len(obj.data.polygons) for obj in meshes))
