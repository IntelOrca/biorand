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

                AssertKey(route, key0, item0a, item0b);
                AssertKey(route, key1, item0a, item0b, item1a);
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

                AssertKey(route, key0, item0a, item0b);
                AssertKey(route, key1, item0a, item0b);
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

                AssertKey(route, key0, item0a);
                Assert.True(route.AllNodesVisited);
            }
        }

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

                AssertKey(route, key0, item0a);
                AssertKey(route, key0, item2a);
                Assert.True(route.AllNodesVisited);
            }
        }

        private void AssertKey(Route route, Node key, params Node[] expected)
        {
            var actual = route.GetGetNodeForKey(key);
            Assert.NotNull(actual);
            Assert.Contains(actual.Value, expected);
        }
    }
}
