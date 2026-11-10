using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Realmkeeper.Exploration.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Realmkeeper.Editor
{
    /// <summary>
    /// One-step, repeatable art setup (menu Realmkeeper ▸ Set Up Art, or batch mode
    /// <c>-executeMethod Realmkeeper.Editor.ArtSetup.RunBatch</c>). From the third-party packs it:
    /// creates the shared URP Lit materials; remaps every model's source materials onto them;
    /// marks looping animation clips; builds the locomotion animator controller; cuts head-only
    /// meshes out of the base characters (the outfits supply the body); measures every kit module;
    /// and writes the <see cref="ArtCatalog"/> into Resources. Running it again gives the same result.
    /// </summary>
    public static class ArtSetup
    {
        private const string ArtRoot = "Assets/Realmkeeper/Art";
        private const string ThirdParty = ArtRoot + "/ThirdParty";
        private const string MegaKit = ThirdParty + "/Quaternius/MedievalVillageMegaKit";
        private const string BaseChars = ThirdParty + "/Quaternius/UniversalBaseCharacters";
        private const string Outfits = ThirdParty + "/Quaternius/ModularCharacterOutfitsFantasy";
        private const string AnimLibrary = ThirdParty + "/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";
        private const string AmbientCg = ThirdParty + "/AmbientCG";
        private const string MaterialDir = ArtRoot + "/Materials";
        private const string CharacterDir = ArtRoot + "/Characters";
        private const string CatalogPath = ArtRoot + "/Resources/" + ArtCatalog.ResourceName + ".asset";
        private const string ControllerPath = CharacterDir + "/Locomotion.controller";

        /// <summary>Source material name in the packs → shared material ID.</summary>
        private static readonly Dictionary<string, string> SourceMaterials = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MI_Plaster"] = "Plaster", ["MI_WoodTrim"] = "WoodTrim", ["MI_WoodTrim_Wear"] = "WoodTrimWear",
            ["MI_RockTrim"] = "RockTrim", ["MI_Brick"] = "Brick", ["MI_UnevenBrick"] = "UnevenBrick",
            ["MI_RoundTiles"] = "RoundTiles", ["MI_RedBrick"] = "RedBrick", ["MI_WindowGlass"] = "WindowGlass",
            ["MI_Vine"] = "Vine", ["MI_MetalOrnaments"] = "Metal",
            ["MI_Peasant"] = "Peasant", ["MI_Ranger"] = "Ranger",
            ["MI_Regular_Male"] = "BodyMale", ["MI_Regular_Female"] = "BodyFemale",
            ["MI_Superhero_Male"] = "HeadMale", ["MI_Superhero_Female"] = "HeadFemale",
            ["MI_Eyes"] = "Eyes", ["MI_Hair_1"] = "Hair1", ["MI_Hair_2"] = "Hair2",
        };

        /// <summary>Clips the locomotion controller uses (names as in the library).</summary>
        public static readonly string[] LocomotionClips = { "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Idle_Talking_Loop" };

        [MenuItem("Realmkeeper/Set Up Art")]
        public static void RunFromMenu() => Debug.Log(Run(null));

        /// <summary>Batch entry point; writes a report to <c>RK_SETUP_OUT</c> if set and exits non-zero on failure.</summary>
        public static void RunBatch()
        {
            try
            {
                var outDir = Environment.GetEnvironmentVariable("RK_SETUP_OUT");
                var report = Run(outDir);
                if (!string.IsNullOrEmpty(outDir)) File.WriteAllText(Path.Combine(outDir, "art-setup-report.txt"), report);
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static string Run(string thumbnailDir)
        {
            var report = new StringBuilder();
            AssetDatabase.Refresh();
            EnsureFolder(MaterialDir);
            EnsureFolder(CharacterDir);
            EnsureFolder(ArtRoot + "/Resources");

            var materials = CreateMaterials(report);
            RemapModels(materials, report);
            ConfigureAnimationClips(report);
            FixAvatars(report);
            var controller = BuildController(report);
            var heads = BuildHeads(materials, report);

            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Materials = materials.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new ArtCatalog.MaterialEntry { Id = p.Key, Material = p.Value }).ToList();
            catalog.Modules = MeasureModules(report);
            catalog.Bodies = Directory.GetFiles(Outfits + "/Models", "*.fbx").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => new ArtCatalog.PrefabEntry { Id = Path.GetFileNameWithoutExtension(f), Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Norm(f)) })
                .ToList();
            var parts = new List<ArtCatalog.PrefabEntry>();
            parts.AddRange(heads.Select(h => new ArtCatalog.PrefabEntry { Id = h.Key, Prefab = h.Value }));
            parts.AddRange(Directory.GetFiles(BaseChars + "/Hair", "*.fbx").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => new ArtCatalog.PrefabEntry { Id = Path.GetFileNameWithoutExtension(f), Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Norm(f)) }));
            catalog.Parts = parts;
            catalog.Locomotion = controller;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            report.AppendLine($"Catalog: {catalog.Modules.Count} modules, {catalog.Materials.Count} materials, {catalog.Bodies.Count} bodies, {catalog.Parts.Count} parts, controller {(controller != null ? "yes" : "NO")}.");
            if (!string.IsNullOrEmpty(thumbnailDir)) RenderThumbnails(catalog, thumbnailDir, report);
            return report.ToString();
        }

        // ----- Materials ------------------------------------------------------------------------

        private static Dictionary<string, Material> CreateMaterials(StringBuilder report)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("URP Lit shader not found.");
            var mk = MegaKit + "/Textures/";
            var d = new Dictionary<string, Material>(StringComparer.Ordinal);

            Material Make(string id, string baseMap, string normal, float smoothness, Color tint, Vector2 tiling = default, float normalScale = 1f)
            {
                var path = $"{MaterialDir}/M_{id}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit);
                    AssetDatabase.CreateAsset(m, path);
                }

                m.shader = lit;
                m.SetTexture("_BaseMap", baseMap != null ? Tex(baseMap) : null);
                m.SetColor("_BaseColor", tint);
                var bump = normal != null ? Tex(normal) : null;
                m.SetTexture("_BumpMap", bump);
                m.SetFloat("_BumpScale", normalScale);
                if (bump != null) m.EnableKeyword("_NORMALMAP");
                else m.DisableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_EnvironmentReflections", 1f);
                var scale = tiling == default ? Vector2.one : tiling;
                m.SetTextureScale("_BaseMap", scale);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                d[id] = m;
                return m;
            }

            var white = Color.white;
            Make("Plaster", mk + "T_Plaster_BaseColor.png", mk + "T_Plaster_Normal.png", 0.08f, white);
            Make("WoodTrim", mk + "T_WoodTrim_BaseColor.png", mk + "T_WoodTrim_Normal.png", 0.15f, white);
            Make("WoodTrimWear", mk + "T_WoodTrim_BaseColor.png", mk + "T_WoodTrim_Normal.png", 0.12f, new Color(0.86f, 0.82f, 0.78f));
            Make("RockTrim", mk + "T_RockTrim_BaseColor.png", mk + "T_RockTrim_Normal.png", 0.1f, white);
            Make("Brick", mk + "T_Brick_BaseColor.png", mk + "T_Brick_Normal.png", 0.1f, white);
            Make("UnevenBrick", mk + "T_UnevenBrick_BaseColor.png", mk + "T_UnevenBrick_Normal.png", 0.1f, white);
            Make("RoundTiles", mk + "T_RoundTiles_BaseColor.png", mk + "T_RoundTiles_Normal.png", 0.22f, white);
            Make("RedBrick", mk + "T_RedBrick_BaseColor.png", mk + "T_Brick_Normal.png", 0.1f, white);
            Make("WindowGlass", mk + "T_WindowGradient.png", null, 0.85f, new Color(0.75f, 0.82f, 0.86f));
            Make("Metal", null, null, 0.45f, new Color(0.22f, 0.21f, 0.2f));
            var vine = Make("Vine", mk + "T_VineLeaf.png", null, 0.1f, white);
            vine.SetFloat("_AlphaClip", 1f);
            vine.SetFloat("_Cutoff", 0.4f);
            vine.EnableKeyword("_ALPHATEST_ON");
            vine.SetFloat("_Cull", (float)CullMode.Off);
            vine.renderQueue = (int)RenderQueue.AlphaTest;

            // Ground: the builder's meshes carry UVs in metres, so tiling = 1 / tile size.
            Make("Grass", $"{AmbientCg}/Grass004/Grass004_Color.jpg", $"{AmbientCg}/Grass004/Grass004_NormalGL.jpg", 0.05f, new Color(0.86f, 0.9f, 0.78f), new Vector2(1f / 4f, 1f / 4f), 0.6f);
            Make("Dirt", $"{AmbientCg}/Ground037/Ground037_Color.jpg", $"{AmbientCg}/Ground037/Ground037_NormalGL.jpg", 0.06f, white, new Vector2(1f / 3f, 1f / 3f), 0.8f);
            Make("Cobble", $"{AmbientCg}/PavingStones070/PavingStones070_Color.jpg", $"{AmbientCg}/PavingStones070/PavingStones070_NormalGL.jpg", 0.12f, new Color(0.93f, 0.9f, 0.86f), new Vector2(1f / 2.5f, 1f / 2.5f));
            Make("Gravel", $"{AmbientCg}/Ground054/Ground054_Color.jpg", $"{AmbientCg}/Ground054/Ground054_NormalGL.jpg", 0.08f, white, new Vector2(1f / 3f, 1f / 3f));
            // Stone walls and towers drawn as metre-UV boxes reuse the kit's rock and brick textures.
            Make("WallStone", mk + "T_UnevenBrick_BaseColor.png", mk + "T_UnevenBrick_Normal.png", 0.08f, new Color(0.9f, 0.88f, 0.84f), new Vector2(1f / 2f, 1f / 2f));
            Make("Planks", $"{AmbientCg}/Planks021/Planks021_Color.jpg", $"{AmbientCg}/Planks021/Planks021_NormalGL.jpg", 0.12f, new Color(0.62f, 0.5f, 0.38f), new Vector2(1f / 2f, 1f / 2f));
            Make("Wood", $"{AmbientCg}/Wood049/Wood049_Color.jpg", $"{AmbientCg}/Wood049/Wood049_NormalGL.jpg", 0.15f, white, new Vector2(1f / 1.5f, 1f / 1.5f));
            Make("Canvas", null, null, 0.05f, new Color(0.78f, 0.68f, 0.5f));
            Make("CanvasRed", null, null, 0.05f, new Color(0.62f, 0.24f, 0.18f));
            Make("CanvasBlue", null, null, 0.05f, new Color(0.25f, 0.36f, 0.52f));
            Make("Foliage", null, null, 0.05f, new Color(0.3f, 0.42f, 0.2f));
            Make("Flowers", null, null, 0.1f, new Color(0.86f, 0.42f, 0.32f));
            Make("Bark", $"{AmbientCg}/Wood049/Wood049_Color.jpg", $"{AmbientCg}/Wood049/Wood049_NormalGL.jpg", 0.05f, new Color(0.55f, 0.47f, 0.4f), new Vector2(1f, 1f / 2f));
            var ember = Make("Ember", null, null, 0.2f, new Color(0.9f, 0.45f, 0.18f));
            ember.SetColor("_EmissionColor", new Color(1.6f, 0.7f, 0.25f));
            ember.EnableKeyword("_EMISSION");
            ember.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var skyPath = MaterialDir + "/M_Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            var skyShader = Shader.Find("Skybox/Procedural");
            if (sky == null)
            {
                sky = new Material(skyShader);
                AssetDatabase.CreateAsset(sky, skyPath);
            }

            sky.shader = skyShader;
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.05f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.62f, 0.6f, 0.56f));
            sky.SetFloat("_Exposure", 1.0f);
            EditorUtility.SetDirty(sky);
            d["Sky"] = sky;

            var lamp = Make("LampGlow", null, null, 0.4f, new Color(1f, 0.85f, 0.6f));
            lamp.SetColor("_EmissionColor", new Color(2.2f, 1.5f, 0.8f));
            lamp.EnableKeyword("_EMISSION");

            // Characters.
            var bc = BaseChars + "/Textures/";
            var of = Outfits + "/Textures/";
            Make("HeadMale", bc + "T_Head_Male_BaseColor.png", null, 0.3f, white);
            Make("HeadFemale", bc + "T_Head_Female_BaseColor.png", null, 0.3f, white);
            Make("BodyMale", of + "T_Body_Male_BaseColor.png", null, 0.3f, white);
            Make("BodyFemale", of + "T_Body_Female_BaseColor.png", null, 0.3f, white);
            Make("Eyes", bc + "T_Eye_Brown.png", null, 0.8f, white);
            Make("Hair1", bc + "T_Hair_1_BaseColor.png", null, 0.25f, white);
            Make("Hair2", bc + "T_Hair_2_BaseColor.png", null, 0.25f, white);
            Make("Peasant", of + "T_Peasant_BaseColor.png", of + "T_Peasant_Normal.png", 0.15f, white);
            Make("Peasant2", of + "T_Peasant_2_BaseColor.png", of + "T_Peasant_Normal.png", 0.15f, white);
            Make("Ranger", of + "T_Ranger_BaseColor.png", of + "T_Ranger_Normal.png", 0.2f, white);
            Make("Ranger3", of + "T_Ranger_3_BaseColor.png", of + "T_Ranger_Normal.png", 0.2f, white);
            AssetDatabase.SaveAssets();
            report.AppendLine($"Materials: {d.Count} (URP Lit) in {MaterialDir}.");
            return d;
        }

        private static Texture2D Tex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) throw new FileNotFoundException("Texture not found: " + path);
            return t;
        }

        // ----- Model material remaps -------------------------------------------------------------

        private static void RemapModels(Dictionary<string, Material> materials, StringBuilder report)
        {
            var remapped = 0;
            var unknown = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { ThirdParty }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || importer.materialImportMode == ModelImporterMaterialImportMode.None) continue;
                var changed = false;
                foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    if (!SourceMaterials.TryGetValue(embedded.name, out var id) || !materials.TryGetValue(id, out var target))
                    {
                        unknown.Add(embedded.name);
                        continue;
                    }

                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), target);
                    changed = true;
                }

                if (!changed) continue;
                importer.SaveAndReimport();
                remapped++;
            }

            report.AppendLine($"Remapped materials on {remapped} models." + (unknown.Count > 0 ? " Unmapped source materials: " + string.Join(", ", unknown) : ""));
        }

        // ----- Animation ---------------------------------------------------------------------------

        private static void ConfigureAnimationClips(StringBuilder report)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(AnimLibrary);
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                var name = ClipName(c.name);
                c.name = name;
                c.loopTime = name.EndsWith("_Loop", StringComparison.Ordinal);
                c.loopPose = false;
                c.lockRootRotation = true; // the in-place library: rotation, height and position come from code
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            report.AppendLine($"Animation library: {clips.Length} clips, {clips.Count(c => c.loopTime)} looping.");
        }

        private static string ClipName(string take) => take.Contains("|") ? take.Substring(take.LastIndexOf('|') + 1) : take;

        private static AnimationClip FindClip(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(AnimLibrary).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        /// <summary>
        /// Locomotion: a 1D blend on Speed (m/s): idle 0, walk 1.6, jog 3.6, sprint 6; and a talk state
        /// entered while Talking is set. Turning in place is shown by the slow end of the walk blend
        /// (the CC0 library has no dedicated turn clip).
        /// </summary>
        private static AnimatorController BuildController(StringBuilder report)
        {
            AssetDatabase.DeleteAsset(ControllerPath); // rebuilt from scratch each run, so it never drifts
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Require("Idle_Loop"), 0f);
            tree.AddChild(Require("Walk_Loop"), 1.6f);
            tree.AddChild(Require("Jog_Fwd_Loop"), 3.6f);
            tree.AddChild(Require("Sprint_Loop"), 6f);
            sm.defaultState = locomotion;

            var talk = sm.AddState("Talk");
            talk.motion = Require("Idle_Talking_Loop");
            var toTalk = locomotion.AddTransition(talk);
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            toTalk.hasExitTime = false;
            toTalk.duration = 0.25f;
            var back = talk.AddTransition(locomotion);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
            back.hasExitTime = false;
            back.duration = 0.25f;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            report.AppendLine("Animator controller: Locomotion (idle/walk/jog/sprint blend on Speed) + Talk.");
            return controller;
        }

        private static AnimationClip Require(string name) =>
            FindClip(name) ?? throw new InvalidOperationException($"Animation clip {name} not found in {AnimLibrary}.");

        /// <summary>
        /// Reports each body's humanoid avatar. Unity's auto-mapping fails on Female_Peasant ("Required human
        /// bone 'Head' not found" although the skeleton is the shared one); the character builder then uses the
        /// avatar of another body of the same frame (identical skeleton). Copying the avatar at import time
        /// was tried and drops the Animator from the model, so it is not done.
        /// </summary>
        private static void FixAvatars(StringBuilder report)
        {
            foreach (var path in Directory.GetFiles(Outfits + "/Models", "*.fbx").Select(Norm).OrderBy(p => p, StringComparer.Ordinal))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.sourceAvatar = null;
                    importer.SaveAndReimport();
                }

                var animator = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Animator>();
                var avatar = animator != null ? animator.avatar : null;
                report.AppendLine($"Avatar {Path.GetFileNameWithoutExtension(path)}: animator {animator != null}, valid {avatar != null && avatar.isValid}, human {avatar != null && avatar.isHuman}.");
            }
        }

        // ----- Heads -------------------------------------------------------------------------------

        /// <summary>
        /// The outfits are bodies without heads (the pack says to use only the base character's head).
        /// Cuts the head and neck out of each base character's full-body mesh by bone weight, and saves
        /// a prefab with that mesh, the eyes and the brows, all skinned to the shared skeleton.
        /// </summary>
        private static Dictionary<string, GameObject> BuildHeads(Dictionary<string, Material> materials, StringBuilder report)
        {
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var sex in new[] { "Male", "Female" })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{BaseChars}/Models/Superhero_{sex}_FullBody.fbx");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "Head_" + sex;
                var animator = instance.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);

                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                var sourceName = body.sharedMesh.name;
                var head = ExtractHead(body);
                var meshPath = $"{CharacterDir}/Head_{sex}_Mesh.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(head, meshPath);
                body.sharedMesh = head;
                body.sharedMaterial = materials["Head" + sex];
                body.name = "Head";
                foreach (var s in skins)
                {
                    s.updateWhenOffscreen = false;
                    s.skinnedMotionVectors = false;
                }

                var prefabPath = $"{CharacterDir}/Head_{sex}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);
                result["Head_" + sex] = prefab;
                report.AppendLine($"Head_{sex}: {head.vertexCount} vertices, {head.triangles.Length / 3} triangles (from {sourceName}).");
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static Mesh ExtractHead(SkinnedMeshRenderer body)
        {
            var src = body.sharedMesh;
            var bones = body.bones;
            var headBones = new HashSet<int>();
            for (var i = 0; i < bones.Length; i++)
            {
                var n = bones[i] != null ? bones[i].name.ToLowerInvariant() : "";
                if (n.Contains("head") || n.Contains("neck") || n.Contains("eye") || n.Contains("jaw")) headBones.Add(i);
            }

            var weights = src.boneWeights;
            float HeadWeight(int v)
            {
                var w = weights[v];
                var sum = 0f;
                if (headBones.Contains(w.boneIndex0)) sum += w.weight0;
                if (headBones.Contains(w.boneIndex1)) sum += w.weight1;
                if (headBones.Contains(w.boneIndex2)) sum += w.weight2;
                if (headBones.Contains(w.boneIndex3)) sum += w.weight3;
                return sum;
            }

            var keep = new List<int>();
            var map = new Dictionary<int, int>();
            var srcTris = new List<int>();
            for (var sub = 0; sub < src.subMeshCount; sub++) srcTris.AddRange(src.GetTriangles(sub));
            for (var t = 0; t < srcTris.Count; t += 3)
            {
                int a = srcTris[t], b = srcTris[t + 1], c = srcTris[t + 2];
                if (HeadWeight(a) + HeadWeight(b) + HeadWeight(c) < 1.5f) continue; // mostly head/neck
                foreach (var v in new[] { a, b, c })
                {
                    if (!map.ContainsKey(v)) map[v] = map.Count;
                    keep.Add(map[v]);
                }
            }

            var order = map.OrderBy(p => p.Value).Select(p => p.Key).ToArray();
            T[] Pick<T>(T[] arr) => arr != null && arr.Length == src.vertexCount ? order.Select(i => arr[i]).ToArray() : null;
            var mesh = new Mesh { name = "Head" };
            mesh.vertices = Pick(src.vertices);
            mesh.normals = Pick(src.normals);
            var tangents = Pick(src.tangents);
            if (tangents != null) mesh.tangents = tangents;
            mesh.uv = Pick(src.uv);
            mesh.boneWeights = Pick(weights);
            mesh.bindposes = src.bindposes;
            mesh.SetTriangles(keep, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ----- Modules -----------------------------------------------------------------------------

        private static List<ArtCatalog.ModuleEntry> MeasureModules(StringBuilder report)
        {
            var list = new List<ArtCatalog.ModuleEntry>();
            report.AppendLine("Module bounds (Unity axes, metres):");
            foreach (var path in Directory.GetFiles(MegaKit + "/Models", "*.fbx").Select(Norm).OrderBy(p => p, StringComparer.Ordinal))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var bounds = LocalBounds(prefab);
                var id = Path.GetFileNameWithoutExtension(path);
                list.Add(new ArtCatalog.ModuleEntry { Id = id, Prefab = prefab, Bounds = bounds });
                var mats = string.Join(" ", prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct());
                var renderers = prefab.GetComponentsInChildren<Renderer>(true).Length;
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-34} min ({1,6:F2},{2,6:F2},{3,6:F2}) max ({4,6:F2},{5,6:F2},{6,6:F2}) renderers {7} [{8}]",
                    id, bounds.min.x, bounds.min.y, bounds.min.z, bounds.max.x, bounds.max.y, bounds.max.z, renderers, mats));
            }

            return list;
        }

        private static Bounds LocalBounds(GameObject prefab)
        {
            var instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero; // keep the model root's own rotation and scale (Blender export: 90° X, ×100)
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(instance);
            return b;
        }

        // ----- Verification thumbnails -----------------------------------------------------------------

        private static void RenderThumbnails(ArtCatalog catalog, string dir, StringBuilder report)
        {
            Directory.CreateDirectory(dir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.7f, 0.78f);
            cam.fieldOfView = 40f;

            void Shot(string name, GameObject subject, Vector3 from, Vector3 at)
            {
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                var rt = new RenderTexture(640, 480, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(640, 480, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
            }

            foreach (var id in new[] { "Wall_Plaster_Window_Wide_Flat", "Roof_RoundTiles_6x8", "Overhang_Plaster_Long" })
            {
                var m = catalog.Module(id);
                if (m == null) continue;
                var go = Object.Instantiate(m.Prefab);
                Shot(id + "_front-neg-z", go, new Vector3(1.5f, 2f, -6f), m.Bounds.center);
                Shot(id + "_back-pos-z", go, new Vector3(1.5f, 2f, 6f), m.Bounds.center);
                Object.DestroyImmediate(go);
            }

            foreach (var body in catalog.Bodies.Take(1))
            {
                var go = Object.Instantiate(body.Prefab);
                var headPrefab = catalog.Part(body.Id.StartsWith("Female", StringComparison.Ordinal) ? "Head_Female" : "Head_Male");
                var head = Object.Instantiate(headPrefab);
                Shot("Character_" + body.Id + "_front", go, new Vector3(0.5f, 1.4f, 3.5f), new Vector3(0f, 1f, 0f));
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(head);
            }

            report.AppendLine("Thumbnails written to " + dir);
        }

        // ----- Helpers -----------------------------------------------------------------------------------

        private static string Norm(string path) => path.Replace('\\', '/');

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
