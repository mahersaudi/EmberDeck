using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Project-level identity, applied through the PlayerSettings API rather than by editing
    /// ProjectSettings.asset. The YAML is the Editor's to own: hand-edits are silently
    /// overwritten the next time it saves, and a field written with the wrong shape is
    /// accepted without complaint and then ignored.
    /// </summary>
    public static class ProjectSetup
    {
        const string Company = "mahersaudi";
        const string Product = "EmberDeck";
        const string Identifier = "com.mahersaudi.emberdeck";
        const string IconPath = "Assets/EmberDeck/Art/AppIcon.png";

        [MenuItem("EmberDeck/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;

            // The identifier is per build-target group; Standalone is the only one this
            // project ships. Setting it here rather than in the UI keeps it in version
            // control with everything else.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, Identifier);
            ApplyIcon();

            AssetDatabase.SaveAssets();
            Debug.Log($"[EmberDeck] Applied: {Company} / {Product} / {Identifier}");
        }

        /// <summary>
        /// The application icon for every platform, from one 1024px image (rendered by
        /// art/generate_icon.py, seed 29). Unity derives every size from it. Chosen by comparing the
        /// candidates at 32 and 64 pixels, where an icon is actually seen: the round emblem stayed
        /// legible, the tall shield became a dark smudge.
        /// </summary>
        public static bool ApplyIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[EmberDeck] No application icon at {IconPath}; builds will use Unity's default.");
                return false;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            return true;
        }
    }
}
