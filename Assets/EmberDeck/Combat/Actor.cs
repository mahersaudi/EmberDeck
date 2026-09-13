using System.Collections.Generic;

namespace EmberDeck.Combat
{
    /// <summary>
    /// A combatant — player or enemy. Pure data plus trivial accessors: every rule that
    /// could ever be modified by a card, status or relic lives in CombatEngine, so there
    /// is exactly one place where damage can be changed.
    /// </summary>
    public class Actor
    {
        public string Name;
        public int MaxHp;
        public int Hp;
        public int Block;
        public bool IsPlayer;

        /// <summary>Set for enemies only — what this actor has announced it will do next.</summary>
        public Intent CurrentIntent;

        public readonly Dictionary<StatusType, int> Statuses = new();

        public bool IsAlive => Hp > 0;

        public Actor(string name, int maxHp, bool isPlayer = false)
        {
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
            IsPlayer = isPlayer;
        }

        public int GetStatus(StatusType type) => Statuses.TryGetValue(type, out var value) ? value : 0;

        public void AddStatus(StatusType type, int amount)
        {
            int next = GetStatus(type) + amount;
            if (next <= 0) Statuses.Remove(type);
            else Statuses[type] = next;
        }

        public void ClearStatus(StatusType type) => Statuses.Remove(type);

        public void Heal(int amount) => Hp = System.Math.Min(MaxHp, Hp + amount);
    }
}
