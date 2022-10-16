using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace rer
{
    internal class PlayGraphFactory
    {
        private GameData _gameData;
        private Map _map = new Map();
        private Dictionary<(int, int), PlayNode> _nodes = new Dictionary<(int, int), PlayNode>();
        private List<ItemPoolEntry> _itemPool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _definedPool = new List<ItemPoolEntry>();
        private HashSet<ushort> _requiredItems = new HashSet<ushort>();
        private HashSet<ushort> _haveItems = new HashSet<ushort>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private Random _random = new Random();

        public PlayGraph Create(GameData gameData, string path)
        {
            _gameData = gameData;

            var jsonMap = File.ReadAllText(path);
            _map = JsonSerializer.Deserialize<Map>(jsonMap, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

            var graph = new PlayGraph();
            graph.Start = GetOrCreateNode(_map.Start!.Stage, _map.Start!.Room);

            Search(graph.Start);
            while (_requiredItems.Count != 0)
            {
                PlaceKeyItem();
                Search(graph.Start);
            }
            RandomiseRemainingPool();
            Save();
            return graph;
        }

        private void Search(PlayNode start)
        {
            var seen = new HashSet<(int, int)>();
            var walkedNodes = new List<PlayNode>();
            var stack = new Stack<PlayNode>();
            stack.Push(start);
            while (stack.Count != 0)
            {
                var node = stack.Pop();
                walkedNodes.Add(node);
                seen.Add((node.Stage, node.Room));

                if (_visitedRooms.Add(node))
                {
                    // First time we have visited room, add room items to pool
                    foreach (var id in node.ItemIds!)
                    {
                        _itemPool.Add(new ItemPoolEntry()
                        {
                            Stage = node.Stage,
                            Room = node.Room,
                            Id = id
                        });
                    }
                }

                foreach (var edge in node.Edges)
                {
                    if (seen.Contains((edge.Node.Stage, edge.Node.Room)))
                        continue;
                    if (edge.Locked)
                        continue;

                    var requiredItems = edge.Requires.Except(_haveItems).ToArray()!;
                    if (requiredItems.Length == 0)
                    {
                        // Console.WriteLine($"{node} -> {edge.Node}");
                        stack.Push(edge.Node);
                    }
                    else
                    {
                        foreach (var item in requiredItems)
                        {
                            _requiredItems.Add(item);
                        }
                    }
                }
            }
        }

        private void PlaceKeyItem()
        {
            if (_requiredItems.Count == 0)
                return;

            var reqIndex = _random.Next(0, _requiredItems.Count);
            var req = _requiredItems.Skip(reqIndex).First();
            _requiredItems.Remove(req);
            _haveItems.Add(req);
            var index = _random.Next(0, _itemPool.Count);
            var itemEntry = _itemPool[index];
            _itemPool.RemoveAt(index);
            itemEntry.Type = req;
            itemEntry.Amount = 1;
            _definedPool.Add(itemEntry);
            Console.WriteLine($"Placing key item ({Items.GetItemName(itemEntry.Type)}) in {Utility.GetHumanRoomId(itemEntry.Stage, itemEntry.Room)}:{itemEntry.Id}");
        }

        private void RandomiseRemainingPool()
        {
            while (_itemPool.Count != 0)
            {
                var entry = _itemPool[0];
                _itemPool.RemoveAt(0);

                entry.Type = 0x19;
                entry.Amount = (ushort)_random.Next(0, 8);
                _definedPool.Add(entry);
            }
        }

        private void Save()
        {
            foreach (var entry in _definedPool)
            {
                var rdt = _gameData.GetRdt(entry.Stage, entry.Room);
                rdt.SetItem(entry.Id, entry.Type, entry.Amount);
                rdt.Save();
            }
        }

        private PlayNode GetOrCreateNode(int stage, int room)
        {
            var node = FindNode(stage, room);
            if (node != null)
                return node;

            var rdt = _gameData.GetRdt(stage, room);
            var itemIds = rdt!.Items.Select(x => x.Id).Distinct().ToArray();

            node = new PlayNode(stage, room);
            node.ItemIds = itemIds;
            _nodes.Add((stage, room), node);

            var mapRoom = _map.GetRoom(stage, room);
            foreach (var door in mapRoom!.Doors!)
            {
                var edgeNode = GetOrCreateNode(door.Stage, door.Room);
                var edge = new PlayEdge(edgeNode, door.Locked, door.Requires!);
                node.Edges.Add(edge);
            }

            return node;
        }

        public PlayNode? FindNode(int stage, int room)
        {
            if (_nodes.TryGetValue((stage, room), out var node))
                return node;
            return null;
        }
    }
}
