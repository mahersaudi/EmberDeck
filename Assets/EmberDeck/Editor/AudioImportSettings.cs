using System.IO;
using UnityEditor;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Import rules for the generated audio.
    ///
    /// Effects are short and fire the instant a card lands, so they are decompressed on load:
    /// decoding a compressed clip at play time adds latency exactly where the player notices it.
    /// Music is a minute long each, so it streams and stays compressed instead of sitting in
    /// memory as ten megabytes of PCM.
    /// </summary>
    sealed class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/EmberDeck/Resources/Audio/")) return;

            var importer = (AudioImporter)assetImporter;
            bool music = Path.GetFileName(assetPath).StartsWith("music_");

            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;

            importer.forceToMono = !music;
            importer.loadInBackground = music;
        }
    }
}
