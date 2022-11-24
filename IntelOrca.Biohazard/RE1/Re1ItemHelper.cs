using System;

namespace IntelOrca.Biohazard.RE1
{
    internal class Re1ItemHelper : IItemHelper
    {
        public byte[] GetAmmoTypeForWeapon(byte type)
        {
            throw new NotImplementedException();
        }

        public byte GetItemId(CommonItemKind kind)
        {
            switch (kind)
            {
                case CommonItemKind.HandgunAmmo:
                    return 0x0B;
                case CommonItemKind.InkRibbon:
                    return 0x2F;
                case CommonItemKind.GreenHerb:
                    return 0x44;
                case CommonItemKind.RedHerb:
                    return 0x43;
                case CommonItemKind.BlueHerb:
                    return 0x45;
                case CommonItemKind.FirstAid:
                    return 0x41;
            }
            throw new NotImplementedException();
        }

        public double GetItemProbability(byte type)
        {
            return 0;
        }

        public byte GetMaxAmmoForAmmoType(byte type)
        {
            return 0;
        }

        public byte[] GetWeapons(Rng rng, RandoConfig config)
        {
            return new byte[0];
        }
    }
}
