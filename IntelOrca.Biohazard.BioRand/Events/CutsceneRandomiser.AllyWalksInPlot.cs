using System;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class AllyWalksInPlot : Plot
        {
            protected override bool Check()
            {
                return GetRandomPoi(x => x.HasTag(PoiKind.Meet)) != null;
            }

            protected override void Build()
            {
                var numAllys = Rng.Next(0, 3);
                numAllys = 3;
                var allyIds = Builder.AllocateEnemies(numAllys);
                var entranceDoors = Enumerable.Range(0, numAllys).Select(x => GetRandomDoor()!).ToArray();
                var exitDoors = Enumerable.Range(0, numAllys).Select(x => GetRandomDoor()!).ToArray();

                var meetup = GetRandomPoi(x => x.HasTag(PoiKind.Meet))!;
                var meetA = new REPosition(meetup.X + 1000, meetup.Y, meetup.Z, 2000);
                var meetB = new REPosition(meetup.X - 1000, meetup.Y, meetup.Z, 0);
                var meetC = new REPosition(meetup.X, meetup.Y, meetup.Z + 1000, 1000);
                var meetD = new REPosition(meetup.X, meetup.Y, meetup.Z - 1000, 3000);
                var allyMeets = new[] { meetB, meetC, meetD };

                Builder.IfPlotTriggered();
                Builder.ElseBeginTriggerThread();
                for (var i = 0; i < numAllys; i++)
                {
                    var enemyType = Cr._npcRandomiser!.GetRandomNpc(Cr._rdt!, Rng);
                    Builder.Ally(allyIds[i], enemyType, REPosition.OutOfBounds.WithY(entranceDoors[i].Position.Y));
                }

                var triggerCut = AddTriggers(entranceDoors[0].Cuts);
                if (triggerCut == null)
                    throw new Exception("Cutscene not supported for non-cut triggers.");

                Builder.LockPlot();

                // Mark the cutscene as done in case it softlocks
                Builder.SetFlag(Cr._plotFlag);

                DoDoorOpenCloseCut(entranceDoors[0]);
                Builder.BeginCutsceneMode();
                Builder.MoveEnemy(allyIds[0], entranceDoors[0].Position);
                if (Rng.NextProbability(50))
                {
                    Builder.PlayVoice(21);
                }
                Builder.Sleep(30);
                LogAction($"Ally walk in");

                Builder.CutRevert();

                IntelliTravelTo(24, -1, triggerCut, meetup, meetA, PlcDestKind.Run, cutFollow: true);
                IntelliTravelTo(25, allyIds[0], entranceDoors[0], meetup, allyMeets[0], PlcDestKind.Run);
                Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);
                Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 25);

                LogAction($"Focus on {{ {meetup} }}");
                var meetCut = meetup.CloseCut ?? meetup.Cut;
                Builder.CutChange(meetCut);
                LongConversation(new[] { 1, 2, 3, 4, 5 });

                for (var i = 1; i < numAllys; i++)
                {
                    // Another ally walks in
                    Builder.MoveEnemy(allyIds[i], entranceDoors[i].Position);
                    DoDoorOpenCloseCutAway(entranceDoors[i], meetCut);
                    Builder.Sleep(15);
                    LogAction($"NPC walk in");
                    IntelliTravelTo(24, allyIds[i], entranceDoors[i], meetup, allyMeets[i], PlcDestKind.Run, cutFollow: true);
                    Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);

                    LogAction($"Focus on {meetup}");
                    Builder.CutChange(meetCut);
                    LongConversation(new[] { 2, 4, 6, 7, 8 });
                }

                if (Rng.NextProbability(50))
                {
                    // Backstep
                    Builder.PlayVoiceAsync(Rng.Next(0, 15));
                    var backstepPos = new REPosition(meetup.X - 2500, meetup.Y, meetup.Z, 0);
                    Builder.SetEnemyDestination(allyIds[0], backstepPos, PlcDestKind.Backstep);
                    Builder.WaitForEnemyTravel(allyIds[0]);
                    Builder.Sleep(2);
                }

                IntelliTravelTo(24, allyIds[0], meetup, exitDoors[0], exitDoors[0].Position.Reverse(), PlcDestKind.Run);
                Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);

                Builder.MoveEnemy(allyIds[0], REPosition.OutOfBounds);
                Builder.StopEnemy(allyIds[0]);

                DoDoorOpenCloseCutAway(exitDoors[0], meetup.Cut);
                Builder.ReleaseEnemyControl(-1);
                Builder.CutAuto();
                Builder.EndCutsceneMode();
                Builder.UnlockPlot();
            }
        }
    }
}
