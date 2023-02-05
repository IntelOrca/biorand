using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using IntelOrca.Biohazard.RE1;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class EnemyRandomiser
    {
        private static object g_xmlSync = new object();

        private BioVersion _version;
        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly IEnemyHelper _enemyHelper;
        private readonly string _modPath;
        private readonly DataManager _dataManager;
        private XmlDocument? _re1sounds;
        private EnemyPosition[] _enemyPositions = new EnemyPosition[0];
        private HashSet<byte> _killIdPool = new HashSet<byte>();
        private Queue<byte> _killIds = new Queue<byte>();

        public EnemyRandomiser(BioVersion version, RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng rng, IEnemyHelper enemyHelper, string modPath, DataManager dataManager)
        {
            _version = version;
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = rng;
            _enemyHelper = enemyHelper;
            _modPath = modPath;
            _dataManager = dataManager;
        }

        private void ReadEnemyPlacements()
        {
            if (_config.RandomEnemyPlacement)
            {
                var json = _dataManager.GetText(_version, "enemy.json");
                _enemyPositions = JsonSerializer.Deserialize<EnemyPosition[]>(json, new JsonSerializerOptions()
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })!;
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
            foreach (var rdt in _gameData.Rdts)
            {
                if (_enemyPositions.Any(x => x.RdtId == rdt.RdtId))
                    continue;

                var killIds = rdt.Enemies
                    .Where(x => _enemyHelper.IsEnemy(x.Type))
                    .Select(x => x.KillId)
                    .ToArray();
                _killIdPool.RemoveMany(killIds);
            }
        }

        public void Randomise(PlayGraph? graph)
        {
            _logger.WriteHeading("Randomizing enemies:");
            ReadEnemyPlacements();
            SetupRandomEnemyPlacements();
            RandomizeRooms(GetAccessibleRdts(graph));
            FixRooms();
        }

        private Rdt[] GetAccessibleRdts(PlayGraph? graph)
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
            return visited
                .Select(x => _gameData.GetRdt(x.RdtId)!)
                .ToArray();
        }

        private void RandomizeRooms(Rdt[] rdts)
        {
            var enemyRdts = rdts
                .Where(RdtCanHaveEnemies)
                .Shuffle(_rng)
                .ToList();

            if (_config.RandomEnemyPlacement)
            {
                var maxArray = new[] { 3, 5, 8, 10 };
                var maxQuantity = maxArray[_config.EnemyQuantity];
                var numEmptyRdts = Enumerable.Range(0, enemyRdts.Count)
                    .Count(x => _rng.Next(0, maxQuantity) == 0);
                numEmptyRdts = Math.Min(numEmptyRdts, enemyRdts.Count);

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

            var roomRandomized = true;
            while (enemyRdts.Count != 0 && roomRandomized)
            {
                roomRandomized = false;
                var enemyRatioTotal = _config.EnemyRatios.Sum(x => x);
                if (enemyRatioTotal == 0)
                    throw new BioRandUserException("No enemy ratios set.");

                var enemyRooms = _config.EnemyRatios.Select(x => (x * enemyRdts.Count) / enemyRatioTotal);
                var enemies = _enemyHelper.GetSelectableEnemies()
                    .Zip(enemyRooms, (e, q) => (e, q))
                    .Where(x => x.q != 0)
                    .OrderBy(x => x.q)
                    .ToList();

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

        private bool RdtCanHaveEnemies(Rdt rdt)
        {
            var hasEnemyPlacements =
                _config.RandomEnemyPlacement &&
                _enemyPositions.Any(x => x.RdtId == rdt.RdtId);
            return hasEnemyPlacements || rdt.Enemies
                .Where(x => _enemyHelper.IsEnemy(x.Type))
                .Any();
        }

        private bool RdtSupportsEnemyType(Rdt rdt, byte[] enemyTypes)
        {
            var hasEnemyPlacements =
                _config.RandomEnemyPlacement &&
                _enemyPositions.Any(x => x.RdtId == rdt.RdtId);

            foreach (var enemySpec in GetEnemySpecs(rdt.RdtId))
            {
                foreach (var type in enemyTypes)
                {
                    if (!_enemyHelper.SupportsEnemyType(_config, rdt, enemySpec.Difficulty ?? "", hasEnemyPlacements && !enemySpec.KeepPositions, type))
                        continue;

                    if (enemySpec.IncludeTypes != null)
                    {
                        if (enemySpec.IncludeTypes.Contains(type))
                            return true;
                    }
                    else if (enemySpec.ExcludeTypes != null)
                    {
                        if (!enemySpec.ExcludeTypes.Contains(type))
                            return true;
                    }
                    else
                    {
                        return true;
                    }
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

        private bool RandomizeRoomWithEnemy(Rdt rdt, SelectableEnemy targetEnemy)
        {
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
                    _logger.WriteLine($"Created {rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) {newLog}");
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
                        _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) {oldLog} becomes {newLog}");
                        fixType ??= enemy.Type;
                    }
                }
            }

            if (rdt.Version == BioVersion.Biohazard1)
            {
                if (fixType != null)
                {
                    FixRE1Sounds(rdt.RdtId, fixType.Value);
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

        private string GetEnemyLogText(SceEmSetOpcode enemy)
        {
            return $"[{_enemyHelper.GetEnemyName(enemy.Type)},{enemy.State},{enemy.Ai},{enemy.SoundBank},{enemy.Texture}]";
        }

        private string GetNewEnemyLogText(SceEmSetOpcode enemy)
        {
            return $"[{_enemyHelper.GetEnemyName(enemy.Type)},{enemy.State},{enemy.Ai},{enemy.SoundBank},{enemy.Texture}] at ({enemy.X},{enemy.Y},{enemy.Z})";
        }

        private bool RemoveAllEnemiesFromRoom(Rdt rdt)
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

        private void RandomiseRoom(Rng rng, Rdt rdt, MapRoomEnemies enemySpec, SelectableEnemy targetEnemy)
        {
            if (enemySpec.Nop != null)
            {
                var nopArray = Map.ParseNopArray(enemySpec.Nop, rdt);
                foreach (var offset in nopArray)
                {
                    rdt.Nop(offset);
                    _logger.WriteLine($"{rdt.RdtId} (0x{offset:X2}) opcode removed");
                }
            }

            var enemiesToChange = rdt.Enemies
                .Where(e => enemySpec.ExcludeOffsets?.Contains(e.Offset) != true)
                .Where(e => _enemyHelper.ShouldChangeEnemy(_config, e))
                .ToArray();

            var includeTypes = enemySpec.IncludeTypes == null ?
                null :
                enemySpec.IncludeTypes.Select(x => (byte)x).ToHashSet();
            var excludeTypes = enemySpec.ExcludeTypes == null ?
                new HashSet<byte>() :
                enemySpec.ExcludeTypes.Select(x => (byte)x).ToHashSet();

            var possibleTypes = targetEnemy.Types
                .Where(x => !excludeTypes.Contains(x))
                .Shuffle(_rng)
                .ToArray();
            if (possibleTypes.Length == 0)
                return;

            if (_config.RandomEnemyPlacement && !enemySpec.KeepPositions)
            {
                enemiesToChange = GenerateRandomEnemies(rng, rdt, enemiesToChange, possibleTypes[0]);
            }

            // _enemyHelper.ExcludeEnemies(_config, rdt, enemySpec.Difficulty ?? "", x => excludeTypes.Add(x));
            // possibleTypes = possibleTypes
            //     .Where(x => !excludeTypes.Contains(x))
            //     .ToArray();

            if (possibleTypes.Length == 0)
                return;

            var randomEnemyType = possibleTypes[0];
            var ids = rdt.Enemies.Select(x => x.Id).Distinct().ToArray();
            var enemyTypesId = ids.Select(x => randomEnemyType).ToArray();

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

        private SceEmSetOpcode[] GenerateRandomEnemies(Rng rng, Rdt rdt, SceEmSetOpcode[] currentEnemies, byte enemyType)
        {
            var relevantPlacements = _enemyPositions
                .Where(x => x.RdtId == rdt.RdtId)
                .Shuffle(rng);

            if (relevantPlacements.Length == 0)
                return currentEnemies;

            var maxArray = new[] { 3, 5, 8, 10 };
            var avgArray = new[] { 1, 2, 4, 6 };
            var max = maxArray[Math.Min(_config.EnemyQuantity, maxArray.Length - 1)];
            var avg = avgArray[Math.Min(_config.EnemyQuantity, avgArray.Length - 1)];
            max = Math.Min(max, relevantPlacements.Length);
            var quantity = rng.Next(1, avg * 2);
            quantity = Math.Min(quantity, max);
            quantity = Math.Min(quantity, _enemyHelper.GetEnemyTypeLimit(_config, enemyType));
            relevantPlacements = relevantPlacements
                .Take(quantity)
                .ToArray();

            foreach (var enemy in currentEnemies)
                rdt.Nop(enemy.Offset);

            var usedIds = rdt.Enemies.Select(x => x.Id).ToHashSet();

            var enemies = new List<SceEmSetOpcode>();
            byte enemyId = 0;
            byte killId = 0;
            foreach (var ep in relevantPlacements)
            {
                while (usedIds.Contains(enemyId))
                {
                    enemyId++;
                }

                var newEnemy = new SceEmSetOpcode()
                {
                    Length = 22,
                    Opcode = (byte)OpcodeV2.SceEmSet,
                    Unk01 = 0,
                    Id = enemyId,
                    Type = (byte)EnemyType.ZombieRandom,
                    State = 0,
                    Ai = 0,
                    Floor = (byte)ep.F,
                    SoundBank = 9,
                    Texture = 0,
                    KillId = GetNextKillId(),
                    X = (short)ep.X,
                    Y = (short)ep.Y,
                    Z = (short)ep.Z,
                    D = (short)ep.D,
                    Animation = 0,
                    Unk15 = 0
                };
                rdt.AdditionalOpcodes.Add(newEnemy);
                enemies.Add(newEnemy);
                enemyId++;
                killId++;
            }
            return enemies.ToArray();
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

        private void FixRE1Sounds(RdtId rdtId, byte enemyType)
        {
            // XML files are shared between players, so make sure it is thread safe
            lock (g_xmlSync)
            {
                var xmlDir = Path.Combine(_modPath, "tables");
                var xmlPath = Path.Combine(xmlDir, $"room_{rdtId}.xml");
                if (File.Exists(xmlPath))
                {
                    // XML file already produced
                    return;
                }

                var roomNode = GetRoomXml(rdtId);
                if (roomNode == null)
                    return;

                var template = GetTemplateXml(enemyType);
                var entryNodes = roomNode.SelectNodes("Sound/Entry");
                for (int i = 0; i < 16; i++)
                {
                    entryNodes[i].InnerText = template[i] ?? "";
                }

                var xml = roomNode.InnerXml;
                Directory.CreateDirectory(xmlDir);
                File.WriteAllText(xmlPath, xml);
            }
        }

        private XmlNode? GetRoomXml(RdtId rdtId)
        {
            var doc = _re1sounds;
            if (doc == null)
            {
                var xml = _dataManager.GetText(_version, "sounds.xml");
                doc = new XmlDocument();
                doc.LoadXml(xml);
                _re1sounds = doc;
            }

            var roomNodes = doc.SelectNodes("Rooms/Room");
            foreach (XmlNode roomNode in roomNodes)
            {
                var idAttribute = roomNode.Attributes["id"];
                if (idAttribute == null)
                    continue;

                if (!RdtId.TryParse(idAttribute.Value, out var roomId))
                    continue;

                if (roomId != rdtId)
                    continue;

                return roomNode;
            }

            return null;
        }

        private string[] GetTemplateXml(byte enemyType)
        {
            string[]? result = null;
            switch (enemyType)
            {
                case Re1EnemyIds.Zombie:
                    result = new[] { "z_taore", "z_ftL", "z_ftR", "z_kamu", "z_k02", "z_k01", "z_head", "z_haki", "z_sanj", "z_k03" };
                    break;
                case Re1EnemyIds.ZombieNaked:
                    result = new[] { "z_taore", "zep_ftL", "z_ftR", "ze_kamu", "z_nisi2", "z_nisi1", "ze_head", "ze_haki", "ze_sanj", "z_nisi3", "FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash" };
                    break;
                case Re1EnemyIds.Cerberus:
                    result = new[] { "cer_foot", "cer_taoA", "cer_unar", "cer_bite", "cer_cryA", "cer_taoB", "cer_jkMX", "cer_kamu", "cer_cryB", "cer_runMX" };
                    break;
                case Re1EnemyIds.SpiderBrown:
                    result = new[] { "kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "sp_sanj2" };
                    break;
                case Re1EnemyIds.SpiderBlack:
                    result = new[] { "kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "poison" };
                    break;
                case Re1EnemyIds.Crow:
                    result = new[] { "RVcar1", "RVpat", "RVcar2", "RVwing1", "RVwing2", "RVfryed" };
                    break;
                case Re1EnemyIds.Hunter:
                    result = new[] { "HU_walkA", "HU_walkB", "HU_jump", "HU_att", "HU_land", "HU_smash", "HU_dam", "HU_Nout" };
                    break;
                case Re1EnemyIds.Bee:
                    result = new[] { "bee4_ed", "hatinage", "bee_fumu" };
                    break;
                case Re1EnemyIds.Plant42:
                    break;
                case Re1EnemyIds.Chimera:
                    result = new[] { "FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash", "FL_att", "FL_dam", "FL_out" };
                    break;
                case Re1EnemyIds.Snake:
                    result = new[] { "PY_mena", "PY_hit2", "PY_fall" };
                    break;
                case Re1EnemyIds.Neptune:
                    result = new[] { "nep_attB", "nep_attA", "nep_nomu", "nep_tura", "nep_twis", "nep_jump" };
                    break;
                case Re1EnemyIds.Tyrant1:
                    result = new[] { "TY_foot", "TY_kaze", "TY_slice", "TY_HIT", "TY_trust", "", "TY_taore", "TY_nage" };
                    break;
                case Re1EnemyIds.Yawn1:
                    break;
                case Re1EnemyIds.Plant42Roots:
                    break;
                case Re1EnemyIds.Plant42Vines:
                    break;
                case Re1EnemyIds.Tyrant2:
                    break;
                case Re1EnemyIds.ZombieResearcher:
                    result = new[] { "z_taore", "z_ftL", "z_ftR", "z_kamu", "z_mika02", "z_mika01", "z_head", "z_Hkick", "z_Ugoron", "z_mika03" };
                    break;
                case Re1EnemyIds.Yawn2:
                    break;
            }
            Array.Resize(ref result, 16);
            return result;
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
                DetachPartner(new RdtId(4, 0x01));
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
            private RdtId _rdtId;

            public RdtId RdtId => _rdtId;

            public string? Room
            {
                get => _rdtId.ToString();
                set
                {
                    _rdtId = value == null ? default(RdtId) : RdtId.Parse(value);
                }
            }

            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public int D { get; set; }
            public int F { get; set; }

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
