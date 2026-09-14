using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EmberDeck.View
{
    /// <summary>
    /// Shows a tooltip while the pointer is over this object.
    ///
    /// The content is a function, not a snapshot, because what needs explaining changes during a
    /// fight: an enemy's tooltip has to describe the intent it has now and the Burn it has now, not
    /// the ones it had when its view was built.
    ///
    /// Put triggers only on top-level things — a card, an enemy, a panel — never on a child of
    /// another trigger. Pointer-enter reaches every ancestor too, so nested triggers would fight
    /// over the one tooltip.
    /// </summary>
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<IReadOnlyList<Tooltip.Entry>> Content;

        /// <summary>What the tooltip is placed beside; this object when unset.</summary>
        public RectTransform Anchor;

        public static TooltipTrigger Attach(GameObject target, Func<IReadOnlyList<Tooltip.Entry>> content,
                                            RectTransform anchor = null)
        {
            var trigger = target.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = target.AddComponent<TooltipTrigger>();
            trigger.Content = content;
            trigger.Anchor = anchor;
            return trigger;
        }

        public void OnPointerEnter(PointerEventData eventData) => ShowNow();

        public void OnPointerExit(PointerEventData eventData) => Tooltip.Hide(this);

        void OnDisable() => Tooltip.Hide(this);

        void OnDestroy() => Tooltip.Hide(this);

        public void ShowNow()
        {
            var entries = Content?.Invoke();
            Tooltip.Show(this, Anchor != null ? Anchor : (RectTransform)transform, entries);
        }
    }
}
