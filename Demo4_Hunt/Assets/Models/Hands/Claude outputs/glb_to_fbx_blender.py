"""Convert the low-poly hand GLBs to FBX (all pose clips included) and save .blend copies.

Keep this script in the same folder as the .glb files, then run (Blender as a Flatpak):
    flatpak run org.blender.Blender --background --python /full/path/to/glb_to_fbx_blender.py
"""
import bpy, os

here = os.path.dirname(os.path.abspath(__file__))  # works from any folder
for side in ("R", "L"):
    src = os.path.join(here, f"LowPolyHand_{side}.glb")
    if not os.path.exists(src):
        print("missing", src); continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    # Blender's glTF importer adds an "Icosphere" helper mesh that it uses as the bone
    # display shape. It is not part of the hand, so drop it before saving/exporting.
    for ob in bpy.data.objects:
        if ob.type == "ARMATURE":
            for pb in ob.pose.bones:
                pb.custom_shape = None
    for ob in [o for o in bpy.data.objects if o.name.startswith("Icosphere")]:
        bpy.data.objects.remove(ob, do_unlink=True)
    # Keep every pose as a named action with a fake user so it survives saving.
    for act in bpy.data.actions:
        act.use_fake_user = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(here, f"LowPolyHand_{side}.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(here, f"LowPolyHand_{side}.fbx"),
        use_selection=False, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0, apply_scale_options="FBX_SCALE_UNITS",
    )
    print("wrote", side)
