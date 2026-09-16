using UnityEngine;

namespace EmberDeck.View
{
    /// <summary>
    /// Whether this is a touch screen, which the interface has to answer in a few places.
    ///
    /// A phone has no pointer to hover with and no Escape key, and its screen is far taller than wide when
    /// held in landscape. So on touch: a card is tapped once to read it and again to play it, a tooltip opens
    /// on the tap rather than on hover, the canvas matches the design's height instead of splitting the
    /// difference, the layout keeps clear of the notch, and the pause menu gets a button on screen.
    ///
    /// Forced on for capture runs (-emberdeck-touch), so the phone layout can be photographed on a Mac.
    /// </summary>
    public static class TouchMode
    {
        static bool? _forced;

        public static bool Active => _forced ?? Application.isMobilePlatform;

        public static void Force(bool touch) => _forced = touch;
    }
}
