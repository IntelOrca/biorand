using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class EnemyRandomiser
    {
        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly IEnemyHelper _enemyHelper;

        public EnemyRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng random, IEnemyHelper enemyHelper)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _enemyHelper = enemyHelper;
        }

        public void Randomise()
        {
            _logger.WriteHeading("Randomizing enemies:");
            foreach (var rdt in _gameData.Rdts)
            {
                var enemies = rdt.Enemies.ToArray();
                var logEnemies = enemies.Select(GetEnemyLogText).ToArray();
                RandomiseRoom(_rng.NextFork(), rdt);
                for (int i = 0; i < logEnemies.Length; i++)
                {
                    var enemy = enemies[i];
                    var oldLog = logEnemies[i];
                    var newLog = GetEnemyLogText(enemy);
                    if (oldLog != newLog)
                    {
                        _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) {oldLog} becomes {newLog}");
                    }
                }
            }
        }

        private Rng.Table<byte> CreateEnemyProbabilityTable(Rng rng, HashSet<byte>? includeTypes, HashSet<byte>? excludeTypes)
        {
            var table = rng.CreateProbabilityTable<byte>();
            _enemyHelper.GetEnemyProbabilities(_config, AddIfSupported);
            return table;

            void AddIfSupported(byte type, double prob)
            {
                if (excludeTypes?.Contains(type) != true &&
                    includeTypes?.Contains(type) != false)
                {
                    table.Add(type, prob);
                }
            }
        }

        private string GetEnemyLogText(SceEmSetOpcode enemy)
        {
            return $"[{_enemyHelper.GetEnemyName(enemy.Type)},{enemy.State},{enemy.Ai},{enemy.SoundBank},{enemy.Texture}]";
        }

        private void RandomiseRoom(Rng rng, Rdt rdt)
        {
            var enemySpecs = _map.GetRoom(rdt.RdtId)?.Enemies;
            if (enemySpecs == null)
            {
                enemySpecs = new[] { new MapRoomEnemies() };
            }
            foreach (var enemySpec in enemySpecs)
            {
                RandomiseRoom(rng, rdt, enemySpec);
            }
        }

        private void RandomiseRoom(Rng rng, Rdt rdt, MapRoomEnemies enemySpec)
        {
            if (enemySpec.Player != null && enemySpec.Player != _config.Player)
                return;

            if (enemySpec.Scenario != null && enemySpec.Scenario != _config.Scenario)
                return;

            if (enemySpec.Nop != null)
            {
                foreach (var offset in enemySpec.Nop)
                {
                    rdt.Nop(offset);
                    _logger.WriteLine($"{rdt.RdtId} (0x{offset:X2}) opcode removed");
                }
            }

            var enemiesToChange = rdt.Enemies
                .Where(e => enemySpec.ExcludeOffsets?.Contains(e.Offset) != true)
                .Where(e => _enemyHelper.ShouldChangeEnemy(_config, e))
                .ToArray();
            var numEnemies = enemiesToChange.DistinctBy(x => x.Id).Count();

            var includeTypes = enemySpec.IncludeTypes == null ?
                null :
                enemySpec.IncludeTypes.Select(x => (byte)x).ToHashSet();
            var excludeTypes = enemySpec.ExcludeTypes == null ?
                new HashSet<byte>() :
                enemySpec.ExcludeTypes.Select(x => (byte)x).ToHashSet();

            _enemyHelper.ExcludeEnemies(_config, rdt, enemySpec.Difficulty ?? "", x => excludeTypes.Add(x));

            var probTable = CreateEnemyProbabilityTable(rng, includeTypes, excludeTypes);
            if (probTable.IsEmpty)
                return;

#if MULTIPLE_ENEMY_TYPES
            var ids = rdt.Enemies.Select(x => x.Id).Distinct().ToArray();
            var enemyTypesId = ids.Select(x => EnemyType.Cerebrus).ToArray();
            if (enemyTypesId.Length >= 2)
                enemyTypesId[0] = EnemyType.LickerRed;
            if (enemyTypesId.Length >= 3)
                enemyTypesId[1] = EnemyType.LickerRed;
#else
            var ids = rdt.Enemies.Select(x => x.Id).Distinct().ToArray();
            var randomEnemyType = probTable.Next();
            var enemyTypesId = ids.Select(x => randomEnemyType).ToArray();
#endif

            _enemyHelper.BeginRoom(rdt);

            foreach (var enemy in enemiesToChange)
            {
                var index = Array.IndexOf(ids, enemy.Id);
                var enemyType = enemyTypesId[index];
                enemy.Type = enemyType;
                if (!enemySpec.KeepState)
                    enemy.State = 0;
                if (!enemySpec.KeepAi)
                    enemy.Ai = 0;
                enemy.Texture = 0;
                if (enemySpec.Y != null)
                    enemy.Y = enemySpec.Y.Value;
                _enemyHelper.SetEnemy(_config, rng, enemy, enemySpec, enemyType);
            }
        }

        private static void PrintAllEnemies(GameData gameData)
        {
            foreach (var rdt in gameData.Rdts)
            {
                if (rdt.Enemies.Count() != 0)
                {
                    Console.WriteLine($"RDT: {rdt.RdtId}:");
                    foreach (var enemy in rdt.Enemies)
                    {
                        Console.WriteLine($"    {enemy.Id}: {enemy.Type}, {enemy.State}, {enemy.Ai}, {enemy.SoundBank}, {enemy.Texture}");
                    }
                }
            }
        }
    }
}
