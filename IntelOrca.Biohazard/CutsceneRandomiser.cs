using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using IntelOrca.Biohazard.Script.Opcodes;

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

        // Current room
        private CutsceneBuilder _cb = new CutsceneBuilder();
        private RdtId _rdtId;
        private List<PlotKind> _plots = new List<PlotKind>();
        private int _plotId;
        private int _lastPlotId;
        private Action? _extra;
        private PointOfInterest[] _poi = new PointOfInterest[0];
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

            _enemyPositions = _allEnemyPositions
                .Where(x => x.RdtId == rdt.RdtId)
                .Select(p => new REPosition(p.X, p.Y, p.Z, p.D))
                .Shuffle(_rng)
                .ToArray();

            _logger.WriteLine($"  {rdt}:");

            var doors = info.Poi?.Where(x => x.Kind == "door").ToArray() ?? new PointOfInterest[0];
            var triggers = info.Poi?.Where(x => x.Kind == "trigger").ToArray() ?? new PointOfInterest[0];
            var meets = info.Poi?.Where(x => x.Kind == "meet").ToArray() ?? new PointOfInterest[0];

            var cb = new CutsceneBuilder();
            cb.Begin();

            _cb = cb;
            _rdtId = rdt.RdtId;
            _lastPlotId = -1;
            _poi = info.Poi ?? new PointOfInterest[0];
            // ChainRandomPlot(PlotKind.MeetStaticNPC);
            // ChainRandomPlot(PlotKind.EnemyWalksIn);
            // ChainRandomPlot(PlotKind.EnemyGetsUp);
            ChainRandomPlot(PlotKind.MeetWalkInNPC);
            ChainRandomPlot(PlotKind.EnemyWalksIn);
            ChainRandomPlot(PlotKind.EnemyWalksIn);

#if false
            var triggerPoi = triggers[1]; // pois.Select(x => (int?)x.Cut).Shuffle(_rng).FirstOrDefault();
            var meetPoi = meets[0];

            // Trigger camera
            var triggerCut = (int?)triggerPoi.Cut;
            if (triggerCut != null)
            {
                // Random door, not in the cut
                var randomDoor = doors
                    .Where(x => !x.Cuts.Contains(triggerCut.Value))
                    .Shuffle(_rng)
                    .FirstOrDefault();
                if (randomDoor != null)
                {
                    // New plot: enemy enters when cut is triggered
                    // var plot0id = CreateNPCEntranceExitEvent(cb, triggerPoi, randomDoor, meetPoi);
                    // var plot1id = CreateEnemyEntranceEvent(cb, plot0id, randomDoor);
                    // DoZombieWakeUp(cb, rdt.RdtId);
                }
            }
