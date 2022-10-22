using System;
using System.IO;

namespace rer
{
    internal class ScriptDecompiler : BioScriptVisitor
    {
        private ScriptBuilder _sb = new ScriptBuilder();
        private bool _expectingEndIf;

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
        }

        public override void VisitEndSubroutine(int index)
        {
            _sb.ResetIndent();
            _sb.Indent();
            _sb.WriteLine("}");
        }

        public override void VisitOpcode(int offset, Opcode opcode, Span<byte> operands)
        {
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
            _sb.WriteLine($"door_aot_se({door.Id}, 0x{door.Stage:X}, 0x{door.Room:X2}, {door.DoorFlag}, {door.DoorLockFlag}, {door.DoorKey});");
        }

        protected override void VisitAotReset(Reset reset)
        {
            _sb.WriteLine($"aot_reset({reset.Id}, 0x{reset.Type:X2}, 0x{reset.Amount:X2}, 0x{reset.Unk8:X2});");
        }

        protected override void VisitSceEmSet(RdtEnemy enemy)
        {
            _sb.WriteLine($"sce_em_set({enemy.Id}, {enemy.Type}, {enemy.State}, {enemy.Ai}, {enemy.Floor}, {enemy.SoundBank}, {enemy.Texture}, {enemy.KillId}, ..., {enemy.Animation});");
        }

        protected override void VisitItemAotSet(Item item)
        {
            _sb.WriteLine($"item_aot_set({item.Id}, 0x{item.Type:X2}, 0x{item.Amount:X2});");
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
                case Opcode.IfelCk:
                    sb.Write($"if (");
                    sb.Indent();
                    break;
                case Opcode.ElseCk:
                    if (_expectingEndIf)
                    {
                        sb.Unindent();
                        sb.WriteLine($"end-if");
                    }
                    sb.Unindent();
                    sb.WriteLine($"else");
                    sb.Indent();
                    _expectingEndIf = true;
                    break;
                case Opcode.EndIf:
                    sb.Unindent();
                    sb.WriteLine($"endif");
                    _expectingEndIf = false;
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
                        sb.Indent();
                        break;
                    }
                case Opcode.Next:
                    {
                        sb.Unindent();
                        sb.WriteLine($"next");
                        break;
                    }
                case Opcode.While:
                    {
                        sb.WriteLine($"while (");
                        sb.Indent();
                        break;
                    }
                case Opcode.Ewhile:
                    {
                        sb.Unindent();
                        sb.WriteLine($"next");
                        break;
                    }
                case Opcode.Do:
                    {
                        sb.WriteLine($"do");
                        sb.Indent();
                        break;
                    }
                case Opcode.Edwhile:
                    {
                        sb.Unindent();
                        sb.WriteLine($"while (");
                        break;
                    }
                case Opcode.Switch:
                    {
                        var varw = br.ReadByte();
                        sb.WriteLine($"switch (var_{varw:X2})");
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
                        sb.WriteLine($"end-switch");
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
                case Opcode.Ck:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var value = br.ReadByte();
                        if (value == 0)
                            sb.Write($"!");
                        sb.WriteLine($"bits[{bitArray}][{number}])");
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
                        sb.WriteLine($"sce_item_lost(0x{item:X});");
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
