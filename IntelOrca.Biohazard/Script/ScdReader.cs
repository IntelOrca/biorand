using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard.Script
{
    public class ScdReader
    {
        private IConstantTable _constantTable = new Bio1ConstantTable();

        public int BaseOffset { get; set; }

        public string Diassemble(ReadOnlyMemory<byte> data, BioVersion version, BioScriptKind kind, bool listing = false)
        {
            var decompiler = new ScriptDecompiler(true, listing);
            decompiler.VisitVersion(version);
            ReadScript(data, version, kind, decompiler);
            return decompiler.GetScript();
        }

        internal void ReadScript(ReadOnlyMemory<byte> data, BioVersion version, BioScriptKind kind, BioScriptVisitor visitor)
        {
            ReadScript(new SpanStream(data), data.Length, version, kind, visitor);
        }

        internal void ReadScript(Stream stream, int length, BioVersion version, BioScriptKind kind, BioScriptVisitor visitor)
        {
            var br = new BinaryReader(stream);
            if (version == BioVersion.Biohazard1)
                ReadScript1(br, length, kind, visitor);
            else
                ReadScript2(br, length, kind, visitor);
        }

        private void ReadScript1(BinaryReader br, int length, BioScriptKind kind, BioScriptVisitor visitor)
        {
            var scriptEnd = kind == BioScriptKind.Event ? length : br.ReadUInt16();

            visitor.VisitBeginScript(kind);
            visitor.VisitBeginSubroutine(0);
            try
            {
                while (br.BaseStream.Position < scriptEnd)
                {
                    var instructionPosition = (int)br.BaseStream.Position;
                    var opcode = br.ReadByte();
                    var instructionSize = _constantTable.GetInstructionSize(opcode, br);
                    if (instructionSize == 0)
                        break;

                    br.BaseStream.Position = instructionPosition + 1;
                    var bytes = new byte[instructionSize];
                    bytes[0] = opcode;
                    if (br.Read(bytes, 1, instructionSize - 1) != instructionSize - 1)
                        break;

                    visitor.VisitOpcode(BaseOffset + instructionPosition, new Span<byte>(bytes));
                }
            }
            catch (Exception)
            {
            }
            visitor.VisitEndSubroutine(0);
            visitor.VisitEndScript(kind);
        }

        private void ReadScript2(BinaryReader br, int length, BioScriptKind kind, BioScriptVisitor visitor)
        {
            visitor.VisitBeginScript(kind);

            var constantTable = new Bio2ConstantTable();
            var start = (int)br.BaseStream.Position;
            var functionOffsets = new List<int>();
            var firstFunctionOffset = br.ReadUInt16();
            functionOffsets.Add(start + firstFunctionOffset);
            var numFunctions = firstFunctionOffset / 2;
            for (int i = 1; i < numFunctions; i++)
            {
                functionOffsets.Add(start + br.ReadUInt16());
            }
            functionOffsets.Add(start + length);
            for (int i = 0; i < numFunctions; i++)
            {
                visitor.VisitBeginSubroutine(i);

                var functionOffset = functionOffsets[i];
                var functionEnd = functionOffsets[i + 1];
                var functionEndMin = functionOffset;
                var ifStack = 0;
                var isEnd = false;
                br.BaseStream.Position = functionOffset;
                while (br.BaseStream.Position < functionEnd)
                {
                    var instructionPosition = (int)br.BaseStream.Position;
                    var remainingSize = functionEnd - instructionPosition;
                    if (isEnd)
                    {
                        visitor.VisitTrailingData(BaseOffset + instructionPosition, br.ReadBytes(remainingSize));
                        break;
                    }

                    var opcode = br.ReadByte();
                    var instructionSize = constantTable.GetInstructionSize(opcode, br);
                    if (instructionSize == 0 || instructionSize > remainingSize)
                    {
                        instructionSize = Math.Min(16, remainingSize);
                    }

                    var opcodeBytes = new byte[instructionSize];
                    opcodeBytes[0] = opcode;
                    if (br.Read(opcodeBytes, 1, instructionSize - 1) != instructionSize - 1)
                    {
                        throw new Exception("Unable to read opcode");
                    }

                    visitor.VisitOpcode(BaseOffset + instructionPosition, opcodeBytes);

                    if (i == numFunctions - 1)
                    {
                        switch ((OpcodeV2)opcode)
                        {
                            case OpcodeV2.EvtEnd:
                                if (instructionPosition >= functionEndMin && ifStack == 0)
                                    isEnd = true;
                                break;
                            case OpcodeV2.IfelCk:
                                functionEndMin = instructionPosition + BitConverter.ToUInt16(opcodeBytes, 2);
                                ifStack++;
                                break;
                            case OpcodeV2.ElseCk:
                                ifStack--;
                                functionEndMin = instructionPosition + BitConverter.ToUInt16(opcodeBytes, 2);
                                break;
                            case OpcodeV2.EndIf:
                                ifStack--;
                                break;
                        }
                    }
                }

                visitor.VisitEndSubroutine(i);
            }

            visitor.VisitEndScript(kind);
        }
    }
}
