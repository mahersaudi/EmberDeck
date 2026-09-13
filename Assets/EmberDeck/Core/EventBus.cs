using System;
using System.Collections.Generic;

namespace EmberDeck.Core
{
    /// <summary>
    /// Typed publish/subscribe used by every card, status and relic.
    ///
    /// Payloads are reference types on purpose: a handler may MUTATE the payload, which is
    /// how a relic or a status changes a value (damage, block, card cost) without the combat
    /// code knowing that relic exists. That one indirection is the reason a deckbuilder can
    /// grow to hundreds of interacting pieces instead of collapsing into a giant switch.
    /// </summary>
    public sealed class EventBus
    {
        readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Subscribe<T>(Action<T> handler) where T : class
        {
            var key = typeof(T);
            if (!_handlers.TryGetValue(key, out var list))
            {
                list = new List<Delegate>();
                _handlers[key] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T payload) where T : class
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
                return;

            // Snapshot: a handler may subscribe or unsubscribe mid-dispatch. A relic that
            // fires once and then removes itself is the ordinary case, not an edge case.
            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
                ((Action<T>)snapshot[i]).Invoke(payload);
        }

        public void Clear() => _handlers.Clear();
    }
}
