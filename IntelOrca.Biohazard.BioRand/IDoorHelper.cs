namespace IntelOrca.Biohazard.BioRand
{
    internal interface IDoorHelper
    {
        byte[] GetReservedLockIds();
        void Begin(RandoConfig config, GameData gameData, Map map);
        void End(RandoConfig config, GameData gameData, Map map);
        string? GetRoomDisplayName(RdtId id);
    }
}
