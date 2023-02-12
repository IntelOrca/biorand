using System;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3ItemHelper : IItemHelper
    {
        public byte[] GetAmmoTypeForWeapon(byte type)
        {
            throw new NotImplementedException();
        }

        public byte[] GetDefaultWeapons(RandoConfig config)
        {
            throw new NotImplementedException();
        }

        public byte[] GetInitialItems(RandoConfig config)
        {
            throw new NotImplementedException();
        }

        public int[]? GetInventorySize(RandoConfig config)
        {
            throw new NotImplementedException();
        }

        public byte GetItemId(CommonItemKind kind)
        {
            throw new NotImplementedException();
        }

        public string GetItemName(byte type)
        {
            throw new NotImplementedException();
        }

        public double GetItemProbability(byte type)
        {
            throw new NotImplementedException();
        }

        public int GetItemQuantity(RandoConfig config, byte item)
        {
            throw new NotImplementedException();
        }

        public byte GetItemSize(byte type)
        {
            throw new NotImplementedException();
        }

        public byte GetMaxAmmoForAmmoType(byte type)
        {
            throw new NotImplementedException();
        }

        public WeaponKind GetWeaponKind(byte item)
        {
            throw new NotImplementedException();
        }

        public byte[] GetWeapons(Rng rng, RandoConfig config)
        {
            throw new NotImplementedException();
        }

        public byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config)
        {
            throw new NotImplementedException();
        }

        public bool HasInkRibbons(RandoConfig config)
        {
            throw new NotImplementedException();
        }

        public bool IsItemDocument(byte type)
        {
            throw new NotImplementedException();
        }

        public bool IsItemInfinite(byte type)
        {
            throw new NotImplementedException();
        }

        public bool IsItemTypeDiscardable(byte type)
        {
            throw new NotImplementedException();
        }

        public bool IsOptionalItem(RandoConfig config, byte type)
        {
            throw new NotImplementedException();
        }

        public bool IsWeaponCompatible(byte player, byte item)
        {
            throw new NotImplementedException();
        }
    }
}
