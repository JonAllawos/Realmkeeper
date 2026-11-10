using UnityEditor;
using UnityEngine;

namespace Realmkeeper.Editor
{
    /// <summary>
    /// Import settings for the third-party art in <c>Assets/Realmkeeper/Art/ThirdParty</c>, so every
    /// machine imports it identically (the settings also live in the .meta files):
    /// axis conversion baked (the packs' documented Unity setup), no imported cameras or lights,
    /// humanoid rigs for bodies and the animation library, no rig for kit modules, normal maps
    /// flagged as such, and texture sizes capped to keep memory within budget.
    /// </summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/Realmkeeper/Art/ThirdParty/";

        public override int GetPostprocessOrder() => 10;

        /// <summary>Bumped when the settings change, so Unity reimports the art.</summary>
        public override uint GetVersion() => 2;

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
            else if (assetPath.Contains("/UniversalAnimationLibrary/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.optimizeGameObjects = false;
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
        }
    }
}
