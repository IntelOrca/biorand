using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class PlotBuilder
    {
        private readonly RandoConfig _config;
        private readonly EnemyRandomiser? _enemyRandomiser;
        private readonly NPCRandomiser? _npcRandomiser;
        private readonly VoiceRandomiser? _voiceRandomiser;
        private readonly RandomizedRdt _rdt;
        private readonly EndlessBag<REPosition> _enemyPositions;
        private readonly EndlessBag<ReFlag> _availableGlobalFlags = new EndlessBag<ReFlag>();
        private readonly Queue<ReFlag> _availableLocalFlags = new Queue<ReFlag>();
        private readonly Queue<byte> _availableEntityIds = new Queue<byte>();
        private readonly Queue<byte> _availableAotIds = new Queue<byte>();
        private readonly CsPlayer _player = new CsPlayer();
        private readonly List<CsEnemy> _enemies = new List<CsEnemy>();
        private readonly byte? _enemyType;
        private int _currentEnemyCount;

        public Rng Rng { get; }
        public PoiGraph PoiGraph { get; }
        public int MaximumEnemyCount { get; }

        public PlotBuilder(
            RandoConfig config,
            Rng rng,
            EnemyRandomiser? enemyRandomiser,
            NPCRandomiser? npcRandomiser,
            VoiceRandomiser? voiceRandomiser,
            RandomizedRdt rdt,
            PoiGraph poiGraph,
            EndlessBag<REPosition> enemyPositions,
            EndlessBag<ReFlag> globalFlags,
            ReFlag[] localFlags,
            byte[] entityIds,
            byte[] aotIds,
            byte? enemyType,
            int maximumEnemyCount)
        {
            _config = config;
            Rng = rng;
            _rdt = rdt;
            PoiGraph = poiGraph;
            _enemyPositions = enemyPositions;
            _enemyRandomiser = enemyRandomiser;
            _npcRandomiser = npcRandomiser;
            _voiceRandomiser = voiceRandomiser;
            _availableGlobalFlags = globalFlags;
            _availableLocalFlags = localFlags.ToQueue();
            _availableEntityIds = entityIds.ToQueue();
            _availableAotIds = aotIds.ToQueue();
            _enemyType = enemyType;
            MaximumEnemyCount = maximumEnemyCount;
        }

        public CsFlag AllocateLocalFlag()
        {
            var flag = _availableLocalFlags.Dequeue();
            return new CsFlag(flag);
        }

        public CsFlag AllocateGlobalFlag()
        {
            var flag = _availableGlobalFlags.Next();
            return new CsFlag(flag);
        }

        public CsPlayer GetPlayer()
        {
            return _player;
        }

        public CsEnemy AllocateEnemy()
        {
            var id = _availableEntityIds.Dequeue();
            var globalId = _enemyRandomiser?.GetNextKillId() ?? 255;
            var type = _enemyType ?? Re2EnemyIds.ZombieRandom;
            var position = _enemyPositions.Next();
            var enemyHelper = _enemyRandomiser?.EnemyHelper ?? new Re2EnemyHelper();
            var result = new CsEnemy(id, globalId, type, position,
                opcode => enemyHelper.SetEnemy(_config, Rng, opcode, new MapRoomEnemies(), opcode.Type));
            _enemies.Add(result);
            return result;
        }

        public CsAlly AllocateAlly()
        {
            var enemyType = Re2EnemyIds.ClaireRedfield;
            var actor = "claire";
            if (_npcRandomiser != null)
            {
                enemyType = _npcRandomiser.GetRandomNpc(_rdt, Rng);
                actor = _npcRandomiser.GetActor(enemyType) ?? actor;
            }
            var id = _availableEntityIds.Dequeue();
            return new CsAlly(id, enemyType, actor);
        }

        public CsItem AllocateItem(Item item)
        {
            var id = _availableAotIds.Dequeue();
            return new CsItem(id, 255, item);
        }

        public CsAot AllocateEvent()
        {
            var id = _availableAotIds.Dequeue();
            return new CsAot(id);
        }

        public CsEnemy[] GetEnemies()
        {
            return _enemies.ToArray();
        }

        public CsEnemy[] AllocateEnemies(int min = 1, int max = int.MaxValue)
        {
            var count = TakeEnemyCountForEvent(min, max);
            return Enumerable.Range(0, count)
                .Select(x => AllocateEnemy())
                .ToArray();
        }

        private int TakeEnemyCountForEvent(int min = 1, int max = int.MaxValue)
        {
            var max2 = Math.Min(max, Math.Max(min, MaximumEnemyCount - _currentEnemyCount));
            var count = Rng.Next(min, max2 + 1);
            _currentEnemyCount += count;
            return count;
        }

        public SbNode Voice(ICsHero[] participants) => Voice(participants, participants);
        public SbNode Voice(ICsHero[] participants, ICsHero[] speakers)
        {
            if (_voiceRandomiser == null)
            {
                return new SbNop();
            }
            else
            {
                var participantsActors = participants.Select(x => x.Actor).ToArray();
                var speakerActors = speakers.Select(x => x.Actor).ToArray();
                var result = _voiceRandomiser.AllocateConversation(Rng, _rdt.RdtId, 1, participantsActors, speakerActors);
                return new SbVoice(result[0]);
            }
        }

        public SbContainerNode Conversation(int minLines, int maxLines, ICsHero[] participants, ICsHero[] speakers)
        {
            var count = Rng.Next(minLines, maxLines + 1);
            var nodes = new List<SbNode>();
            for (var i = 0; i < count; i++)
            {
                nodes.Add(Voice(participants, speakers));
            }
            return new SbContainerNode(nodes.ToArray());
        }

        public SbContainerNode Travel(
            CsEntity entity,
            PointOfInterest from,
            PointOfInterest destination,
            PlcDestKind kind,
            REPosition? overrideDestination = null,
            bool cutFollow = false)
        {
            var completeFlag = AllocateLocalFlag();
            var subCompleteFlag = AllocateLocalFlag();
            var route = PoiGraph.GetTravelRoute(from, destination);

            var nodes = new List<SbNode>();
            foreach (var poi in route)
            {
                if (overrideDestination != null && poi == destination)
                    break;

                nodes.Add(new SbSetFlag(subCompleteFlag, false));
                nodes.Add(new SbEntityTravel(entity, subCompleteFlag.Flag, poi.Position, kind));
                nodes.Add(new SbWaitForFlag(subCompleteFlag.Flag));
                if (cutFollow)
                {
                    nodes.Add(new SbCut(poi.Cut));
                }
            }
            if (overrideDestination != null)
            {
                nodes.Add(new SbEntityTravel(entity, subCompleteFlag.Flag, overrideDestination.Value, kind));
                nodes.Add(new SbWaitForFlag(subCompleteFlag.Flag));
                nodes.Add(new SbMoveEntity(entity, overrideDestination.Value));
            }
            else
            {
                nodes.Add(new SbMoveEntity(entity, destination.Position));
            }
            nodes.Add(new SbSetFlag(completeFlag));

            var travelProcedure = new SbProcedure(
                nodes.ToArray());

            return new SbContainerNode(
                new SbSetFlag(completeFlag, false),
                new SbSetEntityCollision(entity, false),
                new SbFork(travelProcedure),
                new SbWaitForFlag(completeFlag.Flag),
                new SbSetEntityCollision(entity, true));
        }

        public SbNode CreateTrigger(
            int[]? notCuts = null,
            int minSleepTime = 0,
            int maxSleepTime = 20)
        {
            int? triggerTime = null;
            int? triggerCut = null;

            var triggerPoi = PoiGraph.GetRandomPoi(Rng, x => x.HasTag(PoiKind.Trigger) && notCuts?.Contains(x.Cut) != true);
            if (triggerPoi != null && Rng.NextProbability(75))
            {
                triggerCut = triggerPoi.Cut;
            }

            if (triggerCut == null && Rng.NextProbability(50))
            {
                triggerTime = Rng.Next(minSleepTime, maxSleepTime) * 30;
            }

            var result = new List<SbNode>();
            if (triggerTime != null)
            {
                result.Add(new SbCommentNode($"[trigger] wait {triggerTime / 30} seconds",
                    new SbSleep(triggerTime.Value)));
            }

            if (triggerCut != null)
            {
                result.Add(new SbCommentNode($"[trigger] wait for cut {triggerCut.Value}",
                    new SbWaitForCut(triggerCut.Value)));
            }

            result.Add(new SbWaitForFlag(new ReFlag(CutsceneBuilder.FG_ROOM, 23), false));
            result.Add(new SbWaitForFlag(new ReFlag(CutsceneBuilder.FG_STATUS, 27), false));
            result.Add(new SbWaitForFlag(new ReFlag(CutsceneBuilder.FG_STOP, 7), false));

            return new SbContainerNode(result.ToArray());
        }
    }
}
