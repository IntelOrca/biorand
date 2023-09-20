using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.Network.Packets;
using Microsoft.Extensions.Logging;

namespace IntelOrca.Biohazard.BioRand.Network
{
    public class BioRandServer : IDisposable
    {
        public const int DefaultPort = 31070;
        public const int Version = 1;

        private readonly ILogger _logger;
        private readonly List<TcpListener> _listeners = new List<TcpListener>();
        private CancellationTokenSource _runCts;
        private Task _runTask;
        private readonly List<BioRandPlayer> _players = new List<BioRandPlayer>();
        private readonly List<BioRandRoom> _rooms = new List<BioRandRoom>();
        private Random _random = new Random();

        public IReadOnlyList<BioRandPlayer> Players => _players;

        public BioRandServer(ILogger logger)
        {
            _logger = logger;
            _runCts = new CancellationTokenSource();
            _runTask = Task.Run(() => RunAsync(_runCts.Token));
        }

        public void Dispose()
        {
            _runCts.Cancel();
            _runTask.Wait();
        }

        public void Listen(IPEndPoint endPoint)
        {
            var listener = new TcpListener(endPoint);
            listener.Start();
            _listeners.Add(listener);
            _logger.LogInformation("Listening on {0}", endPoint);
        }

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await HandleListenersAsync();
                await HandleDisconnectedClientsAsync();
                RoomEmptyRooms();
                await Task.Delay(10);
            }
            foreach (var player in _players)
            {
                player.TcpClient.Dispose();
            }
            foreach (var listener in _listeners)
            {
                listener.Stop();
            }
        }

        private async Task HandleListenersAsync()
        {
            foreach (var listener in _listeners)
            {
                if (listener.Pending())
                {
                    var client = await listener.AcceptTcpClientAsync();
                    AcceptClient(client);
                }
            }
        }

        private void AcceptClient(TcpClient client)
        {
            var player = new BioRandPlayer(client, GetNextPlayerId());
            player.ReceivePacket += OnReceievePacket;
            _players.Add(player);
            _logger.LogInformation("{0} connected", player.Id);
        }

        private async void OnReceievePacket(object sender, Packet e)
        {
            var player = sender as BioRandPlayer;
            var reply = await ReceievePacketAsync(player, e);
            if (reply != null)
            {
                reply.ReplyId = e.PacketId;
                await player.Stream.SendPacketAsync(reply);
            }
        }

        private async Task HandleDisconnectedClientsAsync()
        {
            var disconnectedPlayers = _players.Where(x => !x.Connected).ToArray();
            foreach (var player in disconnectedPlayers)
            {
                await RemovePlayerFromRoomAsync(player);
                _players.Remove(player);
                _logger.LogInformation("{0} disconnected", player.Id);
            }
        }

        private async Task<Packet> ReceievePacketAsync(BioRandPlayer player, Packet p)
        {
            switch (p)
            {
                case AuthenticatePacket auth:
                    if (auth.ClientVersion < Version)
                    {
                        return ErrorPacket("Incompatible version with server");
                    }
                    player.Name = auth.ClientName;
                    _logger.LogInformation("{0} authenticated as {1}", player.Id, player.Name);
                    return new AuthenticatedPacket()
                    {
                        ClientId = player.Id,
                        ClientName = player.Name,
                        ServerVersion = Version
                    };
                case DisconnectPacket _:
                    await RemovePlayerFromRoomAsync(player);
                    player.TcpClient.Close();
                    break;
                case CreateRoomPacket _:
                    {
                        await RemovePlayerFromRoomAsync(player);
                        var room = CreateRoom();
                        room.Players.Add(player);
                        player.Room = room;
                        return GetRoomDetailsPacket(room);
                    }
                case JoinRoomPacket joinPacket:
                    {
                        var room = _rooms.Find(r => r.Id == joinPacket.RoomId);
                        if (room == null)
                        {
                            return ErrorPacket("No room found with this ID.");
                        }
                        await RemovePlayerFromRoomAsync(player);
                        player.Room = room;
                        room.Players.Add(player);
                        _logger.LogInformation("{0} joined {1}", player.Id, room.Id);
                        return await RefreshRoomDetailsAsync(room, player);
                    }
                case LeaveRoomPacket leavePacket:
                    await RemovePlayerFromRoomAsync(player);
                    break;
                default:
                    return ErrorPacket("Unsupported packet type");
            }
            return null;
        }

        private BioRandRoom CreateRoom()
        {
            var room = new BioRandRoom();
            room.Id = GetNextRoomId();
            _rooms.Add(room);
            _logger.LogInformation("{0} created", room.Id);
            return room;
        }

        private void RoomEmptyRooms()
        {
            var emptyRooms = _rooms.Where(x => x.Empty).ToArray();
            foreach (var room in emptyRooms)
            {
                _rooms.Remove(room);
                _logger.LogInformation("{0} deleted", room.Id);
            }
        }

        private RoomDetailsPacket GetRoomDetailsPacket(BioRandRoom room)
        {
            var packet = new RoomDetailsPacket()
            {
                RoomId = room.Id,
                Players = room.Players.Select(x => x.Name).ToArray()
            };
            return packet;
        }

        private async Task RemovePlayerFromRoomAsync(BioRandPlayer player)
        {
            var room = player.Room;
            if (room != null)
            {
                room.Players.Remove(player);
                await RefreshRoomDetailsAsync(room);
                player.Room = null;
                _logger.LogInformation("{0} left {1}", player.Id, room.Id);
            }
        }

        private async Task<Packet> RefreshRoomDetailsAsync(BioRandRoom room, BioRandPlayer exceptPlayer = null)
        {
            var packet = GetRoomDetailsPacket(room);
            foreach (var player in room.Players)
            {
                if (exceptPlayer == player)
                    continue;

                try
                {
                    await player.Stream.SendPacketAsync(packet);
                }
                catch
                {
                }
            }
            return packet;
        }

        private ErrorPacket ErrorPacket(string message)
        {
            return new ErrorPacket()
            {
                Message = message
            };
        }

        private string GetNextPlayerId()
        {
            var id = _random.Next(100000, 1000000);
            return $"PL-{id}";
        }

        private string GetNextRoomId()
        {
            var id = _random.Next(10000, 100000);
            return $"ROOM-{id}";
        }
    }

    public class BioRandRoom
    {
        public List<BioRandPlayer> Players { get; } = new List<BioRandPlayer>();
        public string Id { get; set; }

        public bool Empty => Players.Count == 0;
    }
}
