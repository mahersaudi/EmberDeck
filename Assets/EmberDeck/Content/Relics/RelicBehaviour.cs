using EmberDeck.Combat;

namespace EmberDeck.Content.Relics
{
    /// <summary>
    /// One relic, live in one combat. Attach subscribes to the event bus; Detach must undo
    /// every subscription — a relic that outlives its combat keeps modifying damage in the
    /// next one, and that class of bug is nearly impossible to find from the symptom.
    /// </summary>
    public abstract class RelicBehaviour
    {
        public RelicData Data { get; private set; }
        protected CombatState State;
        protected CombatEngine Engine;

        public void Attach(RelicData data, CombatState state, CombatEngine engine)
        {
            Data = data;
            State = state;
            Engine = engine;
            OnAttach();
        }

        public void Detach() => OnDetach();

        protected abstract void OnAttach();
        protected abstract void OnDetach();
    }
}
