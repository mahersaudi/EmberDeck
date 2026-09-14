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
            // Resources/Icons too: the UI icons are loaded by name at runtime, and Resources.Load<Sprite>
            // returns null for a texture that was not imported as a Sprite.
            if (!assetPath.Contains("/EmberDeck/Art/") && !assetPath.Contains("/EmberDeck/Resources/Icons/")
                && !assetPath.Contains("/EmberDeck/Resources/Backgrounds/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Backgrounds are full-screen paintings: uncompressed, each would add about 8 MB to the build
            // for detail nobody can see behind a darkened layer of cards and text.
            if (assetPath.Contains("/Resources/Backgrounds/"))
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
            }
        }
    }
}
