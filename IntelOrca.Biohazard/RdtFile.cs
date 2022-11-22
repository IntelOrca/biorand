using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal class RdtFile
    {
        private readonly int[] _offsets;

        public byte[] Data { get; }
        public ulong Checksum { get; }

        public RdtFile(string path)
        {
            Data = File.ReadAllBytes(path);
            _offsets = ReadHeader();
            Checksum = Data.CalculateFnv1a();
        }

        public MemoryStream GetStream()
        {
            return new MemoryStream(Data);
        }

        private int[] ReadHeader()
        {
            if (Data.Length <= 8)
                return new int[0];

            var br = new BinaryReader(new MemoryStream(Data));
            br.ReadBytes(8);

            var offsets = new int[23];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = br.ReadInt32();
            }
            return offsets;
        }

        public void ReadScript(BioScriptVisitor visitor)
        {
            ReadScript(BioScriptKind.Init, visitor, 16);
            ReadScript(BioScriptKind.Main, visitor, 17);
        }

        private void ReadScript(BioScriptKind kind, BioScriptVisitor visitor, int offsetIndex)
        {
            var scriptOffset = _offsets[offsetIndex];
            if (scriptOffset == 0)
                return;

            var stream = new MemoryStream(Data);
            stream.Position = scriptOffset;
            var len = _offsets[offsetIndex + 1] - scriptOffset;
            ReadScript(kind, visitor, new BinaryReader(stream), len);
        }

        private static void ReadScript(BioScriptKind kind, BioScriptVisitor visitor, BinaryReader br, int length)
        {
            var stream = br.BaseStream;

            visitor.VisitBeginScript(kind);

            var start = (int)stream.Position;
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
                stream.Position = functionOffset;
                while (stream.Position < functionEnd)
                {
                    var instructionPosition = (int)stream.Position;
                    var opcode = (Opcode)br.ReadByte();
                    if (i == numFunctions - 1 && (byte)opcode >= _instructionSizes.Length)
                    {
                        break;
                    }
                    var instructionSize = _instructionSizes[(byte)opcode];
                    var operands = br.ReadBytes(instructionSize - 1);
                    visitor.VisitOpcode(instructionPosition, opcode, new Span<byte>(operands));

                    if (i == numFunctions - 1)
                    {
                        var isEnd = false;
                        switch (opcode)
                        {
                            case Opcode.EvtEnd:
                                if (instructionPosition >= functionEndMin && ifStack == 0)
                                    isEnd = true;
                                break;
                            case Opcode.IfelCk:
                                functionEndMin = instructionPosition + BitConverter.ToUInt16(operands, 1);
                                ifStack++;
                                break;
                            case Opcode.ElseCk:
                                ifStack--;
                                functionEndMin = instructionPosition + BitConverter.ToUInt16(operands, 1);
                                break;
                            case Opcode.EndIf:
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

        private static int[] _instructionSizes = new int[]
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
