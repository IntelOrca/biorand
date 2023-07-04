namespace IntelOrca.Biohazard
{
    internal interface INpcHelper
    {
        string GetNpcName(byte type);
        string[] GetPlayerActors(int player);
        byte[] GetDefaultIncludeTypes(Rdt rdt);
        bool IsNpc(byte type);
        string? GetActor(byte type);
        byte[] GetSlots(RandoConfig config, byte id);
        bool IsSpareSlot(byte id);
        void CreateEmdFile(byte type, string pldPath, string baseEmdPath, string targetEmdPath, FileRepository fileRepository, Rng rng);
    }
}
