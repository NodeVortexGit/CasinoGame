"""
Generates the casino staff as realistic, rigged 3D people with MakeHuman (MPFB2 add-on, CC0 assets) and exports
each one as an FBX for Unity (Humanoid-ready "game_engine" rig, low-poly body proxy, small textures).

Run with the portable Blender in Tools/.external (see README, "Regenerating the staff"):
  blender -b --python Tools/make_staff.py -- <out_dir> [name ...] [--preview]
"""
import bpy, bmesh, os, sys, json, math
from mathutils import Vector

from bl_ext.user_default.mpfb.services.humanservice import HumanService
from bl_ext.user_default.mpfb.services.exportservice import ExportService
from bl_ext.user_default.mpfb.services.objectservice import ObjectService
from bl_ext.user_default.mpfb.services.assetservice import AssetService
from bl_ext.user_default.mpfb.services.locationservice import LocationService
from bl_ext.user_default.mpfb.services.rigservice import RigService

TEXTURE_SIZE = 512

FEMALE = dict(gender=0.0, age=0.5, muscle=0.5, weight=0.42, proportions=0.85, height=0.55, cupsize=0.62, firmness=0.7)
MALE = dict(gender=1.0, age=0.52, muscle=0.62, weight=0.48, proportions=0.85, height=0.62, cupsize=0.5, firmness=0.5)
RACES = {
    "caucasian": dict(asian=0.0, caucasian=1.0, african=0.0),
    "african": dict(asian=0.0, caucasian=0.0, african=1.0),
    "asian": dict(asian=1.0, caucasian=0.0, african=0.0),
    "latin": dict(asian=0.15, caucasian=0.6, african=0.25),
}

# name: (female, race, skin, hair, eyebrows, eye colour, outfit, shoes)
STAFF = {
    "f_host":      (True, "caucasian", "young_caucasian_female", "long01", "eyebrow010", "bluegreen", None, "shoes06"),
    "f_cashier":   (True, "asian", "young_asian_female", "ponytail01", "eyebrow010", "brownlight", None, "shoes06"),
    "f_server":    (True, "latin", "young_caucasian_female2", "long01", "eyebrow010", "brown", None, "shoes06"),
    "f_dealer1":   (True, "caucasian", "young_caucasian_female", "bob02", "eyebrow010", "blue", None, "shoes06"),
    "f_dealer2":   (True, "african", "young_african_female", "long01", "eyebrow010", "brown", None, "shoes06"),
    "f_dealer3":   (True, "asian", "young_asian_female", "bob01", "eyebrow010", "brown", None, "shoes06"),
    "m_host":      (False, "caucasian", "young_caucasian_male", "short02", "eyebrow001", "blue", "male_elegantsuit01", "shoes02"),
    "m_cashier":   (False, "african", "young_african_male", "short04", "eyebrow001", "brown", "male_elegantsuit01", "shoes02"),
    "m_server":    (False, "latin", "young_caucasian_male2", "short03", "eyebrow001", "brownlight", "male_elegantsuit01", "shoes02"),
    "m_dealer1":   (False, "caucasian", "young_caucasian_male", "short01", "eyebrow001", "bluegreen", "male_elegantsuit01", "shoes02"),
    "m_dealer2":   (False, "asian", "young_asian_male", "short02", "eyebrow001", "brown", "male_elegantsuit01", "shoes02"),
    "m_dealer3":   (False, "african", "young_african_male", "short04", "eyebrow001", "brown", "male_elegantsuit01", "shoes02"),
}


def asset(subdir, name):
    return "%s/%s.mhclo" % (name, name)


def build(name, spec):
    female, race, skin, hair, brows, eyes, outfit, shoes = spec
    phenotype = dict(FEMALE if female else MALE)
    phenotype["race"] = RACES[race]
    info = {
        "name": name,
        "phenotype": phenotype,
        "rig": "mixamo_unity",
        "eyes": "low-poly/low-poly.mhclo",
        "eyebrows": asset("eyebrows", brows),
        "eyelashes": "eyelashes02/eyelashes02.mhclo" if female else "eyelashes01/eyelashes01.mhclo",
        "teeth": "",
        "tongue": "",
        "hair": asset("hair", hair),
        "proxy": "female1605/female1605.proxy" if female else "male1591/male1591.proxy",
        "targets": [],
        "clothes": ([asset("clothes", outfit)] if outfit else []) + [asset("clothes", shoes)],
        "skin_mhmat": "%s/%s.mhmat" % (skin, skin),
        "skin_material_type": "GAMEENGINE",
        "eyes_material_type": "MAKESKIN",
        "eyes_material_settings": {},
        "skin_material_settings": {},
        "clothes_material_type": "MAKESKIN",
        "expressions": [],
        "alternative_materials": {},
    }
    settings = HumanService.get_default_deserialization_settings()
    settings["subdiv_levels"] = 0
    settings["override_rig"] = "mixamo_unity"
    settings["material_instances"] = "NEVER"
    basemesh = HumanService.deserialize_from_dict(info, settings)
    eyes_png = os.path.join(LocationService.get_user_data("eyes"), "materials", "%s_eye.png" % eyes)
    return basemesh, eyes_png


