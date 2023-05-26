namespace IntelOrca.Biohazard.RE2
{
    internal class Re2DoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds() => new byte[0];

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
            // See https://github.com/IntelOrca/biorand/issues/265
            var rdt = gameData.GetRdt(new RdtId(0, 0x0D));
            if (rdt == null || rdt.Version != BioVersion.Biohazard2 || config.Player != 1)
                return;

            rdt.Nop(0x0894);
            rdt.Nop(0x08BA);
        }
    }
}
