using System;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Routing;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Tests
{
    public class TestRouting
    {
        private const int Retries = 100;

        /// <summary>
        /// Tests an OR gate, i.e. two entrances to a room.
        /// </summary>
        [Fact]
        public void AltWaysInSameRoom()
        {
            var builder = new GraphBuilder();
            var room0 = builder.AndGate("ROOM 0");
            var room1 = builder.AndGate("ROOM 1", room0);
            var room2 = builder.AndGate("ROOM 2", room0);
            var room3 = builder.OrGate("ROOM 3", room1, room2);
            var route = builder.GenerateRoute();
            Assert.True(route.AllNodesVisited);
        }

        /// <summary>
        /// A simple 3 room map with items and keys.
        /// </summary>
        [Fact]
        public void Basic()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.Key(1, "KEY 0");
                var key1 = builder.Key(1, "KEY 1");

                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);

                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1a = builder.Item(1, "ITEM 1.A", room1);

                var room2 = builder.AndGate("ROOM 2", room1, key1);
                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a, item0b);
                AssertKeyOnce(route, key1, item0a, item0b, item1a);
            }
        }

        [Fact]
        public void KeyBehindKey()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.Key(1, "KEY 0");
                var key1 = builder.Key(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var item0c = builder.Item(1, "ITEM 0.C", room0, key0);
                var room1 = builder.AndGate("ROOM 1", room0, key0, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a, item0b);
                AssertKeyOnce(route, key1, item0a, item0b, item0c);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// An edge case where the keys must be placed in a certain order,
        /// otherwise we run out of accessible item nodes for all the keys.
        /// </summary>
        [Fact]
        public void KeyOrderMatters()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.Key(1, "KEY 0");
                var key1 = builder.Key(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0, key0);
                var room1 = builder.AndGate("ROOM 1", room0, key0, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a);
                AssertKeyOnce(route, key1, item0b);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Test that when we fulfill a key, we don't add its edges to the next list.
        /// This prevents cases where we place keys prematurely before we require them.
        /// </summary>
        [Fact]
        public void NoKey()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.Key(1, "KEY 0");
                var key1 = builder.Key(1, "KEY 1");
                var key2 = builder.Key(1, "KEY 2");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var room2 = builder.AndGate("ROOM 2", room1, key1);
                var room3 = builder.AndGate("ROOM 3", room2, key0, key2);
                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0, key1);
                AssertItem(route, item0b, key0, key1);
                Assert.False(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Test a map where a (start) room requires a key.
        /// </summary>
        [Fact]
        public void RequireOnlyKey()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.Key(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.AndGate("ROOM 1", key0);
                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with two segments where a key must be placed in both
        /// segments to prevent softlock if player does not collect key in first segment.
        /// </summary>
        [Fact]
        public void EnsureKeyPlacedAgain()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.Key(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var room2 = builder.OneWay("ROOM 2", room0);
                var item2a = builder.Item(1, "ITEM 2.A", room2);
                var room3 = builder.AndGate("ROOM 3", room2, key0);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0);
                AssertItem(route, item2a, key0);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with two segments where a key only needs to be placed
        /// once as the key is required to get to the second segment.
        /// </summary>
        [Fact]
        public void EnsureKeyNotPlacedAgain()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.Key(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.OneWay("ROOM 1", room0, key0);
                var item1a = builder.Item(1, "ITEM 1.A", room1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0);
                AssertItemNotFulfilled(route, item1a);
                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void Key2xRequired()
        {
            var builder = new GraphBuilder();

            var key0 = builder.Key(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0a = builder.Item(1, "ITEM 0.A", room0);
            var item0b = builder.Item(1, "ITEM 0.B", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0a, key0);
            AssertItem(route, item0b, key0);
            Assert.True(route.AllNodesVisited);
        }

        private static void AssertItemNotFulfilled(Route route, Node item)
        {
            var actual = route.GetItemContents(item);
            Assert.True(actual == null,
                string.Format("Expected {0} to be unfulfilled but was {1}",
                    item,
                    actual));
        }

        private static void AssertItem(Route route, Node item, params Node?[] expected)
        {
            var actual = route.GetItemContents(item);
            Assert.True(Array.IndexOf(expected, actual) != -1,
                string.Format("Expected {0} to be {{{1}}} but was {2}",
                    item,
                    string.Join(", ", expected),
                    actual?.ToString() ?? "(null)"));
        }

        private static void AssertKeyOnce(Route route, Node key, params Node[] expected)
        {
            var items = route.Graph.Nodes
                .Where(x => x.Kind == NodeKind.Item)
                .Where(x => route.GetItemContents(x) == key)
                .ToArray();

            if (items.Length == 0)
            {
                Assert.True(items.Length == expected.Length,
                    string.Format("Expected {0} to be at {{{1}}} but was not placed",
                        key,
                        string.Join(", ", expected)));
            }
            else
            {
                foreach (var item in items)
                {
                    Assert.True(Array.IndexOf(expected, item) != -1,
                        string.Format("Expected {0} to be at {{{1}}} but was at {2}",
                            key,
                            string.Join(", ", expected),
                            item));
                }
            }
            Assert.True(items.Length == 1, "Expected key to only be placed once");
        }
    }
}
