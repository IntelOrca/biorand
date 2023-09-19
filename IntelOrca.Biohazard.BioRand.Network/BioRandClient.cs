using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.Network.Packets;

namespace IntelOrca.Biohazard.BioRand.Network
{
    public class BioRandClient : IDisposable
    {
        private TcpClient _client;

        public event EventHandler RoomUpdated;

        public string ClientId { get; private set; }
        public string ClientName { get; private set; }
        public string RoomId { get; private set; }
        public string[] RoomPlayers { get; private set; }
        public BioRandJsonStream Stream { get; private set; }

        public void Dispose()
        {
            if (Stream != null)
            {
                Stream.SendPacketAsync(new DisconnectPacket(), default(CancellationToken)).Wait();
                Stream.Dispose();
            }
            Stream = null;
            _client?.Dispose();
            _client = null;
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _client.NoDelay = true;
            Stream = new BioRandJsonStream(_client.GetStream());
            Stream.ReceievePacket += ReceievePacket;
        }

        private void ReceievePacket(object sender, Packet e)
        {
            if (e is RoomDetailsPacket rdp)
            {
                UpdateRoom(rdp);
            }
        }

        private void UpdateRoom(RoomDetailsPacket rdp)
        {
            if (rdp == null)
            {
                RoomId = null;
                RoomPlayers = null;
            }
            else
            {
                RoomId = rdp.RoomId;
                RoomPlayers = rdp.Players;
            }
            RoomUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task AuthenticateAsync(string name, CancellationToken ct = default)
        {
            var r = await Stream.SendReceivePacketAsync(new AuthenticatePacket()
            {
                ClientName = name,
                ClientVersion = BioRandServer.Version
            }, ct);
            var auth = ThrowOnErrorPacket<AuthenticatedPacket>(r);
            ClientId = auth.ClientId;
            ClientName = auth.ClientName;
        }

        public async Task CreateRoomAsync(CancellationToken ct = default)
        {
            var r = await Stream.SendReceivePacketAsync(new CreateRoomPacket(), ct);
            var rdp = ThrowOnErrorPacket<RoomDetailsPacket>(r);
            RoomId = rdp.RoomId;
            RoomPlayers = rdp.Players;
        }

        public async Task JoinRoomAsync(string id, CancellationToken ct = default)
        {
            var r = await Stream.SendReceivePacketAsync(new JoinRoomPacket()
            {
                RoomId = id
            }, ct);
            var rdp = ThrowOnErrorPacket<RoomDetailsPacket>(r);
            UpdateRoom(rdp);
        }

        public async Task LeaveRoomAsync(CancellationToken ct = default)
        {
            await Stream.SendPacketAsync(new LeaveRoomPacket(), ct);
            UpdateRoom(null);
        }

        private T ThrowOnErrorPacket<T>(Packet packet) where T : Packet
        {
            if (packet is ErrorPacket errorPacket)
            {
                throw new Exception(errorPacket.Message);
            }
            if (!(packet is T typedPacket))
            {
                throw new Exception("Incorrect response packet");
            }
            return typedPacket;
        }
    }
}
