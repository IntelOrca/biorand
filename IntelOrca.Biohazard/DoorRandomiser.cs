﻿using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class DoorRandomiser
    {
        private static bool g_debugLogging = false;

        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private Map _map = new Map();
        private Dictionary<RdtId, PlayNode> _nodeMap = new Dictionary<RdtId, PlayNode>();
        private List<PlayNode> _allNodes = new List<PlayNode>();
        private Rng _rng;
        private IItemHelper _itemHelper;

        private int _keyItemSpotsLeft;
        private readonly HashSet<ushort> _keyItemRequiredSet = new HashSet<ushort>();
        private int _numUnconnectedEdges;
        private int _numKeyEdges;
        private int _numUnlockedEdges;
        private PlayEdge? _keyRichEdge;
        private int _keyRichEdgeScore;
        private bool _boxRoomReached;
        private byte _lockId = 128;
        private List<PlayNode> _nodesLeft = new List<PlayNode>();

        private static readonly ConnectConstraint[] g_strictConstraints = new ConnectConstraint[]
        {
            new LockConstraint(),
            new FixedLinkConstraint(),
            new LoopbackConstraint(),
            new LeafConstraint(),
            new KeyConstraint(),
            new BoxConstraint()
        };

        private static readonly ConnectConstraint[] g_looseConstraints = new ConnectConstraint[]
        {
            new LockConstraint(),
            new FixedLinkConstraint(),
            new LoopbackConstraint(),
            new LeafConstraint(),
            new KeyConstraint()
        };

        private static readonly ConnectConstraint[] g_endConstraints = new ConnectConstraint[]
        {
            new LockConstraint(),
            new FixedLinkConstraint()
        };

        public DoorRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng random, IItemHelper itemHelper)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _itemHelper = itemHelper;
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
            AddStickyNodeGroup(beginNode, pool);
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
            UnfixRE1Doors();
            UpdateLinkedRooms();
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

        private List<PlayNode> CreateNodes()
        {
            FixRE1Doors();

            var nodes = new List<PlayNode>();
            foreach (var kvp in _map.Rooms!)
            {
                var node = GetOrCreateNode(RdtId.Parse(kvp.Key));
                if (node.Category == DoorRandoCategory.Exclude)
                    continue;

                foreach (var edge in node.Edges)
                {
                    edge.Node = null;
                    edge.NoReturn = false;
                }
                nodes.Add(node);
            }
            return nodes;
        }

        // TODO is this still needed?
        private void FixRE1Doors()
        {
            // For RE 1 doors that have 0 as target stage, that means keep the stage
            // the same. This replaces every door with an explicit stage to simplify things
            foreach (var rdt in _gameData.Rdts)
            {
                if (rdt.Version == BioVersion.Biohazard1)
                {
                    if (!ShouldFixRE1Rdt(rdt.RdtId))
                        continue;

                    foreach (var door in rdt.Doors)
                    {
                        var target = door.Target;
                        if (target.Stage == 255)
                            target = new RdtId(rdt.RdtId.Stage, target.Room);
                        door.Target = GetRE1FixedId(target);
                    }
                }
            }
        }

        private void UnfixRE1Doors()
        {
            foreach (var rdt in _gameData.Rdts)
            {
                if (rdt.Version == BioVersion.Biohazard1)
                {
                    if (!ShouldFixRE1Rdt(rdt.RdtId))
                        continue;

                    foreach (var door in rdt.Doors)
                    {
                        var target = door.Target;
                        if (target.Stage == rdt.RdtId.Stage)
                        {
                            target = new RdtId(255, target.Room);
                            door.Target = target;
                        }
                    }
                }
            }
        }

        private bool ShouldFixRE1Rdt(RdtId rdtId)
        {
            var room = _map.GetRoom(rdtId);
            if (room == null || room.DoorRando == null)
                return true;

            foreach (var spec in room.DoorRando)
            {
                if (spec.Player != null && spec.Player != _config.Player)
                    continue;
                if (spec.Scenario != null && spec.Scenario != _config.Scenario)
                    continue;

                if (spec.Category != null)
                {
                    var category = (DoorRandoCategory)Enum.Parse(typeof(DoorRandoCategory), spec.Category, true);
                    if (category == DoorRandoCategory.Exclude)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private RdtId GetRE1FixedId(RdtId rdtId)
        {
            var rooms = _map.Rooms!;
            if (rdtId.Stage == 0 || rdtId.Stage == 1)
            {
                if (!rooms.ContainsKey(rdtId.ToString()))
                    return new RdtId(rdtId.Stage + 5, rdtId.Room);
            }
            else if (rdtId.Stage == 5 || rdtId.Stage == 6)
            {
                if (!rooms.ContainsKey(rdtId.ToString()))
                    return new RdtId(rdtId.Stage - 5, rdtId.Room);
            }
            return rdtId;
        }

        private void UpdateLinkedRooms()
        {
            foreach (var node in _allNodes)
            {
                if (node.LinkedRdtId == null)
                    continue;

                var rdt = _gameData.GetRdt(node.RdtId)!;
                var linkedRdt = _gameData.GetRdt(node.LinkedRdtId.Value)!;

                var rdtDoors = rdt.Doors.ToArray();
                var linkedRdtDoors = linkedRdt.Doors.ToArray();
                for (int i = 0; i < rdtDoors.Length; i++)
                {
                    var src = rdtDoors[i];
                    var dst = linkedRdtDoors[i];
                    dst.Target = src.Target;
                    dst.NextX = src.NextX;
                    dst.NextY = src.NextY;
                    dst.NextZ = src.NextZ;
                    dst.NextD = src.NextD;
                    dst.LockId = src.LockId;
                    dst.LockType = src.LockType;
                }
                _logger.WriteLine($"Synchronising doors from {node.RdtId} to {node.LinkedRdtId}");
            }
        }

        private void CreateArea(PlayNode begin, PlayNode end, List<PlayNode> pool)
        {
            _boxRoomReached = false;
            _keyItemSpotsLeft = 0;
            _keyItemRequiredSet.Clear();

            PlayEdge[] unfinishedEdges;
            do
            {
                CalculateEdgeCounts(pool);
                unfinishedEdges = GetUnfinishedEdges(pool);

                if (g_debugLogging)
                    _logger.WriteLine($"        Edges left: {_numUnconnectedEdges} (key = {_numKeyEdges}, unlocked = {_numUnlockedEdges})");
            } while (ConnectUpRandomNode(unfinishedEdges, pool));
            if (!ConnectUpNode(end, unfinishedEdges))
            {
                _logger.WriteLine($"    Failed to connect to end node {end.RdtId}");
                throw new Exception("Unable to connect end node");
            }

            // Lock other side of begin -> begin + 1 door
            foreach (var edge in begin.Edges)
            {
                if (edge.NoReturn && edge.Node != null)
                {
                    foreach (var oppositeEdge in edge.Node.Edges)
                    {
                        if (oppositeEdge.Node == begin)
                        {
                            oppositeEdge.Lock = LockKind.Always;
                        }
                    }
                }
            }

            // Set end -> end + 1 door to no return
            foreach (var edge in end.Edges)
            {
                if (edge.Node == null)
                    edge.NoReturn = true;
            }

            pool.RemoveAll(x => x.Visited);
        }

        private void FinalChecks(PlayGraph graph)
        {
            foreach (var node in _nodeMap.Values)
            {
                // if (!node.Visited)
                //     continue;

                // NOP out instructions
                var rdt = _gameData.GetRdt(node.RdtId)!;
                foreach (var offset in node.DoorRandoNop)
                {
                    rdt.Nop(offset);
                    _logger.WriteLine($"{rdt.RdtId} (0x{offset:X2}) opcode removed");
                }

                // Lock any unconnected doors and loopback to itself to be extra safe
                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null)
                    {
                        _logger.WriteLine($"{node.RdtId}:{edge.DoorId} -> null");

                        // Connect door back to itself
                        LockDoor(edge);

                        // Remove requirement of keys
                        edge.Requires = new ushort[0];
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

            var boxNodes = _nodesLeft
                .Where(x => x.Category == DoorRandoCategory.Box)
                .Shuffle(_rng)
                .OrderBy(x => x.HasCutscene ? 0 : 1)
                .ToList();
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
            _nodesLeft = _nodesLeft
                .Shuffle(_rng)
                .OrderBy(x => x.HasCutscene ? 0 : 1)
                .ToList();
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
            foreach (var reqRoom in node.RequiresRoom)
            {
                if (!list.Contains(reqRoom))
                {
                    list.Add(reqRoom);
                    AddStickyNodeGroup(reqRoom, list);
                }
            }

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
                foreach (var reqRoom in node2.RequiresRoom)
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
            // Connect key doors before non-key doors
            unfinishedEdges = unfinishedEdges
                .Shuffle(_rng)
                .OrderBy(x => x.Requires.Length != 0 ? 1 : 0)
                .ToArray();

            if (ConnectUpRandomNode(g_strictConstraints, unfinishedEdges, availableExitNodes))
                return true;

            return ConnectUpRandomNode(g_looseConstraints, unfinishedEdges, availableExitNodes);
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

        private bool ConnectUpNode(PlayNode endNode, IEnumerable<PlayEdge> unfinishedEdges)
        {
            // Order by most key rich edge first
            unfinishedEdges = unfinishedEdges
                .Shuffle(_rng)
                .OrderByDescending(x => x.Parent.DoorRandoAllRequiredItems.Length + x.Requires.Length)
                .ToArray();

            foreach (var exit in endNode.Edges.Shuffle(_rng))
            {
                foreach (var entrance in unfinishedEdges)
                {
                    if (ValidateConnection(g_endConstraints, entrance, exit))
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
                AddToKeyItemRequiredCount(exitNode.Edges.SelectMany(x => x.Requires));
                AddToKeyItemRequiredCount(exitNode.Requires);
            }

            ConnectDoor(entrance, exit, loopback);
        }

        private void AddToKeyItemRequiredCount(IEnumerable<ushort> keys)
        {
            foreach (var key in keys)
            {
                _keyItemRequiredSet.Add(key);
            }
        }

        private void ConnectDoor(PlayEdge aEdge, PlayEdge bEdge, bool isLocked = false)
        {
            var a = aEdge.Parent;
            var b = bEdge.Parent;
            aEdge.Node = b;
            bEdge.Node = a;

            var isAlwaysLocked = false;
            if (aEdge.Requires.Length != 0 || (aEdge.Lock == LockKind.Unblock && bEdge.Lock != LockKind.Gate))
            {
                // If the door is locked or temporarily blocked, lock from the other side
                // This is a safety measure for loopbacks
                isLocked = true;
            }
            if (aEdge.NoUnlock)
            {
                isLocked = false;
            }
            else if (!aEdge.Randomize || !bEdge.Randomize)
            {
                isLocked = false;
            }
            else if (aEdge == bEdge)
            {
                isLocked = true;
                isAlwaysLocked = true;
            }

            // Do not rewrite last room comparisons if edge is the same
            // this breaks rooms like 401 (South Office) when a door is unconnected
            var noCompareRewrite = aEdge == bEdge;
            if (a.Category != DoorRandoCategory.Static)
            {
                var aDoorId = aEdge.DoorId!.Value;
                var aRdt = _gameData.GetRdt(a.RdtId)!;
                aRdt.SetDoorTarget(aDoorId, b.RdtId, bEdge.Entrance!.Value, aEdge.OriginalTargetRdt, noCompareRewrite);
                aRdt.RemoveDoorUnlock(aDoorId);
                if (aEdge.Lock == LockKind.Side)
                {
                    aEdge.Lock = LockKind.None;
                    aRdt.RemoveDoorLock(aDoorId);
                }
                if (isAlwaysLocked)
                {
                    aEdge.LockId = _lockId;
                    aRdt.SetDoorLock(aDoorId, _lockId);
                }
                else if (isLocked)
                {
                    aEdge.LockId = _lockId;
                    aRdt.EnsureDoorUnlock(aDoorId, _lockId);
                }
            }

            if (aEdge != bEdge && b.Category != DoorRandoCategory.Static && bEdge.Lock != LockKind.Always)
            {
                var bDoorId = bEdge.DoorId!.Value;
                var bRdt = _gameData.GetRdt(b.RdtId)!;
                if (aEdge.Entrance == null)
                {
                    bRdt.SetDoorTarget(bDoorId, b.RdtId, bEdge.Entrance!.Value, bEdge.OriginalTargetRdt, noCompareRewrite);
                    bRdt.SetDoorLock(bDoorId, 255);
                }
                else
                {
                    bRdt.SetDoorTarget(bDoorId, a.RdtId, aEdge.Entrance.Value, bEdge.OriginalTargetRdt, noCompareRewrite);
                    if (isLocked)
                    {
                        bEdge.Lock = LockKind.Side;
                        bEdge.LockId = _lockId;
                        bRdt.SetDoorLock(bDoorId, _lockId);
                    }
                    else if (b.Category != DoorRandoCategory.Static)
                    {
                        if (bEdge.Lock == LockKind.Side)
                            bEdge.Lock = LockKind.None;
                        bRdt.RemoveDoorLock(bDoorId);
                    }
                }
            }

            if (isLocked)
            {
                _lockId++;
            }

            _logger.WriteLine($"    Connected {GetEdgeString(a, aEdge)} to {GetEdgeString(b, bEdge)}");
        }

        private void LockDoor(PlayEdge edge)
        {
            if (edge.DoorId == null)
                return;

            if (edge.Entrance != null)
            {
                ConnectDoor(edge, edge);
                return;
            }

            var doorId = edge.DoorId.Value;
            var rdt = _gameData.GetRdt(edge.Parent.RdtId)!;
            rdt.SetDoorLock(doorId, _lockId++);
        }

        private string GetEdgeString(PlayNode node, PlayEdge edge)
        {
            var rs = edge.GetRequiresString(_itemHelper);
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
            _keyRichEdge = null;
            _keyRichEdgeScore = 0;

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

                            var keyRichScore = edge.Parent.DoorRandoAllRequiredItems.Length + edge.Requires.Length;
                            if (keyRichScore > _keyRichEdgeScore)
                            {
                                _keyRichEdge = edge;
                                _keyRichEdgeScore = keyRichScore;
                            }
                        }
                    }
                }
            }
        }

        private Rdt GetIdealRdt(RdtId rtdId)
        {
            var rdt = _gameData.GetRdt(rtdId)!;
            if (rdt.Version == BioVersion.Biohazard1)
            {
                // The mansion 2 RDTs are better for door positions
                if (rtdId.Stage <= 1)
                {
                    return _gameData.GetRdt(new RdtId(rtdId.Stage + 5, rtdId.Room))!;
                }
            }
            return rdt;
        }

        private static bool IsSameRdtId(Rdt rdt, RdtId id, RdtId other)
        {
            if (rdt.Version == BioVersion.Biohazard1)
            {
                if (id.Stage == 255)
                {
                    return new RdtId(rdt.RdtId.Stage, id.Room) == other;
                }
                else if (id.Room == other.Room)
                {
                    var stageA = id.Stage;
                    var stageB = other.Stage;
                    if (stageA >= 5) stageA -= 5;
                    if (stageB >= 5) stageB -= 5;
                    return stageA == stageB;
                }
            }
            return id == other;
        }

        private PlayNode GetOrCreateNode(RdtId rdtId)
        {
            var node = FindNode(rdtId);
            if (node != null)
                return node;

            var rdt = _gameData.GetRdt(rdtId)!;
            var items = rdt.EnumerateOpcodes<IItemAotSetOpcode>(_config)
                .DistinctBy(x => x.Id)
                .Where(x => _config.IncludeDocuments || !_itemHelper.IsItemDocument((byte)x.Type))
                .Select(x => new ItemPoolEntry()
                {
                    RdtId = rdt.RdtId,
                    Id = x.Id,
                    Type = x.Type,
                    Amount = x.Amount
                })
                .ToArray();

            // RE1 Jill does not have ink ribbons
            if (rdt.Version == BioVersion.Biohazard1 && _config.Player == 1)
            {
                var inkRibbonType = _itemHelper.GetItemId(CommonItemKind.InkRibbon);
                items = items.Where(x => x.Type != inkRibbonType).ToArray();
            }

            node = new PlayNode(rdtId);
            node.Items = items;
            _nodeMap.Add(rdtId, node);

            var mapRoom = _map.GetRoom(rdtId);
            if (mapRoom == null)
                throw new Exception("No JSON definition for room");

            node.Requires = mapRoom.Requires ?? Array.Empty<ushort>();
            if (mapRoom.Items != null)
            {
                node.RequiresRoom = mapRoom.Items
                    .SelectMany(x => x.RequiresRoom ?? Array.Empty<string>())
                    .Select(x => GetOrCreateNode(RdtId.Parse(x)))
                    .ToArray();
            }

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

                    IDoorAotSetOpcode? targetEntrance = null;
                    if (door.ReadOffset != null && door.WriteOffset != null)
                    {
                        targetEntrance = rdt.ConvertToDoor(door.ReadOffset.Value, door.WriteOffset.Value);
                    }

                    DoorEntrance? entrance = null;

                    Rdt targetRdt;
                    IDoorAotSetOpcode? targetExit;
                    var target = RdtDoorTarget.Parse(door.Target!);
                    if (target.Id != null)
                    {
                        targetRdt = GetIdealRdt(target.Rdt);
                        targetExit = targetRdt.Doors.First(x => x.Id == target.Id);
                    }
                    else
                    {
                        targetRdt = GetIdealRdt(target.Rdt);
                        targetExit = targetRdt.Doors.FirstOrDefault(x => IsSameRdtId(targetRdt, x.Target, rdtId));
                    }
                    var doorId = door.Id ?? rdt.Doors.FirstOrDefault(x => x.Target == target.Rdt)?.Id;
                    if (targetExit != null)
                    {
                        entrance = DoorEntrance.FromOpcode(targetExit);
                        if (targetEntrance != null)
                        {
                            targetEntrance.Texture = targetExit.Texture;
                            targetEntrance.Animation = targetExit.Animation;
                            targetEntrance.Sound = targetExit.Sound;
                        }

                        if (entrance != null && door.Cut != null)
                        {
                            entrance = entrance.Value.WithCamera(door.Cut.Value);
                        }
                    }

                    var edgeNode = GetOrCreateNode(target.Rdt);
                    var edge = new PlayEdge(node, edgeNode, door.NoReturn, door.Requires, doorId, entrance);
                    edge.Randomize = door.Randomize ?? true;
                    edge.NoUnlock = door.NoUnlock;
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
                    node.HasCutscene = spec.Cutscene;
                }
            }

            if (mapRoom.LinkedRoom != null)
            {
                node.LinkedRdtId = RdtId.Parse(mapRoom.LinkedRoom);
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
                            _allNodes.Add(edge.Node);
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

                // Do not connect this node up yet until we have visited all required rooms (e.g. armory)
                if (!entrance.RequiresRoom.All(x => x.Visited))
                    return false;

                // Ignore rest of checks if this is a fixed edge
                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Exit node must have an entrance
                if (exit.Entrance == null)
                    return false;

                // For now, ignore any rooms with one way door setups
                // if (exit.Parent.Edges.Any(x => x.Entrance == null && x.Randomize))
                //     return false;

                // Don't connect to a door with a key requirement on the other side
                if (exit.Requires.Length != 0)
                    return false;

                // Don't connect to a door with a room requirement on the other side
                if (exit.RequiresRoom.Length != 0)
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
                // Check if this is a loopback node
                if (!exit.Parent.Visited)
                    return true;

                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Do not waste the edge requiring the most keys on a loopback
                if (entrance == dr._keyRichEdge)
                    return false;

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
                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Check if this is a leaf node
                var extraEdges = exit.Parent.Edges.Count(x => x != exit && ValidateEntranceNodeForCounting(x));
                if (extraEdges != 0)
                    return true;

                // Do not connect to leaf node if this is our last edge (reserved for bridge node)
                var remainingEdges = dr._numUnlockedEdges + dr._numKeyEdges - 1;
                if (remainingEdges == 0)
                    return false;

                // Do not waste the edge requiring the most keys on a leaf node
                if (entrance == dr._keyRichEdge)
                    return false;

                return true;
            }
        }

        private class KeyConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                var numRequiredKeys = dr._keyItemRequiredSet.Count;
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

                if (!entrance.Randomize || !exit.Randomize)
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
