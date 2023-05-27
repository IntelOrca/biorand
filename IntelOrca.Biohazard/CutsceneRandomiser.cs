using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace IntelOrca.Biohazard
{
    internal class CutsceneRandomiser
    {
        private readonly RandoLogger _logger;
        private readonly DataManager _dataManager;
        private readonly RandoConfig _config;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly IEnemyHelper _enemyHelper;
        private readonly INpcHelper _npcHelper;
        private readonly Dictionary<RdtId, CutsceneRoomInfo> _cutsceneRoomInfoMap = new Dictionary<RdtId, CutsceneRoomInfo>();
        private EnemyPosition[] _allEnemyPositions = new EnemyPosition[0];
        private Plot[] _registeredPlots = new Plot[0];

        // Current room
        private CutsceneBuilder _cb = new CutsceneBuilder();
        private RdtId _rdtId;
        private int _plotId;
        private int _lastPlotId;
        private PointOfInterest[] _poi = new PointOfInterest[0];
        private int[] _allKnownCuts = new int[0];
        private REPosition[] _enemyPositions = new REPosition[0];

        public CutsceneRandomiser(RandoLogger logger, DataManager dataManager, RandoConfig config, GameData gameData, Map map, Rng rng, IEnemyHelper enemyHelper, INpcHelper npcHelper)
        {
            _logger = logger;
            _dataManager = dataManager;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = rng;
            _enemyHelper = enemyHelper;
            _npcHelper = npcHelper;

            LoadCutsceneRoomInfo();
            ReadEnemyPlacements();
            InitialisePlots();
        }

        public void Randomise(PlayGraph? graph)
        {
            _logger.WriteHeading("Randomizing cutscenes");

            var rdts = graph?.GetAccessibleRdts(_gameData) ?? _gameData.Rdts;
            foreach (var rdt in rdts)
            {
                RandomizeRoom(rdt);
            }
        }

        public void RandomizeRoom(Rdt rdt)
        {
            if (!_cutsceneRoomInfoMap.TryGetValue(rdt.RdtId, out var info))
                return;

            _logger.WriteLine($"  {rdt}:");

            ClearEnemies(rdt);

            _enemyPositions = _allEnemyPositions
                .Where(x => x.RdtId == rdt.RdtId)
                .Select(p => new REPosition(p.X, p.Y, p.Z, p.D))
                .Shuffle(_rng)
                .ToArray();

            var doors = info.Poi?.Where(x => x.Kind == PoiKind.Door).ToArray() ?? new PointOfInterest[0];
            var triggers = info.Poi?.Where(x => x.Kind == PoiKind.Trigger).ToArray() ?? new PointOfInterest[0];
            var meets = info.Poi?.Where(x => x.Kind == PoiKind.Meet).ToArray() ?? new PointOfInterest[0];

            var cb = new CutsceneBuilder();
            cb.Begin();

            _cb = cb;
            _rdtId = rdt.RdtId;
            _lastPlotId = -1;
            _poi = info.Poi ?? new PointOfInterest[0];
            _allKnownCuts = _poi.SelectMany(x => x.AllCuts).ToArray();
            TidyPoi();

            // ChainRandomPlot<EnemyChangePlot>();

            // ChainRandomPlot<AllyStaticPlot>();
            // ChainRandomPlot<EnemyWakeUpPlot>();
            // ChainRandomPlot<EnemyWalksInPlot>();

            ChainRandomPlot<AllyWalksInPlot>();
            ChainRandomPlot<EnemyWalksInPlot>();
            ChainRandomPlot<EnemyWalksInPlot>();

            cb.End();
            rdt.CustomAdditionalScript = cb.ToString();
        }

        private void ClearEnemies(Rdt rdt)
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
                            _logger.WriteLine($"    {rdt.RdtId} (0x{offset:X2}) opcode removed");
                        }
                    }
                }
            }
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
                new EnemyChangePlot(),
                new EnemyWakeUpPlot(),
                new EnemyWalksInPlot(),
                new AllyStaticPlot(),
                new AllyWalksInPlot()
            };
            foreach (var plot in _registeredPlots)
            {
                plot.Cr = this;
            }
        }

        private abstract class Plot
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public CutsceneRandomiser Cr { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public CutsceneBuilder Builder => Cr._cb;
            public Rng Rng => Cr._rng;
            public RandoLogger Logger => Cr._logger;

            public bool IsCompatible() => Check();

            public void Create()
            {
                Cr._plotId = Builder.BeginPlot();

                Logger.WriteLine($"    [plot] #{Cr._plotId}: {GetType().Name}");
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

            protected PointOfInterest? AddTriggers(int[]? notCuts)
            {
                if (Cr._lastPlotId != -1)
                {
                    LogTrigger($"after plot {Cr._lastPlotId}");
                    Builder.WaitForPlot(Cr._lastPlotId);
                }

                var sleepTime = Rng.Next(0, 10);
                var justSleep = false;
                if (notCuts == null || notCuts.Length == 0)
                {
                    if (Rng.NextProbability(50))
                    {
                        // Just a sleep trigger
                        sleepTime = Rng.Next(5, 30);
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
                var triggerPoi = GetRandomPoi(x => x.Kind == PoiKind.Trigger && !notCuts.Contains(x.Cut));
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
                if (door.Kind == "door")
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
                Builder.SetFlag(2, 7, true);
                DoDoorOpenClose(door);
                Builder.CutChange(door.Cut);
                LogAction($"door cut {door.Cut}");
            }

            protected void IntelliTravelTo(int flag, int enemyId, PointOfInterest from, PointOfInterest destination, REPosition? overrideDestination, bool run)
            {
                Builder.SetFlag(4, flag, false);
                Builder.BeginSubProcedure();
                var route = GetTravelRoute(from, destination);
                foreach (var poi in route)
                {
                    if (overrideDestination != null && poi == destination)
                        break;

                    Builder.SetEnemyDestination(enemyId, poi.Position, run);
                    Builder.WaitForEnemyTravel(enemyId);
                    Builder.Sleep(2);
                    if (enemyId == -1)
                        Builder.CutChange(poi.Cut);
                    LogAction($"{GetCharLogName(enemyId)} travel to {poi}");
                }
                if (overrideDestination != null)
                {
                    Builder.SetEnemyDestination(enemyId, overrideDestination.Value, run);
                    Builder.WaitForEnemyTravel(enemyId);
                    Builder.MoveEnemy(enemyId, overrideDestination.Value);
                    LogAction($"{GetCharLogName(enemyId)} travel to {overrideDestination}");
                }
                else
                {
                    Builder.MoveEnemy(enemyId, destination.Position);
                }
                Builder.SetFlag(4, flag);
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

            protected void LongConversation()
            {
                Builder.FadeOutMusic();
                Builder.Sleep(60);
                for (int i = 0; i < 2; i++)
                {
                    Builder.PlayVoice(Rng.Next(0, 30));
                    Builder.Sleep(15);
                }
                Builder.Sleep(60);
                Builder.ResumeMusic();
                LogAction($"conversation");
            }

            protected PointOfInterest? GetRandomDoor()
            {
                return GetRandomPoi(x => x.Kind == PoiKind.Door || x.Kind == PoiKind.Stairs);
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

        private class EnemyChangePlot : Plot
        {
            protected override void Build()
            {
                var numPlacements = Rng.Next(6, 12);
                var placements = Cr._enemyPositions
                    .Take(numPlacements)
                    .ToArray();
                var enemyIds = Builder.AllocateEnemies(placements.Length);

                Builder.IfPlotTriggered();
                LogTrigger("re-enter room");
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    Builder.Enemy(enemyIds[i], Cr._enemyPositions[i], 6, 0);
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
                    .Take(numPlacements)
                    .ToArray();
                var enemyIds = Builder.AllocateEnemies(placements.Length);

                Builder.IfPlotTriggered();
                // Setup enemies in woken up positions
                for (int i = 0; i < placements.Length; i++)
                {
                    Builder.Enemy(enemyIds[i], placements[i], Rng.NextOf(0, 6), 0);
                }

                Builder.ElseBeginTriggerThread();

                // Setup initial enemy positions
                for (int i = 0; i < placements.Length; i++)
                {
                    Builder.Enemy(enemyIds[i], placements[i], 4, 128);
                }

                // Wait for triggers
                AddTriggers(new int[0]);

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

        private class EnemyWalksInPlot : Plot
        {
            protected override void Build()
            {
                var door = GetRandomDoor()!;
                var enemyIds = Builder.AllocateEnemies(Rng.Next(1, 5));

                Builder.IfPlotTriggered();
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    int eid = enemyIds[i];
                    var pos = door.Position;
                    if (Cr._enemyPositions.Length != 0)
                    {
                        pos = Cr._enemyPositions[i % Cr._enemyPositions.Length];
                    }
                    Builder.Enemy(eid, pos, 6, 0);
                }

                Builder.ElseBeginTriggerThread();

                foreach (var eid in enemyIds)
                {
                    Builder.Enemy(eid, REPosition.OutOfBounds.WithY(door.Position.Y), 6, 128);
                }

                AddTriggers(door.Cuts);

                // Move enemies into position and cut to them
                Builder.LockPlot();
                DoDoorOpenCloseCut(door);
                Builder.BeginCutsceneMode();
                foreach (var eid in enemyIds)
                {
                    Builder.MoveEnemy(eid, door.Position);
                    Builder.ActivateEnemy(eid);
                }
                LogAction($"{enemyIds.Length}x enemy walk in");
                Builder.Sleep(60);
                Builder.CutRevert();
                Builder.EndCutsceneMode();
                Builder.UnlockPlot();
            }
        }

        private class AllyStaticPlot : Plot
        {
            protected override bool Check()
            {
                return GetRandomPoi(x => x.Kind == PoiKind.Meet || x.Kind == PoiKind.Npc) != null;
            }

            protected override void Build()
            {
                var npcId = Builder.AllocateEnemies(1).FirstOrDefault();
                var meetup = GetRandomPoi(x => x.Kind == PoiKind.Meet || x.Kind == PoiKind.Npc)!;

                Builder.BeginSubProcedure();
                Builder.BeginCutsceneMode();
                Builder.BeginIf();
                Builder.CheckFlag(4, Cr._plotId);
                Builder.PlayVoice(21);
                Builder.Else();
                if (meetup.CloseCut != null)
                    Builder.CutChange(meetup.CloseCut.Value);
                LongConversation();
                if (meetup.CloseCut != null)
                    Builder.CutRevert();
                Builder.SetFlag(4, Cr._plotId);
                Builder.EndIf();
                Builder.EndCutsceneMode();
                var eventProc = Builder.EndSubProcedure();

                Builder.Ally(npcId, meetup.Position);
                Builder.Event(meetup.Position, 2000, eventProc);
            }
        }

        private class AllyWalksInPlot : Plot
        {
            protected override bool Check()
            {
                return GetRandomPoi(x => x.Kind == PoiKind.Meet) != null;
            }

            protected override void Build()
            {
                var npcId = Builder.AllocateEnemies(1).FirstOrDefault();
                var door = GetRandomDoor()!;
                var meetup = GetRandomPoi(x => x.Kind == PoiKind.Meet)!;
                var meetA = new REPosition(meetup.X + 1000, meetup.Y, meetup.Z, 2000);
                var meetB = new REPosition(meetup.X - 1000, meetup.Y, meetup.Z, 0);

                Builder.IfPlotTriggered();
                Builder.ElseBeginTriggerThread();
                Builder.Ally(npcId, REPosition.OutOfBounds.WithY(door.Position.Y));

                var triggerCut = AddTriggers(door.Cuts);
                if (triggerCut == null)
                    throw new Exception("Cutscene not supported for non-cut triggers.");

                Builder.LockPlot();

                // Mark the cutscene as done in case it softlocks
                Builder.SetFlag(4, Cr._plotId);

                DoDoorOpenCloseCut(door);
                Builder.BeginCutsceneMode();
                Builder.MoveEnemy(npcId, door.Position);
                Builder.PlayVoice(21);
                Builder.Sleep(30);
                LogAction($"NPC walk in");

                Builder.CutRevert();

                IntelliTravelTo(100, npcId, door, meetup, meetB, run: true);
                IntelliTravelTo(101, -1, triggerCut, meetup, meetA, run: true);

                Builder.WaitForFlag(100);
                Builder.WaitForFlag(101);

                LogAction($"Focus on {meetup}");
                if (meetup.CloseCut != null)
                    Builder.CutChange(meetup.CloseCut.Value);
                else
                    Builder.CutChange(meetup.Cut);
                LongConversation();
                Builder.CutChange(meetup.Cut);

                IntelliTravelTo(100, npcId, meetup, door, null, run: true);
                Builder.WaitForFlag(100);

                Builder.MoveEnemy(npcId, REPosition.OutOfBounds);
                Builder.StopEnemy(npcId);

                DoDoorOpenCloseCutAway(door, meetup.Cut);
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

        public static REPosition OutOfBounds { get; } = new REPosition(-32000, 0, -32000);

        public override string ToString() => $"({X},{Y},{Z},{D})";
    }

    public class CutsceneRoomInfo
    {
        public PointOfInterest[]? Poi { get; set; }
    }

    public class PointOfInterest
    {
        public int Id { get; set; }
        public string? Kind { get; set; }
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

        public override string ToString() => $"Id = {Id} Kind = {Kind} Cut = {Cut} Position = {Position}";
    }

    public static class PoiKind
    {
        public static string Trigger = "trigger";
        public static string Door = "door";
        public static string Stairs = "stairs";
        public static string Waypoint = "waypoint";
        public static string Meet = "meet";
        public static string Npc = "npc";
    }
}
