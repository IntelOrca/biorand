using System;
using System.Collections.Generic;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class ScriptDecompiler : BioScriptVisitor
    {
        private ScriptBuilder _sb = new ScriptBuilder();
        private Stack<(Opcode, int)> _blockEnds = new Stack<(Opcode, int)>();
        private bool _endDoWhile;
        private bool _constructingBinaryExpression;
        private int _expressionCount;

        private static readonly string[] g_sceNames = new string[] {
            "SCE_AUTO",
            "SCE_DOOR",
            "SCE_ITEM",
            "SCE_NORMAL",
            "SCE_MESSAGE",
            "SCE_EVENT",
            "SCE_FLAG_CHG",
            "SCE_WATER",
            "SCE_MOVE",
            "SCE_SAVE",
            "SCE_ITEMBOX",
            "SCE_DAMAGE",
            "SCE_STATUS",
            "SCE_HIKIDASHI",
            "SCE_WINDOWS"
        };

        private static readonly string[] g_workKinds = new string[]
        {
            "WK_NONE",
            "WK_PLAYER",
            "WK_SPLAYER",
            "WK_ENEMY",
            "WK_OBJECT",
            "WK_DOOR",
            "WK_ALL"
        };

        public bool AssemblyFormat => _sb.AssemblyFormat;

        public ScriptDecompiler(bool assemblyFormat)
        {
            _sb.AssemblyFormat = assemblyFormat;
        }

        public string GetScript()
        {
            return _sb.ToString();
        }

        public override void VisitBeginScript(BioScriptKind kind)
        {
            switch (kind)
            {
                case BioScriptKind.Init:
                    if (AssemblyFormat)
                    {
                        _sb.WriteLine("init:");
                    }
                    else
                    {
                        _sb.WriteLine("init");
                        _sb.OpenBlock();
                    }
                    break;
                case BioScriptKind.Main:
                    _sb.WriteLine();
                    if (AssemblyFormat)
                    {
                        _sb.WriteLine("main:");
                    }
                    else
                    {
                        _sb.WriteLine("main");
                        _sb.OpenBlock();
                    }
                    break;
            }
        }

        public override void VisitEndScript(BioScriptKind kind)
        {
            if (AssemblyFormat)
                return;

            _sb.ResetIndent();
            _sb.CloseBlock();
        }

        public override void VisitBeginSubroutine(int index)
        {
            if (index != 0)
            {
                _sb.WriteLine();
            }
            if (AssemblyFormat)
            {
                _sb.WriteLine($"sub_{index:X2}:");
            }
            else
            {
                _sb.ResetIndent();

                _sb.Indent();
                _sb.WriteLine($"sub_{index:X2}()");
                _sb.OpenBlock();

                _blockEnds.Clear();
            }
        }

        public override void VisitEndSubroutine(int index)
        {
            if (AssemblyFormat)
                return;

            while (_blockEnds.Count != 0)
            {
                CloseCurrentBlock();
            }

            _sb.CloseBlock();
        }

        public override void VisitOpcode(int offset, Opcode opcode, Span<byte> operands)
        {
            _sb.RecordOpcode(offset, opcode, operands);

            if (_constructingBinaryExpression)
            {
                if (opcode != Opcode.Ck && opcode != Opcode.Cmp && opcode != Opcode.MemberCmp)
                {
                    _constructingBinaryExpression = false;
                    if (_endDoWhile)
                    {
                        _endDoWhile = false;
                        _sb.WriteLine(");");
                    }
                    else
                    {
                        _sb.WriteLine(")");
                        _sb.OpenBlock();
                    }
                }
            }
            else
            {
                // _sb.WriteLabel(offset);
            }

            while (_blockEnds.Count != 0 && _blockEnds.Peek().Item2 <= offset)
            {
                CloseCurrentBlock();
            }

            switch (opcode)
            {
                case Opcode.AotSet:
                case Opcode.DoorAotSe:
                case Opcode.DoorAotSet4p:
                case Opcode.SceEmSet:
                case Opcode.AotReset:
                case Opcode.ItemAotSet:
                case Opcode.ItemAotSet4p:
                case Opcode.XaOn:
                    base.VisitOpcode(offset, opcode, operands);
                    break;
                default:
                    VisitOpcode(offset, opcode, new BinaryReader(new MemoryStream(operands.ToArray())));
                    break;
            }
        }

        private void CloseCurrentBlock()
        {
            if (_blockEnds.Count != 0)
            {
                _blockEnds.Pop();
                if (!_endDoWhile)
                {
                    _sb.CloseBlock();
                }
            }
        }

        protected override void VisitAotSet(AotSetOpcode set)
        {
            _sb.WriteStandardOpcode("aot_set",
                set.Id,
                GetSCE(set.SCE),
                GetSAT(set.SAT),
                set.Floor,
                set.Super,
                set.X,
                set.Z,
                set.W,
                set.D,
                set.Data0,
                set.Data1,
                set.Data2);
        }

        protected override void VisitDoorAotSe(DoorAotSeOpcode door)
        {
            _sb.WriteStandardOpcode("door_aot_se",
                door.Id,
                GetSCE(door.SCE),
                GetSAT(door.SAT),
                door.Floor,
                door.Super,
                door.X,
                door.Z,
                door.W,
                door.D,
                door.NextX,
                door.NextY,
                door.NextZ,
                door.NextD,
                $"{door.NextStage:X}",
                $"0x{door.NextRoom:X2}",
                door.NextCamera,
                door.NextFloor,
                door.Texture,
                door.Animation,
                door.Sound,
                door.KeyId,
                door.KeyType,
                door.Free);
        }

        protected override void VisitDoorAotSet4p(DoorAotSet4pOpcode door)
        {
            _sb.WriteStandardOpcode("door_aot_set_4p",
                door.Id,
                GetSCE(door.SCE),
                GetSAT(door.SAT),
                door.Floor,
                door.Super,
                door.X0,
                door.Z0,
                door.X1,
                door.Z1,
                door.X2,
                door.Z2,
                door.X3,
                door.Z3,
                door.NextX,
                door.NextY,
                door.NextZ,
                door.NextD,
                $"{door.NextStage:X}",
                $"0x{door.NextRoom:X2}",
                door.NextCamera,
                door.NextFloor,
                door.Texture,
                door.Animation,
                door.Sound,
                door.KeyId,
                door.KeyType,
                door.Free);
        }

        protected override void VisitAotReset(AotResetOpcode reset)
        {
            _sb.WriteStandardOpcode("aot_reset",
                reset.Id,
                GetSCE(reset.SCE),
                GetSAT(reset.SAT),
                reset.SCE == 2 ? GetItemConstant(reset.Data0) : reset.Data0.ToString(),
                reset.Data1,
                reset.Data2);
        }

        protected override void VisitSceEmSet(SceEmSetOpcode enemy)
        {
            _sb.WriteStandardOpcode("sce_em_set", enemy.Id, GetEnemyConstant(enemy.Type),
                enemy.State, enemy.Ai, enemy.Floor, enemy.SoundBank, enemy.Texture, enemy.KillId, enemy.X, enemy.Y, enemy.Z, enemy.D, enemy.Animation);
        }

        protected override void VisitItemAotSet(ItemAotSetOpcode item)
        {
            _sb.WriteStandardOpcode("item_aot_set",
                item.Id,
                GetSCE(item.SCE),
                GetSAT(item.SAT),
                item.Floor,
                item.Super,
                item.X,
                item.Y,
                item.W,
                item.H,
                GetItemConstant(item.Type),
                item.Amount,
                item.Array8Idx,
                item.MD1,
                item.Action);
        }

        protected override void VisitItemAotSet4p(ItemAotSet4pOpcode item)
        {
            _sb.WriteStandardOpcode("item_aot_set_4p",
                item.Id,
                GetSCE(item.SCE),
                GetSAT(item.SAT),
                item.Floor,
                item.Super,
                item.X0,
                item.Z0,
                item.X1,
                item.Z1,
                item.X2,
                item.Z2,
                item.X3,
                item.Z3,
                GetItemConstant(item.Type),
                item.Amount,
                item.Array8Idx,
                item.MD1,
                item.Action);
        }

        protected override void VisitXaOn(XaOnOpcode sound)
        {
            _sb.WriteStandardOpcode("xa_on", sound.Channel, sound.Id);
        }

        protected override void VisitSceItemGet(SceItemGetOpcode itemGet)
        {
            _sb.WriteStandardOpcode("sce_item_get", GetItemConstant(itemGet.Type), itemGet.Amount);
        }

        private void VisitOpcode(int offset, Opcode opcode, BinaryReader br)
        {
            var sb = _sb;
            switch (opcode)
            {
                default:
                    if (Enum.IsDefined(typeof(Opcode), opcode))
                    {
                        sb.WriteStandardOpcode(opcode.ToString());
                    }
                    else
                    {
                        sb.WriteStandardOpcode($"op_{opcode:X}");
                    }
                    break;
                case Opcode.Nop:
                case Opcode.Nop20:
                    if (AssemblyFormat)
                        sb.WriteStandardOpcode("nop");
                    break;
                case Opcode.EvtEnd:
                    {
                        var ret = br.ReadByte();
                        if (AssemblyFormat)
                            sb.WriteStandardOpcode("return", ret);
                        else
                            sb.WriteLine($"return {ret};");
                        break;
                    }
                case Opcode.EvtNext:
                    sb.WriteStandardOpcode("evt_next");
                    break;
                case Opcode.EvtExec:
                    {
                        var cond = br.ReadByte();
                        var exOpcode = br.ReadByte();
                        var evnt = br.ReadByte();
                        var exOpcodeS = $"OP_{ ((Opcode)exOpcode).ToString().ToUpperInvariant()}";
                        if (cond == 255)
                            sb.WriteStandardOpcode("evt_exec", "CAMERA", exOpcodeS, evnt);
                        else
                            sb.WriteStandardOpcode("evt_exec", cond, exOpcodeS, evnt);
                        break;
                    }
                case Opcode.IfelCk:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        if (AssemblyFormat)
                        {
                            _sb.InsertLabel(offset + blockLen);
                            _sb.WriteStandardOpcode("if", sb.GetLabelName(offset + blockLen));
                        }
                        else
                        {
                            // _blockEnds.Push((opcode, offset + blockLen));
                            sb.Write("if (");
                            _constructingBinaryExpression = true;
                            _expressionCount = 0;
                        }
                        break;
                    }
                case Opcode.ElseCk:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        if (AssemblyFormat)
                        {
                            _sb.InsertLabel(offset + blockLen);
                            _sb.WriteStandardOpcode("else", sb.GetLabelName(offset + blockLen));
                        }
                        else
                        {
                            _sb.CloseBlock();

                            // CloseCurrentBlock();

                            // while (_blockEnds.Count != 0 && _blockEnds.Peek().Item1 != Opcode.IfelCk)
                            // {
                            //     CloseCurrentBlock();
                            // }
                            // if (_blockEnds.Count != 0 && _blockEnds.Peek().Item1 == Opcode.IfelCk)
                            // {
                            //     CloseCurrentBlock();
                            // }

                            _blockEnds.Push((opcode, offset + blockLen));

                            sb.WriteLine("else");
                            _sb.OpenBlock();
                        }
                    }
                    break;
                case Opcode.EndIf:
                    if (AssemblyFormat)
                    {
                        sb.WriteStandardOpcode("endif");
                    }
                    else
                    {
                        _sb.CloseBlock();
                        // CloseCurrentBlock();
                    }
                    break;
                case Opcode.Sleep:
                    {
                        br.ReadByte();
                        var count = br.ReadUInt16();
                        sb.WriteStandardOpcode("sleep", count);
                        break;
                    }
                case Opcode.Sleeping:
                    {
                        var count = br.ReadUInt16();
                        sb.WriteStandardOpcode("sleeping", count);
                        break;
                    }
                case Opcode.Wsleep:
                    {
                        sb.WriteStandardOpcode("wsleep");
                        break;
                    }
                case Opcode.Wsleeping:
                    {
                        sb.WriteStandardOpcode("wsleeping");
                        break;
                    }
                case Opcode.For:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        var count = br.ReadUInt16();
                        if (AssemblyFormat)
                        {
                            _sb.InsertLabel(offset + blockLen);
                            _sb.WriteStandardOpcode("for", sb.GetLabelName(offset + blockLen), count);
                        }
                        else
                        {
                            sb.WriteLine($"for {count} times");
                            _sb.OpenBlock();
                        }
                        break;
                    }
                case Opcode.Next:
                    if (AssemblyFormat)
                    {
                        _sb.WriteStandardOpcode("next");
                    }
                    else
                    {
                        sb.CloseBlock();
                    }
                    break;
                case Opcode.While:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        if (AssemblyFormat)
                        {
                            _sb.InsertLabel(offset + blockLen);
                            _sb.WriteStandardOpcode("while", sb.GetLabelName(offset + blockLen));
                        }
                        else
                        {
                            sb.WriteLine($"while (");
                            _sb.OpenBlock();
                        }
                        break;
                    }
                case Opcode.Ewhile:
                    if (AssemblyFormat)
                    {
                        _sb.WriteStandardOpcode("ewhile");
                    }
                    else
                    {
                        sb.CloseBlock();
                    }
                    break;
                case Opcode.Do:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();

                        if (AssemblyFormat)
                        {
                            _sb.WriteStandardOpcode("do", sb.GetLabelName(offset + blockLen));
                        }
                        else
                        {
                            _blockEnds.Push((opcode, offset + blockLen));

                            sb.WriteLine($"do");
                            _sb.OpenBlock();
                        }
                        break;
                    }
                case Opcode.Edwhile:
                    {
                        if (AssemblyFormat)
                        {
                            _sb.WriteStandardOpcode("edwhile");
                        }
                        else
                        {
                            sb.Unindent();
                            sb.Write("} while (");
                            _constructingBinaryExpression = true;
                            _expressionCount = 0;
                            _endDoWhile = true;
                        }
                        break;
                    }
                case Opcode.Switch:
                    {
                        var varw = br.ReadByte();
                        if (AssemblyFormat)
                        {
                            _sb.WriteStandardOpcode("switch", varw);
                        }
                        else
                        {
                            sb.WriteLine($"switch ({GetVariableName(varw)})");
                            _sb.OpenBlock();
                        }
                        break;
                    }
                case Opcode.Case:
                    {
                        br.ReadByte();
                        br.ReadUInt16();
                        var value = br.ReadUInt16();
                        if (AssemblyFormat)
                        {
                            _sb.WriteStandardOpcode("case", value);
                        }
                        else
                        {
                            sb.Unindent();
                            sb.WriteLine($"case {value}:");
                            sb.Indent();
                        }
                        break;
                    }
                case Opcode.Default:
                    {
                        if (AssemblyFormat)
                        {
                            _sb.WriteStandardOpcode("default");
                        }
                        else
                        {
                            sb.Unindent();
                            sb.WriteLine($"default:");
                            sb.Indent();
                        }
                        break;
                    }
                case Opcode.Eswitch:
                    if (AssemblyFormat)
                    {
                        _sb.WriteStandardOpcode("eswitch");
                    }
                    else
                    {
                        sb.CloseBlock();
                    }
                    break;
                case Opcode.Goto:
                    {
                        var a = br.ReadByte();
                        var b = br.ReadByte();
                        var c = br.ReadByte();
                        var rel = br.ReadInt16();
                        var io = offset + rel;
                        if (AssemblyFormat)
                        {
                            _sb.WriteStandardOpcode("goto", sb.GetLabelName(io));
                        }
                        else
                        {
                            sb.WriteLine($"goto {sb.GetLabelName(io)};");
                            sb.InsertLabel(io);
                        }
                        break;
                    }
                case Opcode.Gosub:
                    {
                        var num = br.ReadByte();
                        if (AssemblyFormat)
                            sb.WriteStandardOpcode("gosub", $"0x{num:X2}");
                        else
                            sb.WriteLine($"sub_{num:X2}();");
                        break;
                    }
                case Opcode.Return:
                    if (AssemblyFormat)
                        sb.WriteStandardOpcode("return");
                    else
                        sb.WriteLine("return;");
                    break;
                case Opcode.Break:
                    {
                        if (AssemblyFormat)
                            sb.WriteStandardOpcode("break");
                        else
                            sb.WriteLine("break;");
                        break;
                    }
                case Opcode.WorkCopy:
                    {
                        var varw = br.ReadByte();
                        var dst = br.ReadByte();
                        var size = br.ReadByte();
                        sb.WriteStandardOpcode("work_copy", varw, dst, size);
                        break;
                    }
                case Opcode.Cmp:
                    {
                        br.ReadByte();
                        var index = br.ReadByte();
                        var op = br.ReadByte();
                        var value = br.ReadInt16();

                        if (AssemblyFormat)
                        {
                            sb.WriteStandardOpcode("cmp", index, op, value);
                        }
                        else
                        {
                            if (_constructingBinaryExpression)
                            {
                                if (_expressionCount != 0)
                                {
                                    sb.Write(" && ");
                                }

                                var ops = new[] { "==", ">", ">=", "<", "<=", "!=" };
                                var opS = ops.Length > op ? ops[op] : "?";
                                if (index == 27)
                                    sb.Write($"game.last_room {opS} 0x{value:X3}");
                                else
                                    sb.Write($"arr[{index}] {opS} {value}");
                                _expressionCount++;
                            }
                        }
                        break;
                    }
                case Opcode.Ck:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var value = br.ReadByte();
                        var bitString = GetBitsString(bitArray, number);

                        if (AssemblyFormat)
                        {
                            if (bitString.StartsWith("bits"))
                                sb.WriteStandardOpcode("ck", bitArray, number, value);
                            else
                                sb.WriteStandardOpcode("ck", bitString);
                        }
                        else
                        {
                            if (_constructingBinaryExpression)
                            {
                                if (_expressionCount != 0)
                                {
                                    sb.Write(" && ");
                                }
                                sb.Write($"{GetBitsString(bitArray, number)} == {value}");
                                _expressionCount++;
                            }
                        }
                        break;
                    }
                case Opcode.ObjModelSet:
                    {
                        var id = br.ReadByte();
                        sb.WriteStandardOpcode("obj_model_set", id, "...");
                        break;
                    }
                case Opcode.WorkSet:
                    {
                        var kind = br.ReadByte();
                        var id = br.ReadByte();
                        sb.WriteStandardOpcode("work_set", GetWorkKind(kind), id);
                        break;
                    }
                case Opcode.Set:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var opChg = br.ReadByte();
                        if (AssemblyFormat)
                        {
                            sb.WriteStandardOpcode("set", bitArray, number, opChg);
                        }
                        else
                        {
                            sb.Write(GetBitsString(bitArray, number));
                            if (opChg == 0)
                                sb.WriteLine(" = 0;");
                            else if (opChg == 1)
                                sb.WriteLine(" = 1;");
                            else if (opChg == 7)
                                sb.WriteLine(" ^= 1;");
                            else
                                sb.WriteLine(" (INVALID);");
                        }
                        break;
                    }
                case Opcode.Calc:
                    {
                        br.ReadByte();
                        var op = br.ReadByte();
                        var var = br.ReadByte();
                        var src = br.ReadInt16();

                        if (AssemblyFormat)
                        {
                            sb.WriteStandardOpcode("calc", op, var, src);
                        }
                        else
                        {
                            var ops = new string[] { "+", "-", "*", "/", "%", "|", "&", "^", "~", "<<", ">>", ">>>" };
                            var opS = ops.Length > op ? ops[op] : "?";
                            sb.WriteLine($"{GetVariableName(var)} {opS}= {src:X2};");
                        }
                        break;
                    }
                case Opcode.PosSet:
                    {
                        var x = br.ReadInt16();
                        var y = br.ReadInt16();
                        var z = br.ReadInt16();
                        sb.WriteStandardOpcode("pos_set", x, y, z);
                        break;
                    }
                case Opcode.ScaIdSet:
                    {
                        var entry = br.ReadByte();
                        var id = br.ReadUInt16();
                        sb.WriteStandardOpcode("sca_id_set", entry, id);
                        break;
                    }
                case Opcode.MemberSet:
                    {
                        var dst = br.ReadByte();
                        var src = br.ReadInt16();
                        sb.WriteStandardOpcode("member_set", dst, src);
                        break;
                    }
                case Opcode.MemberSet2:
                    {
                        var dst = br.ReadByte();
                        var src = br.ReadByte();
                        if (AssemblyFormat)
                            sb.WriteStandardOpcode("member_set2", dst, src);
                        else
                            sb.WriteStandardOpcode("member_set", dst, GetVariableName(src));
                        break;
                    }
                case Opcode.DirCk:
                    {
                        br.ReadByte();
                        var x = br.ReadInt16();
                        var y = br.ReadInt16();
                        var add = br.ReadInt16();

                        if (AssemblyFormat)
                        {
                            sb.WriteStandardOpcode("dir_ck", x, y, add);
                        }
                        else
                        {
                            if (_constructingBinaryExpression)
                            {
                                if (_expressionCount != 0)
                                {
                                    sb.Write(" && ");
                                }

                                sb.Write($"dir_ck({x}, {y}, {add})");
                                _expressionCount++;
                            }
                        }
                        break;
                    }
                case Opcode.MemberCopy:
                    {
                        var dst = br.ReadByte();
                        var src = br.ReadByte();
                        if (AssemblyFormat)
                            sb.WriteStandardOpcode("member_copy", dst, src);
                        else
                            sb.WriteStandardOpcode("member_copy", GetVariableName(dst), src);
                        break;
                    }
                case Opcode.MemberCmp:
                    {
                        br.ReadByte();
                        var flag = br.ReadByte();
                        var op = br.ReadByte();
                        var value = br.ReadInt16();

                        if (AssemblyFormat)
                        {
                            sb.WriteStandardOpcode("member_cmp", flag, op, value);
                        }
                        else
                        {
                            if (_constructingBinaryExpression)
                            {
                                if (_expressionCount != 0)
                                {
                                    sb.Write(" && ");
                                }

                                var ops = new[] { "==", ">", ">=", "<", "<=", "!=" };
                                var opS = ops.Length > op ? ops[op] : "?";
                                sb.Write($"&{flag} {opS} {value}");
                                _expressionCount++;
                            }
                        }
                        break;
                    }
                case Opcode.AotOn:
                    {
                        var id = br.ReadByte();
                        sb.WriteStandardOpcode("aot_on", id);
                        break;
                    }
                case Opcode.CutReplace:
                    {
                        var id = br.ReadByte();
                        var value = br.ReadByte();
                        sb.WriteStandardOpcode("cut_replace", id, value);
                        break;
                    }
                case Opcode.SceBgmControl:
                    {
                        var bgm = br.ReadByte();
                        var action = br.ReadByte();
                        var dummy = br.ReadByte();
                        var volume = br.ReadByte();
                        var channel = br.ReadByte();
                        sb.WriteStandardOpcode("sce_bgm_control", bgm, action, volume, channel);
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
                        sb.WriteStandardOpcode("sce_bgmtbl_set", stage, $"0x{roomId:X2}", $"0x{main:X2}", $"0x{sub:X2}");
                        break;
                    }
                case Opcode.XaOn:
                    {
                        var channel = br.ReadByte();
                        var id = br.ReadInt16();
                        sb.WriteStandardOpcode("xa_on", channel, id);
                        break;
                    }
                case Opcode.SceItemLost:
                    {
                        var item = br.ReadByte();
                        sb.WriteStandardOpcode("sce_item_lost", GetItemConstant(item));
                        break;
                    }
            }
        }

        private static string GetSCE(byte sce)
        {
            if (sce >= g_sceNames.Length)
                return "SCE_NULL";
            return g_sceNames[sce];
        }

        private static string GetSAT(byte sat)
        {
            if (sat == 0)
                return "SAT_AUTO";

            var s = "";
            if ((sat & (1 << 0)) != 0)
                s += "SAT_PL | ";
            if ((sat & (1 << 1)) != 0)
                s += "SAT_EM | ";
            if ((sat & (1 << 2)) != 0)
                s += "SAT_SPL | ";
            if ((sat & (1 << 3)) != 0)
                s += "SAT_OB | ";
            if ((sat & (1 << 4)) != 0)
                s += "SAT_MANUAL | ";
            if ((sat & (1 << 5)) != 0)
                s += "SAT_FRONT | ";
            if ((sat & (1 << 6)) != 0)
                s += "SAT_UNDER | ";
            if ((sat & (1 << 7)) != 0)
                s += "0x80 | ";
            return s.TrimEnd(' ', '|');
        }

        private static string GetWorkKind(byte kind)
        {
            if (kind < g_workKinds.Length)
                return g_workKinds[kind];
            else if (kind == 0x80)
                return "WK_PL_PARTS";
            else if (kind == 0xA0)
                return "WK_SPL_PARTS";
            else if (kind == 0xC0)
                return "WK_EM_PARTS";
            else if (kind == 0xE0)
                return "WK_OM_PARTS";
            else
                return "WK_NULL";
        }

        private static string GetEnemyConstant(EnemyType type)
        {
            return $"ENEMY_{type.ToString().ToUpperInvariant()}";
        }

        private static string GetItemConstant(ushort item)
        {
            return $"ITEM_{ ((ItemType)item).ToString().ToUpperInvariant()}";
        }

        private static string GetVariableName(int id)
        {
            return $"var_{id:X2}";
        }

        private static string GetBitsString(int bitArray, int number)
        {
            if (bitArray == 0 && number == 0x19)
                return "game.difficult";

            if (bitArray == 1 && number == 0)
                return "game.player";
            if (bitArray == 1 && number == 1)
                return "game.scenario";
            if (bitArray == 1 && number == 6)
                return "game.bonus";
            if (bitArray == 1 && number == 0x1B)
                return "game.cutscene";
            if (bitArray == 0xB && number == 0x1F)
                return "input.question";

            return $"bits[{bitArray}][{number}]";
        }
    }
}
