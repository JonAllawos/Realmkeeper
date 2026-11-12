# Blender headless: convert each .glb in IN_DIR to .fbx in OUT_DIR and report its contents.
# Usage: blender -b --factory-startup -P glb2fbx.py -- IN_DIR OUT_DIR
import bpy, os, sys, json

args = sys.argv[sys.argv.index("--") + 1:]
in_dir, out_dir = args[0], args[1]
os.makedirs(out_dir, exist_ok=True)
report = {}
TARGET = {"Spider":1.1,"Rat":0.45,"Deer":1.5,"Stag":1.8,"Fox":0.55,"Goblin":1.0,"Giant":3.0,"Orc":1.9,"Bull":1.6,"Chicken":0.45,"BlackBear":1.2,"Boar":0.9,"Goat":0.9,"Well":2.4,"MarketStalls":2.6,"MarketStand":1.6,"PostLantern":2.6,"StreetLight":3.2,"Wolf":0.85,"Husky":0.75,"Horse":2.1,"Cow":1.5}
KEEP = {"Idle", "Walk", "Run", "Gallop", "Attack", "Attack_Headbutt", "Attack_Kick", "Punch", "Weapon",
        "Idle_HitReact_Left", "HitRecieve", "HitReact", "Death",
        "Rat_Idle", "Rat_Walk", "Rat_Run", "Rat_Attack", "Rat_Death",
        "Spider_Idle", "Spider_Walk", "Spider_Attack", "Spider_Death"}

for name in sorted(os.listdir(in_dir)):
    if not name.lower().endswith(".glb"):
        continue
    base = os.path.splitext(name)[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    bpy.ops.import_scene.gltf(filepath=os.path.join(in_dir, name))
    # Some sources carry a stray helper sphere; it is not part of the model.
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name.startswith("Icosphere")]:
        bpy.data.objects.remove(o, do_unlink=True)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    tris = 0
    for o in meshes:
        o.data.calc_loop_triangles()
        tris += len(o.data.loop_triangles)
    xs, ys, zs = [], [], []
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ __import__("mathutils").Vector(c)
            xs.append(w.x); ys.append(w.y); zs.append(w.z)
    size = [round(max(v) - min(v), 3) for v in (xs, ys, zs)] if xs else [0, 0, 0]
    images = [i.name for i in bpy.data.images if i.users > 0]
    # Keep only the clips the game drives: idle, walk, run, attack, a hit reaction and death.
    source_actions = sorted({a.name.split("|")[-1] for a in bpy.data.actions})
    for a in list(bpy.data.actions):
        if a.name.split("|")[-1] not in KEEP:
            bpy.data.actions.remove(a)
    for o in bpy.context.scene.objects:
        if o.animation_data:
            if o.animation_data.action is None or o.animation_data.action.name.split("|")[-1] not in KEEP:
                o.animation_data.action = None
            for t in list(o.animation_data.nla_tracks):
                o.animation_data.nla_tracks.remove(t)
    actions = sorted({a.name.split("|")[-1] for a in bpy.data.actions})

    scale = (TARGET[base] / size[2]) if size[2] > 0 and base in TARGET else 1.0
    out = os.path.join(out_dir, base + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=False, apply_scale_options="FBX_SCALE_ALL", global_scale=scale, axis_forward="-Z", axis_up="Y",
        object_types={"ARMATURE", "MESH", "EMPTY"}, add_leaf_bones=False, bake_anim=bool(actions),
        bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=1.0,
        path_mode="COPY", embed_textures=True, mesh_smooth_type="FACE")
    report[base] = {"tris": tris, "meshes": len(meshes), "armatures": len(arms), "size_xyz_m": size,
                    "images": images, "materials": len(bpy.data.materials), "animations": actions, "dropped": sorted(set(source_actions) - set(actions)),
                    "fbx_bytes": os.path.getsize(out), "scale": round(scale, 5), "height_m": round(size[2] * scale, 3)}

with open(os.path.join(out_dir, "report.json"), "w") as f:
    json.dump(report, f, indent=1)
print("CONVERTED", len(report))
