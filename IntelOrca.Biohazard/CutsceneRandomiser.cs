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
        }

        public void Randomise(PlayGraph? graph)
        {
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

            var doors = info.Doors ?? new CutsceneRoomInfo.DoorInfo[0];
            var pois = info.Poi ?? new CutsceneRoomInfo.PoiInfo[0];

            var cb = new CutsceneBuilder();
            cb.Begin();

            var triggerPoi = pois[2]; // pois.Select(x => (int?)x.Cut).Shuffle(_rng).FirstOrDefault();
            var conversePoi = pois[1];

            // Trigger camera
            var triggerCut = (int?)triggerPoi.Cut;
            if (triggerCut != null)
            {
                // Random door, not in the cut
                var excludeDoors = doors
                    .Where(x => x.Cuts.Contains(triggerCut.Value))
                    .ToArray();
                var randomDoor = doors
                    .Where(x => !excludeDoors.Any(y => y.Id == x.Id))
                    .Shuffle(_rng)
                    .FirstOrDefault();
                if (randomDoor != null)
                {
                    var spawnPosition = new REPosition(randomDoor.X, randomDoor.Y, randomDoor.Z, randomDoor.D);
                    var conversePositionA = new REPosition(conversePoi.X + 1000, conversePoi.Y, conversePoi.Z, conversePoi.D);
                    var conversePositionB = new REPosition(conversePoi.X - 1000, conversePoi.Y, conversePoi.Z, conversePoi.D);

                    // New plot: enemy enters when cut is triggered
                    cb.BeginPlot();

                    var enemyIds = new List<int>();
                    enemyIds.Add(cb.AddEnemy());
                    enemyIds.Add(cb.AddEnemy());
                    var npcId = cb.AddNPC();
                    cb.PlotActivated();
                    {
                        foreach (var eid in enemyIds)
                            cb.SpawnEnemy(eid, spawnPosition);
                    }
                    cb.PlotTriggerLoop();
                    {
                        cb.WaitForTriggerCut(triggerCut.Value);
                        cb.Sleep(15);
                        cb.PlayDoorSoundOpen(spawnPosition);
                        cb.Sleep(15);
                        cb.PlayDoorSoundClose(spawnPosition);
                        cb.BeginCutsceneMode();
                        cb.CutChange(randomDoor.Cut);
                        // cb.PlayMusic(rdt.RdtId, 0x14, 255);
                        // foreach (var eid in enemyIds)
                        //     cb.SpawnEnemy(eid, spawnPosition);

                        cb.MoveEnemy(npcId, spawnPosition);
                        cb.PlayVoice(21);
                        cb.Sleep(30);

                        cb.CutChange(conversePoi.Cut);
                        cb.SetEnemyDestination(-1, conversePositionA, true);
                        cb.SetEnemyDestination(npcId, conversePositionB, true);
                        cb.WaitForEnemyTravel(-1);
                        cb.FadeOutMusic();
                        cb.Sleep(60);

                        for (int i = 0; i < 2; i++)
                        {
                            cb.PlayVoice(_rng.Next(0, 30));
                            cb.Sleep(15);
                        }

                        cb.Sleep(60);
                        cb.SetEnemyDestination(npcId, spawnPosition, true);
                        cb.ReleaseEnemyControl(-1);
                        cb.ResumeMusic();

                        // cb.CutRevert();
                        cb.AppendLine("cut_auto", 1);
                        cb.EndCutsceneMode();
                    }
                    cb.EndPlot();
                }
            }

            cb.End();
            rdt.CustomAdditionalScript = cb.ToString();
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
    }

    public struct REPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }

        public REPosition(int x, int y, int z, int d)
        {
            X = x;
            Y = y;
            Z = z;
            D = d;
        }
    }

    public class CutsceneRoomInfo
    {
        public DoorInfo[]? Doors { get; set; }
        public PoiInfo[]? Poi { get; set; }

        public class DoorInfo
        {
            public int Id { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public int D { get; set; }
            public int Cut { get; set; }
            public int[]? Cuts { get; set; }
        }

        public class PoiInfo
        {
            public int Cut { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public int D { get; set; }
        }
    }
}
