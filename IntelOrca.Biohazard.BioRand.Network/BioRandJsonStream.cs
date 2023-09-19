using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.Network.Packets;

namespace IntelOrca.Biohazard.BioRand.Network
{
    public class BioRandJsonStream : IDisposable
    {
        private readonly NetworkStream _stream;
        private readonly object _packetSync = new object();
        private readonly List<Packet> _receivedPackets = new List<Packet>();
        private readonly CancellationTokenSource _receiveLoopCts;
        private readonly Task _receiveLoop;
        private int _packetId;

        public event EventHandler<Packet> ReceievePacket;

        public BioRandJsonStream(NetworkStream stream)
        {
            _stream = stream;
            _receiveLoopCts = new CancellationTokenSource();
            _receiveLoop = Task.Run(() => ReceiveLoop(_receiveLoopCts.Token));
        }

        public void Dispose()
        {
            _receiveLoopCts.Cancel();
            _receiveLoop.Wait();
        }

        private int GetNextPacketId()
        {
            return Interlocked.Increment(ref _packetId);
        }

        private async void ReceiveLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_stream.DataAvailable)
                {
                    var packet = await ReadPacketAsync(ct);
                    if (packet != null)
                    {
                        if (packet.ReplyId == null)
                        {
                            ReceievePacket?.Invoke(this, packet);
                        }
                        else
                        {
                            lock (_packetSync)
                            {
                                _receivedPackets.Add(packet);
                            }
                        }
                    }
                }
                else
                {
                    await Task.Delay(10);
                }
            }
        }

        private Task<Packet> ReadPacketAsync(CancellationToken ct)
        {
            var br = new BinaryReader(_stream);
            var packetLen = br.ReadUInt16();
            var data = br.ReadBytes(packetLen);
            var jsonDoc = JsonDocument.Parse(data);
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var jKind = jsonDoc.RootElement.GetProperty("Kind");
                if (jKind.ValueKind == JsonValueKind.String)
                {
                    var kind = jKind.GetString();
                    var dType = Assembly.GetExecutingAssembly()
                        .DefinedTypes
                        .FirstOrDefault(x => x.Name == kind);
                    return Task.FromResult((Packet)jsonDoc.Deserialize(dType));
                }
            }
            return Task.FromResult<Packet>(null);
        }

        public async Task SendPacketAsync(Packet packet, CancellationToken ct = default)
        {
            var packetId = GetNextPacketId();

            packet.Kind = packet.GetType().Name;
            packet.PacketId = packetId;
            var json = JsonSerializer.Serialize(packet, packet.GetType());
            var bytes = Encoding.UTF8.GetBytes(json);

            var bw = new BinaryWriter(_stream);
            bw.Write((ushort)bytes.Length);
            bw.Write(bytes);
            await _stream.FlushAsync();
        }

        private async Task<Packet> ReceivePacketAsync(int packetId, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                lock (_packetSync)
                {
                    for (var i = 0; i < _receivedPackets.Count; i++)
                    {
                        var p = _receivedPackets[i];
                        if (p.ReplyId == packetId)
                        {
                            _receivedPackets.RemoveAt(i);
                            return p;
                        }
                    }
                }
                await Task.Delay(10);
            }
        }

        public async Task<Packet> SendReceivePacketAsync(Packet packet, CancellationToken ct)
        {
            await SendPacketAsync(packet, ct);
            return await ReceivePacketAsync(packet.PacketId, ct);
        }

        public bool IsPacketAvailable => _receivedPackets.Count != 0;

        [Obsolete]
        public Task<Packet> ReceivePacketAsync()
        {
            lock (_packetSync)
            {
                for (var i = 0; i < _receivedPackets.Count; i++)
                {
                    var p = _receivedPackets[i];
                    if (p.ReplyId == null)
                    {
                        _receivedPackets.RemoveAt(i);
                        return Task.FromResult(p);
                    }
                }
            }
            return Task.FromResult<Packet>(null);
        }
    }
}
