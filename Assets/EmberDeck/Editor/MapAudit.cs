using System.Collections.Generic;
using System.Text;
using EmberDeck.Core;
using EmberDeck.Run;
using UnityEditor;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Generates many maps and reports the shape of what comes out.
    ///
    /// A map is generated once per run and looked at for ten seconds, so a distribution bug
    /// — too few elites, a row with one node, a dead end — reads as bad luck rather than as
    /// a defect. Only the aggregate shows it.
    /// </summary>
    public static class MapAudit
    {
        [MenuItem("EmberDeck/Audit Map Generation")]
        public static void Audit()
        {
            const int samples = 500;
            var counts = new Dictionary<NodeType, int>();
            int deadEnds = 0, unreachable = 0, totalNodes = 0;
            int minRowWidth = int.MaxValue, maxRowWidth = 0;

            for (int i = 0; i < samples; i++)
            {
                var map = RunMap.Generate(new DeterministicRng(i));
                var reachable = new HashSet<MapNode>(map.Grid[0]);

                for (int row = 0; row < map.Grid.Count; row++)
                {
                    minRowWidth = Mathf.Min(minRowWidth, map.Grid[row].Count);
                    maxRowWidth = Mathf.Max(maxRowWidth, map.Grid[row].Count);

                    foreach (var node in map.Grid[row])
                    {
                        totalNodes++;
                        counts.TryGetValue(node.Type, out int n);
                        counts[node.Type] = n + 1;

                        if (node.Next.Count == 0) deadEnds++;
                        if (row > 0 && !reachable.Contains(node)) unreachable++;
                        foreach (var next in node.Next) reachable.Add(next);
                    }
                }
            }

            var report = new StringBuilder();
            report.AppendLine($"=== Map audit ({samples} maps, {totalNodes} nodes) ===");
            foreach (var pair in counts)
                report.AppendLine($"{pair.Key,-9} {100f * pair.Value / totalNodes,5:F1}%   ({pair.Value})");
            report.AppendLine();
            report.AppendLine($"row width   : {minRowWidth} to {maxRowWidth}");
            report.AppendLine($"dead ends   : {deadEnds}  (must be 0 — a dead end ends the run in a wall)");
            report.AppendLine($"unreachable : {unreachable}  (must be 0)");
            Debug.Log(report.ToString());
        }
    }
}
