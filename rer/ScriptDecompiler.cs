using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace rer
{
    internal class ScriptDecompiler : BioScriptVisitor
    {
        private ScriptBuilder _sb = new ScriptBuilder();
        private Stack<int> _blockEnds = new Stack<int>();
        private bool _constructingBinaryExpression;
        private int _expressionCount;

        public string GetScript()
        {
            return _sb.ToString();
        }

        public override void VisitBeginScript(BioScriptKind kind)
        {
            switch (kind)
            {
                case BioScriptKind.Init:
                    _sb.WriteLine("init");
                    _sb.WriteLine("{");
                    _sb.Indent();
                    break;
                case BioScriptKind.Main:
                    _sb.WriteLine();
                    _sb.WriteLine("main");
                    _sb.WriteLine("{");
                    _sb.Indent();
                    break;
            }
        }

        public override void VisitEndScript(BioScriptKind kind)
        {
            _sb.ResetIndent();
            _sb.WriteLine("}");
        }

        public override void VisitBeginSubroutine(int index)
        {
            _sb.ResetIndent();
            if (index != 0)
            {
                _sb.WriteLine();
            }
            _sb.Indent();
            _sb.WriteLine($"sub_{index:X2}()");
            _sb.WriteLine("{");
            _sb.Indent();

            _blockEnds.Clear();
        }

        public override void VisitEndSubroutine(int index)
        {
            _sb.ResetIndent();
            _sb.Indent();
            _sb.WriteLine("}");
        }

        public override void VisitOpcode(int offset, Opcode opcode, Span<byte> operands)
        {
            if (_constructingBinaryExpression)
            {
                if (opcode != Opcode.Ck && opcode != Opcode.Cmp)
                {
                    _constructingBinaryExpression = false;
                    _sb.WriteLine(")");
                    _sb.WriteLine("{");
                    _sb.Indent();
                }
            }

            while (_blockEnds.Count != 0 && _blockEnds.Peek() <= offset)
            {
                _blockEnds.Pop();
                _sb.Unindent();
                _sb.WriteLine("}");
            }

            switch (opcode)
            {
                case Opcode.DoorAotSe:
                case Opcode.SceEmSet:
                case Opcode.AotReset:
                case Opcode.ItemAotSet:
                case Opcode.XaOn:
                    base.VisitOpcode(offset, opcode, operands);
                    break;
                default:
                    VisitOpcode(offset, opcode, new BinaryReader(new MemoryStream(operands.ToArray())));
                    break;
            }
        }

        protected override void VisitDoorAotSe(Door door)
        {
            _sb.WriteLine($"door_aot_se({door.Id}, {door.Stage:X}, 0x{door.Room:X2}, {door.DoorFlag}, {door.DoorLockFlag}, {door.DoorKey});");
        }

        protected override void VisitAotReset(Reset reset)
        {
            _sb.WriteLine($"aot_reset({reset.Id}, {reset.Type}, {reset.Amount}, 0x{reset.Unk8:X2});");
        }

        protected override void VisitSceEmSet(RdtEnemy enemy)
        {
            _sb.WriteLine($"sce_em_set({enemy.Id}, ENEMY_{enemy.Type.ToString().ToUpperInvariant()}, {enemy.State}, {enemy.Ai}, {enemy.Floor}, {enemy.SoundBank}, {enemy.Texture}, {enemy.KillId}, ..., {enemy.Animation});");
        }

        protected override void VisitItemAotSet(Item item)
        {
            _sb.WriteLine($"item_aot_set({item.Id}, ITEM_{((ItemType)item.Type).ToString().ToUpperInvariant()}, {item.Amount});");
        }

        protected override void VisitXaOn(RdtSound sound)
        {
            _sb.WriteLine($"xa_on({sound.Channel}, {sound.Id});");
        }

        private void VisitOpcode(int offset, Opcode opcode, BinaryReader br)
        {
            var sb = _sb;
            switch (opcode)
            {
                default:
                    if (Enum.IsDefined(typeof(Opcode), opcode))
                    {
                        sb.WriteLine($"{opcode}();");
                    }
                    else
                    {
                        sb.WriteLine($"op_{opcode:X}();");
                    }
                    break;
                case Opcode.Nop:
                    break;
                case Opcode.EvtEnd:
                    {
                        var ret = br.ReadByte();
                        sb.WriteLine($"return {ret};");
                        break;
                    }
                case Opcode.EvtExec:
                    {
                        var cond = br.ReadByte();
                        var exOpcode = br.ReadByte();
                        var evnt = br.ReadByte();
                        var exOpcodeS = $"OP_{ ((Opcode)exOpcode).ToString().ToUpperInvariant()}";
                        if (cond == 255)
                            sb.WriteLine($"evt_exec(CAMERA, {exOpcodeS}, {evnt});");
                        else
                            sb.WriteLine($"evt_exec({cond}, {exOpcodeS}, {evnt});");
                        break;
                    }
                case Opcode.IfelCk:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        _blockEnds.Push(offset + blockLen);
                        sb.Write($"if (");
                        _constructingBinaryExpression = true;
                        _expressionCount = 0;
                        break;
                    }
                case Opcode.ElseCk:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        _blockEnds.Push(offset + blockLen);

                        sb.WriteLine("else");
                        sb.WriteLine("{");
                        sb.Indent();
                    }
                    break;
                case Opcode.EndIf:
                    // sb.Unindent();
                    // sb.WriteLine("}");
                    // _expectingEndIf = false;
                    break;
                case Opcode.Sleep:
                    {
                        br.ReadByte();
                        var count = br.ReadUInt16();
                        sb.WriteLine($"sleep({count});");
                        break;
                    }
                case Opcode.Sleeping:
                    {
                        var count = br.ReadUInt16();
                        sb.WriteLine($"sleeping({count});");
                        break;
                    }
                case Opcode.Wsleep:
                    {
                        sb.WriteLine($"wsleep();");
                        break;
                    }
                case Opcode.Wsleeping:
                    {
                        sb.WriteLine($"wsleeping();");
                        break;
                    }
                case Opcode.For:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        var count = br.ReadUInt16();
                        sb.WriteLine($"for {count} times");
                        sb.WriteLine("{");
                        sb.Indent();
                        break;
                    }
                case Opcode.Next:
                    {
                        sb.Unindent();
                        sb.WriteLine("}");
                        break;
                    }
                case Opcode.While:
                    {
                        sb.WriteLine($"while (");
                        sb.WriteLine("{");
                        sb.Indent();
                        break;
                    }
                case Opcode.Ewhile:
                    {
                        sb.Unindent();
                        sb.WriteLine("}");
                        break;
                    }
                case Opcode.Do:
                    {
                        sb.WriteLine($"do");
                        sb.WriteLine("{");
                        sb.Indent();
                        break;
                    }
                case Opcode.Edwhile:
                    {
                        sb.Unindent();
                        sb.WriteLine($"while (");
                        sb.WriteLine("{");
                        break;
                    }
                case Opcode.Switch:
                    {
                        var varw = br.ReadByte();
                        sb.WriteLine($"switch (var_{varw:X2})");
                        sb.WriteLine("{");
                        sb.Indent();
                        break;
                    }
                case Opcode.Case:
                    {
                        br.ReadByte();
                        br.ReadUInt16();
                        var value = br.ReadUInt16();
                        sb.Unindent();
                        sb.WriteLine($"case {value}:");
                        sb.Indent();
                        break;
                    }
                case Opcode.Default:
                    {
                        sb.Unindent();
                        sb.WriteLine($"default:");
                        sb.Indent();
                        break;
                    }
                case Opcode.Eswitch:
                    {
                        sb.Unindent();
                        sb.WriteLine("}");
                        break;
                    }
                case Opcode.Gosub:
                    {
                        var num = br.ReadByte();
                        sb.WriteLine($"sub_{num:X2}();");
                        break;
                    }
                case Opcode.Return:
                    {
                        sb.WriteLine($"return;");
                        break;
                    }
                case Opcode.Break:
                    {
                        sb.WriteLine("break;");
                        break;
                    }
                case Opcode.Cmp:
                    {
                        var ops = new[] { "==", ">", ">=", "<", "<=", "!=" };

                        br.ReadByte();
                        var index = br.ReadByte();
                        var op = br.ReadByte();
                        var value = br.ReadInt16();
                        var opS = ops.Length > op ? ops[op] : "?";

                        if (_constructingBinaryExpression)
                        {
                            if (_expressionCount != 0)
                            {
                                sb.Write(" && ");
                            }
                            sb.Write($"arr[{index}] {opS} {value}");
                            _expressionCount++;
                        }
                        break;
                    }
                case Opcode.Ck:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var value = br.ReadByte();

                        if (_constructingBinaryExpression)
                        {
                            if (_expressionCount != 0)
                            {
                                sb.Write(" && ");
                            }
                            if (value == 0)
                                sb.Write($"!");
                            sb.Write($"bits[{bitArray}][{number}]");
                            _expressionCount++;
                        }
                        break;
                    }
                case Opcode.ObjModelSet:
                    {
                        var id = br.ReadByte();
                        sb.WriteLine($"obj_model_set({id}, ...)");
                        break;
                    }
                case Opcode.WorkSet:
                    {
                        var kind = br.ReadByte();
                        var id = br.ReadByte();

                        var kindS = kind.ToString();
                        if (kind == 1)
                            kindS = "wk_player";
                        else if (kind == 3)
                            kindS = "wk_entity";
                        else if (kind == 4)
                            kindS = "wk_door";

                        sb.WriteLine($"work_set({kindS}, {id});");
                        break;
                    }
                case Opcode.Set:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var opChg = br.ReadByte();
                        sb.Write($"bits[{bitArray}][{number}]");
                        if (opChg == 0)
                            sb.WriteLine(" = 0;");
                        else if (opChg == 1)
                            sb.WriteLine(" = 1;");
                        else if (opChg == 7)
                            sb.WriteLine(" ^= 1;");
                        else
                            sb.WriteLine(" (INVALID);");
                        break;
                    }
                case Opcode.Calc:
                    {
                        var ops = new string[] { "+", "-", "*", "/", "%", "|", "&", "^", "~", "<<", ">>", ">>>" };
                        var op = br.ReadByte();
                        var var = br.ReadByte();
                        var src = br.ReadByte();
                        var opS = ops.Length > op ? ops[op] : "?";
                        sb.WriteLine($"var_{var:X2} {opS}= {src:X2};");
                        break;
                    }
                case Opcode.AotSet:
                    {
                        var id = br.ReadByte();
                        var type = br.ReadByte();
                        br.ReadBytes(3);
                        br.ReadBytes(8);
                        br.ReadBytes(6);
                        sb.WriteLine($"aot_set({id}, 0x{type:X});");
                        break;
                    }
                case Opcode.PosSet:
                    {
                        var x = br.ReadInt16();
                        var y = br.ReadInt16();
                        var z = br.ReadInt16();
                        sb.WriteLine($"pos_set({x}, {y}, {z});");
                        break;
                    }
                case Opcode.ScaIdSet:
                    {
                        var entry = br.ReadByte();
                        var id = br.ReadUInt16();
                        sb.WriteLine($"sca_id_set({entry}, {id});");
                        break;
                    }
                case Opcode.AotOn:
                    {
                        var id = br.ReadByte();
                        sb.WriteLine($"aot_on({id});");
                        break;
                    }
                case Opcode.SceBgmControl:
                    {
                        var bgm = br.ReadByte();
                        var action = br.ReadByte();
                        var dummy = br.ReadByte();
                        var volume = br.ReadByte();
                        var channel = br.ReadByte();
                        sb.WriteLine($"sce_bgm_control({bgm},{action},{volume},{channel});");
                        break;
                    }
                case Opcode.SceBgmtblSet:
                    {
                        var dummy = br.ReadByte();
                        var roomId = br.ReadByte();
                        var stage = br.ReadByte();
                        var main = br.ReadByte();
                        var sub = br.ReadByte();
                        var dummy1 = br.ReadByte();
                        var dummy2 = br.ReadByte();
                        sb.WriteLine($"sce_bgmtbl_set({stage:X}{roomId:X2} = MAIN{main:X2} SUB{sub:X2});");
                        break;
                    }
                case Opcode.XaOn:
                    {
                        var channel = br.ReadByte();
                        var id = br.ReadInt16();
                        sb.WriteLine($"xa_on({channel}, {id});");
                        break;
                    }
                case Opcode.SceItemLost:
                    {
                        var item = br.ReadByte();
                        sb.WriteLine($"sce_item_lost(ITEM_{((ItemType)item).ToString().ToUpperInvariant()});");
                        break;
                    }
                case Opcode.DoorAotSet4p:
                    {
                        var id = br.ReadByte();
                        sb.WriteLine($"door_aot_set_4p({id});");
                        break;
                    }
                case Opcode.ItemAotSet4p:
                    {
                        var id = br.ReadByte();
                        sb.WriteLine($"item_aot_set_4p({id});");
                        break;
                    }
                case Opcode.SceItemGet:
                    {
                        var type = br.ReadByte();
                        var amount = br.ReadByte();
                        sb.WriteLine($"sce_item_get(0x{type}, {amount});");
                        break;
                    }
            }
        }
    }
}
