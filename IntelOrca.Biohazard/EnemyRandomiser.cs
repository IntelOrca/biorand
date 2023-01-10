using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
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
            var json = _dataManager.GetText(_version, "enemy.json");
            _enemyPositions = JsonSerializer.Deserialize<EnemyPosition[]>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
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

        public void Randomise()
        {
            _logger.WriteHeading("Randomizing enemies:");
            ReadEnemyPlacements();
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

            foreach (var rdt in _gameData.Rdts)
            {
                byte? fixType = null;
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
                        fixType ??= enemy.Type;
                    }
                }

                if (rdt.Version == BioVersion.Biohazard1 && fixType != null)
                {
                    FixRE1Sounds(rdt.RdtId, fixType.Value);
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

            if (enemySpec.RandomPlacements != null && enemySpec.RandomPlacements != _config.RandomEnemyPlacement)
                return;
            
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

            if (_config.RandomEnemyPlacement)
            {
                enemiesToChange = GenerateRandomEnemies(rng, rdt, enemiesToChange);
            }

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

        private SceEmSetOpcode[] GenerateRandomEnemies(Rng rng, Rdt rdt, SceEmSetOpcode[] currentEnemies)
        {
            var relevantPlacements = _enemyPositions
                .Where(x => x.RdtId == rdt.RdtId)
                .Shuffle(rng);

            if (relevantPlacements.Length == 0)
                return currentEnemies;

            var maxQuantity = (_config.EnemyDifficulty + 1) * 4;
            var upperBound = Math.Max(maxQuantity, relevantPlacements.Length);
            var quantity = rng.Next(0, upperBound);
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
                _logger.WriteLine($"Created new enemy at {ep.RdtId}, {ep.X}, {ep.Y}, {ep.Z}");

                // if (enemyId >= 8)
                //     break;
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
