using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.RE1;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3EnemyHelper : IEnemyHelper
    {
        public void BeginRoom(Rdt rdt)
        {
        }

        public string GetEnemyName(byte type)
        {
            var name = new Bio3ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public int GetEnemyTypeLimit(RandoConfig config, byte type)
        {
            throw new NotImplementedException();
        }

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Arms", "LightGray", Re3EnemyIds.Arm),
            new SelectableEnemy("Crow", "Black", Re3EnemyIds.Crow),
            new SelectableEnemy("Mini Worm", "LightGray", Re3EnemyIds.MiniWorm),
            new SelectableEnemy("Spider", "YellowGreen", Re3EnemyIds.Spider),
            new SelectableEnemy("Zombie", "LightGray", _zombieTypes),
            new SelectableEnemy("Hunter", "IndianRed", new[] { Re3EnemyIds.Hunter }),
            new SelectableEnemy("Brain Sucker", "DarkOliveGreen", new[] { Re3EnemyIds.BS23, Re3EnemyIds.BS28 }),
            new SelectableEnemy("Zombie Dog", "Black", Re3EnemyIds.ZombieDog),
            new SelectableEnemy("Nemesis", "LightGray", Re3EnemyIds.Nemesis),
        };

        public bool IsEnemy(byte type)
        {
            return type < Re3EnemyIds.CarlosOliveira1;
        }

        public bool IsUniqueEnemyType(byte type)
        {
            throw new NotImplementedException();
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyType)
        {
            switch (enemyType)
            {
                case Re3EnemyIds.ZombieGuy1:
                case Re3EnemyIds.ZombieGirl1:
                case Re3EnemyIds.ZombieFat:
                case Re3EnemyIds.ZombieGirl2:
                case Re3EnemyIds.ZombieRpd1:
                case Re3EnemyIds.ZombieGuy2:
                case Re3EnemyIds.ZombieGuy3:
                case Re3EnemyIds.ZombieGuy4:
                case Re3EnemyIds.ZombieNaked:
                case Re3EnemyIds.ZombieGuy5:
                case Re3EnemyIds.ZombieGuy6:
                case Re3EnemyIds.ZombieLab:
                case Re3EnemyIds.ZombieGirl3:
                case Re3EnemyIds.ZombieRpd2:
                case Re3EnemyIds.ZombieGuy7:
                case Re3EnemyIds.ZombieGuy8:
                    if (!enemySpec.KeepState)
                        enemy.State = rng.NextOf<byte>(0, 1, 2, 3, 4, 6);
                    enemy.SoundBank = GetZombieSoundBank(enemyType);
                    break;
                case Re3EnemyIds.ZombieDog:
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
                    enemy.SoundBank = 32;
                    break;
                case Re3EnemyIds.Crow:
                    enemy.State = 0;
                    enemy.SoundBank = 33;
                    break;
                case Re3EnemyIds.Hunter:
                    enemy.State = 0;
                    enemy.SoundBank = 34;
                    break;
                case Re3EnemyIds.BS23:
                case Re3EnemyIds.BS28:
                case Re3EnemyIds.MiniBrainsucker:
                    enemy.State = 0;
                    enemy.SoundBank = 35;
                    break;
                case Re3EnemyIds.HunterGamma:
                    enemy.State = 0;
                    enemy.SoundBank = 36;
                    break;
                case Re3EnemyIds.Spider:
                    enemy.State = 0;
                    enemy.SoundBank = 37;
                    break;
                case Re3EnemyIds.MiniSpider:
                    enemy.State = 2;
                    enemy.SoundBank = 38;
                    break;
                case Re3EnemyIds.Arm:
                    enemy.State = 0;
                    enemy.SoundBank = 31;
                    break;
                case Re3EnemyIds.MiniWorm:
                    enemy.State = 0;
                    enemy.SoundBank = 49;
                    break;
                case Re3EnemyIds.Nemesis:
                case Re3EnemyIds.Nemesis3:
                    enemy.State = 0;
                    enemy.SoundBank = 54;
                    break;
            }
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            switch (enemy.Type)
            {
                case Re3EnemyIds.ZombieGuy1:
                case Re3EnemyIds.ZombieGirl1:
                case Re3EnemyIds.ZombieFat:
                case Re3EnemyIds.ZombieGirl2:
                case Re3EnemyIds.ZombieRpd1:
                case Re3EnemyIds.ZombieGuy2:
                case Re3EnemyIds.ZombieGuy3:
                case Re3EnemyIds.ZombieGuy4:
                case Re3EnemyIds.ZombieNaked:
                case Re3EnemyIds.ZombieGuy5:
                case Re3EnemyIds.ZombieGuy6:
                case Re3EnemyIds.ZombieLab:
                case Re3EnemyIds.ZombieGirl3:
                case Re3EnemyIds.ZombieRpd2:
                case Re3EnemyIds.ZombieGuy7:
                case Re3EnemyIds.ZombieGuy8:
                case Re3EnemyIds.ZombieDog:
                case Re3EnemyIds.Hunter:
                case Re3EnemyIds.BS23:
                case Re3EnemyIds.Spider:
                case Re3EnemyIds.MiniSpider:
                case Re3EnemyIds.MiniBrainsucker:
                case Re3EnemyIds.BS28:
                    return true;
                default:
                    return false;
            }
        }

        public bool SupportsEnemyType(RandoConfig config, Rdt rdt, string difficulty, bool hasEnemyPlacements, byte enemyType)
        {
            // These enemies always work
            switch (enemyType)
            {
                case Re3EnemyIds.Hunter:
                case Re3EnemyIds.BS23:
                case Re3EnemyIds.BS28:
                case Re3EnemyIds.ZombieDog:
                case Re3EnemyIds.Nemesis:
                    return true;
            }

            // Enemies that can already be in the room will work
            var existingEnemyTypes = rdt.Enemies
                .Select(x => x.Type)
                .Where(IsEnemy)
                .ToArray();

            if (existingEnemyTypes.Contains(enemyType))
                return true;

            return false;
        }

        private static bool IsZombie(byte type)
        {
            return _zombieTypes.Contains(type);
        }

        private static byte GetZombieSoundBank(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.ZombieGirl1:
                case Re3EnemyIds.ZombieGirl2:
                case Re3EnemyIds.ZombieGirl3:
                    return 3;
                case Re3EnemyIds.ZombieGuy1:
                case Re3EnemyIds.ZombieFat:
                case Re3EnemyIds.ZombieRpd1:
                case Re3EnemyIds.ZombieGuy2:
                case Re3EnemyIds.ZombieGuy3:
                case Re3EnemyIds.ZombieGuy4:
                case Re3EnemyIds.ZombieNaked:
                case Re3EnemyIds.ZombieGuy5:
                case Re3EnemyIds.ZombieGuy6:
                case Re3EnemyIds.ZombieLab:
                case Re3EnemyIds.ZombieRpd2:
                case Re3EnemyIds.ZombieGuy7:
                case Re3EnemyIds.ZombieGuy8:
                    return 4;
                // return 1;
                // return 2;
                // return 4;
                // return 5;
                // return 6;
                // return 7;
                // return 8;
                // return 10;
                // return 11;
                // return 13;
                // return 17;
                // return 18;
                // return 19;
                default:
                    return 0;
            }
        }

        private static readonly byte[] _zombieTypes = new byte[]
        {
            Re3EnemyIds.ZombieGuy1,
            Re3EnemyIds.ZombieGirl1,
            Re3EnemyIds.ZombieFat,
            Re3EnemyIds.ZombieGirl2,
            Re3EnemyIds.ZombieRpd1,
            Re3EnemyIds.ZombieGuy2,
            Re3EnemyIds.ZombieGuy3,
            Re3EnemyIds.ZombieGuy4,
            Re3EnemyIds.ZombieNaked,
            Re3EnemyIds.ZombieGuy5,
            Re3EnemyIds.ZombieGuy6,
            Re3EnemyIds.ZombieLab,
            Re3EnemyIds.ZombieGirl3,
            Re3EnemyIds.ZombieRpd2,
            Re3EnemyIds.ZombieGuy7,
            Re3EnemyIds.ZombieGuy8,
        };
    }
}
