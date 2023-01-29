using System;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard
{
    internal interface IEnemyHelper
    {
        string GetEnemyName(byte type);
        void GetEnemyProbabilities(RandoConfig config, Action<byte, double> addIfSupported);
        void ExcludeEnemies(RandoConfig config, Rdt rdt, string difficulty, Action<byte> exclude);
        bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy);
        void BeginRoom(Rdt rdt);
        void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyTypeRaw);
        bool IsEnemy(byte type);
        SelectableEnemy[] GetSelectableEnemies();
    }
}
