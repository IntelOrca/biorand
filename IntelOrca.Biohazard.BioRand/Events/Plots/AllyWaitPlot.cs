using System;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class AllyWaitPlot : IPlot
    {
        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var subplots = new Func<SubPlot>[]
            {
                () => new ChatSubPlot(),
                () => new HelpSubPlot(),
                () => new ItemGiftSubPlot(),
                () => new TradeSubPlot(),
                () => new CurePoisonSubPlot(),
                () => new CureSubPlot()
            };
            var subPlotFactory = builder.Rng.NextOf(subplots);
            return BuildPlot(builder, subPlotFactory());
        }

        private CsPlot? BuildPlot(PlotBuilder builder, SubPlot subPlot)
        {
            var rng = builder.Rng;

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
                if (!subPlot.CanExit || rng.NextProbability(50))
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
            var interaction = builder.AllocateEvent();

            subPlot.PlotBuilder = builder;
            subPlot.Player = player;
            subPlot.Ally = ally;
            subPlot.Cut = waitPoi!.CloseCut;
            subPlot.ConverseFlag = converseFlag;

            var loopProcedure = new SbProcedure(
                new SbLoop(
                    new SbSetEntityNeck(ally, 64),
                    new SbSleep(30)));

            var eventProcedure = new SbProcedure(
                new SbIf(builder.GetPlotLockFlag(), false,
                    subPlot.OnConversation(),
                    SbNode.Conditional(doorExit != null, () => new SbContainerNode(
                        new SbDisableAot(interaction),
                        new SbSetEntityCollision(ally, false),
                        new SbCommentNode($"[action] ally travel to {{ {waitPoi} }}",
                            builder.Travel(ally, waitPoi, doorExit!, PlcDestKind.Run, overrideDestination: doorExit!.Position.Reverse())),
                        new SbMoveEntity(ally, REPosition.OutOfBounds),
                        new SbCommentNode($"[action] ally leave at {{ {doorExit} }}",
                            new SbDoor(doorExit))))));

            SbProcedure? triggerProcedure = null;
            if (doorEntry != null)
            {
                triggerProcedure = new SbProcedure(
                    builder.CreateTrigger(doorEntry.Cuts),
                    new SbSetFlag(plotFlag),
                    new SbLockPlot(
                        new SbCommentNode($"[action] ally enter at {{ {doorEntry} }}",
                            new SbCutSequence(
                                new SbDoor(doorEntry),
                                new SbCutsceneBars(
                                    new SbCut(doorEntry.Cut,
                                        new SbFreezeAllEnemies(
                                            new SbMoveEntity(ally, doorEntry.Position),
                                            builder.Voice(new ICsHero[] { ally, player }, new[] { ally })))))),
                        new SbCommentNode($"[action] ally travel to {{ {waitPoi} }}",
                            builder.Travel(ally, doorEntry, waitPoi!, PlcDestKind.Run)),
                        new SbCommentNode($"[action] ally enable interaction",
                            new SbEnableEvent(interaction, eventProcedure)),
                        new SbSleep(30 * 2)));
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
                subPlot.OnInit());
            if (doorExit != null)
            {
                initBlock = new SbIf(converseFlag, false, initBlock);
            }
            var initProcedure = new SbProcedure(
                new SbCommentNode($"[plot] ally wait {{ flag {plotFlag.Flag} }}",
                    initBlock));

            return new CsPlot(initProcedure);
        }

        private static string PrettyActorName(string s)
        {
            var dot = s.IndexOf('.');
            if (dot != -1)
            {
                s = s.Substring(0, dot);
            }
            return s.ToTitle();
        }

        private class SubPlot
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public PlotBuilder PlotBuilder { get; set; }
            public ICsHero Player { get; set; }
            public ICsHero Ally { get; set; }
            public int? Cut { get; set; }
            public CsFlag ConverseFlag { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public ICsHero[] Participants => new[] { Player, Ally };
            public virtual bool CanExit => true;

            public virtual SbNode OnInit() => new SbNop();

            public virtual SbNode OnConversation()
            {
                return
                    new SbLockPlot(
                        new SbIf(ConverseFlag, true,
                            OnQuickConversation())
                        .Else(
                            OnFullConversation(),
                            new SbSetFlag(ConverseFlag),
                            new SbSleep(2 * 30)));
            }

            public virtual SbNode OnFullConversation()
            {
                var builder = PlotBuilder;
                SbNode result = new SbMuteMusic(
                    new SbCommentNode("[action] conversation",
                        new SbSleep(60),
                        builder.Conversation(2, 4, Participants, Participants),
                        OnAfterChat(),
                        new SbSleep(60)));
                if (Cut is int cut)
                {
                    result = new SbCut(cut, result);
                }
                return
                    new SbFreezeAllEnemies(
                        new SbCutsceneBars(
                            result));
            }

            public virtual SbNode OnQuickConversation()
            {
                return
                    new SbFreezeAllEnemies(
                        new SbCutsceneBars(
                            PlotBuilder.Voice(Participants, new[] { Ally })));
            }

            public virtual SbNode OnAfterChat() => new SbNop();
        }

        private class ChatSubPlot : SubPlot
        {
        }

        private class HelpSubPlot : SubPlot
        {
            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var msg = builder.AllocateMessage($"Will you help {PrettyActorName(Ally.Actor)}?@");
                var msg2 = builder.AllocateMessage($"You helped {PrettyActorName(Ally.Actor)}.");
                return new SbCommentNode("[action] question",
                    new SbMessage(msg,
                        new SbContainerNode(
                            new SbMessage(msg2),
                            builder.Voice(Participants, new[] { Ally })),
                        builder.Voice(Participants, new[] { Ally }, "doom")));
            }
        }

        private class ItemGiftSubPlot : SubPlot
        {
            private CsItem? _item;

            public CsItem GetOrCreateItem()
            {
                _item ??= GetRandomItem();
                return _item;
            }

            public override SbNode OnInit()
            {
                var item = GetOrCreateItem();
                return new SbContainerNode(
                    new SbItem(item),
                    new SbSetFlag(new CsFlag(CutsceneBuilder.FG_ITEM, 255), false));
            }

            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var item = GetOrCreateItem();
                var itemHelper = new Re2ItemHelper();
                return new SbContainerNode(
                    builder.Voice(Participants, new[] { Ally }, "item"),
                    new SbCommentNode($"[action] gift {{ {itemHelper.GetItemName(item.Item.Type)} x{item.Item.Amount} }}",
                        new SbAotOn(item)),
                    builder.Conversation(0, 2, Participants, Participants));
            }

            private CsItem GetRandomItem()
            {
                var builder = PlotBuilder;
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

        private class TradeSubPlot : SubPlot
        {
            public override bool CanExit => false;

            public override SbNode OnQuickConversation() => OnAfterChat();

            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var msg0 = builder.AllocateMessage("Will trade {herb} for {ink}.");
                var msg1 = builder.AllocateMessage("Trade {ink} for {herb}?@");
                return
                    new SbIf(new SbCkItem(Re2ItemIds.InkRibbon),
                        new SbCommentNode("[action] question - trade",
                            new SbMessage(msg1,
                                new SbContainerNode(
                                    new SbRemoveItem(Re2ItemIds.InkRibbon),
                                    new SbGetItem(Re2ItemIds.HerbG, 1)))))
                    .Else(
                        new SbCommentNode("[action] message - trade",
                            new SbMessage(msg0)));
            }
        }

        private class CurePoisonSubPlot : SubPlot
        {
            public override bool CanExit => false;

            public override SbNode OnQuickConversation() => OnAfterChat();

            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var msg0 = builder.AllocateMessage("I can treat poison wounds.");
                var msg1 = builder.AllocateMessage("You poisoned wounds\nhave been treated.");
                return
                    new SbIf(new SbCkPoison(),
                        new SbCommentNode("[action] heal player (poison)",
                            new SbHealPoison(),
                            new SbMessage(msg1)))
                    .Else(
                        new SbCommentNode("[action] message - poison",
                            new SbMessage(msg0)));
            }
        }

        private class CureSubPlot : SubPlot
        {
            public override bool CanExit => false;

            public override SbNode OnQuickConversation() => OnAfterChat();

            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var msg0 = builder.AllocateMessage("Would you like me to treat\nyour wounds?@");
                var msg1 = builder.AllocateMessage("Your wounds have been\ntreated.");
                return
                    new SbCommentNode("[action] question - heal",
                        new SbMessage(msg0,
                            new SbCommentNode("[action] heal player",
                                new SbHeal(),
                                new SbMessage(msg1))));
            }
        }
    }
}
