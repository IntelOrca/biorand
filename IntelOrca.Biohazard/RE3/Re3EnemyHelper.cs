using System;
using IntelOrca.Biohazard.RE1;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3EnemyHelper : IEnemyHelper
    {
        public void BeginRoom(Rdt rdt)
        {
            throw new NotImplementedException();
        }

        public void ExcludeEnemies(RandoConfig config, Rdt rdt, string difficulty, Action<byte> exclude)
        {
            throw new NotImplementedException();
        }

        public string GetEnemyName(byte type)
        {
            throw new NotImplementedException();
        }

        public int GetEnemyTypeLimit(RandoConfig config, byte type)
        {
            throw new NotImplementedException();
        }

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Zombie", "LightGray", Re3EnemyIds.Zombie),
            new SelectableEnemy("Cerberus", "Black", Re3EnemyIds.Cerberus),
            new SelectableEnemy("Crow", "Black", Re3EnemyIds.Crow),
            new SelectableEnemy("Hunter", "IndianRed", Re3EnemyIds.Hunter),
            new SelectableEnemy("BS23", "Black", Re3EnemyIds.BS23),
            new SelectableEnemy("HunterGamma", "Black", Re3EnemyIds.HunterGamma),
            new SelectableEnemy("Spider", "YellowGreen", Re3EnemyIds.Spider),
            new SelectableEnemy("MiniSpider", "Black", Re3EnemyIds.MiniSpider),
            new SelectableEnemy("MiniBrainsucker", "Black", Re3EnemyIds.MiniBrainsucker),
            new SelectableEnemy("BS28", "Black", Re3EnemyIds.BS28),
            new SelectableEnemy("Arms", "LightGray", Re3EnemyIds.Arm),
            new SelectableEnemy("MiniWorm", "LightGray", Re3EnemyIds.MiniWorm),
            new SelectableEnemy("Nemesis", "LightGray", Re3EnemyIds.Nemesis),
            new SelectableEnemy("Nemesis3", "LightGray", Re3EnemyIds.Nemesis3),
        };

        public bool IsEnemy(byte type)
        {
            throw new NotImplementedException();
        }

        public bool IsUniqueEnemyType(byte type)
        {
            throw new NotImplementedException();
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyTypeRaw)
        {
            throw new NotImplementedException();
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            throw new NotImplementedException();
        }

        public bool SupportsEnemyType(RandoConfig config, Rdt rdt, string difficulty, bool hasEnemyPlacements, byte enemyType)
        {
            throw new NotImplementedException();
        }
    }
}
