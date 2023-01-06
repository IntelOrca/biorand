using System;
using System.Collections.Generic;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.RE1
{
    internal class Re1ItemHelper : IItemHelper
    {
        public byte GetItemSize(byte type)
        {
            return 1;
        }

        public string GetItemName(byte type)
        {
            var name = new Bio1ConstantTable().GetItemName(type);
            return name
                .Remove(0, 5)
                .Replace("_", " ");
        }

        public bool IsOptionalItem(RandoConfig config, byte type)
        {
            return false;
        }

        public bool IsItemTypeDiscardable(byte type)
        {
            switch (type)
            {
                case Re1ItemIds.SwordKey:
                case Re1ItemIds.ArmorKey:
                case Re1ItemIds.SheildKey:
                case Re1ItemIds.HelmetKey:
                case Re1ItemIds.MasterKey:
                case Re1ItemIds.SpecialKey:
                case Re1ItemIds.DormKey002:
                case Re1ItemIds.DormKey003:
                case Re1ItemIds.ControlRoomKey:
                case Re1ItemIds.PowerRoomKey:
                case Re1ItemIds.SmallKey:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsItemInfinite(byte type)
        {
            switch (type)
            {
                case Re1ItemIds.SquareCrank:
                case Re1ItemIds.HexCrank:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsItemDocument(byte type)
        {
            return type > Re1ItemIds.HerbGGB &&
                type != Re1ItemIds.Uzi &&
                type != Re1ItemIds.MiniGun;
        }

        public byte[] GetInitialItems(RandoConfig config)
        {
            // Jill has the lockpick which can open sword key doors
            if (config.Player == 1)
                return new[] { Re1ItemIds.SwordKey };

            return new byte[0];
        }

        public int GetItemQuantity(RandoConfig config, byte item)
        {
            switch (item)
            {
                case Re1ItemIds.Battery:
                    return 2;
                case Re1ItemIds.SmallKey:
                    return config.RandomDoors ? 1 : 5;
                case Re1ItemIds.MODisk:
                    return 3;
            }
            return 1;
        }

        public byte[] GetAmmoTypeForWeapon(byte type)
        {
            switch (type)
            {
                case Re1ItemIds.Beretta:
                    return new byte[] { Re1ItemIds.Clip };
                case Re1ItemIds.Shotgun:
                    return new byte[] { Re1ItemIds.Shells };
                case Re1ItemIds.ColtPython:
                    return new byte[] { Re1ItemIds.MagnumRounds };
                case Re1ItemIds.DumDumColt:
                    return new byte[] { Re1ItemIds.DumDumRounds };
                case Re1ItemIds.FlameThrower:
                    return new byte[] { Re1ItemIds.FlameThrowerFuel };
                case Re1ItemIds.BazookaAcid:
                case Re1ItemIds.BazookaExplosive:
                case Re1ItemIds.BazookaFlame:
                    return new byte[] {
                        Re1ItemIds.AcidRounds,
                        Re1ItemIds.ExplosiveRounds,
                        Re1ItemIds.FlameRounds
                    };
                default:
                    return new byte[0];
            }
        }

        public byte GetItemId(CommonItemKind kind)
        {
            switch (kind)
            {
                case CommonItemKind.HandgunAmmo:
                    return Re1ItemIds.Clip;
                case CommonItemKind.InkRibbon:
                    return Re1ItemIds.InkRibbon;
                case CommonItemKind.HerbG:
                    return Re1ItemIds.HerbG;
                case CommonItemKind.HerbGG:
                    return Re1ItemIds.HerbGG;
                case CommonItemKind.HerbGGG:
                    return Re1ItemIds.HerbGGG;
                case CommonItemKind.HerbR:
                    return Re1ItemIds.HerbR;
                case CommonItemKind.HerbGR:
                    return Re1ItemIds.HerbGR;
                case CommonItemKind.HerbB:
                    return Re1ItemIds.HerbB;
                case CommonItemKind.HerbGB:
                    return Re1ItemIds.HerbGB;
                case CommonItemKind.HerbGGB:
                    return Re1ItemIds.HerbGGB;
                case CommonItemKind.HerbGRB:
                    return Re1ItemIds.HerbGRB;
                case CommonItemKind.FirstAid:
                    return Re1ItemIds.FAidSpray;
                case CommonItemKind.Knife:
                    return Re1ItemIds.CombatKnife;
            }
            throw new NotImplementedException();
        }

        public double GetItemProbability(byte type)
        {
            switch (type)
            {
                case Re1ItemIds.Clip:
                    return 0.3;
                case Re1ItemIds.Shells:
                    return 0.2;
                case Re1ItemIds.MagnumRounds:
                case Re1ItemIds.DumDumRounds:
                case Re1ItemIds.AcidRounds:
                case Re1ItemIds.ExplosiveRounds:
                case Re1ItemIds.FlameRounds:
                case Re1ItemIds.FlameThrowerFuel:
                    return 0.1;
                default:
                    return 0;
            }
        }

        public byte GetMaxAmmoForAmmoType(byte type)
        {
            switch (type)
            {
                default:
                    return 1;

                case Re1ItemIds.InkRibbon:
                    return 3;

                case Re1ItemIds.Beretta:
                    return 15;
                case Re1ItemIds.Shotgun:
                    return 7;
                case Re1ItemIds.ColtPython:
                case Re1ItemIds.DumDumColt:
                case Re1ItemIds.BazookaAcid:
                case Re1ItemIds.BazookaExplosive:
                case Re1ItemIds.BazookaFlame:
                    return 6;
                case Re1ItemIds.RocketLauncher:
                    return 5;
                case Re1ItemIds.FlameThrower:
                    return 100;

                case Re1ItemIds.Clip:
                    return 60;
                case Re1ItemIds.Shells:
                    return 30;
                case Re1ItemIds.MagnumRounds:
                case Re1ItemIds.DumDumRounds:
                case Re1ItemIds.AcidRounds:
                case Re1ItemIds.ExplosiveRounds:
                case Re1ItemIds.FlameRounds:
                    return 10;
                case Re1ItemIds.FlameThrowerFuel:
                    return 100;
            }
        }

        public byte[] GetWeapons(Rng rng, RandoConfig config)
        {
            var enemyDifficulty = config.RandomEnemies ? 0 : config.EnemyDifficulty;

            var items = new List<byte>();
            items.Add(Re1ItemIds.Beretta);
            if (enemyDifficulty >= 2 || rng.Next(0, 2) >= 1)
                items.Add(Re1ItemIds.Shotgun);
            if (enemyDifficulty >= 3 || rng.Next(0, 3) >= 1)
                items.Add(rng.NextOf(Re1ItemIds.ColtPython, Re1ItemIds.DumDumColt));
            // if (rng.Next(0, 2) == 0)
            //     items.Add(Re1ItemIds.FlameThrower);
            if (enemyDifficulty >= 3 || rng.Next(0, 3) >= 1)
                items.Add(rng.NextOf(Re1ItemIds.BazookaAcid, Re1ItemIds.BazookaExplosive, Re1ItemIds.BazookaFlame));
            if (rng.Next(0, 2) == 0)
                items.Add(Re1ItemIds.RocketLauncher);
            return items.ToArray();
        }

        public byte? GetWeaponUpgrade(byte weapon, Rng rng, RandoConfig config)
        {
            return null;
        }

        public bool IsWeaponCompatible(byte player, byte item)
        {
            return true;
        }

        public bool HasInkRibbons(RandoConfig config)
        {
            return config.Player == 0;
        }

        public int[] GetInventorySize(RandoConfig config)
        {
            if (config.Player == 0)
                return new int[] { 6, 6 };
            else
                return new int[] { 8 };
        }
    }
}
