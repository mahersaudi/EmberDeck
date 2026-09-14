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

            // A painted wasteland under the map, kept dark: the paths and nodes must stay the brightest
            // thing on screen. Without the painting, the flat background shows through as before.
            var backdrop = UiFactory.Panel(root, "MapBackdrop", new Color(0.45f, 0.45f, 0.5f, 1f));
            UiFactory.Stretch(backdrop);
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.raycastTarget = false;
            var mapArt = Resources.Load<Sprite>("Backgrounds/bg_map");
            if (mapArt != null) backdropImage.sprite = mapArt;
            else backdropImage.color = new Color(0f, 0f, 0f, 0f);

            _title = UiFactory.Label(root, "MapTitle", "CHOOSE YOUR PATH", 34, Palette.Ink);
            UiFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -40f), new Vector2(900f, 46f));

            var legend = UiFactory.Label(root, "Legend",
                                         "Fight      Elite — harder, better reward      "
                                         + "Rest — heal or upgrade      Treasure — card and gold      Shop — spend gold      ? — event", 18, Palette.InkMuted);
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

            // A coloured ring around a dark face holding the node's icon. Colour still says "elite" or
            // "rest" from across the map; the icon says it up close, where a letter only hinted.
            var panel = UiFactory.Panel(_board, $"Node_{node.Row}_{node.Column}", NodeColor(node, available));
            UiFactory.Place(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                            PositionOf(node), new Vector2(size, size));
            _drawn.Add(panel.gameObject);

            var face = UiFactory.Panel(panel, "Face", available ? Palette.PanelRaised : Palette.PanelDark);
            UiFactory.Stretch(face, 4f);
            face.GetComponent<Image>().raycastTarget = false;

            var sprite = Icons.Get(IconId(node.Type));
            if (sprite != null)
            {
                float iconSize = size * 0.8f;
                var icon = Icons.Create(face, "Icon", sprite, iconSize);
                UiFactory.Place((RectTransform)icon.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(iconSize, iconSize));
                // Reachable nodes at full strength; the rest recede, and visited ones grey out.
                icon.color = available ? Color.white
                           : node.Visited ? new Color(0.55f, 0.55f, 0.55f, 0.8f)
                           : new Color(1f, 1f, 1f, 0.3f);
            }
            else
            {
                var glyph = UiFactory.Label(face, "Glyph", Glyph(node.Type), node.Type == NodeType.Boss ? 30 : 22,
                                            available || node.Visited ? Palette.Ink : Palette.InkMuted);
                UiFactory.Stretch(glyph.rectTransform);
            }

            var type = node.Type;
            TooltipTrigger.Attach(panel.gameObject, () => new[] { new Tooltip.Entry(NodeTitle(type), NodeDescription(type), IconId(type)) });

            if (!available) return;

            // Only reachable nodes take clicks. An unreachable node that highlights on hover
            // and then does nothing is worse than one that plainly cannot be pressed.
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            var chosen = node;
            button.onClick.AddListener(() => NodeChosen?.Invoke(chosen));
            panel.gameObject.AddComponent<NodePulse>();
        }

        static Color NodeColor(MapNode node, bool available)
        {
            var baseColour = node.Type switch
            {
                NodeType.Elite    => Palette.RarityRare,
                NodeType.Rest     => Palette.Victory,
                NodeType.Treasure => Palette.RarityUncommon,
                NodeType.Shop     => Palette.Energy,
                NodeType.Event    => new Color(0.68f, 0.58f, 0.9f),
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
            NodeType.Shop     => "S",
            NodeType.Event    => "?",
            NodeType.Boss     => "!!",
            _                 => "?",
        };

        static string IconId(NodeType type) => type switch
        {
            NodeType.Fight    => "node_fight",
            NodeType.Elite    => "node_elite",
            NodeType.Rest     => "node_rest",
            NodeType.Treasure => "node_treasure",
            NodeType.Shop     => "node_shop",
            NodeType.Event    => "node_event",
            NodeType.Boss     => "node_boss",
            _                 => null,
        };

        static string NodeTitle(NodeType type) => type switch
        {
            NodeType.Fight    => "Fight",
            NodeType.Elite    => "Elite",
            NodeType.Rest     => "Rest site",
            NodeType.Treasure => "Treasure",
            NodeType.Shop     => "Shop",
            NodeType.Event    => "Event",
            NodeType.Boss     => "The Forge Tyrant",
            _                 => "?",
        };

        static string NodeDescription(NodeType type) => type switch
        {
            NodeType.Fight    => "A hallway fight. Win a card and gold, and sometimes a potion.",
            NodeType.Elite    => "A harder fight. Win a relic, a better card, more gold, and often a potion.",
            NodeType.Rest     => "Heal, or upgrade a card at the forge.",
            NodeType.Treasure => "A free card and some gold, with no fight.",
            NodeType.Shop     => "Spend gold on cards, a relic, potions, or removing a card.",
            NodeType.Event    => "Something unexpected, and a choice with a price.",
            NodeType.Boss     => "The end of the run.",
            _                 => "",
        };
    }
}
