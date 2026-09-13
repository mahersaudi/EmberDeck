using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Combat
{
    /// <summary>
    /// An Actor plus the small amount of state needed to choose its next move. Kept off
    /// Actor so the player never carries fields that mean nothing for it.
    /// </summary>
    public sealed class Enemy : Actor
    {
        public readonly EnemyData Data;

        int _sequenceIndex;
        EnemyMove _lastMove;
        int _lastMoveRepeats;

        public Enemy(EnemyData data, int hp) : base(data.DisplayName, hp)
        {
            Data = data;
        }

        /// <summary>
        /// Decides the next move. Enemies announce their intent a full turn ahead, which is
        /// what turns each turn from a gamble into a solvable problem — the player can always
        /// see whether blocking or racing is correct.
        /// </summary>
        public EnemyMove ChooseNextMove(DeterministicRng rng)
        {
            if (Data.Moves.Count == 0) return null;

            EnemyMove chosen = Data.Pattern == MovePattern.Sequence
                ? Data.Moves[_sequenceIndex++ % Data.Moves.Count]
                : PickWeighted(rng);

            if (chosen == _lastMove) _lastMoveRepeats++;
            else { _lastMove = chosen; _lastMoveRepeats = 1; }

            return chosen;
        }

        EnemyMove PickWeighted(DeterministicRng rng)
        {
            // A move that already ran twice in a row is excluded. Without this guard, pure
            // weighted random produces streaks that read to the player as the game cheating.
            bool Blocked(EnemyMove m) => m == _lastMove && _lastMoveRepeats >= 2;

            int total = 0;
            foreach (var move in Data.Moves)
                if (!Blocked(move)) total += UnityEngine.Mathf.Max(1, move.Weight);

            if (total <= 0) return Data.Moves[0];

            int roll = rng.Range(0, total);
            foreach (var move in Data.Moves)
            {
                if (Blocked(move)) continue;
                roll -= UnityEngine.Mathf.Max(1, move.Weight);
                if (roll < 0) return move;
            }
            return Data.Moves[0];
        }
    }
}
