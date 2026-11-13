using UnityEditor;
using UnityEngine;

namespace Realmkeeper.Editor
{
    /// <summary>
    /// Import settings for the third-party art in <c>Assets/Realmkeeper/Art/ThirdParty</c>, so every
    /// machine imports it identically (the settings also live in the .meta files):
    /// axis conversion baked (the packs' documented Unity setup), no imported cameras or lights,
    /// humanoid rigs for bodies and the animation libraries (Quaternius, KayKit), no rig for kit modules
    /// and held props (KayKit accessories), normal maps
    /// flagged as such, and texture sizes capped to keep memory within budget.
    /// </summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/Realmkeeper/Art/ThirdParty/";

        public override int GetPostprocessOrder() => 10;

        /// <summary>Bumped when the settings change, so Unity reimports the art.</summary>
        public override uint GetVersion() => 8; // Phase 19: interface art (icons, frames, map symbols, parchment)

        /// <summary>Phase 19: the interface textures loaded from Resources (RkIcons, RkFrames, RkMap).</summary>
        public static bool IsInterfaceArt(string path) =>
            path.Contains("/Resources/RkIcons/") || path.Contains("/Resources/RkFrames/") || path.Contains("/Resources/RkMap/");

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.addCollider = false;
            importer.generateSecondaryUV = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importBlendShapes = false;

            if (assetPath.Contains("/MedievalVillageMegaKit/"))
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.isReadable = true; // static batching combines the kit pieces at runtime
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            }
            else if (assetPath.Contains("/UniversalAnimationLibrary/") || assetPath.Contains("/UniversalAnimationLibrary2/")) // the second library joined with the Phase 20A assets
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.optimizeGameObjects = false;
            }
            else if (assetPath.Contains("/KayKit/") && assetPath.Contains("/Accessories/"))
            {
                // KayKit weapons and shields: static props held in a hand (no rig).
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            }
            else if (assetPath.Contains("/KayKit/CharacterAnimations/"))
            {
                // KayKit animation sets: humanoid, so they drive KayKit characters and retarget to other humanoid bodies.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.optimizeGameObjects = false;
            }
            else if (IsAnimal(assetPath))
            {
                // Phase 18: the creatures (Quaternius animals and monsters): generic rigs with their own animations; URP
                // materials through the pipeline's material description path.
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.optimizeGameObjects = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            }
            else if (IsStaticKit(assetPath))
            {
                // Phase 18: static models (the dungeon kit, the props and nature kits, settlement props, the Poly by Google
                // animals): no rig; readable so static batching can combine them; URP materials.
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            }
            else if (assetPath.Contains("/Hair/"))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            }
            else
            {
                // Bodies: outfits and the base characters (whose heads the setup cuts out, so readable).
                importer.animationType = ModelImporterAnimationType.Human;
                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther) importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.isReadable = assetPath.Contains("/UniversalBaseCharacters/");
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            }
        }

        public static bool IsAnimal(string path) => path.Contains("/Quaternius/Creatures/") || path.Contains("/Quaternius/FarmAnimals/");

        public static bool IsStaticKit(string path) =>
            path.Contains("/KayKit/DungeonRemastered/") || path.Contains("/KayKit/PostLantern/") || path.Contains("/Quaternius/FantasyPropsMegaKit/") ||
            path.Contains("/Quaternius/StylizedNatureMegaKit/") || path.Contains("/Quaternius/SettlementProps/") || path.Contains("/PolyByGoogle/") ||
            path.Contains("/KayKit/FantasyWeaponsBits/") || path.Contains("/Kenney/FantasyTownKit/"); // Phase 20A assets

        /// <summary>Phase 18: the animals' standing, walking and running clips loop; one-shots (attacks, hits, deaths) do not.</summary>
        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal) || !IsAnimal(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                var n = c.name;
                c.loopTime = n.EndsWith("Idle", System.StringComparison.Ordinal) || n.EndsWith("Walk", System.StringComparison.Ordinal) ||
                             n.EndsWith("Run", System.StringComparison.Ordinal) || n.EndsWith("Gallop", System.StringComparison.Ordinal);
            }

            importer.clipAnimations = clips;
        }

        /// <summary>
        /// Phase 18: leaves, grass, flowers and petals in the nature kit are cut out by their texture's alpha and drawn on
        /// both sides (the kit ships them for its own stylised shader; on URP Lit they would show their texture's black edges).
        /// </summary>
        private void OnPreprocessMaterialDescription(UnityEditor.AssetImporters.MaterialDescription description, Material material, AnimationClip[] animations)
        {
            BindExtractedTexture(material);
            if (!assetPath.Contains("/Quaternius/StylizedNatureMegaKit/")) return;
            var n = material.name + " " + description.materialName;
            if (!(n.Contains("Leaf") || n.Contains("Leaves") || n.Contains("Grass") || n.Contains("Flower") || n.Contains("Petal") || n.Contains("Pine") || n.Contains("Plant") || n.Contains("Fern"))) return;
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.45f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        /// <summary>
        /// Phase 18: the converted creatures whose texture was embedded in the FBX (the Quaternius goblin and giant, the Poly by
        /// Google animals) came in untextured. Their texture is extracted beside the model as <c>&lt;Model&gt;_Texture.png</c>
        /// (Tools/Blender/extracttexture.py) and bound here as the base map.
        /// </summary>
        private void BindExtractedTexture(Material material)
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal) || !(IsAnimal(assetPath) || IsStaticKit(assetPath))) return;
            var texturePath = System.IO.Path.ChangeExtension(assetPath, null) + "_Texture.png";
            context.DependsOnSourceAsset(texturePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) return;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var isNormal = name.EndsWith("_Normal", System.StringComparison.Ordinal) || name.EndsWith("_NormalGL", System.StringComparison.Ordinal);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.maxTextureSize = assetPath.Contains("/ModularCharacterOutfitsFantasy/") && !isNormal ? 2048 : 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaIsTransparency = name == "T_VineLeaf";
            if (IsInterfaceArt(assetPath))
            {
                // Phase 19: interface art (icons, frames, map symbols, parchment), drawn by UI Toolkit at screen size.
                // No mipmaps (the UI never minifies far), clamped edges for 9-slices, transparency kept clean.
                var parchment = name.StartsWith("parchment", System.StringComparison.Ordinal);
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = 0;
                importer.alphaIsTransparency = !parchment;
                importer.maxTextureSize = parchment ? 1024 : assetPath.Contains("/RkIcons/") ? 256 : 128;
                importer.textureCompression = parchment ? TextureImporterCompression.Compressed : TextureImporterCompression.CompressedHQ;
                importer.npotScale = TextureImporterNPOTScale.None;
                return;
            }

            if (name.EndsWith("_Texture", System.StringComparison.Ordinal) && (IsAnimal(assetPath) || assetPath.Contains("/PolyByGoogle/")))
            {
                // Phase 18: the extracted creature textures; the small ones are colour palettes, which mipmaps would blur together.
                importer.mipmapEnabled = !assetPath.Contains("/Quaternius/");
                importer.filterMode = assetPath.Contains("/Quaternius/") || name == "Boar_Texture" ? FilterMode.Point : FilterMode.Bilinear;
                if (name == "Boar_Texture") importer.mipmapEnabled = false;
            }
        }
    }
}
