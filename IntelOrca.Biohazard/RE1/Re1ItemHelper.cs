using System;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.RE1
{
    internal class Re1ItemHelper : IItemHelper
    {
        public string GetItemName(byte type)
        {
            var name = new Bio1ConstantTable().GetItemName(type);
            return name
                .Remove(0, 5)
                .Replace("_", " ");
        }

        public bool IsOptionalItem(byte type)
        {
            return type == 56;
        }

        public bool IsItemTypeDiscardable(byte type)
        {
            switch (type)
            {
                case 0x33:
                case 0x34:
                case 0x35:
                case 0x36:
                case 0x37:
                case 0x38:
                case 0x39:
                case 0x3A:
                case 0x3B:
                case 0x3C:
                case 0x3D:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsItemDocument(byte type)
        {
            return false;
        }

        public byte[] GetInitialItems(RandoConfig config)
        {
            return new byte[0];
        }

        public int GetItemQuantity(RandoConfig config, byte item)
        {
            return 1;
        }

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
