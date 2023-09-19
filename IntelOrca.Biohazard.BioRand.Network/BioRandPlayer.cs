using System;
using System.Net.Sockets;
using IntelOrca.Biohazard.BioRand.Network.Packets;

namespace IntelOrca.Biohazard.BioRand.Network
{
    public class BioRandPlayer
    {
        public event EventHandler<Packet> ReceivePacket;

        public TcpClient TcpClient { get; }
        public BioRandJsonStream Stream { get; }
        public string Id { get; }
        public string Name { get; set; }
        public BioRandRoom Room { get; set; }

        public bool Connected => TcpClient.Connected;

        public BioRandPlayer(TcpClient tcpClient, string id)
        {
            TcpClient = tcpClient;
            Id = id;
            Stream = new BioRandJsonStream(tcpClient.GetStream());
            Stream.ReceievePacket += Stream_ReceievePacket;
        }

        private void Stream_ReceievePacket(object sender, Packet e)
        {
            ReceivePacket?.Invoke(this, e);
        }
    }
}
