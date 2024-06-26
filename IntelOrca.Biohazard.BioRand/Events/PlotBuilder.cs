﻿using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.Room;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class PlotBuilder
    {
        private readonly EnemyRandomiser? _enemyRandomiser;
        private readonly NPCRandomiser? _npcRandomiser;
        private readonly VoiceRandomiser? _voiceRandomiser;
        private readonly RandomizedRdt _rdt;
        private readonly EndlessBag<REPosition> _enemyPositions;
        private readonly EndlessBag<ReFlag> _availableGlobalFlags = new EndlessBag<ReFlag>();
        private readonly Queue<ReFlag> _availableLocalFlags = new Queue<ReFlag>();
        private readonly Queue<byte> _availableEntityIds = new Queue<byte>();
        private readonly Queue<byte> _availableAotIds = new Queue<byte>();
        private readonly List<CsEnemy> _enemies = new List<CsEnemy>();
        private readonly ScdCondition? _enemyCondition;
        private CsPlayer? _player;

        public RandoConfig Config { get; }
        public Rng Rng { get; }
        public ItemRandomiser? ItemRandomiser { get; }
        public PoiGraph PoiGraph { get; }
        public int CurrentEnemyCount { get; private set; }
        public int MaximumEnemyCount { get; }
        public IEnemyHelper EnemyHelper => _enemyRandomiser?.EnemyHelper ?? new Re2EnemyHelper();
        public byte? EnemyType { get; }
        public RdtId RdtId => _rdt.RdtId;
        public ReFlag PartnerJoinFlag { get; }

        public PlotBuilder(
            RandoConfig config,
            Rng rng,
            ItemRandomiser? itemRandomiser,
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
            ScdCondition? enemyCondition,
            int maximumEnemyCount,
            ReFlag partnerJoinFlag)
        {
            Config = config;
            Rng = rng;
            _rdt = rdt;
            PoiGraph = poiGraph;
            _enemyPositions = enemyPositions;
            ItemRandomiser = itemRandomiser;
            _enemyRandomiser = enemyRandomiser;
            _npcRandomiser = npcRandomiser;
            _voiceRandomiser = voiceRandomiser;
            _availableGlobalFlags = globalFlags;
            _availableLocalFlags = localFlags.ToQueue();
            _availableEntityIds = entityIds.ToQueue();
            _availableAotIds = aotIds.ToQueue();
            EnemyType = enemyType;
            _enemyCondition = enemyCondition;
            MaximumEnemyCount = maximumEnemyCount;
            PartnerJoinFlag = partnerJoinFlag;
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

        public CsFlag GetPlotLockFlag()
        {
            return new CsFlag(new ReFlag(CutsceneBuilder.FG_ROOM, 23));
        }

        public CsPlayer GetPlayer()
        {
            if (_player == null)
            {
                var actor = _npcRandomiser?.PlayerActor ?? "leon";
                _player = new CsPlayer(actor);
            }
            return _player;
        }

        public CsEnemy AllocateEnemy(byte? type = null, bool hasGlobalId = true)
        {
            var id = _availableEntityIds.Dequeue();
            if (id == 255)
                throw new InvalidOperationException();

            var globalId = (byte)(hasGlobalId ? _enemyRandomiser?.GetNextKillId() ?? 255 : 255);
            type ??= EnemyType ?? Re2EnemyIds.ZombieRandom;
            var position = _enemyPositions.Next();
            var enemyHelper = _enemyRandomiser?.EnemyHelper ?? new Re2EnemyHelper();
            var result = new CsEnemy(id, globalId, type.Value, position,
                opcode => enemyHelper.SetEnemy(Config, Rng, opcode, new MapRoomEnemies(), opcode.Type));
            _enemies.Add(result);
            return result;
        }

        public CsAlly AllocateAlly()
        {
            var enemyType = Re2EnemyIds.ClaireRedfield;
            var actor = "claire";
            if (_npcRandomiser != null)
            {
                // HACK partner does not work if another partner type is in room
                var excludePartnerTypes = !_availableEntityIds.Contains(255);
                for (var i = 0; i < 1000; i++)
                {
                    var suggestedEnemyType = _npcRandomiser.GetRandomNpc(_rdt, Rng);
                    if (excludePartnerTypes)
                    {
                        if (suggestedEnemyType != Re2EnemyIds.AdaWong1 &&
                            suggestedEnemyType != Re2EnemyIds.AdaWong2 &&
                            suggestedEnemyType != Re2EnemyIds.SherryWithPendant &&
                            suggestedEnemyType != Re2EnemyIds.SherryWithClairesJacket)
                        {
                            enemyType = suggestedEnemyType;
                            break;
                        }
                    }
                    else
                    {
                        enemyType = suggestedEnemyType;
                        break;
                    }
                }
                actor = _npcRandomiser.GetActor(enemyType) ?? actor;
            }
            var id = _availableEntityIds.Dequeue();
            if (id == 255)
                throw new InvalidOperationException();
            return new CsAlly(id, enemyType, actor);
        }

        public CsAlly? AllocatePartner()
        {
            var enemyType = Re2EnemyIds.AdaWong1;
            var actor = "ada";
            if (_npcRandomiser != null)
            {
                actor = _npcRandomiser.GetActor(enemyType) ?? actor;
            }
            if (_availableEntityIds.Contains(255))
            {
                // HACK remove 255 from queue
                var newQueue = _availableEntityIds
                    .Except(new byte[] { 255 })
                    .ToArray();
                _availableEntityIds.Clear();
                foreach (var x in newQueue)
                    _availableEntityIds.Enqueue(x);
                return new CsAlly(255, enemyType, actor);
            }
            return null;
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

        internal CsMessage AllocateMessage(string message, bool autoBreak = true)
        {
            var builder = ((Rdt2)_rdt.RdtFile).ToBuilder();

            var en = builder.MSGEN.ToBuilder();
            var id = en.Messages.Count;
            en.Messages.Add(message.ToMsg(MsgLanguage.English, BioVersion.Biohazard2, autoBreak));
            builder.MSGEN = en.ToMsgList();

            var ja = builder.MSGJA.ToBuilder();
            en.Messages.Add(message.ToMsg(MsgLanguage.Japanese, BioVersion.Biohazard2, autoBreak));
            builder.MSGJA = ja.ToMsgList();

            _rdt.RdtFile = builder.ToRdt();

            return new CsMessage(id, message);
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
            var max2 = Math.Min(max, Math.Max(min, MaximumEnemyCount - CurrentEnemyCount));
            var count = Rng.Next(min, max2 + 1);
            CurrentEnemyCount += count;
            return count;
        }

        public SbNode SoundEffect(string? kind = null)
        {
            if (_voiceRandomiser == null)
            {
                return new SbNop();
            }
            else
            {
                var participantsActors = new string[0];
                var speakerActors = new string[0];
                var result = _voiceRandomiser.AllocateConversation(Rng, _rdt.RdtId, 1, speakerActors, participantsActors, kind);
                return new SbContainerNode(
                    new SbVoice(result[0], true));
            }
        }

        public SbNode Voice(ICsHero[] participants, string? kind = null) => Voice(participants, participants, kind);
        public SbNode Voice(ICsHero[] participants, ICsHero[] speakers, string? kind = null, bool trailingSilence = true)
        {
            if (_voiceRandomiser == null)
            {
                return new SbNop();
            }
            else
            {
                var participantsActors = participants.Select(x => x.Actor).ToArray();
                var speakerActors = speakers.Select(x => x.Actor).ToArray();
                var result = _voiceRandomiser.AllocateConversation(Rng, _rdt.RdtId, 1, speakerActors, participantsActors, kind);
                return new SbContainerNode(
                    new SbVoice(result[0]),
                    SbNode.Conditional(trailingSilence,
                        () => new SbSleep(Rng.Next(5, 30))));
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
            bool cutFollow = false,
            bool strict = true,
            CsFlag? completeFlag = null,
            CsFlag? subCompleteFlag = null)
        {
            completeFlag ??= AllocateLocalFlag();
            subCompleteFlag ??= AllocateLocalFlag();
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
                nodes.Add(new SbSetFlag(subCompleteFlag, false));
                nodes.Add(new SbEntityTravel(entity, subCompleteFlag.Flag, overrideDestination.Value, kind));
                nodes.Add(new SbWaitForFlag(subCompleteFlag.Flag));
                if (strict)
                {
                    nodes.Add(new SbMoveEntity(entity, overrideDestination.Value));
                }
            }
            else
            {
                if (strict)
                {
                    nodes.Add(new SbMoveEntity(entity, destination.Position));
                }
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
            int minSleepTime = 3,
            int maxSleepTime = 20,
            bool allowCutTrigger = true)
        {
            int? triggerTime = null;
            int? triggerCut = null;

            // Use a trigger cut if we can't show event in all cuts
            if (allowCutTrigger && (notCuts?.Any() == true || Rng.NextProbability(75)))
            {
                var triggerPoi = PoiGraph.GetRandomPoi(Rng, x => x.HasTag(PoiKind.Trigger) && notCuts?.Contains(x.Cut) != true);
                if (triggerPoi != null)
                {
                    triggerCut = triggerPoi.Cut;
                }
            }

            if (triggerCut == null || Rng.NextProbability(50))
            {
                triggerTime = Rng.Next(minSleepTime, maxSleepTime) * 30;
            }
            else
            {
                triggerTime = minSleepTime * 30;
            }

            var result = new List<SbNode>();
            if (triggerTime != null && triggerTime != 0)
            {
                result.Add(new SbCommentNode($"[trigger] wait {triggerTime / 30} seconds",
                    new SbSleep(triggerTime.Value)));
            }

            var conditions = new List<ISbCondition>();
            if (triggerCut != null)
            {
                result.Add(new SbCommentNode($"[trigger] wait for cut to be {triggerCut.Value}"));
                conditions.Add(new SbCmpCut(triggerCut.Value, false));
            }

            conditions.Add(new SbCk(new ReFlag(CutsceneBuilder.FG_STATUS, 27), true));
            conditions.Add(new SbCk(new ReFlag(CutsceneBuilder.FG_STOP, 7), true));
            conditions.Add(new SbCk(GetPlotLockFlag().Flag, true));
            if (notCuts?.Any() == true)
            {
                foreach (var cut in notCuts)
                {
                    conditions.Add(new SbCmpCut(cut));
                }
            }

            var enemyCondition = GetEnemyWaitCondition();
            if (enemyCondition != null)
            {
                result.Add(
                    new SbCommentNode($"[trigger] wait for {_enemyCondition!.Expression}",
                        new SbWaitUnlessAny(enemyCondition)));
            }
            result.Add(new SbWaitIfAny(conditions.ToArray()));

            return new SbContainerNode(result.ToArray());
        }

        public SbNode CreateEnemyConditionGuard(params SbNode[] children)
        {
            var enemyCondition = GetEnemyWaitCondition();
            if (enemyCondition != null)
            {
                return new SbCommentNode($"[trigger] wait for {_enemyCondition!.Expression}",
                    new SbIf(enemyCondition, children));
            }
            return new SbContainerNode(children);
        }

        public ISbCondition? GetEnemyWaitCondition()
        {
            if (_enemyCondition == null)
                return null;

            return Get(_enemyCondition.Expression);

            static ISbCondition Get(ScdCondition.IExpression expr)
            {
                switch (expr)
                {
                    case ScdCondition.Flag flagExpr:
                        return new SbCk(new ReFlag(flagExpr.Group, (byte)flagExpr.Index), true);
                    case ScdCondition.Variable varExpr:
                        return new SbCmp(varExpr.Index, varExpr.Value, true);
                    case ScdCondition.And andExpr:
                        return new SbAnd(Get(andExpr.Left), Get(andExpr.Right));
                    case ScdCondition.Negated nExpr:
                    {
                        if (nExpr.Child is ScdCondition.Flag flagExpr)
                        {
                            return new SbCk(new ReFlag(flagExpr.Group, (byte)flagExpr.Index), false);
                        }
                        else if (nExpr.Child is ScdCondition.Variable varExpr)
                        {
                            return new SbCmp(varExpr.Index, varExpr.Value, false);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public void BuildHelpers(CutsceneBuilder builder)
        {
            var fgEnemy = RdtId.Stage >= 5 ? "FG_ENEMY_2" : "FG_ENEMY";

            builder.BeginProcedure("freeze_all_enemies");
            foreach (var enemy in _enemies)
            {
                var i = enemy.Id;
                builder.WorkOnEnemy(i);
                builder.BeginIf();
                builder.AppendLine("ck", fgEnemy, enemy.GlobalId, 0);
                builder.AppendLine("member_cmp", 0, "M_X_POS", "CMP_NE", 0, 0x83);
                builder.DisableEnemyCollision(i);
                builder.HideEnemy(i);
                builder.DeactivateEnemy(i);
                builder.EndIf();
            }
            builder.EndProcedure();

            builder.BeginProcedure("unfreeze_all_enemies");
            foreach (var enemy in _enemies)
            {
                var i = enemy.Id;
                builder.WorkOnEnemy(i);
                builder.BeginIf();
                builder.AppendLine("ck", fgEnemy, enemy.GlobalId, 0);
                builder.AppendLine("member_cmp", 0, "M_X_POS", "CMP_NE", 0, 0x83);
                builder.EnableEnemyCollision(i);
                builder.UnhideEnemy(i);
                builder.ActivateEnemy(i);
                builder.EndIf();
            }
            builder.EndProcedure();

            builder.BeginProcedure("get_msg_answer");
            builder.AppendLine("save", "V_TEMP", 0);
            builder.BeginIf();
            builder.AppendLine("ck", "FG_MESSAGE", 29, 1);
            builder.AppendLine("calc", 0, "OP_OR", "V_TEMP", 4);
            builder.EndIf();
            builder.BeginIf();
            builder.AppendLine("ck", "FG_MESSAGE", 30, 1);
            builder.AppendLine("calc", 0, "OP_OR", "V_TEMP", 2);
            builder.EndIf();
            builder.BeginIf();
            builder.AppendLine("ck", "FG_MESSAGE", 31, 1);
            builder.AppendLine("calc", 0, "OP_OR", "V_TEMP", 1);
            builder.EndIf();
            builder.EndProcedure();
        }
    }
}
