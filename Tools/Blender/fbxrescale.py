# Rescale an FBX that ships at the wrong size to a stated height (metres), keeping only the named clips, exported the same
# way as glb2fbx.py (FBX_SCALE_ALL, -Z forward, Y up). Usage: blender -b -P fbxrescale.py -- in.fbx out.fbx height clip1,clip2
import bpy, sys, mathutils
args = sys.argv[sys.argv.index("--") + 1:]
src, out, height, keep = args[0], args[1], float(args[2]), set(args[3].split(",")) if len(args) > 3 else set()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
zs = [(o.matrix_world @ mathutils.Vector(c)).z for o in bpy.context.scene.objects if o.type == "MESH" for c in o.bound_box]
size = max(zs) - min(zs)
for a in list(bpy.data.actions):
    if a.name.split("|")[-1] not in keep:
        bpy.data.actions.remove(a)
kept = [a.name.split("|")[-1] for a in bpy.data.actions]
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_scale_options="FBX_SCALE_ALL", global_scale=height / size,
    axis_forward="-Z", axis_up="Y", object_types={"ARMATURE", "MESH", "EMPTY"}, add_leaf_bones=False, bake_anim=bool(kept),
    bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=1.0, path_mode="COPY",
    embed_textures=True, mesh_smooth_type="FACE")
print("RESCALED", src, round(size, 3), "->", height, "clips", kept)
