using UnityEngine;

namespace EmberDeck.Content
{
    /// <summary>
    /// One atomic thing a card does. Cards own a LIST of these, composed in the Inspector.
    ///
    /// This is the single most important decision in the codebase. The obvious alternative —
    /// an enum on CardData plus a switch in the play routine — works fine for thirty cards
    /// and then stops scaling: every new card edits shared code, and combinations that the
    /// switch never anticipated are simply impossible. With composable effect assets, a new
    /// card is authored content, not a code change, and "deal 6 damage AND apply 2 Weak AND
    /// draw a card" needs nothing new at all.
    ///
    /// Enemy moves use this same type, so anything a card can do, an enemy can do too.
    /// </summary>
    public abstract class CardEffect : ScriptableObject
    {
        public abstract void Apply(in EffectContext context);

        /// <summary>
        /// True when this effect acts on the chosen target. A card aimed at every enemy
        /// repeats only its per-target effects, so "deal 8 to all enemies and gain 5 Block"
        /// grants block once rather than once per enemy.
        /// </summary>
        public virtual bool IsPerTarget => false;

        /// <summary>
        /// Text for the card face. Written per-effect so rules text can never drift out of
        /// sync with behaviour — a mismatch players notice instantly and never forgive.
        /// </summary>
        public abstract string Describe();
    }
}
