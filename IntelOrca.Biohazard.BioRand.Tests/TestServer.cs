using System;
using System.Net;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Tests
{
    public class TestServer
    {
        [Fact]
        public async Task TestConnectionAsync()
        {
            using (var server = CreateServer())
            {
                using (var clientA = new BioRandClient())
                {
                    await clientA.ConnectAsync("localhost", BioRandServer.DefaultPort);
                    await clientA.AuthenticateAsync("Alice");
                    Assert.Equal("Alice", clientA.ClientName);

                    using (var clientB = new BioRandClient())
                    {
                        await clientB.ConnectAsync("localhost", BioRandServer.DefaultPort);
                        await clientB.AuthenticateAsync("Bob");
                        Assert.Equal("Bob", clientB.ClientName);

                        Assert.Equal(2, server.Players.Count);
                        Assert.Equal("Alice", server.Players[0].Name);
                        Assert.Equal("Bob", server.Players[1].Name);
                    }
                }
                await WaitForAsync(() => server.Players.Count == 0);
                Assert.Equal(0, server.Players.Count);
            }
        }

        [Fact]
        public async Task TestRoomCreationAsync()
        {
            using (var server = CreateServer())
            {
                using (var clientA = await CreateClientAsync("Alice"))
                {
                    await clientA.CreateRoomAsync();
                    Assert.NotNull(clientA.RoomId);
                    Assert.Equal(new[] { "Alice" }, clientA.RoomPlayers);
                }
            }
        }

        [Fact]
        public async Task TestJoinRoomAsync()
        {
            using (var server = CreateServer())
            {
                using (var clientA = await CreateClientAsync("Alice"))
                {
                    await clientA.CreateRoomAsync();
                    using (var clientB = await CreateClientAsync("Bob"))
                    {
                        await clientB.JoinRoomAsync(clientA.RoomId);
                        Assert.Equal(new[] { "Alice", "Bob" }, clientB.RoomPlayers);
                        await WaitForAsync(() => clientA.RoomPlayers.Length == 2);
                        Assert.Equal(new[] { "Alice", "Bob" }, clientA.RoomPlayers);
                    }
                    await WaitForAsync(() => clientA.RoomPlayers.Length == 1);
                    Assert.Equal(new[] { "Alice" }, clientA.RoomPlayers);
                }
            }
        }

        private static async Task WaitForAsync(Func<bool> predicate)
        {
            for (var i = 0; i < 1000; i++)
            {
                if (predicate())
                    break;

                await Task.Delay(5);
            }
        }

        private static BioRandServer CreateServer()
        {
            var server = new BioRandServer(NullLogger.Instance);
            server.Listen(new IPEndPoint(IPAddress.Any, BioRandServer.DefaultPort));
            return server;
        }

        private static async Task<BioRandClient> CreateClientAsync(string name)
        {
            var client = new BioRandClient();
            await client.ConnectAsync("localhost", BioRandServer.DefaultPort);
            await client.AuthenticateAsync(name);
            return client;
        }
    }
}
