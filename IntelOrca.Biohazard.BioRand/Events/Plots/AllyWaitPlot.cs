using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                () => new CureSubPlot(),
                () => builder.ItemRandomiser == null ?
                    (SubPlot)new ItemGiftSubPlot() :
                    new ChoiceSubPlot()
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
                if (!subPlot.MustExit && (!subPlot.CanExit || rng.NextProbability(50)))
                    doorExit = null;
            }
            else
            {
                waitPoi = builder.PoiGraph.GetRandomPoi(rng, x => x.HasTag(PoiKind.Npc));
            }
            if (waitPoi == null)
                return null;

            var enterFlag = builder.AllocateGlobalFlag();
            var exitFlag = builder.AllocateGlobalFlag();

            var player = builder.GetPlayer();
            var ally = builder.AllocateAlly();
            var interaction = builder.AllocateEvent();

            subPlot.PlotBuilder = builder;
            subPlot.Player = player;
            subPlot.Ally = ally;
            subPlot.Cut = waitPoi!.CloseCut;
            subPlot.ExitFlag = exitFlag;

            var loopProcedure = new SbProcedure(
                new SbLoop(
                    new SbSetEntityNeck(ally, 64),
                    new SbSleep(30)));

            var eventProcedure = new SbProcedure(
                new SbIf(builder.GetPlotLockFlag(), false,
                    subPlot.OnConversation(),
                    SbNode.Conditional(doorExit != null, () => new SbContainerNode(
                        new SbIf(exitFlag, true,
                            new SbDisableAot(interaction),
                            new SbCommentNode($"[action] ally travel to {{ {waitPoi} }}",
                                builder.Travel(ally, waitPoi, doorExit!, PlcDestKind.Run, overrideDestination: doorExit!.Position.Reverse())),
                            new SbMoveEntity(ally, REPosition.OutOfBounds),
                            new SbCommentNode($"[action] ally leave at {{ {doorExit} }}",
                                new SbDoor(doorExit)))))));

            SbProcedure? triggerProcedure = null;
            if (doorEntry != null)
            {
                triggerProcedure = new SbProcedure(
                    builder.CreateTrigger(doorEntry.Cuts),
                    new SbSetFlag(enterFlag),
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

            var nodeBuilder = new SbNodeBuilder();
            nodeBuilder.Append(
                new SbContainerNode(
                new SbAlly(ally, waitPoi!.Position),
                new SbAot(interaction, waitPoi.Position, 2000),
                new SbEnableEvent(interaction, eventProcedure)));
            if (doorEntry != null)
            {
                nodeBuilder.Reparent(x =>
                    new SbIf(enterFlag, false,
                        SbNode.Conditional(doorEntry != null, () => new SbContainerNode(
                            new SbAlly(ally, REPosition.OutOfBounds),
                            new SbAot(interaction, waitPoi.Position, 2000),
                            new SbFork(triggerProcedure!))))
                    .Else(x));
            }

            nodeBuilder.Reparent(x =>
                new SbContainerNode(
                    x,
                    new SbFork(loopProcedure),
                    subPlot.OnInit()));
            if (doorExit != null)
            {
                nodeBuilder.Reparent(x => new SbIf(exitFlag, false, x));
            }
            return new CsPlot(
                new SbProcedure(
                    new SbCommentNode($"[plot] ally wait {{ flag {enterFlag.Flag} }}",
                        nodeBuilder.Build())));
        }

        private class SubPlot
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public PlotBuilder PlotBuilder { get; set; }
            public CsPlayer Player { get; set; }
            public CsAlly Ally { get; set; }
            public int? Cut { get; set; }
            public CsFlag ExitFlag { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public ICsHero[] Participants => new ICsHero[] { Player, Ally };
            public virtual bool CanExit => true;
            public virtual bool MustExit => true;
            public virtual bool AlwaysExit => true;
            public virtual bool HasQuickConversation => true;

            public virtual SbNode OnInit() => new SbNop();

            public virtual SbNode OnConversation()
            {
                return
                    new SbLockPlot(
                        new SbIf(ExitFlag, true,
                            OnQuickConversation())
                        .Else(
                            OnFullConversation(),
                            SbNode.Conditional(AlwaysExit, () =>
                                new SbSetFlag(ExitFlag)),
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
                var msg = builder.AllocateMessage($"Will you help {Ally.DisplayName}?@");
                var msg2 = builder.AllocateMessage($"You helped {Ally.DisplayName}.");
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
                    var item = itemRando.GetRandomGift(rng, 1);
                    type = item.Type;
                    amount = item.Amount;
                }

                return builder.AllocateItem(new Item(type, (byte)amount));
            }
        }

        private class TradeSubPlot : SubPlot
        {
            public override bool CanExit => false;
            public override bool AlwaysExit => false;

            public override SbNode OnQuickConversation() => OnAfterChat();

            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var msg0 = builder.AllocateMessage("I will give you a {herb} for an {ink}.");
                var msg1 = builder.AllocateMessage("Will you give an {ink} for a {herb}?@");
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

        private class ChoiceSubPlot : SubPlot
        {
            public override bool CanExit => false;
            public override bool AlwaysExit => false;

            public override SbNode OnQuickConversation() => OnAfterChat();

            public override SbNode OnAfterChat()
            {
                var rng = PlotBuilder.Rng;
                var itemRando = PlotBuilder.ItemRandomiser!;
                var itemHelper = itemRando.ItemHelper;

                var pool = new List<Choice>();
                var limit = 0;
                while (pool.Count < 5)
                {
                    var item = itemRando.GetRandomGift(rng, 0);
                    if (limit < 1000 && pool.Any(x => IsItemSimilar(x.Item, item)))
                    {
                        limit++;
                    }
                    else
                    {
                        var itemName = itemHelper.GetFriendlyItemName(item.Type);
                        pool.Add(new Choice(item, itemName));
                    }
                }
                var msgText = new StringBuilder();
                msgText.Append("Which item would you like?\n");
                msgText.Append("1. Decide later#");
                for (var i = 0; i < pool.Count; i++)
                {
                    msgText.Append($"{i + 2}. {{{pool[i].Display}}}");
                    msgText.Append(i % 2 == 0 ? '\n' : '#');
                }
                msgText.Append("@02");

                var builder = PlotBuilder;
                var msg = builder.AllocateMessage(msgText.ToString(), autoBreak: false);
                var choices =
                    new SbNode[] { new SbNop() }.Concat(
                    Enumerable.Range(0, 5).Select(x =>
                        new SbContainerNode(
                            new SbSetFlag(ExitFlag),
                            pool[x].BuildChoiceNode(builder)))).ToArray();
                return
                    new SbCommentNode("[action] choice",
                        new SbMessage(msg, choices));
            }

            private static bool IsItemSimilar(Item a, Item b)
            {
                if (a.Type == b.Type)
                    return true;

                // For health items, check if any components are the same
                var aMask = GetGRB(a.Type);
                var bMask = GetGRB(b.Type);
                if ((aMask & bMask) != 0)
                    return true;

                return false;

                static int GetGRB(byte type)
                {
                    return type switch
                    {
                        Re2ItemIds.HerbG => 0b100,
                        Re2ItemIds.HerbGG => 0b100,
                        Re2ItemIds.HerbGGG => 0b100,
                        Re2ItemIds.HerbGR => 0b110,
                        Re2ItemIds.HerbGB => 0b100,
                        Re2ItemIds.HerbGGB => 0b101,
                        Re2ItemIds.HerbGRB => 0b111,
                        Re2ItemIds.HerbR => 0b010,
                        Re2ItemIds.HerbB => 0b001,
                        Re2ItemIds.FAidSpray => 0b110,
                        _ => 0b000,
                    };
                }
            }

            private class Choice
            {
                public Item Item { get; }
                public string ItemName { get; }
                public string Display => Item.Amount == 1 ? ItemName.TrimEnd('s') : $"{Item.Amount}x {ItemName}";

                public Choice(Item item, string itemName)
                {
                    Item = item;
                    ItemName = itemName;
                }

                public SbNode BuildChoiceNode(PlotBuilder builder)
                {
                    var msg = builder.AllocateMessage($"You received {Display}");
                    return new SbContainerNode(
                        new SbGetItem(Item),
                        new SbMessage(msg));
                }
            }
        }

        private class CurePoisonSubPlot : SubPlot
        {
            public override bool CanExit => false;
            public override bool MustExit => true;
            public override bool AlwaysExit => false;

            public override SbNode OnQuickConversation() => OnAfterChat();

            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var msg0 = builder.AllocateMessage("I can treat poison wounds.");
                var msg1 = builder.AllocateMessage("You poisoned wounds have been treated.");
                var use0 = builder.AllocateGlobalFlag();
                return
                    new SbIf(new SbCkPoison(),
                        new SbCommentNode("[action] heal player (poison)",
                            new SbIf(use0, true,
                                new SbSetFlag(ExitFlag))
                            .Else(
                                new SbSetFlag(use0)),
                            new SbHealPoison(),
                            new SbMessage(msg1)))
                    .Else(
                        new SbCommentNode("[action] message - poison",
                            new SbMessage(msg0)));
            }
        }

        private class CureSubPlot : SubPlot
        {
            public override bool CanExit => true;
            public override bool MustExit => true;
            public override bool AlwaysExit => false;

            public override SbNode OnQuickConversation() => OnAfterChat();

            public override SbNode OnAfterChat()
            {
                var builder = PlotBuilder;
                var msg0 = builder.AllocateMessage("Would you like me to treat your wounds?@");
                var msg1 = builder.AllocateMessage("Your wounds have been treated.");
                var use0 = builder.AllocateGlobalFlag();
                return
                    new SbCommentNode("[action] question - heal",
                        new SbMessage(msg0,
                            new SbCommentNode("[action] heal player",
                                new SbIf(use0, true,
                                    new SbSetFlag(ExitFlag))
                                .Else(
                                    new SbSetFlag(use0)),
                                new SbHeal(),
                                new SbMessage(msg1))));
            }
        }
    }
}
