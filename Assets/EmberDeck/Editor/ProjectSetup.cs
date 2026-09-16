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
            ApplyInput();
            ApplyFonts();

            AssetDatabase.SaveAssets();
            Debug.Log($"[EmberDeck] Applied: {Company} / {Product} / {Identifier}");
        }

        // Joystick axes for PadNavigator: both sticks' first axes, and the D-pad, which Windows and Steam
        // Input report as the 6th and 7th axes. Y is inverted on the stick so that up is positive, as it is
        // on the D-pad. A button needs no axis: it is read as KeyCode.JoystickButtonN.
        static readonly (string Name, int Axis, bool Invert)[] PadAxes =
        {
            ("Pad Stick X", 0, false),
            ("Pad Stick Y", 1, true),
            ("Pad DPad X", 5, false),
            ("Pad DPad Y", 6, false),
        };

        /// <summary>
        /// Adds the gamepad axes to the input manager, through SerializedObject for the same reason the rest
        /// of this class uses APIs: the settings file is the Editor's to write. Idempotent; an axis that
        /// already exists is updated in place.
        /// </summary>
        public static bool ApplyInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[EmberDeck] Input manager settings not found; gamepad axes not added.");
                return false;
            }

            var settings = new SerializedObject(assets[0]);
            var axes = settings.FindProperty("m_Axes");
            foreach (var (name, axis, invert) in PadAxes)
            {
                SerializedProperty entry = null;
                for (int i = 0; i < axes.arraySize && entry == null; i++)
                    if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == name)
                        entry = axes.GetArrayElementAtIndex(i);
                if (entry == null)
                {
                    axes.arraySize++;
                    entry = axes.GetArrayElementAtIndex(axes.arraySize - 1);
                }

                entry.FindPropertyRelative("m_Name").stringValue = name;
                entry.FindPropertyRelative("descriptiveName").stringValue = "";
                entry.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
                entry.FindPropertyRelative("negativeButton").stringValue = "";
                entry.FindPropertyRelative("positiveButton").stringValue = "";
                entry.FindPropertyRelative("altNegativeButton").stringValue = "";
                entry.FindPropertyRelative("altPositiveButton").stringValue = "";
                entry.FindPropertyRelative("gravity").floatValue = 0f;
                entry.FindPropertyRelative("dead").floatValue = 0.2f;
                entry.FindPropertyRelative("sensitivity").floatValue = 1f;
                entry.FindPropertyRelative("snap").boolValue = false;
                entry.FindPropertyRelative("invert").boolValue = invert;
                entry.FindPropertyRelative("type").intValue = 2;   // joystick axis
                entry.FindPropertyRelative("axis").intValue = axis;
                entry.FindPropertyRelative("joyNum").intValue = 0; // any controller
            }

            if (settings.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>
        /// The Android player's settings. Landscape only, because the board is a wide row of cards above a wide
        /// hand and nothing about it works in a column. ARM64 with IL2CPP is what the Play Store requires and
        /// what every phone since 2017 runs; an APK rather than an app bundle, so a tester can install the file
        /// directly. The debug keystore is deliberate: this signs playtest builds, not store uploads.
        /// </summary>
        public static void ApplyAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Identifier);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // 26 is this editor's floor: asking for 24 is silently raised, and the APK then disagrees with
            // the setting it was built from.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            // Nothing in the game talks to a network. This only stops Unity from *forcing* the permission;
            // its own unityLibrary manifest declares INTERNET anyway, and the merged APK still asks for it.
            // Dropping it needs a custom main manifest that overrides the template — worth doing before a
            // store release, not before a playtest.
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.useCustomKeystore = false;
            // Declares the one format the art is compressed to (see ArtImportSettings). Every ARM64 phone
            // this build targets supports ASTC.
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };
            EditorUserBuildSettings.buildAppBundle = false;
        }

        const string ArabicFontPath = "Assets/EmberDeck/Resources/Fonts/NotoNaskhArabicUI-Regular.ttf";
        const string FallbackFontPath = "Assets/EmberDeck/Resources/Fonts/DejaVuSans.ttf";

        /// <summary>
        /// Gives the Arabic font DejaVu Sans as its fallback. Noto Naskh Arabic UI has almost no Latin, and Arabic
        /// screens still show some: key names in the controls bar, "×", a seed. Idempotent.
        /// </summary>
        public static bool ApplyFonts()
        {
            var importer = AssetImporter.GetAtPath(ArabicFontPath) as TrueTypeFontImporter;
            var fallback = AssetDatabase.LoadAssetAtPath<Font>(FallbackFontPath);
            if (importer == null || fallback == null)
            {
                Debug.LogWarning("[EmberDeck] Arabic font or its fallback not found; Latin text in Arabic mode may be missing.");
                return false;
            }
            if (importer.fontReferences != null && importer.fontReferences.Length == 1 && importer.fontReferences[0] == fallback)
                return true;

            // The property alone did not reach the .meta in a batch build (fallbackFontReferences stayed empty), so the
            // serialized field is written too and the importer marked dirty before reimporting.
            importer.fontReferences = new[] { fallback };
            var serialized = new SerializedObject(importer);
            var references = serialized.FindProperty("fallbackFontReferences");
            if (references != null)
            {
                references.arraySize = 1;
                references.GetArrayElementAtIndex(0).objectReferenceValue = fallback;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            bool applied = importer.fontReferences != null && importer.fontReferences.Length == 1 && importer.fontReferences[0] == fallback;
            Debug.Log($"[EmberDeck] Arabic font fallback {(applied ? "set to DejaVu Sans" : "NOT applied")}");
            return applied;
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
