import bpy
import math
import os
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "Assets", "Models", "ACME_PropFactory")
os.makedirs(OUT, exist_ok=True)


def clean():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        pass


def mat(name, color, metallic=0.0, rough=0.45):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    return m


YELLOW = mat("ACME Yellow", (1.0, 0.55, 0.025), 0.15, 0.32)
RED = mat("Warning Red", (0.8, 0.035, 0.025), 0.05, 0.3)
DARK = mat("Rubber Black", (0.025, 0.03, 0.035), 0.0, 0.7)
STEEL = mat("Toy Steel", (0.24, 0.29, 0.32), 0.75, 0.28)
WHITE = mat("Label Cream", (0.95, 0.88, 0.67), 0.0, 0.6)
BLUE = mat("Wire Blue", (0.03, 0.25, 0.9), 0.1, 0.3)
GREEN = mat("Safe Green", (0.05, 0.72, 0.18), 0.05, 0.35)


def finish(obj, material, bevel=0.035):
    obj.data.materials.append(material)
    if bevel:
        mod = obj.modifiers.new("Soft toy edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth_by_angle()
    obj.select_set(False)
    return obj


def cube(name, loc, scale, material, bevel=0.035):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, material, bevel)


def cyl(name, loc, radius, depth, material, verts=24, rotation=(0, 0, 0), bevel=0.025):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rotation)
    o = bpy.context.object
    o.name = name
    return finish(o, material, bevel)


def torus(name, loc, major, minor, material, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=24, minor_segments=8,
                                    location=loc, rotation=rotation)
    o = bpy.context.object
    o.name = name
    return finish(o, material, 0)


def text_obj(body, name, loc, size=0.18, extrude=0.012, align='CENTER'):
    bpy.ops.object.text_add(location=loc, rotation=(math.pi / 2, 0, 0))
    o = bpy.context.object
    o.name = name
    o.data.body = body
    o.data.align_x = align
    o.data.align_y = 'CENTER'
    o.data.size = size
    o.data.extrude = extrude
    o.data.bevel_depth = 0.004
    o.data.materials.append(DARK)
    return o


def parent_group(name, objects):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 0.18
    for o in objects:
        o.parent = root
    return root


def helix(name, loc, radius=0.16, height=0.42, turns=4):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = 0.028
    curve.bevel_resolution = 2
    spline = curve.splines.new('POLY')
    count = 65
    spline.points.add(count - 1)
    for i, p in enumerate(spline.points):
        t = i / (count - 1)
        a = t * turns * math.tau
        p.co = (loc[0] + math.cos(a) * radius, loc[1] + math.sin(a) * radius, loc[2] + t * height, 1)
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(STEEL)
    return o


def bowl(name, radius=.5, height=.34, thickness=.075, segments=32, rings=8):
    """Open, thick-walled hemispherical bowl with visible inner cavity."""
    verts, faces = [], []
    # Outer rings run bottom-to-rim; inner rings run rim-to-inner-bottom.
    for side in range(2):
        for j in range(rings + 1):
            t = j / rings
            theta = t * math.pi / 2 if side == 0 else (1 - t) * math.pi / 2
            rad = radius if side == 0 else radius - thickness
            h = height if side == 0 else height - thickness
            z0 = .035 if side == 0 else .11
            # Start the outer profile with a broad circular foot instead of a
            # spherical point, so the shell can stand securely on a table.
            rr = max(.18, rad * math.sin(theta)) if side == 0 else rad * math.sin(theta)
            zz = z0 + h * (1 - math.cos(theta))
            for i in range(segments):
                a = i * math.tau / segments
                verts.append((rr * math.cos(a), rr * math.sin(a), zz))
    width = segments
    total_rings = (rings + 1) * 2
    for j in range(total_rings - 1):
        for i in range(segments):
            a = j * width + i
            b = j * width + (i + 1) % segments
            c = (j + 1) * width + (i + 1) % segments
            d = (j + 1) * width + i
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(range(segments))))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(o)
    finish(o, YELLOW, .018)
    return o


