using System.Collections.Generic;
using UnityEngine;

namespace EmberDeck.View
{
    public enum WindowMode { Borderless, Exclusive, Windowed }

    /// <summary>
    /// The player's preferences: volume and display, kept in PlayerPrefs.
    ///
    /// PlayerPrefs rather than the run save because these belong to the machine, not to a run —
    /// abandoning a run must never reset someone's volume.
    ///
    /// Display settings are applied at launch only once the player has actually chosen them.
    /// Unity already restores the last window size on its own, and forcing a default resolution on
    /// every start would override that, fight the operating system, and resize the capture
    /// harness's window mid-run.
    /// </summary>
    public static class Settings
    {
        const string Prefix = "emberdeck.settings.";

        public static float MasterVolume = 1f;
        public static float MusicVolume = 0.5f;
        public static float SfxVolume = 0.9f;

        public static WindowMode Window = WindowMode.Borderless;
        public static int Width;
        public static int Height;
        public static bool VSync = true;

        static bool _displayChosen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadOnStartup()
        {
            Load();
            ApplyAudio();
            if (_displayChosen) ApplyDisplay();
        }

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "master", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "music", 0.5f);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", 0.9f);

            _displayChosen = PlayerPrefs.GetInt(Prefix + "displayChosen", 0) == 1;
            Window = (WindowMode)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "window", (int)CurrentWindowMode()), 0, 2);
            Width = PlayerPrefs.GetInt(Prefix + "width", Screen.width);
            Height = PlayerPrefs.GetInt(Prefix + "height", Screen.height);
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
        }

        public static void ApplyAudio()
        {
            AudioDirector.MasterVolume = MasterVolume;
            AudioDirector.MusicVolume = MusicVolume;
            AudioDirector.SfxVolume = SfxVolume;
        }

        public static void SaveAudio()
        {
            PlayerPrefs.SetFloat(Prefix + "master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "music", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", SfxVolume);
            PlayerPrefs.Save();
        }

        public static void ApplyDisplay()
        {
            var mode = Window switch
            {
                WindowMode.Exclusive => FullScreenMode.ExclusiveFullScreen,
                WindowMode.Windowed  => FullScreenMode.Windowed,
                _                    => FullScreenMode.FullScreenWindow,
            };

            if (Width > 0 && Height > 0) Screen.SetResolution(Width, Height, mode);
            else Screen.fullScreenMode = mode;

            QualitySettings.vSyncCount = VSync ? 1 : 0;
        }

        public static void SaveDisplay()
        {
            _displayChosen = true;
            PlayerPrefs.SetInt(Prefix + "displayChosen", 1);
            PlayerPrefs.SetInt(Prefix + "window", (int)Window);
            PlayerPrefs.SetInt(Prefix + "width", Width);
            PlayerPrefs.SetInt(Prefix + "height", Height);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static string Describe(WindowMode mode) => mode switch
        {
            WindowMode.Exclusive => "Fullscreen (exclusive)",
            WindowMode.Windowed  => "Windowed",
            _                    => "Fullscreen (borderless)",
        };

        /// <summary>
        /// The sizes the display offers — one entry per size, with refresh rates collapsed — smallest
        /// first, and never below 1280×720, which is the smallest size the layout was designed for.
        /// The current size is always included, so the list can show where the player is now.
        /// </summary>
        public static List<Vector2Int> Resolutions()
        {
            var sizes = new List<Vector2Int>();
            foreach (var resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (size.x >= 1280 && size.y >= 720 && !sizes.Contains(size)) sizes.Add(size);
            }

            var current = new Vector2Int(Width > 0 ? Width : Screen.width, Height > 0 ? Height : Screen.height);
            if (!sizes.Contains(current)) sizes.Add(current);

            sizes.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
            return sizes;
        }

        static WindowMode CurrentWindowMode() => Screen.fullScreenMode switch
        {
            FullScreenMode.ExclusiveFullScreen => WindowMode.Exclusive,
            FullScreenMode.FullScreenWindow    => WindowMode.Borderless,
            _                                  => WindowMode.Windowed,
        };
    }
}
