using System.Collections.Generic;

namespace EmberDeck.Run
{
    public enum NodeType
    {
        Fight,
        Elite,
        Rest,
        Treasure,
        Boss
    }

    /// <summary>One stop on the map. Rows run bottom to top; the boss is the last row.</summary>
    public sealed class MapNode
    {
        public int Row;
        public int Column;
        public NodeType Type;

        /// <summary>Nodes in the row above that this one leads to.</summary>
        public readonly List<MapNode> Next = new();

        public bool Visited;

        public override string ToString() => $"{Type}@{Row},{Column}";
    }
}
