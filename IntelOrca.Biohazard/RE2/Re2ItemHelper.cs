using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.RE2
{
    internal class Re2ItemHelper : IItemHelper
    {
        private const bool g_claireWeaponFix = true;

        public byte GetItemSize(byte type)
        {
            switch ((ItemType)type)
            {
                case ItemType.Flamethrower:
                case ItemType.Sparkshot:
                case ItemType.RocketLauncher:
                case ItemType.SMG:
                    return 2;
                default:
                    return 1;
            }
        }

        public string GetItemName(byte type)
        {
            return Items.GetItemName(type);
        }

        public byte GetItemId(CommonItemKind kind)
        {
            switch (kind)
            {
                case CommonItemKind.HandgunAmmo:
                    return (byte)ItemType.HandgunAmmo;
                case CommonItemKind.InkRibbon:
                    return (byte)ItemType.InkRibbon;
                case CommonItemKind.HerbG:
                    return (byte)ItemType.HerbG;
                case CommonItemKind.HerbGG:
                    return (byte)ItemType.HerbGG;
                case CommonItemKind.HerbGGG:
                    return (byte)ItemType.HerbGGG;
                case CommonItemKind.HerbR:
                    return (byte)ItemType.HerbR;
                case CommonItemKind.HerbGR:
                    return (byte)ItemType.HerbGR;
                case CommonItemKind.HerbB:
                    return (byte)ItemType.HerbB;
                case CommonItemKind.HerbGB:
                    return (byte)ItemType.HerbGB;
                case CommonItemKind.HerbGGB:
                    return (byte)ItemType.HerbGGB;
                case CommonItemKind.HerbGRB:
                    return (byte)ItemType.HerbGRB;
                case CommonItemKind.FirstAid:
                    return (byte)ItemType.FAidSpray;
                case CommonItemKind.Knife:
                    return (byte)ItemType.Knife;
            }
            throw new NotImplementedException();
        }

        public bool IsOptionalItem(RandoConfig config, byte type)
        {
            if (config.RandomDoors)
                return false;

            switch ((ItemType)type)
            {
                case ItemType.FilmA:
                case ItemType.FilmB:
                case ItemType.FilmC:
                case ItemType.FilmD:
                case ItemType.Cord:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsItemTypeDiscardable(byte type)
        {
            switch ((ItemType)type)
            {
                case ItemType.SmallKey: // Small keys can be stacked
                case ItemType.CabinKey:
                case ItemType.SpadeKey:
                case ItemType.DiamondKey:
                case ItemType.HeartKey:
                case ItemType.ClubKey:
                case ItemType.PowerRoomKey:
                case ItemType.UmbrellaKeyCard:
                case ItemType.MasterKey:
                case ItemType.PlatformKey:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsItemInfinite(byte type)
        {
            return false;
        }

        public bool IsItemDocument(byte type)
        {
            return type > (byte)ItemType.PlatformKey;
        }

        public byte[] GetInitialItems(RandoConfig config)
        {
            if (config.Player == 0)
            {
                return new[] { (byte)ItemType.Lighter };
            }
            return new byte[0];
        }

        public int GetItemQuantity(RandoConfig config, byte item)
        {
            if (item == (byte)ItemType.RedJewel)
                return 2;

            if (item == (byte)ItemType.SmallKey && !config.RandomDoors)
            {
                return config.Scenario == 1 ? 3 : 2;
            }

            return 1;
        }

        public byte[] GetAmmoTypeForWeapon(byte type)
        {
            switch ((ItemType)type)
            {
                case ItemType.HandgunLeon:
                case ItemType.HandgunClaire:
                case ItemType.CustomHandgun:
                case ItemType.ColtSAA:
                case ItemType.Beretta:
                    return new[] { (byte)ItemType.HandgunAmmo };
                case ItemType.Shotgun:
                    return new[] { (byte)ItemType.ShotgunAmmo };
                case ItemType.Magnum:
                case ItemType.CustomMagnum:
                    return new[] { (byte)ItemType.MagnumAmmo };
                case ItemType.Bowgun:
                    return new[] { (byte)ItemType.BowgunAmmo };
                case ItemType.Sparkshot:
                    return new[] { (byte)ItemType.SparkshotAmmo };
                case ItemType.Flamethrower:
                    return new[] { (byte)ItemType.FuelTank };
                case ItemType.SMG:
                    return new[] { (byte)ItemType.SMGAmmo };
                case ItemType.GrenadeLauncherFlame:
                case ItemType.GrenadeLauncherExplosive:
                case ItemType.GrenadeLauncherAcid:
                    return new[] {
                        (byte)ItemType.FlameRounds,
                        (byte)ItemType.ExplosiveRounds,
                        (byte)ItemType.AcidRounds
                    };
                default:
                    return new byte[0];
            }
        }

        public byte GetMaxAmmoForAmmoType(byte type)
        {
            switch ((ItemType)type)
            {
                default:
                    return 1;

                case ItemType.InkRibbon:
                    return 3;

                case ItemType.HandgunLeon:
                case ItemType.CustomHandgun:
                    return 18;
                case ItemType.HandgunClaire:
                    return 13;
                case ItemType.Beretta:
                    return 15;
                case ItemType.ColtSAA:
                    return 6;
                case ItemType.Bowgun:
                    return 18;
                case ItemType.Shotgun:
                    return 5;
                case ItemType.CustomShotgun:
                    return 7;
                case ItemType.Magnum:
                case ItemType.CustomMagnum:
                    return 8;
                case ItemType.GrenadeLauncherAcid:
                case ItemType.GrenadeLauncherExplosive:
                case ItemType.GrenadeLauncherFlame:
                    return 6;
                case ItemType.SMG:
                case ItemType.Sparkshot:
                case ItemType.Flamethrower:
                    return 100;

                case ItemType.HandgunAmmo:
                    return 60;
                case ItemType.ShotgunAmmo:
                case ItemType.BowgunAmmo:
                    return 30;
                case ItemType.MagnumAmmo:
                case ItemType.ExplosiveRounds:
                case ItemType.AcidRounds:
                case ItemType.FlameRounds:
                    return 10;
                case ItemType.FuelTank:
                case ItemType.SparkshotAmmo:
                case ItemType.SMGAmmo:
                    return 100;
            }
        }

        public double GetItemProbability(byte type)
        {
            switch ((ItemType)type)
            {
                case ItemType.HandgunAmmo:
                    return 0.3;
                case ItemType.ShotgunAmmo:
                case ItemType.BowgunAmmo:
                    return 0.2;
                case ItemType.MagnumAmmo:
                case ItemType.ExplosiveRounds:
                case ItemType.AcidRounds:
                case ItemType.FlameRounds:
                case ItemType.FuelTank:
                case ItemType.SparkshotAmmo:
                case ItemType.SMGAmmo:
                    return 0.1;
                default:
                    return 0;
            }
        }

        public byte[] GetWeapons(Rng rng, RandoConfig config)
        {
            var enemyDifficulty = config.RandomEnemies ? config.EnemyDifficulty : 0;
            var glWeapon = rng.NextOf(ItemType.GrenadeLauncherExplosive, ItemType.GrenadeLauncherFlame, ItemType.GrenadeLauncherAcid);

            var items = new List<ItemType>();
            ItemType[] allWeapons;
            if (config.Player == 0 || g_claireWeaponFix)
            {
                allWeapons = new[] {
                    ItemType.HandgunLeon,
                    ItemType.HandgunClaire,
                    ItemType.SMG,
                    ItemType.Flamethrower,
                    ItemType.ColtSAA,
                    ItemType.Beretta,
                    ItemType.Sparkshot,
                    ItemType.Bowgun,
                    ItemType.Shotgun,
                    ItemType.Magnum,
                    ItemType.RocketLauncher,
                    glWeapon
                };

                // Guarantee good weapons for higher enemy difficulty
                if (enemyDifficulty >= 2)
                    items.Add(rng.NextOf(ItemType.Shotgun, ItemType.Bowgun));
                if (enemyDifficulty >= 3)
                    items.Add(rng.NextOf(ItemType.Magnum, glWeapon));
            }
            else
            {
                allWeapons = new[] {
                    ItemType.HandgunClaire,
                    ItemType.SMG,
                    ItemType.ColtSAA,
                    ItemType.Beretta,
                    ItemType.Sparkshot,
                    ItemType.Bowgun,
                    ItemType.RocketLauncher,
                    glWeapon
                };

                // Guarantee good weapons for higher enemy difficulty
                if (enemyDifficulty >= 2)
                    items.Add(ItemType.Bowgun);
                if (enemyDifficulty >= 3)
                    items.Add(glWeapon);
            }

            // Remaining weapons
            foreach (var weapon in allWeapons)
            {
                if (rng.NextProbability(50) && !items.Contains(weapon))
                {
                    items.Add(weapon);
                }
            }

            return items.Select(x => (byte)x).ToArray();
        }

        public byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config)
        {
            switch ((ItemType)weapon)
            {
                case ItemType.HandgunLeon:
                    if (rng.NextProbability(33))
                        return (byte)ItemType.HandgunParts;
                    break;
                case ItemType.Shotgun:
                    if (rng.NextProbability(50))
                        return (byte)ItemType.ShotgunParts;
                    break;
                case ItemType.Magnum:
                    if (rng.NextProbability(50))
                        return (byte)ItemType.MagnumParts;
                    break;
            }
            return null;
        }

        public bool IsWeaponCompatible(byte player, byte item)
        {
            if (!g_claireWeaponFix && player == 1)
            {
                switch ((ItemType)item)
                {
                    case ItemType.Flamethrower:
                    case ItemType.Shotgun:
                    case ItemType.Magnum:
                        return false;
                }
            }
            return true;
        }

    public bool HasInkRibbons(RandoConfig config)
    {
        return true;
    }

    public int[]? GetInventorySize(RandoConfig config)
    {
        if (config.Player == 0)
            return new int[] { 8 };
        else
            return new int[] { 8 };
    }
}
}
