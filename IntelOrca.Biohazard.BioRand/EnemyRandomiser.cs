using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.BioRand.RE3;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand
{
    internal class EnemyRandomiser
    {
        private static bool g_debugLogging = false;
        private static readonly Dictionary<RdtId, SelectableEnemy> g_stickyEnemies = new Dictionary<RdtId, SelectableEnemy>();
        private static int g_player;

        private BioVersion _version;
        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly IEnemyHelper _enemyHelper;
        private readonly DataManager _dataManager;
        private EnemyPosition[] _enemyPositions = new EnemyPosition[0];
        private HashSet<byte> _killIdPool = new HashSet<byte>();
        private Queue<byte> _killIds = new Queue<byte>();
        private Dictionary<byte, EmbeddedEffect> _effects = new Dictionary<byte, EmbeddedEffect>();

        public EnemyRandomiser(BioVersion version, RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng rng, IEnemyHelper enemyHelper, DataManager dataManager)
        {
            _version = version;
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = rng;
            _enemyHelper = enemyHelper;
            _dataManager = dataManager;
        }

        private void ReadEnemyPlacements()
        {
            var json = _dataManager.GetText(_version, "enemy.json");
            var enemyPositions = JsonSerializer.Deserialize<EnemyPosition[]>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!.ToList();

            foreach (var room in _map.Rooms!)
            {
                var linkedRoom = room.Value.LinkedRoom;
                if (linkedRoom == null)
                    continue;

                var rdtId = RdtId.Parse(room.Key);
                var linkedRdtId = RdtId.Parse(linkedRoom);
                if (!enemyPositions.Any(x => x.RdtId == linkedRdtId))
                {
                    // Copy positions to linked room
                    var srcEnemyPositions = enemyPositions.Where(x => x.RdtId == rdtId).ToArray();
                    foreach (var pos in srcEnemyPositions)
                    {
                        var copy = pos;
                        copy.RdtId = linkedRdtId;
                        enemyPositions.Add(copy);
                    }
                }
            }
            _enemyPositions = enemyPositions.ToArray();
        }

        private void GatherEsps()
        {
            foreach (var rdt in _gameData.Rdts)
            {
                var embeddedEffects = rdt.RdtFile.EmbeddedEffects;
                for (var i = 0; i < embeddedEffects.Count; i++)
                {
                    var ee = embeddedEffects[i];
                    if (ee.Id != 0xFF && !_effects.ContainsKey(ee.Id))
                    {
                        _effects[ee.Id] = ee;
                    }
                }
            }
        }

        private byte GetNextKillId()
        {
            if (_killIds.Count == 0)
            {
                foreach (var id in _killIdPool.Shuffle(_rng))
                {
                    _killIds.Enqueue(id);
                }
            }
            return _killIds.Dequeue();
        }

        private void SetupRandomEnemyPlacements()
        {
            _enemyPositions = _enemyPositions.Shuffle(_rng);
            for (byte i = 0; i < 255; i++)
                _killIdPool.Add(i);

            var reservedEnemyIds = _enemyHelper.GetReservedEnemyIds();
            foreach (var enemyId in reservedEnemyIds)
                _killIdPool.Remove(enemyId);

            foreach (var rdt in _gameData.Rdts)
            {
                byte[] killIds;
                if (_enemyPositions.Any(x => x.RdtId == rdt.RdtId))
                {
                    // Exclude IDs which belong to special enemies like tyrants or bosses
                    killIds = rdt.Enemies
                        .Where(x => !_enemyHelper.IsEnemy(x.Type) || _enemyHelper.IsUniqueEnemyType(x.Type))
                        .Select(x => x.KillId)
                        .ToArray();
                }
                else
                {
                    // Exclude IDs which belong to static enemy rooms
                    killIds = rdt.Enemies
                        .Select(x => x.KillId)
                        .ToArray();
                }
                _killIdPool.RemoveMany(killIds);
            }
        }

        public void Randomise(PlayGraph? graph)
        {
            _logger.WriteHeading("Randomizing enemies:");
            ResetStickyEnemies();
            try
            {
                ReadEnemyPlacements();
                GatherEsps();
                SetupRandomEnemyPlacements();
                RandomizeRooms(GetAccessibleRdts(graph));
                FixRooms();
            }
            finally
            {
                EndStickyEnemies();
            }
        }

        private void ResetStickyEnemies()
        {
            if (_config.Game != 1)
                return;

            // Wait for player to switch
            while (_config.Player != g_player)
            {
                Thread.Sleep(100);
            }

            if (_config.Player == 0)
            {
                g_stickyEnemies.Clear();
            }
        }

        private void EndStickyEnemies()
        {
            g_player = (g_player + 1) % 2;
        }

        private RandomizedRdt[] GetAccessibleRdts(PlayGraph? graph)
        {
            if (graph == null || graph.Start == null)
                return _gameData.Rdts;

            var visited = new HashSet<PlayNode>();
            var q = new Queue<PlayNode>();
            q.Enqueue(graph.Start);
            while (q.Count > 0)
            {
                var node = q.Dequeue();
                if (visited.Add(node))
                {
                    foreach (var e in node.Edges)
                    {
                        if (e.Node != null)
                        {
                            q.Enqueue(e.Node);
                        }
                    }
                }
            }

            var result = visited
                .Select(x => _gameData.GetRdt(x.RdtId)!)
                .ToHashSet();

            // Add linked RDTs as well
            foreach (var v in visited)
            {
                if (v.LinkedRdtId != null)
                {
                    result.Add(_gameData.GetRdt(v.LinkedRdtId.Value)!);
                }
            }

            return result.ToArray();
        }

        private void RandomizeRooms(RandomizedRdt[] rdts)
        {
            var enemyRdts = rdts
                .Where(RdtCanHaveEnemies)
                .Shuffle(_rng)
                .ToList();

            // Clear some rooms of any enemies
            if (_config.RandomEnemyPlacement)
            {
                var maxRooms = rdts.Length;
                var populatedRooms = (int)((_config.EnemyRooms / 7.0) * maxRooms);
                var numEmptyRdts = maxRooms - populatedRooms;
                var numRdtsCleared = 0;
                for (int i = enemyRdts.Count - 1; i >= 0; i--)
                {
                    if (numRdtsCleared >= numEmptyRdts)
                        break;

                    var rdt = enemyRdts[i];
                    if (RemoveAllEnemiesFromRoom(rdt))
                    {
                        enemyRdts.RemoveAt(i);
                        numRdtsCleared++;
                    }
                }
            }

            // Now randomize each populated room's enemies
            var roomRandomized = true;
            var selectableEnemies = _enemyHelper.GetSelectableEnemies();
            var enemyRatios = _config.EnemyRatios
                .Take(selectableEnemies.Length)
                .ToArray();
            var enemyRatioTotal = enemyRatios.Sum(x => x);
            if (enemyRatioTotal == 0)
                throw new BioRandUserException("No enemy ratios set.");

            // Pair up enemy and ratio, remove any with zero ratio
            var enemies = selectableEnemies
                .Zip(enemyRatios, (e, q) => (e, q: (int)q))
                .Where(x => x.q != 0)
                .ToList();

            // Remove rooms that don't support any of our enemies from the list
            enemyRdts.RemoveAll(rdt => !enemies.Any(x => RdtSupportsEnemyType(rdt, x.e.Types)));

            // Multply the ratios by the remaining room count
            enemies = enemies
                .Select(x => (x.e, q: (x.q * enemyRdts.Count) / enemyRatioTotal))
                .OrderBy(x => x.q)
                .ToList();

            // First randomize rooms that ignore enemy ratios
            // This was added for RE 1 to stop Yawn (who can only be in two rooms) always appearing in those rooms
            for (var i = 0; i < enemyRdts.Count; i++)
            {
                var rdt = enemyRdts[i];
                var firstSpec = GetEnemySpecs(rdt.RdtId).FirstOrDefault();
                if (firstSpec == null || !firstSpec.IgnoreRatio)
                    continue;

                var allowedEnemies = enemies
                    .Select(x => x.e)
                    .Where(x => RdtSupportsEnemyType(rdt, x.Types))
                    .Shuffle(_rng);
                if (allowedEnemies.Length == 0)
                    continue;

                RandomizeRoomWithEnemy(rdt, allowedEnemies.First());
                enemyRdts.RemoveAt(i);
                i--;
            }

            // Distribute enemies across remaining rooms
            while (enemyRdts.Count != 0 && roomRandomized)
            {
                roomRandomized = false;

                for (int j = 0; j < enemies.Count; j++)
                {
                    var (selectableEnemy, numRooms) = enemies[j];
                    if (numRooms > 0)
                    {
                        var rdtIndex = enemyRdts.FindIndex(x => RdtSupportsEnemyType(x, selectableEnemy.Types));
                        if (rdtIndex != -1)
                        {
                            // Place enemy/s in room
                            var rdt = enemyRdts[rdtIndex];
                            RandomizeRoomWithEnemy(rdt, selectableEnemy);
                            roomRandomized = true;
                            enemyRdts.RemoveAt(rdtIndex);
                            enemies[j] = (selectableEnemy, numRooms - 1);
                        }
                        else
                        {
                            // Do not try to place this enemy again
                            enemies.RemoveAt(j);
                            j--;
                        }
                    }
                }
            }
        }

        private bool RdtCanHaveEnemies(RandomizedRdt rdt)
        {
            var hasEnemyPlacements =
                _config.RandomEnemyPlacement &&
                _enemyPositions.Any(x => x.RdtId == rdt.RdtId);
            return hasEnemyPlacements || rdt.Enemies
                .Where(x => _enemyHelper.IsEnemy(x.Type))
                .Any();
        }

        private bool RdtSupportsEnemyType(RandomizedRdt rdt, byte[] enemyTypes)
        {
            // RE 1, only allow enemy that was placed in Chris room due to shared sounds
            if (g_stickyEnemies.TryGetValue(rdt.RdtId, out var stickyEnemy))
            {
                if (!stickyEnemy.Types.Intersect(enemyTypes).Any())
                {
                    return false;
                }
            }

            var hasEnemyPlacements =
                _config.RandomEnemyPlacement &&
                _enemyPositions.Any(x => x.RdtId == rdt.RdtId);

            foreach (var enemySpec in GetEnemySpecs(rdt.RdtId))
            {
                foreach (var type in enemyTypes)
                {
                    if (!_enemyHelper.SupportsEnemyType(_config, rdt, hasEnemyPlacements && !enemySpec.KeepPositions, type))
                        continue;

                    if (enemySpec.IncludeTypes != null)
                    {
                        if (!enemySpec.IncludeTypes.Contains(type))
                            return false;
                    }
                    else if (enemySpec.ExcludeTypes != null)
                    {
                        if (enemySpec.ExcludeTypes.Contains(type))
                            return false;
                    }
                    return true;
                }
            }
            return false;
        }

        private MapRoomEnemies[] GetEnemySpecs(RdtId rdtId)
        {
            var enemySpecs = _map.GetRoom(rdtId)?.Enemies;
            if (enemySpecs == null)
            {
                enemySpecs = new[] { new MapRoomEnemies() };
            }
            return enemySpecs
                .Where(IsEnemySpecValid)
                .ToArray();
        }

        private bool IsEnemySpecValid(MapRoomEnemies enemySpec)
        {
            if (enemySpec.Player != null && enemySpec.Player != _config.Player)
                return false;

            if (enemySpec.Scenario != null && enemySpec.Scenario != _config.Scenario)
                return false;

            if (enemySpec.RandomPlacements != null && enemySpec.RandomPlacements != _config.RandomEnemyPlacement)
                return false;

            if (enemySpec.Restricted != null && enemySpec.Restricted != _config.AllowEnemiesAnyRoom)
                return false;

            if (enemySpec.DoorRando != null && enemySpec.DoorRando != _config.RandomDoors)
                return false;

            return true;
        }

        private bool RandomizeRoomWithEnemy(RandomizedRdt rdt, SelectableEnemy targetEnemy)
        {
            _logger.WriteLine($"Randomizing room {rdt} with enemy: {targetEnemy.Name}");

            byte? fixType = null;
            var enemies = rdt.Enemies.ToArray();
            var logEnemies = enemies.Select(GetEnemyLogText).ToArray();

            var enemySpecs = GetEnemySpecs(rdt.RdtId);
            foreach (var enemySpec in enemySpecs)
            {
                RandomiseRoom(_rng, rdt, enemySpec, targetEnemy);
            }

            // Force log if enemy count changed
            var newEnemies = rdt.Enemies.ToArray();
            if (newEnemies.Length != logEnemies.Length)
            {
                foreach (var enemy in newEnemies)
                {
                    if (!rdt.AdditionalOpcodes.Contains(enemy))
                        continue;

                    var newLog = GetNewEnemyLogText(enemy);
                    _logger.WriteLine($"  Created {rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) {newLog}");
                    fixType ??= enemy.Type;
                }
            }
            else
            {
                for (int i = 0; i < logEnemies.Length; i++)
                {
                    var enemy = newEnemies[i];
                    var oldLog = logEnemies[i];
                    var newLog = GetEnemyLogText(enemy);
                    if (oldLog != newLog)
                    {
                        _logger.WriteLine($"  {rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) {oldLog} becomes {newLog}");
                        fixType ??= enemy.Type;
                    }
                }
            }

            if (fixType != null)
            {
                AddRequiredEsps(rdt, fixType.Value);
            }

            if (rdt.Version == BioVersion.Biohazard1)
            {
                if (fixType != null)
                {
                    if (_config.Player == 0)
                    {
                        g_stickyEnemies.Add(rdt.RdtId, targetEnemy);
                    }
                }
            }
            else
            {
                // Mute all NPCs in the room so that we can hear enemies
                foreach (var em in rdt.Enemies)
                {
                    if (!_enemyHelper.IsEnemy(em.Type))
                    {
                        em.SoundBank = 0;
                    }
                }
            }
            return fixType != null;
        }

        private void AddRequiredEsps(RandomizedRdt rdt, byte enemyType)
        {
            var espIds = _enemyHelper.GetRequiredEsps(enemyType);
            if (espIds.Length == 0)
                return;

            var rdtFile = rdt.RdtFile;
            var embeddedEffects = rdtFile.EmbeddedEffects;
            var missingIds = espIds.Except(embeddedEffects.Ids).ToArray();
            if (missingIds.Length == 0)
                return;

            var existingEffects = embeddedEffects.Effects.ToList();
            foreach (var id in missingIds)
            {
                existingEffects.Add(_effects[id]);
                _logger.WriteLine($"  {rdt.RdtId} ESP{id:X2} added");
            }

            var rdtBuilder = rdtFile.ToBuilder();
            rdtBuilder.EmbeddedEffects = new EmbeddedEffectList(rdtFile.Version, existingEffects.ToArray());
            rdt.RdtFile = rdtBuilder.ToRdt();
        }

        private string GetEnemyLogText(SceEmSetOpcode enemy)
        {
            return $"[{_enemyHelper.GetEnemyName(enemy.Type)},{enemy.State},{enemy.Ai},{enemy.SoundBank},{enemy.Texture}]";
        }

        private string GetNewEnemyLogText(SceEmSetOpcode enemy)
        {
            return $"[{_enemyHelper.GetEnemyName(enemy.Type)},{enemy.State},{enemy.Ai},{enemy.SoundBank},{enemy.Texture}] at ({enemy.X},{enemy.Y},{enemy.Z})";
        }

        private bool RemoveAllEnemiesFromRoom(RandomizedRdt rdt)
        {
            // Only count rooms that could have had enemies changed
            var newPlacements = _enemyPositions.Count(x => x.RdtId == rdt.RdtId);
            if (newPlacements == 0)
                return false;

            var numEnemiesRemoved = 0;
            var enemySpecs = GetEnemySpecs(rdt.RdtId);
            foreach (var enemySpec in enemySpecs)
            {
                if (enemySpec.Nop != null)
                {
                    var nopArray = Map.ParseNopArray(enemySpec.Nop, rdt);
                    foreach (var offset in nopArray)
                    {
                        rdt.Nop(offset);
                        if (g_debugLogging)
                            _logger.WriteLine($"{rdt.RdtId} (0x{offset:X2}) opcode removed");
                    }
                }

                var currentEnemies = rdt.Enemies
                    .Where(e => enemySpec.ExcludeOffsets?.Contains(e.Offset) != true)
                    .Where(e => _enemyHelper.ShouldChangeEnemy(_config, e))
                    .ToArray();

                foreach (var enemy in currentEnemies)
                {
                    rdt.Nop(enemy.Offset);
                    numEnemiesRemoved++;
                }
            }
            if (numEnemiesRemoved != 0)
                _logger.WriteLine($"{rdt.RdtId}, {numEnemiesRemoved} enemies removed");
            return true;
        }

        private void RandomiseRoom(Rng rng, RandomizedRdt rdt, MapRoomEnemies enemySpec, SelectableEnemy targetEnemy)
        {
            if (enemySpec.Nop != null)
            {
                var nopArray = Map.ParseNopArray(enemySpec.Nop, rdt);
                foreach (var offset in nopArray)
                {
                    rdt.Nop(offset);
                    if (g_debugLogging)
                        _logger.WriteLine($"{rdt.RdtId} (0x{offset:X2}) opcode removed");
                }
            }

            var enemiesToChange = rdt.Enemies
                .Where(e => enemySpec.ExcludeOffsets?.Contains(e.Offset) != true)
                .Where(e => _enemyHelper.ShouldChangeEnemy(_config, e))
                .ToArray();

            var possibleTypes = targetEnemy.Types.Shuffle(_rng);
            if (enemySpec.IncludeTypes != null)
            {
                var includeTypes = enemySpec.IncludeTypes.Select(x => (byte)x).ToHashSet();
                possibleTypes = possibleTypes.Intersect(includeTypes).ToArray();
            }
            else if (enemySpec.ExcludeTypes != null)
            {
                var excludeTypes = enemySpec.ExcludeTypes.Select(x => (byte)x).ToHashSet();
                possibleTypes = possibleTypes.Except(excludeTypes).ToArray();
            }
            if (possibleTypes.Length == 0)
                return;

            var randomEnemiesToChange = new SceEmSetOpcode[0];
            if (_config.RandomEnemyPlacement && !enemySpec.KeepPositions)
            {
                randomEnemiesToChange = GenerateRandomEnemies(rng, rdt, enemySpec, enemiesToChange, possibleTypes[0]);
            }
            if (randomEnemiesToChange.Length != 0)
            {
                enemiesToChange = randomEnemiesToChange;
            }
            else
            {
                var quantity = enemiesToChange.DistinctBy(x => x.Id).Count();
                possibleTypes = possibleTypes.Where(type =>
                {
                    var difficulty = Math.Min(enemySpec.MaxDifficulty ?? 3, _config.EnemyDifficulty);
                    var maxQuantity = _enemyHelper.GetEnemyTypeLimit(_config, enemySpec.MaxDifficulty ?? _config.EnemyDifficulty, type);
                    return maxQuantity >= quantity;
                }).ToArray();
            }

            if (possibleTypes.Length == 0)
                return;

            var randomEnemyType = possibleTypes[0];
            var ids = rdt.Enemies.Select(x => x.Id).Distinct().ToArray();
            var enemyTypesId = ids.Select(x => randomEnemyType).ToArray();

            _enemyHelper.BeginRoom(rdt);

            for (var i = 0; i < enemiesToChange.Length; i++)
            {
                var enemy = enemiesToChange[i];
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

                foreach (var dependencyType in _enemyHelper.GetEnemyDependencies(enemyType))
                {
                    i++;
                    enemy = enemiesToChange[i];
                    enemy.Type = dependencyType;
                    _enemyHelper.SetEnemy(_config, rng, enemy, enemySpec, enemyType);
                }
            }
        }

        private SceEmSetOpcode[] GenerateRandomEnemies(Rng rng, RandomizedRdt rdt, MapRoomEnemies enemySpec, SceEmSetOpcode[] currentEnemies, byte enemyType)
        {
            var relevantPlacements = _enemyPositions
                .Where(x => x.RdtId == rdt.RdtId)
                .ToEndlessBag(_rng);

            if (relevantPlacements.Count == 0)
                return new SceEmSetOpcode[0];

            var difficulty = Math.Min(enemySpec.MaxDifficulty ?? 3, _config.EnemyDifficulty);
            var enemyTypeLimit = _enemyHelper.GetEnemyTypeLimit(_config, difficulty, enemyType);
            var avg = 1 + _config.EnemyQuantity;
            var quantity = rng.Next(1, avg * 2);
            quantity = Math.Min(quantity, Math.Min(enemyTypeLimit, relevantPlacements.Count * 3));

            foreach (var enemy in currentEnemies)
                rdt.Nop(enemy.Offset);

            var usedIds = rdt.Enemies.Select(x => x.Id).ToHashSet();

            var enemies = new List<SceEmSetOpcode>();
            byte enemyId = 0;

            var enemyOpcodes = new List<OpcodeBase>();
            var firstEnemyOpcodeIndex = rdt.AdditionalOpcodes.Count;
            for (var i = 0; i < quantity; i++)
            {
                var ep = relevantPlacements.Next();
                while (usedIds.Contains(enemyId))
                {
                    enemyId++;
                }

                var killId = GetNextKillId();
                var newEnemy = CreateEnemy(enemyId, killId, ep);
                enemyOpcodes.Add(newEnemy);
                enemies.Add(newEnemy);
                enemyId++;

                var dependencies = _enemyHelper.GetEnemyDependencies(enemyType);
                foreach (var d in dependencies)
                {
                    newEnemy = CreateEnemy(enemyId, killId, ep);
                    enemyOpcodes.Add(newEnemy);
                    enemies.Add(newEnemy);
                }
            }

            InsertConditions(rdt, enemyOpcodes, enemySpec.Condition);

            return enemies.ToArray();
        }

        private SceEmSetOpcode CreateEnemy(byte id, byte killId, EnemyPosition ep)
        {
            if (_version == BioVersion.Biohazard1)
            {
                var enemy = new SceEmSetOpcode()
                {
                    Length = 22,
                    Opcode = (byte)OpcodeV1.SceEmSet,
                    Type = Re1EnemyIds.Zombie,
                    State = 0,
                    KillId = killId,
                    Re1Unk04 = 1,
                    Re1Unk05 = 2,
                    Re1Unk06 = 0,
                    Re1Unk07 = 0,
                    D = (short)ep.D,
                    Re1Unk0A = 0,
                    Re1Unk0B = 0,
                    X = (short)ep.X,
                    Y = (short)ep.Y,
                    Z = (short)ep.Z,
                    Id = id,
                    Re1Unk13 = 0,
                    Re1Unk14 = 0,
                    Re1Unk15 = 0,
                };
                return enemy;
            }
            else
            {
                var enemy = new SceEmSetOpcode()
                {
                    Length = _config.Game == 2 ? 22 : 24,
                    Opcode = _config.Game == 2 ? (byte)OpcodeV2.SceEmSet : (byte)OpcodeV3.SceEmSet,
                    Unk01 = 0,
                    Id = id,
                    Type = _config.Game == 2 ? Re2EnemyIds.ZombieRandom : Re3EnemyIds.ZombieDog,
                    State = 0,
                    Ai = 0,
                    Floor = (byte)ep.F,
                    SoundBank = (byte)(_config.Game == 2 ? 9 : 32),
                    Texture = 0,
                    KillId = killId,
                    X = (short)ep.X,
                    Y = (short)ep.Y,
                    Z = (short)ep.Z,
                    D = (short)ep.D,
                    Animation = 0,
                    Unk15 = 0
                };
                return enemy;
            }
        }

        private void InsertConditions(RandomizedRdt rdt, List<OpcodeBase> enemyOpcodes, string? condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                rdt.AdditionalOpcodes.AddRange(enemyOpcodes);
                return;
            }

            // if (_config.Game != 3)
            //     throw new NotSupportedException("Enemy conditions not supported for this game");

            var scdCondition = ScdCondition.Parse(condition!);
            var opcodes = scdCondition.Generate(_version, enemyOpcodes);
            rdt.AdditionalOpcodes.AddRange(opcodes);
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

        private void FixRooms()
        {
            // For RE 2, room 402, 405, 407, 501, and 504 crash if partner and random enemy placements
            // are both enabled.
            if (_config.RandomDoors)
            {
                DetachPartner(new RdtId(3, 0x02));
                DetachPartner(new RdtId(3, 0x05));
                DetachPartner(new RdtId(3, 0x07));
                DetachPartner(new RdtId(4, 0x04));
            }
        }

        private void DetachPartner(RdtId rdtId)
        {
            var rdt = _gameData.GetRdt(rdtId);
            if (rdt != null && rdt.Version == BioVersion.Biohazard2)
            {
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x22, new byte[] { 0x01, 0x03, 0x00 }));
            }
        }

        public struct EnemyPosition : IEquatable<EnemyPosition>
        {
            public RdtId RdtId { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public int D { get; set; }
            public int F { get; set; }

            public string? Room
            {
                get => RdtId.ToString();
                set => RdtId = value == null ? default(RdtId) : RdtId.Parse(value);
            }

            public override bool Equals(object? obj)
            {
                return obj is EnemyPosition pos ? Equals(pos) : false;
            }

            public bool Equals(EnemyPosition other)
            {
                return other is EnemyPosition position &&
                       Room == position.Room &&
                       X == position.X &&
                       Y == position.Y &&
                       Z == position.Z &&
                       D == position.D &&
                       F == position.F;
            }

            public override int GetHashCode()
            {
                return (Room?.GetHashCode() ?? 0) ^ X ^ Y ^ Z ^ D ^ F;
            }
        }
    }
}
