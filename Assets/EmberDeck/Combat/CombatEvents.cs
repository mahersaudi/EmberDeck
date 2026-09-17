using EmberDeck.Content;

namespace EmberDeck.Combat
{
    // Every event below is a class, not a struct, so handlers can mutate it in place.
    // The "Calculation" events are the extension points: a relic that adds damage or a
    // status that halves it subscribes here and edits Amount. Nothing in CombatEngine
    // needs to know which relics exist.

    /// <summary>Raised before damage is applied. Mutate Amount to modify the hit.</summary>
    public sealed class DamageCalculation
    {
        public Actor Source;
        public Actor Target;
        public int Amount;
        /// <summary>False for unblockable/direct HP loss (poison), which skips Strength, Weak and Vulnerable.</summary>
        public bool IsAttack;
    }

    /// <summary>Raised before block is granted. Mutate Amount to modify it.</summary>
    public sealed class BlockCalculation
    {
        public Actor Target;
        public int Amount;
    }

    /// <summary>Raised before a card's energy cost is paid. Mutate Cost to discount it.</summary>
    public sealed class CardCostCalculation
    {
        public CardInstance Card;
        public int Cost;
    }

    public sealed class CardPlayedEvent
    {
        public CardInstance Card;
        public Actor Target;
    }

    public sealed class CardDrawnEvent
    {
        public CardInstance Card;
    }

    public sealed class TurnStartedEvent
    {
        public bool IsPlayerTurn;
        public int TurnNumber;
    }

    public sealed class TurnEndedEvent
    {
        public bool IsPlayerTurn;
        public int TurnNumber;
    }

    public sealed class DamageAppliedEvent
    {
        public Actor Source;
        public Actor Target;
        public int HpLost;
        public int BlockAbsorbed;
    }

    public sealed class ActorDiedEvent
    {
        public Actor Actor;
    }

    public sealed class CombatEndedEvent
    {
        public bool PlayerWon;
    }

    /// <summary>Raised after Block is actually applied. Forge Rite listens here.</summary>
    public sealed class BlockGainedEvent
    {
        public Actor Target;
        public int Amount;
    }

    /// <summary>Raised after Heat is added. Ember Engine listens here.</summary>
    /// <summary>The Emberheart's rule paid out: the player started a turn overheated and gained Energy.</summary>
    public sealed class HeartFireEvent
    {
        public int Energy;
    }

    public sealed class HeatGainedEvent
    {
        public int Amount;
        public int Total;
    }

    /// <summary>Raised when a card leaves play permanently. Pyre Rite and Phoenix Ash listen here.</summary>
    public sealed class CardExhaustedEvent
    {
        public CardInstance Card;
    }

    /// <summary>Raised after a status lands. Searing Blade listens for Burn.</summary>
    public sealed class StatusAppliedEvent
    {
        public Actor Target;
        public StatusType Status;
        public int Amount;
    }

    /// <summary>Anything the view should redraw. Kept coarse on purpose: the model never
    /// knows what a view is, and the view never reaches into the model to poll.</summary>
    public sealed class CombatStateChangedEvent
    {
    }

    /// <summary>A potion was drunk. Published after its effects have resolved.</summary>
    public sealed class PotionUsedEvent
    {
        public PotionData Potion;
        public Actor Target;
    }
}