def clock_spring(name, loc=(0,0,.26), turns=3.25):
    """Flat Archimedean spiral strip, visually distinct from a compression spring."""
    verts, faces = [], []
    steps = 96
    strip = .035
    for i in range(steps):
        t = i / (steps - 1)
        angle = t * turns * math.tau
        center_r = .045 + .16 * t
        for edge in (-strip / 2, strip / 2):
            r = center_r + edge
            verts.append((loc[0] + r*math.cos(angle), loc[1] + r*math.sin(angle), loc[2]))
    for i in range(steps - 1):
        a = i * 2
        faces.append((a, a+1, a+3, a+2))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(STEEL)
    solid = o.modifiers.new("Clock spring thickness", "SOLIDIFY")
    solid.thickness = .018
    bevel = o.modifiers.new("Clock spring edge", "BEVEL")
    bevel.width = .006
    bevel.segments = 2
    return o


def make_shell():
    parts = [bowl("Hollow_Bowl_Shell")]
    parts += [torus("Shell_Rim", (0, 0, .375), .46, .045, DARK)]
    for i in range(8):
        a = i * math.tau / 8
        parts.append(cyl(f"Shell_Bolt_{i+1}", (.43*math.cos(a), .43*math.sin(a), .39), .028, .035, STEEL, verts=12))
    return parent_group("01_CartoonShell", parts)


def make_spring():
    parts = [cyl("ClockSpring_Cartridge", (0, 0, .07), .27, .14, RED, verts=24, bevel=.035),
             cyl("ClockSpring_Face", (0, 0, .16), .235, .045, DARK, verts=24, bevel=.02),
             clock_spring("Flat_ClockSpring", (0, 0, .19)),
             cyl("Winding_Hub", (0, 0, .225), .045, .09, STEEL, verts=16)]
    parts.append(cube("Windup_Key_Stem", (0, 0, .31), (.035, .035, .09), STEEL, .015))
    parts.append(cube("Windup_Key_Grip", (0, 0, .41), (.16, .04, .04), STEEL, .025))
    return parent_group("02_WindupSpring", parts)


def make_top_plate():
    body = cyl("TopPlate_Body", (0,0,.055), .47, .11, STEEL, verts=32, bevel=.035)
    parts = [body,
             cyl("PressurePlate_AlignmentPost", (0,0,.155), .035, .09, DARK, verts=20, bevel=.008),
             torus("Post_Snap_Ridge", (0,0,.195), .033, .006, GREEN)]
    for i in range(3):
        a = math.radians(90 + i * 120)
        x, y = .33 * math.cos(a), .33 * math.sin(a)
        # A dark metal rim gives the user a clear target, while the cylinder
        # cutter creates actual empty geometry through the plate.
        parts.append(torus(f"ScrewHole_Rim_{i+1}", (x,y,.115), .052, .014, DARK))
        bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=.034, depth=.3, location=(x,y,.06))
        cutter = bpy.context.object
        cutter.name = f"ScrewHole_Cutter_{i+1}"
        modifier = body.modifiers.new(f"Actual_ScrewHole_{i+1}", "BOOLEAN")
        modifier.operation = 'DIFFERENCE'
        modifier.solver = 'EXACT'
        modifier.object = cutter
        bpy.context.view_layer.objects.active = body
        body.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        body.select_set(False)
        bpy.data.objects.remove(cutter, do_unlink=True)
    return parent_group("03_TopPlate_WithPost", parts)


def make_screw():
    parts = [cyl("Screw_Shaft", (0,0,.09), .026, .18, STEEL, verts=16, bevel=.006),
             cyl("Screw_Head", (0,0,.205), .07, .05, STEEL, verts=20, bevel=.012),
             cube("Screwdriver_Slot", (0,0,.234), (.045,.009,.006), DARK, .002)]
    return parent_group("04_TopPlateScrew_Duplicate3x", parts)


def make_plate():
    parts = [cyl("Snap_Collar", (0,0,.018), .055, .036, DARK, verts=20, bevel=.006),
             torus("Snap_Collar_Ridge", (0,0,.004), .045, .006, GREEN),
             cyl("Plate_Underbase", (0,0,.045), .25, .035, DARK, verts=32, bevel=.012),
             cyl("Pressure_Plate", (0,0,.071), .235, .035, RED, verts=32, bevel=.015),
             cyl("Impact_Target", (0,0,.091), .105, .008, WHITE, verts=32, bevel=.004)]
    return parent_group("05_PressurePlate", parts)


