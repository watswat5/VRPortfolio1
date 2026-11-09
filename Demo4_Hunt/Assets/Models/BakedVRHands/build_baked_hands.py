import bpy
from mathutils import Vector
from pathlib import Path


OUT = Path(__file__).resolve().parent


def mat(name, color):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = 0.72
    return material


GLOVE = mat("LowPoly Glove", (0.78, 0.84, 0.95))
CUFF = mat("LowPoly Cuff", (0.055, 0.075, 0.12))


def apply(obj):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for face in obj.data.polygons:
        face.use_smooth = False
    return obj


def ico(name, location, scale, material=GLOVE, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply(obj)
    obj.data.materials.append(material)
    return obj


def faceted_box(name, location, scale, bevel, material=GLOVE):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply(obj)
    modifier = obj.modifiers.new("Faceted corners", "BEVEL")
    modifier.width = bevel
    modifier.segments = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.materials.append(material)
    return apply(obj)


def segment(name, start, end, radius, material=GLOVE, vertices=8):
    start, end = Vector(start), Vector(end)
    delta = end - start
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=delta.length,
        end_fill_type="TRIFAN",
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(delta.normalized())
    obj.data.materials.append(material)
    apply(obj)
    return [
        obj,
        ico(name + "_base", start, (radius, radius, radius), material, 1),
        ico(name + "_tip", end, (radius, radius, radius), material, 1),
    ]


def join(items, name):
    bpy.ops.object.select_all(action="DESELECT")
    for item in items:
        item.select_set(True)
    bpy.context.view_layer.objects.active = items[0]
    bpy.ops.object.join()
    result = bpy.context.object
    result.name = name
    apply(result)
    return result


def cuff(pose):
    bpy.ops.mesh.primitive_cone_add(
        vertices=8, radius1=0.43, radius2=0.36, depth=0.30, location=(0, 0, -0.54)
    )
    obj = bpy.context.object
    obj.name = pose + "_Cuff"
    obj.scale.y = 0.58
    apply(obj)
    bevel = obj.modifiers.new("Cuff bevel", "BEVEL")
    bevel.width = 0.045
    bevel.segments = 1
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.data.materials.append(CUFF)
    return apply(obj)


def common_palm(pose):
    return [
        faceted_box(pose + "_Palm", (0, 0, -0.03), (0.42, 0.18, 0.43), 0.16),
        ico(pose + "_PalmPad", (0, -0.035, 0.07), (0.46, 0.20, 0.39), subdivisions=2),
    ]


def pointing():
    pose = "PointingHand"
    parts = common_palm(pose)
    # Oversized raised index, aligned with Unity +Z.
    parts += segment(pose + "_Index", (-0.24, 0.0, 0.18), (-0.24, 0.0, 1.24), 0.145)
    # Three fingers folded into the palm.
    for i, (x, z, width) in enumerate(((0.03, 0.28, 0.145), (0.24, 0.20, 0.14), (0.40, 0.11, 0.13))):
        parts += segment(pose + f"_Folded_{i}", (x, 0.0, z), (x + 0.035, -0.13, -0.10), width)
        parts.append(ico(pose + f"_Knuckle_{i}", (x, -0.13, z + 0.02), (width * 1.15, width, width * 1.2), subdivisions=1))
    # Thumb crosses the folded fingers.
    parts += segment(pose + "_Thumb", (-0.37, -0.01, 0.08), (-0.12, -0.19, -0.22), 0.145)
    return join(parts, pose + "_Glove"), cuff(pose)


def fist():
    pose = "FistHand"
    parts = common_palm(pose)
    # Four blocky curled fingers across the face of the palm.
    for i, (x, z, r) in enumerate(((-0.29, 0.25, 0.15), (-0.10, 0.30, 0.155), (0.10, 0.28, 0.15), (0.29, 0.20, 0.14))):
        parts.append(ico(pose + f"_Knuckle_{i}", (x, -0.08, z), (r, r * 0.92, r * 1.15), subdivisions=1))
        parts += segment(pose + f"_Finger_{i}", (x, -0.03, z), (x, -0.15, -0.10), r * 0.88)
    # Thumb locks diagonally over the curled fingertips.
    parts += segment(pose + "_Thumb", (-0.39, 0.0, 0.06), (0.20, -0.20, -0.15), 0.145)
    parts.append(ico(pose + "_ThumbPad", (0.17, -0.20, -0.14), (0.18, 0.13, 0.15), subdivisions=1))
    return join(parts, pose + "_Glove"), cuff(pose)


def export_pose(name, objects):
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0))
    root = bpy.context.object
    root.name = name
    # Rebase geometry so the FBX origin sits at the wrist opening.
    for obj in objects:
        obj.location.z += 0.69
        obj.location *= 0.12
        obj.scale *= 0.12
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        obj.parent = root
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(OUT / f"{name}.fbx"),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
    )
    return root


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

point_objects = pointing()
point_root = export_pose("PointingHand_LowPoly", point_objects)
fist_objects = fist()
fist_root = export_pose("FistHand_LowPoly", fist_objects)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "BakedVRHands.blend"))

# Preview both baked models side by side.
point_root.location.x = -0.14
fist_root.location.x = 0.14
bpy.ops.object.camera_add(location=(0.55, -0.72, 0.48))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 0, 0.105)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.lens = 58
bpy.context.scene.camera = camera
for loc, energy, color, size in (
    ((2.5, 3.0, 3.5), 900, (1.0, 0.82, 0.70), 3.0),
    ((-3.0, 1.0, 2.0), 700, (0.35, 0.55, 1.0), 2.5),
    ((0.0, -2.5, 1.5), 500, (0.45, 0.65, 1.0), 2.0),
):
    bpy.ops.object.light_add(type="AREA", location=loc)
    light = bpy.context.object
    light.data.energy = energy
    light.data.color = color
    light.data.shape = "DISK"
    light.data.size = size
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True
scene.render.filepath = str(OUT / "BakedVRHands_preview.png")
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)

for name in ("PointingHand_LowPoly", "FistHand_LowPoly"):
    print("Created", OUT / f"{name}.fbx")
