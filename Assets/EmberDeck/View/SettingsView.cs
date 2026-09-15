using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Volume and display settings.
    ///
    /// Volume changes apply as the slider moves, and the effects slider clicks as it goes, so the
    /// level can be judged by ear rather than by a percentage. Display changes wait for Apply: a
    /// resolution that takes effect on each arrow press would resize the window under the pointer
    /// while it is still trying to press the arrow.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        public event Action Closed;

        Slider _master;
        Slider _music;
        Slider _effects;
        Text _masterReadout;
        Text _musicReadout;
        Text _effectsReadout;

        UiControls.Stepper _windowStepper;
        UiControls.Stepper _resolutionStepper;
        UiControls.Stepper _vsyncStepper;
        Text _displayNote;
        UiControls.Stepper _tipsStepper;

        List<Vector2Int> _resolutions = new();
        int _resolutionIndex;
        WindowMode _window;
        bool _vsync;

        public bool IsOpen => gameObject.activeSelf;

        public static SettingsView Create(Transform parent)
        {
            var root = UiFactory.Panel(parent, "SettingsScreen", new Color(0.05f, 0.05f, 0.07f, 0.98f));
            UiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<SettingsView>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            var title = UiFactory.Label(root, "SettingsTitle", "SETTINGS", 44, Palette.Ink);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -90f), new Vector2(800f, 60f));

            float y = -190f;
            Section(root, "AUDIO", y);
            y -= 48f;

            var master = UiControls.Row(root, "Master", "Master volume", y);
            _master = UiControls.VolumeSlider(master, Settings.MasterVolume,
                                              v => { Settings.MasterVolume = v; Settings.ApplyAudio(); }, out _masterReadout);
            y -= 64f;

            var music = UiControls.Row(root, "Music", "Music", y);
            _music = UiControls.VolumeSlider(music, Settings.MusicVolume,
                                             v => { Settings.MusicVolume = v; Settings.ApplyAudio(); }, out _musicReadout);
            y -= 64f;

            var effects = UiControls.Row(root, "Effects", "Sound effects", y);
            _effects = UiControls.VolumeSlider(effects, Settings.SfxVolume, v =>
            {
                Settings.SfxVolume = v;
                Settings.ApplyAudio();
                AudioDirector.Play(Sfx.Click);
            }, out _effectsReadout);
            y -= 96f;

            Section(root, "DISPLAY", y);
            y -= 48f;

            var window = UiControls.Row(root, "WindowMode", "Window", y);
            _windowStepper = UiControls.AddStepper(window, "Window", () => Settings.Describe(_window),
                                                   step => _window = (WindowMode)(((int)_window + step + 3) % 3));
            y -= 64f;

            var resolution = UiControls.Row(root, "Resolution", "Resolution", y);
            _resolutionStepper = UiControls.AddStepper(resolution, "Resolution", DescribeResolution, step =>
            {
                if (_resolutions.Count == 0) return;
                _resolutionIndex = (_resolutionIndex + step + _resolutions.Count) % _resolutions.Count;
            });
            y -= 64f;

            var vsync = UiControls.Row(root, "VSync", "V-Sync", y);
            _vsyncStepper = UiControls.AddStepper(vsync, "VSync", () => _vsync ? "On" : "Off", _ => _vsync = !_vsync);
            y -= 70f;

            _displayNote = UiFactory.Label(root, "DisplayNote", "", 20, Palette.InkMuted);
            UiFactory.Place(_displayNote.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, y), new Vector2(900f, 30f));
            y -= 62f;

            // Applies at once, unlike display: turning tips back on starts them over.
            Section(root, "GAMEPLAY", y);
            y -= 48f;
            var tips = UiControls.Row(root, "Tips", "Tutorial tips", y);
            _tipsStepper = UiControls.AddStepper(tips, "Tips", () => Coach.Enabled ? "On" : "Off",
                                                 _ => Coach.SetEnabled(!Coach.Enabled));

            var apply = UiFactory.TextButton(root, "ApplyDisplay", "Apply display", Palette.PanelRaised, Palette.Ink, 26);
            UiFactory.Place((RectTransform)apply.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(-150f, 80f), new Vector2(260f, 68f));
            apply.onClick.AddListener(ApplyDisplay);

            var back = UiFactory.TextButton(root, "SettingsBack", "Back", Palette.PanelRaised, Palette.Ink, 26);
            UiFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(150f, 80f), new Vector2(260f, 68f));
            back.onClick.AddListener(Close);
            NavHint.On(back).Cancel = true;

            gameObject.SetActive(false);
        }

        static void Section(Transform parent, string text, float y)
        {
            var label = UiFactory.Label(parent, "Section_" + text, text, 20, Palette.Energy, TextAnchor.MiddleLeft);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, y), new Vector2(UiControls.RowWidth, 36f));
        }

        string DescribeResolution()
        {
            if (_resolutions.Count == 0) return "—";
            var size = _resolutions[Mathf.Clamp(_resolutionIndex, 0, _resolutions.Count - 1)];
            return $"{size.x} × {size.y}";
        }

        public void Show()
        {
            _master.SetValueWithoutNotify(Settings.MasterVolume);
            _music.SetValueWithoutNotify(Settings.MusicVolume);
            _effects.SetValueWithoutNotify(Settings.SfxVolume);
            _masterReadout.text = UiControls.Percent(Settings.MasterVolume);
            _musicReadout.text = UiControls.Percent(Settings.MusicVolume);
            _effectsReadout.text = UiControls.Percent(Settings.SfxVolume);

            _window = Settings.Window;
            _vsync = Settings.VSync;
            _resolutions = Settings.Resolutions();
            var current = new Vector2Int(Settings.Width > 0 ? Settings.Width : Screen.width,
                                         Settings.Height > 0 ? Settings.Height : Screen.height);
            _resolutionIndex = Mathf.Max(0, _resolutions.IndexOf(current));

            _windowStepper.Refresh();
            _resolutionStepper.Refresh();
            _vsyncStepper.Refresh();
            _tipsStepper.Refresh();
            _displayNote.text = "Display changes take effect when applied.";

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Settings.SaveAudio();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void ApplyDisplay()
        {
            Settings.Window = _window;
            Settings.VSync = _vsync;
            if (_resolutions.Count > 0)
            {
                var size = _resolutions[Mathf.Clamp(_resolutionIndex, 0, _resolutions.Count - 1)];
                Settings.Width = size.x;
                Settings.Height = size.y;
            }
            Settings.SaveDisplay();
            Settings.ApplyDisplay();
            _displayNote.text = $"Applied: {Settings.Describe(_window)}, {DescribeResolution()}, V-Sync {(_vsync ? "on" : "off")}.";
        }
    }
}
