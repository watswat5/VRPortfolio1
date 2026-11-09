import bpy
import math
from mathutils import Vector
from pathlib import Path


OUT_DIR = Path(__file__).resolve().parent
FBX_PATH = OUT_DIR / "PointingHand.fbx"
BLEND_PATH = OUT_DIR / "PointingHand.blend"
PREVIEW_PATH = OUT_DIR / "PointingHand_preview.png"


def material(name, color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def smooth(obj):
    if obj.type == "MESH":
        for poly in obj.data.polygons:
            poly.use_smooth = True


def uv_sphere(name, location, scale, mat, segments=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, location=location
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    smooth(obj)
    return obj


def rounded_cube(name, location, scale, radius, mat):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new("Soft glove edges", "BEVEL")
    bevel.width = radius
    bevel.segments = 5
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.data.materials.append(mat)
    smooth(obj)
    return obj


def capsule_between(name, start, end, radius, mat):
    start, end = Vector(start), Vector(end)
    delta = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32, radius=radius, depth=delta.length, location=midpoint
    )
    body = bpy.context.object
    body.name = name
    body.rotation_mode = "QUATERNION"
    body.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(delta.normalized())
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    body.data.materials.append(mat)
    smooth(body)
    a = uv_sphere(name + "_A", start, (radius, radius, radius), mat, 24, 16)
    b = uv_sphere(name + "_B", end, (radius, radius, radius), mat, 24, 16)
    return [body, a, b]


def make_curve(name, points, radius, mat):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 4
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bp, co in zip(spline.bezier_points, points):
        bp.co = co
        bp.handle_left_type = "AUTO"
        bp.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def join_objects(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    smooth(obj)
    return obj


# Clean scene.
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    pass

white = material("Glove White", (0.92, 0.94, 0.98), roughness=0.42)
black = material("Cuff Black", (0.025, 0.03, 0.045), roughness=0.3)
crease = material("Crease Dark", (0.13, 0.15, 0.19), roughness=0.52)

# The glove points along Unity +Z. +Y is the back of the hand.
parts = []
parts.append(rounded_cube("Palm", (0.08, 0.0, 0.0), (0.47, 0.18, 0.48), 0.18, white))
parts.append(uv_sphere("PalmPad", (0.02, -0.01, 0.15), (0.52, 0.22, 0.48), white))

# Long index finger and rounded fingertip.
parts += capsule_between("Index", (-0.26, 0.01, 0.20), (-0.26, 0.01, 1.34), 0.175, white)

# Three curled fingers make the unmistakable cursor-hand fist.
finger_paths = [
    ((0.02, 0.00, 0.34), (0.11, -0.12, 0.46), (0.16, -0.20, 0.20), (0.13, -0.20, -0.02)),
    ((0.24, 0.00, 0.30), (0.34, -0.11, 0.39), (0.39, -0.20, 0.13), (0.34, -0.19, -0.09)),
    ((0.43, -0.01, 0.22), (0.51, -0.10, 0.29), (0.53, -0.18, 0.04), (0.45, -0.17, -0.15)),
]
for i, pts in enumerate(finger_paths, 2):
    parts.append(make_curve(f"CurledFinger_{i}", pts, 0.145 - (i - 2) * 0.008, white))

# Thumb sweeping across the front of the curled fingers.
parts.append(make_curve(
    "Thumb",
    ((-0.39, -0.01, 0.15), (-0.52, -0.06, 0.04), (-0.43, -0.16, -0.20), (-0.17, -0.19, -0.29)),
    0.165,
    white,
))

# Join the white geometry into one selectable hand mesh. Overlaps are intentional
# and produce a soft, toy-like classic glove silhouette without fragile booleans.
hand = join_objects(parts, "PointingHand_Glove")

# Chunky black wrist cuff, slightly flared toward the arm.
bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=0.47, radius2=0.39, depth=0.34, location=(0.08, 0.0, -0.57))
cuff = bpy.context.object
cuff.name = "PointingHand_Cuff"
cuff.scale.y = 0.62
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bevel = cuff.modifiers.new("Rounded cuff", "BEVEL")
bevel.width = 0.055
bevel.segments = 4
bpy.context.view_layer.objects.active = cuff
bpy.ops.object.modifier_apply(modifier=bevel.name)
cuff.data.materials.append(black)
smooth(cuff)

# Raised dark seam marks evoke the thick black linework of old cursor art.
seams = []
for x, z, sx in ((0.10, 0.35, 0.11), (0.32, 0.28, 0.10), (0.49, 0.18, 0.085)):
    seam = rounded_cube(f"FingerCrease_{len(seams)+1}", (x, -0.205, z), (sx, 0.018, 0.026), 0.018, crease)
    seams.append(seam)
seam_mesh = join_objects(seams, "PointingHand_Creases")

# Root object keeps transforms clean in Unity.
bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0))
root = bpy.context.object
root.name = "PointingHand"
for obj in (hand, cuff, seam_mesh):
    obj.parent = root

# Save editable source before export.
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

# FBX in meters, with Unity-friendly axes and no cameras/lights.
bpy.ops.object.select_all(action="DESELECT")
for obj in (root, hand, cuff, seam_mesh):
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(
    filepath=str(FBX_PATH),
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

# Render a compact preview without exporting preview-only objects to FBX.
bpy.ops.object.camera_add(location=(2.8, 2.55, 2.65))
camera = bpy.context.object
camera.name = "PreviewCamera"
bpy.context.scene.camera = camera
direction = Vector((0.02, 0.0, 0.38)) - camera.location
camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
camera.data.lens = 58

bpy.ops.object.light_add(type="AREA", location=(1.7, 2.5, 2.8))
bpy.context.object.data.energy = 850
bpy.context.object.data.shape = "DISK"
bpy.context.object.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, 0.7, 1.0))
bpy.context.object.data.energy = 520
bpy.context.object.data.color = (0.35, 0.55, 1.0)
bpy.context.object.data.size = 2.2
bpy.ops.object.light_add(type="AREA", location=(0.0, -2.2, 1.5))
bpy.context.object.data.energy = 600
bpy.context.object.data.color = (1.0, 0.45, 0.2)
bpy.context.object.data.size = 2.0

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 720
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True
scene.render.filepath = str(PREVIEW_PATH)
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)

print(f"Created {FBX_PATH}")
print(f"Created {BLEND_PATH}")
print(f"Created {PREVIEW_PATH}")
