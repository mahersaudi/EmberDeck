using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Keeps the whole 1920×1080 design on screen whatever shape the device is.
    ///
    /// A CanvasScaler matches one axis and lets the other overflow. Matching height suits a phone,
    /// which is far wider than the design: the extra width becomes margin. It is exactly wrong on an
    /// iPad, whose 4:3 screen is *narrower* than 16:9 — matched to height, the design's 1920 units of
    /// width do not fit in the 1440 the screen has, and the End Turn button and the player's panel
    /// are cut off the sides.
    ///
    /// So the axis follows the device: wider than 16:9 matches height, narrower matches width, and
    /// the design always fits. Re-checked when the screen changes, because a tablet rotates and a
    /// phone can be put in a split view.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ScalerFit : MonoBehaviour
    {
        const float DesignAspect = 1920f / 1080f;

        CanvasScaler _scaler;
        Vector2Int _applied;

        void Awake() => _scaler = GetComponent<CanvasScaler>();

        void OnEnable() => Apply();

        void Update() => Apply();

        void Apply()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == _applied || screen.x <= 0 || screen.y <= 0) return;
            _applied = screen;

            float aspect = (float)screen.x / screen.y;
            _scaler.matchWidthOrHeight = aspect >= DesignAspect ? 1f : 0f;
        }
    }
}
