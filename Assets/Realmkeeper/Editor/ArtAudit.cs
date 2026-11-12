using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Realmkeeper.Editor
{
    /// <summary>
    /// Phase 15: lists what the installed third-party art really contains — every animation clip (name, length, loop),
    /// each model's avatar (valid, humanoid), the hand bones of rigged characters, and whether the local-only Bestiary is
    /// present. Run in batch mode with <c>-executeMethod Realmkeeper.Editor.ArtAudit.Run</c>; writes to RK_AUDIT_OUT
    /// (or Temp/art_audit.txt). Read-only: it changes no asset.
    /// </summary>
    public static class ArtAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var root in new[] { "Assets/Realmkeeper/Art/ThirdParty/KayKit", "Assets/Realmkeeper/Art/ThirdParty/Quaternius/UniversalAnimationLibrary", "Assets/Realmkeeper/Art/ThirdParty/QuaterniusBestiary", "Assets/Realmkeeper/Art/ThirdParty/Quaternius/UniversalBaseCharacters" })
            {
                sb.AppendLine("### " + root + (AssetDatabase.IsValidFolder(root) ? string.Empty : " (absent)"));
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    var avatar = assets.OfType<Avatar>().FirstOrDefault();
                    var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
                    sb.AppendLine($"{path} | rig {importer?.animationType} | avatar {(avatar == null ? "none" : (avatar.isValid ? "valid" : "INVALID") + (avatar.isHuman ? " human" : " generic"))} | clips {clips.Count}");
                    foreach (var c in clips) sb.AppendLine($"    clip {c.name} {c.length:0.00}s loop={c.isLooping}");
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null && clips.Count == 0)
                    {
                        var bones = go.GetComponentsInChildren<Transform>(true).Select(t => t.name).Where(n => n.ToLowerInvariant().Contains("hand") || n.ToLowerInvariant().Contains("slot")).ToList();
                        if (bones.Count > 0) sb.AppendLine("    hand bones: " + string.Join(", ", bones));
                        var r = go.GetComponentsInChildren<Renderer>(true);
                        sb.AppendLine($"    renderers {r.Length}; bounds height {(r.Length > 0 ? r.Select(x => x.bounds.size.y).Max() : 0):0.00} m");
                    }
                }
            }

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Realmkeeper/Art/Characters/Locomotion.controller");
            if (controller != null)
            {
                sb.AppendLine("### Locomotion.controller");
                foreach (var p in controller.parameters) sb.AppendLine($"    parameter {p.name} {p.type}");
                foreach (var l in controller.layers)
                    foreach (var s in l.stateMachine.states) sb.AppendLine($"    state {l.name}/{s.state.name} motion {(s.state.motion != null ? s.state.motion.name : "-")}");
            }

            var outPath = System.Environment.GetEnvironmentVariable("RK_AUDIT_OUT");
            if (string.IsNullOrEmpty(outPath)) outPath = "Temp/art_audit.txt";
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("Art audit written to " + outPath);
        }
    }
}
