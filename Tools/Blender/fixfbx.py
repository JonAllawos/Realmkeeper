# Phase 18 fix-up of converted creature FBX files, in place: removes the stray helper meshes some sources carry ("Icosphere"),
# applies object transforms on static meshes (no tilted root in Unity), sets the feet on the ground (lowest point at 0),
# and turns the model half round so it faces Unity's +Z (the conversions had faced -Z; changing the export axis does not
# help, because Unity reads the axis system from the file and undoes it). Usage: blender -b -P fixfbx.py -- in.fbx out.fbx
import bpy, sys, mathutils
src, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
removed = [o.name for o in list(bpy.context.scene.objects) if o.type == "MESH" and o.name.startswith("Icosphere")]
for n in removed:
    bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
has_armature = any(o.type == "ARMATURE" for o in bpy.context.scene.objects)
turn = mathutils.Matrix.Rotation(3.141592653589793, 4, "Z")
# A root that animates its own transform (the sheep) would lose the turn to its keys: it is wrapped in a still parent first.
for o in [o for o in bpy.context.scene.objects if o.parent is None and o.animation_data and o.animation_data.action]:
    holder = bpy.data.objects.new("RootNode", None)
    bpy.context.scene.collection.objects.link(holder)
    o.parent = holder
bpy.context.view_layer.update()
for o in bpy.context.scene.objects:
    if o.parent is None:
        o.matrix_world = turn @ o.matrix_world
bpy.context.view_layer.update()
if not has_armature:
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.context.view_layer.update()
zs = [(o.matrix_world @ mathutils.Vector(c)).z for o in bpy.context.scene.objects if o.type == "MESH" for c in o.bound_box]
lift = -min(zs)
for o in bpy.context.scene.objects:
    if o.parent is None:
        o.location.z += lift
bpy.context.view_layer.update()
if not has_armature:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
anim = len(bpy.data.actions) > 0
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    object_types={"ARMATURE", "MESH", "EMPTY"}, add_leaf_bones=False, bake_anim=anim, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_simplify_factor=1.0, path_mode="COPY", embed_textures=True, mesh_smooth_type="FACE")
print("FIXED", src, "removed", removed, "lift", round(lift, 3), "armature", has_armature, "clips", [a.name.split("|")[-1] for a in bpy.data.actions])