def convex_hull(points):
    pts = sorted(set(points))
    if len(pts) < 3:
        return pts
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def ray_hull(hull, cx, cy, ang):
    """Distance from (cx, cy) to the hull boundary along angle ang."""
    dx, dy = math.cos(ang), math.sin(ang)
    best = 0.0
    n = len(hull)
    for i in range(n):
        ax, ay = hull[i]
        bx, by = hull[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        den = dx * ey - dy * ex
        if abs(den) < 1e-9:
            continue
        t = ((ax - cx) * ey - (ay - cy) * ex) / den
        u = ((ax - cx) * dy - (ay - cy) * dx) / den
        if t > 0 and -1e-6 <= u <= 1 + 1e-6:
            best = max(best, t)
    return best


def dense_points(basemesh):
    """World-space points of the full-resolution body (shape keys applied) with their arm weight, for measuring."""
    # Evaluate with modifiers off so vertex indices still match the vertex groups (the mask modifiers renumber them).
    states = [(m, m.show_viewport) for m in basemesh.modifiers]
    for m, _ in states:
        m.show_viewport = False
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    ev = basemesh.evaluated_get(deps)
    me = ev.to_mesh()
    mw = basemesh.matrix_world
    arm_groups = {g.index for g in basemesh.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Shoulder"))}
    body_group = basemesh.vertex_groups.get("body")
    out = []
    for v in me.vertices:
        groups = {g.group: g.weight for g in basemesh.data.vertices[v.index].groups} if v.index < len(basemesh.data.vertices) else {}
        if body_group is not None and groups.get(body_group.index, 0.0) < 0.5:
            continue  # helper geometry (clothes helpers, joints)
        armw = sum(w for g, w in groups.items() if g in arm_groups)
        co = mw @ v.co
        out.append((co.x, co.y, co.z, armw))
    ev.to_mesh_clear()
    for m, shown in states:
        m.show_viewport = shown
    bpy.context.view_layer.update()
    print("MEASURE points", len(out), "of", len(me.vertices) if False else len(basemesh.data.vertices))
    return out


def make_dress(body, rig, height, measure=None):
    """
    A short, fitted, strapless cocktail dress shaped to this body: slices of the body (arms excluded) are wrapped in a
    smooth hull from mid-thigh to the bust line, with a curved neckline that dips at the front and a slight flare at the
    hem. UV v runs from the hem (0) to the neckline (1) so the game can print trims and sequins on it.
    """
    arm_groups = {g.index for g in body.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Shoulder"))}
    pts = [(x, y, z) for (x, y, z, a) in measure if a < 0.25]
    z_hem, z_top, z_crotch = 0.435 * height, 0.765 * height, 0.475 * height
    rings, segs = [], 40
    steps = 26
    for i in range(steps + 1):
        z = z_hem + (z_top - z_hem) * i / steps
        band = [(x, y) for (x, y, zz) in pts if abs(zz - z) < 0.02]
        hull = convex_hull(band)
        cx = sum(p[0] for p in hull) / max(1, len(hull))
        cy = sum(p[1] for p in hull) / max(1, len(hull))
        below = max(0.0, (z_crotch - z) / (z_crotch - z_hem))
        offset = 0.011 + 0.016 * below
        ring = []
        for k in range(segs):
            ang = 2 * math.pi * k / segs
            r = ray_hull(hull, cx, cy, ang) + offset
            ring.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r, z))
        rings.append(ring)
    # smooth the rings vertically a little
    for _ in range(2):
        for i in range(1, len(rings) - 1):
            for k in range(segs):
                a, b, c = rings[i - 1][k], rings[i][k], rings[i + 1][k]
                rings[i][k] = ((a[0] + 2 * b[0] + c[0]) / 4, (a[1] + 2 * b[1] + c[1]) / 4, b[2])
    # curved neckline: lower at the front (MakeHuman characters face -Y)
    top = rings[-1]
    for k in range(segs):
        ang = 2 * math.pi * k / segs
        front = max(0.0, -math.sin(ang))
        x, y, z = top[k]
        top[k] = (x, y, z - 0.022 * front * front)
    mesh = bpy.data.meshes.new("Dress")
    bm = bmesh.new()
    vrows = [[bm.verts.new(p) for p in ring] for ring in rings]
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for i in range(len(vrows) - 1):
        for k in range(segs):
            k1 = (k + 1) % segs
            f = bm.faces.new((vrows[i][k], vrows[i][k1], vrows[i + 1][k1], vrows[i + 1][k]))
            for loop, (uu, vv) in zip(f.loops, ((k / segs, i / steps), ((k + 1) / segs, i / steps), ((k + 1) / segs, (i + 1) / steps), (k / segs, (i + 1) / steps))):
                loop[uv_layer].uv = (uu, vv)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = True
    dress = bpy.data.objects.new(body.name.split(".")[0] + ".dress", mesh)
    bpy.context.scene.collection.objects.link(dress)
    dress.parent = rig
    dress.matrix_parent_inverse = rig.matrix_world.inverted()
    # skin weights copied from the body
    for g in body.vertex_groups:
        dress.vertex_groups.new(name=g.name)
    dt = dress.modifiers.new("weights", 'DATA_TRANSFER')
    dt.object = body
    dt.use_vert_data = True
    dt.data_types_verts = {'VGROUP_WEIGHTS'}
    dt.vert_mapping = 'POLYINTERP_NEAREST'
    bpy.context.view_layer.objects.active = dress
    bpy.ops.object.datalayout_transfer(modifier="weights")
    bpy.ops.object.modifier_apply(modifier="weights")
    arm = dress.modifiers.new("Armature", 'ARMATURE')
    arm.object = rig
    mat = bpy.data.materials.new("Dress")
    dress.data.materials.append(mat)
    hide_body_under(body, z_hem + 0.012, z_top - 0.035, arm_groups)
    return dress


def hide_body_under(body, z0, z1, arm_groups):
    """Delete the body's skin that the dress covers, so it can never poke through when animated."""
    mw = body.matrix_world
    bm = bmesh.new()
    bm.from_mesh(body.data)
    deform = bm.verts.layers.deform.active
    doomed = []
    for v in bm.verts:
        z = (mw @ v.co).z
        if not (z0 < z < z1):
            continue
        if deform is not None and sum(w for g, w in v[deform].items() if g in arm_groups) >= 0.25:
            continue
        doomed.append(v)
    faces = {f for v in doomed for f in v.link_faces if all(z0 < (mw @ fv.co).z < z1 for fv in f.verts)}
    bmesh.ops.delete(bm, geom=list(faces), context='FACES_ONLY')
    bm.to_mesh(body.data)
    bm.free()


def first_image(material):
    """The diffuse image a MakeHuman material uses (searching inside node groups too)."""
    if material is None or not material.use_nodes:
        return None
    stack = [material.node_tree]
    found = []
    while stack:
        tree = stack.pop()
        for node in tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image is not None:
                found.append(node.image)
            if node.type == 'GROUP' and node.node_tree is not None:
                stack.append(node.node_tree)
    for img in found:
        n = img.name.lower()
        if "normal" not in n and "_ao" not in n and "bump" not in n and "rough" not in n and "spec" not in n:
            return img
    return found[0] if found else None


def simplify_materials(objects, out_dir, char, eyes_png=None):
    """Replace MakeHuman's node setups with one plain texture per material (what FBX and mobile shaders understand)."""
    for obj in objects:
        if obj.type != 'MESH':
            continue
        for slot in obj.material_slots:
            mat = slot.material
            img = first_image(mat)
            iname = img.name.lower() if img is not None else ""
            if eyes_png and "_eye" in iname and os.path.exists(eyes_png):
                img = bpy.data.images.load(eyes_png)
            new = bpy.data.materials.new(char + "_" + (mat.name if mat else obj.name))
            new.use_nodes = True
            nt = new.node_tree
            bsdf = nt.nodes.get("Principled BSDF")
            if img is not None:
                size = TEXTURE_SIZE * 2 if obj.get("mpfb_object_type") == "Proxymeshes" or "skin" in img.name.lower() else TEXTURE_SIZE
                if max(img.size[0], img.size[1]) > size:
                    img.scale(size, size)
                path = os.path.join(out_dir, "%s_%s.png" % (char, bpy.path.clean_name(img.name.rsplit(".", 1)[0])))
                img.filepath_raw = path
                img.file_format = 'PNG'
                img.save()
                tex = nt.nodes.new("ShaderNodeTexImage")
                tex.image = img
                nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
                nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
            slot.material = new


def export(basemesh, out_dir, name, eyes_png=None, dress=False):
    rig = basemesh.parent
    # Bake clothes masks (hide the body under the dress) and drop helper geometry, then remove the full-res body.
    measure = dense_points(basemesh) if dress else None
    ExportService.bake_modifiers_remove_helpers(basemesh, bake_masks=True, bake_subdiv=False, remove_helpers=True, also_proxy=True)
    proxy = ObjectService.find_object_of_type_amongst_nearest_relatives(basemesh, "Proxymeshes")
    if proxy is not None:
        bpy.data.objects.remove(basemesh, do_unlink=True)
    body = proxy if proxy is not None else basemesh
    if dress:
        height = max((body.matrix_world @ v.co).z for v in body.data.vertices)
        make_dress(body, rig, height, measure)
    children = [o for o in bpy.data.objects if o.parent == rig]
    for o in children:
        # bake the remaining shape keys (body shape) into the mesh; Unity needs none of them
        if o.type == 'MESH' and o.data.shape_keys:
            bpy.context.view_layer.objects.active = o
            o.shape_key_add(name="baked", from_mix=True)
            for kb in list(o.data.shape_keys.key_blocks):
                if kb.name != "baked":
                    o.shape_key_remove(kb)
            o.shape_key_clear()
    simplify_materials(children, out_dir, name, eyes_png)
    # Lighter clothes for phones, then one mesh per character (one skinning pass, one renderer).
    meshes = [o for o in children if o.type == 'MESH']
    body = proxy if proxy is not None else meshes[0]
    for o in meshes:
        if o is not body and len(o.data.vertices) > 4000:
            dec = o.modifiers.new("decimate", 'DECIMATE')
            dec.ratio = 0.45
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_move_to_index(modifier="decimate", index=0)
            bpy.ops.object.modifier_apply(modifier="decimate")
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = name
    children = [body]
    # Unity's humanoid retargeting wants a T-pose rest pose; MakeHuman rests in an A-pose.
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    with open(os.path.join(LocationService.get_mpfb_data("poses"), "mixamo_unity_fk", "t-pose.json")) as f:
        RigService.set_pose_from_dict(rig, json.load(f))
    bpy.ops.object.mode_set(mode='OBJECT')
    RigService.apply_pose_as_rest_pose(rig)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                             bake_anim=False, apply_scale_options='FBX_SCALE_UNITS', axis_forward='Z', axis_up='Y',
                             path_mode='RELATIVE', mesh_smooth_type='FACE', use_armature_deform_only=True)
    verts = sum(len(o.data.vertices) for o in children if o.type == 'MESH')
    print("EXPORTED", name, path, "materials", len(body.material_slots), "verts", verts)
    return rig, children


