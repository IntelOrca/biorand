namespace IntelOrca.Biohazard
{
    internal interface IDoorHelper
    {
        void Begin(RandoConfig config, GameData gameData, Map map);
        void End(RandoConfig config, GameData gameData, Map map);
    }
}
