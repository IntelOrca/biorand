namespace IntelOrca.Biohazard
{
    public interface IItemHelper
    {
        byte GetItemSize(byte type);
        string GetItemName(byte type);
        byte GetItemId(CommonItemKind kind);
        bool IsOptionalItem(RandoConfig config, byte type);
        bool IsItemTypeDiscardable(byte type);
        bool IsItemInfinite(byte type);
        bool IsItemDocument(byte type);
        byte[] GetInitialItems(RandoConfig config);
        int GetItemQuantity(RandoConfig config, byte item);
        byte[] GetAmmoTypeForWeapon(byte type);
        byte GetMaxAmmoForAmmoType(byte type);
        double GetItemProbability(byte type);
        byte[] GetWeapons(Rng rng, RandoConfig config);
        byte[] GetDefaultWeapons(RandoConfig config);
        byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config);
        byte[] GetWeaponGunpowder(byte weapon);
        bool IsWeaponCompatible(byte player, byte item);
        WeaponKind GetWeaponKind(byte item);
        bool HasInkRibbons(RandoConfig config);
        int[]? GetInventorySize(RandoConfig config);
    }
}
