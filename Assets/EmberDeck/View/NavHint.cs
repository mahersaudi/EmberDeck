using UnityEngine;

namespace EmberDeck.View
{
    /// <summary>
    /// How PadNavigator should treat one object. Plain flags rather than several marker types: every
    /// screen is built in code, and one component with named fields reads better at the call site.
    /// </summary>
    public sealed class NavHint : MonoBehaviour
    {
        /// <summary>Focus lands on the highest priority when a screen opens. Ties go top-left first.</summary>
        public int Priority;

        /// <summary>A panel covering its screen, like a picker: while it is open, only it can be navigated.</summary>
        public bool Modal;

        /// <summary>The button Back (Escape, or B on a pad) presses on this screen.</summary>
        public bool Cancel;

        /// <summary>Never focused: something that looks clickable but does nothing, like a card in a summary.</summary>
        public bool Skip;

        public static NavHint On(Component target)
        {
            var hint = target.GetComponent<NavHint>();
            return hint != null ? hint : target.gameObject.AddComponent<NavHint>();
        }
    }
}
