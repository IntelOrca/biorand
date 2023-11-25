using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class PlotBuilder
    {
        private readonly NPCRandomiser? _npcRandomiser;
        private readonly VoiceRandomiser? _voiceRandomiser;
        private readonly RandomizedRdt _rdt;
        private readonly Queue<ReFlag> _availableGlobalFlags = new Queue<ReFlag>();
        private readonly Queue<ReFlag> _availableLocalFlags = new Queue<ReFlag>();
        private readonly Queue<byte> _availableEntityIds = new Queue<byte>();
        private readonly Queue<byte> _availableAotIds = new Queue<byte>();
        private readonly CsPlayer _player = new CsPlayer();
        private readonly List<CsEnemy> _enemies = new List<CsEnemy>();

        public Rng Rng { get; }
        public PoiGraph PoiGraph { get; }

        public PlotBuilder(
            Rng rng,
            NPCRandomiser? npcRandomiser,
            VoiceRandomiser? voiceRandomiser,
            RandomizedRdt rdt,
            PoiGraph poiGraph,
            ReFlag[] globalFlags,
            ReFlag[] localFlags,
            byte[] entityIds,
            byte[] aotIds)
        {
            Rng = rng;
            _rdt = rdt;
            PoiGraph = poiGraph;
            _npcRandomiser = npcRandomiser;
            _voiceRandomiser = voiceRandomiser;
            _availableGlobalFlags = globalFlags.ToQueue();
            _availableLocalFlags = localFlags.ToQueue();
            _availableEntityIds = entityIds.ToQueue();
            _availableAotIds = aotIds.ToQueue();
        }

        public CsFlag AllocateLocalFlag()
        {
            var flag = _availableLocalFlags.Dequeue();
            return new CsFlag(flag);
        }

        public CsFlag AllocateGlobalFlag()
        {
            var flag = _availableGlobalFlags.Dequeue();
            return new CsFlag(flag);
        }

        public CsPlayer GetPlayer()
        {
            return _player;
        }

        public CsEnemy AllocateEnemy()
        {
            var id = _availableEntityIds.Dequeue();
            var result = new CsEnemy(id);
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
    }
}
