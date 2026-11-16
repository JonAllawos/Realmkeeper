using UnityEditor;
using UnityEngine;

namespace Realmkeeper.Editor
{
    /// <summary>
    /// 2026-11-15 audio pass: music under any <c>Resources/RkMusic/</c> folder is streamed from disk as Vorbis (minutes long,
    /// so never decompressed into memory); short sounds keep Unity's defaults.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/RkMusic/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.loadInBackground = true;
            importer.forceToMono = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
