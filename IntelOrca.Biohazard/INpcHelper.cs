namespace IntelOrca.Biohazard
{
    internal interface INpcHelper
    {
        string GetNpcName(byte type);
        string GetPlayerActor(int player);
        byte[] GetDefaultIncludeTypes(Rdt rdt);
        bool IsNpc(byte type);
        string? GetActor(byte type);
        byte[] GetSlots(byte id);
        bool IsSpareSlot(byte id);
    }
}
