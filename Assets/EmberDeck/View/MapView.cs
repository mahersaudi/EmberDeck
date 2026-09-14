using System;
using System.Collections.Generic;
using EmberDeck.Run;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The run map: rows of nodes, the paths between them, and which ones can be entered now.
    ///
    /// Drawn bottom-up so "forward" is upward, and edges are drawn before nodes so a line
    /// never crosses a node's face. Unreachable nodes stay visible rather than being hidden —
    /// seeing the elite two rows up that you are steering away from is the decision.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        const float NodeSize = 58f;
        const float RowSpacing = 86f;
        const float ColumnSpacing = 132f;

        RectTransform _root;
        RectTransform _board;
        Text _title;
        RunMap _map;

        readonly List<GameObject> _drawn = new();

        public event Action<MapNode> NodeChosen;

        public static MapView Create(Transform parent)
        {
            // Fully opaque: at 0.97 the combat HUD showed through as ghost furniture behind
            // the map, which reads as a rendering bug rather than as depth.
            var root = UiFactory.Panel(parent, "MapScreen", new Color(0.05f, 0.05f, 0.07f, 1f));
            UiFactory.Stretch(root);

            var view = root.gameObject.AddComponent<MapView>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            _root = root;

            _title = UiFactory.Label(root, "MapTitle", "CHOOSE YOUR PATH", 34, Palette.Ink);
            UiFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -40f), new Vector2(900f, 46f));

            var legend = UiFactory.Label(root, "Legend",
                                         "Fight      Elite — harder, better reward      "
                                         + "Rest — heal or upgrade      Treasure — free card", 18, Palette.InkMuted);
            UiFactory.Place(legend.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -86f), new Vector2(1200f, 28f));

            _board = UiFactory.Panel(root, "Board", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_board, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 70f), new Vector2(1200f, 820f));

            gameObject.SetActive(false);
        }

        public void Show(RunMap map)
        {
            _map = map;
            Redraw();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        void Redraw()
        {
            foreach (var item in _drawn)
                if (item != null) { item.transform.SetParent(null, false); Destroy(item); }
            _drawn.Clear();

            // Edges first so no line is drawn over a node's face.
            foreach (var row in _map.Grid)
                foreach (var node in row)
                    foreach (var next in node.Next)
                        DrawEdge(node, next);

            foreach (var row in _map.Grid)
                foreach (var node in row)
                    DrawNode(node);

            DrawNode(_map.Boss);
        }

        Vector2 PositionOf(MapNode node)
        {
            float columnOffset = (node.Column - (RunMap.Columns - 1) * 0.5f) * ColumnSpacing;
            return new Vector2(columnOffset, node.Row * RowSpacing);
        }

        void DrawEdge(MapNode from, MapNode to)
        {
            Vector2 a = PositionOf(from), b = PositionOf(to);
            Vector2 delta = b - a;

            bool travelled = from.Visited && to.Visited;
            var line = UiFactory.Panel(_board, "Edge",
                                       travelled ? Palette.Energy : new Color(0.30f, 0.28f, 0.36f, 1f));
            UiFactory.Place(line, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                            a + delta * 0.5f, new Vector2(delta.magnitude, travelled ? 4f : 2.5f));
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            _drawn.Add(line.gameObject);
        }

        void DrawNode(MapNode node)
        {
            bool available = _map.IsAvailable(node);
            float size = node.Type == NodeType.Boss ? NodeSize * 1.45f : NodeSize;

            var panel = UiFactory.Panel(_board, $"Node_{node.Row}_{node.Column}", NodeColor(node, available));
            UiFactory.Place(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                            PositionOf(node), new Vector2(size, size));
            _drawn.Add(panel.gameObject);

            var glyph = UiFactory.Label(panel, "Glyph", Glyph(node.Type),
                                        node.Type == NodeType.Boss ? 30 : 22,
                                        available || node.Visited ? Palette.Background : Palette.InkMuted);
            UiFactory.Stretch(glyph.rectTransform);

            if (!available) return;

            // Only reachable nodes take clicks. An unreachable node that highlights on hover
            // and then does nothing is worse than one that plainly cannot be pressed.
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            var chosen = node;
            button.onClick.AddListener(() => NodeChosen?.Invoke(chosen));
        }

        static Color NodeColor(MapNode node, bool available)
        {
            var baseColour = node.Type switch
            {
                NodeType.Elite    => Palette.RarityRare,
                NodeType.Rest     => Palette.Victory,
                NodeType.Treasure => Palette.RarityUncommon,
                NodeType.Boss     => Palette.Defeat,
                _                 => Palette.InkMuted,
            };

            if (node.Visited) return baseColour * 0.45f;
            return available ? baseColour : baseColour * 0.28f;
        }

        static string Glyph(NodeType type) => type switch
        {
            NodeType.Fight    => "X",
            NodeType.Elite    => "XX",
            NodeType.Rest     => "+",
            NodeType.Treasure => "$",
            NodeType.Boss     => "!!",
            _                 => "?",
        };
    }
}
