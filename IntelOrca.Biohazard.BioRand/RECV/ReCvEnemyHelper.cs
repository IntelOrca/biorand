using System;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    internal class ReCvEnemyHelper : IEnemyHelper
    {
        public void BeginRoom(RandomizedRdt rdt)
        {
        }

        public string GetEnemyName(byte type)
        {
            switch (type)
            {
                case ReCvEnemyIds.Zombie:
                    return "ZOMBIE";
                default:
                    return $"EM_{type:X2}";
            }
        }

        public int GetEnemyTypeLimit(RandoConfig config, int difficulty, byte type)
        {
            if (type == RECV.ReCvEnemyIds.Tyrant)
                return 1;
            if (type == RECV.ReCvEnemyIds.Zombie)
                return 1;

            var limit = new byte[] { 2, 4, 7, 10 };
            var index = Math.Min(limit.Length - 1, difficulty);
            return limit[index];
        }

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Bat", "Black", ReCvEnemyIds.Bat),
            new SelectableEnemy("Zombie", "LightGray", ReCvEnemyIds.Zombie),
            new SelectableEnemy("Hunter", "IndianRed", ReCvEnemyIds.Hunter),
            new SelectableEnemy("Bandersnatch", "Cyan", ReCvEnemyIds.Bandersnatch),
            new SelectableEnemy("Zombie Dog", "Black", ReCvEnemyIds.ZombieDog),
            new SelectableEnemy("Tyrant", "Gray", ReCvEnemyIds.Tyrant)
        };

        public bool IsEnemy(byte type)
        {
            return type < ReCvEnemyIds.Unknown43;
        }

        public bool IsUniqueEnemyType(byte type)
        {
            switch (type)
            {
                case ReCvEnemyIds.Bat:
                case ReCvEnemyIds.Hunter:
                case ReCvEnemyIds.Bandersnatch:
                case ReCvEnemyIds.Zombie:
                case ReCvEnemyIds.ZombieDog:
                    return false;
                default:
                    return true;
            }
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyType)
        {
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            return false;
        }

        public bool SupportsEnemyType(RandoConfig config, RandomizedRdt rdt, bool hasEnemyPlacements, byte enemyType)
        {
            return true;
        }

        public bool IsZombie(byte type) => type == ReCvEnemyIds.Zombie;

        public byte[] GetReservedEnemyIds() => new byte[] { };

        public byte[] GetEnemyDependencies(byte enemyType) => new byte[0];

        public byte[] GetRequiredEsps(byte enemyType) => new byte[0];
    }
}
