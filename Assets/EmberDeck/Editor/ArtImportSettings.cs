using UnityEditor;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Forces everything under Art/ to import as a Sprite with no compression.
    ///
    /// Without this the icons arrive as default Textures, which cannot be assigned to a UI
    /// Image at all — and the failure is a null reference at runtime rather than anything
    /// the import log mentions. Setting it here rather than by hand also means a fresh clone
    /// is correct on first open, with no per-file .meta to get out of sync.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/EmberDeck/Art/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