def preview(rig, children, path):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.device = 'CPU'
    scene.render.resolution_x, scene.render.resolution_y = 600, 900
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.0, -2.3, 1.45)
    cam.rotation_euler = (math.radians(88), 0, 0)
    cam_data.lens = 50
    scene.camera = cam
    for i, (loc, energy) in enumerate((((1.5, -2.0, 2.5), 400), ((-2.0, -1.0, 2.0), 200), ((0.0, 2.0, 2.5), 300))):
        ld = bpy.data.lights.new("l%d" % i, 'AREA')
        ld.energy = energy
        ld.size = 2
        lo = bpy.data.objects.new("l%d" % i, ld)
        lo.location = loc
        lo.rotation_euler = (math.atan2(math.hypot(loc[0], loc[1]), loc[2] - 1.2), 0, math.atan2(loc[0], -loc[1]))
        scene.collection.objects.link(lo)
    world = bpy.data.worlds.new("w")
    world.color = (0.08, 0.05, 0.08)
    scene.world = world
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def clear_scene():
    """Empty the scene without resetting preferences (which would unload the MPFB add-on)."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights, bpy.data.node_groups):
        for block in list(coll):
            if block.users == 0:
                coll.remove(block)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir = os.path.abspath(argv[0])
    do_preview = "--preview" in argv
    names = [a for a in argv[1:] if not a.startswith("--")] or list(STAFF.keys())
    os.makedirs(out_dir, exist_ok=True)
    for name in names:
        clear_scene()
        basemesh, eyes_png = build(name, STAFF[name])
        rig, children = export(basemesh, out_dir, name, eyes_png, dress=STAFF[name][0])
        if do_preview:
            preview(rig, children, os.path.join(out_dir, name + "_preview.png"))


main()
