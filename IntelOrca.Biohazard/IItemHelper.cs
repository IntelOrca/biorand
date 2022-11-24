namespace IntelOrca.Biohazard
{
    public interface IItemHelper
    {
        byte GetItemId(CommonItemKind kind);
        byte[] GetAmmoTypeForWeapon(byte type);
        byte GetMaxAmmoForAmmoType(byte type);
        double GetItemProbability(byte type);
        byte[] GetWeapons(Rng rng, RandoConfig config);
    }
}
