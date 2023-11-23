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
                    IntelliTravelTo(24, npcId, doorEntry, waitPoi, null, PlcDestKind.Run);
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
                    IntelliTravelTo(24, npcId, waitPoi, doorExit, null, PlcDestKind.Run);
                    Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);
                    Builder.MoveEnemy(npcId, REPosition.OutOfBounds);
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
                    RandomItem(255, itemId.Value);
                    Builder.SetFlag(CutsceneBuilder.FG_ITEM, 255, false);
                }

                if (doorExit != null)
                {
                    Builder.EndIf();
                }
            }

            private void RandomItem(int globalId, int aotId)
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

                Builder.Item(globalId, aotId, type, (byte)amount);
                LogAction($"item gift [{itemHelper.GetItemName(type)} x{amount}]");
            }
        }
    }
}
