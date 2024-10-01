using System;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Routing;
using Ps2IsoTools.ISO.Builders;
using Xunit;
using Xunit.Abstractions;

namespace IntelOrca.Biohazard.BioRand.Tests
{
    public class TestRouting
    {
        private const int Retries = 100;

        private readonly ITestOutputHelper _output;

        public TestRouting(ITestOutputHelper output)
        {
            _output = output;
        }

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

                var key0 = builder.ReusableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");

                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);

                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1a = builder.Item(1, "ITEM 1.A", room1);

                var room2 = builder.AndGate("ROOM 2", room1, key1);
                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a, item0b);
                AssertKeyOnce(route, key1, item0a, item0b, item1a);
                Assert.Equal((RouteSolverResult)0, route.Solve());
            }
        }

        [Fact]
        public void KeyBehindKey()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ReusableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");
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

                var key0 = builder.ReusableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");
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

                var key0 = builder.ReusableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");
                var key2 = builder.ReusableKey(1, "KEY 2");
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
        public void StartRoomRequiresKey()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ReusableKey(1, "KEY 0");
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

                var key0 = builder.ReusableKey(1, "KEY 0");
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

                var key0 = builder.ReusableKey(1, "KEY 0");
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

        /// <summary>
        /// Tests a map with a mini segment which can go back to main segment.
        /// </summary>
        [Fact]
        public void MiniSegment()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ReusableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");

                var room1 = builder.AndGate("ROOM 1");
                var room2 = builder.AndGate("ROOM 2", room1);
                var room3 = builder.OneWay("ROOM 3", room1);
                var room4 = builder.AndGate("ROOM 4", room3, key1);
                var room5 = builder.OrGate("ROOM 5", room2, room4);
                var room6 = builder.AndGate("ROOM 6", room3, key0);

                var item2 = builder.Item(1, "Item 2", room2);
                var item3 = builder.Item(1, "Item 3", room3);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key0, item2);
                AssertKeyOnce(route, key1, item3);
            }
        }

        /// <summary>
        /// Tests a map with a two segments which you can go between.
        /// </summary>
        [Fact]
        public void CircularSegments()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ReusableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(2, "KEY 1");

                var room0 = builder.AndGate("ROOM 0");
                var room1 = builder.OneWay("ROOM 1", room0);
                var room2 = builder.AndGate("ROOM 2", room1);
                var room3 = builder.AndGate("ROOM 3", room0);
                var room4 = builder.AndGate("ROOM 4", room2);
                var room5 = builder.OrGate("ROOM 5", room3, builder.OneWay(null, room4));
                var room6 = builder.AndGate("ROOM 6", room2, key1);
                var room7 = builder.AndGate("ROOM 7", room3, key0);
                var item2 = builder.Item(1, "Item 2", room2);
                var item3 = builder.Item(2, "Item 3", room3);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key0, item2);
                AssertKeyOnce(route, key1, item3);
            }
        }

        [Fact]
        public void SingleUseKey_DoorAfterDoor()
        {
            var builder = new GraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0);
            var item1 = builder.Item(1, "ITEM 1", room1);
            var room2 = builder.AndGate("ROOM 2", room1, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_TwoDoors()
        {
            var builder = new GraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var item1 = builder.Item(1, "ITEM 1", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0);
            var room2 = builder.AndGate("ROOM 2", room0, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_TwoDoors_NoPossibleSoftlock()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0A", room0);
                var item0b = builder.Item(1, "ITEM 0B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key0);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0);
                AssertItem(route, item0b, key0);
                AssertItemNotFulfilled(route, item1);
                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void SingleUseKey_RouteOrderMatters()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);
                var room3 = builder.AndGate("ROOM 3", room0, key0);
                var item3a = builder.Item(1, "ITEM 3.A", room3);
                var item3b = builder.Item(1, "ITEM 3.B", room3);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0, key0);
                AssertKeyOnce(route, key1, item3a, item3b);
                AssertKeyQuantity(route, key0, 2);
                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void SingleUseKey_RouteOrderMatters_Flexible()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);
                var room3 = builder.AndGate("ROOM 3", room0, key0);
                var item3a = builder.Item(1, "ITEM 3.A", room3);
                var item3b = builder.Item(1, "ITEM 3.B", room3);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key1, item3a, item3b);
            }
        }

        [Fact]
        public void SingleUseKey_TwoOneWayDoors()
        {
            var builder = new GraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var item1 = builder.Item(1, "ITEM 1", room0);
            var room1 = builder.OneWay("ROOM 1", room0,key0);
            var room2 = builder.OneWay("ROOM 2", room0,key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_TwoKeyDoor()
        {
            var builder = new GraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var key1 = builder.ReusableKey(2, "KEY 1");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0);
            var item1a = builder.Item(1, "ITEM 1A", room1);
            var item1b = builder.Item(2, "ITEM 1B", room1);
            var room2 = builder.AndGate("ROOM 2", room0, key0, key1);
            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1a, key0);
            AssertItem(route, item1b, key1);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_KeyOrderMatters()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");
                var key2 = builder.ReusableKey(1, "KEY 2");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1a = builder.Item(1, "ITEM 1.A", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key1);
                var item2a = builder.Item(1, "ITEM 2.A", room2);
                var room3 = builder.AndGate("ROOM 3", room2, key2);
                var room4 = builder.AndGate("ROOM 4", room3, key0);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void Key2xRequired()
        {
            var builder = new GraphBuilder();

            var key0 = builder.ReusableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0a = builder.Item(1, "ITEM 0.A", room0);
            var item0b = builder.Item(1, "ITEM 0.B", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0a, key0);
            AssertItem(route, item0b, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void TwoRoutes()
        {
            var builder = new GraphBuilder();

            var keyTop = builder.ReusableKey(1, "KEY TOP");
            var keyBottom = builder.ReusableKey(1, "KEY BOTTOM");
            var keyEnd = builder.ReusableKey(1, "KEY END");

            var roomStart = builder.AndGate("ROOM START");

            var roomTop1 = builder.OneWay("ROOM TOP 1", roomStart);
            var itemTop1 = builder.Item(1, "ITEM TOP 1", roomTop1);
            var roomTop2 = builder.AndGate("ROOM TOP 2", roomTop1, keyTop);

            var roomBottom1 = builder.OneWay("ROOM BOTTOM 1", roomStart);
            var itemBottom1 = builder.Item(1, "ITEM BOTTOM 1", roomBottom1);
            var roomBottom2 = builder.AndGate("ROOM BOTTOM 2", roomBottom1, keyBottom);

            var roomMerge = builder.OrGate("ROOM MERGE", roomTop2, roomBottom2);
            var itemMerge = builder.Item(1, "ITEM MERGE", roomMerge);
            var roomEnd = builder.AndGate("ROOM END", roomMerge, keyEnd);

            var route = builder.GenerateRoute();

            AssertItem(route, itemTop1, keyTop);
            AssertItem(route, itemBottom1, keyBottom);
            AssertItem(route, itemMerge, keyEnd);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void Removable_SingleKeyRequired()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.RemovableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 1");

                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key0);
                var item2 = builder.Item(1, "ITEM 1", room2);
                var room3 = builder.AndGate("ROOM 2", room2, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0);
                AssertKeyOnce(route, key1, item1, item2);
                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void Removable_MultipleKeysRequired()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.RemovableKey(1, "KEY 0");

                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);
                var item2 = builder.Item(1, "ITEM 2", room2);
                var room3 = builder.AndGate("ROOM 3", room2, key0);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertItem(route, item0, key0);
                AssertKeyQuantity(route, key0, 3);
            }
        }

        [Fact]
        public void Removable_MultipleKeysRequiredOnce()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();

                var key0 = builder.RemovableKey(1, "KEY 0");
                var key1 = builder.ReusableKey(1, "KEY 0");

                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key0);
                var item2 = builder.Item(1, "ITEM 2", room2);
                var room3 = builder.AndGate("ROOM 3", room1, key0);
                var room4 = builder.AndGate("ROOM 4", room2, key0);
                var room5 = builder.AndGate("ROOM 5", room4, key1);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key1, item1, item2);
                AssertItem(route, item0, key0);
                AssertItem(route, item1, key0, key1);
                AssertItem(route, item2, key0, key1);
                AssertKeyQuantity(route, key0, 2);
            }
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

        private static void AssertKeyQuantity(Route route, Node key, int expectedCount)
        {
            var actualCount = route.GetItemsContainingKey(key).Count;
            Assert.Equal(expectedCount, actualCount);
        }
    }
}
