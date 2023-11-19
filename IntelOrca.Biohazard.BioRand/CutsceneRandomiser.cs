using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.Script.Opcodes;
using static IntelOrca.Biohazard.BioRand.EnemyRandomiser;

namespace IntelOrca.Biohazard.BioRand
{
    internal class CutsceneRandomiser
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
        private Queue<ushort> _flagQueue = new Queue<ushort>();

        // Current room
        private CutsceneBuilder _cb = new CutsceneBuilder();
        private RandomizedRdt? _rdt;
        private RdtId _rdtId;
        private int _plotId;
        private int _lastPlotId;
        private PointOfInterest[] _poi = new PointOfInterest[0];
        private int[] _allKnownCuts = new int[0];
        private EndlessBag<REPosition> _enemyPositions;
        private Rng _plotRng;

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
            _itemRandomiser = itemRandomiser;
            _enemyRandomiser = enemyRandomiser;
            _npcRandomiser = npcRandomiser;
            _voiceRandomiser = voiceRandomiser;
            _enemyPositions = new EndlessBag<REPosition>(new Rng(), new REPosition[0]);

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

            ClearEnemies(rdt);

            var doors = info.Poi?.Where(x => x.HasTag(PoiKind.Door)).ToArray() ?? new PointOfInterest[0];
            var triggers = info.Poi?.Where(x => x.HasTag(PoiKind.Trigger)).ToArray() ?? new PointOfInterest[0];
            var meets = info.Poi?.Where(x => x.HasTag(PoiKind.Meet)).ToArray() ?? new PointOfInterest[0];

            var cb = new CutsceneBuilder();
            cb.Begin();

            _cb = cb;
            _rdt = rdt;
            _rdtId = rdt.RdtId;
            _plotRng = rng;
            _lastPlotId = -1;
            _poi = info.Poi ?? new PointOfInterest[0];
            _allKnownCuts = _poi.SelectMany(x => x.AllCuts).ToArray();
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

            var aotIds = Enumerable.Range(0, 32).ToHashSet();
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

            // ChainRandomPlot<EnemyChangePlot>();
            // ChainRandomPlot<EnemyWakeUpPlot>();
            // ChainRandomPlot<EnemyWalksInPlot>();
            // ChainRandomPlot<AllyWalksInPlot>();

            // for (var i = 0; i < 4; i++)
            // {
            //     ChainRandomPlot<AllyStaticPlot>();
            // }

