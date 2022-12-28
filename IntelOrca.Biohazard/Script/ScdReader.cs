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
                br.BaseStream.Position = functionOffset;
                while (br.BaseStream.Position < functionEnd)
                {
                    var instructionPosition = (int)br.BaseStream.Position;
                    var opcode = br.ReadByte();
                    if (i == numFunctions - 1 && opcode >= _instructionSizes2.Length)
                    {
                        break;
                    }
                    var instructionSize = _instructionSizes2[opcode];

                    var opcodeBytes = new byte[instructionSize];
                    opcodeBytes[0] = opcode;
                    if (br.Read(opcodeBytes, 1, instructionSize - 1) != instructionSize - 1)
                        throw new Exception("Unable to read opcode");

                    visitor.VisitOpcode(BaseOffset + instructionPosition, opcodeBytes);

                    if (i == numFunctions - 1)
                    {
                        var isEnd = false;
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
                        if (isEnd)
                            break;
                    }
                }

                visitor.VisitEndSubroutine(i);
            }

            visitor.VisitEndScript(kind);
        }

        private static int[] _instructionSizes1 = new int[]
        {
            2, 2, 2, 2, 4, 4, 4, 6, 4, 2, 2, 4, 26, 18, 2, 8,
            2, 2, 10, 4, 4, 2, 2, 10, 26, 4, 2, 22, 6, 2, 4, 28,
            14, 14, 4, 2, 4, 4, 0, 2, 4 + 0, 2, 12, 4, 2, 4, 0, 4,
            12, 4, 4, 4 + 0, 8, 4, 4, 4, 4, 2, 4, 6, 6, 12, 2, 6,
            16, 4, 4, 4, 2, 2, 44 + 0, 14, 2, 2, 2, 2, 4, 2, 4, 2,
            2
        };

        private static int[] _instructionSizes2 = new int[]
        {
            1, 2, 1, 4, 4, 2, 4, 4, 1, 4, 3, 1, 1, 6, 2, 4,
            2, 4, 2, 4, 6, 2, 2, 6, 2, 2, 2, 6, 1, 4, 1, 1,
            1, 4, 4, 6, 4, 3, 6, 4, 1, 2, 1, 6, 20, 38, 3, 4,
            1, 1, 8, 8, 4, 3, 12, 4, 3, 8, 16, 32, 2, 3, 6, 4,
            8, 10, 1, 4, 22, 5, 10, 2, 16, 8, 2, 3, 5, 22, 22, 4,
            4, 6, 6, 6, 22, 6, 4, 8, 4, 4, 2, 2, 3, 2, 2, 2,
            14, 4, 2, 1, 16, 2, 1, 28, 40, 30, 6, 4, 1, 4, 6, 2,
            1, 1, 16, 8, 4, 22, 3, 4, 6, 1, 16, 16, 6, 6, 6, 6,
            2, 3, 3, 1, 2, 6, 1, 1, 3, 1, 6, 6, 8, 24, 24
        };
    }
}
