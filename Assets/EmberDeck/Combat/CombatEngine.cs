using System;
using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Content.Effects;
using EmberDeck.Core;

namespace EmberDeck.Combat
{
    /// <summary>
    /// Every rule of combat, in one place. Cards, statuses and relics never modify HP or
    /// block directly — they call into here, so there is exactly one code path a bug can
    /// hide in and exactly one place where a relic can intercept a value.
    /// </summary>
    public sealed class CombatEngine
    {
        public readonly CombatState State;

        public CombatEngine(CombatState state) => State = state;

        // ── Setup ────────────────────────────────────────────────────────────────────

        public void StartCombat(IEnumerable<CardInstance> deck)
        {
            State.DrawPile.Clear();
            State.Hand.Clear();
            State.DiscardPile.Clear();
            State.ExhaustPile.Clear();

            State.DrawPile.AddRange(deck);
            State.Rng.Shuffle.Shuffle(State.DrawPile);

            State.TurnNumber = 0;
            State.IsOver = false;
            State.PlayerWon = false;

            foreach (var enemy in State.Enemies)
                RollIntent(enemy);

            BeginPlayerTurn();
        }

        // ── Turn flow ────────────────────────────────────────────────────────────────

        void BeginPlayerTurn()
        {
            State.TurnNumber++;

            // Block expires at the start of its owner's turn, so block bought this turn is
            // what defends against the enemy turn that follows. Any other timing quietly
            // makes every defensive card a turn too slow.
            State.Player.Block = 0;
            State.Energy = State.EnergyPerTurn;

            DrawCards(State.CardsPerTurn);

            State.Bus.Publish(new TurnStartedEvent { IsPlayerTurn = true, TurnNumber = State.TurnNumber });
            State.Bus.Publish(new CombatStateChangedEvent());
        }

        public void EndPlayerTurn()
        {
            if (State.IsOver) return;

            State.Bus.Publish(new TurnEndedEvent { IsPlayerTurn = true, TurnNumber = State.TurnNumber });

            DiscardHand();
            TickEndOfTurn(State.Player);
            if (CheckCombatOver()) return;

            RunEnemyTurn();
            if (CheckCombatOver()) return;

            BeginPlayerTurn();
        }

        void RunEnemyTurn()
        {
            foreach (var enemy in State.Enemies)
            {
                if (!enemy.IsAlive) continue;

                enemy.Block = 0;
                ExecuteIntent(enemy);
                TickEndOfTurn(enemy);

                if (!State.Player.IsAlive) return;
            }

            // Announce the next round only after everyone has acted, so the player reads
            // one stable board instead of intents shifting mid-resolution.
            foreach (var enemy in State.Enemies)
                if (enemy.IsAlive) RollIntent(enemy);

            State.Bus.Publish(new CombatStateChangedEvent());
        }

        void ExecuteIntent(Enemy enemy)
        {
            var move = enemy.CurrentIntent.Move;
            if (move == null) return;

            foreach (var effect in move.Effects)
            {
                if (effect == null) continue;
                effect.Apply(new EffectContext(State, this, enemy, State.Player, null));
                if (!State.Player.IsAlive) return;
            }
        }

        /// <summary>End-of-turn upkeep for one actor: poison bites, durations tick down.</summary>
        void TickEndOfTurn(Actor actor)
        {
            int poison = actor.GetStatus(StatusType.Poison);
            if (poison > 0)
            {
                // Poison is HP loss, not an attack: it ignores block, Strength and Vulnerable.
                LoseHp(actor, poison);
                actor.AddStatus(StatusType.Poison, -1);
            }

            foreach (StatusType status in Enum.GetValues(typeof(StatusType)))
                if (status.IsDuration() && actor.GetStatus(status) > 0)
                    actor.AddStatus(status, -1);
        }

        // ── Playing cards ────────────────────────────────────────────────────────────

        public int GetCardCost(CardInstance card)
        {
            var calculation = new CardCostCalculation { Card = card, Cost = card.BaseCost };
            State.Bus.Publish(calculation);
            return Math.Max(0, calculation.Cost);
        }

        public bool CanPlay(CardInstance card, Actor target)
        {
            if (State.IsOver || card == null || !State.Hand.Contains(card)) return false;
            if (GetCardCost(card) > State.Energy) return false;
            if (card.Data.Target == TargetMode.SingleEnemy && (target == null || !target.IsAlive)) return false;
            return true;
        }

        public bool TryPlayCard(CardInstance card, Actor target)
        {
            if (!CanPlay(card, target)) return false;

            State.Energy -= GetCardCost(card);
            State.Hand.Remove(card);

            ResolveEffects(card, target);

            if (card.Data.Exhaust) State.ExhaustPile.Add(card);
            else State.DiscardPile.Add(card);

            State.Bus.Publish(new CardPlayedEvent { Card = card, Target = target });
            CheckCombatOver();
            State.Bus.Publish(new CombatStateChangedEvent());
            return true;
        }

        void ResolveEffects(CardInstance card, Actor target)
        {
            foreach (var effect in card.Data.Effects)
            {
                if (effect == null) continue;

                if (card.Data.Target == TargetMode.AllEnemies && effect.IsPerTarget)
                {
                    // Snapshot: an effect may kill an enemy, and mutating the list mid-loop
                    // would skip the next one.
                    var targets = new List<Enemy>(State.LivingEnemies());
                    foreach (var enemy in targets)
                        effect.Apply(new EffectContext(State, this, State.Player, enemy, card));
                }
                else
                {
                    effect.Apply(new EffectContext(State, this, State.Player, target, card));
                }
            }
        }

