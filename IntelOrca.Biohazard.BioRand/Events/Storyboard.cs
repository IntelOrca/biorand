using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class CsPlot
    {
        public SbNode Root { get; }

        public CsPlot(SbNode root)
        {
            Root = root;
        }
    }

    internal class CsEntity
    {
        public byte Id { get; set; }

        public CsEntity(byte id)
        {
            Id = id;
        }
    }

    internal class CsEnemy : CsEntity
    {
        public CsEnemy(byte id)
            : base(id)
        {
        }
    }

    internal interface ICsHero
    {
        string Actor { get; }
    }

    internal class CsAlly : CsEntity, ICsHero
    {
        public byte Type { get; }
        public string Actor { get; }

        public CsAlly(byte id, byte type, string actor)
            : base(id)
        {
            Type = type;
            Actor = actor;
        }
    }

    internal class CsPlayer : ICsHero
    {
        public string Actor { get; } = "leon";
    }

    internal class CsFlag
    {
        public ReFlag Flag { get; set; }

        public CsFlag() { }

        public CsFlag(ReFlag flag)
        {
            Flag = flag;
        }

        public CsFlag(byte type, byte index)
        {
            Flag = new ReFlag(type, index);
        }
    }

    internal class CsAot
    {
        public byte Id { get; }

        public CsAot(byte id)
        {
            Id = id;
        }
    }

    internal class CsItem : CsAot
    {
        public byte GlobalId { get; } = 255;
        public Item Item { get; }

        public CsItem(byte id, byte globalId, Item item)
            : base(id)
        {
            GlobalId = globalId;
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

        public static SbNode Conditional(bool value, Func<SbNode> callback)
        {
            return value ? callback() : new SbNop();
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
            builder.AotOn(Aot.Id);
        }
    }

    internal class SbDoor : SbNode
    {
        public PointOfInterest Poi { get; }

        public SbDoor(PointOfInterest poi)
        {
            Poi = poi;
        }

        public override void Build(CutsceneBuilder builder)
        {
            var poi = Poi;
            var pos = poi.Position;
            if (poi.HasTag("door"))
            {
                builder.PlayDoorSoundOpen(pos);
                builder.Sleep(30);
                builder.PlayDoorSoundClose(pos);
            }
            else
            {
                builder.Sleep(30);
            }
        }
    }

    internal class SbEntityTravel : SbNode
    {
        public CsEntity Entity { get; }
        public ReFlag CompletionFlag { get; }
        public REPosition Destination { get; }
        public PlcDestKind Kind { get; }

        public SbEntityTravel(
            CsEntity entity,
            ReFlag completionFlag,
            REPosition destination,
            PlcDestKind kind)
        {
            Entity = entity;
            CompletionFlag = completionFlag;
            Destination = destination;
            Kind = kind;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.WorkOnEnemy(Entity.Id);
            builder.AppendLine("plc_dest", 0, (byte)Kind, CompletionFlag.Index, Destination.X, Destination.Z);
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
        public REPosition Position { get; }

        public SbAlly(CsAlly ally, REPosition position)
        {
            Ally = ally;
            Position = position;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.Ally(Ally.Id, Ally.Type, Position);
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
            builder.MoveEnemy(Entity.Id, Position);
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
            if (Value)
                builder.EnableEnemyCollision(Entity.Id);
            else
                builder.DisableEnemyCollision(Entity.Id);
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
            builder.SetEnemyNeck(Entity.Id, Speed);
        }
    }

    internal class SbFreezeEnemies : SbContainerNode
    {
        private readonly CsEnemy[] _enemies;

        public SbFreezeEnemies(CsEnemy[] enemies, params SbNode[] children)
            : base(children)
        {
            _enemies = enemies;
        }

        public override void Build(CutsceneBuilder builder)
        {
            foreach (var e in _enemies)
            {
                builder.DisableEnemyCollision(e.Id);
                builder.HideEnemy(e.Id);
                builder.DeactivateEnemy(e.Id);
            }
            base.Build(builder);
            foreach (var e in _enemies)
            {
                builder.EnableEnemyCollision(e.Id);
                builder.UnhideEnemy(e.Id);
                builder.ActivateEnemy(e.Id);
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
            builder.SetFlag(Flag.Flag, Value);
        }
    }

    internal class SbProcedure : SbContainerNode
    {
        private static int g_nextId;
        private readonly int _id;

        public string Name => $"proc_{_id:x8}";

        public SbProcedure(params SbNode[] children)
            : base(children)
        {
            _id = Interlocked.Increment(ref g_nextId);
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.BeginProcedure(Name);
            base.Build(builder);
            builder.EndProcedure();
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

        public override IEnumerable<SbNode> Children => _if.Concat(_else ?? new SbNode[0]);

        public override void Build(CutsceneBuilder builder)
        {
            builder.BeginIf();
            builder.CheckFlag(Flag.Flag, Value);
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

        public override void Build(CutsceneBuilder builder)
        {
            builder.WaitForTriggerCut(Cut);
        }
    }

    internal class SbWaitForFlag : SbNode
    {
        public ReFlag Flag { get; }
        public bool Value { get; }

        public SbWaitForFlag(ReFlag flag, bool value = true)
        {
            Flag = flag;
            Value = value;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.WaitForFlag(Flag, Value);
        }
    }

    internal class SbXaOn : SbNode
    {
        public int Value { get; }

        public SbXaOn(int value)
        {
            Value = value;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.PlayVoiceAsync(Value);
        }
    }

    internal class SbVoice : SbNode
    {
        public int Value { get; }

        public SbVoice(int value)
        {
            Value = value;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.PlayVoice(Value);
        }
    }

    internal class SbFork : SbNode
    {
        public SbProcedure Procedure { get; }

        public SbFork(SbProcedure procedure)
        {
            Procedure = procedure;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.CallThread(Procedure.Name);
        }
    }

    internal class SbAot : SbNode
    {
        public CsAot Aot { get; }
        public REPosition Position { get; }
        public int Size { get; }

        public SbAot(CsAot aot, REPosition pos, int size)
        {
            Aot = aot;
            Position = pos;
            Size = size;
        }

        public override void Build(CutsceneBuilder builder)
        {
            var id = Aot.Id;
            var pos = Position;
            var size = Size;
            builder.AppendLine("aot_set", id, "SCE_AUTO", "SAT_PL | SAT_MANUAL | SAT_FRONT", pos.Floor, 0, pos.X - size / 2, pos.Z - size / 2, size, size, 0, 0, 0, 0, 0, 0);
        }
    }

    internal class SbEnableEvent : SbNode
    {
        public CsAot Aot { get; }
        public SbProcedure Procedure { get; }

        public SbEnableEvent(CsAot aot, SbProcedure procedure)
        {
            Aot = aot;
            Procedure = procedure;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("aot_reset", Aot.Id, "SCE_EVENT", "SAT_PL | SAT_MANUAL | SAT_FRONT", 255, 0, "I_GOSUB", Procedure.Name, 0, 0);
        }
    }

    internal class SbItem : SbNode
    {
        public CsItem Item { get; set; }

        public SbItem(CsItem item)
        {
            Item = item;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.Item(Item.GlobalId, Item.Id, Item.Item.Type, Item.Item.Amount);
        }
    }
}