def make_stamp():
    parts = [cube("Stamp_Pad", (0,0,.04), (.25,.15,.04), GREEN, .025),
             cyl("Stamp_Post", (0,0,.23), .07, .32, STEEL, verts=16),
             cube("Stamp_Handle", (0,0,.44), (.2,.07,.07), RED, .045)]
    return parent_group("06_InspectionStamp", parts)


def make_jig():
    parts = [cube("Jig_Base", (0,0,.05), (.72,.64,.05), STEEL, .045),
             cube("Jig_Stripe_A", (-.47,-.61,.115), (.12,.025,.015), YELLOW, .005),
             cube("Jig_Stripe_B", (0,-.61,.115), (.12,.025,.015), YELLOW, .005),
             cube("Jig_Stripe_C", (.47,-.61,.115), (.12,.025,.015), YELLOW, .005)]
    for x in (-.58,.58):
        for y in (-.5,.5):
            parts.append(cyl("Jig_Stop", (x,y,.17), .075, .24, DARK, verts=16))
    parts.append(text_obj("ASSEMBLY JIG", "Jig_Text", (0,-.66,.125), .11))
    return parent_group("07_AssemblyJig", parts)


def select_tree(root):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for o in bpy.data.objects:
        p = o.parent
        while p:
            if p == root:
                o.select_set(True)
                break
            p = p.parent
    bpy.context.view_layer.objects.active = root


def export_glb(root):
    select_tree(root)
    path = os.path.join(OUT, root.name + ".glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True,
                              export_apply=True, export_yup=True)


def export_fbx(root):
    select_tree(root)
    path = os.path.join(OUT, root.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True,
                             axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                             bake_space_transform=True, mesh_smooth_type='FACE')


clean()
roots = [make_shell(), make_spring(), make_top_plate(), make_screw(), make_plate(), make_stamp(), make_jig()]
for root in roots:
    export_glb(root)
    export_fbx(root)

# Lay out a readable complete reference assembly without changing individual exports.
layout = {
    "01_CartoonShell": (0,0,.18), "02_WindupSpring": (-1.25,0,0),
    "03_TopPlate_WithPost": (0,0,.55), "04_TopPlateScrew_Duplicate3x": (1.1,-.8,0),
    "05_PressurePlate": (0,0,.72), "06_InspectionStamp": (.7,-1.0,0),
    "07_AssemblyJig": (0,0,0)
}
for root in roots:
    root.location = layout[root.name]

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "ACME_PropFactory_Source.blend"))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, "ACME_PropFactory_Complete.glb"),
                          export_format='GLB', export_apply=True, export_yup=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "ACME_PropFactory_Complete.fbx"),
                         apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
                         add_leaf_bones=False, bake_space_transform=True,
                         mesh_smooth_type='FACE')

# Quick contact-sheet-style preview for visual QA.
bpy.ops.object.select_all(action='DESELECT')
floor = cube("Preview_Floor", (0, 0, -.08), (2.4, 2.0, .04), mat("Floor", (.055,.065,.075), 0, .75), .03)
bpy.ops.object.camera_add(location=(3.8, -5.2, 3.6))
camera = bpy.context.object
bpy.context.scene.camera = camera
direction = Vector((0, 0, .3)) - camera.location
camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
camera.data.lens = 52
for location, energy, size in (((-3,-4,6), 1150, 4.0), ((4,1,4), 850, 3.0)):
    bpy.ops.object.light_add(type='AREA', location=location)
    light = bpy.context.object
    light.data.energy = energy
    light.data.shape = 'DISK'
    light.data.size = size
bpy.context.scene.render.engine = 'BLENDER_EEVEE'
bpy.context.scene.render.resolution_x = 900
bpy.context.scene.render.resolution_y = 650
bpy.context.scene.render.resolution_percentage = 100
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.context.scene.render.filepath = os.path.join(OUT, "ACME_PropFactory_Preview.png")
bpy.context.scene.world.color = (.025, .025, .035)
bpy.ops.render.render(write_still=True)
print(f"Built {len(roots)} props in {OUT}")
