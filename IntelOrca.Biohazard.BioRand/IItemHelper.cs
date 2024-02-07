namespace IntelOrca.Biohazard.BioRand
{
    public interface IItemHelper
    {
        byte GetItemSize(byte type);
        string GetItemName(byte type);
        byte GetItemId(CommonItemKind kind);
        bool IsOptionalItem(RandoConfig config, byte type);
        bool IsRe2ItemIdsDiscardable(byte type);
        bool IsItemInfinite(byte type);
        ItemAttribute GetItemAttributes(byte item);
        byte[] GetInitialKeyItems(RandoConfig config);
        int GetItemQuantity(RandoConfig config, byte item);
        byte[] GetAmmoTypeForWeapon(byte type);
        byte GetMaxAmmoForAmmoType(byte type);
        double GetItemProbability(byte type);
        byte[] GetWeapons(Rng rng, RandoConfig config);
        string[] GetWeaponNames();
        byte[] GetDefaultWeapons(RandoConfig config);
        byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config);
        byte[] GetWeaponGunpowder(byte weapon);
        bool IsWeaponCompatible(byte player, byte item);
        WeaponKind GetWeaponKind(byte item);
        bool HasInkRibbons(RandoConfig config);
        bool HasGunPowder(RandoConfig config);
        int[]? GetInventorySize(RandoConfig config);
        string GetFriendlyItemName(byte type);
    }
}