#endif

            cb.End();
            rdt.CustomAdditionalScript = cb.ToString();
        }

        private void ChainRandomPlot()
        {
            var plotKinds = Enum.GetValues(typeof(PlotKind))
                .Cast<PlotKind>()
                .Shuffle(_rng)
                .ToArray();

            foreach (var plotKind in plotKinds)
            {
                ChainRandomPlot(plotKind);
            }
        }

        private void ChainRandomPlot(PlotKind kind)
        {
            _extra = null;
            _plotId = _cb.BeginPlot();

            _logger.WriteLine($"    [plot] #{_plotId}: {kind}");
            switch (kind)
            {
                case PlotKind.EnemyOnReturn:
                    DoEnemyOnReturn();
                    break;
                case PlotKind.EnemyGetsUp:
                    DoZombieWakeUp();
                    break;
                case PlotKind.EnemyWalksIn:
                    DoEnemyWalksIn();
                    break;
                case PlotKind.MeetWalkInNPC:
                    DoNPCWalksIn();
                    break;
                case PlotKind.MeetStaticNPC:
                    DoStaticNPC();
                    break;
                default:
                    throw new NotImplementedException();
            }

            _cb.EndPlot();
            _extra?.Invoke();
            _lastPlotId = _plotId;
        }

        private void AddTriggers(int[]? notCuts)
        {
            if (_lastPlotId != -1)
            {
                LogTrigger($"after plot {_lastPlotId}");
                _cb.WaitForPlot(_lastPlotId);
            }

            var sleepTime = _rng.Next(0, 10);
            var justSleep = false;
            if (notCuts == null || notCuts.Length == 0)
            {
                if (_rng.NextProbability(50))
                {
                    // Just a sleep trigger
                    sleepTime = _rng.Next(5, 30);
                    justSleep = true;
                }
            }

            // Sleep trigger
            if (sleepTime != 0)
            {
                _cb.Sleep(30 * sleepTime);
                LogTrigger($"wait {sleepTime} seconds");
            }

            if (justSleep)
                return;

            // Random cut
            var cut = _poi
                .Select(x => x.Cut)
                .Distinct()
                .Where(x => !notCuts.Contains(x))
                .Shuffle(_rng)
                .FirstOrDefault();
            LogTrigger($"cut {cut}");
            _cb.WaitForTriggerCut(cut);
        }

        private void LogTrigger(string s) => _logger.WriteLine($"      [trigger] {s}");
        private void LogAction(string s) => _logger.WriteLine($"      [action] {s}");

        private void DoEnemyOnReturn()
        {
            var numPlacements = _rng.Next(6, 12);
            var placements = _enemyPositions
                .Take(numPlacements)
                .ToArray();
            var enemyIds = _cb.AllocateEnemies(placements.Length);

            _cb.IfPlotTriggered();
            LogTrigger("re-enter room");
            for (int i = 0; i < enemyIds.Length; i++)
            {
                _cb.Enemy(enemyIds[i], _enemyPositions[i], 6, 0);
            }
            LogAction($"{enemyIds.Length}x enemy");
        }

        private void DoZombieWakeUp()
        {
            var numPlacements = _rng.Next(6, 12);
            var placements = _enemyPositions
                .Take(numPlacements)
                .ToArray();
            var enemyIds = _cb.AllocateEnemies(placements.Length);

            _cb.IfPlotTriggered();
            // Setup enemies in woken up positions
            for (int i = 0; i < placements.Length; i++)
            {
                _cb.Enemy(enemyIds[i], placements[i], _rng.NextOf(0, 6), 0);
            }

            _cb.ElseBeginTriggerThread();

            // Setup initial enemy positions
            for (int i = 0; i < placements.Length; i++)
            {
                _cb.Enemy(enemyIds[i], placements[i], 4, 128);
            }

            // Wait for triggers
            AddTriggers(new int[0]);

            // Wake up enemies incrementally
            foreach (var eid in enemyIds)
            {
                _cb.Sleep(_rng.Next(5, 15));
                _cb.ActivateEnemy(eid);
            }
            LogAction($"{enemyIds.Length}x enemy wake up");
        }

        private void DoEnemyWalksIn()
        {
            var door = GetRandomDoor();
            var enemyIds = _cb.AllocateEnemies(_rng.Next(1, 5));

            _cb.IfPlotTriggered();
            for (int i = 0; i < enemyIds.Length; i++)
            {
                int eid = enemyIds[i];
                var pos = door.Position;
                if (_enemyPositions.Length != 0)
                {
                    pos = _enemyPositions[i % _enemyPositions.Length];
                }
                _cb.Enemy(eid, pos, 6, 0);
            }

            _cb.ElseBeginTriggerThread();

            foreach (var eid in enemyIds)
            {
                _cb.Enemy(eid, REPosition.OutOfBounds.WithY(door.Position.Y), 6, 128);
            }

            AddTriggers(door.Cuts);

            // Move enemies into position and cut to them
            DoDoorOpenCloseCut(door);
            _cb.BeginCutsceneMode();
            foreach (var eid in enemyIds)
            {
                _cb.MoveEnemy(eid, door.Position);
                _cb.ActivateEnemy(eid);
            }
            LogAction($"{enemyIds.Length}x enemy walk in");
            _cb.Sleep(60);
            _cb.CutRevert();
            _cb.EndCutsceneMode();
        }

        private void DoNPCWalksIn()
        {
            var npcId = _cb.AllocateEnemies(1).FirstOrDefault();
            var door = GetRandomDoor();
            var meetup = _poi.FirstOrDefault(x => x.Kind == "meet")!;
            var meetA = new REPosition(meetup.X + 1000, meetup.Y, meetup.Z, meetup.D);
            var meetB = new REPosition(meetup.X - 1000, meetup.Y, meetup.Z, meetup.D);

            _cb.IfPlotTriggered();
            _cb.ElseBeginTriggerThread();
            _cb.Ally(npcId, REPosition.OutOfBounds.WithY(door.Position.Y));

            AddTriggers(door.Cuts);

            // Mark the cutscene as done in case it softlocks
            _cb.SetFlag(4, _plotId);

            DoDoorOpenCloseCut(door);
            _cb.BeginCutsceneMode();
            _cb.MoveEnemy(npcId, door.Position);
            _cb.SetEnemyNeck(npcId);
            _cb.PlayVoice(21);
            _cb.Sleep(30);
            LogAction($"NPC walk in");

            _cb.CutChange(meetup.Cut);
            _cb.SetEnemyDestination(-1, meetA, true);
            _cb.SetEnemyDestination(npcId, meetB, true);
            _cb.WaitForEnemyTravel(-1);
            LogAction($"player & NPC travel to cut {meetup.Cut}");

            LongConversation();

            _cb.SetEnemyDestination(npcId, door.Position, true);
            _cb.WaitForEnemyTravel(-1);
            LogAction($"NPC travel");

            _cb.MoveEnemy(npcId, REPosition.OutOfBounds);
            DoDoorOpenClose(door);
            _cb.ReleaseEnemyControl(-1);
            _cb.CutAuto();
            _cb.EndCutsceneMode();
        }

        private void DoStaticNPC()
        {
            var npcId = _cb.AllocateEnemies(1).FirstOrDefault();
            var meetup = _poi.FirstOrDefault(x => x.Kind == "meet")!;
            var eventProc = _cb.AllocateProcedure();

            _cb.Ally(npcId, meetup.Position);
            _cb.Event(meetup.Position, 2000, eventProc);

            _extra = () =>
            {
                _cb.BeginProcedure(eventProc);
                _cb.BeginCutsceneMode();
                _cb.BeginIf();
                _cb.CheckFlag(4, _plotId);
                _cb.PlayVoice(21);
                _cb.Else();
                LongConversation();
                _cb.SetFlag(4, _plotId);
                _cb.EndIf();
                _cb.EndCutsceneMode();
                _cb.EndProcedure();
            };
        }

        private void LongConversation()
        {
            _cb.FadeOutMusic();
            _cb.Sleep(60);
            for (int i = 0; i < 2; i++)
            {
                _cb.PlayVoice(_rng.Next(0, 30));
                _cb.Sleep(15);
            }
            _cb.Sleep(60);
            _cb.ResumeMusic();
            LogAction($"conversation");
        }

        private void DoDoorOpenClose(PointOfInterest door)
        {
            var pos = door.Position;
            if (door.Kind == "door")
            {
                _cb.PlayDoorSoundOpen(pos);
                _cb.Sleep(30);
                _cb.PlayDoorSoundClose(pos);
            }
            else
            {
                _cb.Sleep(30);
            }
        }

        private void DoDoorOpenCloseCut(PointOfInterest door)
        {
            DoDoorOpenClose(door);
            _cb.CutChange(door.Cut);
            LogAction($"door cut {door.Cut}");
        }

        private PointOfInterest GetRandomDoor()
        {
            return _poi
                .Where(x => x.Kind == "door" || x.Kind == "stairs")
                .Shuffle(_rng)
                .FirstOrDefault();
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
    }

    public struct REPosition
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int D { get; }

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
    }

    public class CutsceneRoomInfo
    {
        public PointOfInterest[]? Poi { get; set; }
    }

    public class PointOfInterest
    {
        public string? Kind { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public int Cut { get; set; }
        public int[]? Cuts { get; set; }

        public REPosition Position => new REPosition(X, Y, Z, D);
    }

    public enum PlotKind
    {
        MeetWalkInNPC,
        MeetStaticNPC,
        EnemyWalksIn,
        EnemyGetsUp,
        EnemyOnReturn,
    }
}
