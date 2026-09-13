using EmberDeck.Combat;

namespace EmberDeck.Content.Powers
{
    /// <summary>
    /// One Power, live for one combat. Same shape as RelicBehaviour on purpose — a Power and
    /// a relic are the same thing with different timing of acquisition, and giving them the
    /// same lifecycle means one set of rules about attaching and detaching rather than two.
    /// </summary>
    public abstract class PowerBehaviour
    {
        protected CombatState State;
        protected CombatEngine Engine;

        public void Attach(CombatState state, CombatEngine engine)
        {
            State = state;
            Engine = engine;
            OnAttach();
        }

        public void Detach() => OnDetach();

        protected abstract void OnAttach();
        protected abstract void OnDetach();
    }
}
