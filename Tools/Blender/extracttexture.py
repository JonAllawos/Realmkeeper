# Save the first base-colour image an FBX uses as <out>.png (for models whose embedded texture Unity does not bind).
# Usage: blender -b -P extracttexture.py -- in.fbx out.png
import bpy, sys
src, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
img = next(n.image for m in bpy.data.materials if m.use_nodes for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image and n.image.has_data)
img.filepath_raw = out
img.file_format = "PNG"
img.save()
print("EXTRACTED", src, img.name, img.size[:], "->", out)
