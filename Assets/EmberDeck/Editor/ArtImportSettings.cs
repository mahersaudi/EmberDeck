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
        /// <summary>Bumped whenever the settings below change, which is what makes Unity reimport the art.</summary>
        public override uint GetVersion() => 2;

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

            // Android is the one platform that cannot afford the paintings uncompressed: the same art that
            // costs 425 MB in the Mac build made a 985 MB APK. ASTC at 1024 is about a ninth of that, for
            // detail a phone draws into a 200-unit-wide card. The 128px UI icons are left alone — they are a
            // third of a megabyte in total, and block compression is most visible on small flat shapes.
            if (!assetPath.Contains("/Resources/Icons/"))
            {
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = 1024,
                    format = TextureImporterFormat.ASTC_6x6,
                    textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 100,
                });
            }

            // Frames are 9-sliced. The border has to be declared at import: without it Unity stretches the
            // whole image and the corners smear. Sizes are in the 128px source tile.
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (file == "frame_card") importer.spriteBorder = new UnityEngine.Vector4(26, 26, 26, 26);
            else if (file == "frame_button") importer.spriteBorder = new UnityEngine.Vector4(14, 14, 14, 14);
            else if (file == "frame_panel") importer.spriteBorder = new UnityEngine.Vector4(12, 12, 12, 12);
        }
    }
}
