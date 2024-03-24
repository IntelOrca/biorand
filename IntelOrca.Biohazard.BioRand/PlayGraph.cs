using System.Collections.Generic;
using System.Linq;
using OpenSoftware.DgmlTools;
using OpenSoftware.DgmlTools.Builders;
using OpenSoftware.DgmlTools.Model;

namespace IntelOrca.Biohazard.BioRand
{
    internal class PlayGraph
    {
        public PlayNode? Start { get; set; }
        public PlayNode? End { get; set; }

        public void GenerateDgml(string path, IItemHelper itemHelper)
        {
            var builder = new DgmlBuilder()
            {
                NodeBuilders = new[]
                {
                    new NodeBuilder<PlayNode>(n => GetNode(n, itemHelper))
                },
                LinkBuilders = new LinkBuilder[]
                {
                    new LinksBuilder<PlayNode>(n => GetLinksForNode(n, itemHelper))
                },
                CategoryBuilders = new CategoryBuilder[0],
                StyleBuilders = new StyleBuilder[0]
            };
            var dgmlGraph = builder.Build(GetAllNodes());
            dgmlGraph.WriteToFile(path);
        }

        private Node GetNode(PlayNode node, IItemHelper itemHelper)
        {
            var label = node.RdtId.ToString();
            foreach (var keyItem in node.PlacedKeyItems)
            {
                label += "\n" + itemHelper.GetItemName((byte)keyItem.Type) + " x" + keyItem.Amount;
            }
            var itemsLeft = node.Items.Length - node.PlacedKeyItems.Count;
            if (itemsLeft > 0)
            {
                var potentialKeyItems = node.Items.Count(x => x.Priority == ItemPriority.Normal && (x.Requires?.Length ?? 0) == 0) - node.PlacedKeyItems.Count;
                label += $"\n{potentialKeyItems}/{node.Items.Length - node.PlacedKeyItems.Count} other items";
            }

            var result = new Node()
            {
                Id = node.RdtId.ToString(),
                Label = label
            };

            if (node == Start || node == End)
                result.Properties["Background"] = "LightBlue";
            if (node.Category == DoorRandoCategory.Box)
                result.Properties["Background"] = "Green";
            if (node.Category == DoorRandoCategory.Bridge)
                result.Properties["Background"] = "Orange";
            if (node.Category == DoorRandoCategory.Segment)
                result.Properties["Background"] = "Red";
            return result;
        }

        private List<Link> GetLinksForNode(PlayNode node, IItemHelper itemHelper)
        {
            var links = new List<Link>();
            foreach (var edge in node.Edges)
            {
                if (!IsLinkAccessible(edge))
                    continue;

                var oppositeEdge = edge.Node!.Edges.FirstOrDefault(x => x.Node == node);
                if (oppositeEdge != null && IsLinkAccessible(oppositeEdge) && edge.Node.Depth < node.Depth)
                    continue;

                var label = edge.DoorId.ToString();
                if (edge.NoReturn)
                {
                    label += $"\n(no return)";
                }
                if (edge.Lock == LockKind.None)
                {
                    if (oppositeEdge != null && oppositeEdge.Lock == LockKind.Side)
                    {
                        label += $"\n(locked)";
                    }
                }
                else
                {
                    label += $"\n({edge.Lock.ToString().ToLower()})";
                }
                foreach (var req in edge.Requires)
                {
                    label += $"\n[{req.ToString(itemHelper)}]";
                }

                links.Add(new Link()
                {
                    Source = node.RdtId.ToString(),
                    Target = edge.Node.RdtId.ToString(),
                    Label = label
                });
            }
            return links;
        }

        private static bool IsLinkAccessible(PlayEdge edge)
        {
            if (edge.Node == null)
                return false;
            if (edge.Lock != LockKind.None && edge.Lock != LockKind.Unblock)
                return false;
            return true;
        }

        public PlayNode[] GetAllNodes()
        {
            var nodes = new HashSet<PlayNode>();
            if (Start != null)
                AddNodeAndEdges(nodes, Start);
            if (End != null)
                AddNodeAndEdges(nodes, End);
            return nodes.ToArray();
        }

        private void AddNodeAndEdges(HashSet<PlayNode> nodes, PlayNode node)
        {
            if (nodes.Add(node))
            {
                foreach (var edge in node.Edges)
                {
                    if (edge.Node != null)
                    {
                        AddNodeAndEdges(nodes, edge.Node);
                    }
                }
            }
        }

        public RandomizedRdt[] GetAccessibleRdts(GameData gameData)
        {
            if (Start == null)
                return gameData.Rdts;

            var visited = new HashSet<PlayNode>();
            var q = new Queue<PlayNode>();
            q.Enqueue(Start);
            while (q.Count > 0)
            {
                var node = q.Dequeue();
                if (visited.Add(node))
                {
                    foreach (var e in node.Edges)
                    {
                        if (e.Node != null)
                        {
                            q.Enqueue(e.Node);
                        }
                    }
                }
            }

            var result = visited
                .Select(x => gameData.GetRdt(x.RdtId)!)
                .ToHashSet();

            // Add linked RDTs as well
            foreach (var v in visited)
            {
                if (v.LinkedRdtId != null)
                {
                    result.Add(gameData.GetRdt(v.LinkedRdtId.Value)!);
                }
            }

            return result
                .OrderBy(x => x.RdtId)
                .ToArray();
        }
    }
}