            if (rng.NextProbability(50))
            {
                ChainRandomPlot<AllyStaticPlot>();
                _lastPlotId = -1;
            }
            if (_enemyRandomiser?.ChosenEnemies.TryGetValue(_rdt, out var enemy) == true)
            {
                if (!enemy.Types.Contains(Re2EnemyIds.ZombieArms) &&
                    !enemy.Types.Contains(Re2EnemyIds.GAdult))
                {
                    if (rng.NextProbability(10))
                    {
                        ChainRandomPlot<EnemyFromDarkPlot>();
                    }
                    else
                    {
                        var wakeUp = false;
                        if (enemy.Types.Any(x => x <= Re2EnemyIds.ZombieRandom))
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
                            _lastPlotId = -1;
                        }
                    }
                    ChainRandomPlot<EnemyWalksInPlot>();
                    for (var i = 0; i < 3; i++)
                    {
                        if (rng.NextProbability(50))
                        {
                            ChainRandomPlot<EnemyWalksInPlot>();
                        }
                    }
                    _enemyRandomiser.ChosenEnemies.Remove(_rdt);
                }
            }
            else
            {
                _logger.WriteLine($"  (no enemies defined)");
            }

            cb.End();
            rdt.CustomAdditionalScript = cb.ToString();
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
                    .Where(e => _enemyRandomiser!.EnemyHelper.ShouldChangeEnemy(_config, e))
                    .ToArray();

                foreach (var enemy in currentEnemies)
                {
                    rdt.Nop(enemy.Offset);
                    numEnemiesRemoved++;
                }
            }
            if (numEnemiesRemoved != 0)
                _logger.WriteLine($"  {numEnemiesRemoved} enemies removed");
            return true;
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
                poi.Edges ??= _poi
                    .Where(x => x.Edges?.Contains(poi.Id) == true)
                    .Select(x => x.Id)
                    .ToArray();
            }
        }

        private void ChainRandomPlot()
        {
            foreach (var plot in _registeredPlots)
            {
                ChainRandomPlot(plot);
            }
        }

        private void ChainRandomPlot<T>() where T : Plot
        {
            var plot = _registeredPlots.FirstOrDefault(x => x.GetType() == typeof(T));
            if (plot == null)
                throw new ArgumentException("Unknown plot");

            ChainRandomPlot(plot);
        }

        private void ChainRandomPlot(Plot plot)
        {
            if (!plot.IsCompatible())
                return;

            plot.Create();
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
                new AllyStaticPlot(),
                new AllyWalksInPlot()
            };
            foreach (var plot in _registeredPlots)
            {
                plot.Cr = this;
            }
        }

        private ushort GetNextFlag()
        {
            if (_flagQueue.Count == 0)
            {
                foreach (var value in _availableFlags4)
                {
                    _flagQueue.Enqueue((ushort)(value | (CutsceneBuilder.FG_COMMON << 8)));
                }
                foreach (var value in _availableFlags3)
                {
                    _flagQueue.Enqueue((ushort)(value | (CutsceneBuilder.FG_SCENARIO << 8)));
                }
            }
            return _flagQueue.Dequeue();
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

        private abstract class Plot
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public CutsceneRandomiser Cr { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public CutsceneBuilder Builder => Cr._cb;
            public Rng Rng => Cr._plotRng;
            public RandoLogger Logger => Cr._logger;

            public bool IsCompatible() => Check();

            public void Create()
            {
                Cr._plotId = Builder.BeginPlot(Cr.GetNextFlag());

                Logger.WriteLine($"    [plot] #{Cr._plotId - 0x0300}: {GetType().Name}");
                Build();
                Builder.EndPlot();
                Cr._lastPlotId = Cr._plotId;
            }

            protected virtual bool Check()
            {
                return true;
            }

            protected virtual void Build()
            {
            }

            protected byte GetEnemyTypeForRoom()
            {
                var enemyRandomiser = Cr._enemyRandomiser!;
                var type = Re2EnemyIds.ZombieRandom;
                if (enemyRandomiser!.ChosenEnemies.TryGetValue(Cr._rdt!, out var selectedEnemy))
                {
                    type = selectedEnemy.Types[0];
                }
                return type;
            }

            protected SceEmSetOpcode GenerateEnemy(int id, REPosition position)
            {
                var enemyRandomiser = Cr._enemyRandomiser!;
                var enemyHelper = enemyRandomiser.EnemyHelper;

                var type = GetEnemyTypeForRoom();

                var opcode = new SceEmSetOpcode();
                opcode.Id = (byte)id;
                opcode.X = (short)position.X;
                opcode.Y = (short)position.Y;
                opcode.Z = (short)position.Z;
                opcode.D = (short)position.D;
                opcode.Floor = (byte)position.Floor;
                opcode.KillId = enemyRandomiser.GetNextKillId();
                opcode.Type = type;

                var enemySpec = new MapRoomEnemies();
                enemyHelper.SetEnemy(Cr._config, Rng, opcode, enemySpec, type);
                return opcode;
            }

            protected PointOfInterest? AddTriggers(
                int[]? notCuts = null,
                int minSleepTime = 0,
                int maxSleepTime = 20)
            {
                if (Cr._lastPlotId != -1)
                {
                    LogTrigger($"after plot {Cr._lastPlotId - 0x0300}");
                    Builder.WaitForPlot(Cr._lastPlotId);
                }

                var sleepTime = minSleepTime;
                var justSleep = false;
                if (notCuts == null || notCuts.Length == 0)
                {
                    if (Rng.NextProbability(50))
                    {
                        // Just a sleep trigger
                        sleepTime = Rng.Next(minSleepTime, maxSleepTime);
                        justSleep = true;
                    }
                }

                // Sleep trigger
                if (sleepTime != 0)
                {
                    Builder.Sleep(30 * sleepTime);
                    LogTrigger($"wait {sleepTime} seconds");
                }

                Builder.WaitForPlotUnlock();

                if (justSleep)
                    return null;

                // Random cut
                var triggerPoi = GetRandomPoi(x => x.HasTag(PoiKind.Trigger) && notCuts?.Contains(x.Cut) != true);
                if (triggerPoi == null)
                    throw new Exception("Unable to find cut trigger");

                LogTrigger($"cut {triggerPoi.Cut}");
                Builder.WaitForTriggerCut(triggerPoi.Cut);
                return triggerPoi;
            }

            protected void DoDoorOpenCloseCutAway(PointOfInterest door, int currentCut)
            {
                var cuts = door.AllCuts;
                var needsCut = cuts.Contains(currentCut) == true;
                if (needsCut)
                {
                    var cut = Cr._allKnownCuts.Except(cuts).Shuffle(Rng).FirstOrDefault();
                    Builder.CutChange(cut);
                    LogAction($"door away cut {cut}");
                }
                else
                {
                    LogAction($"door");
                }
                DoDoorOpenClose(door);
                Builder.CutRevert();
            }

            protected void DoDoorOpenClose(PointOfInterest door)
            {
                var pos = door.Position;
                if (door.HasTag("door"))
                {
                    Builder.PlayDoorSoundOpen(pos);
                    Builder.Sleep(30);
                    Builder.PlayDoorSoundClose(pos);
                }
                else
                {
                    Builder.Sleep(30);
                }
            }

            protected void DoDoorOpenCloseCut(PointOfInterest door)
            {
                DoDoorOpenClose(door);
                Builder.CutChange(door.Cut);
                LogAction($"door cut {door.Cut}");
            }

            protected void IntelliTravelTo(int flag, int enemyId, PointOfInterest from, PointOfInterest destination, REPosition? overrideDestination, PlcDestKind kind, bool cutFollow = false)
            {
                Builder.SetFlag(CutsceneBuilder.FG_ROOM, flag, false);
                Builder.BeginSubProcedure();
                var route = GetTravelRoute(from, destination);
                foreach (var poi in route)
                {
                    if (overrideDestination != null && poi == destination)
                        break;

                    Builder.SetEnemyDestination(enemyId, poi.Position, kind);
                    Builder.WaitForEnemyTravel(enemyId);
                    Builder.Sleep(2);
                    if (cutFollow)
                        Builder.CutChange(poi.Cut);
                    LogAction($"{GetCharLogName(enemyId)} travel to {{ {poi} }}");
                }
                if (overrideDestination != null)
                {
                    Builder.SetEnemyDestination(enemyId, overrideDestination.Value, kind);
                    Builder.WaitForEnemyTravel(enemyId);
                    Builder.MoveEnemy(enemyId, overrideDestination.Value);
                    LogAction($"{GetCharLogName(enemyId)} travel to {overrideDestination}");
                }
                else
                {
                    Builder.MoveEnemy(enemyId, destination.Position);
                }
                Builder.SetFlag(CutsceneBuilder.FG_ROOM, flag);
                var subName = Builder.EndSubProcedure();
                Builder.CallThread(subName);
            }

            private PointOfInterest[] GetTravelRoute(PointOfInterest from, PointOfInterest destination)
            {
                var prev = new Dictionary<PointOfInterest, PointOfInterest>();
                var q = new Queue<PointOfInterest>();
                q.Enqueue(from);

                var found = false;
                while (!found && q.Count != 0)
                {
                    var curr = q.Dequeue();
                    var edges = GetEdges(curr);
                    foreach (var edge in edges)
                    {
                        if (!prev.ContainsKey(edge))
                        {
                            prev[edge] = curr;
                            if (edge == destination)
                            {
                                found = true;
                                break;
                            }
                            q.Enqueue(edge);
                        }
                    }
                }

                if (!found)
                {
                    // throw new Exception("Failed to find POI route from source to destination.");
                    return new[] { destination };
                }

                var route = new List<PointOfInterest>();
                var poi = destination;
                while (poi != from)
                {
                    route.Add(poi);
                    poi = prev[poi];
                }
                return ((IEnumerable<PointOfInterest>)route).Reverse().ToArray();
            }

            private PointOfInterest[] GetEdges(PointOfInterest poi)
            {
                var edges = poi.Edges;
                if (edges == null)
                    return new PointOfInterest[0];

                return edges
                    .Select(x => FindPoi(x))
                    .Where(x => x != null)
                    .Select(x => x!)
                    .ToArray();
            }

            protected void LongConversation(int[] vids)
            {
                BeginConversation();
                Converse(vids);
                EndConversation();
            }

            protected void BeginConversation()
            {
                Builder.FadeOutMusic();
                Builder.Sleep(60);
            }

            protected void Converse(int[] vids)
            {
                for (int i = 0; i < vids.Length; i++)
                {
                    Builder.PlayVoice(vids[i]);
                    Builder.Sleep(15);
                }
            }

            protected void EndConversation()
            {
                Builder.Sleep(60);
                Builder.ResumeMusic();
                LogAction($"conversation");
            }

            protected PointOfInterest? GetRandomDoor()
            {
                return GetRandomPoi(x => x.HasTag(PoiKind.Door) || x.HasTag(PoiKind.Stairs));
            }

            protected PointOfInterest? GetRandomPoi(Predicate<PointOfInterest> predicate)
            {
                return Cr._poi
                    .Where(x => predicate(x))
                    .Shuffle(Rng)
                    .FirstOrDefault();
            }

            private PointOfInterest? FindPoi(int id)
            {
                return Cr._poi.FirstOrDefault(x => x.Id == id);
            }

            private string GetCharLogName(int enemyId)
            {
                return enemyId == -1 ? "player" : $"npc {enemyId}";
            }

            protected void LogTrigger(string s) => Logger.WriteLine($"      [trigger] {s}");
            protected void LogAction(string s) => Logger.WriteLine($"      [action] {s}");
        }

        private class StaticEnemyPlot : Plot
        {
            protected override void Build()
            {
                var numPlacements = Rng.Next(2, 6);
                var placements = Cr._enemyPositions
                    .Next(numPlacements);
                var enemyIds = Builder.AllocateEnemies(placements.Length);
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], Cr._enemyPositions.Next());
                    Builder.Enemy(opcode);
                }
                LogAction($"{enemyIds.Length}x enemy");
            }
        }

        private class EnemyChangePlot : Plot
        {
            protected override void Build()
            {
                var numPlacements = Rng.Next(6, 12);
                var placements = Cr._enemyPositions
                    .Next(numPlacements);
                var enemyIds = Builder.AllocateEnemies(placements.Length);

                Builder.IfPlotTriggered();
                LogTrigger("re-enter room");
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], Cr._enemyPositions.Next());
                    Builder.Enemy(opcode);
                }
                LogAction($"{enemyIds.Length}x enemy");
            }
        }

        private class EnemyWakeUpPlot : Plot
        {
            protected override void Build()
            {
                var numPlacements = Rng.Next(6, 12);
                var placements = Cr._enemyPositions
                    .Next(numPlacements);
                var enemyIds = Builder.AllocateEnemies(placements.Length);

                Builder.IfPlotTriggered();
                // Setup enemies in woken up positions
                for (int i = 0; i < placements.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], placements[i]);
                    Builder.Enemy(opcode);
                }

                Builder.Else();

                // Setup initial enemy positions
                for (int i = 0; i < placements.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], placements[i]);
                    opcode.State = 4;
                    opcode.Ai = 128;
                    Builder.Enemy(opcode);
                }

                // Wait for triggers
                Builder.BeginTriggerThread();
                AddTriggers();

                // Wake up enemies incrementally
                Builder.LockPlot();
                foreach (var eid in enemyIds)
                {
                    Builder.Sleep(Rng.Next(5, 15));
                    Builder.ActivateEnemy(eid);
                }
                Builder.UnlockPlot();
                LogAction($"{enemyIds.Length}x enemy wake up");
            }
        }

        private class EnemyFromDarkPlot : Plot
        {
            protected override void Build()
            {
                var enemyRandomiser = Cr._enemyRandomiser!;
                var enemyHelper = enemyRandomiser.EnemyHelper;
                var enemyType = GetEnemyTypeForRoom();
                var limit = enemyHelper.GetEnemyTypeLimit(Cr._config, Cr._config.EnemyDifficulty, enemyType);

                var maxUsualPlacements = Math.Min(Cr._enemyPositions.Count, limit);
                var numPlacements = Rng.Next(maxUsualPlacements - 2, maxUsualPlacements + 2);
                var placements = Cr._enemyPositions
                    .Next(numPlacements);
                var enemyIds = Builder.AllocateEnemies(placements.Length);

                Builder.IfPlotTriggered();
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], Cr._enemyPositions.Next());
                    Builder.Enemy(opcode);
                }

                Builder.Else();
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], REPosition.OutOfBounds);
                    opcode.Ai = 128;
                    Builder.Enemy(opcode);
                }

                Builder.BeginTriggerThread();
                AddTriggers(minSleepTime: 5);
                Builder.LockPlot();

                Builder.LockControls();
                Builder.SetFade(0, 2, 7, 0, 0);
                for (var i = 0; i < 5; i++)
                {
                    Builder.AdjustFade(0, 0, ((i & 1) == 0) ? 0 : 127);
                    Builder.Sleep1();
                }
                Builder.AdjustFade(0, 0, 127);
                Builder.Sleep(30);
                foreach (var eid in enemyIds)
                {
                    Builder.MoveEnemy(eid, Cr._enemyPositions.Next());
                    Builder.ActivateEnemy(eid);
                }
                Builder.AdjustFade(0, 0, 0);
                Builder.Sleep1();
                Builder.SetFade(0, 2, 7, 255, 127);
                Builder.Sleep1();
                Builder.UnlockControls();

                Builder.UnlockPlot();
                LogAction($"{enemyIds.Length}x enemy spawn in from dark");
            }
        }

        private class EnemyWalksInPlot : Plot
        {
            protected override void Build()
            {
                var door = GetRandomDoor()!;
                var enemyIds = Builder.AllocateEnemies(Rng.Next(1, GetMaxEnemiesToWalkIn() + 1));

                Builder.IfPlotTriggered();
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    int eid = enemyIds[i];
                    var pos = door.Position;
                    if (Cr._enemyPositions.Count != 0)
                    {
                        pos = Cr._enemyPositions.Next();
                    }
                    var opcode = GenerateEnemy(eid, pos);
                    if (opcode.Type <= Re2EnemyIds.ZombieRandom)
                        opcode.State = 6;
                    Builder.Enemy(opcode);
                }

                Builder.Else();
                foreach (var eid in enemyIds)
                {
                    var opcode = GenerateEnemy(eid, REPosition.OutOfBounds.WithY(door.Position.Y));
                    if (opcode.Type <= Re2EnemyIds.ZombieRandom)
                        opcode.State = 6;
                    opcode.Ai = 128;
                    Builder.Enemy(opcode);
                }

                Builder.BeginTriggerThread();
                AddTriggers(door.Cuts);

                // Move enemies into position and cut to them
                Builder.LockPlot();
                DoDoorOpenCloseCut(door);
                Builder.BeginCutsceneMode();

                var previousEnemies = Builder.PlacedEnemyIds.ToArray();
                LockEnemies(previousEnemies);

                foreach (var eid in enemyIds)
                {
                    var pos = door.Position + new REPosition(
                        Rng.Next(-50, 50),
                        0,
                        Rng.Next(-50, 50));
                    Builder.MoveEnemy(eid, pos);
                    Builder.ActivateEnemy(eid);
                }
                LogAction($"{enemyIds.Length}x enemy walk in");
                Builder.Sleep(60);
                UnlockEnemies(previousEnemies);
                Builder.CutRevert();
                Builder.EndCutsceneMode();
                Builder.SetFlag(Cr._plotId >> 8, Cr._plotId & 0xFF);

                // Delay next plot for at least 4s
                Builder.Sleep(30 * 4);

                Builder.UnlockPlot();
            }

            private void LockEnemies(int[] enemies)
            {
                foreach (var eid in enemies)
                {
                    Builder.DeactivateEnemy(eid);
                    // Builder.HideEnemy(eid);
                }
            }

            private void UnlockEnemies(int[] enemies)
            {
                foreach (var eid in enemies)
                {
                    // Builder.UnhideEnemy(eid);
                    Builder.ActivateEnemy(eid);
                }
            }

            private int GetMaxEnemiesToWalkIn()
            {
                var type = Re2EnemyIds.ZombieRandom;
                if (Cr._enemyRandomiser!.ChosenEnemies.TryGetValue(Cr._rdt!, out var selectedEnemy))
                {
                    type = selectedEnemy.Types.FirstOrDefault();
                }
                switch (type)
                {
                    case Re2EnemyIds.ZombieDog:
                        return 4;
                    case Re2EnemyIds.Crow:
                        return 6;
                    case Re2EnemyIds.LickerRed:
                    case Re2EnemyIds.LickerGrey:
                        return 2;
                    case Re2EnemyIds.Spider:
                        return 3;
                    case Re2EnemyIds.GEmbryo:
                        return 8;
                    case Re2EnemyIds.Tyrant1:
                        return 4;
                    case Re2EnemyIds.Ivy:
                    case Re2EnemyIds.IvyPurple:
                        return 2;
                    case Re2EnemyIds.Birkin1:
                        return 1;
                    case Re2EnemyIds.GiantMoth:
                        return 1;
                    default:
                        return 4;
                }
            }
        }

        private class AllyStaticPlot : Plot
        {
            protected override bool Check()
            {
                return GetRandomPoi(x => x.HasTag(PoiKind.Meet) || x.HasTag(PoiKind.Npc)) != null;
            }

            protected override void Build()
            {
                var npcId = Builder.AllocateEnemies(1).FirstOrDefault();
                var meetup = GetRandomPoi(x => x.HasTag(PoiKind.Meet) || x.HasTag(PoiKind.Npc))!;

                var enemyType = Re2EnemyIds.ClaireRedfield;
                var actor0 = "leon";
                var actor1 = "claire";
                var npcRando = Cr._npcRandomiser;
                if (npcRando != null)
                {
                    enemyType = npcRando.GetRandomNpc(Cr._rdt!, Rng);
                    actor0 = npcRando.PlayerActor!;
                    actor1 = npcRando.GetActor(enemyType)!;
                }

                var voiceRando = Cr._voiceRandomiser;
                var vIds0 = new int[0];
                var vIds1 = new int[0];
                if (voiceRando != null)
                {
                    var actors = new[] { actor0, actor1 };
                    vIds0 = voiceRando.AllocateConversation(Rng, Cr._rdtId, 1, actors.Skip(1).ToArray(), actors);
                    vIds1 = voiceRando.AllocateConversation(Rng, Cr._rdtId, Rng.Next(2, 6), actors, actors);
                }

                var interactId = Builder.AllocateAots(1)[0];
                int? itemId = null;
                if (Rng.NextProbability(75))
                {
                    itemId = Builder.AllocateAots(1).FirstOrDefault();
                }


                Builder.BeginSubProcedure();
                Builder.BeginCutsceneMode();
                Builder.BeginIf();
                Builder.CheckFlag(Cr._plotId >> 8, Cr._plotId & 0xFF);
                Converse(vIds0);
                Builder.Else();
                if (meetup.CloseCut != null)
                    Builder.CutChange(meetup.CloseCut.Value);
                BeginConversation();
                Converse(vIds1.Take(vIds1.Length - 1).ToArray());
                if (itemId != null)
                {
                    Builder.AotOn(itemId.Value);
                }
                Converse(vIds1.Skip(vIds1.Length - 1).ToArray());
                EndConversation();
                if (meetup.CloseCut != null)
                    Builder.CutRevert();
                Builder.SetFlag(Cr._plotId >> 8, Cr._plotId & 0xFF);
                Builder.EndIf();
                Builder.EndCutsceneMode();
                var eventProc = Builder.EndSubProcedure();

                Builder.Ally(npcId, enemyType, meetup.Position);
                Builder.Event(interactId, meetup.Position, 2000, eventProc);
                if (itemId != null)
                {
                    RandomItem(255, itemId.Value);
                    Builder.SetFlag(CutsceneBuilder.FG_ITEM, 255, false);
                }
            }

            private void RandomItem(int globalId, int aotId)
            {
                var type = Re2ItemIds.FAidSpray;
                var amount = 1;

                var config = Cr._config;
                var itemHelper = new Re2ItemHelper();
                var itemRando = Cr._itemRandomiser;
                if (itemRando == null)
                {
                    var kind = Rng.NextOf(ItemAttribute.Ammo, ItemAttribute.Heal, ItemAttribute.InkRibbon);
                    if (kind == ItemAttribute.Ammo)
                    {
                        if (config.Player == 0)
                        {
                            type = Rng.NextOf(
                                Re2ItemIds.HandgunAmmo,
                                Re2ItemIds.ShotgunAmmo,
                                Re2ItemIds.MagnumAmmo);
                        }
                        else
                        {
                            type = Rng.NextOf(
                                Re2ItemIds.HandgunAmmo,
                                Re2ItemIds.BowgunAmmo,
                                Re2ItemIds.GrenadeLauncherAcid,
                                Re2ItemIds.GrenadeLauncherExplosive,
                                Re2ItemIds.GrenadeLauncherFlame);
                        }
                        var capacity = itemHelper.GetMaxAmmoForAmmoType(type);
                        amount = Rng.Next(capacity / 2, capacity);
                    }
                    else if (kind == ItemAttribute.Heal)
                    {
                        type = Rng.NextOf(
                            Re2ItemIds.HerbGRB,
                            Re2ItemIds.FAidSpray);
                    }
                    else
                    {
                        type = Re2ItemIds.InkRibbon;
                        amount = Rng.Next(3, 6);
                    }
                }
                else
                {
                    var item = itemRando.GetRandomGift(Rng);
                    type = item.Type;
                    amount = item.Amount;
                }

                Builder.Item(globalId, aotId, type, (byte)amount);
                LogAction($"item gift [{itemHelper.GetItemName(type)} x{amount}]");
            }
        }

        private class AllyWalksInPlot : Plot
        {
            protected override bool Check()
            {
                return GetRandomPoi(x => x.HasTag(PoiKind.Meet)) != null;
            }

            protected override void Build()
            {
                var numAllys = Rng.Next(0, 3);
                numAllys = 3;
                var allyIds = Builder.AllocateEnemies(numAllys);
                var entranceDoors = Enumerable.Range(0, numAllys).Select(x => GetRandomDoor()!).ToArray();
                var exitDoors = Enumerable.Range(0, numAllys).Select(x => GetRandomDoor()!).ToArray();

                var meetup = GetRandomPoi(x => x.HasTag(PoiKind.Meet))!;
                var meetA = new REPosition(meetup.X + 1000, meetup.Y, meetup.Z, 2000);
                var meetB = new REPosition(meetup.X - 1000, meetup.Y, meetup.Z, 0);
                var meetC = new REPosition(meetup.X, meetup.Y, meetup.Z + 1000, 1000);
                var meetD = new REPosition(meetup.X, meetup.Y, meetup.Z - 1000, 3000);
                var allyMeets = new[] { meetB, meetC, meetD };

                Builder.IfPlotTriggered();
                Builder.ElseBeginTriggerThread();
                for (var i = 0; i < numAllys; i++)
                {
                    var enemyType = Cr._npcRandomiser!.GetRandomNpc(Cr._rdt!, Rng);
                    Builder.Ally(allyIds[i], enemyType, REPosition.OutOfBounds.WithY(entranceDoors[i].Position.Y));
                }

                var triggerCut = AddTriggers(entranceDoors[0].Cuts);
                if (triggerCut == null)
                    throw new Exception("Cutscene not supported for non-cut triggers.");

                Builder.LockPlot();

                // Mark the cutscene as done in case it softlocks
                Builder.SetFlag(Cr._plotId >> 8, Cr._plotId & 0xFF);

                DoDoorOpenCloseCut(entranceDoors[0]);
                Builder.BeginCutsceneMode();
                Builder.MoveEnemy(allyIds[0], entranceDoors[0].Position);
                if (Rng.NextProbability(50))
                {
                    Builder.PlayVoice(21);
                }
                Builder.Sleep(30);
                LogAction($"Ally walk in");

                Builder.CutRevert();

                IntelliTravelTo(24, -1, triggerCut, meetup, meetA, PlcDestKind.Run, cutFollow: true);
                IntelliTravelTo(25, allyIds[0], entranceDoors[0], meetup, allyMeets[0], PlcDestKind.Run);
                Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);
                Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 25);

                LogAction($"Focus on {{ {meetup} }}");
                var meetCut = meetup.CloseCut ?? meetup.Cut;
                Builder.CutChange(meetCut);
                LongConversation(new[] { 1, 2, 3, 4, 5 });

                for (var i = 1; i < numAllys; i++)
                {
                    // Another ally walks in
                    Builder.MoveEnemy(allyIds[i], entranceDoors[i].Position);
                    DoDoorOpenCloseCutAway(entranceDoors[i], meetCut);
                    Builder.Sleep(15);
                    LogAction($"NPC walk in");
                    IntelliTravelTo(24, allyIds[i], entranceDoors[i], meetup, allyMeets[i], PlcDestKind.Run, cutFollow: true);
                    Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);

                    LogAction($"Focus on {meetup}");
                    Builder.CutChange(meetCut);
                    LongConversation(new[] { 2, 4, 6, 7, 8 });
                }

                if (Rng.NextProbability(50))
                {
                    // Backstep
                    Builder.PlayVoiceAsync(Rng.Next(0, 15));
                    var backstepPos = new REPosition(meetup.X - 2500, meetup.Y, meetup.Z, 0);
                    Builder.SetEnemyDestination(allyIds[0], backstepPos, PlcDestKind.Backstep);
                    Builder.WaitForEnemyTravel(allyIds[0]);
                    Builder.Sleep(2);
                }

                IntelliTravelTo(24, allyIds[0], meetup, exitDoors[0], exitDoors[0].Position.Reverse(), PlcDestKind.Run);
                Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);

                Builder.MoveEnemy(allyIds[0], REPosition.OutOfBounds);
                Builder.StopEnemy(allyIds[0]);

                DoDoorOpenCloseCutAway(exitDoors[0], meetup.Cut);
                Builder.ReleaseEnemyControl(-1);
                Builder.CutAuto();
                Builder.EndCutsceneMode();
                Builder.UnlockPlot();
            }
        }
    }

    public struct REPosition
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int D { get; }

        public int Floor => Y / -1800;

        public REPosition(int x, int y, int z) : this(x, y, z, 0) { }
        public REPosition(int x, int y, int z, int d)
        {
            X = x;
            Y = y;
            Z = z;
            D = d;
        }

        public REPosition WithY(int y) => new REPosition(X, y, Z, D);

        public REPosition Reverse()
        {
            return new REPosition(X, Y, Z, (D + 2000) % 4000);
        }

        public static REPosition OutOfBounds { get; } = new REPosition(-32000, -10000, -32000);

        public static REPosition operator +(REPosition a, REPosition b)
            => new REPosition(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.D + b.D);

        public override string ToString() => $"({X},{Y},{Z},{D})";
    }

    public class CutsceneRoomInfo
    {
        public PointOfInterest[]? Poi { get; set; }
    }

    public class PointOfInterest
    {
        public int Id { get; set; }
        public string[]? Tags { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public int Cut { get; set; }
        public int? CloseCut { get; set; }
        public int[]? Cuts { get; set; }
        public int[]? Edges { get; set; }

        public REPosition Position => new REPosition(X, Y, Z, D);

        public int[] AllCuts
        {
            get
            {
                var cuts = new List<int> { Cut };
                if (Cuts != null)
                    cuts.AddRange(Cuts);
                if (CloseCut != null)
                    cuts.Add(CloseCut.Value);
                return cuts.ToArray();
            }
        }

        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        public override string ToString() => $"Id = {Id} Tags = [{string.Join(", ", Tags)}] Cut = {Cut} Position = {Position}";
    }

    public static class PoiKind
    {
        public const string Trigger = "trigger";
        public const string Door = "door";
        public const string Stairs = "stairs";
        public const string Waypoint = "waypoint";
        public const string Meet = "meet";
        public const string Npc = "npc";
    }
}
