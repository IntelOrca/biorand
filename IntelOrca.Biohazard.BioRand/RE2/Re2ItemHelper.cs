using System;
using System.Linq;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.BioRand.RE2
{
    public class Re2ItemHelper : IItemHelper
    {
        private const bool g_leonWeaponFix = true;
        private const bool g_claireWeaponFix = true;

        public byte GetItemSize(byte type)
        {
            switch (type)
            {
                case Re2ItemIds.Flamethrower:
                case Re2ItemIds.Sparkshot:
                case Re2ItemIds.RocketLauncher:
                case Re2ItemIds.SMG:
                    return 2;
                default:
                    return 1;
            }
        }

        public string GetItemName(byte type)
        {
            var name = new Bio2ConstantTable().GetItemName(type);
            return name
                .Remove(0, 5)
                .Replace("_", " ");
        }

        public byte GetItemId(CommonItemKind kind)
        {
            switch (kind)
            {
                case CommonItemKind.HandgunAmmo:
                    return Re2ItemIds.HandgunAmmo;
                case CommonItemKind.InkRibbon:
                    return Re2ItemIds.InkRibbon;
                case CommonItemKind.HerbG:
                    return Re2ItemIds.HerbG;
                case CommonItemKind.HerbGG:
                    return Re2ItemIds.HerbGG;
                case CommonItemKind.HerbGGG:
                    return Re2ItemIds.HerbGGG;
                case CommonItemKind.HerbR:
                    return Re2ItemIds.HerbR;
                case CommonItemKind.HerbGR:
                    return Re2ItemIds.HerbGR;
                case CommonItemKind.HerbB:
                    return Re2ItemIds.HerbB;
                case CommonItemKind.HerbGB:
                    return Re2ItemIds.HerbGB;
                case CommonItemKind.HerbGGB:
                    return Re2ItemIds.HerbGGB;
                case CommonItemKind.HerbGRB:
                    return Re2ItemIds.HerbGRB;
                case CommonItemKind.FirstAid:
                    return Re2ItemIds.FAidSpray;
                case CommonItemKind.Knife:
                    return Re2ItemIds.Knife;
            }
            throw new NotImplementedException();
        }

        public bool IsOptionalItem(RandoConfig config, byte type)
        {
            if (config.RandomDoors)
                return false;

            switch (type)
            {
                case Re2ItemIds.FilmA:
                case Re2ItemIds.FilmB:
                case Re2ItemIds.FilmC:
                case Re2ItemIds.FilmD:
                case Re2ItemIds.Cord:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsRe2ItemIdsDiscardable(byte type)
        {
            switch (type)
            {
                case Re2ItemIds.SmallKey: // Small keys can be stacked
                case Re2ItemIds.CabinKey:
                case Re2ItemIds.SpadeKey:
                case Re2ItemIds.DiamondKey:
                case Re2ItemIds.HeartKey:
                case Re2ItemIds.ClubKey:
                case Re2ItemIds.PowerRoomKey:
                case Re2ItemIds.UmbrellaKeyCard:
                case Re2ItemIds.MasterKey:
                case Re2ItemIds.PlatformKey:
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
            switch (item)
            {
                case Re2ItemIds.None:
                    return 0;
                case Re2ItemIds.Knife:
                case Re2ItemIds.HandgunLeon:
                case Re2ItemIds.HandgunClaire:
                case Re2ItemIds.CustomHandgun:
                case Re2ItemIds.Magnum:
                case Re2ItemIds.CustomMagnum:
                case Re2ItemIds.Shotgun:
                case Re2ItemIds.CustomShotgun:
                case Re2ItemIds.GrenadeLauncherExplosive:
                case Re2ItemIds.GrenadeLauncherFlame:
                case Re2ItemIds.GrenadeLauncherAcid:
                case Re2ItemIds.Bowgun:
                case Re2ItemIds.ColtSAA:
                case Re2ItemIds.Sparkshot:
                case Re2ItemIds.SMG:
                case Re2ItemIds.Flamethrower:
                case Re2ItemIds.RocketLauncher:
                case Re2ItemIds.GatlingGun:
                case Re2ItemIds.Beretta:
                    return ItemAttribute.Weapon;
                case Re2ItemIds.HandgunAmmo:
                case Re2ItemIds.ShotgunAmmo:
                case Re2ItemIds.MagnumAmmo:
                case Re2ItemIds.FuelTank:
                case Re2ItemIds.ExplosiveRounds:
                case Re2ItemIds.FlameRounds:
                case Re2ItemIds.AcidRounds:
                case Re2ItemIds.SMGAmmo:
                case Re2ItemIds.SparkshotAmmo:
                case Re2ItemIds.BowgunAmmo:
                    return ItemAttribute.Ammo;
                case Re2ItemIds.InkRibbon:
                    return ItemAttribute.InkRibbon;
                case Re2ItemIds.SmallKey:
                    return ItemAttribute.Key;
                case Re2ItemIds.HandgunParts:
                case Re2ItemIds.MagnumParts:
                case Re2ItemIds.ShotgunParts:
                    return ItemAttribute.Weapon;
                case Re2ItemIds.FAidSpray:
                    return ItemAttribute.Heal;
                case Re2ItemIds.AntivirusBomb:
                case Re2ItemIds.ChemicalACw32:
                    return ItemAttribute.Key;
                case Re2ItemIds.HerbG:
                case Re2ItemIds.HerbR:
                case Re2ItemIds.HerbB:
                case Re2ItemIds.HerbGG:
                case Re2ItemIds.HerbGR:
                case Re2ItemIds.HerbGB:
                case Re2ItemIds.HerbGGG:
                case Re2ItemIds.HerbGGB:
                case Re2ItemIds.HerbGRB:
                    return ItemAttribute.Heal;
                case Re2ItemIds.Lighter:
                case Re2ItemIds.Lockpick:
                    return ItemAttribute.Key;
                case Re2ItemIds.PhotoSherry:
                    return 0;
                case Re2ItemIds.ValveHandle:
                case Re2ItemIds.RedJewel:
                case Re2ItemIds.RedCard:
                case Re2ItemIds.BlueCard:
                case Re2ItemIds.SerpentStone:
                case Re2ItemIds.JaguarStone:
                case Re2ItemIds.JaguarStoneL:
                case Re2ItemIds.JaguarStoneR:
                case Re2ItemIds.EagleStone:
                case Re2ItemIds.BishopPlug:
                case Re2ItemIds.RookPlug:
                case Re2ItemIds.KnightPlug:
                case Re2ItemIds.KingPlug:
                case Re2ItemIds.WeaponBoxKey:
                case Re2ItemIds.Detonator:
                case Re2ItemIds.C4:
                case Re2ItemIds.C4Detonator:
                case Re2ItemIds.Crank:
                case Re2ItemIds.FilmA:
                case Re2ItemIds.FilmB:
                case Re2ItemIds.FilmC:
                case Re2ItemIds.UnicornMedal:
                case Re2ItemIds.EagleMedal:
                case Re2ItemIds.WolfMedal:
                case Re2ItemIds.Cog:
                case Re2ItemIds.ManholeOpener:
                case Re2ItemIds.MainFuse:
                case Re2ItemIds.FuseCase:
                case Re2ItemIds.Vaccine:
                case Re2ItemIds.VaccineCart:
                case Re2ItemIds.FilmD:
                case Re2ItemIds.VaccineBase:
                case Re2ItemIds.GVirus:
                case Re2ItemIds.SpecialKey:
                case Re2ItemIds.JointPlugBlue:
                case Re2ItemIds.JointPlugRed:
                case Re2ItemIds.Cord:
                    return ItemAttribute.Key;
                case Re2ItemIds.PhotoAda:
                    return 0;
                case Re2ItemIds.CabinKey:
                case Re2ItemIds.SpadeKey:
                case Re2ItemIds.DiamondKey:
                case Re2ItemIds.HeartKey:
                case Re2ItemIds.ClubKey:
                case Re2ItemIds.DownKey:
                case Re2ItemIds.UpKey:
                case Re2ItemIds.PowerRoomKey:
                case Re2ItemIds.MODisk:
                case Re2ItemIds.UmbrellaKeyCard:
                case Re2ItemIds.MasterKey:
                case Re2ItemIds.PlatformKey:
                    return ItemAttribute.Key;
                default:
                    return ItemAttribute.Document;
            }
        }

        public byte[] GetInitialKeyItems(RandoConfig config)
        {
            return new[] { (byte)(config.Player == 0 ? Re2ItemIds.Lighter : Re2ItemIds.SmallKey) };
        }

        public byte[] GetDefaultWeapons(RandoConfig config)
        {
            if (config.Player == 0)
                return new[] { Re2ItemIds.HandgunLeon };
            else
                return new[] { Re2ItemIds.HandgunClaire };
        }

        public int GetItemQuantity(RandoConfig config, byte item)
        {
            if (item == Re2ItemIds.RedJewel)
                return 2;

            if (item == Re2ItemIds.SmallKey && !config.RandomDoors)
            {
                return config.Scenario == 1 ? 3 : 2;
            }

            return 1;
        }

        public byte[] GetAmmoTypeForWeapon(byte type)
        {
            switch (type)
            {
                case Re2ItemIds.HandgunLeon:
                case Re2ItemIds.HandgunClaire:
                case Re2ItemIds.CustomHandgun:
                case Re2ItemIds.ColtSAA:
                case Re2ItemIds.Beretta:
                    return new[] { Re2ItemIds.HandgunAmmo };
                case Re2ItemIds.Shotgun:
                    return new[] { Re2ItemIds.ShotgunAmmo };
                case Re2ItemIds.Magnum:
                case Re2ItemIds.CustomMagnum:
                    return new[] { Re2ItemIds.MagnumAmmo };
                case Re2ItemIds.Bowgun:
                    return new[] { Re2ItemIds.BowgunAmmo };
                case Re2ItemIds.Sparkshot:
                    return new[] { Re2ItemIds.SparkshotAmmo };
                case Re2ItemIds.Flamethrower:
                    return new[] { Re2ItemIds.FuelTank };
                case Re2ItemIds.SMG:
                    return new[] { Re2ItemIds.SMGAmmo };
                case Re2ItemIds.GrenadeLauncherFlame:
                case Re2ItemIds.GrenadeLauncherExplosive:
                case Re2ItemIds.GrenadeLauncherAcid:
                    return new[] {
                        Re2ItemIds.FlameRounds,
                        Re2ItemIds.ExplosiveRounds,
                        Re2ItemIds.AcidRounds
                    };
                default:
                    return new byte[0];
            }
        }

        public byte GetMaxAmmoForAmmoType(byte type)
        {
            switch (type)
            {
                default:
                    return 1;

                case Re2ItemIds.InkRibbon:
                    return 3;

                case Re2ItemIds.HandgunLeon:
                case Re2ItemIds.CustomHandgun:
                    return 18;
                case Re2ItemIds.HandgunClaire:
                    return 13;
                case Re2ItemIds.Beretta:
                    return 15;
                case Re2ItemIds.ColtSAA:
                    return 6;
                case Re2ItemIds.Bowgun:
                    return 18;
                case Re2ItemIds.Shotgun:
                    return 5;
                case Re2ItemIds.CustomShotgun:
                    return 7;
                case Re2ItemIds.Magnum:
                case Re2ItemIds.CustomMagnum:
                    return 8;
                case Re2ItemIds.GrenadeLauncherAcid:
                case Re2ItemIds.GrenadeLauncherExplosive:
                case Re2ItemIds.GrenadeLauncherFlame:
                    return 6;
                case Re2ItemIds.SMG:
                case Re2ItemIds.Sparkshot:
                case Re2ItemIds.Flamethrower:
                    return 100;

                case Re2ItemIds.HandgunAmmo:
                    return 60;
                case Re2ItemIds.ShotgunAmmo:
                case Re2ItemIds.BowgunAmmo:
                    return 30;
                case Re2ItemIds.MagnumAmmo:
                case Re2ItemIds.ExplosiveRounds:
                case Re2ItemIds.AcidRounds:
                case Re2ItemIds.FlameRounds:
                    return 10;
                case Re2ItemIds.FuelTank:
                case Re2ItemIds.SparkshotAmmo:
                case Re2ItemIds.SMGAmmo:
                    return 100;
            }
        }

        public double GetItemProbability(byte type)
        {
            switch (type)
            {
                case Re2ItemIds.HandgunAmmo:
                    return 0.3;
                case Re2ItemIds.ShotgunAmmo:
                case Re2ItemIds.BowgunAmmo:
                    return 0.2;
                case Re2ItemIds.MagnumAmmo:
                case Re2ItemIds.ExplosiveRounds:
                case Re2ItemIds.AcidRounds:
                case Re2ItemIds.FlameRounds:
                case Re2ItemIds.FuelTank:
                case Re2ItemIds.SparkshotAmmo:
                case Re2ItemIds.SMGAmmo:
                    return 0.1;
                default:
                    return 0;
            }
        }

        public byte[] GetWeapons(Rng rng, RandoConfig config)
        {
            var allWeapons = new[] {
                Re2ItemIds.HandgunLeon,
                Re2ItemIds.HandgunClaire,
                Re2ItemIds.SMG,
                Re2ItemIds.Flamethrower,
                Re2ItemIds.ColtSAA,
                Re2ItemIds.Beretta,
                Re2ItemIds.Sparkshot,
                Re2ItemIds.Bowgun,
                Re2ItemIds.Shotgun,
                Re2ItemIds.Magnum,
                Re2ItemIds.RocketLauncher,
                rng.NextOf(Re2ItemIds.GrenadeLauncherExplosive, Re2ItemIds.GrenadeLauncherFlame, Re2ItemIds.GrenadeLauncherAcid)
            };
            return allWeapons
                .Where(x => IsWeaponCompatible((byte)config.Player, (byte)x))
                .Select(x => (byte)x)
                .ToArray();
        }

        public byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config)
        {
            switch (weapon)
            {
                case Re2ItemIds.HandgunLeon:
                    return Re2ItemIds.HandgunParts;
                case Re2ItemIds.Shotgun:
                    return Re2ItemIds.ShotgunParts;
                case Re2ItemIds.Magnum:
                    return Re2ItemIds.MagnumParts;
            }
            return null;
        }

        public bool IsWeaponCompatible(byte player, byte item)
        {
            if (player == 0 && !g_leonWeaponFix)
            {
                switch (item)
                {
                    case Re2ItemIds.HandgunClaire:
                    case Re2ItemIds.Bowgun:
                    case Re2ItemIds.GrenadeLauncherAcid:
                    case Re2ItemIds.GrenadeLauncherExplosive:
                    case Re2ItemIds.GrenadeLauncherFlame:
                    case Re2ItemIds.ColtSAA:
                    case Re2ItemIds.Sparkshot:
                    case Re2ItemIds.Beretta:
                        return false;
                }
            }
            if (player == 1 && !g_claireWeaponFix)
            {
                switch (item)
                {
                    case Re2ItemIds.HandgunLeon:
                    case Re2ItemIds.CustomHandgun:
                    case Re2ItemIds.Shotgun:
                    case Re2ItemIds.CustomShotgun:
                    case Re2ItemIds.Magnum:
                    case Re2ItemIds.CustomMagnum:
                    case Re2ItemIds.Beretta:
                    case Re2ItemIds.Flamethrower:
                        return false;
                }
            }
            return true;
        }

        public WeaponKind GetWeaponKind(byte item)
        {
            switch (item)
            {
                case Re2ItemIds.HandgunLeon:
                case Re2ItemIds.HandgunClaire:
                case Re2ItemIds.CustomHandgun:
                case Re2ItemIds.Beretta:
                case Re2ItemIds.ColtSAA:
                    return WeaponKind.Sidearm;
                case Re2ItemIds.Shotgun:
                case Re2ItemIds.CustomShotgun:
                case Re2ItemIds.Bowgun:
                case Re2ItemIds.SMG:
                case Re2ItemIds.Flamethrower:
                case Re2ItemIds.Sparkshot:
                    return WeaponKind.Primary;
                case Re2ItemIds.Magnum:
                case Re2ItemIds.CustomMagnum:
                case Re2ItemIds.GrenadeLauncherAcid:
                case Re2ItemIds.GrenadeLauncherExplosive:
                case Re2ItemIds.GrenadeLauncherFlame:
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
