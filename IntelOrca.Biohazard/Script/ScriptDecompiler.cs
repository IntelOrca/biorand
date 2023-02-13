using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IntelOrca.Biohazard.Script
{
    internal class ScriptDecompiler : BioScriptVisitor
    {
        private ScriptBuilder _sb = new ScriptBuilder();
        private Stack<(byte, int)> _blockEnds = new Stack<(byte, int)>();
        private bool _endDoWhile;
        private bool _constructingBinaryExpression;
        private int _expressionCount;
        private IConstantTable _constantTable = new Bio1ConstantTable();
        private BioScriptKind _kind;

        public bool AssemblyFormat => _sb.AssemblyFormat;

        public ScriptDecompiler(bool assemblyFormat, bool listingFormat)
        {
            _sb.AssemblyFormat = assemblyFormat;
            _sb.ListingFormat = listingFormat;
        }

        public string GetScript()
        {
            return _sb.ToString();
        }

        public override void VisitVersion(BioVersion version)
        {
            base.VisitVersion(version);

            int versionNumber;
            switch (version)
            {
                case BioVersion.Biohazard1:
                    _constantTable = new Bio1ConstantTable();
                    versionNumber = 1;
                    break;
                case BioVersion.Biohazard2:
                    _constantTable = new Bio2ConstantTable();
                    versionNumber = 2;
                    break;
                case BioVersion.Biohazard3:
                    _constantTable = new Bio3ConstantTable();
                    versionNumber = 3;
                    break;
                default:
                    throw new NotSupportedException();
            }
            _sb.WriteLine(".version " + versionNumber);
        }

        public override void VisitBeginScript(BioScriptKind kind)
        {
            switch (kind)
            {
                case BioScriptKind.Init:
                    _kind = BioScriptKind.Init;
                    if (AssemblyFormat)
                    {
                        _sb.WriteLine(".init");
                    }
                    else
                    {
                        _sb.WriteLine("init");
                        _sb.OpenBlock();
                    }
                    break;
                case BioScriptKind.Main:
                    _kind = BioScriptKind.Main;
                    _sb.WriteLine();
                    if (AssemblyFormat)
                    {
                        _sb.WriteLine(".main");
                    }
                    else
                    {
                        _sb.WriteLine("main");
                        _sb.OpenBlock();
                    }
                    break;
                case BioScriptKind.Event:
                    _kind = BioScriptKind.Event;
                    _sb.WriteLine();
                    if (AssemblyFormat)
                    {
                        _sb.WriteLine(".event");
                    }
                    else
                    {
                        _sb.WriteLine("event");
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
                if (Version != BioVersion.Biohazard1)
                    _sb.WriteLine($".proc {GetProcedureName(index)}");
            }
            else
            {
                _sb.ResetIndent();

                _sb.Indent();
                _sb.WriteLine($"{GetProcedureName(index)}()");
                _sb.OpenBlock();

                _blockEnds.Clear();
            }
        }

        private string GetProcedureName(int index)
        {
            if (_kind == BioScriptKind.Init)
                return $"init_{index:X2}";
            else
                return $"main_{index:X2}";
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

        public override void VisitOpcode(int offset, Span<byte> opcodeSpan)
        {
            _sb.RecordOpcode(offset, opcodeSpan);

            var opcode = opcodeSpan[0];
            if (_constructingBinaryExpression)
            {
                if (!_constantTable.IsOpcodeCondition(opcode))
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

            var opcodeBytes = opcodeSpan.ToArray();
            var br = new BinaryReader(new MemoryStream(opcodeBytes));
            var backupPosition = br.BaseStream.Position;
            if (!AssemblyFormat)
            {
                switch (Version)
                {
                    case BioVersion.Biohazard1:
                        if (VisitOpcode(offset, (OpcodeV1)opcode, br))
                            return;
                        break;
                    case BioVersion.Biohazard2:
                        if (VisitOpcode(offset, (OpcodeV2)opcode, br))
                            return;
                        break;
                    case BioVersion.Biohazard3:
                        if (VisitOpcode(offset, (OpcodeV3)opcode, br))
                            return;
                        break;
                }
                br.BaseStream.Position = backupPosition;
            }
            DiassembleGeneralOpcode(br, offset, opcode, opcodeBytes.Length);
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

        private bool VisitOpcode(int offset, OpcodeV1 opcode, BinaryReader br)
        {
            var sb = _sb;
            br.ReadByte();
            switch (opcode)
            {
                default:
                    return false;
                case OpcodeV1.Nop:
                    break;
                case OpcodeV1.IfelCk:
                    {
                        sb.Write("if (");
                        _constructingBinaryExpression = true;
                        _expressionCount = 0;
                        break;
                    }
                case OpcodeV1.ElseCk:
                    {
                        var blockLen = br.ReadByte();
                        _sb.CloseBlock();
                        _blockEnds.Push(((byte)opcode, offset + blockLen));
                        sb.WriteLine("else");
                        _sb.OpenBlock();
                    }
                    break;
                case OpcodeV1.EndIf:
                    _sb.CloseBlock();
                    break;
                case OpcodeV1.Ck:
                    {
                        var obj = br.ReadByte();
                        var temp = br.ReadByte();
                        var bitArray = temp >> 5;
                        var number = temp & 0b11111;
                        var value = br.ReadByte();
                        if (_constructingBinaryExpression)
                        {
                            if (_expressionCount != 0)
                            {
                                sb.Write(" && ");
                            }
                            sb.Write($"{GetBitsString(obj, bitArray, number)} == {value}");
                            _expressionCount++;
                        }
                        break;
                    }
                case OpcodeV1.Set:
                    {
                        var obj = br.ReadByte();
                        var temp = br.ReadByte();
                        var bitArray = temp >> 5;
                        var number = temp & 0b11111;
                        var opChg = br.ReadByte();
                        sb.Write(GetBitsString(obj, bitArray, number));
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
                case OpcodeV1.Cmp6:
                case OpcodeV1.Cmp7:
                    {
                        var index = br.ReadByte();
                        var op = br.ReadByte();
                        var value = opcode == OpcodeV1.Cmp6 ? br.ReadByte() : br.ReadInt16();
                        if (_constructingBinaryExpression)
                        {
                            if (_expressionCount != 0)
                            {
                                sb.Write(" && ");
                            }

                            var ops = new[] { "==", "<", "<=", ">", ">=", "!=" };
                            var opS = ops.Length > op ? ops[op] : "?";
                            sb.Write($"arr[{index}] {opS} {value}");
                            _expressionCount++;
                        }
                        break;
                    }
                case OpcodeV1.Set8:
                    {
                        var src = br.ReadByte();
                        var value = br.ReadByte();
                        sb.WriteLine($"$${src} = {value};");
                        break;
                    }
            }
            return true;
        }

        private bool VisitOpcode(int offset, OpcodeV2 opcode, BinaryReader br)
        {
            var sb = _sb;
            br.ReadByte();
            switch (opcode)
            {
                default:
                    return false;
                case OpcodeV2.Nop:
                case OpcodeV2.Nop20:
                    break;
                case OpcodeV2.EvtEnd:
                    {
                        var ret = br.ReadByte();
                        sb.WriteLine($"return {ret};");
                        break;
                    }
                case OpcodeV2.IfelCk:
                    {
                        sb.Write("if (");
                        _constructingBinaryExpression = true;
                        _expressionCount = 0;
                        break;
                    }
                case OpcodeV2.ElseCk:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        _sb.CloseBlock();
                        _blockEnds.Push(((byte)opcode, offset + blockLen));
                        sb.WriteLine("else");
                        _sb.OpenBlock();
                    }
                    break;
                case OpcodeV2.EndIf:
                    _sb.CloseBlock();
                    break;
                case OpcodeV2.For:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        var count = br.ReadUInt16();
                        sb.WriteLine($"for {count} times");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV2.Next:
                    sb.CloseBlock();
                    break;
                case OpcodeV2.While:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        sb.WriteLine($"while (");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV2.Ewhile:
                    sb.CloseBlock();
                    break;
                case OpcodeV2.Do:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        _blockEnds.Push(((byte)opcode, offset + blockLen));
                        sb.WriteLine($"do");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV2.Edwhile:
                    {
                        sb.Unindent();
                        sb.Write("} while (");
                        _constructingBinaryExpression = true;
                        _expressionCount = 0;
                        _endDoWhile = true;
                        break;
                    }
                case OpcodeV2.Switch:
                    {
                        var varw = br.ReadByte();
                        sb.WriteLine($"switch ({GetVariableName(varw)})");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV2.Case:
                    {
                        br.ReadByte();
                        br.ReadUInt16();
                        var value = br.ReadUInt16();
                        sb.Unindent();
                        sb.WriteLine($"case {value}:");
                        sb.Indent();
                        break;
                    }
                case OpcodeV2.Default:
                    sb.Unindent();
                    sb.WriteLine($"default:");
                    sb.Indent();
                    break;
                case OpcodeV2.Eswitch:
                    sb.CloseBlock();
                    break;
                case OpcodeV2.Goto:
                    var a = br.ReadByte();
                    var b = br.ReadByte();
                    var c = br.ReadByte();
                    var rel = br.ReadInt16();
                    var io = offset + rel;
                    sb.WriteLine($"goto {sb.GetLabelName(io)};");
                    sb.InsertLabel(io);
                    break;
                case OpcodeV2.Gosub:
                    var num = br.ReadByte();
                    sb.WriteLine($"{GetProcedureName(num)}();");
                    break;
                case OpcodeV2.Return:
                    sb.WriteLine("return;");
                    break;
                case OpcodeV2.Break:
                    sb.WriteLine("break;");
                    break;
                case OpcodeV2.Cmp:
                    {
                        br.ReadByte();
                        var index = br.ReadByte();
                        var op = br.ReadByte();
                        var value = br.ReadInt16();
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
                        break;
                    }
                case OpcodeV2.Ck:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var value = br.ReadByte();
                        var bitString = GetBitsString(bitArray, number);
                        if (_constructingBinaryExpression)
                        {
                            if (_expressionCount != 0)
                            {
                                sb.Write(" && ");
                            }
                            sb.Write($"{GetBitsString(bitArray, number)} == {value}");
                            _expressionCount++;
                        }
                        break;
                    }
                case OpcodeV2.Set:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var opChg = br.ReadByte();
                        sb.Write(GetBitsString(bitArray, number));
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
                case OpcodeV2.Calc:
                    {
                        br.ReadByte();
                        var op = br.ReadByte();
                        var var = br.ReadByte();
                        var src = br.ReadInt16();
                        var ops = new string[] { "+", "-", "*", "/", "%", "|", "&", "^", "~", "<<", ">>", ">>>" };
                        var opS = ops.Length > op ? ops[op] : "?";
                        sb.WriteLine($"{GetVariableName(var)} {opS}= {src:X2};");
                        break;
                    }
                case OpcodeV2.DirCk:
                    {
                        br.ReadByte();
                        var x = br.ReadInt16();
                        var y = br.ReadInt16();
                        var add = br.ReadInt16();
                        if (_constructingBinaryExpression)
                        {
                            if (_expressionCount != 0)
                            {
                                sb.Write(" && ");
                            }

                            sb.Write($"dir_ck({x}, {y}, {add})");
                            _expressionCount++;
                        }
                        break;
                    }
                case OpcodeV2.MemberCmp:
                    {
                        br.ReadByte();
                        var flag = br.ReadByte();
                        var op = br.ReadByte();
                        var value = br.ReadInt16();
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
                        break;
                    }
            }
            return true;
        }

        private bool VisitOpcode(int offset, OpcodeV3 opcode, BinaryReader br)
        {
            var sb = _sb;
            br.ReadByte();
            switch (opcode)
            {
                default:
                    return false;
                case OpcodeV3.Nop:
                    break;
                case OpcodeV3.EvtEnd:
                    {
                        var ret = br.ReadByte();
                        sb.WriteLine($"return {ret};");
                        break;
                    }
                case OpcodeV3.IfelCk:
                    {
                        sb.Write("if (");
                        _constructingBinaryExpression = true;
                        _expressionCount = 0;
                        break;
                    }
                case OpcodeV3.ElseCk:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        _sb.CloseBlock();
                        _blockEnds.Push(((byte)opcode, offset + blockLen));
                        sb.WriteLine("else");
                        _sb.OpenBlock();
                    }
                    break;
                case OpcodeV3.EndIf:
                    _sb.CloseBlock();
                    break;
                case OpcodeV3.For:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        var count = br.ReadUInt16();
                        sb.WriteLine($"for {count} times");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV3.EndFor:
                    sb.CloseBlock();
                    break;
                case OpcodeV3.While:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        sb.WriteLine($"while (");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV3.Ewhile:
                    sb.CloseBlock();
                    break;
                case OpcodeV3.Do:
                    {
                        br.ReadByte();
                        var blockLen = br.ReadUInt16();
                        _blockEnds.Push(((byte)opcode, offset + blockLen));
                        sb.WriteLine($"do");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV3.Edwhile:
                    {
                        sb.Unindent();
                        sb.Write("} while (");
                        _constructingBinaryExpression = true;
                        _expressionCount = 0;
                        _endDoWhile = true;
                        break;
                    }
                case OpcodeV3.Switch:
                    {
                        var varw = br.ReadByte();
                        sb.WriteLine($"switch ({GetVariableName(varw)})");
                        _sb.OpenBlock();
                        break;
                    }
                case OpcodeV3.Case:
                    {
                        br.ReadByte();
                        sb.Unindent();
                        sb.WriteLine($"case when (");
                        sb.Indent();
                        _constructingBinaryExpression = true;
                        _expressionCount = 0;
                        break;
                    }
                case OpcodeV3.Default:
                    sb.Unindent();
                    sb.WriteLine($"default:");
                    sb.Indent();
                    break;
                case OpcodeV3.Eswitch:
                    sb.CloseBlock();
                    break;
                case OpcodeV3.Goto:
                    var a = br.ReadByte();
                    var b = br.ReadByte();
                    var c = br.ReadByte();
                    var rel = br.ReadInt16();
                    var io = offset + rel;
                    sb.WriteLine($"goto {sb.GetLabelName(io)};");
                    sb.InsertLabel(io);
                    break;
                case OpcodeV3.Gosub:
                    var num = br.ReadByte();
                    sb.WriteLine($"{GetProcedureName(num)}();");
                    break;
                case OpcodeV3.Return:
                    sb.WriteLine("return;");
                    break;
                case OpcodeV3.Break:
                    sb.WriteLine("break;");
                    break;
                case OpcodeV3.Cmp:
                    {
                        br.ReadByte();
                        var index = br.ReadByte();
                        var op = br.ReadByte();
                        var value = br.ReadInt16();
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
                        break;
                    }
                case OpcodeV3.Ck:
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
                            sb.Write($"{GetBitsString(bitArray, number)} == {value}");
                            _expressionCount++;
                        }
                        break;
                    }
                case OpcodeV3.Set3:
                    {
                        var bitArray = br.ReadByte();
                        var number = br.ReadByte();
                        var opChg = br.ReadByte();
                        sb.Write(GetBitsString(bitArray, number));
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
                case OpcodeV3.Calc:
                    {
                        br.ReadByte();
                        var op = br.ReadByte();
                        var var = br.ReadByte();
                        var src = br.ReadInt16();
                        var ops = new string[] { "+", "-", "*", "/", "%", "|", "&", "^", "~", "<<", ">>", ">>>" };
                        var opS = ops.Length > op ? ops[op] : "?";
                        sb.WriteLine($"{GetVariableName(var)} {opS}= {src:X2};");
                        break;
                    }
            }
            return true;
        }

        private void DiassembleGeneralOpcode(BinaryReader br, int offset, byte opcode, int instructionLength)
        {
            var parameters = new List<object>();
            string opcodeName;

            var originalStreamPosition = br.BaseStream.Position;

            var opcodeRaw = br.ReadByte();
            Debug.Assert(opcodeRaw == opcode);

            br.BaseStream.Position = originalStreamPosition + 1;

            var signature = _constantTable.GetOpcodeSignature(opcode);
            var expectedLength = _constantTable.GetInstructionSize(opcode, br);
            if (expectedLength == 0 || expectedLength != instructionLength)
            {
                signature = "";
            }

            var colonIndex = signature.IndexOf(':');
            if (colonIndex == -1)
            {
                opcodeName = signature;
                if (opcodeName == "")
                {
                    opcodeName = "unk";
                    parameters.Add(opcode);
                }
                foreach (var b in br.ReadBytes(instructionLength))
                {
                    parameters.Add(b);
                }
            }
            else
            {
                opcodeName = signature.Substring(0, colonIndex);
                var pIndex = 0;
                for (int i = colonIndex + 1; i < signature.Length; i++)
                {
                    var c = signature[i];
                    string? szv;
                    using (var br2 = br.Fork())
                    {
                        br.BaseStream.Position = originalStreamPosition + 1;
                        szv = _constantTable.GetConstant(opcode, pIndex, br);
                    }
                    if (szv != null)
                    {
                        br.BaseStream.Position++;
                        if (c == 'L' || c == '~' || c == 'U' || c == 'I')
                            br.BaseStream.Position++;
                        parameters.Add(szv);
                    }
                    else
                    {
                        switch (c)
                        {
                            case 'l':
                                {
                                    var blockLen = br.ReadByte();
                                    var labelOffset = offset + instructionLength + blockLen;
                                    _sb.InsertLabel(labelOffset);
                                    parameters.Add(_sb.GetLabelName(labelOffset));
                                    break;
                                }
                            case '\'':
                                {
                                    var blockLen = br.ReadByte();
                                    var labelOffset = offset + instructionLength + blockLen;
                                    _sb.InsertLabel(labelOffset);
                                    parameters.Add(_sb.GetLabelName(labelOffset));
                                    break;
                                }
                            case 'L':
                            case '~':
                                {
                                    var blockLen = c == '~' ? (int)br.ReadInt16() : (int)br.ReadUInt16();
                                    var labelOffset = offset + instructionLength + blockLen;
                                    if (c == '~')
                                        labelOffset -= 2;
                                    _sb.InsertLabel(labelOffset);
                                    parameters.Add(_sb.GetLabelName(labelOffset));
                                    break;
                                }
                            case '@':
                                {
                                    var blockLen = br.ReadInt16();
                                    var labelOffset = offset + blockLen;
                                    _sb.InsertLabel(labelOffset);
                                    parameters.Add(_sb.GetLabelName(labelOffset));
                                    break;
                                }
                            case 'b':
                                {
                                    var temp = br.ReadByte();
                                    var bitArray = temp >> 5;
                                    var number = temp & 0b11111;
                                    parameters.Add($"{bitArray << 5} | {number}");
                                    break;
                                }
                            case 'u':
                                parameters.Add(br.ReadByte());
                                break;
                            case 'U':
                                parameters.Add(br.ReadUInt16());
                                break;
                            case 'I':
                                parameters.Add(br.ReadInt16());
                                break;
                            case 'r':
                                {
                                    var target = br.ReadByte();
                                    var stage = (byte)(target >> 5);
                                    var room = (byte)(target & 0b11111);
                                    parameters.Add($"RDT_{stage:X}{room:X2}");
                                    break;
                                }
                            default:
                                {
                                    var v = char.IsUpper(c) ? br.ReadUInt16() : br.ReadByte();
                                    szv = _constantTable.GetConstant(c, v);
                                    parameters.Add(szv ?? (object)v);
                                    break;
                                }
                        }
                    }
                    pIndex++;
                }
            }
            if (!AssemblyFormat && _constructingBinaryExpression)
            {
                _sb.WriteStandardExpression(opcodeName, parameters.ToArray());
            }
            else
            {
                _sb.WriteStandardOpcode(opcodeName, parameters.ToArray());
            }

            var streamPosition = br.BaseStream.Position;
            if (streamPosition != originalStreamPosition + instructionLength)
                throw new Exception($"Opcode {opcode} not diassembled correctly.");
        }

        public override void VisitTrailingData(int offset, Span<byte> data)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i += 16)
            {
                var slice = data.Slice(i, Math.Min(data.Length - i, 16));

                sb.Clear();
                for (int j = 0; j < slice.Length; j++)
                {
                    sb.AppendFormat("0x{0:X2}, ", slice[j]);
                }
                sb.Remove(sb.Length - 2, 2);
                _sb.CurrentOffset = offset + i;
                _sb.CurrentOpcodeBytes = slice.ToArray();
                _sb.WriteStandardOpcode("db", sb.ToString());
            }
        }

        private static string GetVariableName(int id)
        {
            return $"var_{id:X2}";
        }

        private static string GetBitsString(int obj, int bitArray, int number)
        {
            return $"${obj}[{bitArray}][{number}]";
        }

        private string GetBitsString(int bitArray, int number)
        {
            var name = _constantTable.GetNamedFlag(bitArray, number);
            if (name != null)
                return name;
            return $"bits[{bitArray}][{number}]";
        }
    }
}
