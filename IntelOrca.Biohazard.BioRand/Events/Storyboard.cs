using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class CsEntity
    {
        public int? Id { get; set; }
    }

    internal class CsEnemy : CsEntity
    {
    }

    internal interface ICsHero
    {
        string? Actor { get; set; }
    }

    internal class CsAlly : CsEntity, ICsHero
    {
        public string? Actor { get; set; }
    }

    internal class CsPlayer : ICsHero
    {
        public static CsPlayer Default { get; } = new CsPlayer();

        public string? Actor { get; set; }
    }

    internal class CsFlag
    {
        public ReFlag? Flag { get; set; }

        public CsFlag() { }
        public CsFlag(byte type, byte index)
        {
            Flag = new ReFlag(type, index);
        }
    }

    internal class CsLocalFlag : CsFlag
    {
    }

    internal class CsGlobalFlag : CsFlag
    {
    }

    internal class CsAot
    {
        public int? Id { get; set; }
    }

    internal class CsItem : CsAot
    {
        public int? GlobalId { get; set; }
        public Item Item { get; set; }

        public CsItem(Item item)
        {
            Item = item;
        }
    }

    internal abstract class SbNode
    {
        public virtual IEnumerable<SbNode> Children => Enumerable.Empty<SbNode>();
        public virtual void Build(CutsceneBuilder builder)
        {
            foreach (var child in Children)
            {
                child.Build(builder);
            }
        }
    }

    internal class SbNop : SbNode
    {
    }

    internal class SbContainerNode : SbNode
    {
        private SbNode[] _children;

        public SbContainerNode(params SbNode[] children)
        {
            _children = children;
        }

        public override IEnumerable<SbNode> Children => _children;
    }

    internal class SbConditionalNode : SbContainerNode
    {
        public SbConditionalNode(bool include, params SbNode[] children)
            : base(include ? children : new SbNode[0])
        {
        }
    }

    internal class SbSleep : SbNode
    {
        public int Ticks { get; }

        public SbSleep(int ticks)
        {
            Ticks = ticks;
        }
    }

    internal class SbCut : SbContainerNode
    {
        public int Cut { get; }

        public SbCut(int cut, params SbNode[] children)
            : base(children)
        {
            Cut = cut;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.CutChange(Cut);
            base.Build(builder);
            builder.CutRevert();
        }
    }

    internal class SbCutRevert : SbNode
    {
        public override void Build(CutsceneBuilder builder)
        {
            builder.CutRevert();
        }
    }

    internal class SbMuteMusic : SbContainerNode
    {
        public SbMuteMusic(params SbNode[] children)
            : base(children)
        {
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.FadeOutMusic();
            base.Build(builder);
            builder.ResumeMusic();
        }
    }

    internal class SbAotOn : SbNode
    {
        public CsAot Aot { get; }

        public SbAotOn(CsAot aot)
        {
            Aot = aot;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (Aot.Id is int id)
            {
                builder.AotOn(id);
            }
        }
    }

    internal class SbDoor : SbNode
    {
        public PointOfInterest Poi { get; }

        public SbDoor(PointOfInterest poi)
        {
            Poi = poi;
        }
    }

    internal class SbTravel : SbNode
    {
        public CsEntity Entity { get; }
        public PointOfInterest From { get; }
        public PointOfInterest Destination { get; }
        public REPosition? OverrideDestination { get; }
        public PlcDestKind Kind { get; }
        public bool CutFollow { get; }

        public SbTravel(
            CsEntity entity,
            PointOfInterest from,
            PointOfInterest destination,
            PlcDestKind kind,
            REPosition? overrideDestination = null,
            bool cutFollow = false)
        {
            Entity = entity;
            From = from;
            Destination = destination;
            OverrideDestination = overrideDestination;
            Kind = kind;
            CutFollow = cutFollow;
        }

        public override void Build(CutsceneBuilder builder)
        {
        }
    }

    internal class SbLockPlot : SbContainerNode
    {
        public SbLockPlot(params SbNode[] children)
            : base(children)
        {
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.SetFlag(CutsceneBuilder.FG_ROOM, 23);
            base.Build(builder);
            builder.SetFlag(CutsceneBuilder.FG_ROOM, 23, false);
        }
    }

    internal class SbCutsceneBars : SbContainerNode
    {
        public SbCutsceneBars(params SbNode[] children)
            : base(children)
        {
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.SetFlag(CutsceneBuilder.FG_STATUS, 27, true);
            builder.SetFlag(CutsceneBuilder.FG_STOP, 7, true);
            base.Build(builder);
            builder.SetFlag(CutsceneBuilder.FG_STOP, 7, false);
            builder.SetFlag(CutsceneBuilder.FG_STATUS, 27, false);
        }
    }

    internal class SbAlly : SbNode
    {
        public CsAlly Ally { get; }

        public SbAlly(CsAlly ally, REPosition position)
        {
            Ally = ally;
        }

        public override void Build(CutsceneBuilder builder)
        {
        }
    }

    internal class SbEnemy : SbNode
    {
        public SceEmSetOpcode Opcode { get; }

        public SbEnemy(SceEmSetOpcode opcode)
        {
            Opcode = opcode;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.Enemy(Opcode);
        }
    }

    internal class SbMoveEntity : SbNode
    {
        public CsEntity Entity { get; }
        public REPosition Position { get; }

        public SbMoveEntity(CsEntity entity, REPosition position)
        {
            Entity = entity;
            Position = position;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (Entity.Id is int id)
            {
                builder.MoveEnemy(id, Position);
            }
        }
    }

    internal class SbSetEntityCollision : SbNode
    {
        public CsEntity Entity { get; }
        public bool Value { get; }

        public SbSetEntityCollision(CsEntity entity, bool value)
        {
            Entity = entity;
            Value = value;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (Entity.Id is int id)
            {
                if (Value)
                    builder.EnableEnemyCollision(id);
                else
                    builder.DisableEnemyCollision(id);
            }
        }
    }

    internal class SbSetEntityNeck : SbNode
    {
        public CsEntity Entity { get; }
        public int Speed { get; }

        public SbSetEntityNeck(CsEntity entity, int speed)
        {
            Entity = entity;
            Speed = speed;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (Entity.Id != null)
            {
                builder.SetEnemyNeck(Entity.Id.Value, Speed);
            }
        }
    }

    internal class SbFreezeEnemies : SbContainerNode
    {
        private readonly int[] _enemyIds;

        public SbFreezeEnemies(int[] enemyIds, params SbNode[] children)
            : base(children)
        {
            _enemyIds = enemyIds;
        }

        public override void Build(CutsceneBuilder builder)
        {
            foreach (var eid in _enemyIds)
            {
                builder.DisableEnemyCollision(eid);
                builder.HideEnemy(eid);
                builder.DeactivateEnemy(eid);
            }
            base.Build(builder);
            foreach (var eid in _enemyIds)
            {
                builder.EnableEnemyCollision(eid);
                builder.UnhideEnemy(eid);
                builder.ActivateEnemy(eid);
            }
        }
    }

    internal class SbSetFlag : SbNode
    {
        public CsFlag Flag { get; }
        public bool Value { get; }

        public SbSetFlag(CsFlag flag, bool value = true)
        {
            Flag = flag;
            Value = value;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.SetFlag(Flag.Flag!.Value, Value);
        }
    }

    internal class SbProcedure : SbContainerNode
    {
        public SbProcedure(params SbNode[] children)
            : base(children)
        {
        }
    }

    internal class SbLoop : SbContainerNode
    {
        public SbLoop(params SbNode[] children)
            : base(children)
        {
        }

        public override void Build(CutsceneBuilder builder)
        {
            var repeatLabel = builder.CreateLabel();
            builder.AppendLabel(repeatLabel);
            base.Build(builder);
            builder.Goto(repeatLabel);
        }
    }

    internal class SbIf : SbNode
    {
        private readonly SbNode[] _if;
        private SbNode[]? _else = null;

        public CsFlag Flag { get; }
        public bool Value { get; }

        public SbIf(CsFlag flag, bool value, params SbNode[] children)
        {
            _if = children;
            Flag = flag;
            Value = value;
        }

        public SbIf Else(params SbNode[] children)
        {
            if (_else != null)
                throw new InvalidOperationException("Else already defined");

            _else = children;
            return this;
        }

        public override IEnumerable<SbNode> Children => _if.Concat(_else);

        public override void Build(CutsceneBuilder builder)
        {
            builder.BeginIf();
            builder.CheckFlag(Flag.Flag!.Value, Value);
            foreach (var child in _if)
            {
                child.Build(builder);
            }
            if (_else != null)
            {
                builder.Else();
                foreach (var child in _else)
                {
                    child.Build(builder);
                }
            }
            builder.EndIf();
        }
    }

    internal class SbWaitForCut : SbNode
    {
        public int Cut { get; }

        public SbWaitForCut(int cut)
        {
            Cut = cut;
        }
    }

    internal class SbWaitForFlag : SbNode
    {
        public ReFlag Flag { get; }
        public bool Value { get; }

        public SbWaitForFlag(ReFlag flag, bool value)
        {
            Flag = flag;
            Value = value;
        }
    }

    internal class SbXaOn : SbNode
    {
        public int Value { get; }

        public SbXaOn(int value)
        {
            Value = value;
        }
    }

    internal class SbVoice : SbNode
    {
        public ICsHero Speaker { get; }
        public ICsHero[] Participants { get; }
        public int? XaId { get; }

        public SbVoice(ICsHero speaker, ICsHero[] participants)
        {
            Speaker = speaker;
            Participants = participants;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (XaId != null)
            {
                builder.PlayVoice(XaId.Value);
            }
        }
    }

    internal class SbFork : SbNode
    {
        public SbProcedure Procedure { get; }

        public SbFork(SbProcedure procedure)
        {
            Procedure = procedure;
        }
    }

    internal class SbEvent : SbNode
    {
        public CsAot Aot { get; }
        public REPosition Position { get; }
        public int Size { get; }
        public SbProcedure Procedure { get; }

        public SbEvent(CsAot aot, REPosition pos, int size, SbProcedure procedure)
        {
            Aot = aot;
            Position = pos;
            Size = size;
            Procedure = procedure;
        }
    }

    internal class SbItem : SbNode
    {
        public CsItem Item { get; set; }

        public SbItem(CsItem item)
        {
            Item = item;
        }
    }
}
