using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RE2
{
    internal class Re2EnemyHelper : IEnemyHelper
    {
        private static readonly byte[] _zombieTypes = new byte[]
        {
            Re2EnemyIds.ZombieCop,
            Re2EnemyIds.ZombieGuy1,
            Re2EnemyIds.ZombieGirl,
            Re2EnemyIds.ZombieTestSubject,
            Re2EnemyIds.ZombieScientist,
            Re2EnemyIds.ZombieNaked,
            Re2EnemyIds.ZombieGuy2,
            Re2EnemyIds.ZombieGuy3,
            Re2EnemyIds.ZombieRandom,
            Re2EnemyIds.ZombieBrad
        };

        public Re2EnemyHelper()
        {
        }

        public string GetEnemyName(byte type)
        {
            var name = new Bio2ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public bool SupportsEnemyType(RandoConfig config, RandomizedRdt rdt, bool hasEnemyPlacements, byte enemyType)
        {
            if (config.RandomEnemyPlacement && hasEnemyPlacements)
            {
                return true;
            }
            else
            {
                var exclude = new HashSet<byte>();
                ExcludeEnemies(config, rdt, x => exclude.Add(x));
                return !exclude.Contains(enemyType);
            }
        }

        private void ExcludeEnemies(RandoConfig config, RandomizedRdt rdt, Action<byte> exclude)
        {
            var types = rdt.Enemies
                .Select(x => x.Type)
                .Where(IsEnemy)
                .ToArray();

            if (types.Length != 1)
            {
                exclude(Re2EnemyIds.Birkin1);
            }
            if (!config.RandomEnemyPlacement)
            {
                exclude(Re2EnemyIds.GAdult);
            }
        }

        public void BeginRoom(RandomizedRdt rdt)
        {
            // Mute dead zombies or vines, this ensures our random enemy type
            // will be heard
            foreach (var enemy in rdt.Enemies)
            {
                if (enemy.Type == Re2EnemyIds.Vines ||
                    enemy.Type == Re2EnemyIds.Maggots ||
                    (IsZombie(enemy.Type) && enemy.State == 2))
                {
                    enemy.SoundBank = 0;
                }
            }
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            switch (enemy.Type)
            {
                case Re2EnemyIds.Crow:
                case Re2EnemyIds.Spider:
                case Re2EnemyIds.BabySpider:
                case Re2EnemyIds.GiantMoth:
                case Re2EnemyIds.LickerRed:
                case Re2EnemyIds.LickerGrey:
                case Re2EnemyIds.ZombieDog:
                case Re2EnemyIds.Ivy:
                case Re2EnemyIds.Vines:
                case Re2EnemyIds.IvyPurple:
                case Re2EnemyIds.ZombieBrad:
                    return true;
                case Re2EnemyIds.MarvinBranagh:
                    // Edge case: Marvin is only a zombie in scenario B
                    return config.Scenario == 1;
                default:
                    return IsZombie(enemy.Type);
            }
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyType)
        {
            switch (enemyType)
            {
                case Re2EnemyIds.ZombieGuy1:
                case Re2EnemyIds.ZombieGuy2:
                case Re2EnemyIds.ZombieGuy3:
                case Re2EnemyIds.ZombieGirl:
                case Re2EnemyIds.ZombieCop:
                case Re2EnemyIds.ZombieTestSubject:
                case Re2EnemyIds.ZombieScientist:
                case Re2EnemyIds.ZombieNaked:
                case Re2EnemyIds.ZombieRandom:
                case Re2EnemyIds.ZombieBrad:
                    if (!enemySpec.KeepState)
                        enemy.State = rng.NextOf<byte>(0, 1, 2, 3, 4, 6);
                    enemy.SoundBank = GetZombieSoundBank(enemyType);
                    break;
                case Re2EnemyIds.ZombieDog:
                    enemy.State = 0;
                    if (config.EnemyDifficulty >= 3)
                    {
                        // %50 of running
                        enemy.State = rng.NextOf<byte>(0, 2);
                    }
                    else if (config.EnemyDifficulty >= 2)
                    {
                        // %25 of running
                        enemy.State = rng.NextOf<byte>(0, 0, 0, 2);
                    }
                    enemy.SoundBank = 12;
                    break;
                case Re2EnemyIds.ZombieArms:
                    enemy.State = 0;
                    enemy.SoundBank = 17;
                    break;
                case Re2EnemyIds.Crow:
                    enemy.State = 0;
                    enemy.SoundBank = 13;
                    break;
                case Re2EnemyIds.BabySpider:
                case Re2EnemyIds.Spider:
                    enemy.State = 0;
                    enemy.SoundBank = 16;
                    break;
                case Re2EnemyIds.LickerRed:
                    enemy.State = 0;
                    enemy.SoundBank = 51;
                    break;
                case Re2EnemyIds.LickerGrey:
                    enemy.State = 0;
                    enemy.SoundBank = 14;
                    break;
                case Re2EnemyIds.Cockroach:
                    enemy.State = 0;
                    enemy.SoundBank = 15;
                    break;
                case Re2EnemyIds.Ivy:
                    enemy.State = 0;
                    enemy.SoundBank = 19;
                    break;
                case Re2EnemyIds.IvyPurple:
                    enemy.State = 0;
                    enemy.SoundBank = 48;
                    break;
                case Re2EnemyIds.GiantMoth:
                    enemy.State = 0;
                    enemy.SoundBank = 23;
                    break;
                case Re2EnemyIds.Tyrant1:
                    enemy.State = 0;
                    enemy.SoundBank = 18;
                    break;
                case Re2EnemyIds.Birkin1:
                    enemy.State = 1;
                    enemy.SoundBank = 24;
                    break;
                case Re2EnemyIds.GEmbryo:
                    enemy.State = 3;
                    enemy.SoundBank = 20;
                    break;
                case Re2EnemyIds.GAdult:
                    if (enemy.Type == Re2EnemyIds.GEmbryo)
                    {
                        enemy.State = 0;
                        enemy.SoundBank = 20;
                    }
                    else
                    {
                        enemy.State = 1;
                        enemy.SoundBank = 21;
                        if (enemy.Id != 0)
                        {
                            throw new Exception("G-Adult not set for ID_EM_0");
                        }
                    }
                    break;
            }
        }

        private static byte GetZombieSoundBank(byte type)
        {
            switch (type)
            {
                case Re2EnemyIds.ZombieCop:
                case Re2EnemyIds.ZombieGuy1:
                case Re2EnemyIds.ZombieGuy2:
                case Re2EnemyIds.ZombieGuy3:
                case Re2EnemyIds.ZombieRandom:
                    return 9;
                case Re2EnemyIds.ZombieScientist:
                    return 47;
                case Re2EnemyIds.ZombieTestSubject:
                    return 47;
                case Re2EnemyIds.ZombieBrad:
                    return 1;
                case Re2EnemyIds.ZombieGirl:
                    return 45;
                case Re2EnemyIds.ZombieNaked:
                    return 46;
                default:
                    return 0;
            }
        }

        public bool IsZombie(byte type)
        {
            switch (type)
            {
                case Re2EnemyIds.ZombieCop:
                case Re2EnemyIds.ZombieBrad:
                case Re2EnemyIds.ZombieGuy1:
                case Re2EnemyIds.ZombieGirl:
                case Re2EnemyIds.ZombieTestSubject:
                case Re2EnemyIds.ZombieScientist:
                case Re2EnemyIds.ZombieNaked:
                case Re2EnemyIds.ZombieGuy2:
                case Re2EnemyIds.ZombieGuy3:
                case Re2EnemyIds.ZombieRandom:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsEnemy(byte type)
        {
            return type < Re2EnemyIds.ChiefIrons1;
        }

        public bool IsUniqueEnemyType(byte type)
        {
            switch (type)
            {
                case Re2EnemyIds.Alligator:
                case Re2EnemyIds.Tyrant1:
                case Re2EnemyIds.Tyrant2:
                case Re2EnemyIds.Birkin1:
                case Re2EnemyIds.Birkin2:
                case Re2EnemyIds.Birkin3:
                case Re2EnemyIds.Birkin4:
                case Re2EnemyIds.Birkin5:
                    return true;
                default:
                    return false;
            }
        }

        public int GetEnemyTypeLimit(RandoConfig config, int difficulty, byte type)
        {
            byte[] limit;
            switch (type)
            {
                case Re2EnemyIds.Birkin1:
                case Re2EnemyIds.GAdult:
                    limit = new byte[] { 1 };
                    break;
                case Re2EnemyIds.ZombieDog:
                case Re2EnemyIds.GiantMoth:
                case Re2EnemyIds.Ivy:
                case Re2EnemyIds.IvyPurple:
                    limit = new byte[] { 2, 4, 6, 8 };
                    break;
                case Re2EnemyIds.LickerRed:
                case Re2EnemyIds.LickerGrey:
                case Re2EnemyIds.Tyrant1:
                    limit = new byte[] { 2, 3, 4, 6 };
                    break;
                default:
                    limit = new byte[] { 16 };
                    break;
            }
            var index = Math.Min(limit.Length - 1, difficulty);
            return limit[index];
        }

        public byte[] GetEnemyDependencies(byte enemyType)
        {
            if (enemyType == Re2EnemyIds.GAdult)
            {
                return new[] { Re2EnemyIds.GEmbryo };
            }
            return new byte[0];
        }

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Arms", "LightGray", new[] { Re2EnemyIds.ZombieArms }),
            new SelectableEnemy("Crow", "Black", new[] { Re2EnemyIds.Crow }),
            new SelectableEnemy("G-Embryo", "DarkOliveGreen", new[] { Re2EnemyIds.GEmbryo }),
            new SelectableEnemy("Spider", "YellowGreen", new[] { Re2EnemyIds.Spider }),
            new SelectableEnemy("Zombie", "LightGray", _zombieTypes),
            new SelectableEnemy("Moth", "DarkOliveGreen", new[] { Re2EnemyIds.GiantMoth }),
            new SelectableEnemy("Ivy", "SpringGreen", new[] { Re2EnemyIds.Ivy, Re2EnemyIds.IvyPurple }),
            new SelectableEnemy("Licker", "IndianRed", new[] { Re2EnemyIds.LickerRed, Re2EnemyIds.LickerGrey }),
            new SelectableEnemy("Zombie Dog", "Black", new[] { Re2EnemyIds.ZombieDog }),
            new SelectableEnemy("Tyrant", "DarkGray", new[] { Re2EnemyIds.Tyrant1 }),
            new SelectableEnemy("Birkin", "IndianRed", new[] { Re2EnemyIds.Birkin1 }),
            new SelectableEnemy("G-Adult", "DarkOliveGreen", new[] { Re2EnemyIds.GAdult }),
        };

        public byte GetEnemySapNumber(byte enemyType)
        {
            switch (enemyType)
            {
                case Re2EnemyIds.LickerGrey:
                    return 8;
                case Re2EnemyIds.LickerRed:
                    return 9;
                case Re2EnemyIds.Tyrant1:
                    return 12;
                case Re2EnemyIds.Ivy:
                case Re2EnemyIds.IvyPurple:
                    return 13;
                case Re2EnemyIds.ZombieScientist:
                case Re2EnemyIds.ZombieTestSubject:
                    return 4;
                case Re2EnemyIds.ZombieNaked:
                    return 5;
                case Re2EnemyIds.ZombieCop:
                case Re2EnemyIds.ZombieGuy1:
                case Re2EnemyIds.ZombieGuy2:
                case Re2EnemyIds.ZombieGuy3:
                case Re2EnemyIds.ZombieRandom:
                    return 50;
                case Re2EnemyIds.ZombieGirl:
                    return 52;
                default:
                    return 0;
            }
        }

        public byte[] GetRequiredEsps(byte enemyType)
        {
            if (enemyType == Re2EnemyIds.Spider ||
                enemyType == Re2EnemyIds.Ivy ||
                enemyType == Re2EnemyIds.IvyPurple)
            {
                return new byte[] { 0x1D };
            }
            return new byte[0];
        }

        public byte[] GetReservedEnemyIds() => new byte[0];

        public void CreateZombie(byte type, PldFile srcPld, EmdFile srcEmd, string dstPath)
        {
            var tim = srcPld.GetTim(0);
            if (type == Re2EnemyIds.ZombieRandom)
            {
                tim.ImportPage(0, tim.ExportPage(3));
                tim.ImportPage(1, tim.ExportPage(2));
                tim.ImportPage(3, tim.ExportPage(2));
            }
            else
            {
                tim.ImportPage(1, tim.ExportPage(2));
                tim.ImportPage(0, tim.ExportPage(3));
                tim.ImportPage(2, tim.ExportPage(0));
                tim.ImportPage(3, tim.ExportPage(1));
            }

            var targetScale = srcPld.CalculateEmrScale(srcEmd) * 0.85;
            srcEmd.SetEmr(0, srcEmd.GetEmr(0).WithSkeleton(srcPld.GetEmr(0)).Scale(targetScale));
            srcEmd.SetEmr(1, srcEmd.GetEmr(1).Scale(targetScale));

            srcEmd.SetMesh(0, srcPld.GetMesh(0).SwapPages(0, 1, true));
            if (type == Re2EnemyIds.ZombieRandom)
            {
                srcEmd.SetMesh(0, srcEmd.GetMesh(0)
                    .EditMeshTextures(m =>
                    {
                        if (m.PartIndex != 8)
                            m.Page = 0;
                    }));
            }

            var mesh = srcEmd.GetMesh(0).ToBuilder();
            while (mesh.Count > 15)
            {
                mesh.RemoveAt(mesh.Count - 1);
            }
            mesh.Add(mesh[9]);
            mesh.Add(mesh[0]);
            srcEmd.SetMesh(0, mesh.ToMesh());

            Directory.CreateDirectory(Path.GetDirectoryName(dstPath));
            srcEmd.Save(dstPath);
            tim.Save(Path.ChangeExtension(dstPath, ".tim"));
        }
    }
}
