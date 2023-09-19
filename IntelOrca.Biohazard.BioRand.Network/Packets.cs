namespace IntelOrca.Biohazard.BioRand.Network.Packets
{
    public class Packet
    {
        public string Kind { get; set; }
        public int PacketId { get; set; }
        public int? ReplyId { get; set; }
    }

    public class ErrorPacket : Packet
    {
        public string Message { get; set; }
    }

    public class AuthenticatePacket : Packet
    {
        public string ClientName { get; set; }
        public int ClientVersion { get; set; }
    }

    public class AuthenticatedPacket : Packet
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public int ServerVersion { get; set; }
    }

    public class DisconnectPacket : Packet
    {
    }

    public class CreateRoomPacket : Packet
    {
    }

    public class JoinRoomPacket : Packet
    {
        public string RoomId { get; set; }
    }

    public class LeaveRoomPacket : Packet
    {
    }

    public class RoomDetailsPacket : Packet
    {
        public string RoomId { get; set; }
        public string[] Players { get; set; }
    }

    public class ItemBoxPacket : Packet
    {
    }
}
