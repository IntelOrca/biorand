using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private abstract class Plot
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public CutsceneRandomiser Cr { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public CutsceneBuilder Builder => Cr._cb;
            public Rng Rng => Cr._plotRng;
            public RandoLogger Logger => Cr._logger;

            public bool IsCompatible() => Check();

            public void Create()
            {
                Cr._plotId = Builder.BeginPlot(Cr.GetNextFlag());

                Logger.WriteLine($"    [plot] #{Cr._plotId - 0x0300}: {GetType().Name}");
                Build();
                Builder.EndPlot();
                Cr._lastPlotId = Cr._plotId;
            }

            protected virtual bool Check()
            {
                return true;
            }

            protected virtual void Build()
            {
            }

            protected SceEmSetOpcode GenerateEnemy(int id, REPosition position)
            {
                var enemyRandomiser = Cr._enemyRandomiser!;
                var enemyHelper = enemyRandomiser.EnemyHelper;
                var type = Cr._enemyType!.Value;

                var opcode = new SceEmSetOpcode();
                opcode.Id = (byte)id;
                opcode.X = (short)position.X;
                opcode.Y = (short)position.Y;
                opcode.Z = (short)position.Z;
                opcode.D = (short)position.D;
                opcode.Floor = (byte)position.Floor;
                opcode.KillId = enemyRandomiser.GetNextKillId();
                opcode.Type = type;

                var enemySpec = new MapRoomEnemies();
                enemyHelper.SetEnemy(Cr._config, Rng, opcode, enemySpec, type);
                return opcode;
            }

            protected PointOfInterest? AddTriggers(
                int[]? notCuts = null,
                int minSleepTime = 0,
                int maxSleepTime = 20)
            {
                if (Cr._lastPlotId != -1)
                {
                    LogTrigger($"after plot {Cr._lastPlotId - 0x0300}");
                    Builder.WaitForPlot(Cr._lastPlotId);
                }

                var sleepTime = minSleepTime;
                var justSleep = false;
                if (notCuts == null || notCuts.Length == 0)
                {
                    if (Rng.NextProbability(50))
                    {
                        // Just a sleep trigger
                        sleepTime = Rng.Next(minSleepTime, maxSleepTime);
                        justSleep = true;
                    }
                }

                // Sleep trigger
                if (sleepTime != 0)
                {
                    Builder.Sleep(30 * sleepTime);
                    LogTrigger($"wait {sleepTime} seconds");
                }

                Builder.WaitForPlotUnlock();

                if (justSleep)
                    return null;

                // Random cut
                var triggerPoi = GetRandomPoi(x => x.HasTag(PoiKind.Trigger) && notCuts?.Contains(x.Cut) != true);
                if (triggerPoi == null)
                    throw new Exception("Unable to find cut trigger");

                LogTrigger($"cut {triggerPoi.Cut}");
                Builder.WaitForTriggerCut(triggerPoi.Cut);
                return triggerPoi;
            }

            protected void DoDoorOpenCloseCutAway(PointOfInterest door, int currentCut)
            {
                var cuts = door.AllCuts;
                var needsCut = cuts.Contains(currentCut) == true;
                if (needsCut)
                {
                    var cut = Cr._allKnownCuts.Except(cuts).Shuffle(Rng).FirstOrDefault();
                    Builder.CutChange(cut);
                    LogAction($"door away cut {cut}");
                }
                else
                {
                    LogAction($"door");
                }
                DoDoorOpenClose(door);
                Builder.CutRevert();
            }

            protected void DoDoorOpenClose(PointOfInterest door)
            {
                var pos = door.Position;
                if (door.HasTag("door"))
                {
                    Builder.PlayDoorSoundOpen(pos);
                    Builder.Sleep(30);
                    Builder.PlayDoorSoundClose(pos);
                }
                else
                {
                    Builder.Sleep(30);
                }
            }

            protected void DoDoorOpenCloseCut(PointOfInterest door)
            {
                DoDoorOpenClose(door);
                Builder.CutChange(door.Cut);
                LogAction($"door cut {door.Cut}");
            }

            protected void IntelliTravelTo(int flag, int enemyId, PointOfInterest from, PointOfInterest destination, REPosition? overrideDestination, PlcDestKind kind, bool cutFollow = false)
            {
                Builder.SetFlag(CutsceneBuilder.FG_ROOM, flag, false);
                Builder.BeginSubProcedure();
                var route = GetTravelRoute(from, destination);
                foreach (var poi in route)
                {
                    if (overrideDestination != null && poi == destination)
                        break;

                    Builder.SetEnemyDestination(enemyId, poi.Position, kind);
                    Builder.WaitForEnemyTravel(enemyId);
                    Builder.Sleep(2);
                    if (cutFollow)
                        Builder.CutChange(poi.Cut);
                    LogAction($"{GetCharLogName(enemyId)} travel to {{ {poi} }}");
                }
                if (overrideDestination != null)
                {
                    Builder.SetEnemyDestination(enemyId, overrideDestination.Value, kind);
                    Builder.WaitForEnemyTravel(enemyId);
                    Builder.MoveEnemy(enemyId, overrideDestination.Value);
                    LogAction($"{GetCharLogName(enemyId)} travel to {overrideDestination}");
                }
                else
                {
                    Builder.MoveEnemy(enemyId, destination.Position);
                }
                Builder.SetFlag(CutsceneBuilder.FG_ROOM, flag);
                var subName = Builder.EndSubProcedure();
                Builder.CallThread(subName);
            }

            private PointOfInterest[] GetTravelRoute(PointOfInterest from, PointOfInterest destination)
            {
                var prev = new Dictionary<PointOfInterest, PointOfInterest>();
                var q = new Queue<PointOfInterest>();
                q.Enqueue(from);

                var found = false;
                while (!found && q.Count != 0)
                {
                    var curr = q.Dequeue();
                    var edges = GetEdges(curr);
                    foreach (var edge in edges)
                    {
                        if (!prev.ContainsKey(edge))
                        {
                            prev[edge] = curr;
                            if (edge == destination)
                            {
                                found = true;
                                break;
                            }
                            q.Enqueue(edge);
                        }
                    }
                }

                if (!found)
                {
                    // throw new Exception("Failed to find POI route from source to destination.");
                    return new[] { destination };
                }

                var route = new List<PointOfInterest>();
                var poi = destination;
                while (poi != from)
                {
                    route.Add(poi);
                    poi = prev[poi];
                }
                return ((IEnumerable<PointOfInterest>)route).Reverse().ToArray();
            }

            private PointOfInterest[] GetEdges(PointOfInterest poi)
            {
                var edges = poi.Edges;
                if (edges == null)
                    return new PointOfInterest[0];

                return edges
                    .Select(x => FindPoi(x))
                    .Where(x => x != null)
                    .Select(x => x!)
                    .ToArray();
            }

            protected void LongConversation(int[] vids)
            {
                BeginConversation();
                Converse(vids);
                EndConversation();
            }

            protected void BeginConversation()
            {
                Builder.FadeOutMusic();
                Builder.Sleep(60);
            }

            protected void Converse(int[] vids)
            {
                for (int i = 0; i < vids.Length; i++)
                {
                    Builder.PlayVoice(vids[i]);
                    Builder.Sleep(15);
                }
            }

            protected void EndConversation()
            {
                Builder.Sleep(60);
                Builder.ResumeMusic();
                LogAction($"conversation");
            }

            protected PointOfInterest? GetRandomDoor()
            {
                return GetRandomPoi(x => x.HasTag(PoiKind.Door) || x.HasTag(PoiKind.Stairs));
            }

            protected PointOfInterest? GetRandomPoi(Predicate<PointOfInterest> predicate)
            {
                return Cr._poi
                    .Where(x => predicate(x))
                    .Shuffle(Rng)
                    .FirstOrDefault();
            }

            private PointOfInterest? FindPoi(int id)
            {
                return Cr._poi.FirstOrDefault(x => x.Id == id);
            }

            private string GetCharLogName(int enemyId)
            {
                return enemyId == -1 ? "player" : $"npc {enemyId}";
            }

            protected void LogTrigger(string s) => Logger.WriteLine($"      [trigger] {s}");
            protected void LogAction(string s) => Logger.WriteLine($"      [action] {s}");
        }
    }
}
