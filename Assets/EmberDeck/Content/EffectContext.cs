using EmberDeck.Combat;

namespace EmberDeck.Content
{
    /// <summary>
    /// Everything an effect is allowed to see. Passing one context instead of five loose
    /// arguments means adding a new capability later (say, "which card triggered this")
    /// doesn't force an edit to every effect in the game.
    /// </summary>
    public readonly struct EffectContext
    {
        public readonly CombatState State;
        public readonly CombatEngine Engine;
        public readonly Actor Source;
        public readonly Actor Target;
        public readonly CardInstance Card;

        public EffectContext(CombatState state, CombatEngine engine, Actor source, Actor target, CardInstance card)
        {
            State = state;
            Engine = engine;
            Source = source;
            Target = target;
            Card = card;
        }

        public EffectContext WithTarget(Actor target) => new(State, Engine, Source, target, Card);
    }
}
