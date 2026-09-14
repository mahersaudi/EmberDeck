using System;
using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>Everything a choice needs to decide whether it can be taken and to take it.</summary>
    public sealed class EventContext
    {
        public RunState Run;
        public RunConfig Config;

        /// <summary>Derived from the event's map position, so a gamble resolves the same way after a restart.</summary>
        public DeterministicRng Rng;
    }

    /// <summary>One option in an event.</summary>
    public sealed class EventChoice
    {
        public string Label;

        /// <summary>What the choice does, shown before it is taken. A choice is never a surprise.</summary>
        public string Effect;

        /// <summary>Why the choice cannot be taken right now, or null when it can.</summary>
        public Func<EventContext, string> Blocked = _ => null;

        /// <summary>Applies the choice and returns the sentence shown afterwards.</summary>
        public Func<EventContext, string> Apply;

        /// <summary>
        /// How much the simulator's bot wants this choice; the highest available one is taken, and
        /// leaving is worth 0. Written beside the choice so a new event cannot ship without one.
        /// </summary>
        public Func<EventContext, int> BotValue = _ => 0;
    }

    /// <summary>
    /// A "?" node on the map: a short scene and a choice with a cost and a reward.
    ///
    /// Events are plain code rather than assets because each choice is behaviour — a condition, an
    /// effect, a sentence — and three delegates read far better in one place than spread across a
    /// ScriptableObject and the effect assets it would have to reference.
    /// </summary>
    public sealed class RunEvent
    {
        public string Id;
        public string Title;
        public string Body;
        public readonly List<EventChoice> Choices = new();
    }
}