        // ── The mutating primitives every effect goes through ────────────────────────

        public void DealDamage(Actor source, Actor target, int amount, bool isAttack)
        {
            if (target == null || !target.IsAlive) return;

            int damage = amount;

            if (isAttack)
            {
                // Order is load-bearing: Strength adds flat, THEN the multipliers apply.
                // Multiplying before the bonus would make Strength scale with Vulnerable
                // and turn every buff stack into a runaway.
                damage += source?.GetStatus(StatusType.Strength) ?? 0;

                if (source != null && source.GetStatus(StatusType.Weak) > 0)
                    damage = (int)(damage * 0.75f);

                if (target.GetStatus(StatusType.Vulnerable) > 0)
                    damage = (int)(damage * 1.5f);
            }

            var calculation = new DamageCalculation
            {
                Source = source, Target = target, Amount = damage, IsAttack = isAttack
            };
            State.Bus.Publish(calculation);

            damage = Math.Max(0, calculation.Amount);

            int absorbed = Math.Min(target.Block, damage);
            target.Block -= absorbed;
            int hpLost = damage - absorbed;
            target.Hp = Math.Max(0, target.Hp - hpLost);

            State.Bus.Publish(new DamageAppliedEvent
            {
                Source = source, Target = target, HpLost = hpLost, BlockAbsorbed = absorbed
            });

            if (!target.IsAlive)
                State.Bus.Publish(new ActorDiedEvent { Actor = target });
        }

        /// <summary>Direct HP loss — ignores block entirely. Used by poison and card costs.</summary>
        public void LoseHp(Actor actor, int amount)
        {
            if (actor == null || !actor.IsAlive || amount <= 0) return;

            actor.Hp = Math.Max(0, actor.Hp - amount);
            State.Bus.Publish(new DamageAppliedEvent
            {
                Source = null, Target = actor, HpLost = amount, BlockAbsorbed = 0
            });

            if (!actor.IsAlive)
                State.Bus.Publish(new ActorDiedEvent { Actor = actor });
        }

        public void GainBlock(Actor actor, int amount)
        {
            if (actor == null || !actor.IsAlive) return;

            int block = amount + actor.GetStatus(StatusType.Dexterity);

            var calculation = new BlockCalculation { Target = actor, Amount = block };
            State.Bus.Publish(calculation);

            actor.Block += Math.Max(0, calculation.Amount);
        }

        public void ApplyStatus(Actor actor, StatusType status, int amount)
        {
            if (actor == null || !actor.IsAlive) return;
            actor.AddStatus(status, amount);
        }

        // ── Deck handling ────────────────────────────────────────────────────────────

        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (State.DrawPile.Count == 0)
                {
                    if (State.DiscardPile.Count == 0) return;  // genuinely out of cards
                    ReshuffleDiscardIntoDraw();
                }

                int last = State.DrawPile.Count - 1;
                var card = State.DrawPile[last];
                State.DrawPile.RemoveAt(last);
                State.Hand.Add(card);

                State.Bus.Publish(new CardDrawnEvent { Card = card });
            }
        }

        void ReshuffleDiscardIntoDraw()
        {
            State.DrawPile.AddRange(State.DiscardPile);
            State.DiscardPile.Clear();
            State.Rng.Shuffle.Shuffle(State.DrawPile);
        }

        void DiscardHand()
        {
            State.DiscardPile.AddRange(State.Hand);
            State.Hand.Clear();
        }

        // ── Intent ───────────────────────────────────────────────────────────────────

        void RollIntent(Enemy enemy)
        {
            var move = enemy.ChooseNextMove(State.Rng.Enemies);
            enemy.CurrentIntent = BuildIntent(enemy, move);
        }

        /// <summary>
        /// Resolves the announced number through the same modifiers the real hit will use.
        /// A preview that the actual attack then contradicts costs more trust than showing
        /// no number at all.
        /// </summary>
        public Intent BuildIntent(Enemy enemy, EnemyMove move)
        {
            var intent = new Intent { Move = move, Kind = move?.Kind ?? IntentKind.Attack, Hits = 1 };
            if (move == null) return intent;

            foreach (var effect in move.Effects)
            {
                switch (effect)
                {
                    case DealDamageEffect damage:
                    {
                        int value = damage.Amount + enemy.GetStatus(StatusType.Strength);
                        if (enemy.GetStatus(StatusType.Weak) > 0) value = (int)(value * 0.75f);
                        if (State.Player.GetStatus(StatusType.Vulnerable) > 0) value = (int)(value * 1.5f);

                        intent.Value = Math.Max(0, value);
                        intent.Hits = Math.Max(1, damage.Hits);
                        intent.Kind = IntentKind.Attack;
                        return intent;
                    }
                    case GainBlockEffect block:
                        intent.Value = block.Amount + enemy.GetStatus(StatusType.Dexterity);
                        intent.Kind = IntentKind.Block;
                        break;
                }
            }
            return intent;
        }

        // ── Resolution ───────────────────────────────────────────────────────────────

        bool CheckCombatOver()
        {
            if (State.IsOver) return true;

            if (!State.Player.IsAlive)
            {
                State.IsOver = true;
                State.PlayerWon = false;
            }
            else if (!State.AnyEnemyAlive())
            {
                State.IsOver = true;
                State.PlayerWon = true;
            }
            else return false;

            State.Bus.Publish(new CombatEndedEvent { PlayerWon = State.PlayerWon });
            return true;
        }
    }
}
