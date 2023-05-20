using System;

namespace IntelOrca.Biohazard
{
    [Flags]
    public enum ItemAttribute
    {
        Ammo = 1 << 0,
        Gunpowder = 1 << 1,
        Heal = 1 << 2,
        InkRibbon = 1 << 3,
        Key = 1 << 4,
        Weapon = 1 << 5,
        Document = 1 << 6,
        Special = 1 << 7,
    }
}
