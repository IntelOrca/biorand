using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.Script;

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
            }
            throw new NotImplementedException();
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
                case ItemType.RocketLauncher:
                    return 5;
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
            var items = new List<ItemType>();
            if (config.Player == 0)
            {
                if (rng.Next(0, 3) >= 1)
                    items.Add(ItemType.HandgunParts);
                if (config.EnemyDifficulty >= 2 || rng.Next(0, 2) >= 1)
                {
                    items.Add(ItemType.Shotgun);
                    if (rng.Next(0, 2) >= 1)
                        items.Add(ItemType.ShotgunParts);
                }
                if (config.EnemyDifficulty >= 3 || rng.Next(0, 3) >= 1)
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
                if (config.EnemyDifficulty >= 2 || rng.Next(0, 3) >= 1)
                    items.Add(ItemType.Bowgun);
                if (config.EnemyDifficulty >= 3 || rng.Next(0, 3) >= 1)
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
            return items.Cast<byte>().ToArray();
        }
    }
}
