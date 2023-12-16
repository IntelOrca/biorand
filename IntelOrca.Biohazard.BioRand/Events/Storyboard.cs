using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class CsPlot
    {
        public SbProcedure Root { get; }
        public bool EndOfScript { get; }

        public CsPlot(SbProcedure root, bool endOfScript = false)
        {
            Root = root;
            EndOfScript = endOfScript;
        }
    }

    internal class CsEntity
    {
        public int Id { get; set; }

        public CsEntity(int id)
        {
            Id = id;
        }
    }

    internal class CsEnemy : CsEntity
    {
        public byte Type { get; }
        public byte GlobalId { get; }
        public REPosition DefaultPosition { get; }
        public Action<SceEmSetOpcode> ProcessFunc { get; }

        public CsEnemy(byte id, byte globalId, byte type, REPosition defaultPosition, Action<SceEmSetOpcode> processFunc)
            : base(id)
        {
            GlobalId = globalId;
            Type = type;
            DefaultPosition = defaultPosition;
            ProcessFunc = processFunc;
        }
    }

    internal interface ICsHero
    {
        int Id { get; }
        string Actor { get; }
    }

    internal class CsAlly : CsEntity, ICsHero
    {
        public int Type { get; }
        public string Actor { get; }

        public CsAlly(byte id, byte type, string actor)
            : base(id)
        {
            Type = type;
            Actor = actor;
        }

        public string DisplayName
        {
            get
            {
                var s = Actor;
                var dot = s.IndexOf('.');
                if (dot != -1)
                {
                    s = s.Substring(0, dot);
                }
                return s.ToTitle();
            }
        }
    }

    internal class CsPlayer : ICsHero
    {
        public int Id => -1;
        public string Actor { get; }

        public CsPlayer(string actor)
        {
            Actor = actor;
        }
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

    internal class SbCommentNode : SbContainerNode
    {
        public string Description { get; }

        public SbCommentNode(string description, params SbNode[] children)
            : base(children)
        {
            Description = description;
        }
    }

    internal class SbSleep : SbNode
    {
        public int Ticks { get; }

        public SbSleep(int ticks)
        {
            Ticks = ticks;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (Ticks == 0)
            {
            }
            else if (Ticks == 1)
            {
                builder.Sleep1();
            }
            else
            {
                builder.Sleep(Ticks);
            }
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
            builder.AppendLine("cut_chg", Cut);
            base.Build(builder);
            builder.AppendLine("cut_old");
            builder.AppendLine("nop");
        }
    }

    internal class SbCutSequence : SbContainerNode
    {
        public SbCutSequence(params SbNode[] children)
            : base(children)
        {
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("cut_auto", 0);
            base.Build(builder);
            builder.AppendLine("cut_auto", 1);
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

    internal class SbSetFade : SbNode
    {
        private byte _a;
        private byte _b;
        private byte _c;
        private byte _d;
        private byte _e;

        public SbSetFade(byte a, byte b, byte c, byte d, byte e)
        {
            _a = a;
            _b = b;
            _c = c;
            _d = d;
            _e = e;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.SetFade(_a, _b, _c, _d, _e);
        }
    }

    internal class SbAdjustFade : SbNode
    {
        private byte _a;
        private byte _b;
        private byte _c;

        public SbAdjustFade(byte a, byte b, byte c)
        {
            _a = a;
            _b = b;
            _c = c;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AdjustFade(_a, _b, _c);
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
            builder.AppendLine("plc_rot", 0, 256);
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

    internal class SbLockControls : SbContainerNode
    {
        public SbLockControls(params SbNode[] children)
            : base(children)
        {
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.SetFlag(CutsceneBuilder.FG_STOP, 7, true);
            base.Build(builder);
            builder.SetFlag(CutsceneBuilder.FG_STOP, 7, false);
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
            builder.Ally(Ally.Id, (byte)Ally.Type, Position);
        }
    }

    internal class SbEnemy : SbNode
    {
        private readonly CsEnemy _enemy;
        private readonly REPosition _position;
        private readonly bool _enabled;
        private readonly byte? _pose;

        public SbEnemy(CsEnemy enemy, bool enabled = true, byte? pose = null)
            : this(enemy, enemy.DefaultPosition, enabled, pose)
        {
        }

        public SbEnemy(CsEnemy enemy, REPosition position, bool enabled = true, byte? pose = null)
        {
            _enemy = enemy;
            _position = position;
            _enabled = enabled;
            _pose = pose;
        }

        public override void Build(CutsceneBuilder builder)
        {
            var opcode = new SceEmSetOpcode();
            opcode.Id = (byte)_enemy.Id;
            opcode.Ai = (byte)(_enabled ? 0 : 128);
            opcode.X = (short)_position.X;
            opcode.Y = (short)_position.Y;
            opcode.Z = (short)_position.Z;
            opcode.D = (short)_position.D;
            opcode.Floor = (byte)_position.Floor;
            opcode.KillId = _enemy.GlobalId;
            opcode.Type = _enemy.Type;
            _enemy.ProcessFunc(opcode);
            if (_pose is byte pose)
            {
                opcode.State = pose;
            }
            builder.Enemy(opcode);
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
        public ICsHero Entity { get; }
        public int Speed { get; }
        public int? X { get; }
        public int? Y { get; }

        public SbSetEntityNeck(ICsHero entity, int speed)
        {
            Entity = entity;
            Speed = speed;
        }

        public SbSetEntityNeck(ICsHero entity, int speed, int x, int y)
        {
            Entity = entity;
            Speed = speed;
            X = x;
            Y = y;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (X == null || Y == null)
            {
                builder.SetEnemyNeck(Entity.Id, Speed);
            }
            else
            {
                builder.SetEnemyNeck(Entity.Id, Speed, X.Value, Y.Value);
            }
        }
    }

    internal class SbReleaseEntity : SbNode
    {
        public ICsHero Entity { get; }

        public SbReleaseEntity(ICsHero entity)
        {
            Entity = entity;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.WorkOnEnemy(Entity.Id);
            builder.AppendLine("plc_ret");
            builder.AppendLine("nop");
        }
    }

    internal class SbStopEntity : SbNode
    {
        public ICsHero Entity { get; }

        public SbStopEntity(ICsHero entity)
        {
            Entity = entity;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.WorkOnEnemy(Entity.Id);
            builder.AppendLine("plc_stop");
            builder.AppendLine("nop");
        }
    }

    internal class SbSetEntityEnabled : SbNode
    {
        public CsEntity Entity { get; }
        public bool Enabled { get; }

        public SbSetEntityEnabled(CsEntity entity, bool enabled)
        {
            Entity = entity;
            Enabled = enabled;
        }

        public override void Build(CutsceneBuilder builder)
        {
            var id = Entity.Id;
            if (Enabled)
                builder.ActivateEnemy(id);
            else
                builder.DeactivateEnemy(id);
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

    internal class SbFreezeAllEnemies : SbContainerNode
    {
        public SbFreezeAllEnemies(params SbNode[] children)
            : base(children)
        {
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.Call("freeze_all_enemies");
            base.Build(builder);
            builder.Call("unfreeze_all_enemies");
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

        public string Name { get; }

        public SbProcedure(params SbNode[] children)
            : base(children)
        {
            _id = Interlocked.Increment(ref g_nextId);
            Name = $"proc_{_id:x8}";
        }

        public SbProcedure(string name, params SbNode[] children)
            : base(children)
        {
            _id = Interlocked.Increment(ref g_nextId);
            Name = name;
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
        private ISbCondition _condition;
        private readonly SbNode[] _if;
        private SbNode[]? _else = null;

        public SbIf(CsFlag flag, bool value, params SbNode[] children)
        {
            _condition = new SbCk(flag.Flag, value);
            _if = children;
        }

        public SbIf(ISbCondition condition, params SbNode[] children)
        {
            _condition = condition;
            _if = children;
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
            _condition.Build(builder);
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
        public int[] Cuts { get; }
        public bool Equal { get; }

        public SbWaitForCut(int cut, bool equal = true)
        {
            Cuts = new[] { cut };
            Equal = equal;
        }

        public SbWaitForCut(int[]? cuts, bool equal = true)
        {
            Cuts = cuts ?? new int[0];
            Equal = equal;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (Cuts.Length == 0)
                return;

            if (Equal)
            {
                new SbWaitUnlessAny(
                    Cuts
                        .Select(x => new SbCmpCut(x))
                        .ToArray()).Build(builder);
            }
            else
            {
                new SbWaitIfAny(
                    Cuts
                        .Select(x => new SbCmpCut(x))
                        .ToArray()).Build(builder);
            }
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

    internal interface ISbCondition
    {
        public int Size { get; }
        public void Build(CutsceneBuilder builder);
    }

    internal class SbAnd : ISbCondition
    {
        private readonly ISbCondition _left;
        private readonly ISbCondition _right;

        public int Size => _left.Size + _right.Size;

        public SbAnd(ISbCondition a, ISbCondition b)
        {
            _left = a;
            _right = b;
        }

        public void Build(CutsceneBuilder builder)
        {
            _left.Build(builder);
            _right.Build(builder);
        }
    }

    internal class SbCk : ISbCondition
    {
        private readonly ReFlag _flag;
        private readonly bool _value;

        public int Size => 4;

        public SbCk(ReFlag flag, bool value)
        {
            _flag = flag;
            _value = value;
        }

        public void Build(CutsceneBuilder builder)
        {
            builder.CheckFlag(_flag, _value);
        }
    }

    internal class SbCkItem : ISbCondition
    {
        private readonly byte _item;

        public int Size => 2;

        public SbCkItem(byte item)
        {
            _item = item;
        }

        public void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("keep_item_ck", _item);
        }
    }

    internal class SbCkPoison : ISbCondition
    {
        public int Size => 2;

        public void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("poison_ck");
            builder.AppendLine("nop");
        }
    }

    internal class SbCmp : ISbCondition
    {
        private readonly int _variable;
        private readonly int _value;
        private readonly bool _equal;

        public int Size => 6;

        public SbCmp(int variable, int value, bool equal = true)
        {
            _variable = variable;
            _value = value;
            _equal = equal;
        }

        public void Build(CutsceneBuilder builder)
        {
            var op = _equal ? "CMP_EQ" : "CMP_NE";
            builder.AppendLine("cmp", 0, _variable, op, _value);
        }
    }

    internal class SbCmpCut : ISbCondition
    {
        private readonly int _cut;
        private readonly bool _equal;

        public int Size => 6;

        public SbCmpCut(int cut, bool equal = true)
        {
            _cut = cut;
            _equal = equal;
        }

        public void Build(CutsceneBuilder builder)
        {
            var op = _equal ? "CMP_EQ" : "CMP_NE";
            builder.AppendLine("cmp", 0, 26, op, _cut);
        }
    }

    internal class SbWaitIfAll : SbNode
    {
        public ISbCondition[] Conditions { get; }

        public SbWaitIfAll(params ISbCondition[] condtions)
        {
            Conditions = condtions;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.BeginWhileLoop(Conditions.Sum(x => x.Size));
            foreach (var condition in Conditions)
            {
                condition.Build(builder);
            }
            builder.AppendLine("evt_next");
            builder.AppendLine("nop");
            builder.EndLoop();
        }
    }

    internal class SbWaitIfAny : SbNode
    {
        public ISbCondition[] Conditions { get; }

        public SbWaitIfAny(params ISbCondition[] condtions)
        {
            Conditions = condtions;
        }

        public override void Build(CutsceneBuilder builder)
        {
            var combineFlag = new CsFlag(new ReFlag(CutsceneBuilder.FG_ROOM, 24));

            var label = builder.BeginDoWhileLoop();
            builder.AppendLine("evt_next");
            builder.AppendLine("nop");
            new SbSetFlag(combineFlag, false).Build(builder);
            foreach (var condition in Conditions)
            {
                new SbIf(condition,
                    new SbSetFlag(combineFlag)).Build(builder);
            }
            builder.BeginDoWhileConditions(label);
            builder.CheckFlag(combineFlag.Flag);
            builder.EndDoLoop();
        }
    }

    internal class SbWaitUnlessAny : SbNode
    {
        public ISbCondition[] Conditions { get; }

        public SbWaitUnlessAny(params ISbCondition[] condtions)
        {
            Conditions = condtions;
        }

        public override void Build(CutsceneBuilder builder)
        {
            var combineFlag = new CsFlag(new ReFlag(CutsceneBuilder.FG_ROOM, 24));

            var label = builder.BeginDoWhileLoop();
            builder.AppendLine("evt_next");
            builder.AppendLine("nop");
            new SbSetFlag(combineFlag).Build(builder);
            foreach (var condition in Conditions)
            {
                new SbIf(condition,
                    new SbSetFlag(combineFlag, false)).Build(builder);
            }
            builder.BeginDoWhileConditions(label);
            builder.CheckFlag(combineFlag.Flag);
            builder.EndDoLoop();
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
        public bool Async { get; }

        public SbVoice(int value, bool async = false)
        {
            Value = value;
            Async = async;
        }

        public override void Build(CutsceneBuilder builder)
        {
            if (Async)
                builder.PlayVoiceAsync(Value);
            else
                builder.PlayVoice(Value);
        }
    }

    internal interface ISbSubProcedure
    {
        public SbProcedure Procedure { get; }
    }

    internal class SbCall : SbNode, ISbSubProcedure
    {
        public SbProcedure Procedure { get; }

        public SbCall(SbProcedure procedure)
        {
            Procedure = procedure;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.Call(Procedure.Name);
        }
    }

    internal class SbFork : SbContainerNode, ISbSubProcedure
    {
        public SbProcedure Procedure { get; }

        public SbFork(SbProcedure procedure, params SbNode[] children)
            : base(children)
        {
            Procedure = procedure;
        }

        public override void Build(CutsceneBuilder builder)
        {
            var children = Children;
            if (children.Any())
            {
                builder.AppendLine("evt_exec", 9, "I_GOSUB", Procedure.Name);
                base.Build(builder);
                builder.AppendLine("evt_kill", 9);
            }
            else
            {
                builder.AppendLine("evt_exec", 255, "I_GOSUB", Procedure.Name);
            }
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

    internal class SbDisableAot : SbNode
    {
        public CsAot Aot { get; }

        public SbDisableAot(CsAot aot)
        {
            Aot = aot;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("aot_reset", Aot.Id, "SCE_AUTO", "SAT_AUTO", 0, 0, 0, 0, 0, 0);
        }
    }

    internal class SbEnableEvent : SbNode, ISbSubProcedure
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

    internal class SbMessage : SbNode
    {
        private readonly CsMessage _message;
        private readonly SbNode[] _options;

        public SbMessage(CsMessage message, params SbNode[] options)
        {
            _message = message;
            _options = options;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("message_on", 0, _message.Id, 0, 255, 255);
            builder.AppendLine("evt_next");
            builder.AppendLine("nop");

            if (_options.Length >= 1)
            {
                builder.BeginIf();
                builder.AppendLine("ck", "FG_MESSAGE", "F_QUESTION", 0);
                _options[0].Build(builder);
                if (_options.Length >= 2)
                {
                    builder.Else();
                    _options[1].Build(builder);
                }
                builder.EndIf();
            }
        }
    }

    internal class SbRemoveItem : SbNode
    {
        private readonly byte _item;
        private readonly byte _count;

        public SbRemoveItem(byte item, byte count = 1)
        {
            _item = item;
            _count = count;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("sce_item_ck_lost", _item, _count);
        }
    }

    internal class SbGetItem : SbNode
    {
        private readonly byte _item;
        private readonly byte _count;

        public SbGetItem(byte item, byte count)
        {
            _item = item;
            _count = count;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("sce_item_get", _item, _count);
        }
    }

    internal class SbHeal : SbNode
    {
        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("heal");
            builder.AppendLine("nop");
        }
    }

    internal class SbHealPoison : SbNode
    {
        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine("poison_clr");
            builder.AppendLine("nop");
        }
    }

    internal class SbMotion : SbNode
    {
        public ICsHero Entity { get; }
        public int Group { get; set; }
        public int Animation { get; set; }
        public int Flags { get; set; }

        public SbMotion(ICsHero entity, int group, int animation, int flags)
        {
            Entity = entity;
            Group = group;
            Animation = animation;
            Flags = flags;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.WorkOnEnemy(Entity.Id);
            builder.AppendLine("plc_motion", Group, Animation, Flags);
        }
    }

    internal class SbKageSet : SbNode
    {
        public CsEntity Entity { get; }
        public Color Colour { get; }
        public int Size { get; set; }

        public SbKageSet(CsEntity entity, Color colour, int size)
        {
            Entity = entity;
            Colour = colour;
            Size = size;
        }

        public override void Build(CutsceneBuilder builder)
        {
            builder.AppendLine(
                "kage_set",
                3,
                Entity.Id,
                255 - Colour.R,
                255 - Colour.G,
                255 - Colour.B,
                Size,
                Size,
                0,
                0);
        }
    }

    internal class SbCustom : SbNode
    {
        public Action<CutsceneBuilder> Builder { get; }

        public SbCustom(Action<CutsceneBuilder> builder)
        {
            Builder = builder;
        }

        public override void Build(CutsceneBuilder builder)
        {
            Builder?.Invoke(builder);
        }
    }

    internal class CsMessage
    {
        public int Id { get; set; }
        public string Message { get; set; }

        public CsMessage(int id, string message)
        {
            Id = id;
            Message = message;
        }
    }
}
