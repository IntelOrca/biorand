using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class AllyStaticPlot : Plot
        {
            protected override bool Check()
            {
                return GetRandomPoi(x => x.HasTag(PoiKind.Npc)) != null;
            }

            private void Build1()
            {
                var previousEnemies = Builder.PlacedEnemyIds.ToArray();

                // Can the NPC enter the room via a door
                var graphs = GetGraphsContaining(PoiKind.Door, PoiKind.Npc).Shuffle(Rng);
                var graph = graphs.FirstOrDefault();
                var waitPoi = null as PointOfInterest;
                var doorEntry = null as PointOfInterest;
                var doorExit = null as PointOfInterest;
                if (graph != null)
                {
                    doorEntry = graph.Shuffle(Rng).FirstOrDefault(x => x.HasTag(PoiKind.Door));
                    doorExit = graph.Shuffle(Rng).FirstOrDefault(x => x.HasTag(PoiKind.Door));
                    waitPoi = graph.Shuffle(Rng).FirstOrDefault(x => x.HasTag(PoiKind.Npc));

                    if (Rng.NextProbability(50))
                        doorEntry = null;
                    if (Rng.NextProbability(50))
                        doorExit = null;
                }
                else
                {
                    waitPoi = GetRandomPoi(x => x.HasTag(PoiKind.Npc));
                }

                var plotFlag = Cr._plotFlag;
                var converseFlag = Cr.GetNextFlag();

                var npcId = Builder.AllocateEnemies(1).FirstOrDefault();
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

                // Trigger thread
                Builder.BeginSubProcedure();
                if (doorEntry != null)
                {
                    // Triggers
                    AddTriggers(doorEntry.Cuts);

                    Builder.SetFlag(plotFlag);

                    // Move ally into position and cut to them
                    Builder.LockPlot();
                    DoDoorOpenCloseCut(doorEntry);
                    Builder.BeginCutsceneMode();
                    LockEnemies(previousEnemies);
                    Builder.MoveEnemy(npcId, doorEntry.Position);
                    Builder.Sleep(60);
                    UnlockEnemies(previousEnemies);
                    Builder.CutRevert();
                    Builder.EndCutsceneMode();
                    IntelliTravelTo(24, npcId, doorEntry, waitPoi!, null, PlcDestKind.Run);
                    Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);
                    Builder.EnableEnemyCollision(npcId);

                    Builder.Sleep(30 * 4);
                    Builder.UnlockPlot();
                }
                var triggerThread = Builder.EndSubProcedure();

                // Continous loop
                Builder.BeginSubProcedure();
                var repeatLabel = Builder.CreateLabel();
                Builder.AppendLabel(repeatLabel);
                Builder.SetEnemyNeck(npcId, 64);
                Builder.Sleep(30);
                Builder.Goto(repeatLabel);
                var loopThread = Builder.EndSubProcedure();

                // Event
                Builder.BeginSubProcedure();
                Builder.BeginCutsceneMode();
                Builder.BeginIf();
                Builder.CheckFlag(converseFlag);
                Converse(vIds0);
                Builder.Else();
                if (waitPoi!.CloseCut != null)
                    Builder.CutChange(waitPoi.CloseCut.Value);
                BeginConversation();
                Converse(vIds1.Take(vIds1.Length - 1).ToArray());
                if (itemId != null)
                {
                    Builder.AotOn(itemId.Value);
                }
                Converse(vIds1.Skip(vIds1.Length - 1).ToArray());
                EndConversation();
                if (waitPoi.CloseCut != null)
                    Builder.CutRevert();
                Builder.SetFlag(converseFlag);
                Builder.EndIf();

                if (doorExit != null)
                {
                    Builder.DisableEnemyCollision(npcId);
                    IntelliTravelTo(24, npcId, waitPoi, doorExit, doorExit.Position.Reverse(), PlcDestKind.Run);
                    Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);
                    Builder.MoveEnemy(npcId, REPosition.OutOfBounds);
                    DoDoorOpenClose(doorExit);
                }

                Builder.EndCutsceneMode();
                var eventProc = Builder.EndSubProcedure();

                // Init
                if (doorExit != null)
                {
                    Builder.BeginIf();
                    Builder.CheckFlag(converseFlag, false);
                }
                if (doorEntry == null)
                {
                    Builder.Ally(npcId, enemyType, waitPoi!.Position);
                }
                else
                {
                    Builder.BeginIf();
                    Builder.CheckFlag(plotFlag);
                    Builder.Ally(npcId, enemyType, waitPoi!.Position);
                    Builder.Else();
                    Builder.Ally(npcId, enemyType, REPosition.OutOfBounds);
                    Builder.DisableEnemyCollision(npcId);
                    Builder.EndIf();
                }
                Builder.CallThread(triggerThread);
                Builder.CallThread(loopThread);

                Builder.Event(interactId, waitPoi.Position, 2000, eventProc);
                if (itemId != null)
                {
                    // GetRandomItem(255, itemId.Value);
                    Builder.SetFlag(CutsceneBuilder.FG_ITEM, 255, false);
                }

                if (doorExit != null)
                {
                    Builder.EndIf();
                }
            }

            protected override void Build()
            {
                var previousEnemies = Builder.PlacedEnemyIds.ToArray();

                // Can the NPC enter the room via a door
                var graphs = GetGraphsContaining(PoiKind.Door, PoiKind.Npc).Shuffle(Rng);
                var graph = graphs.FirstOrDefault();
                var waitPoi = null as PointOfInterest;
                var doorEntry = null as PointOfInterest;
                var doorExit = null as PointOfInterest;
                if (graph != null)
                {
                    doorEntry = graph.Shuffle(Rng).FirstOrDefault(x => x.HasTag(PoiKind.Door));
                    doorExit = graph.Shuffle(Rng).FirstOrDefault(x => x.HasTag(PoiKind.Door));
                    waitPoi = graph.Shuffle(Rng).FirstOrDefault(x => x.HasTag(PoiKind.Npc));

                    if (Rng.NextProbability(50))
                        doorEntry = null;
                    if (Rng.NextProbability(50))
                        doorExit = null;
                }
                else
                {
                    waitPoi = GetRandomPoi(x => x.HasTag(PoiKind.Npc));
                }

                var plotFlag = new CsGlobalFlag();
                var converseFlag = new CsGlobalFlag();

                var player = CsPlayer.Default;
                var ally = new CsAlly();
                var item = Rng.NextProbability(75) ? GetRandomItem() : null;
                var interaction = new CsAot();

                SbProcedure? triggerProcedure = null;
                if (doorEntry != null)
                {
                    triggerProcedure = new SbProcedure(
                        // Triggers
                        new SbSleep(30),
                        new SbWaitForCut(0),
                        new SbWaitForFlag(new ReFlag(CutsceneBuilder.FG_ROOM, 23), false),
                        new SbWaitForFlag(new ReFlag(CutsceneBuilder.FG_STATUS, 27), false),
                        new SbWaitForFlag(new ReFlag(CutsceneBuilder.FG_STOP, 7), false),

                        new SbSetFlag(plotFlag),
                            new SbLockPlot(
                                new SbDoor(doorEntry),
                                new SbCutsceneBars(
                                    new SbFreezeEnemies(previousEnemies,
                                        new SbMoveEntity(ally, doorEntry.Position),
                                        new SbSleep(60)),
                                    new SbCutRevert(),
                                    new SbTravel(ally, doorEntry, waitPoi!, PlcDestKind.Run),
                                    new SbSleep(30 * 4))));
                }

                var loopProcedure = new SbProcedure(
                    new SbLoop(
                        new SbSetEntityNeck(ally, 64),
                        new SbSleep(30)));

                var eventProcedure = new SbProcedure(
                    new SbCutsceneBars(
                        new SbIf(converseFlag, true,
                            new SbXaOn(0))
                        .Else(
                            CreateInitalConversation(waitPoi!.CloseCut, new ICsHero[] { player, ally }, item),
                            new SbSetFlag(converseFlag)),
                        new SbConditionalNode(doorExit != null,
                            new SbSetEntityCollision(ally, false),
                            new SbTravel(ally, waitPoi, doorExit!, PlcDestKind.Run, overrideDestination: doorExit!.Position.Reverse()),
                            new SbMoveEntity(ally, REPosition.OutOfBounds),
                            new SbDoor(doorExit))));

                var initProcedure = new SbProcedure(
                    new SbIf(converseFlag, true,
                        new SbAlly(ally, waitPoi!.Position),
                        new SbConditionalNode(triggerProcedure != null,
                            new SbFork(triggerProcedure!)),
                        new SbFork(loopProcedure),
                        new SbEvent(interaction, waitPoi.Position, 2000, eventProcedure),
                        new SbConditionalNode(item != null, new SbItem(item!)),
                        new SbSetFlag(new CsFlag(CutsceneBuilder.FG_ITEM, 255), false)));
            }

            private SbNode CreateInitalConversation(int? cut, ICsHero[] participants, CsItem? item)
            {
                SbNode result = new SbMuteMusic(
                    new SbSleep(60),
                    RandomConversation(2, 4, participants, participants),
                    new SbConditionalNode(item != null,
                        new SbAotOn(item!)),
                    RandomConversation(0, 2, participants, participants),
                    new SbSleep(60));
                if (cut != null)
                {
                    result = new SbCut(cut.Value, result);
                }
                return result;
            }

            private SbContainerNode RandomConversation(int minLines, int maxLines, ICsHero[] participants, ICsHero[] speakers)
            {
                var count = Rng.Next(minLines, maxLines + 1);
                var nodes = new List<SbNode>();
                for (var i = 0; i < count; i++)
                {
                    var speaker = Rng.NextOf(speakers);
                    nodes.Add(new SbVoice(speaker, participants));
                }
                return new SbContainerNode(nodes.ToArray());
            }

            private CsItem GetRandomItem()
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

                return new CsItem(new Item(type, (byte)amount))
                {
                    GlobalId = 255
                };
            }
        }
    }
}
