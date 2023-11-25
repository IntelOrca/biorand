using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.Script.Opcodes;
using static IntelOrca.Biohazard.BioRand.EnemyRandomiser;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private static bool g_debugLogging = false;

        private readonly RandoLogger _logger;
        private readonly DataManager _dataManager;
        private readonly RandoConfig _config;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly ItemRandomiser? _itemRandomiser;
        private readonly EnemyRandomiser? _enemyRandomiser;
        private readonly NPCRandomiser? _npcRandomiser;
        private readonly VoiceRandomiser? _voiceRandomiser;
        private readonly Dictionary<RdtId, CutsceneRoomInfo> _cutsceneRoomInfoMap = new Dictionary<RdtId, CutsceneRoomInfo>();
        private EnemyPosition[] _allEnemyPositions = new EnemyPosition[0];
        private Plot[] _registeredPlots = new Plot[0];
        private EndlessBag<ReFlag> _globalFlags;

        // Current room
        private CutsceneBuilder _cb = new CutsceneBuilder();
        private RandomizedRdt? _rdt;
        private RdtId _rdtId;
        private ReFlag _plotFlag;
        private ReFlag? _lastPlotFlag;
        private PointOfInterest[] _poi = new PointOfInterest[0];
        private int[] _allKnownCuts = new int[0];
        private EndlessBag<REPosition> _enemyPositions;
        private Rng _plotRng;
        private int _currentEnemyCount;
        private int _maximumEnemyCount;
        private byte? _enemyType;

        public CutsceneRandomiser(
            RandoLogger logger,
            DataManager dataManager,
            RandoConfig config,
            GameData gameData,
            Map map,
            Rng rng,
            ItemRandomiser? itemRandomiser,
            EnemyRandomiser? enemyRandomiser,
            NPCRandomiser? npcRandomiser,
            VoiceRandomiser? voiceRandomiser)
        {
            _logger = logger;
            _dataManager = dataManager;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = rng;
            _plotRng = rng;
            _itemRandomiser = itemRandomiser;
            _enemyRandomiser = enemyRandomiser;
            _npcRandomiser = npcRandomiser;
            _voiceRandomiser = voiceRandomiser;
            _enemyPositions = new EndlessBag<REPosition>(new Rng(), new REPosition[0]);

            var flags3 = _availableFlags3.Select(x => new ReFlag(CutsceneBuilder.FG_SCENARIO, x));
            var flags4 = _availableFlags4.Select(x => new ReFlag(CutsceneBuilder.FG_COMMON, x));
            _globalFlags = flags3.Concat(flags4).ToEndlessBag(rng);

            LoadCutsceneRoomInfo();
            ReadEnemyPlacements();
            InitialisePlots();
        }

        public void Randomise(PlayGraph? graph)
        {
            _logger.WriteHeading("Randomizing cutscenes");

            var rdts = graph?.GetAccessibleRdts(_gameData) ?? _gameData.Rdts;
            rdts = rdts.OrderBy(x => x.RdtId).ToArray();
            foreach (var rdt in rdts)
            {
                RandomizeRoom(rdt, _rng.NextFork());
            }
        }

        public void RandomizeRoom(RandomizedRdt rdt, Rng rng)
        {
            if (!_cutsceneRoomInfoMap.TryGetValue(rdt.RdtId, out var info))
                return;

            _enemyPositions = _allEnemyPositions
                .Where(x => x.RdtId == rdt.RdtId)
                .Select(p => new REPosition(p.X, p.Y, p.Z, p.D))
                .ToEndlessBag(rng);

            if (_enemyPositions.Count == 0)
                return;

            _logger.WriteLine($"{rdt}:");

            if (_enemyRandomiser != null)
            {
                ClearEnemies(rdt);
            }

            var doors = info.Poi?.Where(x => x.HasTag(PoiKind.Door)).ToArray() ?? new PointOfInterest[0];
            var triggers = info.Poi?.Where(x => x.HasTag(PoiKind.Trigger)).ToArray() ?? new PointOfInterest[0];
            var meets = info.Poi?.Where(x => x.HasTag(PoiKind.Meet)).ToArray() ?? new PointOfInterest[0];

            var cb = new CutsceneBuilder();
            cb.Begin();

            _cb = cb;
            _rdt = rdt;
            _rdtId = rdt.RdtId;
            _plotRng = rng;
            _lastPlotFlag = null;
            _poi = info.Poi ?? new PointOfInterest[0];
            _allKnownCuts = _poi.SelectMany(x => x.AllCuts).ToArray();
            _currentEnemyCount = 0;

            var enemyHelper = _enemyRandomiser?.EnemyHelper;
            if (_enemyRandomiser?.ChosenEnemies.TryGetValue(_rdt, out var enemy) == true)
            {
                _enemyType = enemy.Types[0];

                var spec = GetEnemySpecs(_rdtId);
                var difficulty = Math.Min(_config.EnemyDifficulty, spec.MaxDifficulty ?? _config.EnemyDifficulty);
                var enemyTypeLimit = enemyHelper!.GetEnemyTypeLimit(_config, difficulty, _enemyType.Value);

                var avg = 1 + _config.EnemyQuantity;
                var quantity = rng.Next(1, avg * 2);
                _maximumEnemyCount = Math.Min(quantity, Math.Min(enemyTypeLimit, _enemyPositions.Count * 3));
            }
            else
            {
                _enemyType = null;
                _maximumEnemyCount = 0;
            }

            TidyPoi();

            var reservedIds = _rdt.Enemies
                .Select(x => x.Id)
                .Distinct()
                .ToArray();
            var availableIds = Enumerable
                .Range(0, 32)
                .Select(x => (byte)x)
                .Except(reservedIds)
                .ToArray();
            foreach (var id in availableIds)
            {
                _cb.AvailableEnemyIds.Enqueue(id);
            }

            var aotIds = Enumerable.Range(0, 32).Select(x => (byte)x).ToHashSet();
            foreach (var opcode in _rdt.AllOpcodes)
            {
                if (opcode is IAot aot)
                {
                    aotIds.Remove(aot.Id);
                }
            }
            foreach (var id in aotIds)
            {
                _cb.AvailableAotIds.Enqueue(id);
            }

            var localFlags = Enumerable.Range(24, 64)
                .Select(x => new ReFlag(CutsceneBuilder.FG_ROOM, (byte)x))
                .ToArray();

            var plotBuilder = new PlotBuilder(
                _config,
                rng,
                _enemyRandomiser,
                _npcRandomiser,
                _voiceRandomiser,
                _rdt,
                new PoiGraph(_poi),
                _enemyPositions,
                _globalFlags,
                localFlags,
                availableIds.ToArray(),
                aotIds.ToArray(),
                _enemyType,
                _maximumEnemyCount);

            var plots = new List<CsPlot>();

            if (_enemyType.HasValue)
            {
                // var enemyPlot = ChainRandomPlot<StaticEnemyPlot>(plotBuilder);
                // if (enemyPlot != null)
                // {
                //     plots.Add(enemyPlot);
                // }

                for (var i = 0; i < 3; i++)
                {
                    var enemyEnterPlot = ChainRandomPlot<EnemyWalksInPlot>(plotBuilder);
                    if (enemyEnterPlot != null)
                    {
                        plots.Add(enemyEnterPlot);
                    }
                }

                // var enemyDarkness = ChainRandomPlot<EnemyFromDarkPlot>(plotBuilder);
                // if (enemyDarkness != null)
                // {
                //     plots.Add(enemyDarkness);
                // }

                // var enemyWakeUp = ChainRandomPlot<EnemyWakeUpPlot>(plotBuilder);
                // if (enemyWakeUp != null)
                // {
                //     plots.Add(enemyWakeUp);
                // }
            }

            var allyPlot = ChainRandomPlot<AllyWaitPlot>(plotBuilder);
            if (allyPlot != null)
            {
                plots.Add(allyPlot);
            }

            foreach (var plot in plots)
            {
                var procedures = GetAllProcedures(plot.Root);
                foreach (var proc in procedures)
                {
                    proc.Build(_cb);
                }
                LogPlot(plot);
            }

            var entryProc = new SbProcedure("biorand_custom",
                plots
                    .Select(x => new SbCall(x.Root))
                    .ToArray());
            entryProc.Build(_cb);

            // ChainRandomPlot<EnemyChangePlot>();
            // ChainRandomPlot<EnemyWakeUpPlot>();
            // ChainRandomPlot<EnemyWalksInPlot>();
            // ChainRandomPlot<AllyWalksInPlot>();

            // for (var i = 0; i < 4; i++)
            // {
            //     ChainRandomPlot<AllyStaticPlot>();
            // }

            // if (rng.NextProbability(50))
            // {
            //     ChainRandomPlot<AllyStaticPlot>();
            // }
            // if (rng.NextProbability(25))
            // {
            //     ChainRandomPlot<AllyPassByPlot>();
            // }
            if (_enemyType != null)
            {
                if (_enemyType != Re2EnemyIds.ZombieArms &&
                    _enemyType != Re2EnemyIds.GAdult)
                {
                    /*
                    if (rng.NextProbability(10))
                    {
                        ChainRandomPlot<EnemyFromDarkPlot>();
                    }
                    else
                    {
                        var wakeUp = false;
                        if (enemyHelper!.IsZombie(_enemyType.Value))
                        {
                            if (rng.NextProbability(25))
                            {
                                ChainRandomPlot<EnemyWakeUpPlot>();
                                wakeUp = true;
                            }
                        }

                        if (!wakeUp && rng.NextProbability(50))
                        {
                            ChainRandomPlot<StaticEnemyPlot>();
                            _lastPlotFlag = null;
                        }
                    }

                    while (_currentEnemyCount < _maximumEnemyCount)
                    {
                        ChainRandomPlot<EnemyWalksInPlot>();
                    }
                    */
                    _enemyRandomiser!.ChosenEnemies.Remove(_rdt);
                }

                // _logger.WriteLine($"  Enemy type: {enemyHelper!.GetEnemyName(_enemyType.Value)}");
                // _logger.WriteLine($"  {_currentEnemyCount} / {_maximumEnemyCount} enemies placed");
            }
            else
            {
                _logger.WriteLine($"  (no enemies defined)");
            }

            // cb.End();
            rdt.CustomAdditionalScript = cb.ToString();
        }

        private SbProcedure[] GetAllProcedures(SbNode node)
        {
            var procedures = new HashSet<SbProcedure>();

            var q = new Queue<SbNode>();
            q.Enqueue(node);

            while (q.Count != 0)
            {
                node = q.Dequeue();
                foreach (var child in node.Children)
                {
                    q.Enqueue(child);
                }

                if (node is SbProcedure sbProc)
                {
                    if (procedures.Add(sbProc))
                    {
                        q.Enqueue(sbProc);
                    }
                }
                else if (node is ISbSubProcedure sbSubProc)
                {
                    var proc = sbSubProc.Procedure;
                    if (procedures.Add(proc))
                    {
                        q.Enqueue(proc);
                    }
                }
            }

            return procedures.OrderBy(x => x.Name).ToArray();
        }

        private void LogPlot(CsPlot plot)
        {
            LogNode(plot.Root, 0);

            void LogNode(SbNode node, int level)
            {
                if (node is SbCommentNode commentNode)
                {
                    _logger.WriteLine(new string(' ', 2 + (level * 2)) + commentNode.Description);
                    level++;
                }
                if (node is ISbSubProcedure sbSubProc)
                {
                    LogNode(sbSubProc.Procedure, level);
                }
                foreach (var child in node.Children)
                {
                    LogNode(child, level);
                }
            }
        }

        private void ClearEnemies(RandomizedRdt rdt)
        {
            var room = _map.GetRoom(rdt.RdtId);
            if (room?.Enemies != null)
            {
                var enemySpecs = room.Enemies
                    .Where(IsEnemySpecValid)
                    .ToArray();

                foreach (var enemySpec in enemySpecs)
                {
                    if (enemySpec.Nop != null)
                    {
                        var nopArray = Map.ParseNopArray(enemySpec.Nop, rdt);
                        foreach (var offset in nopArray)
                        {
                            rdt.Nop(offset);
                            if (g_debugLogging)
                                _logger.WriteLine($"    {rdt.RdtId} (0x{offset:X2}) opcode removed");
                        }
                    }
                }
            }

            RemoveAllEnemiesFromRoom(rdt);
        }

        private bool RemoveAllEnemiesFromRoom(RandomizedRdt rdt)
        {
            var numEnemiesRemoved = 0;
            var enemySpec = GetEnemySpecs(rdt.RdtId);
            if (enemySpec == null)
                return false;

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
                .Where(e => _enemyRandomiser!.EnemyHelper.ShouldChangeEnemy(_config, e))
                .ToArray();

            foreach (var enemy in currentEnemies)
            {
                rdt.Nop(enemy.Offset);
                numEnemiesRemoved++;
            }
            if (numEnemiesRemoved != 0)
                _logger.WriteLine($"  {numEnemiesRemoved} enemies removed");
            return true;
        }

        private MapRoomEnemies GetEnemySpecs(RdtId rdtId)
        {
            var enemySpecs = _map.GetRoom(rdtId)?.Enemies;
            if (enemySpecs == null)
            {
                enemySpecs = new[] { new MapRoomEnemies() };
            }
            return enemySpecs.FirstOrDefault(IsEnemySpecValid);
        }

        private bool IsEnemySpecValid(MapRoomEnemies enemySpec)
        {
            if (enemySpec.Player != null && enemySpec.Player != _config.Player)
                return false;

            if (enemySpec.Scenario != null && enemySpec.Scenario != _config.Scenario)
                return false;

            if (enemySpec.RandomPlacements != null && enemySpec.RandomPlacements == false)
                return false;

            if (enemySpec.Restricted != null && enemySpec.Restricted != true)
                return false;

            if (enemySpec.DoorRando != null && enemySpec.DoorRando != _config.RandomDoors)
                return false;

            return true;
        }

        private void TidyPoi()
        {
            foreach (var poi in _poi)
            {
                var reverseEdges = _poi
                    .Where(x => x.Edges?.Contains(poi.Id) == true)
                    .Select(x => x.Id)
                    .ToArray();

                if (poi.Edges == null)
                {
                    poi.Edges = reverseEdges;
                }
                else
                {
                    poi.Edges = poi.Edges
                        .Concat(reverseEdges)
                        .Distinct()
                        .ToArray();
                }
            }
        }

        private void ChainRandomPlot<T>() where T : Plot
        {
            var plot = _registeredPlots.FirstOrDefault(x => x.GetType() == typeof(T));
            if (plot == null)
                throw new ArgumentException("Unknown plot");

            if (!plot.IsCompatible())
                return;

            plot.Create();
        }

        private CsPlot? ChainRandomPlot<T>(PlotBuilder builder) where T : Plot
        {
            var plot = _registeredPlots.FirstOrDefault(x => x.GetType() == typeof(T));
            if (plot == null)
                throw new ArgumentException("Unknown plot");

            if (!plot.IsCompatible())
                return null;

            if (plot is INewPlot newPlot)
                return newPlot.BuildPlot(builder);

            return null;
        }

        private void LoadCutsceneRoomInfo()
        {
            _cutsceneRoomInfoMap.Clear();

            var json = _dataManager.GetText(BioVersion.Biohazard2, "cutscene.json");
            var map = JsonSerializer.Deserialize<Dictionary<string, CutsceneRoomInfo>>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
            foreach (var kvp in map)
            {
                var key = RdtId.Parse(kvp.Key);
                _cutsceneRoomInfoMap[key] = kvp.Value;
            }
        }

        private void ReadEnemyPlacements()
        {
            var json = _dataManager.GetText(BioVersion.Biohazard2, "enemy.json");
            _allEnemyPositions = JsonSerializer.Deserialize<EnemyPosition[]>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
        }

        private void InitialisePlots()
        {
            _registeredPlots = new Plot[]
            {
                new StaticEnemyPlot(),
                new EnemyChangePlot(),
                new EnemyWakeUpPlot(),
                new EnemyFromDarkPlot(),
                new EnemyWalksInPlot(),
                new AllyWaitPlot(),
                new AllyWalksInPlot(),
                new AllyPassByPlot()
            };
            foreach (var plot in _registeredPlots)
            {
                plot.Cr = this;
            }
        }

        private ReFlag GetNextFlag() => throw new NotImplementedException();

        private int TakeEnemyCountForEvent(int min = 1, int max = int.MaxValue)
        {
            var max2 = Math.Min(max, Math.Max(min, _maximumEnemyCount - _currentEnemyCount));
            var count = _plotRng.Next(min, max2 + 1);
            _currentEnemyCount += count;
            return count;
        }

        public REPosition[] GetEnemyPlacements(int count)
        {
            return _enemyPositions.Next(count);
        }

        private readonly static byte[] _availableFlags4 = new byte[]
        {
            // Claire extra flags: 56, 71, 104, 157, 158, 176, 194
            4, 8, 11, 12, 13, 16, 17, 20, 21, 22, 25, 28, 29, 42, 44, 45, 66,
            81, 82, 123, 126, 127, 132, 134, 150, 166, 167, 180, 190, 195,
            196, 197, 198, 199, 200, 201, 202,
            203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215,
            216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228,
            229, 230, 231, 232, 233, 234, 235, 236, 237, 238, 239, 240, 241,
            242, 243, 244, 245, 246, 247, 248, 249, 250, 251, 252, 253, 255,
        };

        private readonly static byte[] _availableFlags3 = new byte[]
        {
            // Claire extra flags
            // 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13
            // 32, 33, 34, 35, 36, 37
            // 64, 65, 66, 67, 68
            // 99, 100, 129, 130, 131, 132, 133, 134, 192
            14, 15,
            16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
            29, 30, 31, 38, 39, 40, 41, 42, 43, 44, 45,
            46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
            59, 60, 61, 62, 63, 69, 70, 71, 72, 73, 74, 75, 76,
            77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89,
            90, 91, 92, 93, 94, 95, 96, 105, 108, 109,
            110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120,
            121, 122, 123, 124, 125, 126, 127, 135,
            136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146,
            147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157,
            158, 159, 161, 162, 163, 164, 165, 166, 167, 168, 169,
            170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180,
            181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191,
            194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204,
            205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215,
            216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226,
            227, 228, 229, 230, 231, 232, 233, 234, 235, 236, 237,
            238, 239, 240, 241, 242, 243, 244, 245, 246, 247, 248,
            249, 250, 251, 252, 253, 254, 255
        };
    }
}
