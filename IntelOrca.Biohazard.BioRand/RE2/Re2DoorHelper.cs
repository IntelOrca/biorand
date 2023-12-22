namespace IntelOrca.Biohazard.BioRand.RE2
{
    internal class Re2DoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds() => new byte[0];

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            Nop(0x10B, 0x1756, 0x175E);
            Nop(0x10C, 0x1AB4);
            Nop(0x20B, 0x3762, 0x3754);
            Nop(0x20D, 0x113C);
            Nop(0x20F, 0x182A, 0x182A);
            Nop(0x103, 0x36FE);
            Nop(0x108, 0x1B0A);
            Nop(0x118, 0x1EF6);
            Nop(0x208, 0x14EC, 0x14F4);
            Nop(0x210, 0x135E, 0x12E8);
            Nop(0x211, 0x177E);
            Nop(0x408, 0x2974);
            Nop(0x615, 0x2FD8, 0x3562);
            Nop(0x60B, 0x1432);

            FixDarkRoomLocker(config, gameData);

            void Nop(ushort rdtId, int address0, int address1 = -1)
            {
                var rdt = gameData.GetRdt(RdtId.FromInteger(rdtId));
                if (config.Player == 0)
                    rdt?.Nop(address0);
                else
                    rdt?.Nop(address1 == -1 ? address0 : address1);
            }
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

        private void FixDarkRoomLocker(RandoConfig config, GameData gameData)
        {
            var rdt = gameData.GetRdt(new RdtId(1, 0x08));
            if (rdt != null)
            {
                if (config.Player == 0)
                {
                    // Allow Leon to use Claire's locker and disable outfit change
                    rdt.Nop(0x14A2, 0x14AA);
                    rdt.Nop(0x14E6);
                    rdt.Nop(0x1758, 0x1762);
                    rdt.Nop(0x178E, 0x1798);
                    rdt.Nop(0x1940, 0x197E);
                    rdt.Nop(0x198A, 0x1990);
                    rdt.Nop(0x1996, 0x19B8);
                }
                else
                {
                    // Disable outfit change
                    rdt.Nop(0x1948, 0x1986);
                    rdt.Nop(0x1992, 0x1998);
                    rdt.Nop(0x199E, 0x19C0);
                }
            }
        }
    }
}
