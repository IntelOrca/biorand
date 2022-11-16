using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard
{
    internal class DoorRandomiser
    {
        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private Map _map = new Map();
        private Dictionary<RdtId, PlayNode> _nodeMap = new Dictionary<RdtId, PlayNode>();
        private PlayNode[] _allNodes = new PlayNode[0];
        private Rng _rng;
        private bool _debugLogging = false;

        private int _keyItemSpotsLeft;
        private int _keyItemRequiredCount;
        private int _numUnconnectedEdges;
        private int _numKeyEdges;
        private int _numUnlockedEdges;
        private bool _boxRoomReached;
        private byte _lockId = 145;
        private List<PlayNode> _nodesLeft = new List<PlayNode>();

        public DoorRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng random)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = random;
        }

        public PlayGraph CreateOriginalGraph()
        {
            _logger.WriteHeading("Creating Original Room Graph:");

            // Create all nodes
            foreach (var kvp in _map.Rooms!)
            {
                GetOrCreateNode(RdtId.Parse(kvp.Key));
            }

            var graph = new PlayGraph();
            var beginEnd = GetBeginEnd();
            graph.Start = GetOrCreateNode(RdtId.Parse(beginEnd.Start!));
            graph.End = GetOrCreateNode(RdtId.Parse(beginEnd.End!));

            var queue = new Queue<PlayNode>();
            queue.Enqueue(graph.Start);
            graph.Start.Visited = true;
            while (!graph.End.Visited)
            {
                var node = queue.Dequeue();
                foreach (var edge in node.Edges)
                {
                    var edgeNode = edge.Node;
                    if (edgeNode != null)
                    {
                        if (!edgeNode.Visited)
                        {
                            edgeNode.Visited = true;
                            edgeNode.Depth = node.Depth + 1;
                            queue.Enqueue(edgeNode);
                        }
                    }
                }
            }

            return graph;
        }

        public PlayGraph CreateRandomGraph()
        {
            _logger.WriteHeading("Creating Random Room Graph:");
            _allNodes = CreateNodes();

            // Create start and end
            var graph = new PlayGraph();
            var beginEnd = GetBeginEnd();
            graph.Start = GetOrCreateNode(RdtId.Parse(beginEnd.Start!));
            graph.End = GetOrCreateNode(RdtId.Parse(beginEnd.End!));

            var numAreas = _config.AreaCount + 1;

            _nodesLeft.AddRange(_allNodes);
            _nodesLeft.Remove(graph.Start);
            _nodesLeft.Remove(graph.End);
            var bridgeSuperNodes = GetBridgeSuperNodes();
            var areaSuperNodes = GetAreaSuperNodes(numAreas);

            var beginNode = graph.Start;
            beginNode.Visited = true;
            var pool = new List<PlayNode>() { beginNode };
            for (int i = 0; i < numAreas; i++)
            {
                pool.AddRange(areaSuperNodes[i]);
                var bridgeNode = i == numAreas - 1 ? graph.End : bridgeSuperNodes[i][0];
                _logger.WriteLine($"Creating area from {beginNode} to {bridgeNode}:");
                CreateArea(beginNode, bridgeNode, pool);
                beginNode = bridgeNode;
                if (i < numAreas - 1)
                {
                    pool.Clear();
                    pool.AddRange(bridgeSuperNodes[i]);
                }
            }
            FinishOffEndNodes(graph.End);

            FinalChecks(graph);
            return graph;
        }

        private MapStartEnd GetBeginEnd()
        {
            foreach (var startEnd in _map.BeginEndRooms!)
            {
                if (startEnd.Player != null && startEnd.Player != _config.Player)
                    continue;
                if (startEnd.Scenario != null && startEnd.Scenario != _config.Scenario)
                    continue;
                if (startEnd.DoorRando != null && startEnd.DoorRando != _config.RandomDoors)
                    continue;

                return startEnd;
            }
            throw new Exception("No begin / end room set.");
        }

        private PlayNode[] CreateNodes()
        {
            var nodes = new List<PlayNode>();
            foreach (var kvp in _map.Rooms!)
            {
                var node = GetOrCreateNode(RdtId.Parse(kvp.Key));
                if (node.Category == DoorRandoCategory.Exclude)
                    continue;

                var rdt = _gameData.GetRdt(node.RdtId)!;
                foreach (var offset in node.DoorRandoNop)
                {
                    rdt.Nop(offset);
                    _logger.WriteLine($"{rdt.RdtId} (0x{offset:X2}) opcode removed");
                }

                foreach (var edge in node.Edges)
                {
                    edge.Node = null;
                    edge.NoReturn = false;
                }
                nodes.Add(node);
            }
            return nodes.ToArray();
        }

        private void CreateArea(PlayNode begin, PlayNode end, List<PlayNode> pool)
        {
            _boxRoomReached = false;
            _keyItemSpotsLeft = 0;
            _keyItemRequiredCount = 0;

            PlayEdge[] unfinishedEdges;
            do
            {
                CalculateEdgeCounts(pool);
                unfinishedEdges = GetUnfinishedEdges(pool);

                if (_debugLogging)
                    _logger.WriteLine($"        Edges left: {_numUnconnectedEdges} (key = {_numKeyEdges}, unlocked = {_numUnlockedEdges})");
            } while (ConnectUpRandomNode(unfinishedEdges, pool));
            if (!ConnectUpNode(end, unfinishedEdges))
            {
                _logger.WriteLine($"    Failed to connect to end node {end.RdtId}");
                throw new Exception("Unable to connect end node");
            }

            foreach (var edge in end.Edges)
            {
                if (edge.Node == null)
                    edge.NoReturn = true;
                else
                    edge.Lock = LockKind.Always;
            }

            pool.RemoveAll(x => x.Visited);
        }

        private void FinalChecks(PlayGraph graph)
        {
            foreach (var node in _allNodes)
            {
                if (!node.Visited)
                    return;

                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null)
                    {
                        _logger.WriteLine($"{node.RdtId}:{edge.DoorId} -> null");

                        // Connect door back to itself
                        if (edge.DoorId != null && edge.Entrance != null)
                        {
                            ConnectDoor(edge, edge);
                        }
                    }
                }
            }
            foreach (var node in _allNodes)
            {
                if (!node.Visited)
                {
                    _logger.WriteLine($"{node.RdtId} not used");
                }
            }

            if (!graph.End!.Visited)
            {
                throw new Exception("End not reached");
            }
        }

        private List<PlayNode>[] GetBridgeSuperNodes()
        {
            var bridgeSuperNodes = new List<List<PlayNode>>();
            var bridgeNodes = _nodesLeft.Where(x => x.Category == DoorRandoCategory.Bridge).Shuffle(_rng);
            foreach (var bridgeNode in bridgeNodes)
            {
                var bridgeSuperNode = new List<PlayNode>();
                AddStickyNodeGroup(bridgeNode, bridgeSuperNode);
                _nodesLeft.RemoveMany(bridgeSuperNode);
                bridgeSuperNodes.Add(bridgeSuperNode);
            }
            return bridgeSuperNodes.Shuffle(_rng).ToArray();
        }

        private PlayNode[][] GetAreaSuperNodes(int count)
        {
            var superNodes = Enumerable.Range(0, count).Select(x => new List<PlayNode>()).ToArray();
            var superNodeIndex = _rng.Next(0, count);

            var boxNodes = _nodesLeft.Where(x => x.Category == DoorRandoCategory.Box).Shuffle(_rng).ToList();
            if (_config.AreaSize < 7)
            {
                var minNodes = _config.AreaCount + 1;
                var numNodes = minNodes + ((boxNodes.Count - minNodes) * _config.AreaSize / 7);
                boxNodes.RemoveRange(numNodes, boxNodes.Count - numNodes);
            }
            while (boxNodes.Count > 0)
            {
                var node = boxNodes[0];
                var superNode = superNodes[superNodeIndex];
                AddStickyNodeGroup(node, superNode);
                _nodesLeft.RemoveMany(superNode);
                boxNodes.RemoveMany(superNode);
                superNodeIndex = (superNodeIndex + 1) % superNodes.Length;
            }

            // Divide up nodes of same edge count equally
            _nodesLeft = _nodesLeft.Shuffle(_rng).ToList();
            if (_config.AreaSize < 7)
            {
                var numNodes = (_nodesLeft.Count * (_config.AreaSize + 1)) / 8;
                _nodesLeft.RemoveRange(numNodes, _nodesLeft.Count - numNodes);
            }
            foreach (var subg in _nodesLeft.GroupBy(x => x.Edges.Count))
            {
                var subgNodes = subg.Intersect(_nodesLeft).Shuffle(_rng).ToList();
                while (subgNodes.Count > 0)
                {
                    var node = subgNodes[0];
                    var superNode = superNodes[superNodeIndex];
                    AddStickyNodeGroup(node, superNode);
                    _nodesLeft.RemoveMany(superNode);
                    subgNodes.RemoveMany(superNode);
                    superNodeIndex = (superNodeIndex + 1) % superNodes.Length;
                }
            }

            return superNodes.Select(x => x.ToArray()).ToArray();
        }

        private void AddStickyNodeGroup(PlayNode node, List<PlayNode> list)
        {
            if (!list.Contains(node))
                list.Add(node);

            // Find all nodes this room requires
            foreach (var edge in node.Edges)
            {
                if (!edge.Randomize)
                {
                    var stickyNode = GetOrCreateNode(edge.OriginalTargetRdt);
                    if (!list.Contains(stickyNode))
                    {
                        list.Add(stickyNode);
                        AddStickyNodeGroup(stickyNode, list);
                    }
                }
                foreach (var reqRoom in edge.RequiresRoom)
                {
                    if (!list.Contains(reqRoom))
                    {
                        list.Add(reqRoom);
                        AddStickyNodeGroup(reqRoom, list);
                    }
                }
            }

            // Find all other nodes that require this node
            foreach (var node2 in _nodesLeft)
            {
                foreach (var edge in node2.Edges)
                {
                    foreach (var reqRoom in edge.RequiresRoom)
                    {
                        if (reqRoom == node)
                        {
                            if (!list.Contains(node2))
                            {
                                list.Add(node2);
                                AddStickyNodeGroup(node2, list);
                            }
                        }
                    }
                }
            }
        }

        private bool ConnectUpRandomNode(IEnumerable<PlayEdge> unfinishedEdges, IEnumerable<PlayNode> availableExitNodes)
        {
            var strictConstraints = new ConnectConstraint[]
            {
                new LockConstraint(),
                new FixedLinkConstraint(),
                new LoopbackConstraint(),
                new LeafConstraint(),
                new KeyConstraint(),
                new BoxConstraint()
            };
            var looseConstraints = new ConnectConstraint[]
            {
                new LockConstraint(),
                new FixedLinkConstraint(),
                new LoopbackConstraint(),
                new LeafConstraint(),
                new KeyConstraint()
            };
            if (ConnectUpRandomNode(strictConstraints, unfinishedEdges, availableExitNodes))
                return true;
            return ConnectUpRandomNode(looseConstraints, unfinishedEdges, availableExitNodes);
        }

        private bool ConnectUpRandomNode(IEnumerable<ConnectConstraint> constraints, IEnumerable<PlayEdge> unfinishedEdges, IEnumerable<PlayNode> availableExitNodes)
        {
            foreach (var entrance in unfinishedEdges)
            {
                var exit = GetRandomRoom(constraints, entrance, availableExitNodes);
                if (exit != null)
                {
                    ConnectEdges(entrance, exit);
                    return true;
                }
            }
            return false;
        }

        private bool ConnectUpNode(PlayNode endNode, PlayEdge[] unfinishedEdges)
        {
            var endConstraints = new ConnectConstraint[]
            {
                new LockConstraint(),
                new FixedLinkConstraint()
            };
            foreach (var exit in endNode.Edges.Shuffle(_rng))
            {
                foreach (var entrance in unfinishedEdges)
                {
                    if (ValidateConnection(endConstraints, entrance, exit))
                    {
                        ConnectEdges(entrance, exit);
                        return true;
                    }
                }
            }
            return false;
        }

        private PlayEdge[] GetUnfinishedEdges(IEnumerable<PlayNode> nodes)
        {
            var unfinishedEdges = nodes
                .Where(x => x.Visited)
                .SelectMany(x => x.Edges)
                .Where(x => x.Node == null && IsAccessible(x))
                .OrderBy(x => x.Requires.Length != 0 ? 0 : 1)
                // .ThenBy(x => x.Parent.Depth)
                .ToArray();
            return unfinishedEdges;
        }

        private void ConnectEdges(PlayEdge entrance, PlayEdge exit)
        {
            var exitNode = exit.Parent;
            var loopback = exitNode.Visited;
            if (!loopback)
            {
                exitNode.Visited = true;
                if (entrance.Requires.Length != 0)
                {
                    exitNode.DoorRandoAllRequiredItems = entrance.Parent.DoorRandoAllRequiredItems
                        .Union(entrance.Requires)
                        .ToArray();
                }
                else
                {
                    exitNode.DoorRandoAllRequiredItems = entrance.Parent.DoorRandoAllRequiredItems;
                }
                exitNode.Depth = entrance.Parent.Depth + 1;

                if (exitNode.Category == DoorRandoCategory.Box)
                {
                    _boxRoomReached = true;
                }

                _keyItemSpotsLeft += exitNode.Items.Count(x => x.Priority == ItemPriority.Normal && (x.Requires?.Length ?? 0) == 0);
                _keyItemRequiredCount += exitNode.Edges.Sum(x => x.Requires.Length);
                _keyItemRequiredCount += exitNode.Requires.Length;
            }

            ConnectDoor(entrance, exit, loopback);
        }

        private void ConnectDoor(PlayEdge aEdge, PlayEdge bEdge, bool oneWay = false)
        {
            var a = aEdge.Parent;
            var b = bEdge.Parent;
            aEdge.Node = b;
            bEdge.Node = a;

            if (a.Category != DoorRandoCategory.Static)
            {
                var aDoorId = aEdge.DoorId!.Value;
                var aRdt = _gameData.GetRdt(a.RdtId)!;
                aRdt.SetDoorTarget(aDoorId, b.RdtId, bEdge.Entrance!.Value, aEdge.OriginalTargetRdt);
                aRdt.RemoveDoorUnlock(aDoorId);
                if (aEdge == bEdge)
                {
                    aRdt.AddDoorLock(aDoorId, 255);
                }
                else if (aEdge.Lock == LockKind.Side)
                {
                    aEdge.Lock = LockKind.None;
                    aRdt.RemoveDoorLock(aDoorId);
                }
                if (oneWay)
                {
                    aRdt.AddDoorUnlock(aDoorId, _lockId);
                }
            }

            if (aEdge != bEdge && b.Category != DoorRandoCategory.Static && bEdge.Lock != LockKind.Always)
            {
                var bDoorId = bEdge.DoorId!.Value;
                var bRdt = _gameData.GetRdt(b.RdtId)!;
                if (aEdge.Entrance == null)
                {
                    bRdt.SetDoorTarget(bDoorId, b.RdtId, bEdge.Entrance!.Value, bEdge.OriginalTargetRdt);
                    bRdt.AddDoorLock(bDoorId, 255);
                }
                else
                {
                    bRdt.SetDoorTarget(bDoorId, a.RdtId, aEdge.Entrance.Value, bEdge.OriginalTargetRdt);
                    bRdt.RemoveDoorUnlock(bDoorId);
                    if (oneWay)
                    {
                        bEdge.Lock = LockKind.Side;
                        bRdt.RemoveDoorLock(bDoorId);
                        bRdt.AddDoorLock(bDoorId, _lockId);
                    }
                    else if (bEdge.Lock == LockKind.Side && b.Category != DoorRandoCategory.Static)
                    {
                        bEdge.Lock = LockKind.None;
                        bRdt.RemoveDoorLock(bDoorId);
                    }
                }
            }

            if (oneWay)
            {
                _lockId++;
            }

            _logger.WriteLine($"    Connected {GetEdgeString(a, aEdge)} to {GetEdgeString(b, bEdge)}");
        }

        private static string GetEdgeString(PlayNode node, PlayEdge edge)
        {
            var rs = edge.RequiresString;
            var s = $"{node.RdtId}:{edge.DoorId}";
            if (string.IsNullOrEmpty(rs))
                return s;
            else
                return $"{s} {rs}";
        }

        private static bool ValidateEntranceNodeForCounting(PlayEdge edge)
        {
            // Already connected
            if (edge.Node != null)
                return false;

            // Do not connect a blocked edge up
            if (edge.Lock == LockKind.Always)
                return false;

            // Do not connect a gate edge up, other side should be done first
            if (edge.Lock == LockKind.Gate)
                return false;

            // Do not connect this node up yet until we have visited all required rooms
            if (!edge.RequiresRoom.All(x => x.Visited))
                return false;

            // Edges that can't be randomized are not really free
            if (!edge.Randomize)
                return false;

            return true;
        }

        private static int GetFreeUnlockedEdgesForRoom(PlayNode node, PlayEdge excludeEdge)
        {
            return node.Edges.Count(x => x != excludeEdge && x.Node == null && x.Randomize && x.Requires.Length == 0 && IsAccessible(x));
        }

        private static bool IsAccessible(PlayEdge edge)
        {
            return edge.Lock == LockKind.None || edge.Lock == LockKind.Side || edge.Lock == LockKind.Unblock;
        }

        private PlayEdge? GetRandomRoom(IEnumerable<ConnectConstraint> constraints, PlayEdge entrance, IEnumerable<PlayNode> availableExitNodes)
        {
            var pool = new List<PlayEdge>();
            foreach (var exitNode in availableExitNodes)
            {
                foreach (var exitEdge in exitNode.Edges)
                {
                    if (ValidateConnection(constraints, entrance, exitEdge))
                    {
                        pool.Add(exitEdge);
                    }
                }
            }

            if (pool.Count == 0)
                return null;

            var index = _rng.Next(0, pool.Count);
            return pool[index];
        }

        private bool ValidateConnection(IEnumerable<ConnectConstraint> constraints, PlayEdge entrance, PlayEdge exit)
        {
            // Same node
            if (entrance.Parent == exit.Parent)
                return false;

            // Already connected
            if (entrance.Node != null || exit.Node != null)
                return false;

            foreach (var constraint in constraints)
            {
                if (!constraint.Validate(this, entrance, exit))
                {
                    return false;
                }
            }
            return true;
        }

        private void CalculateEdgeCounts(IEnumerable<PlayNode> nodes)
        {
            _numUnconnectedEdges = 0;
            _numUnlockedEdges = 0;
            _numKeyEdges = 0;

            foreach (var node in nodes)
            {
                if (!node.Visited)
                    continue;

                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null && edge.Lock != LockKind.Always && edge.Randomize)
                    {
                        _numUnconnectedEdges++;
                        if (ValidateEntranceNodeForCounting(edge))
                        {
                            if (edge.Requires.Length != 0)
                                _numKeyEdges++;
                            else
                                _numUnlockedEdges++;
                        }
                    }
                }
            }
        }

        private PlayNode GetOrCreateNode(RdtId rdtId)
        {
            var node = FindNode(rdtId);
            if (node != null)
                return node;

            var rdt = _gameData.GetRdt(rdtId);
            var items = rdt!.EnumerateOpcodes<IItemAotSetOpcode>(_config)
                .DistinctBy(x => x.Id)
                .Where(x => _config.IncludeDocuments || x.Type <= (ushort)ItemType.PlatformKey)
                .Select(x => new ItemPoolEntry()
                {
                    RdtId = rdt.RdtId,
                    Id = x.Id,
                    Type = x.Type,
                    Amount = x.Amount
                })
                .ToArray();

            node = new PlayNode(rdtId);
            node.Items = items;
            _nodeMap.Add(rdtId, node);

            var mapRoom = _map.GetRoom(rdtId);
            if (mapRoom == null)
                throw new Exception("No JSON definition for room");

            node.Requires = mapRoom.Requires ?? Array.Empty<ushort>();

            if (mapRoom.Doors != null)
            {
                foreach (var door in mapRoom.Doors)
                {
                    if (door.Player != null && door.Player != _config.Player)
                        continue;
                    if (door.Scenario != null && door.Scenario != _config.Scenario)
                        continue;
                    if (door.DoorRando != null && door.DoorRando != _config.RandomDoors)
                        continue;

                    DoorEntrance? entrance = null;

                    Rdt targetRdt;
                    IDoorAotSetOpcode? targetExit;
                    if (door.Target!.Contains(":"))
                    {
                        var target = RdtItemId.Parse(door.Target!);
                        targetRdt = _gameData.GetRdt(target.Rdt)!;
                        targetExit = targetRdt.Doors.First(x => x.Id == target.Id);
                    }
                    else
                    {
                        var target = RdtId.Parse(door.Target!);
                        targetRdt = _gameData.GetRdt(target)!;
                        targetExit = targetRdt.Doors.FirstOrDefault(x => x.Target == rdtId);
                    }
                    var doorId = door.Id ?? rdt.Doors.FirstOrDefault(x => x.Target == targetRdt.RdtId)?.Id;
                    if (targetExit != null)
                    {
                        entrance = DoorEntrance.FromOpcode(targetExit);
                    }

                    var edgeNode = GetOrCreateNode(targetRdt.RdtId);
                    var edge = new PlayEdge(node, edgeNode, door.NoReturn, door.Requires, doorId, entrance);
                    edge.Randomize = door.Randomize ?? true;
                    if (door.Lock != null)
                    {
                        edge.Lock = (LockKind)Enum.Parse(typeof(LockKind), door.Lock, true);
                    }
                    if (door.RequiresRoom != null)
                    {
                        edge.RequiresRoom = door.RequiresRoom.Select(x => GetOrCreateNode(RdtId.Parse(x))).ToArray();
                    }
                    node.Edges.Add(edge);
                }
            }

            if (mapRoom.Items != null)
            {
                foreach (var correctedItem in mapRoom.Items)
                {
                    if (correctedItem.Player != null && correctedItem.Player != _config.Player)
                        continue;
                    if (correctedItem.Scenario != null && correctedItem.Scenario != _config.Scenario)
                        continue;
                    if (correctedItem.DoorRando != null && correctedItem.DoorRando != _config.RandomDoors)
                        continue;

                    // HACK 255 is used for item get commands
                    if (correctedItem.Id == 255)
                    {
                        items = items.Concat(new[] {
                                new ItemPoolEntry() {
                                    RdtId = rdtId,
                                    Id = correctedItem.Id,
                                    Type = (ushort)(correctedItem.Type ?? 0),
                                    Amount = correctedItem.Amount ?? 1,
                                    Requires = correctedItem.Requires,
                                    Priority = ParsePriority(correctedItem.Priority)
                                }
                            }).ToArray();
                        continue;
                    }

                    var idx = Array.FindIndex(items, x => x.Id == correctedItem.Id);
                    if (idx != -1)
                    {
                        if (correctedItem.Link == null)
                        {
                            items[idx].Type = (ushort)(correctedItem.Type ?? items[idx].Type);
                            items[idx].Amount = correctedItem.Amount ?? items[idx].Amount;
                        }
                        else
                        {
                            items[idx].Type = 0;

                            var rdtItemId = RdtItemId.Parse(correctedItem.Link);
                            node.LinkedItems.Add(correctedItem.Id, rdtItemId);
                        }
                        items[idx].Requires = correctedItem.Requires;
                        items[idx].Priority = ParsePriority(correctedItem.Priority);
                    }
                }

                // Remove any items that have no type (removed fixed items)
                node.Items = items.Where(x => x.Type != 0).ToArray();
            }

            if (mapRoom.DoorRando != null)
            {
                foreach (var spec in mapRoom.DoorRando)
                {
                    if (spec.Player != null && spec.Player != _config.Player)
                        continue;
                    if (spec.Scenario != null && spec.Scenario != _config.Scenario)
                        continue;

                    if (spec.Category != null)
                    {
                        node.Category = (DoorRandoCategory)Enum.Parse(typeof(DoorRandoCategory), spec.Category, true);
                    }
                    if (spec.Nop != null)
                    {
                        node.DoorRandoNop = spec.Nop;
                    }
                }
            }

            return node;
        }

        private void FinishOffEndNodes(PlayNode endNode)
        {
            foreach (var edge in endNode.Edges)
            {
                edge.NoReturn = false;
            }

            var stack = new Stack<PlayNode>();
            stack.Push(endNode);
            while (stack.Count != 0)
            {
                var node = stack.Pop();
                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null && !edge.Randomize)
                    {
                        edge.Node = GetOrCreateNode(edge.OriginalTargetRdt);
                        if (!edge.Node.Visited)
                        {
                            edge.Node.Visited = true;
                            edge.Node.Depth = node.Depth + 1;
                            stack.Push(edge.Node);
                        }
                    }
                }
            }
        }

        private static ItemPriority ParsePriority(string? s)
        {
            if (s == null)
                return ItemPriority.Normal;
            return (ItemPriority)Enum.Parse(typeof(ItemPriority), s, true);
        }

        public PlayNode? FindNode(RdtId rdtId)
        {
            if (_nodeMap.TryGetValue(rdtId, out var node))
                return node;
            return null;
        }

        private abstract class ConnectConstraint
        {
            public virtual bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                return true;
            }
        }

        private class LockConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                // Do not connect a blocked edge up
                if (entrance.Lock == LockKind.Always)
                    return false;

                // Do not connect a gate edge up, other side should be done first (e.g. bottom of ladder in main hall)
                if (entrance.Lock == LockKind.Gate)
                    return false;

                // Ignore rest of checks if this is a fixed edge
                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Do not connect this node up yet until we have visited all required rooms (e.g. armory)
                if (!entrance.RequiresRoom.All(x => x.Visited))
                    return false;

                // Exit node must have an entrance
                if (exit.Entrance == null)
                    return false;

                // For now, ignore any rooms with one way door setups
                // if (exit.Parent.Edges.Any(x => x.Entrance == null && x.Randomize))
                //     return false;

                // Don't connect to a door with a key requirement on the other side
                if (exit.Requires.Length != 0)
                    return false;

                // HACK need to connect to one-way door
                if (exit.Parent.RdtId.Stage == 6 && exit.Parent.RdtId.Room == 0)
                    return true;

                // Don't connect to a door that is blocked / one way
                if (exit.Lock == LockKind.Always || exit.Lock == LockKind.Unblock)
                    return false;

                return true;
            }
        }

        private class FixedLinkConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                if (!entrance.Randomize || !exit.Randomize)
                    return entrance.OriginalTargetRdt == exit.Parent.RdtId && exit.OriginalTargetRdt == entrance.Parent.RdtId;
                return true;
            }
        }

        private class LoopbackConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                if (!exit.Parent.Visited)
                    return true;

                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Do not connect back to a room that already connects to this room
                if (exit.Parent.Edges.Any(x => x.Node == entrance.Parent))
                    return false;

                // Do not loopback until all unvisited single edge rooms are visited
                // if (!dr._nodes.Any(x => !x.Visited && GetFreeEdgesForRoom(x) == 1))
                //     return false;

                // Check if this is a compatible loopback route (i.e. has the same tokens, no different ones)
                // var outliers = node.DoorRandoRouteTokens.UnionExcept(exitNode.DoorRandoRouteTokens).Count();
                var outliers = exit.Parent.DoorRandoAllRequiredItems.Except(entrance.Parent.DoorRandoAllRequiredItems).Count();
                if (outliers != 0)
                    return false;

                // Seen room before, check if we have another spare unconnected edge
                var remainingEdges = dr._numUnlockedEdges + dr._numKeyEdges - 1;
                return remainingEdges > 1;
            }
        }

        private class LeafConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                var extraEdges = exit.Parent.Edges.Count(x => x != exit && ValidateEntranceNodeForCounting(x));
                var remainingEdges = dr._numUnlockedEdges + dr._numKeyEdges - 1;
                if (remainingEdges == 0 && extraEdges == 0)
                    return false;

                return true;
            }
        }

        private class KeyConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                var numRequiredKeys = dr._keyItemRequiredCount;
                var numKeySlots = dr._keyItemSpotsLeft;
                var unlockedEdges = dr._numUnlockedEdges;
                if (entrance.Requires.Length == 0)
                {
                    unlockedEdges--;
                }
                else if (numKeySlots < numRequiredKeys)
                {
                    // Do not extend past a key door until we our rich in item pickups
                    return false;
                }

                // Still a key free edge left
                if (unlockedEdges > 0)
                    return true;

                var extraEdges = exit.Parent.Edges.Where(x => x != exit && IsAccessible(x)).ToArray();
                var extraItems = exit.Parent.Items.Count(x => x.Priority == ItemPriority.Normal && (x.Requires?.Length ?? 0) == 0);
                if (extraEdges.Length != 0)
                {
                    var minKeyReq = extraEdges.Min(x => x.Requires.Length);

                    // Still a key free edge left
                    if (minKeyReq == 0)
                        return true;
                }

                // Make sure we have more items to pick up than required key items
                var totalKeyReq = extraEdges.Sum(x => x.Requires.Length) + exit.Requires.Length + exit.Parent.Items.Sum(x => x.Requires?.Length ?? 0);
                if (numKeySlots + extraItems >= numRequiredKeys + totalKeyReq)
                    return true;

                return false;
            }
        }

        private class BoxConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                if (dr._boxRoomReached)
                    return true;

                // Do not extend graph via required key edges until a box room is accessible
                if (entrance.Parent.Category != DoorRandoCategory.Bridge && entrance.Requires.Length != 0)
                    return false;

                if (exit.Parent.Category == DoorRandoCategory.Box)
                    return true;

                // Make sure we still have a free unlocked edge for the box room
                var extraUnlockedEdges = GetFreeUnlockedEdgesForRoom(exit.Parent, exit);
                if (dr._numUnlockedEdges <= 1 && extraUnlockedEdges == 0)
                    return false;

                return false;
            }
        }
    }
}
