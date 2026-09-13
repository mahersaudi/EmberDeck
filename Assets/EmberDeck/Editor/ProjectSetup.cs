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

        [MenuItem("EmberDeck/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;

            // The identifier is per build-target group; Standalone is the only one this
            // project ships. Setting it here rather than in the UI keeps it in version
            // control with everything else.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, Identifier);

            AssetDatabase.SaveAssets();
            Debug.Log($"[EmberDeck] Applied: {Company} / {Product} / {Identifier}");
        }
    }
}
