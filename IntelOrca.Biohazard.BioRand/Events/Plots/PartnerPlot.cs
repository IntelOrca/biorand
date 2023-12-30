using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class PartnerPlot : IPlot
    {
        private const byte AI_FOLLOW = 0x04;
        private const byte AI_WAIT = 0x40;

        private readonly bool _joinable;

        public PartnerPlot() : this(false) { }
        public PartnerPlot(bool joinable)
        {
            _joinable = joinable;
        }

        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var enemyType = builder.EnemyType ?? 0;
            if (enemyType == Re2EnemyIds.ZombieArms)
                return null;

            var partner = builder.AllocatePartner();
            if (partner == null)
                return null;

            var randomPoi = builder.PoiGraph.GetRandomPoi(builder.Rng, _ => true);
            if (randomPoi == null)
                return null;

            var rng = builder.Rng;
            var graph = builder.PoiGraph;
            SbNode node =
                new SbIf(new CsFlag(builder.PartnerJoinFlag), true,
                    new SbAlly(partner, randomPoi.Position, AI_FOLLOW));
            if (_joinable)
            {
                var waitPoi = graph.GetRandomPoi(rng, x => x.HasTag("npc") || x.HasTag("meet"));
                if (waitPoi != null)
                {
                    var interaction = builder.AllocateEvent();
                    var eventProcedure = new SbProcedure(
                        new SbIf(new CsFlag(builder.PartnerJoinFlag), false,
                            new SbIf(builder.GetPlotLockFlag(), false,
                                AskToFollow(builder, waitPoi, partner))));

                    node = new SbIf(new CsFlag(builder.PartnerJoinFlag), false,
                        new SbAlly(partner, waitPoi.Position, AI_WAIT),
                        new SbFork(new SbProcedure(
                            new SbSleep(1),
                            new SbEntityTravel(partner, new ReFlag(0, 32), waitPoi.Position, PlcDestKind.Walk),
                            new SbLoop(
                                new SbSetEntityNeck(partner, 64),
                                new SbSleep(30)))),
                        new SbAot(interaction, waitPoi.Position, 2000),
                        new SbEnableEvent(interaction, eventProcedure))
                    .Else(
                        node);
                }
            }
            node = new SbCommentNode(_joinable ? "[plot] Partner (joinable)" : "[plot] Partner", node);
            return new CsPlot(new SbProcedure(node));
        }

        private SbNode AskToFollow(PlotBuilder builder, PointOfInterest poi, CsAlly ally)
        {
            var nodeBuilder = new SbNodeBuilder();
            var msg = builder.AllocateMessage($"Will you take {ally.DisplayName} with you?@");
            var player = builder.GetPlayer();
            var participants = new ICsHero[] { player, ally };
            nodeBuilder.Append(new SbMuteMusic(
                new SbCommentNode("[action] conversation",
                    new SbSleep(60),
                    builder.Conversation(2, 4, participants, participants),
                    new SbMessage(msg,
                        new SbContainerNode(
                        // new SbReleaseEntity(ally)
                        new SbSetFlag(new CsFlag(builder.PartnerJoinFlag)))),
                    new SbSleep(60))));
            if (poi.CloseCut is int cut)
            {
                nodeBuilder.Reparent(x => new SbCut(cut, x));
            }
            nodeBuilder.Reparent(x =>
                new SbFreezeAllEnemies(
                    new SbCutsceneBars(x)));
            return nodeBuilder.Build();
        }
    }

    internal class PartnerPlotJoinable : PartnerPlot
    {
        public PartnerPlotJoinable() : base(true) { }
    }
}
