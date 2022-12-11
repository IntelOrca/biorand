namespace IntelOrca.Biohazard
{
    public interface IItemHelper
    {
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
        bool HasInkRibbons(RandoConfig config);
        int[] GetInventorySize(RandoConfig config);
    }
}
