using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class AllyWaitPlot : IPlot
    {
        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var rng = builder.Rng;
            var previousEnemies = builder.GetEnemies();

            // Can the NPC enter the room via a door
            var graphs = builder.PoiGraph
                .GetGraphsContaining(PoiKind.Door, PoiKind.Npc)
                .Shuffle(rng);
            var graph = graphs.FirstOrDefault();
            var waitPoi = null as PointOfInterest;
            var doorEntry = null as PointOfInterest;
            var doorExit = null as PointOfInterest;
            if (graph != null)
            {
                doorEntry = graph.Shuffle(rng).FirstOrDefault(x => x.HasTag(PoiKind.Door));
                doorExit = graph.Shuffle(rng).FirstOrDefault(x => x.HasTag(PoiKind.Door));
                waitPoi = graph.Shuffle(rng).FirstOrDefault(x => x.HasTag(PoiKind.Npc));

                if (rng.NextProbability(50))
                    doorEntry = null;
                if (rng.NextProbability(50))
                    doorExit = null;
            }
            else
            {
                waitPoi = builder.PoiGraph.GetRandomPoi(rng, x => x.HasTag(PoiKind.Npc));
            }
            if (waitPoi == null)
                return null;

            var plotFlag = builder.AllocateGlobalFlag();
            var converseFlag = builder.AllocateGlobalFlag();

            var player = builder.GetPlayer();
            var ally = builder.AllocateAlly();
            var item = rng.NextProbability(75) ? GetRandomItem(builder) : null;
            var interaction = builder.AllocateEvent();

            var loopProcedure = new SbProcedure(
                new SbLoop(
                    new SbSetEntityNeck(ally, 64),
                    new SbSleep(30)));

            var eventProcedure = new SbProcedure(
                new SbCutsceneBars(
                    new SbIf(converseFlag, true,
                        builder.Voice(new ICsHero[] { player, ally }))
                    .Else(
                        CreateInitalConversation(builder, waitPoi!.CloseCut, new ICsHero[] { player, ally }, item),
                        new SbSetFlag(converseFlag))),
                SbNode.Conditional(doorExit != null, () => new SbContainerNode(
                    new SbSetEntityCollision(ally, false),
                    new SbCommentNode($"[action] ally travel to {{ {waitPoi} }}",
                        builder.Travel(ally, waitPoi, doorExit!, PlcDestKind.Run, overrideDestination: doorExit!.Position.Reverse())),
                    new SbMoveEntity(ally, REPosition.OutOfBounds),
                    new SbCommentNode($"[action] ally leave at {{ {doorExit} }}",
                        new SbDoor(doorExit)))));

            SbProcedure? triggerProcedure = null;
            if (doorEntry != null)
            {
                triggerProcedure = new SbProcedure(
                    builder.CreateTrigger(doorEntry.Cuts),

                    new SbSetFlag(plotFlag),
                        new SbLockPlot(
                            new SbCommentNode($"[action] ally enter at {{ {doorEntry} }}",
                                new SbDoor(doorEntry),
                                new SbCutsceneBars(
                                    new SbCut(doorEntry.Cut,
                                        new SbFreezeEnemies(previousEnemies,
                                            new SbMoveEntity(ally, doorEntry.Position),
                                            new SbSleep(60))))),
                            new SbCommentNode($"[action] ally travel to {{ {waitPoi} }}",
                                builder.Travel(ally, doorEntry, waitPoi!, PlcDestKind.Run)),
                            new SbCommentNode($"[action] ally enable interaction",
                                new SbEnableEvent(interaction, eventProcedure)),
                            new SbSleep(30 * 4)));
            }

            SbNode waitBlock = new SbContainerNode(
                new SbAlly(ally, waitPoi!.Position),
                new SbAot(interaction, waitPoi.Position, 2000),
                new SbEnableEvent(interaction, eventProcedure));
            if (doorEntry != null)
            {
                waitBlock =
                    new SbIf(plotFlag, false,
                        SbNode.Conditional(doorEntry != null, () => new SbContainerNode(
                            new SbAlly(ally, REPosition.OutOfBounds),
                            new SbAot(interaction, waitPoi.Position, 2000),
                            new SbFork(triggerProcedure!))))
                    .Else(waitBlock);
            }

            SbNode initBlock = new SbContainerNode(
                waitBlock,
                new SbFork(loopProcedure),
                SbNode.Conditional(item != null, () => new SbContainerNode(
                    new SbItem(item!),
                    new SbSetFlag(new CsFlag(CutsceneBuilder.FG_ITEM, 255), false))));
            if (doorExit != null)
            {
                initBlock = new SbIf(converseFlag, false, initBlock);
            }
            var initProcedure = new SbProcedure(
                new SbCommentNode($"[plot] ally wait {{ flag {plotFlag.Flag} }}",
                    initBlock));

            return new CsPlot(initProcedure);
        }

        private SbNode CreateInitalConversation(PlotBuilder builder, int? cut, ICsHero[] participants, CsItem? item)
        {
            SbNode result = new SbMuteMusic(
                new SbSleep(60),
                builder.Conversation(2, 4, participants, participants),
                SbNode.Conditional(item != null, () => new SbContainerNode(
                    new SbAotOn(item!))),
                builder.Conversation(0, 2, participants, participants),
                new SbSleep(60));
            if (cut != null)
            {
                result = new SbCut(cut.Value, result);
            }
            return result;
        }

        private CsItem GetRandomItem(PlotBuilder builder)
        {
            var rng = builder.Rng;
            var amount = 1;
            var config = builder.Config;
            var itemHelper = new Re2ItemHelper();
            var itemRando = builder.ItemRandomiser;
            byte type;
            if (itemRando == null)
            {
                var kind = rng.NextOf(ItemAttribute.Ammo, ItemAttribute.Heal, ItemAttribute.InkRibbon);
                if (kind == ItemAttribute.Ammo)
                {
                    if (config.Player == 0)
                    {
                        type = rng.NextOf(
                            Re2ItemIds.HandgunAmmo,
                            Re2ItemIds.ShotgunAmmo,
                            Re2ItemIds.MagnumAmmo);
                    }
                    else
                    {
                        type = rng.NextOf(
                            Re2ItemIds.HandgunAmmo,
                            Re2ItemIds.BowgunAmmo,
                            Re2ItemIds.GrenadeLauncherAcid,
                            Re2ItemIds.GrenadeLauncherExplosive,
                            Re2ItemIds.GrenadeLauncherFlame);
                    }
                    var capacity = itemHelper.GetMaxAmmoForAmmoType(type);
                    amount = rng.Next(capacity / 2, capacity);
                }
                else if (kind == ItemAttribute.Heal)
                {
                    type = rng.NextOf(
                        Re2ItemIds.HerbGRB,
                        Re2ItemIds.FAidSpray);
                }
                else
                {
                    type = Re2ItemIds.InkRibbon;
                    amount = rng.Next(3, 6);
                }
            }
            else
            {
                var item = itemRando.GetRandomGift(rng);
                type = item.Type;
                amount = item.Amount;
            }

            return builder.AllocateItem(new Item(type, (byte)amount));
        }
    }
}
