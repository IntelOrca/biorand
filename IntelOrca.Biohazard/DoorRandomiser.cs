using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace IntelOrca.Biohazard
{
    internal class DoorRandomiser
    {
        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private Map _map = new Map();
        private List<PlayNode> _nodes = new List<PlayNode>();
        private Dictionary<RdtId, PlayNode> _nodeMap = new Dictionary<RdtId, PlayNode>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private PlayNode[] _allNodes = new PlayNode[0];
        private HashSet<PlayNode> _allVisitedRooms = new HashSet<PlayNode>();
        private Rng _rng;
        private bool _debugLogging = false;

        private PlayNode _endNode;
        private int _keyItemSpots;
        private int _numUnconnectedEdges;
        private int _numKeyEdges;
        private int _numUnlockedEdges;
        private int _tokenCount;
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
                _nodes.Add(GetOrCreateNode(RdtId.Parse(kvp.Key)));
            }

            var graph = new PlayGraph();
            if (_config.Scenario == 0)
            {
                graph.Start = GetOrCreateNode(RdtId.Parse(_map.StartA!));
                graph.End = GetOrCreateNode(RdtId.Parse(_map.EndA!));
            }
            else
            {
                graph.Start = GetOrCreateNode(RdtId.Parse(_map.StartB!));
                graph.End = GetOrCreateNode(RdtId.Parse(_map.EndB!));
            }
            return graph;
        }

        public PlayGraph CreateRandomGraph()
        {
            _logger.WriteHeading("Creating Random Room Graph:");

            // Create all nodes
            _nodes.Clear();
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
                _nodes.Add(node);
            }
            _allNodes = _nodes.ToArray();

            // Create start and end
            var graph = new PlayGraph();
            if (_config.Scenario == 0)
            {
                graph.Start = GetOrCreateNode(RdtId.Parse(_map.StartA!));
                graph.End = GetOrCreateNode(RdtId.Parse(_map.EndA!));
            }
            else
            {
                graph.Start = GetOrCreateNode(RdtId.Parse(_map.StartB!));
                graph.End = GetOrCreateNode(RdtId.Parse(_map.EndB!));
            }

            var numAreas = 3;

            _nodesLeft.AddRange(_nodes);
            _nodesLeft.Remove(graph.Start);
            _nodesLeft.Remove(graph.End);
            var bridgeSuperNodes = GetBridgeSuperNodes();
            var areaSuperNodes = GetAreaSuperNodes(numAreas);

            var beginNode = graph.Start;
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

            FinalChecks(graph);
            return graph;
        }

        private void CreateArea(PlayNode begin, PlayNode end, List<PlayNode> pool)
        {
            _nodes = pool;
            _visitedRooms.Clear();
            _visitedRooms.Add(begin);
            _allVisitedRooms.Add(begin);
            _endNode = end;
            while (!_visitedRooms.Contains(end))
            {
                ConnectEdges();
            }
        }

        private void FinalChecks(PlayGraph graph)
        {
            foreach (var node in _allVisitedRooms)
            {
                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null)
                    {
                        _logger.WriteLine($"{node.RdtId}:{edge.DoorId} -> null");
                    }
                }
            }
            foreach (var node in _allNodes)
            {
                if (!_allVisitedRooms.Contains(node))
                {
                    _logger.WriteLine($"{node.RdtId} not used");
                }
            }

            if (!_allVisitedRooms.Contains(graph.End))
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
            return bridgeSuperNodes.ToArray();
        }

        private PlayNode[][] GetAreaSuperNodes(int count)
        {
            var superNodes = Enumerable.Range(0, count).Select(x => new List<PlayNode>()).ToArray();
            var boxNodes = _nodesLeft.Where(x => x.Category == DoorRandoCategory.Box).Shuffle(_rng);

            var superNodeIndex = 0;
            foreach (var node in boxNodes)
            {
                var superNode = superNodes[superNodeIndex];
                AddStickyNodeGroup(node, superNode);
                _nodesLeft.RemoveMany(superNode);
                superNodeIndex = (superNodeIndex + 1) % superNodes.Length;
            }

            var remainingNodes = _nodesLeft.Shuffle(_rng);
            foreach (var node in remainingNodes)
            {
                var superNode = superNodes[superNodeIndex];
                AddStickyNodeGroup(node, superNode);
                _nodesLeft.RemoveMany(superNode);
                superNodeIndex = (superNodeIndex + 1) % superNodes.Length;
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
                                AddStickyNodeGroup(reqRoom, list);
                            }
                        }
                    }
                }
            }
        }

        private bool ConnectEdges()
        {
            var unfinishedNodes = _visitedRooms
                .Where(x => x.Edges.Any(y => y.Node == null && (y.Lock == LockKind.None || y.Lock == LockKind.Side || y.Lock == LockKind.Unblock)))
                .Shuffle(_rng)
                .ToArray();
            if (unfinishedNodes.Length == 0)
                return false;

            CalculateEdgeCounts();
            foreach (var node in unfinishedNodes)
            {
                foreach (var edge in node.Edges)
                {
                    if (ValidateEntranceNode(node, edge))
                    {
                        if (TryConnectDoor(node, edge))
                        {
                            return true;
                        }
                    }
                }
            }
            return true;
        }

        private bool TryConnectDoor(PlayNode node, PlayEdge edge)
        {
            var exitNodeEdge = GetRandomRoom(node, edge);
            if (exitNodeEdge == null)
            {
                exitNodeEdge = (_endNode, _endNode.Edges.First(x => x.Randomize));
                edge.NoReturn = true;
            }
            var (exitNode, exitEdge) = exitNodeEdge.Value;

            if (_visitedRooms.Add(exitNode))
            {
                _allVisitedRooms.Add(exitNode);
                if (edge.Requires.Length != 0)
                {
                    exitNode.DoorRandoRouteTokens = node.DoorRandoRouteTokens
                        .Append(_tokenCount)
                        .ToArray();
                    _tokenCount++;
                }
                else
                {
                    exitNode.DoorRandoRouteTokens = node.DoorRandoRouteTokens;
                }
            }
            _keyItemSpots += exitNode.Items.Count(x => x.Priority == ItemPriority.Normal && (x.Requires?.Length ?? 0) == 0);
            ConnectDoor(node, edge, exitNode, exitEdge);
            return true;
        }

        private void ConnectDoor(PlayNode a, PlayEdge aEdge, PlayNode b, PlayEdge bEdge)
        {
            aEdge.Node = b;
            bEdge.Node = a;

            if (a.Category != DoorRandoCategory.Static)
            {
                var aDoorId = aEdge.DoorId.Value;
                var aRdt = _gameData.GetRdt(a.RdtId)!;
                aRdt.SetDoorTarget(aDoorId, b.RdtId, bEdge.Entrance.Value, aEdge.OriginalTargetRdt);
                if (aEdge.Lock == LockKind.Side)
                {
                    aEdge.Lock = LockKind.None;
                    aRdt.RemoveDoorLock(aDoorId);
                }
            }

            if (b.Category != DoorRandoCategory.Static && bEdge.Lock != LockKind.Always)
            {
                var bDoorId = bEdge.DoorId.Value;
                var bRdt = _gameData.GetRdt(b.RdtId)!;
                bRdt.SetDoorTarget(bDoorId, a.RdtId, aEdge.Entrance.Value, bEdge.OriginalTargetRdt);
                if (bEdge.Lock == LockKind.Side && b.Category != DoorRandoCategory.Static)
                {
                    bEdge.Lock = LockKind.None;
                    bRdt.RemoveDoorLock(bDoorId);
                }
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

        private bool ValidateEntranceNode(PlayNode node, PlayEdge edge)
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
            if (!edge.RequiresRoom.All(x => _visitedRooms.Contains(x)))
                return false;

            return true;
        }

        private bool ValidateExitNode(PlayNode node, PlayEdge edge, PlayNode exitNode, PlayEdge exitEdge)
        {
            // Same node
            if (node == exitNode)
                return false;

            // Already connected
            if (exitEdge.Node != null)
                return false;

            // Just do stage 1** and 200 for now
            // if (exitNode.RdtId.Stage != 0 && !(exitNode.RdtId.Stage == 1 && exitNode.RdtId.Room == 0))
            //     return false;
            if (exitNode.Category == DoorRandoCategory.Exclude)
                return false;

            // Fixed node connection
            if (!edge.Randomize || !exitEdge.Randomize)
                return edge.OriginalTargetRdt == exitNode.RdtId && exitEdge.OriginalTargetRdt == node.RdtId;

            // We do not want to connect to the end node
            if (exitNode == _endNode)
                return false;

            // For now, ignore any rooms with one way door setups
            if (exitNode.Edges.Any(x => x.Entrance == null && x.Randomize))
                return false;

            // Don't connect to a door with a key requirement on the other side
            if (exitEdge.Requires.Length != 0)
                return false;

            // Don't connect to a door that is blocked / one way
            if (exitEdge.Lock == LockKind.Always || exitEdge.Lock == LockKind.Unblock)
                return false;

            var finishing = _visitedRooms.Contains(_endNode);
            var remainingEdges = _numUnlockedEdges + _numKeyEdges - 1;
            if (_visitedRooms.Contains(exitNode))
            {
                // Check if this is a compatible loopback route (i.e. has the same tokens, no different ones)
                var outliers = node.DoorRandoRouteTokens.UnionExcept(exitNode.DoorRandoRouteTokens).Count();
                if (outliers != 0)
                {
                    return false;
                }

                // Seen room before, check if we have another space unconnected edge
                return finishing || remainingEdges > 1;
            }

            var extraEdges = exitNode.Edges.Count(x => x.Node == null && (x.Lock == LockKind.None || x.Lock == LockKind.Side || x.Lock == LockKind.Unblock)) - 1;
            if (!finishing && remainingEdges == 0 && extraEdges == 0)
                return false;

            return true;
        }

        private (PlayNode, PlayEdge)? GetRandomRoom(PlayNode node, PlayEdge edge)
        {
            var pool = new List<(PlayNode, PlayEdge)>();
            foreach (var exitNode in _nodes)
            {
                foreach (var exitEdge in exitNode.Edges)
                {
                    if (ValidateExitNode(node, edge, exitNode, exitEdge))
                    {
                        pool.Add((exitNode, exitEdge));
                    }
                }
            }

            if (pool.Count == 0)
                return null;

            var index = _rng.Next(0, pool.Count);
            return pool[index];
        }

        private void CalculateEdgeCounts()
        {
            _numUnconnectedEdges = 0;
            _numUnlockedEdges = 0;
            _numKeyEdges = 0;

            foreach (var nodes in _visitedRooms)
            {
                foreach (var edge in nodes.Edges)
                {
                    if (edge.Node == null && edge.Lock != LockKind.Always && edge.Randomize)
                    {
                        _numUnconnectedEdges++;
                        if (ValidateEntranceNode(nodes, edge))
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
            var items = rdt!.EnumerateOpcodes<ItemAotSetOpcode>(_config)
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
                    var edge = new PlayEdge(edgeNode, door.NoReturn, door.Requires, doorId, entrance);
                    edge.Randomize = door.Randomize ?? true;
                    edge.PreventLoopback = door.PreventLoopback;
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
    }
}
