using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.RE2
{
    internal class Re2ItemHelper : IItemHelper
    {
        private const bool g_leonWeaponFix = true;
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

        public ItemAttribute GetItemAttributes(byte item)
        {
            switch ((ItemType)item)
            {
                case ItemType.None:
                    return 0;
                case ItemType.Knife:
                case ItemType.HandgunLeon:
                case ItemType.HandgunClaire:
                case ItemType.CustomHandgun:
                case ItemType.Magnum:
                case ItemType.CustomMagnum:
                case ItemType.Shotgun:
                case ItemType.CustomShotgun:
                case ItemType.GrenadeLauncherExplosive:
                case ItemType.GrenadeLauncherFlame:
                case ItemType.GrenadeLauncherAcid:
                case ItemType.Bowgun:
                case ItemType.ColtSAA:
                case ItemType.Sparkshot:
                case ItemType.SMG:
                case ItemType.Flamethrower:
                case ItemType.RocketLauncher:
                case ItemType.GatlingGun:
                case ItemType.Beretta:
                    return ItemAttribute.Weapon;
                case ItemType.HandgunAmmo:
                case ItemType.ShotgunAmmo:
                case ItemType.MagnumAmmo:
                case ItemType.FuelTank:
                case ItemType.ExplosiveRounds:
                case ItemType.FlameRounds:
                case ItemType.AcidRounds:
                case ItemType.SMGAmmo:
                case ItemType.SparkshotAmmo:
                case ItemType.BowgunAmmo:
                    return ItemAttribute.Ammo;
                case ItemType.InkRibbon:
                    return ItemAttribute.InkRibbon;
                case ItemType.SmallKey:
                    return ItemAttribute.Key;
                case ItemType.HandgunParts:
                case ItemType.MagnumParts:
                case ItemType.ShotgunParts:
                    return ItemAttribute.Weapon;
                case ItemType.FAidSpray:
                    return ItemAttribute.Heal;
                case ItemType.AntivirusBomb:
                case ItemType.ChemicalACw32:
                    return ItemAttribute.Key;
                case ItemType.HerbG:
                case ItemType.HerbR:
                case ItemType.HerbB:
                case ItemType.HerbGG:
                case ItemType.HerbGR:
                case ItemType.HerbGB:
                case ItemType.HerbGGG:
                case ItemType.HerbGGB:
                case ItemType.HerbGRB:
                    return ItemAttribute.Heal;
                case ItemType.Lighter:
                case ItemType.Lockpick:
                    return ItemAttribute.Key;
                case ItemType.PhotoSherry:
                    return 0;
                case ItemType.ValveHandle:
                case ItemType.RedJewel:
                case ItemType.RedCard:
                case ItemType.BlueCard:
                case ItemType.SerpentStone:
                case ItemType.JaguarStone:
                case ItemType.JaguarStoneL:
                case ItemType.JaguarStoneR:
                case ItemType.EagleStone:
                case ItemType.BishopPlug:
                case ItemType.RookPlug:
                case ItemType.KnightPlug:
                case ItemType.KingPlug:
                case ItemType.WeaponBoxKey:
                case ItemType.Detonator:
                case ItemType.C4:
                case ItemType.C4Detonator:
                case ItemType.Crank:
                case ItemType.FilmA:
                case ItemType.FilmB:
                case ItemType.FilmC:
                case ItemType.UnicornMedal:
                case ItemType.EagleMedal:
                case ItemType.WolfMedal:
                case ItemType.Cog:
                case ItemType.ManholeOpener:
                case ItemType.MainFuse:
                case ItemType.FuseCase:
                case ItemType.Vaccine:
                case ItemType.VaccineCart:
                case ItemType.FilmD:
                case ItemType.VaccineBase:
                case ItemType.GVirus:
                case ItemType.SpecialKey:
                case ItemType.JointPlugBlue:
                case ItemType.JointPlugRed:
                case ItemType.Cord:
                    return ItemAttribute.Key;
                case ItemType.PhotoAda:
                    return 0;
                case ItemType.CabinKey:
                case ItemType.SpadeKey:
                case ItemType.DiamondKey:
                case ItemType.HeartKey:
                case ItemType.ClubKey:
                case ItemType.DownKey:
                case ItemType.UpKey:
                case ItemType.PowerRoomKey:
                case ItemType.MODisk:
                case ItemType.UmbrellaKeyCard:
                case ItemType.MasterKey:
                case ItemType.PlatformKey:
                    return ItemAttribute.Key;
                default:
                    return ItemAttribute.Document;
            }
        }

        public byte[] GetInitialKeyItems(RandoConfig config)
        {
            return new[] { (byte)(config.Player == 0 ? ItemType.Lighter : ItemType.SmallKey) };
        }

        public byte[] GetDefaultWeapons(RandoConfig config)
        {
            if (config.Player == 0)
                return new[] { (byte)ItemType.HandgunLeon };
            else
                return new[] { (byte)ItemType.HandgunClaire };
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
            var allWeapons = new[] {
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
                rng.NextOf(ItemType.GrenadeLauncherExplosive, ItemType.GrenadeLauncherFlame, ItemType.GrenadeLauncherAcid)
            };
            return allWeapons
                .Where(x => IsWeaponCompatible((byte)config.Player, (byte)x))
                .Select(x => (byte)x)
                .ToArray();
        }

        public byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config)
        {
            switch ((ItemType)weapon)
            {
                case ItemType.HandgunLeon:
                    return (byte)ItemType.HandgunParts;
                case ItemType.Shotgun:
                    return (byte)ItemType.ShotgunParts;
                case ItemType.Magnum:
                    return (byte)ItemType.MagnumParts;
            }
            return null;
        }

        public bool IsWeaponCompatible(byte player, byte item)
        {
            if (player == 0 && !g_leonWeaponFix)
            {
                switch ((ItemType)item)
                {
                    case ItemType.HandgunClaire:
                    case ItemType.Bowgun:
                    case ItemType.GrenadeLauncherAcid:
                    case ItemType.GrenadeLauncherExplosive:
                    case ItemType.GrenadeLauncherFlame:
                    case ItemType.ColtSAA:
                    case ItemType.Sparkshot:
                    case ItemType.Beretta:
                        return false;
                }
            }
            if (player == 1 && !g_claireWeaponFix)
            {
                switch ((ItemType)item)
                {
                    case ItemType.HandgunLeon:
                    case ItemType.CustomHandgun:
                    case ItemType.Shotgun:
                    case ItemType.CustomShotgun:
                    case ItemType.Magnum:
                    case ItemType.CustomMagnum:
                    case ItemType.Beretta:
                    case ItemType.Flamethrower:
                        return false;
                }
            }
            return true;
        }

        public WeaponKind GetWeaponKind(byte item)
        {
            switch ((ItemType)item)
            {
                case ItemType.HandgunLeon:
                case ItemType.HandgunClaire:
                case ItemType.CustomHandgun:
                case ItemType.Beretta:
                case ItemType.ColtSAA:
                    return WeaponKind.Sidearm;
                case ItemType.Shotgun:
                case ItemType.CustomShotgun:
                case ItemType.Bowgun:
                case ItemType.SMG:
                case ItemType.Flamethrower:
                case ItemType.Sparkshot:
                    return WeaponKind.Primary;
                case ItemType.Magnum:
                case ItemType.CustomMagnum:
                case ItemType.GrenadeLauncherAcid:
                case ItemType.GrenadeLauncherExplosive:
                case ItemType.GrenadeLauncherFlame:
                    return WeaponKind.Powerful;
                default:
                    return WeaponKind.None;
            }
        }

        public bool HasInkRibbons(RandoConfig config)
        {
            return true;
        }

        public bool HasGunPowder(RandoConfig config) => false;

        public int[]? GetInventorySize(RandoConfig config)
        {
            if (config.Player == 0)
                return new int[] { 8 };
            else
                return new int[] { 8 };
        }

        public byte[] GetWeaponGunpowder(byte weapon)
        {
            return new byte[0];
        }
    }
}
