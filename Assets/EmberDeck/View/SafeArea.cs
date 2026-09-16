using UnityEngine;

namespace EmberDeck.View
{
    /// <summary>
    /// Keeps the stage inside the screen's safe area, so a notch, a rounded corner or a gesture bar never
    /// covers the game.
    ///
    /// The whole interface hangs off one stage (CombatView), so insetting that one rectangle is enough. The
    /// safe area changes when the device rotates or the system bars appear, so it is checked rather than read
    /// once. Only added on touch screens; on a desktop the safe area is the whole window.
    /// </summary>
    public sealed class SafeArea : MonoBehaviour
    {
        RectTransform _rect;
        Rect _applied;
        Vector2Int _appliedScreen;

        void Awake() => _rect = (RectTransform)transform;

        void OnEnable() => Apply();

        void Update() => Apply();

        void Apply()
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (area == _applied && screen == _appliedScreen) return;
            if (screen.x <= 0 || screen.y <= 0) return;

            _applied = area;
            _appliedScreen = screen;
            _rect.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            _rect.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
