using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.RE2
{
    internal class Re2ItemHelper : IItemHelper
    {
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
                case CommonItemKind.GreenHerb:
                    return (byte)ItemType.HerbG;
                case CommonItemKind.RedHerb:
                    return (byte)ItemType.HerbR;
                case CommonItemKind.BlueHerb:
                    return (byte)ItemType.HerbB;
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

            var items = new List<ItemType>();
            if (config.Player == 0)
            {
                if (rng.Next(0, 3) >= 1)
                    items.Add(ItemType.HandgunParts);
                if (enemyDifficulty >= 2 || rng.Next(0, 2) >= 1)
                {
                    items.Add(ItemType.Shotgun);
                    if (rng.Next(0, 2) >= 1)
                        items.Add(ItemType.ShotgunParts);
                }
                if (enemyDifficulty >= 3 || rng.Next(0, 3) >= 1)
                {
                    items.Add(ItemType.Magnum);
                    if (rng.Next(0, 2) >= 1)
                        items.Add(ItemType.MagnumParts);
                }
                if (rng.Next(0, 2) == 0)
                    items.Add(ItemType.SMG);
                if (rng.Next(0, 2) == 0)
                    items.Add(ItemType.Flamethrower);
            }
            else
            {
                if (enemyDifficulty >= 2 || rng.Next(0, 3) >= 1)
                    items.Add(ItemType.Bowgun);
                if (enemyDifficulty >= 3 || rng.Next(0, 3) >= 1)
                    items.Add(rng.NextOf(ItemType.GrenadeLauncherExplosive, ItemType.GrenadeLauncherFlame, ItemType.GrenadeLauncherAcid));
                if (rng.Next(0, 2) == 0)
                    items.Add(ItemType.SMG);
                if (rng.Next(0, 2) == 0)
                    items.Add(ItemType.Sparkshot);
                if (rng.Next(0, 2) == 0)
                    items.Add(ItemType.ColtSAA);
            }
            if (rng.Next(0, 2) == 0)
                items.Add(ItemType.RocketLauncher);
            return items.Select(x => (byte)x).ToArray();
        }

        public bool HasInkRibbons(RandoConfig config)
        {
            return true;
        }

        public int[]? GetInventorySize(RandoConfig config)
        {
            return null;
        }
    }
}
